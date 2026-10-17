using BossMod.Integration;

static partial class Checks
{
    private sealed class LeaseResource(string key, int maximum = 2000) : IntegrationLeases.IResource
    {
        public string Key => key;
        public int MaxLeaseMs => maximum;
        internal string Value = "original";
        private string? _lease;
        private string? _original;
        private long _revision;
        internal bool ThrowAfterApply;
        internal bool ThrowReleaseOnce;
        internal bool RejectRenewal;
        internal int Renewals;
        internal bool ThrowRenewOnce;
        public IntegrationLeases.State Read() => new(Value, _revision, _lease);
        public IntegrationLeases.Write Apply(string id, IntegrationLeases.State expected, string value)
        {
            if (expected.Revision != _revision || expected.Value != Value || _lease != null && _lease != id)
                return new(false, "changed", _revision);
            _original ??= Value;
            Value = value; _lease = id; ++_revision;
            if (ThrowAfterApply) throw new InvalidOperationException("applied-before-failure");
            return new(true, "applied", _revision);
        }
        public bool Release(string id, long revision)
        {
            if (ThrowReleaseOnce) { ThrowReleaseOnce = false; throw new InvalidOperationException("temporary-cleanup-failure"); }
            if (_lease != id || _revision != revision) return false;
            Value = _original!; _lease = null; _original = null; ++_revision;
            return true;
        }
        public bool Renew(string id, long revision, int duration)
        {
            ++Renewals;
            if (ThrowRenewOnce) { ThrowRenewOnce = false; throw new InvalidOperationException("backend-renewal-failed"); }
            return !RejectRenewal;
        }
        internal void UserChange(string value) { Value = value; _lease = null; _original = null; ++_revision; }
    }

    private static void LeaseLifetimes()
    {
        var owners = new HashSet<string> { "a", "b" };
        long now = 100;
        var errors = 0;
        using var store = new IntegrationLeases(owners.Contains, _ => ++errors, () => now, 1000);
        IntegrationLeases.Result Acquire(LeaseResource target, string owner, string scope = "scope", int duration = 1000)
        {
            var state = target.Read();
            return store.Apply(owner, null, scope, target, state.Revision, state.Value, "borrowed", duration);
        }
        var first = new LeaseResource("first"); var second = new LeaseResource("second");
        var a = Acquire(first, "a"); var b = Acquire(second, "b");
        Check(a.Accepted && b.Accepted && first.Value == "borrowed" && second.Value == "borrowed", "independent owners can lease different resources");
        Check(!Acquire(first, "b").Accepted && !store.Release("b", a.LeaseId!).Accepted, "an owner cannot replace or release another owner");
        Check(store.Renew("a", a.LeaseId!, "scope", 1000).Accepted, "renew validates current ownership");
        owners.Remove("a"); store.Sweep("scope", true);
        Check(first.Value == "original" && second.Value == "borrowed", "owner disappearance restores only its own resources");
        owners.Add("a");
        store.Sweep("new-preset", true);
        Check(second.Value == "original", "preset/territory scope change restores the previous borrow");
        a = Acquire(first, "a");
        store.Sweep("scope", false);
        Check(first.Value == "original", "manual intervention/death suppresses and restores controls");
        a = Acquire(first, "a", duration: 100);
        now += 101;
        Check(!store.Renew("a", a.LeaseId!, "scope", 1000).Accepted, "expired resource lease cannot be revived");
        store.Sweep("scope", true);
        Check(first.Value == "original", "monotonic expiry restores the original resource value");
        a = Acquire(first, "a");
        first.UserChange("borrowed");
        Check(!store.Renew("a", a.LeaseId!, "scope", 1000).Accepted && first.Value == "borrowed", "equal-value explicit user intent revokes renewal without overwriting it");
        Check(!store.Release("a", a.LeaseId!).Accepted && first.Value == "borrowed", "released/lost ownership cannot later restore a user's chosen value");
        first.UserChange("original");
        var expected = first.Read(); first.UserChange("other"); first.UserChange("original");
        Check(!store.Apply("a", null, "scope", first, expected.Revision, expected.Value, "borrowed", 100).Accepted, "away-and-back user revision rejects stale acquisition");
        var failed = new LeaseResource("failed") { ThrowAfterApply = true };
        Check(!Acquire(failed, "a").Accepted && failed.Value == "original", "failure after application rolls back its own pending borrow");
        var cleanup = new LeaseResource("cleanup");
        var lease = Acquire(cleanup, "a"); cleanup.ThrowReleaseOnce = true;
        Check(!store.Release("a", lease.LeaseId!).Accepted && cleanup.Value == "borrowed", "failed cleanup is retained for recovery");
        store.Sweep("scope", true);
        Check(cleanup.Value == "original", "failed cleanup retries before the ordinary TTL, even while the owner remains live");
        var unbounded = new LeaseResource("unbounded", 5000);
        lease = Acquire(unbounded, "a");
        Check(lease.Accepted && !store.Renew("a", lease.LeaseId!, "scope", 3000).Accepted, "global resource duration cannot be widened by a backend policy");
        store.Release("a", lease.LeaseId!);
        Check(!Acquire(first, "unknown").Accepted && !Acquire(first, "a", duration: 2001).Accepted, "invalid owner and excessive duration rejected");
        var hardExpiry = new LeaseResource("hard-expiry");
        var hardLease = Acquire(hardExpiry, "a");
        Check(hardLease.Accepted && hardExpiry.Renewals == 1, "backend native expiry is armed during acquisition");
        Check(store.Renew("a", hardLease.LeaseId!, "scope", 100).Accepted && hardExpiry.Renewals == 2,
            "owner renewal also renews the backend's independently enforced lifetime");
        hardExpiry.RejectRenewal = true;
        Check(!store.Renew("a", hardLease.LeaseId!, "scope", 100).Accepted && hardExpiry.Value == "original",
            "backend renewal rejection releases its existing borrow without extending store expiry");
        Check(!Acquire(hardExpiry, "a").Accepted && hardExpiry.Value == "original",
            "initial backend lifetime failure rolls back the pending acquisition");
        var renewalFailure = new LeaseResource("renewal-failure");
        var failedLease = Acquire(renewalFailure, "a");
        renewalFailure.ThrowRenewOnce = true;
        Check(!store.Renew("a", failedLease.LeaseId!, "scope", 100).Accepted && renewalFailure.Value == "original",
            "an exception during renewal triggers immediate cleanup rather than leaving a live abandoned borrow");
        var unload = new LeaseResource("unload") { ThrowReleaseOnce = true };
        Acquire(unload, "a"); Acquire(second, "b");
        store.Dispose();
        Check(unload.Value == "original" && second.Value == "original", "unload restores resources and recovers a transient cleanup failure");
        Check(!Acquire(first, "a").Accepted, "disposed provider cannot accept a new resource lease");
        Check(errors == 4, "resource errors are isolated and reported once per failed cleanup entry");
    }
}
