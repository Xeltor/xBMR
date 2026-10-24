using BossMod;
using BossMod.CombatAssistance.Planning;

public static partial class Checks
{
    private static void PartyFacing()
    {
        var facing = new PartyFacingPreference();
        var now = DateTime.UtcNow;
        Angle[] allies = [89f.Degrees(), 91f.Degrees()];
        Check(!PartyFacingPreference.EligibleDowntime(false, false, DateTime.MaxValue, DateTime.MaxValue),
            "No encounter module means no social facing, even without a target.");
        Check(PartyFacingPreference.EligibleDowntime(true, false, DateTime.MaxValue, DateTime.MaxValue),
            "Module-owned immunity downtime permits social facing without requiring the local combat flag or an active timeline state.");
        Check(PartyFacingPreference.EligibleDowntime(true, true, now.AddSeconds(20), now.AddSeconds(5)),
            "Authored downtime permits social facing while a boss remains targetable.");
        Check(!PartyFacingPreference.EligibleDowntime(true, true, now.AddSeconds(5), now.AddSeconds(20))
            && !PartyFacingPreference.EligibleDowntime(true, true, DateTime.MaxValue, DateTime.MaxValue),
            "An available combat opportunity or unknown timeline cannot manufacture boss downtime.");
        Check(facing.Observe(now, PartyFacingPreference.EligibleDowntime(true, false, DateTime.MaxValue, DateTime.MaxValue), allies, 2, default) == null
            && facing.Observe(now.AddSeconds(.6), PartyFacingPreference.EligibleDowntime(true, false, DateTime.MaxValue, DateTime.MaxValue), allies, 2, default) != null,
            "Immunity downtime still requires stable party consensus before producing a direction.");
        Check(facing.Observe(now.AddSeconds(.7), PartyFacingPreference.EligibleDowntime(false, false, DateTime.MaxValue, DateTime.MaxValue), allies, 2, default) == null,
            "Leaving the encounter module immediately releases an established social direction.");
        facing.Reset();
        Check(PartyFacingPreference.Consensus([179f.Degrees(), (-179f).Degrees()], 2) is { } wrap
            && wrap.AlmostEqual(180f.Degrees(), 0.01f), "party facing consensus spans circular wrap");
        Check(PartyFacingPreference.Consensus([0f.Degrees(), 90f.Degrees(), 180f.Degrees()], 2) == null,
            "split party directions do not request a turn");
        Check(facing.Observe(now, true, allies, 2, default) == null, "party facing requires a stable observation");
        var stable = facing.Observe(now.AddSeconds(0.6), true, allies, 2, default);
        Check(stable is { } angle && angle.AlmostEqual(90f.Degrees(), 0.01f), "stable downtime direction becomes available");
        Check(facing.Observe(now.AddSeconds(0.7), true, [90f.Degrees(), 92f.Degrees()], 2, default) == stable,
            "minor party facing changes retain the same planning input");
        Check(facing.Observe(now.AddSeconds(0.8), true, allies, 2, 80f.Degrees()) == null,
            "aligned player does not require precision correction");
        Check(facing.Observe(now.AddSeconds(0.9), false, allies, 2, default) == null
            && facing.Observe(now.AddSeconds(1), true, allies, 2, default) == null,
            "manual handoff or combat opportunity requires a new stable observation");
        Check(facing.Observe(now.AddSeconds(2.5), true, allies, 2, default) == null,
            "a stale observation does not immediately authorize a cosmetic turn");

        var map = Grid(1, 1);
        var input = Context(map, 0);
        var context = new MovementPlanningContext(input.Identity, map, 0, 0, [true], [0], [], [], downtimeFacing: 90f.Degrees());
        var plan = JointMovementSearch.Search(context, Exhaustive);
        var executor = new MovementPlanExecutor();
        var result = executor.Advance(plan, context, default, true, input.Identity.CapturedTimestamp);
        Check(result is { Kind: MovementExecutionKind.Face, PresentationFacing: true, Waypoint: null },
            "downtime facing completes an arrived plan without adding a walk");
        var disabled = new MovementPlanningContext(input.Identity, map, 0, 0, [true], [0], [], []);
        Check(executor.Advance(plan, disabled, default, true, input.Identity.CapturedTimestamp).Kind != MovementExecutionKind.Face,
            "new combat evidence immediately removes an old cosmetic direction");
        var gaze = new MovementPlanningContext(input.Identity, map, 0, 0, [true], [0], [], [],
            downtimeFacing: 90f.Degrees(), executionRules: new(float.MaxValue, float.MaxValue, [new(90f.Degrees(), 30f.Degrees(), 0)]));
        Check(executor.Advance(plan, gaze, default, true, input.Identity.CapturedTimestamp).Kind != MovementExecutionKind.Face,
            "cosmetic facing cannot override an authored gaze requirement");
        Check(executor.Advance(plan, context, default, false, input.Identity.CapturedTimestamp).Kind == MovementExecutionKind.Invalidated,
            "manual input invalidates cosmetic facing through the same executor");
    }
}
