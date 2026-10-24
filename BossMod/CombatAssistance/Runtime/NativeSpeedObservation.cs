using BossMod.CombatAssistance.Planning;

namespace BossMod.CombatAssistance.Runtime;

internal sealed class NativeSpeedObservation
{
    private ulong _actor;
    private ushort _zone;
    private Class _class;
    private float _baseline;
    private ImmutableArray<ActorStatus> _otherStatuses = [];
    internal static bool SpeedStatus(uint id) => id is 50 or 4239 or 4255 or 4261 or 4276;

    internal PlanningTravel Capture(Actor player, ushort zone, DateTime now, float speed)
    {
        var others = player.Statuses.Where(s => s.ID != 0 && !SpeedStatus(s.ID)).ToImmutableArray();
        var boosts = player.Statuses.Where(s => SpeedStatus(s.ID) && s.ExpireAt > now).ToArray();
        if (_actor != player.InstanceID || _zone != zone || _class != player.Class || !_otherStatuses.SequenceEqual(others)) _baseline = 0;
        _actor = player.InstanceID;
        _zone = zone;
        _class = player.Class;
        _otherStatuses = others;
        if (boosts.Length == 0)
        {
            // A high native speed can precede its status observation by a frame.
            // Do not later mistake that transient boost for an unboosted baseline.
            _baseline = speed <= 6.61f ? speed : 0;
            return new(speed);
        }
        // The observed native speed already includes traits, terrain and other
        // statuses. Retain a slower baseline only when those inputs still match.
        return new(Math.Min(_baseline, speed), speed, Math.Max(0, (float)(boosts.Min(s => s.ExpireAt) - now).TotalSeconds));
    }
}
