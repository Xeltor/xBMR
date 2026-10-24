using System.Diagnostics;

namespace BossMod.CombatAssistance.Planning;

// One live opportunity; actual GCD effects permit rearming. Repeated CanUse or
// polling cannot extend a selected weave, and no journal survives the lifecycle.
internal sealed class NativePlacementOpportunity
{
    private long _serial;
    private (ulong Player, ushort Zone, ulong Target)? _identity;
    private DateTime? _retiredGcd;
    private DateTime _gcd;
    private long _earliest;
    private long _deadline;
    private float _lock;
    private WPos? _origin;
    internal long Active { get; private set; }
    internal bool Approved { get; private set; }
    internal bool Approve(long opportunity)
    {
        if (Active == 0 || Active != opportunity) return false;
        Approved = true; return true;
    }

    internal PlanningFieldPlacement? Observe(ulong player, ushort zone, ulong target, DateTime gcd,
        long now, float ready, float gcdRemaining, float actionLock, WPos? originalPosition = null)
    {
        var identity = (player, zone, target);
        if (_identity != identity) Reset();
        _identity = identity;
        if (Active != 0 && (_gcd != gcd || now >= _deadline)) Retire();
        if (Active == 0)
        {
            if (_retiredGcd == gcd || player == 0 || target == 0 || !float.IsFinite(ready) || ready < 0
                || !float.IsFinite(gcdRemaining) || !float.IsFinite(actionLock) || actionLock <= 0
                || gcdRemaining - actionLock <= ready || gcdRemaining > MovementPlanningContext.MaximumHorizonSeconds)
                return null;
            _gcd = gcd;
            _earliest = now + (long)(ready * Stopwatch.Frequency);
            _deadline = now + (long)((gcdRemaining - actionLock) * Stopwatch.Frequency);
            _lock = actionLock; _origin = originalPosition;
            Active = ++_serial;
        }
        return Capture(now);
    }
    internal PlanningFieldPlacement? Capture(long now)
    {
        if (Active == 0) return null;
        if (now >= _deadline) { Retire(); return null; }
        return new(Active, Math.Max(0, (_earliest - now) / (float)Stopwatch.Frequency),
            (_deadline - now) / (float)Stopwatch.Frequency, _lock, _origin);
    }
    internal PlanningFieldPlacement? Capture(ulong player, ushort zone, ulong target, long now)
    {
        if (_identity != (player, zone, target)) { Retire(); return null; }
        return Capture(now);
    }
    internal void Retire() { if (Active != 0) _retiredGcd = _gcd; Active = 0; Approved = false; }
    internal void Reset() { Active = 0; Approved = false; _identity = null; _retiredGcd = null; }
}
