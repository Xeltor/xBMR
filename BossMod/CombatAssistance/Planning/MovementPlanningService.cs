using System.Diagnostics;
using System.Threading;

namespace BossMod.CombatAssistance.Planning;

// Framework-thread owner. A worker sees only the immutable captured context.
// Revision is supplied by intent collection and changes on meaningful input/authority changes.
// Equivalent observations update the pending snapshot without cancelling useful work every frame.
internal sealed class MovementPlanningService : IDisposable
{
    private sealed record Request(string Owner, long Revision, MovementPlanningContext Context);
    private sealed class Mailbox
    {
        private MovementPlan? _best;
        internal void Publish(MovementPlan plan) => Volatile.Write(ref _best, plan);
        internal MovementPlan? Read() => Volatile.Read(ref _best);
    }
    private readonly Func<MovementPlanningContext, CancellationToken, Action<MovementPlan>, MovementPlan> _search;
    private readonly Func<long> _clock;
    private readonly Pathfinding.NavigationDecision.Context _recoveryContext = new();
    private MovementSearchCost? _lastWorkerCost;
    internal MovementSearchCost? LastWorkerCost => Volatile.Read(ref _lastWorkerCost);
    private Request? _pending;
    private Request? _running;
    private Request? _published;
    private Request? _lastAttempt;
    private Task<MovementPlan>? _worker;
    private CancellationTokenSource? _cancellation;
    private MovementPlan? _plan;
    private Mailbox? _mailbox;
    private bool _disposed;
    internal string? LastFailure { get; private set; }
    internal bool Busy => _worker is { IsCompleted: false };

    internal MovementPlanningService(Func<MovementPlanningContext, CancellationToken, MovementPlan>? search = null, Func<long>? clock = null)
    {
        _search = search == null ? ((context, cancel, publish) => NativeMovementSearch.Run(context, cancel, publish, _recoveryContext))
            : ((context, cancel, _) => search(context, cancel));
        _clock = clock ?? Stopwatch.GetTimestamp;
    }

    internal MovementPlanningService(Func<MovementPlanningContext, CancellationToken, Action<MovementPlan>, MovementPlan> search, Func<long>? clock = null)
    {
        _search = search;
        _clock = clock ?? Stopwatch.GetTimestamp;
    }

    internal void Submit(string owner, long revision, MovementPlanningContext context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        _pending = new(owner, revision, context);
        // A moving target can refresh soft geometry every frame. Cancel the old
        // search, but do not throw away a complete route it already proved when
        // that exact route also passes the new capture. This avoids starvation
        // without tolerances on hazards, recipients or manual authority.
        var carried = CarryVerifiedAlternative(_running, _mailbox?.Read(), _pending)
            ?? CarryVerifiedAlternative(_published, _plan, _pending);
        // A utility or mobility lock/readiness estimate alone must not starve the search.
        // No result is executable through this exception: publication must still
        // pass full route/action/resource validation against the latest capture.
        if (_running is { } running && !SameIntent(running, _pending)
            && !(running.Owner == owner && context.SameProblem(running.Context,
                continuingVerifiedRoute: true, forRevalidation: true)))
            _cancellation?.Cancel();
        if (_published is { } published && !SameIntent(published, _pending))
        {
            _published = null;
            _plan = null;
        }
        if (carried != null)
        {
            _plan = carried;
            _published = _pending;
        }
        Pump();
    }

    private MovementPlan? CarryVerifiedAlternative(Request? source, MovementPlan? plan, Request next)
    {
        if (source == null || plan is not { HasVerifiedRoute: true } || source.Owner != next.Owner
            || source.Revision == next.Revision || !next.Context.Optimize
            || source.Context.Actions.Length != next.Context.Actions.Length
            || !next.Context.PreservesPresent(plan.DeliveredActions) || !next.Context.CompletePreparation(plan.DeliveredActions))
            return null;
        for (var i = 0; i < next.Context.Actions.Length; ++i)
        {
            var before = source.Context.Actions[i];
            var now = next.Context.Actions[i];
            if (before.Id != now.Id || before.TargetId != now.TargetId || before.RequiredRecipients != now.RequiredRecipients
                || before.RequiredEarlierActions != now.RequiredEarlierActions || !before.RecipientIds.SequenceEqual(now.RecipientIds)
                || (before.Opportunity == null) != (now.Opportunity == null)
                || before.Opportunity != null && !before.Opportunity.Recipients.Where(r => r.Required).Select(r => r.Id)
                    .SequenceEqual(now.Opportunity!.Recipients.Where(r => r.Required).Select(r => r.Id)))
                return null;
        }
        if (!MovementPlanValidation.Validate(plan, next.Context, 0, true, _clock()).Allowed) return null;
        // Keep the original input timestamps: a successful recheck never renews
        // freshness, departure or an action deadline. Completion only concerned
        // the old input, so this publication is explicitly bounded for the new one.
        return plan with
        {
            Status = MovementSearchStatus.Bounded,
            Bound = MovementSearchBound.InputChanged,
            NativeRecovery = null,
            OptimizationPending = false,
            Evidence = "complete prior alternative revalidated against changed input; replacement search pending"
        };
    }

