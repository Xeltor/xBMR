using BossMod.CombatAssistance.Integrations;
using Dalamud.Game.ClientState.Objects.Types;

namespace BossMod.CombatAssistance.Control;

internal sealed record CastControlDecision(ulong Target, uint Cast, uint Action, CastControlChoice Choice,
    Vector3 Position, string Reason, DateTime ObservedAt);

// No movement writer and no hazard removal. The native producer supplies cast
// identity; control is useful only while that exact cast remains active. Failed
// control leaves the existing movement plan and authored warning untouched.
internal sealed class NativeCastControl : IDisposable
{
    private readonly WorldState _world;
    private readonly AIHints _hints;
    private readonly ActionManagerEx _actions;
    private readonly Func<bool> _allowed;
    private readonly RsrCastControlSelection _selection;
    private readonly Dictionary<ulong, (ActorCastInfo Cast, CastControlPolicy Policy)> _casts = [];
    private readonly CastControlImmunity _immunity = new();
    private readonly IDisposable _effects;
    private ushort _zone;
    internal CastControlDecision? Decision { get; private set; }
    internal string Status { get; private set; } = "inactive";

    internal NativeCastControl(WorldState world, AIHints hints, ActionManagerEx actions, RsrRuntimeAccess runtime, Func<bool> allowed)
    {
        _world = world;
        _hints = hints;
        _actions = actions;
        _allowed = allowed;
        _selection = new(runtime, allowed);
        _effects = world.Actors.CastEvent.Subscribe(ObserveEffect);
    }

    private void ObserveEffect(Actor source, ActorCastEvent cast)
    {
        if (!_allowed() || _world.Party.Player()?.InCombat != true) return;
        foreach (var target in cast.Targets)
            foreach (var effect in target.Effects.AsSpan())
                _immunity.ObserveEffect(target.ID, cast.Action.ID, (byte)effect.Type, effect.Value);
    }

    internal void Update(bool moving)
    {
        Decision = null;
        var player = _world.Party.Player();
        if (player?.InCombat != true || _zone != _world.CurrentZone)
        {
            _immunity.Reset();
            _casts.Clear();
            _zone = _world.CurrentZone;
        }
        if (!_allowed() || player is not { InCombat: true } || !_selection.Update((uint)player.Class))
        {
            if (!_allowed() || player?.InCombat != true) _selection.Dispose();
            _casts.Clear();
            Status = _selection.Status;
            return;
        }
        try
        {
            var enemies = _hints.PotentialTargets.Where(e => e.Priority >= 0 && e.Actor is
            { IsTargetable: true, InCombat: true, IsDead: false, IsDestroyed: false, CastInfo: { EventHappened: false } })
                .OrderByDescending(e => e.ShouldBeInterrupted || e.ShouldBeStunned).ToArray();
            var live = enemies.Select(e => e.Actor.InstanceID).ToHashSet();
            foreach (var id in _casts.Keys.Where(id => !live.Contains(id)).ToArray()) _casts.Remove(id);
            Status = "No eligible cast; native movement retained";
            foreach (var enemy in enemies)
                if (Evaluate(player, enemy, moving)) break;
        }
        catch (Exception error)
        {
            _selection.Dispose();
            _casts.Clear();
            Status = "Cast-control observation unavailable; RSR retained";
            Service.Logger.Verbose(error, Status);
        }
    }

