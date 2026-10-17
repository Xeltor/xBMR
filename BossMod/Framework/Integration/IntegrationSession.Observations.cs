using System.Threading;

namespace BossMod.Integration;

internal sealed partial class IntegrationSession
{
    internal sealed class ContributionGuard(Func<bool> source)
    {
        private int _state; // pending, active, retired
        internal bool Live => Volatile.Read(ref _state) == 1 && source();
        internal void Activate() => Interlocked.CompareExchange(ref _state, 1, 0);
        internal void Retire() => Interlocked.Exchange(ref _state, 2);
    }
    internal ContributionGuard? PairConstraintGoals(string token, ContributionGuard guard)
    {
        lock (_gate)
        {
            if (!OnThread || !Fresh || !_owners.TryGetValue(token, out var owner) || !Live(owner)) return null;
            owner.Constraint = guard;
            foreach (var goal in owner.Goals) goal.Constraint ??= guard;
            return guard;
        }
    }
    // Read-only copied surfaces share the scoring source lifetime, not a worker's observation time.
    internal Func<bool>? CaptureObservationLiveness(string token)
    {
        lock (_gate)
        {
            if (!OnThread || !Fresh || !_owners.TryGetValue(token, out var owner) || !Live(owner)) return null;
            var revision = Volatile.Read(ref _goalRevision);
            var ownerRevision = Volatile.Read(ref owner.GoalRevision);
            var expires = _frameStarted + Milliseconds(MaxFrameAgeMs);
            return () => !_disposed && _clock() < expires && Live(owner)
                && revision == Volatile.Read(ref _goalRevision) && ownerRevision == Volatile.Read(ref owner.GoalRevision);
        }
    }
}
