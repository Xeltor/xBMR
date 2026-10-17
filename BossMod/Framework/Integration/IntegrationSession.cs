using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace BossMod.Integration;

// Transport-independent contract state. Never holds a game object, manager, or mutable map.
internal sealed partial class IntegrationSession : IDisposable
{
    internal const int SchemaVersion = 1;
    internal const int MaxOwners = 8;
    internal const int MaxGoalsPerOwner = 4;
    internal const int MaxLeaseMs = 2000;
    internal const int MaxFrameAgeMs = 250;
    internal const int MaxQueryPoints = 128;
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    internal sealed record Context(int SchemaVersion, string InstanceGeneration, long FrameId, string Stage);
    internal sealed record Registration(int SchemaVersion, string InstanceGeneration, string Name, int LeaseMs);
    internal sealed record Lease(int SchemaVersion, string InstanceGeneration, string OwnerToken, int LeaseMs = 0);
    internal sealed record GoalRequest(Context Context, string OwnerToken, string Category);
    internal sealed record Query(Context Context, float[][] Points);
    internal sealed record Result(bool Accepted, string Reason, string InstanceGeneration, string? OwnerToken = null);

    internal sealed class Owner(string token, string name, long expires, Action<string> callback)
    {
        internal readonly string Token = token;
        internal readonly string Name = name;
        internal readonly Action<string> Callback = callback;
        internal long Expires = expires;
        internal int Revoked;
        internal int GoalRevision;
        internal volatile ContributionGuard? Constraint;
        internal readonly List<Goal> Goals = [];
    }

    internal sealed class Goal(Owner owner, string category, Func<Vector2, float> score, long frame, long expires, NoLossLeaf[]? noLossLeaves = null)
    {
        internal readonly Owner Owner = owner;
        internal volatile ContributionGuard? Constraint = owner.Constraint;
        internal readonly string Category = category;
        internal readonly Func<Vector2, float> Score = score;
        internal readonly long Frame = frame;
        internal readonly long Expires = expires;
        internal readonly NoLossLeaf[]? NoLossLeaves = noLossLeaves;
    }

    private readonly Dictionary<string, Owner> _owners = [];
    private readonly Func<long> _clock;
    private readonly long _frequency;
    private readonly object _gate = new();
    private int _thread;
    private long _frameStarted;
    private volatile bool _disposed;
    private bool _dispatching;
    private string? _scope;
    internal readonly string InstanceGeneration = Guid.NewGuid().ToString("N");
    internal long FrameId { get; private set; }
    internal string Stage { get; private set; } = "unavailable";
    internal bool CanContribute { get; private set; }
    internal Context CurrentContext => new(SchemaVersion, InstanceGeneration, FrameId, Stage);
    private sealed record Publication(string Json, long FrameStarted);
    private Publication? _latest;
    internal string? LatestPublication
    {
        get
        {
            var publication = Volatile.Read(ref _latest);
            return !_disposed && publication != null && _clock() - publication.FrameStarted < Milliseconds(MaxFrameAgeMs) ? publication.Json : null;
        }
    }
    internal bool ProviderAlive => !_disposed;
    internal bool Fresh => !_disposed && FrameId > 0 && Stage != "unavailable" && _clock() - _frameStarted < Milliseconds(MaxFrameAgeMs);
    internal bool HasActiveOwners
    {
        get
        {
            lock (_gate)
            {
                if (_disposed) return false;
                foreach (var owner in _owners.Values)
                    if (Live(owner)) return true;
                return false;
            }
        }
    }

    internal IntegrationSession(Func<long>? clock = null, long frequency = 0)
    {
        _clock = clock ?? Stopwatch.GetTimestamp;
        _frequency = frequency == 0 ? Stopwatch.Frequency : frequency;
    }

    internal static T? Parse<T>(string json) where T : class
    {
        if (json == null || json.Length > 8192)
            return null;
        try { return JsonSerializer.Deserialize<T>(json, Json); }
        catch (JsonException) { return null; }
    }

    internal static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
    private long Milliseconds(int value) => (long)(value / 1000d * _frequency);
    private bool OnThread => Environment.CurrentManagedThreadId == _thread && !_disposed;
    private bool Live(Owner owner) => Volatile.Read(ref owner.Revoked) == 0 && _clock() < Interlocked.Read(ref owner.Expires);
    private Result Reply(bool accepted, string reason, string? token = null) => new(accepted, reason, InstanceGeneration, token);

    internal bool IsOwnerActive(string? token)
    {
        lock (_gate)
            return !_disposed && token != null && _owners.TryGetValue(token, out var owner) && Live(owner);
    }