    private bool Evaluate(Actor player, AIHints.Enemy hint, bool moving)
    {
        var enemy = hint.Actor;
        var cast = enemy.CastInfo!;
        if (Service.ObjectTable.SearchById(enemy.InstanceID) is not IBattleChara wrapper
            || !wrapper.IsValid() || wrapper.Address == 0 || !_selection.Eligibility.Attackable(wrapper)) return false;
        if (!_casts.TryGetValue(enemy.InstanceID, out var state) || !ReferenceEquals(state.Cast, cast))
            _casts[enemy.InstanceID] = state = (cast, new());
        var now = _world.CurrentTime;
        state.Policy.Observe(cast.Action.ID, cast.ElapsedTime, now);
        var interrupt = player.Role == Role.Tank ? 7538u : 7551u;
        var stun = player.Role == Role.Tank ? 7540u : 7863u;
        var blocked = _immunity.Blocks(enemy.InstanceID) || enemy.Statuses.Any(s => CastControlImmunity.BlocksStun(s.ID))
            || _selection.Eligibility.KnownStunImmune(wrapper);
        if (CastControlImmunity.IsStunAction(state.Policy.SubmittedAction)
            && now - state.Policy.SubmittedAt > TimeSpan.FromMilliseconds(800)
            && !enemy.Statuses.Any(s => s.ID == 2))
            _immunity.MarkUnconfirmed(enemy.InstanceID);
        var ordinary = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.BNpcBase>().TryGetRow(enemy.OID, out var npc)
            && npc.Rank is not (2 or 6);
        var stunReady = !blocked && (hint.ShouldBeStunned || ordinary) && Ready(stun, player, enemy);
        if (hint.ShouldBeStunned && !stunReady && !blocked && player.Class == Class.PLD
            && _actions.GCD() <= 0 && Ready(16, player, enemy))
        {
            stun = 16;
            stunReady = true;
        }
        var area = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(cast.Action.ID, out var row)
            && row.CanTargetHostile && row.CastType > 1;
        var important = hint.ShouldBeInterrupted || cast.Interruptible && (!area || _world.Party.WithoutSlot().Length > 1);
        var free = _actions.EffectiveAnimationLock <= 0 && player.CastInfo == null
            && !player.Statuses.Any(s => s.ID is 496 or 1186 or 1317);
        var choice = state.Policy.Choose(new(cast.Interruptible, important, hint.ShouldBeStunned,
            player.TargetID == enemy.InstanceID && Threatens(_hints, enemy, player.Position), moving,
            cast.ElapsedTime, cast.RemainingTime, Ready(interrupt, player, enemy), stunReady,
            free && (important || hint.ShouldBeStunned || _actions.GCD() >= 0.7f)));
        var action = choice == CastControlChoice.Interrupt ? interrupt : choice == CastControlChoice.Stun ? stun : 0;
        var reason = choice switch
        {
            CastControlChoice.Interrupt => "Interrupt active cast; escape remains available",
            CastControlChoice.Stun => hint.ShouldBeStunned ? "Encounter requests a stun" : "Early stun; escape remains available",
            CastControlChoice.Submitted => "Control submitted; cast cancellation not yet confirmed",
            CastControlChoice.Dodge => "Preserve control; follow native escape",
            _ => "Wait for a legal control window"
        };
        if (Decision == null || action != 0 || player.TargetID == enemy.InstanceID)
            Decision = new(enemy.InstanceID, cast.Action.ID, action, choice, enemy.PosRot.XYZ(), reason, DateTime.UtcNow);
        Status = reason;
        if (action == 0 || !_allowed() || !Ready(action, player, enemy)
            || !_actions.TryExecuteCastControl(action, player, enemy, cast, choice == CastControlChoice.Interrupt, out var rejection))
            return false;
        state.Policy.Submitted(now, action);
        Status = "Control submitted; cast cancellation not yet confirmed";
        Decision = new(enemy.InstanceID, cast.Action.ID, action, CastControlChoice.Submitted, enemy.PosRot.XYZ(), Status, DateTime.UtcNow);
        return true;
    }

    private unsafe bool Ready(uint id, Actor player, Actor enemy)
        => _selection.Allows(id) && ActionDefinitions.Instance[new(ActionType.Spell, id)] is { } definition
            && definition.IsUnlocked(_world, player) && _actions.GetActionStatus(new(ActionType.Spell, id), enemy.InstanceID) == 0;

    internal static bool Threatens(AIHints hints, Actor enemy, WPos point)
        => enemy.CastInfo is { EventHappened: false } cast && hints.ForbiddenZones.Any(zone =>
            zone.Source == enemy.InstanceID && hints.FindCastSource(zone.shapeDistance, zone.activation, zone.Source) is { } source
            && ReferenceEquals(source.Cast, cast) && source.Unchanged && zone.shapeDistance.Distance(point) <= 0);

    public void Dispose()
    {
        _selection.Dispose();
        _effects.Dispose();
        _casts.Clear();
        _immunity.Reset();
        Decision = null;
    }
}
