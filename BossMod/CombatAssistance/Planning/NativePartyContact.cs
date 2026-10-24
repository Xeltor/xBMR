namespace BossMod.CombatAssistance.Planning;

internal sealed record PartyContactObservation(DateTime Now, ulong Tank, WPos Position, int EngagedEnemies,
    bool Dungeon, bool BossActive, bool Allowed, bool PlayerTank, bool TankInCombat, ushort Zone = 0, ulong Player = 0);

internal readonly record struct PartyContactAuthority(bool Enabled, bool NativeAuthority, bool SelfLeader, bool InCombat,
    bool MaintainPull)
{
    internal bool Allowed => Enabled && NativeAuthority && SelfLeader && InCombat && MaintainPull;
}

// The observed tank supplies a useful region, never a movement override. Native
// following, encounter constraints and each selected action retain priority.
internal sealed class NativePartyContact
{
    private ulong _tank;
    private ushort _zone;
    private ulong _player;
    private WPos _position;
    private DateTime _observed;
    private DateTime _moving;
    private DateTime _pack;
    private bool _recovering;
    internal PlanningRange? Current { get; private set; }

    internal PlanningRange? Observe(PartyContactObservation observation, WPos player, float radius)
    {
        Current = null;
        if (!observation.Allowed || !observation.Dungeon || observation.BossActive || observation.Tank == 0
            || observation.PlayerTank || !observation.TankInCombat
            || !float.IsFinite(observation.Position.X) || !float.IsFinite(observation.Position.Z)
            || !float.IsFinite(player.X) || !float.IsFinite(player.Z) || !float.IsFinite(radius) || radius <= 0)
        {
            Reset();
            return null;
        }
        if (_tank != observation.Tank || _zone != observation.Zone || _player != observation.Player || observation.Now < _observed)
        {
            Reset();
            _tank = observation.Tank;
            _zone = observation.Zone;
            _player = observation.Player;
            _position = observation.Position;
            _observed = observation.Now;
        }
        var elapsed = (float)(observation.Now - _observed).TotalSeconds;
        // Measure actual displacement; do not extrapolate a future path or
        // count a teleport as an advancing pull.
        if (elapsed > 0)
        {
            var speed = (observation.Position - _position).Length() / elapsed;
            if (speed is >= 2 and <= 15) _moving = observation.Now;
            _position = observation.Position;
            _observed = observation.Now;
        }
        if (observation.EngagedEnemies >= 2) _pack = observation.Now;
        if (observation.Now - _pack > TimeSpan.FromSeconds(1))
        {
            _recovering = false;
            return null;
        }
        var moving = observation.Now - _moving <= TimeSpan.FromSeconds(1.5);
        var separation = (player - observation.Position).Length();
        if (!float.IsFinite(separation) || !float.IsFinite(radius) || radius <= 0) return null;
        // Combat contact needs actual movement and a corroborated pack. It never
        // requests a catch-up walk between pulls or to a stationary idle tank.
        if (moving && separation > radius + 2) _recovering = true;
        if (separation <= radius || !moving && !_recovering) _recovering = false;
        return Current = _recovering ? new(observation.Tank, observation.Position, radius) : null;
    }

    internal void Reset()
    {
        _tank = 0;
        _zone = 0;
        _player = 0;
        _observed = _moving = _pack = default;
        _recovering = false;
        Current = null;
    }
}
