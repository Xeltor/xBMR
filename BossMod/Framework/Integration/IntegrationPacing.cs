using System.Diagnostics;
using System.Threading;

namespace BossMod.Integration;

// Input-thread gate. It holds no game state and never invokes a consumer callback.
internal sealed class IntegrationPacing(Func<long>? clock = null)
{
    internal const int MaxLeaseMs = 100;
    internal const float MinScale = 0.35f;
    private readonly Func<long> _clock = clock ?? Stopwatch.GetTimestamp;
    private readonly object _changes = new(); // Never taken by Apply/Live/Cancel on the input path.
    private readonly Dictionary<string, long> _ownerEpochs = [];
    private long _frameEpoch;
    internal readonly record struct Epoch(long Frame, long Owner);
    internal sealed class Ticket(string id, string owner, string instance, long frame, long generation, Vector3 direction,
        float scale, long expires, Func<bool> alive)
    {
        internal readonly string Id = id, Owner = owner, Instance = instance;
        internal readonly long Frame = frame, NavigationGeneration = generation, Expires = expires;
        internal readonly Vector3 Direction = direction;
        internal readonly float Scale = scale;
        internal readonly Func<bool> Alive = alive;
        internal IntegrationInputObservation.Assignment? Assignment;
        internal int State; // 0 pending, 3 arming, 1 native assignment armed, 2 revoked
    }
    private Ticket? _ticket;
    internal Ticket? Current => Volatile.Read(ref _ticket);

    internal Epoch? CaptureEpoch(string owner)
    {
        lock (_changes)
        {
            if (!_ownerEpochs.ContainsKey(owner))
            {
                if (_ownerEpochs.Count >= IntegrationSession.MaxOwners) return null;
                _ownerEpochs.Add(owner, 0);
            }
            return new(_frameEpoch, _ownerEpochs[owner]);
        }
    }

    internal bool Queue(Ticket ticket, Epoch? expected = null)
    {
        if (!float.IsFinite(ticket.Scale) || ticket.Scale is < MinScale or > 1 || ticket.Expires <= _clock()
            || !float.IsFinite(ticket.Direction.X) || !float.IsFinite(ticket.Direction.Y) || !float.IsFinite(ticket.Direction.Z)
            || ticket.Direction == default) return false;
        lock (_changes)
        {
            var epoch = expected ?? CaptureEpoch(ticket.Owner);
            if (epoch == null || epoch.Value.Frame != _frameEpoch || !_ownerEpochs.TryGetValue(ticket.Owner, out var current)
                || epoch.Value.Owner != current || Volatile.Read(ref ticket.State) != 0) return false;
            return Interlocked.CompareExchange(ref _ticket, ticket, null) == null;
        }
    }

    internal void Clear()
    {
        lock (_changes)
        {
            ++_frameEpoch; _ownerEpochs.Clear();
            var ticket = Interlocked.Exchange(ref _ticket, null);
            if (ticket != null) Interlocked.Exchange(ref ticket.State, 2);
        }
    }

    internal bool ClearOwner(string owner)
    {
        lock (_changes)
        {
            var ticket = Current;
            if (ticket != null && ticket.Owner != owner) return false;
            if (_ownerEpochs.TryGetValue(owner, out var epoch)) _ownerEpochs[owner] = epoch + 1;
            if (ticket != null) Cancel(ticket);
            return true;
        }
    }

    internal void Cancel(Ticket ticket)
    {
        Interlocked.Exchange(ref ticket.State, 2);
        Interlocked.CompareExchange(ref _ticket, null, ticket);
    }

    internal bool Arm(Ticket ticket, IntegrationInputObservation.Assignment assignment)
    {
        if (!Matches(ticket, assignment, assignment.Direction) || !Live(ticket)) { Cancel(ticket); return false; }
        if (Interlocked.CompareExchange(ref ticket.State, 3, 0) != 0) return false;
        Volatile.Write(ref ticket.Assignment, assignment);
        return Interlocked.CompareExchange(ref ticket.State, 1, 3) == 3;
    }

    private bool Live(Ticket ticket)
    {
        try
        {
            if (ReferenceEquals(ticket, Current) && Volatile.Read(ref ticket.State) != 2 && _clock() < ticket.Expires && ticket.Alive())
                return true;
        }
        catch { /* A failed provider-owned probe revokes the pace, never blocks native input. */ }
        Cancel(ticket);
        return false;
    }

    private static bool Matches(Ticket ticket, IntegrationInputObservation.Assignment? assignment, Vector3? direction)
        => assignment != null && assignment.InstanceGeneration == ticket.Instance && assignment.FrameId == ticket.Frame
            && assignment.Direction == ticket.Direction && direction == ticket.Direction;

    internal readonly record struct Applied(Vector2 Output, float Scale, string? ProofId);
    internal Applied Apply(IntegrationInputObservation.Assignment? assignment, Vector3? direction, Vector2 user, Vector2 actual, Vector2 output,
        bool automatic, bool allowed, bool blocked, bool follow, bool misdirection)
    {
        var ticket = Current;
        if (ticket == null) return new(output, 1, null);
        if (!Live(ticket)) return new(output, 1, null);
        if (!automatic || !allowed || blocked || follow || misdirection || user != default)
        {
            Cancel(ticket); // Manual/follow/special input cannot leave an armed pace behind.
            return new(output, 1, null);
        }
        if (Volatile.Read(ref ticket.State) != 1) return new(output, 1, null);
        if (!ReferenceEquals(Volatile.Read(ref ticket.Assignment), assignment) || !Matches(ticket, assignment, direction)
            || !float.IsFinite(output.X) || !float.IsFinite(output.Y) || !float.IsFinite(actual.X) || !float.IsFinite(actual.Y)
            || Math.Abs(output.LengthSquared() - 1) >= 0.002f || Vector2.DistanceSquared(output, actual) >= 0.0001f)
        {
            Cancel(ticket);
            return new(output, 1, null);
        }
        if (!ReferenceEquals(ticket, Current) || Volatile.Read(ref ticket.State) != 1) return new(output, 1, null);
        return new(output * ticket.Scale, ticket.Scale, ticket.Id);
    }
}
