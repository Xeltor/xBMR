using BossMod;
using BossMod.Integration;
using System.Text.Json;

static partial class Checks
{
    private static void NoLossGoals()
    {
        NoLossConsumerBudget();
        long now = 1;
        using var session = new IntegrationSession(() => now, 1000);
        var owner = Owner(session);
        session.BeginFrame("fixture", true); session.Publish("hints-ready", c => IntegrationSession.Serialize(c));
        var context = session.CurrentContext;
        Func<WPos, float> first = p => p.X <= 0 ? 1 : 0;
        Func<WPos, float> second = p => p.X > 0 ? 1 : 0;
        Func<WPos, float> laterRotation = p => p.Z == 0 ? 1 : 0;
        var batch = new IntegrationSession.NoLossBatch(context, owner, "mechanic-preference",
            [new([[0, 0]]), new(null), new([[1, 0]])]);
        Check(session.SubmitNoLossBatch(batch, [static _ => 3, static _ => 2, static _ => 4]).Accepted,
            "aligned guarded and ordinary leaves submit as one category surface");
        Check(session.CaptureGoals("mechanic-preference").Single()(default) == 2,
            "guarded leaves cannot run before native final-goal capture; ordinary sibling stays independent");
        var goalSet = new IntegrationGoalSet(session, [first]);
        _ = new IntegrationGoalSet(session, [first]).Capture([first, second, laterRotation]); // Warm bounded native first-use work.
        var captured = goalSet.Capture([first, second, laterRotation])!;
        var score = captured.MechanicPreference.Single();
        Check(score(new(0, 0)) == 5 && score(new(1, 0)) == 6,
            "per-leaf native guards run before category aggregation without overconstraining unrelated leaves");
        Check(first(new(0, 0)) + second(new(0, 0)) == first(new(1, 0)) + second(new(1, 0)),
            "fixture has equal summed native value while losing an individual native goal");
        Check(score(new(0, 1)) == 2 && score(new(1, 1)) == 2,
            "later native rotation goals join each guard before worker scheduling");
        using (var observation = JsonDocument.Parse(IntegrationSession.Serialize(goalSet.NoLossObservation)))
            Check(observation.RootElement.GetProperty("nativeGoalCount").GetInt32() == 3
                && observation.RootElement.GetProperty("capturedGuards").GetInt32() == 2 && observation.RootElement.GetProperty("complete").GetBoolean(),
                "capture diagnostics distinguish final native inputs from earlier point queries");
        session.Clear(Lease(session, owner));
        Check(score(default) == 0, "clearing the owner revokes all captured guarded/ordinary leaves together");
        foreach (var invalid in new[]
        {
            batch with { Items = [] }, batch with { Items = [new([])] }, batch with { Items = [null!] },
            batch with { Items = [new([[float.NaN, 0]])] }, batch with { Items = [new([[1]])] },
            batch with { Items = [new(Enumerable.Range(0, 9).Select(_ => new[] { 0f, 0f }).ToArray())] },
            batch with { Items = Enumerable.Repeat(new IntegrationSession.NoLossItem(null), 17).ToArray() }
        }) Check(!session.SubmitNoLossBatch(invalid, [static _ => 1]).Accepted, "invalid batch shape/alignment/baselines reject atomically");
        Check(!session.SubmitNoLossBatch(batch, [static _ => 1]).Accepted, "delegate and item counts must match exactly");
        Check(!session.SubmitNoLossBatch(batch, [static _ => 1, null!, static _ => 1]).Accepted, "a null delegate cannot become an ordinary unguarded leaf");
        Check(!session.SubmitNoLoss(new(context, owner, "presentation", null!), static _ => 1).Accepted,
            "single no-loss submission requires its own positive baseline set");
        Check(session.SubmitNoLoss(new(context, owner, "presentation", [[0, 0], [1, 0]]), static _ => 1).Accepted,
            "single guarded submission accepts independent execution candidates");
        var impossible = new IntegrationGoalSet(session, [first]).Capture([first, second])!;
        Check(impossible.Presentation.Single()(new(0, 0)) == 0 && impossible.Presentation.Single()(new(1, 0)) == 0,
            "each native goal must retain its maximum over all baselines, even when maxima conflict");
        now += 251;
        Check(impossible.Presentation.Single()(default) == 0, "original source lifetime bounds every guarded worker scorer");

        session.BeginFrame("fixture", true); session.Publish("hints-ready", c => IntegrationSession.Serialize(c));
        var guard = new IntegrationSession.ContributionGuard(() => true);
        Check(session.PairConstraintGoals(owner, guard) != null, "pending constraint pairs future goal leaves without changing owner identity");
        Check(session.SubmitNoLossBatch(new(session.CurrentContext, owner, "combat", [new(null), new(null)]),
            [p => 1 + p.X, p => 3 - p.X]).Accepted, "paired batch preserves individually opposing execution leaves");
        var leaves = session.CaptureIndividualGoalLeaves();
        Check(leaves.Length == 2 && leaves.All(f => f(default) == 0), "pending mask cannot contribute paired leaves before native admission");
        guard.Activate();
        Check(leaves[0](Vector2.UnitX) == 2 && leaves[1](Vector2.UnitX) == 2, "native arrival comparison captures individual leaves, not their cancelling sum");
        guard.Retire();
        Check(leaves.All(f => f(default) == 0) && session.IsOwnerActive(owner), "ordinary mask retirement withdraws paired leaves and preserves observer registration");
        session.BeginFrame("fixture", true); session.Publish("hints-ready", c => IntegrationSession.Serialize(c));
        var replacement = new IntegrationSession.ContributionGuard(() => true);
        session.PairConstraintGoals(owner, replacement); replacement.Activate();
        Check(session.Submit(new(session.CurrentContext, owner, "combat"), _ => 4).Accepted
            && session.CaptureIndividualGoalLeaves().Single()(default) == 4 && leaves.All(f => f(default) == 0),
            "fresh source rollover restores fresh paired leaves without reviving old captured contributions");

        var native = new IntegrationNativeGoals([first, second]); var gate = native.Capture([Vector2.Zero])!;
        Check(gate(Vector2.Zero) && !gate(Vector2.UnitX), "continuous native comparator rejects individual goal loss hidden by equal sums");
        Check(new IntegrationNativeGoals([]).Capture([Vector2.Zero])!(new(10, 10)), "an exactly empty native goal set has no invented native obligation");
        Check(new IntegrationNativeGoals([_ => float.NaN]).Capture([Vector2.Zero]) == null,
            "nonfinite native baseline cannot establish no-loss evidence");
        Check(new IntegrationNativeGoals([_ => throw new InvalidOperationException("native")]).Capture([Vector2.Zero]) == null,
            "native function failure cannot approve a guarded contribution");
        Check(new IntegrationNativeGoals(Enumerable.Repeat(first, 65).ToArray()).Capture([Vector2.Zero]) == null,
            "native comparator bounds original goal count");
        Check(new IntegrationNativeGoals([first]).Capture([]) == null, "native comparator requires a baseline rather than vacuous approval");
        Check(IntegrationNativeGoals.Id(first) == IntegrationNativeGoals.Id(first)
            && IntegrationNativeGoals.Id(first) != IntegrationNativeGoals.Id(second), "opaque individual identities distinguish equal summed goals");
    }

