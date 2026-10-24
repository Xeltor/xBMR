using BossMod;
using BossMod.CombatAssistance.Planning;
using BossMod.Pathfinding;
using System.Diagnostics;

public static partial class Checks
{
    private static void GriffinWarningRecovery()
    {
        // The reported stationary Talon window, reconstructed offline from finalized
        // recording d19c9414a197c83415c2fbce34415d8e8a1e1e2f49654611ceddd82461c55938,
        // candidate-runner before_seq 209400. Module hints only: no collision or
        // live RSR selection is inferred from this fixture.
        var origin = new WPos(286.02194f, -627.9452f);
        var boss = new WPos(290.97424f, -602.6856f);
        var center = new WPos(281, -620.375f);
        var now = DateTime.UtcNow;
        var identity = new NavigationIdentity(1, now, DateTimeOffset.UtcNow, Stopwatch.GetTimestamp(),
            origin, false, 6, 0, null, 1, 1314, 1064);
        var source = NavigationMapSource.Capture(
            new ArenaBoundsCustom([new Polygon(center, 20, 64)], MapResolution: .35f), center, null, default, false);
        (float X, float Z, float Rotation)[] lanes =
        [
            (239.67346f, -674.21985f, .8588984f), (257.5265f, -686.9154f, .3779881f),
            (267.13965f, -689.57043f, .16111827f), (277.9431f, -558.58704f, 2.8541586f),
            (315.7854f, -563.9582f, -2.571998f), (336.90393f, -599.8169f, -1.6332831f),
            (273.36536f, -557.702f, 3.0716035f), (329.9458f, -656.94666f, -.7854581f),
            (232.07446f, -619.83673f, 1.6406653f), (272.17517f, -690.08923f, .05095744f)
        ];
        var rect = new AOEShapeRect(92, 3);
        var warnings = lanes.Select(lane => (rect.Distance(new(lane.X, lane.Z), lane.Rotation.Radians()),
            now.AddSeconds(5.4667472), 0ul)).ToArray();
        const float reach = 3 + .5f + 6.9f;
        var input = new NavigationDecision.CapturedInput(source, [], warnings,
            new([point => (point - boss).LengthSq() <= reach * reach ? 1 : 0]), true, [], identity, true);
        var native = NativeNavigationSnapshot.Capture(input);
        MovementPlanningContext Capture(bool dash, bool backstep) => new(identity, native.CopyStrictMap(), 0, 0,
            Enumerable.Repeat(true, native.Cells).ToArray(), new float[native.Cells], [], [], 0x12,
            native: native, targetId: 10, range: new(10, boss, reach, RecoverExecution: true),
            mobilityMechanics: new[]
            {
                new(36951, MobilityMechanic.Target, 20, 0, .5f, 0, .6f, 0, [new(10, boss, 6.9f)]),
                new MobilityMechanics(94, MobilityMechanic.Directional, 0, -15, .5f, 0, .6f, 1)
            }.Where(action => action.ActionId == 36951 ? dash : backstep));
        var context = Capture(true, true);
        var recovery = NativeMovementSearch.Recover(context, new());
        var seed = recovery.NativeRecovery?.Publication is { } publication
            ? NativeMovementIncumbent.Capture(context, publication) : null;
        var clock = Stopwatch.StartNew();
        double? firstContactMs = null;
        var plan = JointMovementSearch.Search(context, new(RespectFreshness: false), nativeIncumbent: seed,
            publishBest: candidate =>
            {
                if (candidate.RangeAccess is { Loss: 0 } && candidate.Stages.Any(s => s.ActionId is 36951 or 94))
                    firstContactMs ??= clock.Elapsed.TotalMilliseconds;
            },
            stopAtCurrentOpportunity: true, includeSpatialMobilityActions: false);
        Console.WriteLine($"Griffin ten-warning recovery: first complete mobility/contact={firstContactMs:F3}ms, total={plan.SearchMilliseconds:F3}ms, {plan.ExpandedStates} expansions, {plan.Status}/{plan.Bound}, range={plan.RangeAccess?.Loss}, stages={string.Join(',', plan.Stages.Select(s => s.Kind))}");
        Check(plan.HasVerifiedRoute && plan.RangeAccess is { Loss: 0 }
            && plan.Stages.Any(s => s.ActionId is 36951 or 94)
            && MovementPlanValidation.Validate(plan, context, 0, true, identity.CapturedTimestamp).Allowed,
            "The recorded ten-warning Griffin arrangement retains a complete mobility/contact/escape route instead of standing at Talon range.");
        foreach (var (dash, backstep) in new[] { (true, false), (false, true) })
        {
            var alternate = Capture(dash, backstep);
            var alternateClock = Stopwatch.StartNew();
            double? alternateFirstMs = null;
            var candidate = JointMovementSearch.Search(alternate, new(RespectFreshness: false),
                publishBest: found =>
                {
                    if (found.RangeAccess is { Loss: 0 } && found.Stages.Any(s => s.ActionId is 36951 or 94))
                        alternateFirstMs ??= alternateClock.Elapsed.TotalMilliseconds;
                },
                stopAtCurrentOpportunity: true, includeSpatialMobilityActions: false);
            Console.WriteLine($"Griffin isolated {(dash ? "dash" : "backstep")}: first={alternateFirstMs:F3}ms, total={candidate.SearchMilliseconds:F3}ms, {candidate.Status}/{candidate.Bound}, contact={candidate.RangeAccess}");
            Check(candidate.HasVerifiedRoute && candidate.RangeAccess is { Loss: 0 }
                && candidate.Stages.Any(s => s.ActionId == (dash ? 36951u : 94u))
                && MovementPlanValidation.Validate(candidate, alternate, 0, true, identity.CapturedTimestamp).Allowed,
                "Either independently available Griffin mobility option keeps its complete verified escape when the other action is unavailable.");
        }
        var sprintIdentity = identity with { CapturedTimestamp = Stopwatch.GetTimestamp(), CapturedUtc = DateTimeOffset.UtcNow };
        var sprintNative = NativeNavigationSnapshot.Capture(input with { Identity = sprintIdentity });
        var sprintContext = new MovementPlanningContext(sprintIdentity, sprintNative.CopyStrictMap(), 0, 0,
            Enumerable.Repeat(true, sprintNative.Cells).ToArray(), new float[sprintNative.Cells], [], [], 1,
            native: sprintNative, targetId: 10, range: new(10, boss, reach, RecoverExecution: true),
            utilities: [new(3, PlanningUtilityEffect.MovementSpeed, 10, .6f, .4f, 0, 7.8f)]);
        MovementPlan? sprintPublication = null;
        var sprintClock = Stopwatch.StartNew();
        double? sprintFirst = null;
        NativeMovementSearch.Run(sprintContext, default, found =>
        {
            if (found.HasVerifiedRoute && found.RangeAccess is { Loss: 0 } && found.Stages.Any(s => s.ActionId == 3))
            { sprintPublication ??= found; sprintFirst ??= sprintClock.Elapsed.TotalMilliseconds; }
        });
        Check(sprintPublication != null && MovementPlanValidation.Validate(sprintPublication, sprintContext, 0, true,
            sprintIdentity.CapturedTimestamp).Allowed,
            "When jumps are unavailable, the recorded warning arrangement promptly publishes a complete Sprint/contact/escape route.");
        Console.WriteLine($"Griffin Sprint-only recovery: first complete publication={sprintFirst:F3}ms; original strict deadlines and actual speed expiry retained.");
    }
}
