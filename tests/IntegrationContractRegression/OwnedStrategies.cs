using BossMod;
using BossMod.Autorotation;
using BossMod.Autorotation.MiscAI;
using System.Reflection;
using System.Text.Json;

static partial class Checks
{
    private static void OwnedStrategies()
    {
        var definition = NormalMovement.Definition();
        var module = new Preset.ModuleSettings(typeof(NormalMovement), definition, (_, _) => throw new NotSupportedException());
        var preset = new Preset("contract-fixture") { Modules = [module] };
        var range = (int)NormalMovement.Track.Range;
        var cast = (int)NormalMovement.Track.Cast;
        var maxRange = (int)NormalMovement.RangeStrategy.MaxRange;
        var greedyRange = (int)NormalMovement.RangeStrategy.GreedAutomatic;
        module.SerializedSettings.Add(new(default, range, new StrategyValueTrack { Option = maxRange, PriorityOverride = 123 }));
        module.SerializedSettings.Add(new(Preset.Modifier.Shift, range, new StrategyValueTrack { Option = 0 }));
        var type = typeof(Preset.ModuleSettings);
        object State(int track) => type.GetMethod("ReadOwnedStrategy", InternalInstance)!.Invoke(module, [track])!;
        bool Release(int track, string id, long revision) => (bool)type.GetMethod("ReleaseOwnedStrategy", InternalInstance)!.Invoke(module, [track, id, revision])!;
        bool Renew(int track, string id, long revision, int duration) => (bool)type.GetMethod("RenewOwnedStrategy", InternalInstance)!.Invoke(module, [track, id, revision, duration])!;
        void Intent(int track) => type.GetMethod("UserStrategyIntent", InternalInstance)!.Invoke(module, [track]);
        int Effective(int track, Preset.Modifier modifier = Preset.Modifier.None) => ((StrategyValueTrack)preset.ActiveStrategyOverrides(0, modifier).Values[track]).Option;

        var resourceType = typeof(Preset).Assembly.GetType("BossMod.Integration.IntegrationStrategy", true)!;
        var live = true;
        var throwProbe = false;
        object Resource(int track) => resourceType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single()
            .Invoke([module, track, "fixture:" + track, (Func<bool>)(() => throwProbe ? throw new InvalidOperationException("owner probe failed") : live)]);
        var rangeResource = Resource(range);
        object ResourceState(object resource) => resourceType.GetMethod("Read")!.Invoke(resource, null)!;
        object Apply(object resource, string owner, string value) => resourceType.GetMethod("Apply")!.Invoke(resource,
            [owner, ResourceState(resource), JsonSerializer.Serialize(new { valueText = value })])!;
        var applied = Apply(rangeResource, "a", "GreedAutomatic");
        Check((bool)Property(applied, "Accepted") && Effective(range) == greedyRange, "owned strategy is consumed through the actual native preset");
        var revision = (long)Property(applied, "Revision");
        Check(((StrategyValueTrack)Property(State(range), "Value")).PriorityOverride == 123,
            "changing an owned option preserves the user's independent priority setting");
        Check(Effective(range, Preset.Modifier.Shift) == 0, "held native modifier choices retain authority over an integration layer");
        Check(module.TransientSettings.Count == 0 && ((StrategyValueTrack)module.SerializedSettings[0].Value).Option == maxRange,
            "owned layers never mutate saved or legacy transient lists");
        Check(((StrategyValueTrack)preset.MakeClone(true).ActiveStrategyOverrides(0, Preset.Modifier.None).Values[range]).Option == maxRange,
            "cloning a preset does not copy another owner's runtime lease");
        Check(!JsonSerializer.Serialize(preset).Contains("GreedAutomatic", StringComparison.Ordinal), "preset persistence excludes borrowed strategy values");
        Check(!(bool)Property(Apply(rangeResource, "b", "Any"), "Accepted") && Effective(range) == greedyRange, "a different owner cannot steal a leased track");
        module.SerializedSettings.Add(new(default, cast, new StrategyValueTrack()));
        Check(Effective(range) == greedyRange && (string?)Property(State(range), "LeaseId") == "a", "an unrelated track edit preserves independent ownership");
        Check(!Release(range, "b", revision) && Release(range, "a", revision) && Effective(range) == maxRange,
            "release removes only the exact owner's layer and restores native evaluation");

        applied = Apply(rangeResource, "a", "GreedAutomatic"); revision = (long)Property(applied, "Revision");
        module.SerializedSettings[0] = module.SerializedSettings[0] with { Value = module.SerializedSettings[0].Value with { } };
        Check(Effective(range) == maxRange && !Release(range, "a", revision), "equal-value native replacement invalidates restoration ownership");
        applied = Apply(rangeResource, "a", "GreedAutomatic"); revision = (long)Property(applied, "Revision");
        Intent(range);
        module.SerializedSettings[0] = new(default, range, new StrategyValueTrack { Option = greedyRange });
        Check(Effective(range) == greedyRange && !Release(range, "a", revision), "explicit user choice equal to the borrowed value wins over cleanup");
        module.SerializedSettings[0] = new(default, range, new StrategyValueTrack { Option = maxRange });
        var before = (long)Property(State(range), "Revision");
        Intent(range); ((StrategyValueTrack)module.SerializedSettings[0].Value).Option = 0;
        Intent(range); ((StrategyValueTrack)module.SerializedSettings[0].Value).Option = maxRange;
        Check((long)Property(State(range), "Revision") > before, "away-and-back user changes cannot reuse an old strategy revision");
        module.TransientSettings.Add(new(default, range, new StrategyValueTrack()));
        Check(!(bool)Property(Apply(rangeResource, "a", "GreedAutomatic"), "Accepted") && Effective(range) == 0,
            "a legacy transient is not silently absorbed or overwritten by a new owner");
        module.TransientSettings.Clear();
        applied = Apply(rangeResource, "a", "GreedAutomatic");
        live = false;
        Check(Effective(range) == maxRange && (string?)Property(State(range), "LeaseId") == null, "owner loss removes its layer without waiting on an external update loop");
        live = true;
        applied = Apply(rangeResource, "a", "GreedAutomatic"); revision = (long)Property(applied, "Revision");
        Check(Renew(range, "a", revision, 1), "native strategy expiry can be renewed within its maximum duration");
        Thread.Sleep(10);
        Check(Effective(range) == maxRange && !Renew(range, "a", revision, 1000), "expired native strategy cannot revive or retain its borrowed option");
        var castResource = Resource(cast);
        Check(!(bool)Property(Apply(castResource, "a", "Greedy"), "Accepted"), "new owned movement strategies cannot bypass native cast/departure safety");
        Check(!(bool)Property(Apply(rangeResource, "a", "GreedLastMomentExplicit"), "Accepted"), "explicit unvalidated movement deadlines are not exposed as strategy leases");
        applied = Apply(rangeResource, "a", "GreedAutomatic");
        throwProbe = true;
        Check(Effective(range) == maxRange, "a failed owner probe removes its layer and cannot throw through native preset execution");
        throwProbe = false;
        applied = Apply(rangeResource, "a", "GreedAutomatic");
        for (var i = 0; i < 257; ++i) module.TransientSettings.Add(new(default, cast, new StrategyValueTrack()));
        Check(Effective(range) == maxRange && !(bool)Property(State(range), "Available"), "oversized changed native data cannot retain a previously valid strategy layer");
        module.TransientSettings.Clear();
        var badTarget = resourceType.GetMethod("Apply")!.Invoke(rangeResource,
            ["a", ResourceState(rangeResource), "{\"valueText\":\"GreedAutomatic\",\"target\":\"PointAbsolute\",\"targetParam\":0}"])!;
        Check(!(bool)Property(badTarget, "Accepted"), "owned strategies never expose a raw explicit-position target");
    }
}
