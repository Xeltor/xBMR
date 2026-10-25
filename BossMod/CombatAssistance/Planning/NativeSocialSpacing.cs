using BossMod.CombatAssistance.Config;
using BossMod.Pathfinding;

namespace BossMod.CombatAssistance.Planning;

internal readonly record struct SocialSpacingPeer(ulong Id, WPos Position, bool Priority = false, bool Flexible = true);

// Optional personal space is a local destination preference, never a forbidden
// zone or a movement owner. Keep the origin fixed during one overlap so repeated
// captures cannot push the desired endpoint farther away as the player walks.
internal sealed record SocialSpacingPreference(WPos Origin, ImmutableArray<SocialSpacingPeer> Peers, WDir Bias)
{
    internal float Value(WPos point)
    {
        var offset = point - Origin;
        var distance = offset.Length();
        if (distance < 0.75f || distance >= 2.25f) return 0;
        var clearance = 1.15f;
        foreach (var peer in Peers)
        {
            clearance = Math.Min(clearance, (point - peer.Position).Length());
            // A comfort adjustment may leave an occupied priority position, but
            // must not cross farther into a caster's/tank's/melee ally's space.
            if (peer.Priority)
            {
                var fraction = Math.Clamp((peer.Position - Origin).Dot(offset) / offset.LengthSq(), 0, 1);
                var closest = (Origin + fraction * offset - peer.Position).Length();
                if (closest + 0.0001f < Math.Min(1.15f, (Origin - peer.Position).Length())) return 0;
            }
        }
        if (clearance <= 0.35f) return 0;
        var local = distance <= 1.25f ? 1 : 2.25f - distance;
        return 0.2f * Math.Clamp((clearance - 0.35f) / 0.8f, 0, 1) * local
            * (0.85f + 0.15f * offset.Normalized().Dot(Bias));
    }
}

internal sealed class NativeSocialSpacing
{
    private readonly Dictionary<ulong, Sample> _samples = [];
    private sealed record Sample(WPos Position, DateTime At, DateTime Since, WPos Occupied, DateTime SettledSince, bool First, bool Separating);
    private (ushort Zone, ulong Player, Class Job) _identity;
    private DateTime _last;
    private DateTime _handoffUntil;
    private DateTime? _meleeSince;
    private WPos? _origin;
    private SocialSpacingPreference? _preference;

    internal SocialSpacingPreference? Capture(Map map, AIHints hints, WorldState world, Actor player,
        AssistancePreferences settings, bool allowed)
    {
        var target = hints.ForcedTarget ?? world.Actors.Find(player.TargetID);
        var casting = player.CastInfo is { EventHappened: false, RemainingTime: > 0 };
        var holding = player.Role == Role.Tank && hints.PotentialTargets.Any(e => e.Actor.InCombat && e.Actor.TargetID == player.InstanceID);
        var origin = map.WorldToGrid(player.Position);
        var recovering = !map.InBounds(origin.x, origin.y);
        if (!recovering)
        {
            var cell = map.GridToIndex(origin);
            recovering = map.PixelMaxG[cell] != float.MaxValue;
            for (var i = 0; !recovering && i < map.Width * map.Height; ++i)
                recovering = map.PixelMaxG[i] == float.MaxValue && map.PixelPriority[i] > map.PixelPriority[cell];
        }
        var positioning = recovering || hints.ForbiddenZones.Count != 0 || hints.TemporaryObstacles.Count != 0
            || hints.ForbiddenDirections.Count != 0 || hints.ImminentSpecialMode.mode != AIHints.SpecialMode.Normal
            || hints.ForcedMovement != null || player.PendingKnockbacks.Count != 0;
        var flexible = !casting && player.Role is Role.Ranged or Role.Healer;
        // XCAI considered visible PCs, not Duty Support NPCs or dead/different-floor allies.
        var peers = world.Actors.Where(a => EligiblePeer(a, player)).Select(a => new SocialSpacingPeer(a.InstanceID, a.Position,
            a.CastInfo is { EventHappened: false, RemainingTime: > 0 }
                || a.Role == Role.Tank && hints.PotentialTargets.Any(e => e.Actor.InCombat && e.Actor.TargetID == a.InstanceID)
                || flexible && a.Role == Role.Melee && target != null && a.TargetID == target.InstanceID,
            a.Role is Role.Ranged or Role.Healer && a.CastInfo is not { EventHappened: false, RemainingTime: > 0 })).ToImmutableArray();
        return Observe((world.CurrentZone, player.InstanceID, player.Class), world.CurrentTime, player.Position, peers,
            allowed && settings.ManageSocialSpacing && player.InCombat && !player.IsDead,
            positioning || casting || holding || settings.BlackMageLateMovement && player.Class == Class.BLM,
            player.Role == Role.Melee && target != null && player.DistanceToHitbox(target) <= 3);
    }