    private static void NoLossConsumerBudget()
    {
        foreach (var nativeAllows in new[] { false, true })
        {
            long now = 1;
            using var session = new IntegrationSession(() => now, 1000);
            var owner = Owner(session);
            session.BeginFrame("budget", true);
            var called = 0;
            session.SubmitNoLossBatch(new(session.CurrentContext, owner, "combat", [new([[0, 0]])]),
                [_ => { called++; return 2; }]);
            var goal = session.CaptureGoals("combat", _ => _ => { now += 3; return nativeAllows; }).Single();
            Check(goal(default) == (nativeAllows ? 2 : 0) && called == (nativeAllows ? 1 : 0),
                "native no-loss comparisons remain binding before consumer scoring");
            Check(session.Renew(Lease(session, owner)).Accepted,
                "bounded native comparison work cannot revoke a healthy observer or be charged to an uncalled consumer");
            session.Clear(Lease(session, owner));
            Check(goal(default) == 0, "separating native timing does not bypass owner cleanup or captured-goal retirement");
        }
        foreach (var count in new[] { 1, 2 })
        {
            long now = 1;
            using var session = new IntegrationSession(() => now, 1000);
            var owner = Owner(session);
            session.BeginFrame("budget", true);
            var items = Enumerable.Repeat(new IntegrationSession.NoLossItem(null), count).ToArray();
            Func<Vector2, float> slow = _ => { now += count == 1 ? 3 : 2; return 1; };
            session.SubmitNoLossBatch(new(session.CurrentContext, owner, "combat", items), Enumerable.Repeat(slow, count).ToArray());
            Check(session.CaptureGoals("combat").Single()(default) == 0 &&
                session.Renew(Lease(session, owner)).Reason == "owner-revoked:goal-evaluation-budget",
                "all consumer leaves still share the original two-millisecond budget");
        }
    }
}
