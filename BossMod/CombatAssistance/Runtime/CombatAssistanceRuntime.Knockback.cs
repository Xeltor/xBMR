using BossMod.CombatAssistance.Knockback;
using BossMod.CombatAssistance.Planning;
using BossMod.Pathfinding;

namespace BossMod.CombatAssistance.Runtime;

internal sealed record NativeKnockbackPlanning(NativeNavigationSnapshot Navigation, NativeKnockbackPhases? Phases,
    bool Ready, ForcedMovementChoices? Forced = null, string? RecoveryReason = null, WPos? Continuity = null);

internal sealed partial class CombatAssistanceRuntime
{
    private readonly NativeKnockbackTransaction _knockbackTransaction = new();
    private string _tankArmsLengthStatus = "inactive";

    private unsafe bool TankArmsLengthUseful()
    {
        var world = _rotation.WorldState;
        var hints = _rotation.Hints;
        var player = world.Party.Player();
        _tankArmsLengthStatus = "inactive under native role/action authority";
        if (!_settings.Preferences.ManageKnockback || !MobilityAllowed() || player is not { Role: Role.Tank, InCombat: true }
            || _actions.CastTimeRemaining > 0 || _actions.EffectiveAnimationLock > 0 || player.CastInfo is { EventHappened: false }) return false;
        var module = _rotation.Bossmods.ActiveModule;
        var dungeon = world.CurrentCFCID != 0
            && Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(world.CurrentCFCID)?.ContentType.RowId == 2;
        if (!Support.TankArmsLengthPolicy.IsDungeonTrash(module?.GetType().Name, dungeon))
        { _tankArmsLengthStatus = "holding outside an authored dungeon trash module"; return false; }
        _ = CaptureKnockbackAuthority(world, player);
        var inventory = _displacementCapture?.Snapshot;
        if (inventory is not { Complete: true, Pending: false, MarchPublished: false }
            || inventory.PreparationConstraints != 0 || _knockbackAuthority is { IncompleteEvidence: true })
        { _tankArmsLengthStatus = "holding for unresolved native displacement"; return false; }
        var next = module?.StateMachine.ActiveState == null ? DateTime.MaxValue
            : module.StateMachine.NextTransitionWithFlag(StateMachine.StateHint.Knockback);
        var nextIn = next == DateTime.MaxValue ? float.MaxValue : (float)(next - world.CurrentTime).TotalSeconds;
        foreach (var source in inventory.Knockbacks)
            if (source.Enabled && source.Participant)
                nextIn = Math.Min(nextIn, source.Activation == default ? 0 : (float)(source.Activation - world.CurrentTime).TotalSeconds);
        var attackers = 0;
        var seen = new HashSet<ulong>();
        foreach (var enemy in hints.PotentialTargets)
            if (enemy.Priority >= 0 && enemy.Actor is { Type: ActorType.Enemy, InCombat: true, IsDead: false, IsDestroyed: false, IsTargetable: true } actor
                && actor.HPMP.CurHP > 0 && actor.TargetID == player.InstanceID && player.DistanceToHitbox(actor) <= 3
                && seen.Add(actor.InstanceID) && ++attackers == 2) break;
        var existing = player.Statuses.Any(status => status.ID is 160 or 1209 && status.ExpireAt > world.CurrentTime);
        if (!Support.TankArmsLengthPolicy.ShouldUse(true, true, attackers, existing, nextIn, false))
        { _tankArmsLengthStatus = "holding for attackers, existing immunity or a known knockback cooldown"; return false; }
        var id = ActionID.MakeSpell(ClassShared.AID.ArmsLength);
        var definition = ActionDefinitions.Instance[id];
        if (definition == null || !definition.IsUnlocked(world, player)) return false;
        var duration = definition.InstantAnimLock + _actions.AnimationLockDelayEstimate;
        var gcd = _actions.GCD();
        if (!float.IsFinite(duration) || duration <= 0 || !float.IsFinite(gcd) || gcd <= duration || !PlanningExecutionRules.Capture(world, hints).PermitsAction(null, 0, duration)
            || _actions.GetActionStatus(id, player.InstanceID) != 0)
        { _tankArmsLengthStatus = "waiting for a free native weave and legal action window"; return false; }
        _tankArmsLengthStatus = "useful against at least two nearby engaged melee attackers";
        return true;
    }

    internal void ReleaseKnockbackTransaction()
    {
        _knockbackTransaction.Release();
        _knockbackSelection.ResetPlan();
    }
    internal void ObserveKnockbackPlan(MovementPlan plan) => _knockbackTransaction.ObservePlan(plan);

