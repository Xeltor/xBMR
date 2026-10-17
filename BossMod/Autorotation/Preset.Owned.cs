using System.Diagnostics;

namespace BossMod.Autorotation;

public sealed partial record class Preset
{
    public partial record class ModuleSettings
    {
        internal sealed record OwnedStrategyState(bool Available, StrategyValue? Value, long Revision, string? LeaseId);
        private sealed record NativeSetting(Modifier Modifier, StrategyValue Reference, StrategyValue Copy);
        private sealed record NativeSettings(NativeSetting[] Saved, NativeSetting[] Transient);
        private sealed record StrategyWatch(NativeSettings Native, long Revision);
        private sealed record StrategyOwner(string LeaseId, StrategyValue Value, long Revision, long Expires, Func<bool> IsLive);
        private readonly object _ownedStrategyGate = new();
        private readonly Dictionary<int, StrategyWatch> _strategyWatches = [];
        private readonly Dictionary<int, StrategyOwner> _strategyOwners = [];
        private long _ownedStrategyRevision;

        private NativeSettings? CaptureNativeStrategy(int track)
        {
            // Bound control work independently of the native UI/preset implementation.
            if (SerializedSettings.Count + TransientSettings.Count > 256)
                return null;
            NativeSetting[] Capture(List<ModuleSetting> values) => values.Where(s => s.Track == track)
                .Select(s => new NativeSetting(s.Mod, s.Value, s.Value with { })).ToArray();
            return new(Capture(SerializedSettings), Capture(TransientSettings));
        }

        private static bool Same(NativeSetting[] left, NativeSetting[] right) => left.Length == right.Length
            && left.Zip(right).All(p => p.First.Modifier == p.Second.Modifier && ReferenceEquals(p.First.Reference, p.Second.Reference)
                && p.First.Copy == p.Second.Copy);

        private StrategyWatch? ObserveNativeStrategy(int track)
        {
            if ((uint)track >= (uint)Definition.Configs.Count)
            {
                _strategyOwners.Remove(track);
                return null;
            }
            if (!_strategyWatches.ContainsKey(track) && _strategyWatches.Count >= 128)
                return null;
            var native = CaptureNativeStrategy(track);
            if (native == null)
            {
                _strategyOwners.Remove(track);
                return null;
            }
            if (!_strategyWatches.TryGetValue(track, out var watch) || !Same(watch.Native.Saved, native.Saved) || !Same(watch.Native.Transient, native.Transient))
            {
                _strategyOwners.Remove(track); // a later native/user write takes authority, even at an equal value
                _strategyWatches[track] = watch = new(native, ++_ownedStrategyRevision);
            }
            if (_strategyOwners.TryGetValue(track, out var owner) && (owner.Expires <= Stopwatch.GetTimestamp() || !OwnerLive(owner)))
            {
                _strategyOwners.Remove(track);
                _strategyWatches[track] = watch = watch with { Revision = ++_ownedStrategyRevision };
            }
            return watch;
        }

        private static bool OwnerLive(StrategyOwner owner)
        {
            try { return owner.IsLive(); }
            catch { return false; } // a failed integration cannot break native strategy evaluation
        }

        private StrategyValue NativeValue(int track)
        {
            var value = Definition.Configs[track].CreateEmpty();
            foreach (var setting in SerializedSettings)
                if (setting.Track == track && setting.Mod == Modifier.None)
                    value = setting.Value;
            foreach (var setting in TransientSettings)
                if (setting.Track == track && setting.Mod == Modifier.None)
                    value = setting.Value;
            return value with { };
        }

        internal OwnedStrategyState ReadOwnedStrategy(int track)
        {
            lock (_ownedStrategyGate)
            {
                var watch = ObserveNativeStrategy(track);
                if (watch == null)
                    return new(false, null, 0, null);
                var owner = _strategyOwners.GetValueOrDefault(track);
                return new(true, owner == null ? NativeValue(track) : owner.Value with { }, watch.Revision, owner?.LeaseId);
            }
        }

        internal bool SetOwnedStrategy(int track, string leaseId, long revision, StrategyValue value, Func<bool> isLive)
        {
            lock (_ownedStrategyGate)
            {
                var watch = ObserveNativeStrategy(track);
                var owner = _strategyOwners.GetValueOrDefault(track);
                if (watch == null || watch.Revision != revision || string.IsNullOrEmpty(leaseId) || TransientSettings.Any(s => s.Track == track)
                    || owner != null && owner.LeaseId != leaseId
                    || owner == null && _strategyOwners.Count >= 32 || !isLive())
                    return false;
                var next = ++_ownedStrategyRevision;
                _strategyWatches[track] = watch with { Revision = next };
                _strategyOwners[track] = new(leaseId, value with { }, next, Stopwatch.GetTimestamp() + 2 * Stopwatch.Frequency, isLive);
                return true;
            }
        }

        internal bool RenewOwnedStrategy(int track, string leaseId, long revision, int leaseMs)
        {
            lock (_ownedStrategyGate)
            {
                var watch = ObserveNativeStrategy(track);
                if (watch == null || watch.Revision != revision || leaseMs is < 1 or > 2000
                    || !_strategyOwners.TryGetValue(track, out var owner) || owner.LeaseId != leaseId)
                    return false;
                _strategyOwners[track] = owner with { Expires = Stopwatch.GetTimestamp() + (long)(leaseMs / 1000d * Stopwatch.Frequency) };
                return true;
            }
        }

        internal bool ReleaseOwnedStrategy(int track, string leaseId, long revision)
        {
            lock (_ownedStrategyGate)
            {
                var watch = ObserveNativeStrategy(track);
                if (watch == null || watch.Revision != revision || !_strategyOwners.TryGetValue(track, out var owner) || owner.LeaseId != leaseId)
                    return false;
                _strategyOwners.Remove(track);
                _strategyWatches[track] = watch with { Revision = ++_ownedStrategyRevision };
                return true;
            }
        }

        internal bool HasOwnedStrategy(int track) => ReadOwnedStrategy(track).LeaseId != null;

        internal void UserStrategyIntent(int track)
        {
            lock (_ownedStrategyGate)
            {
                var watch = ObserveNativeStrategy(track);
                _strategyOwners.Remove(track);
                if (watch != null)
                    _strategyWatches[track] = watch with { Revision = ++_ownedStrategyRevision };
            }
        }

        internal void ApplyOwnedStrategies(StrategyValue[] values, Modifier modifiers)
        {
            lock (_ownedStrategyGate)
            {
                foreach (var track in _strategyOwners.Keys.ToArray())
                {
                    ObserveNativeStrategy(track);
                    // Held native modifier strategies always retain manual authority.
                    if (modifiers == Modifier.None && _strategyOwners.TryGetValue(track, out var owner))
                        values[track] = owner.Value with { };
                }
            }
        }
    }
}
