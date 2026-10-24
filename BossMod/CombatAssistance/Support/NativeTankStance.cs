using BossMod.CombatAssistance.Integrations;

namespace BossMod.CombatAssistance.Support;

internal sealed record TankStanceDecision(uint Action, bool Enable, string Reason, bool Submitted, DateTime ObservedAt);

internal sealed class NativeTankStance(WorldState world, AIHints hints, ActionManagerEx actions,
    RsrRuntimeAccess runtime, Func<bool> allowed, Func<bool> recoveryPending) : IDisposable
{
    private readonly TankStancePolicy _policy = new();
    private readonly RsrTankStanceSelection _selection = new(runtime, allowed);
    private ulong _player;
    private ushort _zone;
    internal TankStanceDecision? Decision { get; private set; }
    internal string Status { get; private set; } = "inactive";

    internal static ActionID Release(Class job) => job switch
    {
        Class.PLD or Class.GLA => ActionID.MakeSpell(PLD.AID.ReleaseIronWill),
        Class.WAR or Class.MRD => ActionID.MakeSpell(WAR.AID.ReleaseDefiance),
        Class.DRK => ActionID.MakeSpell(DRK.AID.ReleaseGrit),
        Class.GNB => ActionID.MakeSpell(GNB.AID.ReleaseRoyalGuard),
        _ => default
    };

    internal void Update()
    {
        Decision = null;
        var player = world.Party.Player();
        if (player?.InstanceID != _player || world.CurrentZone != _zone || !allowed() || player?.InCombat != true)
        {
            _policy.Reset();
            _player = player?.InstanceID ?? 0;
            _zone = world.CurrentZone;
        }
        if (!allowed() || player is not { Role: Role.Tank, InCombat: true })
        {
            _selection.Dispose();
            Status = "Stance coordination inactive";
            return;
        }
        try
        {
            if (!_selection.Update(player.Class)) { Status = _selection.Status; return; }
            var job = Autorotation.xan.TankAI.ActionsForJob(player.Class);
            var enemies = hints.PotentialTargets.Where(e => e.Priority >= 0
                && e.Actor is { InCombat: true, IsDead: false, IsDestroyed: false, IsTargetable: true }).ToArray();
            var target = enemies.FirstOrDefault(e => e.Actor.InstanceID == (hints.ForcedTarget?.InstanceID ?? player.TargetID));
            var holder = target == null ? null : world.Party.WithoutSlot().FirstOrDefault(p => p.InstanceID == target.Actor.TargetID
                && p.InstanceID != player.InstanceID && p.Role == Role.Tank && !p.IsDead && !p.IsDestroyed && p.HPMP.CurHP > 0);
            var holding = enemies.Any(e => e.Actor.TargetID == player.InstanceID);
            // Native assignments win over inferred co-tank ownership, including
            // a pending swap before its Provoke has actually been observed.
            var assigned = recoveryPending() || enemies.Any(e => e.ShouldBeTanked && e.PreferProvoking && e.Actor.TargetID != player.InstanceID);
            var desired = _policy.Evaluate(world.CurrentTime, player.FindStatus(job.StanceBuff) != null, holding, assigned,
                target?.Actor.InstanceID ?? 0, holder?.InstanceID ?? 0,
                TankStancePolicy.RelativeThreat(world.Client.CurrentTargetHate, target?.Actor.InstanceID ?? 0,
                    player.InstanceID, holder?.InstanceID ?? 0));
            Status = _policy.Reason;
            if (desired is not { } enable) return;
            var action = enable ? job.Stance : Release(player.Class);
            var submitted = allowed() && _selection.Active && actions.TryExecuteTankStance(action, player, job.StanceBuff, enable);
            Decision = new(action.ID, enable, _policy.Reason, submitted, DateTime.UtcNow);
        }
        catch (Exception error)
        {
            _selection.Dispose();
            _policy.Reset();
            Status = "Tank stance evidence unavailable; RSR retained";
            Service.Logger.Verbose(error, Status);
        }
    }

    public void Dispose()
    {
        _selection.Dispose();
        _policy.Reset();
        Decision = null;
    }
}