    // Capture on the framework thread; sampling the result needs no dictionary lock and
    // invokes no consumer callback. Input hooks must never wait for lifecycle operations.
    internal Func<bool>? CaptureOwnerLiveness(string token)
    {
        lock (_gate)
        {
            if (_disposed || !_owners.TryGetValue(token, out var owner) || !Live(owner)) return null;
            return () => !_disposed && Live(owner);
        }
    }

    internal Result Register(Registration? request, Action<string>? callback)
    {
        lock (_gate) return RegisterCore(request, callback);
    }

    private Result RegisterCore(Registration? request, Action<string>? callback)
    {
        if (_disposed || request == null || callback == null || request.SchemaVersion != SchemaVersion || request.InstanceGeneration != InstanceGeneration
            || string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 80 || request.LeaseMs is < 1 or > MaxLeaseMs)
            return Reply(false, "invalid-registration");
        Prune();
        if (_owners.Count >= MaxOwners || _owners.Values.Any(o => o.Name == request.Name))
            return Reply(false, "owner-limit-or-duplicate-name");
        var token = Guid.NewGuid().ToString("N");
        _owners.Add(token, new(token, request.Name, _clock() + Milliseconds(request.LeaseMs), callback));
        return Reply(true, "registered", token);
    }

    internal Result Renew(Lease? request)
    {
        lock (_gate) return RenewCore(request);
    }

    private Result RenewCore(Lease? request)
    {
        if (!ValidLease(request, out var owner) || request!.LeaseMs is < 1 or > MaxLeaseMs)
            return Reply(false, "invalid-or-expired-owner");
        Interlocked.Exchange(ref owner!.Expires, _clock() + Milliseconds(request.LeaseMs));
        return Reply(true, "renewed");
    }

    internal Result Unregister(Lease? request)
    {
        lock (_gate) return UnregisterCore(request);
    }

    private Result UnregisterCore(Lease? request)
    {
        if (!ValidLease(request, out var owner))
            return Reply(false, "invalid-or-expired-owner");
        Revoke(owner!);
        _owners.Remove(owner!.Token);
        return Reply(true, "unregistered");
    }

    internal Result Clear(Lease? request)
    {
        lock (_gate) return ClearCore(request);
    }

    private Result ClearCore(Lease? request)
    {
        if (!ValidLease(request, out var owner))
            return Reply(false, "invalid-or-expired-owner");
        // Invalidate already captured delegates too; do not let a worker continue a cleared contribution.
        owner!.Goals.Clear();
        Interlocked.Increment(ref owner.GoalRevision);
        return Reply(true, "cleared");
    }

    private bool ValidLease(Lease? request, out Owner? owner)
    {
        owner = null;
        return !_disposed && request != null && request.SchemaVersion == SchemaVersion && request.InstanceGeneration == InstanceGeneration
            && request.OwnerToken != null && _owners.TryGetValue(request.OwnerToken, out owner) && Live(owner);
    }

    internal bool Matches(Context? context) => OnThread && context != null && context == CurrentContext && Fresh;

    internal Result Submit(GoalRequest? request, Func<Vector2, float>? score)
    {
        lock (_gate) return SubmitCore(request, score);
    }

    private Result SubmitCore(GoalRequest? request, Func<Vector2, float>? score, NoLossLeaf[]? leaves = null)
    {
        if (request == null || !Matches(request.Context) || Stage != "hints-ready" || !CanContribute || score == null
            || request.Category is not ("combat" or "party-utility" or "mechanic-preference" or "presentation")
            || !ValidLease(new(SchemaVersion, InstanceGeneration, request.OwnerToken), out var owner))
            return Reply(false, "invalid-goal-context-or-owner");
        if (owner!.Goals.Count >= MaxGoalsPerOwner)
            return Reply(false, "goal-limit");
        owner.Goals.Add(new(owner, request.Category, score, FrameId, _frameStarted + Milliseconds(MaxFrameAgeMs), leaves));
        return Reply(true, "submitted");
    }

    private int _goalRevision;

    // Native world/module/hint updates mutate the objects behind query endpoints. Retire
    // the previous context before that mutation begins, even if the next hint build fails.
    internal void BeginWorldUpdate()
    {
        lock (_gate)
        {
            if (!OnThread || _dispatching)
                return;
            Stage = "unavailable";
            CanContribute = false;
            Volatile.Write(ref _latest, null);
        }
    }

    // Called exactly once after BMR has generated encounter/zone hints, before manual actions and rotation.
    internal void BeginFrame(string scope, bool canContribute)
    {
        // Initialization may run on a different thread from UiBuilder.Draw. Bind queries to
        // BMR's actual frame executor, while allowing lifecycle calls from consumer unload/update.
        Interlocked.CompareExchange(ref _thread, Environment.CurrentManagedThreadId, 0);
        lock (_gate) BeginFrameCore(scope, canContribute);
    }

