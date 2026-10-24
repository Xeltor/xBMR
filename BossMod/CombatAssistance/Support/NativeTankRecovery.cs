using BossMod.CombatAssistance.Config;
using BossMod.CombatAssistance.Integrations;
using Dalamud.Game.ClientState.Objects.Types;
using System.Reflection;

namespace BossMod.CombatAssistance.Support;

internal sealed record TankRecoveryDecision(ulong Target, Vector3 Position, uint Action, string Reason, bool Submitted, DateTime ObservedAt);

// Recovery submits a legal native action without changing a movement endpoint or
// using the hard target as an action-transport channel. The selected rotation's
// current GCD and recipients remain binding; Provoke uses a free weave.
internal sealed class NativeTankRecovery(WorldState world, AIHints hints, ActionManagerEx actions,
    Func<AssistancePreferences> preferences, RsrRuntimeAccess runtime, Func<bool> allowed)
{
    private readonly TankRecoveryPolicy _policy = new();
    private readonly RsrCastControlEligibility _eligibility = new();
    private ulong _actor;
    private ushort _zone;
    private Assembly? _basic;
    private PropertyInfo? _targetOnly;
    private PropertyInfo? _intercepted;
    private PropertyInfo? _commands;
    private PropertyInfo? _special;
    internal TankRecoveryDecision? Decision { get; private set; }
    internal bool Pending { get; private set; }
    internal ulong LooseEnemyTarget { get; private set; }
    internal string Status { get; private set; } = "inactive";

    internal bool Enabled => preferences().TankRecoverAfterTankDeath || preferences().TankFollowBossModSwaps
        || preferences().TankUseRangedAggroRecovery || preferences().TankTargetLostTrashAggro;

    internal void Update(RsrActionEvidence? currentAction, bool noSelectedAction, ImmutableHashSet<uint> enabledActions)
    {
        Decision = null;
        Pending = false;
        LooseEnemyTarget = 0;
        var player = world.Party.Player();
        if (_actor != player?.InstanceID || _zone != world.CurrentZone || !allowed() || player?.InCombat != true)
        {
            _policy.Reset();
            _actor = player?.InstanceID ?? 0;
            _zone = world.CurrentZone;
        }
        if (!Enabled || !allowed() || player is not { Role: Role.Tank, InCombat: true }) { Status = "inactive"; return; }
        try
        {
            if (!runtime.TryGetSupportContext(out _, out var basic, out var rotation)
                || RsrRuntimeAccess.Job(rotation) != (uint)player.Class) { Status = "RSR recovery evidence unavailable"; return; }
            _eligibility.Prepare(basic);
            if (_basic != basic)
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                var center = basic.GetType("RotationSolver.Basic.DataCenter", true)!;
                _targetOnly = center.GetProperty("IsTargetOnly", flags) ?? throw new MissingMemberException("IsTargetOnly");
                _intercepted = center.GetProperty("CurrentInterceptedAction", flags) ?? throw new MissingMemberException("CurrentInterceptedAction");
                _commands = center.GetProperty("CommandStatus", flags) ?? throw new MissingMemberException("CommandStatus");
                _special = center.GetProperty("SpecialType", flags) ?? throw new MissingMemberException("SpecialType");
                _basic = basic;
            }
            if (!Automatic()) { Status = "Manual RSR action retains control"; return; }
            var config = preferences();
            var job = Autorotation.xan.TankAI.ActionsForJob(player.Class);
            var enemies = hints.PotentialTargets.Where(e => e.Actor is
            { IsTargetable: true, IsDead: false, IsDestroyed: false, InCombat: true }).ToArray();
            var enemyValues = enemies.Select(e => new TankRecoveryEnemy(e.Actor.InstanceID, e.Actor.TargetID, e.Priority,
                e.ShouldBeTanked, e.PreferProvoking, e.Actor.CastInfo is { EventHappened: false },
                BossModuleRegistry.FindByOID(e.Actor.OID) != null, player.DistanceToHitbox(e.Actor))).ToArray();
            var party = world.Party.WithoutSlot(includeDead: true, excludeAlliance: true).Select(p => new TankRecoveryMember(p.InstanceID,
                p.Role == Role.Tank, p.IsDead || p.IsDestroyed, p.HPMP.CurHP, p.HPMP.MaxHP, p.InCombat, p.CastInfo != null,
                p.Role == Role.Tank && p.FindStatus(Autorotation.xan.TankAI.ActionsForJob(p.Class).StanceBuff) != null,
                p.TargetID, p.Position, p.HitboxRadius)).ToArray();
            var choices = _policy.Observe(world.CurrentTime, player.InstanceID, enemyValues, party,
                config.TankFollowBossModSwaps, config.TankRecoverAfterTankDeath,
                config.TankUseRangedAggroRecovery || config.TankTargetLostTrashAggro,
                (member, enemy) => world.Actors.Find(enemy.Id) is { } actor
                    && (member.Position - actor.Position).Length() <= 25 + member.Radius + actor.HitboxRadius);
            var held = enemies.Count(e => e.Actor.TargetID == player.InstanceID);
            LooseEnemyTarget = choices.FirstOrDefault(c => c.Kind == TankRecoveryKind.LooseEnemy
                && TankRecoveryPolicy.CanRetarget(c.Distance, held, c.Urgency))?.Enemy ?? 0;
            Pending = !choices.IsEmpty;
            Status = Pending ? "Waiting for a legal tank-recovery action" : "No confirmed tank recovery";
            foreach (var choice in choices)
            {
                var enemy = enemies.First(e => e.Actor.InstanceID == choice.Enemy);
                var reason = choice.Kind switch
                {
                    TankRecoveryKind.AuthoredSwap => "Native encounter requests a tank swap",
                    TankRecoveryKind.TankDeath => "Recover after an observed tank death",
                    _ => "Recover sustained aggro on a party member"
                };
                if (Decision == null) Decision = new(choice.Enemy, enemy.Actor.PosRot.XYZ(), 0, reason, false, DateTime.UtcNow);
                if (player.FindStatus(job.StanceBuff) == null)
                {
                    if (enabledActions.Contains(job.Stance.ID) && allowed() && Automatic()
                        && actions.TryExecuteTankStance(job.Stance, player, job.StanceBuff, true, noSelectedAction))
                    {
                        Decision = Decision with { Action = job.Stance.ID, Reason = "Enable stance for confirmed recovery", Submitted = true };
                        Status = Decision.Reason;
                        return;
                    }
                    continue;
                }
                var provoke = ActionID.MakeSpell(ClassShared.AID.Provoke);
                var mayProvoke = world.CurrentCFCID != 0 && enemy.Actor.FateID == 0;
                // Only an explicit handoff can use ranged recovery regardless of
                // the optional trash setting. Never replace a selected heal or
                // discard a GCD's independently evidenced target/recipients.
                var mayRange = choice.Kind != TankRecoveryKind.LooseEnemy || config.TankUseRangedAggroRecovery;
                var ranged = mayRange && PreservesCurrentAction(currentAction, noSelectedAction, job.Ranged.ID, enemy.Actor.InstanceID);
                var candidates = choice.Kind != TankRecoveryKind.LooseEnemy
                    ? new[] { mayProvoke ? provoke : default, ranged ? job.Ranged : default }
                    : new[] { ranged && choice.Distance > 3 ? job.Ranged : default,
                        mayProvoke && config.TankUseRangedAggroRecovery && choice.Distance > 3 ? provoke : default };
                foreach (var action in candidates)
                {
                    if (!action || !enabledActions.Contains(action.ID) || !allowed() || !Automatic()
                        || Service.ObjectTable.SearchById(enemy.Actor.InstanceID) is not IBattleChara target
                        || !target.IsValid() || target.Address == 0 || !_eligibility.Attackable(target)) continue;
                    if (actions.TryExecuteTankRecovery(action, player, enemy, choice.Victim, choice.Kind == TankRecoveryKind.AuthoredSwap))
                    {
                        Decision = new(choice.Enemy, enemy.Actor.PosRot.XYZ(), action.ID, reason, true, DateTime.UtcNow);
                        Status = reason + "; action submitted, ownership not yet confirmed";
                        return;
                    }
                }
            }
        }
        catch (Exception error)
        {
            _policy.Reset();
            Pending = false;
            LooseEnemyTarget = 0;
            Status = "Tank recovery evidence unavailable; native rotation retained";
            Service.Logger.Verbose(error, Status);
        }
    }

    private bool Automatic() => _targetOnly?.GetValue(null) is false && _intercepted?.GetValue(null) == null
        && !RsrCommandPolicy.HasExplicitCommand(_commands?.GetValue(null) as Enum, _special?.GetValue(null) as Enum);

    internal static bool PreservesCurrentAction(RsrActionEvidence? current, bool noSelectedAction, uint ranged, ulong target)
        => current == null ? noSelectedAction : !current.Friendly && current.ActionId == ranged && current.TargetId == target
            && current.AffectedRecipients.All(id => id == target);

    internal void Reset() { _policy.Reset(); Pending = false; LooseEnemyTarget = 0; Decision = null; Status = "inactive"; }
}
