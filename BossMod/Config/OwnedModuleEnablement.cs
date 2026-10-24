using System.Diagnostics;
using System.Threading;

namespace BossMod;

// Runtime-only per-OID overrides. The saved disabled list is never borrowed or rewritten.
internal sealed class OwnedModuleEnablement(Func<long>? clock = null, long frequency = 0)
{
    internal const int MaxWatches = 128;
    internal const int MaxOwners = 32;
    internal sealed record State(bool SavedEnabled, bool Enabled, long Revision, long UserRevision, string? LeaseId);
    private sealed class Watch(bool saved, long revision)
    {
        internal bool Saved = saved;
        internal bool Value = saved;
        internal long Revision = revision;
        internal long UserRevision = revision;
        internal string? Owner;
        internal long Expires;
        internal Func<bool>? Live;
        internal LocalScope? Scope;
        internal int Pins;
    }
    private readonly Dictionary<uint, Watch> _watches = [];
    private readonly object _gate = new();
    private readonly Func<long> _clock = clock ?? Stopwatch.GetTimestamp;
    private readonly long _frequency = frequency == 0 ? Stopwatch.Frequency : frequency;
    private long _revision;
    private long _version;
    internal long Version => Interlocked.Read(ref _version);

    internal uint[] WatchedOids()
    {
        lock (_gate) return _watches.Keys.ToArray();
    }

    private void Clear(Watch watch)
    {
        if (watch.Scope != null)
            watch.Scope.Active = false;
        watch.Scope = null;
        watch.Owner = null;
        watch.Live = null;
        watch.Expires = 0;
        watch.Value = watch.Saved;
        watch.Revision = ++_revision;
        Interlocked.Increment(ref _version);
    }

    private void CheckOwner(Watch watch)
    {
        if (watch.Scope is { Active: true }) return;
        if (watch.Owner == null) return;
        var live = false;
        try { live = _clock() < watch.Expires && watch.Live?.Invoke() == true; }
        catch { /* A failed consumer probe can only remove its own runtime override. */ }
        if (!live) Clear(watch);
    }

    private static State Copy(Watch watch) => new(watch.Saved, watch.Value, watch.Revision, watch.UserRevision, watch.Owner);

    // Called on the native frame/UI thread with the currently configured value. Passive native
    // reads do not grow the watch table; acquisition/catalog observation opts in explicitly.
    internal State? Observe(uint oid, bool savedEnabled, bool track)
    {
        lock (_gate)
        {
            if (!_watches.TryGetValue(oid, out var watch))
            {
                if (!track) return null;
                if (_watches.Count >= MaxWatches)
                {
                    foreach (var candidate in _watches.Values) CheckOwner(candidate);
                    var old = _watches.FirstOrDefault(p => p.Value.Owner == null && p.Value.Pins == 0);
                    if (old.Value == null) return null;
                    _watches.Remove(old.Key);
                }
                _watches[oid] = watch = new(savedEnabled, ++_revision);
            }
            if (watch.Saved != savedEnabled)
            {
                watch.Saved = savedEnabled;
                watch.UserRevision = ++_revision;
                Clear(watch);
            }
            CheckOwner(watch);
            return Copy(watch);
        }
    }

    // Lifecycle callers read only the last native observation; no configuration arrays/game
    // state are touched from a consumer's unload/renew thread.
    internal State? Read(uint oid)
    {
        lock (_gate)
        {
            if (!_watches.TryGetValue(oid, out var watch)) return null;
            CheckOwner(watch);
            return Copy(watch);
        }
    }

    internal void UserIntent(uint oid, bool enabled)
    {
        lock (_gate)
        {
            if (!_watches.TryGetValue(oid, out var watch)) return;
            watch.Saved = enabled;
            watch.UserRevision = ++_revision;
            Clear(watch); // Includes an explicit equal-value choice and changes away then back.
        }
    }

    internal bool Apply(uint oid, string leaseId, long expectedRevision, bool enabled, Func<bool> live)
    {
        lock (_gate)
        {
            if (!_watches.TryGetValue(oid, out var watch) || string.IsNullOrWhiteSpace(leaseId) || live == null) return false;
            CheckOwner(watch);
            if (watch.Scope != null || watch.Revision != expectedRevision || watch.Owner != null && watch.Owner != leaseId
                || watch.Owner == null && _watches.Values.Count(w => w.Owner != null) >= MaxOwners) return false;
            watch.Owner = leaseId;
            watch.Value = enabled;
            watch.Live = live;
            // The generic lease store arms the exact expiry before returning accepted.
            watch.Expires = _clock() + _frequency * 2;
            watch.Revision = ++_revision;
            Interlocked.Increment(ref _version);
            CheckOwner(watch);
            return watch.Owner == leaseId;
        }
    }

    internal bool Renew(uint oid, string leaseId, long revision, int leaseMs)
    {
        lock (_gate)
        {
            if (!_watches.TryGetValue(oid, out var watch)) return false;
            CheckOwner(watch);
            if (watch.Scope != null || watch.Owner != leaseId || watch.Revision != revision || leaseMs is < 1 or > 2000) return false;
            watch.Expires = _clock() + (long)(leaseMs / 1000d * _frequency);
            return true;
        }
    }

    internal bool Release(uint oid, string leaseId, long revision)
    {
        lock (_gate)
        {
            if (!_watches.TryGetValue(oid, out var watch) || watch.Scope != null || watch.Owner != leaseId || watch.Revision != revision) return false;
            Clear(watch);
            return true;
        }
    }

    // Native lifetime ownership has no consumer renewal, expiry, reflection or IPC owner limit.
    // The same underlying saved-value and user-revision rules govern both transitional paths.
    internal sealed class LocalScope(OwnedModuleEnablement owner, uint oid) : IDisposable
    {
        internal bool Active { get; set; } = true;
        public void Dispose()
        {
            lock (owner._gate)
            {
                if (owner._watches.TryGetValue(oid, out var watch) && ReferenceEquals(watch.Scope, this))
                    owner.Clear(watch);
                Active = false;
            }
        }
    }

    internal LocalScope? AcquireLocal(uint oid, bool savedEnabled, bool enabled)
    {
        lock (_gate)
        {
            if (!_watches.TryGetValue(oid, out var watch))
                _watches[oid] = watch = new(savedEnabled, ++_revision);
            else
                _ = Observe(oid, savedEnabled, false);
            if (watch.Owner != null)
                return null;
            var scope = new LocalScope(this, oid);
            watch.Scope = scope;
            watch.Owner = "native:" + Guid.NewGuid().ToString("N"); // transitional external observers see an occupied resource
            watch.Value = enabled;
            watch.Revision = ++_revision;
            Interlocked.Increment(ref _version);
            return scope;
        }
    }

    // A native loaded instance pins its provenance so catalog paging cannot evict it and
    // accidentally turn owner expiry into an active-encounter unload.
    internal bool Pin(uint oid)
    {
        lock (_gate)
        {
            if (!_watches.TryGetValue(oid, out var watch)) return false;
            ++watch.Pins;
            return true;
        }
    }

    internal void Unpin(uint oid)
    {
        lock (_gate)
            if (_watches.TryGetValue(oid, out var watch) && watch.Pins > 0) --watch.Pins;
    }
}
