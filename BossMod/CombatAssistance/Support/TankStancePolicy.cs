namespace BossMod.CombatAssistance.Support;

// The three-second ownership interval and 50/80 percent hysteresis are retained
// gameplay policy, not transport limits. Missing target-matched evidence preserves
// the actual stance. A pending native assignment always takes precedence.
internal sealed class TankStancePolicy
{
    private ulong _target;
    private ulong _holder;
    private DateTime _since;
    internal string Reason { get; private set; } = "Tank ownership unknown";

    internal bool? Evaluate(DateTime now, bool active, bool holding, bool recovering,
        ulong target, ulong otherTank, float? relativeThreat)
    {
        if (holding || recovering)
        {
            Reset();
            Reason = holding ? "Keep stance while holding an enemy" : "Keep stance for the native tank assignment";
            return active ? null : true;
        }
        if (target == 0 || otherTank == 0)
        {
            Reset();
            return null;
        }
        if (_target != target || _holder != otherTank || now < _since)
            (_target, _holder, _since) = (target, otherTank, now);
        Reason = "Wait for stable co-tank ownership";
        if ((now - _since).TotalSeconds < 3) return null;
        if (relativeThreat is not (>= 0 and <= 1))
        {
            Reason = "Enmity unavailable; preserve current stance";
            return null;
        }
        if (relativeThreat <= 0.5f)
        {
            Reason = "Build backup threat below half of the established tank";
            return active ? null : true;
        }
        if (relativeThreat < 0.8f)
        {
            Reason = "Backup threat remains inside the stable stance band";
            return null;
        }
        Reason = "Preserve the established tank's lead";
        return active ? false : null;
    }

    internal static float? RelativeThreat(ClientState.HateInfo hate, ulong target, ulong player, ulong holder)
    {
        if (hate.InstanceID != target || target == 0 || player == holder || player == 0 || holder == 0) return null;
        var own = hate.Targets.Where(t => t.InstanceID == player).ToArray();
        var other = hate.Targets.Where(t => t.InstanceID == holder).ToArray();
        if (own.Length != 1 || other.Length != 1 || own[0].Enmity < 0 || other[0].Enmity <= 0) return null;
        var ratio = (float)own[0].Enmity / other[0].Enmity;
        return float.IsFinite(ratio) && ratio <= 1 ? ratio : null;
    }

    internal void Reset()
    {
        (_target, _holder, _since) = (0, 0, default);
        Reason = "Tank ownership unknown";
    }
}