    internal static bool EligiblePeer(Actor ally, Actor player) => ally.Type == ActorType.Player
        && ally.InstanceID != player.InstanceID && !ally.IsDead && !ally.IsDestroyed
        && MathF.Abs(ally.PosRot.Y - player.PosRot.Y) <= 3 && (ally.Position - player.Position).LengthSq() <= 9;

    internal SocialSpacingPreference? Observe((ushort Zone, ulong Player, Class Job) identity, DateTime now, WPos position,
        ImmutableArray<SocialSpacingPeer> peers, bool allowed, bool positioning, bool meleeInRange = false)
    {
        if (!allowed) { Reset(); return null; }
        if (_identity != identity || now < _last || now - _last > TimeSpan.FromMilliseconds(500)) Reset();
        _identity = identity;
        _last = now;
        foreach (var id in _samples.Keys.Where(id => !peers.Any(p => p.Id == id)).ToArray()) _samples.Remove(id);
        var overlapping = peers.Where(p => (p.Position - position).LengthSq() <= 1).ToArray();
        var holds = 0;
        foreach (var peer in peers)
        {
            var overlap = (peer.Position - position).LengthSq() <= 1;
            if (!_samples.TryGetValue(peer.Id, out var sample))
                sample = new(peer.Position, now, now, position, now, !overlap, false);
            // First arrival belongs to an observed spot, regardless of the
            // newcomer casting or having a preferred combat role. Never renew
            // that spot every frame while walking into somebody else's space.
            var leftSpot = (position - sample.Occupied).LengthSq() > 0.35f * 0.35f;
            var first = sample.First && !leftSpot;
            if (leftSpot) sample = sample with { Occupied = position, SettledSince = now };
            if (!overlap && now - sample.SettledSince >= TimeSpan.FromMilliseconds(300)) first = true;
            var elapsed = (float)(now - sample.At).TotalSeconds;
            var separating = sample.Separating;
            if (elapsed >= 0.1f)
            {
                var away = peer.Position - position;
                var moved = peer.Position - sample.Position;
                separating = away.LengthSq() > 0.0001f && moved.Dot(away.Normalized()) >= 0.05f
                    && away.Length() > (sample.Position - position).Length() + 0.04f;
                sample = sample with { Position = peer.Position, At = now };
            }
            sample = sample with { First = first,
                Since = overlap ? sample.Since : now, Separating = separating };
            _samples[peer.Id] = sample;
            var duration = (now - sample.Since).TotalMilliseconds;
            if (overlap && (first || separating || !peer.Priority && (duration < 300 || identity.Player < peer.Id && duration < 650))) ++holds;
        }
        if (positioning) _handoffUntil = now.AddMilliseconds(500);
        if (meleeInRange && overlapping.Length > 0 && overlapping.All(p => p.Flexible)) _meleeSince ??= now;
        else _meleeSince = null;
        // Dense groups often deliberately stack. Cosmetic spacing must yield.
        if (positioning || now < _handoffUntil || overlapping.Length == 0 || holds == overlapping.Length
            || peers.Length >= 3
            || _meleeSince is { } since && now - since < TimeSpan.FromSeconds(1))
        {
            _origin = null;
            _preference = null;
            return null;
        }
        _origin ??= position;
        var hash = unchecked((uint)(identity.Player ^ identity.Player >> 32));
        hash ^= hash >> 16; hash *= 0x7feb352d; hash ^= hash >> 15; hash *= 0x846ca68b; hash ^= hash >> 16;
        // One overlap gets one fixed preference. Ally motion must not keep
        // rotating a cosmetic destination underneath an executing route.
        return _preference ??= new(_origin.Value, peers, (hash / (float)uint.MaxValue * MathF.Tau).Radians().ToDirection());
    }

    internal void Reset()
    {
        _samples.Clear(); _identity = default; _last = _handoffUntil = default; _meleeSince = null; _origin = null; _preference = null;
    }
}
