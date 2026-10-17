using System.Diagnostics;

namespace BossMod.Integration;

internal sealed class IntegrationLeases(Func<string, bool> ownerAlive, Action<Exception>? failure = null,
    Func<long>? clock = null, long frequency = 0) : IDisposable
{
    internal const int MaxLeases = 32;
    internal sealed record State(string Value, long Revision, string? LeaseId);
    internal sealed record Write(bool Accepted, string Reason, long Revision);
    internal sealed record Result(bool Accepted, string Reason, string? LeaseId = null, long? ResourceRevision = null);
    internal interface IResource
    {
        string Key { get; }
        int MaxLeaseMs { get; }
        State Read();
        Write Apply(string leaseId, State expected, string value);
        bool Release(string leaseId, long revision);
        bool Renew(string leaseId, long revision, int leaseMs) => true;
    }
    private sealed class Entry(string id, string owner, string scope, IResource resource, long expires)
    {
        internal readonly string Id = id;
        internal readonly string Owner = owner;
        internal readonly string Scope = scope;
        internal readonly IResource Resource = resource;
        internal long Expires = expires;
        internal long Revision;
        internal bool Pending;
        internal bool CleanupPending;
        internal bool LoggedFailure;
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = [];
    private readonly Func<long> _clock = clock ?? Stopwatch.GetTimestamp;
    private readonly long _frequency = frequency == 0 ? Stopwatch.Frequency : frequency;
    private bool _disposed;
    private long Ticks(int ms) => (long)(ms / 1000d * _frequency);

    internal Result Apply(string owner, string? leaseId, string scope, IResource resource, long expectedRevision,
        string expectedValue, string value, int leaseMs)
    {
        lock (_gate)
        {
            if (_disposed || !ownerAlive(owner) || leaseMs < 1 || leaseMs > resource.MaxLeaseMs || leaseMs > IntegrationSession.MaxLeaseMs)
                return new(false, "invalid-owner-or-duration");
            var entry = _entries.Values.FirstOrDefault(e => e.Resource.Key == resource.Key);
            if (entry is { Pending: false, CleanupPending: false } observedEntry)
            {
                try
                {
                    if (observedEntry.Resource.Read().LeaseId != observedEntry.Id)
                    {
                        // A native user change/expiry already removed the old runtime layer.
                        // Do not reserve an unowned resource until that owner's original TTL.
                        _entries.Remove(observedEntry.Id);
                        entry = null;
                    }
                }
                catch (Exception error)
                {
                    Log(observedEntry, error);
                    return new(false, "resource-unavailable");
                }
            }
            if (entry != null && (entry.Owner != owner || entry.Id != leaseId || entry.Scope != scope || entry.Pending || entry.CleanupPending || entry.Expires <= _clock()))
                return new(false, "resource-owned-or-stale-lease");
            if (entry == null && (leaseId != null || _entries.Count >= MaxLeases))
                return new(false, "unknown-lease-or-limit");
            resource = entry?.Resource ?? resource;
            try
            {
                var current = resource.Read();
                if (current.Revision != expectedRevision || current.Value != expectedValue
                    || entry != null && (current.LeaseId != entry.Id || current.Revision != entry.Revision))
                    return new(false, "changed-resource");
                entry ??= new(Guid.NewGuid().ToString("N"), owner, scope, resource, _clock() + Ticks(leaseMs));
                _entries[entry.Id] = entry;
                entry.Pending = true;
                var result = resource.Apply(entry.Id, current, value);
                entry.Pending = false;
                if (!result.Accepted || !ownerAlive(owner) || !resource.Renew(entry.Id, result.Revision, leaseMs))
                {
                    Cleanup(entry);
                    return new(false, result.Accepted ? "owner-or-backend-lost-during-apply" : result.Reason);
                }
                entry.Revision = result.Revision;
                entry.Expires = _clock() + Ticks(leaseMs);
                return new(true, "applied", entry.Id, entry.Revision);
            }
            catch (Exception error)
            {
                failure?.Invoke(error);
                if (entry != null)
                {
                    entry.Pending = false;
                    Cleanup(entry);
                }
                return new(false, "resource-unavailable");
            }
        }
    }

    internal Result Renew(string owner, string leaseId, string scope, int leaseMs)
    {
        lock (_gate)
        {
            if (_disposed || !_entries.TryGetValue(leaseId, out var entry) || entry.Owner != owner || entry.Scope != scope
                || entry.Pending || entry.CleanupPending || entry.Expires <= _clock() || !ownerAlive(owner) || leaseMs < 1
                || leaseMs > entry.Resource.MaxLeaseMs || leaseMs > IntegrationSession.MaxLeaseMs)
                return new(false, "invalid-or-expired-lease");
            try
            {
                var state = entry.Resource.Read();
                if (state.LeaseId != entry.Id || state.Revision != entry.Revision)
                {
                    _entries.Remove(entry.Id); // a user/new owner took over; never restore its value
                    return new(false, "ownership-lost");
                }
                if (!entry.Resource.Renew(entry.Id, entry.Revision, leaseMs))
                {
                    Cleanup(entry);
                    return new(false, "backend-renewal-rejected");
                }
                entry.Expires = _clock() + Ticks(leaseMs);
                return new(true, "renewed", entry.Id, entry.Revision);
            }
            catch (Exception error)
            {
                Log(entry, error);
                entry.CleanupPending = true;
                Cleanup(entry);
                return new(false, "resource-unavailable");
            }
        }
    }

    internal Result Release(string owner, string leaseId)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(leaseId, out var entry) || entry.Owner != owner || entry.Pending)
                return new(false, "unknown-lease-or-owner");
            return Cleanup(entry) ? new(true, "released") : new(false, "cleanup-pending");
        }
    }

    internal void Sweep(string scope, bool controlsAllowed)
    {
        lock (_gate)
        {
            foreach (var entry in _entries.Values.ToArray())
            {
                if (!entry.Pending && (_disposed || entry.CleanupPending || !controlsAllowed || entry.Scope != scope || entry.Expires <= _clock() || !ownerAlive(entry.Owner)))
                    Cleanup(entry);
            }
        }
    }

    private bool Cleanup(Entry entry)
    {
        try
        {
            var current = entry.Resource.Read();
            if (current.LeaseId == entry.Id)
            {
                // Includes recovery from a target that threw after applying but before returning its revision.
                if (!entry.Resource.Release(entry.Id, current.Revision))
                {
                    entry.CleanupPending = true;
                    return false;
                }
            }
            _entries.Remove(entry.Id);
            return true;
        }
        catch (Exception error) { entry.CleanupPending = true; Log(entry, error); return false; }
    }

    private void Log(Entry entry, Exception error)
    {
        if (!entry.LoggedFailure)
        {
            entry.LoggedFailure = true;
            failure?.Invoke(error);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            foreach (var entry in _entries.Values.ToArray())
                for (var attempt = 0; attempt < 3 && !Cleanup(entry); ++attempt) { }
        }
    }
}