    internal NativeKnockbackPlanning? ResolveKnockbackPlanning(string owner, WorldState world, AIHints hints,
        Actor player, NavigationDecision.CapturedInput input, NativeNavigationSnapshot native, IEnumerable<NativePhysicalObstacle>? blocks)
    {
        _ = CaptureKnockbackAuthority(world, player);
        var allowed = _settings.MayAssist && _settings.Preferences.ManageMovement
            && (_settings.Preferences.PlanKnockbackLandings || _settings.Preferences.ManageKnockback)
            && HasMovementAuthority(player, hints)
            && !Service.ClientState.IsPvP;
        if (!allowed || _displacementCapture == null)
        { _knockbackTransaction.Release(); return null; }
        var inventory = _displacementCapture.Snapshot;
        var module = _rotation.Bossmods.ActiveModule;
        if (_knockbackTransaction.Phase is { Timing: { } expected } armed && !armed.Source.IgnoresImmunity
            && world.CurrentTime < (expected.EarliestUtc < armed.Source.Activation ? expected.EarliestUtc : armed.Source.Activation)
            && inventory.Knockbacks.Any(source => source.SameGeometry(armed.Source) && source.ObservedImmune)
            && armed.Source.Owner >= 0 && armed.Source.Owner < _displacementCapture.Owners.Length
            && world.Party.FindSlot(player.InstanceID) is >= 0 and < PartyState.MaxAllies
            && _displacementCapture.Owners[armed.Source.Owner].IsImmune(world.Party.FindSlot(player.InstanceID), expected.LatestUtc))
            _knockbackTransaction.Release();
        NativeKnockbackPlanning Recover(string reason) => new(native, null, false, RecoveryReason: reason);
        var held = _knockbackTransaction.Phase;
        var transaction = _knockbackTransaction.Capture(owner, module, input.Identity, inventory, KnockbackCompletion, allowed);
        if (transaction?.Failure is { } failure) return Recover(failure);
        held = _knockbackTransaction.Phase;
        var active = transaction is { Preparing: false } ? transaction : null;
        var phases = NativeKnockbackPhaseCapture.Freeze(_displacementCapture, hints, module, player, input, blocks, active?.Phase.Source)
            with { PlanLandings = _settings.Preferences.PlanKnockbackLandings };
        _knockbackPhases = phases;
        if (active != null)
        {
            var stillPublished = inventory.Knockbacks.Any(source => source.SameGeometry(active.Phase.Source));
            var own = phases.Phases.FirstOrDefault(phase => phase.Source.SameGeometry(active.Phase.Source));
            if (stillPublished && own == null) return Recover("active native preparation constraints cannot be separated");
            var remaining = phases with { Phases = phases.Phases.Where(phase => !phase.Source.SameGeometry(active.Phase.Source)).ToImmutableArray() };
            string? reason = null;
            var bound = remaining;
            if (!inventory.Complete || inventory.MarchPublished
                || inventory.PreparationConstraints != phases.Phases.Sum(phase => phase.SeparatedLeaves)
                || inventory.Knockbacks.Count(source => source.Interrupts(world.CurrentTime)
                    && !source.SameGeometry(active.Phase.Source)) != remaining.Phases.Length
                || !BindKnockbackTiming(world, player, remaining, out bound, out reason))
                return Recover(reason ?? "active native knockback inventory or constraints changed");
            var end = (float)(active.Phase.Timing!.LandingLatestUtc - world.CurrentTime).TotalSeconds;
            if (end <= 0 || !active.Phase.LandingAllowed(active.Landing, player.HitboxRadius))
                return Recover("native knockback landing window or footprint changed");
            hints.MaxCastTime = 0;
            _knockbackPhases = bound;
            var route = new PlanningForcedMovement($"native-knockback:{active.Phase.Source.SourceActor}:{active.Phase.CastGeneration}",
                player.Rotation, false, [new(player.Position, active.Landing, 0, end)], true)
            { ConservativeCorridor = true, Knockback = active.Phase.Source };
            return new(own?.After ?? native, bound, true, new([route], false), Continuity: active.UsefulDestination);
        }
        if (phases.Phases.IsEmpty) return null;
        if (inventory.PreparationConstraints != phases.Phases.Sum(phase => phase.SeparatedLeaves))
            return Recover("native preparation includes an unmodeled displacement constraint");
        if (!BindKnockbackTiming(world, player, phases, out var prepared, out var unavailable, held))
            return Recover(unavailable ?? "native knockback timing unavailable");
        _knockbackPhases = prepared;
        _knockbackTransaction.ObservePreparation(owner, module, input.Identity, prepared.Phases[0], player.Position);
        return new(native, prepared, true);
    }

    private void ApplyKnockbackCastWindow()
    {
        if (!_settings.MayAssist || !_settings.Preferences.ManageMovement || !(_settings.Preferences.PlanKnockbackLandings || _settings.Preferences.ManageKnockback)
            || _rotation.WorldState.Party.Player() is not { IsDead: false, MountId: 0 } player
            || !HasMovementAuthority(player, _rotation.Hints))
        { _knockbackTransaction.Release(); return; }
        _rotation.Hints.MaxCastTime = Math.Min(_rotation.Hints.MaxCastTime,
            _knockbackTransaction.CastWindow(_rotation.WorldState.CurrentTime));
    }

    private bool BindKnockbackTiming(WorldState world, Actor player, NativeKnockbackPhases captured,
        out NativeKnockbackPhases bound, out string? reason, NativeKnockbackPhase? held = null)
        => NativeKnockbackTimingCapture.Bind(_knockbackObserver, world, player, captured, out bound, out reason, held);
}
