namespace BossMod.CombatAssistance.Planning;

// A remembered formation is only a presentation value across the native map.
// Capture it before the safety move, so returning to a useful lane cannot become
// a second arrival correction. No remembered point is a safety or action proof.
internal sealed record FormationPreference(WPos Target, WPos? Anchor, Angle? Lane)
{
    internal float Value(WPos point)
    {
        if (Anchor is { } anchor) return 0.2f / (1 + (point - anchor).LengthSq());
        var offset = point - Target;
        return Lane is { } lane && offset.LengthSq() > 1e-6f ? 0.1f * offset.Normalized().Dot(lane.ToDirection()) : 0;
    }
}

internal sealed class NativeFormationPreference
{
    private (ushort Zone, ulong Actor, Class Job, ulong Target) _identity;
    private ImmutableArray<ulong> _party = [];
    private WPos? _settling;
    private WPos _settlingTarget;
    private DateTime _since;
    private WDir? _offset;
    private WPos _anchorTarget;
    private DateTime _captured;
    private DateTime _last;
    private bool _wasMechanic;
    private DateTime _returnUntil;

    internal FormationPreference? Capture(WorldState world, AIHints hints, Actor player, bool allowed)
    {
        var target = hints.ForcedTarget ?? world.Actors.Find(player.TargetID);
        var peers = world.Party.WithoutSlot(excludeAlliance: true).Where(a => !a.IsDead && !a.IsDestroyed && a.Role != Role.Tank).ToArray();
        if (!allowed || !player.InCombat || player.Role == Role.Tank || target is not { IsDead: false, IsTargetable: true }
            || !hints.PriorityTargetsSpan.ToArray().Any(e => e.Actor.InstanceID == target.InstanceID) || peers.Length < 2)
        {
            Reset();
            return null;
        }
        var mechanic = hints.ForbiddenZones.Count != 0 || hints.TemporaryObstacles.Count != 0 || hints.ForbiddenDirections.Count != 0
            || hints.ImminentSpecialMode.mode != AIHints.SpecialMode.Normal || hints.ForcedMovement != null || player.PendingKnockbacks.Count != 0;
        var eligible = player.DistanceToHitbox(target) <= (player.Role == Role.Melee ? 3 : 25)
            && peers.Where(a => a.InstanceID != player.InstanceID).All(a => (a.Position - player.Position).LengthSq() >= 1.15f * 1.15f)
            && (player.Position - target.Position).LengthSq() >= target.HitboxRadius * target.HitboxRadius;
        return Observe((world.CurrentZone, player.InstanceID, player.Class, target.InstanceID),
            peers.Select(a => a.InstanceID).Order().ToImmutableArray(), world.CurrentTime, player.Position, target.Position, target.Rotation,
            eligible, mechanic);
    }

    internal FormationPreference? Observe((ushort Zone, ulong Actor, Class Job, ulong Target) identity, ImmutableArray<ulong> party,
        DateTime now, WPos position, WPos target, Angle facing, bool eligible, bool mechanic)
    {
        if (identity != _identity || !party.SequenceEqual(_party) || now < _last || now - _last > TimeSpan.FromSeconds(2)) Reset();
        _identity = identity;
        _party = party;
        _last = now;
        if (now - _captured >= TimeSpan.FromSeconds(30) || (target - _anchorTarget).LengthSq() > 64) _offset = null;
        if (mechanic)
        {
            _wasMechanic = true;
            _settling = null;
            _returnUntil = now.AddSeconds(8);
        }
        else if (_wasMechanic)
        {
            _wasMechanic = false;
            _returnUntil = now.AddSeconds(8);
        }
        if (mechanic || now < _returnUntil)
        {
            var index = party.IndexOf(identity.Actor);
            // Deterministic angular lanes replace endpoint-cluster candidate caps.
            var lane = index >= 0 && party.Length >= 2 ? facing + ((index + 0.5f) * MathF.Tau / party.Length).Radians() : (Angle?)null;
            return new(target, _offset is { } offset ? target + offset : null, lane);
        }
        if (!eligible) _settling = null;
        else if (_settling is not { } start || (position - start).LengthSq() > 0.04f || (target - _settlingTarget).LengthSq() > 0.04f)
        {
            _settling = position;
            _settlingTarget = target;
            _since = now;
        }
        else if (now - _since >= TimeSpan.FromMilliseconds(500))
        {
            _offset = position - target;
            _anchorTarget = target;
            _captured = now;
        }
        return null;
    }

    internal void Reset()
    {
        _identity = default;
        _party = [];
        _settling = null;
        _offset = null;
        _wasMechanic = false;
        _returnUntil = _last = default;
    }
}