    internal MovementPlan? Poll(string owner, long revision, DateTime worldTime)
    {
        if (_disposed)
            return null;
        Pump();
        if (_published is not { } published || published.Owner != owner || published.Revision != revision || _plan is not { } plan)
            return null;
        return Fresh(plan, worldTime) ? plan : null;
    }

    internal void Release(string owner)
    {
        if (_pending?.Owner == owner)
            _pending = null;
        if (_running?.Owner == owner)
            _cancellation?.Cancel();
        if (_published?.Owner == owner)
        {
            _published = null;
            _plan = null;
        }
    }

    private void Pump()
    {
        if (_running != null && _pending != null && SameIntent(_running, _pending)
            && _cancellation?.IsCancellationRequested != true && _mailbox?.Read() is { } best)
        {
            _plan = best;
            _published = _running;
        }
        if (_worker is { IsCompleted: true } finished)
        {
            try
            {
                var result = finished.GetAwaiter().GetResult(); // completed only; never waits on optimization
                if (_running != null && _pending != null && SameIntent(_running, _pending)
                    && _cancellation?.IsCancellationRequested != true && result.Status != MovementSearchStatus.Invalidated)
                {
                    _plan = result;
                    _published = _running;
                    LastFailure = null;
                }
                else if (_running != null && _pending != null && _cancellation?.IsCancellationRequested != true
                    && CarryVerifiedAlternative(_running, result, _pending) is { } carried)
                {
                    _plan = carried;
                    _published = _pending;
                    LastFailure = null;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LastFailure = ex.GetType().Name + ": " + ex.Message;
            }
            _worker = null;
            _running = null;
            _cancellation?.Dispose();
            _cancellation = null;
            _mailbox = null;
        }
        if (_worker != null || _pending == null || _disposed)
            return;
        // A completed equivalent search has already considered its alternatives.
        // Continue consuming it while it is fresh instead of launching it again
        // on every frame. Changed intent and the native freshness ceiling still
        // require new work; no recheck renews the published input timestamps.
        if (_published is { } complete && SameIntent(complete, _pending)
            && _plan is { Status: MovementSearchStatus.Complete } settled
            && Fresh(settled, _pending.Context.Identity.WorldTime))
            return;
        // Do not repeatedly search an identical capture. A later equivalent capture is still useful.
        if (_lastAttempt == _pending)
            return;
        _running = _pending;
        _lastAttempt = _running;
        var context = _running.Context;
        _cancellation = new();
        var cancel = _cancellation.Token;
        var mailbox = _mailbox = new Mailbox();
        _worker = Task.Run(() =>
        {
            var started = Stopwatch.GetTimestamp();
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            var cancelled = false;
            try { return _search(context, cancel, mailbox.Publish); }
            catch (OperationCanceledException) { cancelled = true; throw; }
            finally
            {
                Volatile.Write(ref _lastWorkerCost, new(Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    GC.GetAllocatedBytesForCurrentThread() - allocated, cancelled));
            }
        });
        // Observe failures even if the plugin unloads and Pump is never called again.
        _ = _worker.ContinueWith(static task => { _ = task.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private static bool SameIntent(Request a, Request b) => a.Owner == b.Owner && a.Revision == b.Revision
        && a.Context.Identity.ActorId == b.Context.Identity.ActorId && a.Context.Identity.ZoneId == b.Context.Identity.ZoneId
        && a.Context.Identity.ContentId == b.Context.Identity.ContentId;

    private bool Fresh(MovementPlan plan, DateTime worldTime)
    {
        var elapsed = (_clock() - plan.Input.CapturedTimestamp) / (double)Stopwatch.Frequency;
        var worldElapsed = (worldTime - plan.Input.WorldTime).TotalSeconds;
        // Both clocks matter: pauses in WorldState cannot renew an old deadline.
        return elapsed is >= 0 and < 0.25 && worldElapsed is >= 0 and < 0.25;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _pending = _published = null;
        _mailbox = null;
        _plan = null;
        _cancellation?.Cancel();
        // The worker owns only frozen data. Cleanup must not wait for it on the framework thread.
        if (_worker is { } worker && _cancellation is { } cancellation)
            _ = worker.ContinueWith(_ => cancellation.Dispose(), CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        else
            _cancellation?.Dispose();
        _cancellation = null;
    }
}