    private void BeginFrameCore(string scope, bool canContribute)
    {
        if (!OnThread || _dispatching)
            return;
        if (_scope != scope)
        {
            // Immutable workers keep their original bounded source lifetime, but must
            // never carry a preference across player/zone/preset changes.
            Interlocked.Increment(ref _goalRevision);
            _scope = scope;
        }
        Volatile.Write(ref _latest, null);
        ++FrameId;
        _frameStarted = _clock();
        Stage = "hints-ready";
        CanContribute = canContribute;
        foreach (var owner in _owners.Values)
        {
            owner.Goals.Clear();
            owner.Constraint = null;
        }
        if (!canContribute)
            Interlocked.Increment(ref _goalRevision);
        Prune();
    }

    internal bool Publish(string stage, Func<Context, string> snapshot)
    {
        if (!OnThread || FrameId == 0 || _dispatching || !((Stage == "hints-ready" && stage is "hints-ready" or "movement-proposed")
            || (Stage == "movement-proposed" && stage == "movement-applied")))
            return false;
        // No stage can be dispatched twice, even if a consumer re-enters an IPC endpoint.
        if (_lastPublishedFrame == FrameId && _lastPublishedStage == stage)
            return false;
        Stage = stage;
        _lastPublishedFrame = FrameId;
        _lastPublishedStage = stage;
        string json;
        try { json = snapshot(CurrentContext); }
        catch
        {
            CanContribute = false;
            Interlocked.Increment(ref _goalRevision);
            json = Serialize(new { available = false, reason = "snapshot-unavailable", context = CurrentContext });
        }
        Volatile.Write(ref _latest, new(json, _frameStarted));
        Owner[] owners;
        lock (_gate) owners = _owners.Values.ToArray();
        _dispatching = true;
        try
        {
            foreach (var owner in owners)
            {
                if (!Live(owner))
                    continue;
                try { owner.Callback(json); }
                catch { Revoke(owner); }
            }
        }
        finally { _dispatching = false; }
        lock (_gate) Prune();
        return true;
    }

    private long _lastPublishedFrame;
    private string? _lastPublishedStage;

    internal Func<Vector2, float>[] CaptureGoals(string category, Func<Vector2[], Func<Vector2, bool>?>? captureGuard = null)
    {
        lock (_gate) return CaptureGoalsCore(category, captureGuard);
    }

    private Func<Vector2, float>[] CaptureGoalsCore(string category, Func<Vector2[], Func<Vector2, bool>?>? captureGuard)
    {
        if (!OnThread || !CanContribute)
            return [];
        var revision = Volatile.Read(ref _goalRevision);
        return _owners.Values.Where(Live).SelectMany(o => o.Goals).Where(g => g.Frame == FrameId && g.Category == category)
            .Select(g => Wrap(g, revision, CaptureNoLossScore(g, captureGuard))).ToArray();
    }

    private Func<Vector2, float> Wrap(Goal goal, int revision, Func<Vector2, float> score)
    {
        var ownerRevision = Volatile.Read(ref goal.Owner.GoalRevision);
        var calls = 0;
        return p => Interlocked.Increment(ref calls) <= 262144 ? Evaluate(goal, revision, ownerRevision, p, score) : 0f;
    }

    private float Evaluate(Goal goal, int revision, int ownerRevision, Vector2 point, Func<Vector2, float> score)
    {
        if (revision != Volatile.Read(ref _goalRevision) || ownerRevision != Volatile.Read(ref goal.Owner.GoalRevision)
            || !Live(goal.Owner) || _clock() >= goal.Expires || goal.Constraint is { Live: false })
            return 0f;
        try
        {
            var start = _clock();
            var value = score(point);
            if (!float.IsFinite(value) || value is < 0 or > 16 || _clock() - start > Milliseconds(2))
            {
                Revoke(goal.Owner);
                return 0f;
            }
            // A scorer can already be running when the native frame or owner is retired.
            // Its eventual return must not republish a contribution that was cleared meanwhile.
            if (revision != Volatile.Read(ref _goalRevision) || ownerRevision != Volatile.Read(ref goal.Owner.GoalRevision)
                || !Live(goal.Owner) || _clock() >= goal.Expires || goal.Constraint is { Live: false })
                return 0f;
            return value;
        }
        catch { Revoke(goal.Owner); return 0f; }
    }

    private static void Revoke(Owner owner) => Interlocked.Exchange(ref owner.Revoked, 1);
    private void Prune()
    {
        foreach (var owner in _owners.Values.Where(o => !Live(o)).ToArray())
        {
            Revoke(owner);
            _owners.Remove(owner.Token);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var owner in _owners.Values)
                Revoke(owner);
            _owners.Clear();
            ++_goalRevision;
            Stage = "unavailable";
            Volatile.Write(ref _latest, null);
            _disposed = true;
        }
    }
}
