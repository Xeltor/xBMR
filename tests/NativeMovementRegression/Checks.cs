using BossMod;
using BossMod.CombatAssistance.Planning;
using BossMod.Pathfinding;
using System.Diagnostics;
using System.Threading;
using System.Collections.Immutable;

public static partial class Checks
{
    private static int _assertions;
    private static readonly JointMovementSearch.Limits Exhaustive = new(1_000_000, 30_000, 1_000_000, RespectFreshness: false);
    public static void Run()
    {
        ExhaustiveTinyMaps();
        WarningRoutes();
        IndependentRecipients();
        ResourceLabels();
        ResourceRecharge();
        ResourceArithmetic();
        TeleportPlanning();
        CastAndLocks();
        StabilityAndBudget();
        SequenceLimit();
        ImmutableSnapshot();
        FreshExecution();
        WorkerOwnership();
        WorkerInputChurn();
        DeliberateArrival();
        FrozenNativeGoals();
        ExhaustiveResourceRoutes();
        BenchmarkBudget();
        NativeIncumbentAndExactPositions();
        NativeConstraintReview();
        NativeSnapshotParity();
        SettingsImport();
        NativeOwnership();
        NativeProblemChanges();
        NativeHostLifecycle();
        IdleNavigation();
        PlanningBuffers();
        CompleteOptimum();
        ScopedPluginObservation();
        CaptureThreadAndCost();
        ActionOpportunities();
        SettingsSession();
        LegacyCommands();
        LegacyRetirement();
        Diagnostics();
        RsrEvidence();
        SelectedPartySequence();
        ConfiguredHealingCoverage();
        RedMageComboRecovery();
        RedMagePreparation();
        Execution();
        OwnerInstructions();
        DeparturePreference();
        UtilityZones();
        CastTiming();
        LeyLinesTiming();
        PictomancerTiming();
        OffensiveTargetSelection();
        OffensivePredicateConsumer();
        NativeTargetChoices();
        NativeTargetOptionInteractions();
        StationaryTargetPlanning();
        NativeTargetHandoff();
        LeyLinesRelocation();
        LeyLinesPlacement();
        CameraPolicies();
        NativeDashSafety();
        MobilityMechanics();
        ResourcePruning();
        CombatRangeRecovery();
        EscapeCommitment();
        GriffinWarningRecovery();
        MobilityRebinding();
        CombatFollowRangeTests();
        RotationalMobility();
        RotationalPreparation();
        MeleeRecoveryDelivery();
        UtilityPlanning();
        SprintPlanning();
        Presentation();
        PartyFacing();
        Formation();
        PartyContact();
        NativeCorrections();
        IngressRecovery();
        NativePhysicalProbes();
        CurrentWalkRoutes();
        CounterfactualProjections();
        KnockbackAuthority();
        DisplacementInventoryAndPhases();
        JointKnockbackPlanning();
        NativeKnockbackObservation();
        ForcedMarch();
        ForcedMarchOwnership();
        ForcedMovementPlanning();
        CastControl();
        TankStance();
        TankRecovery();
        ModuleEnablementCache();
        ArenaGeometry();
        SupportRecipientTests.SustainedResponsibility();
        SupportRecipientTests.RangeAndManualOwnership();
        SupportRecipientTests.DamageAndCards();
        SupportRecipientTests.HealingCardsAndUnion();
        SupportRecipientTests.CardRefreshOwnership();
        SupportRecipientTests.ObservedKardiaCoverage();
        SupportRecipientTests.ObservedFairyCoverage();
        PartyRangedMitigationTests.Run();
        RaisedHealingTests.Recovery();
        RaiseAcceptanceTests.OfferLifecycle();
        RsrSupportLeaseTests.CallbackBoundary();
        RsrSupportLeaseTests.SharedSelectionScopes();
        RsrSupportLeaseTests.LeaseAndExecution();
        RsrSupportDutyTests.PriorityAndRestoration();
        RsrRemainingAbilityTests.Run();
        RsrMitigationSelectionTests.Run();
        RsrMeleeFallbackTests.Regression();
        NativeReplayEvidence();
        ProcessingStatusChecks.Run().GetAwaiter().GetResult();
        LinkedRotation();
        Console.WriteLine($"Native movement: {_assertions} assertions passed.");
    }

    public static void InstalledRsrContracts(string directory)
    {
        RsrSupportLeaseTests.InstalledContract(Path.Combine(directory, "RotationSolver.dll"));
        RsrMeleeFallbackTests.InstalledContract(directory);
        InstalledTargetScopeContract(directory);
    }

    private static Map Grid(int width, int height)
    {
        var map = new Map();
        map.InitGrid(1, default, width, height);
        return map;
    }
    private static WPos Point(Map map, int cell) => map.GridToWorld(cell % map.Width, cell / map.Width, 0.5f, 0.5f);
    private static MovementPlanningContext Context(Map map, int origin, PlanningAction[]? actions = null,
        PlanningMobility[]? mobility = null, ulong resources = 0, float unlock = 0, bool[]? destinations = null, WPos? committed = null, float speed = 1)
    {
        var count = map.Width * map.Height;
        var identity = new NavigationIdentity(1, DateTime.UtcNow, DateTimeOffset.UtcNow, Stopwatch.GetTimestamp(),
            Point(map, origin), unlock > 0, speed, 0, null, 7, 1, 1);
        return new(identity, map, 0, unlock, destinations ?? Enumerable.Repeat(true, count).ToArray(), new float[count],
            actions ?? [], mobility ?? [], resources, committed);
    }
    private static PlanningAction Action(Map map, float deadline, ulong required, ulong[] recipients, float[] values,
        float cast = 0, float earliest = 0)
        => new(1, 10, earliest, deadline, cast, 0, required, recipients, values,
            RecipientIds: Enumerable.Range(0, 64).Select(i => (ulong)i + 100).ToImmutableArray());
    internal static void Check(bool condition, string message)
    {
        ++_assertions;
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void ExhaustiveTinyMaps()
    {
        // Independent flood-fill oracle: static geometry has no reason to revisit a cell.
        // Enumerate every obstacle mask and every preferred endpoint on a 3x2 map.
        for (var mask = 0; mask < 32; ++mask)
            for (var preferred = 0; preferred < 6; ++preferred)
            {
                var map = Grid(3, 2);
                for (var i = 1; i < 6; ++i)
                    if ((mask & (1 << (i - 1))) != 0)
                        map.PixelMaxG[i] = -1;
                map.PixelPriority[preferred] = 2;
                var reachable = new HashSet<int> { 0 };
                var pending = new Queue<int>();
                pending.Enqueue(0);
                while (pending.TryDequeue(out var cell))
                {
                    for (var y = 0; y < 2; ++y)
                        for (var x = 0; x < 3; ++x)
                        {
                            var dx = Math.Abs(x - cell % 3);
                            var dy = Math.Abs(y - cell / 3);
                            var next = y * 3 + x;
                            if (dx > 1 || dy > 1 || dx + dy == 0 || map.PixelMaxG[next] < 0)
                                continue;
                            // Corner touching is conservative: both adjacent side cells must be passable.
                            if (dx == 1 && dy == 1 && (map.PixelMaxG[y * 3 + cell % 3] < 0 || map.PixelMaxG[cell / 3 * 3 + x] < 0))
                                continue;
                            if (reachable.Add(next))
                                pending.Enqueue(next);
                        }
                }
                var result = JointMovementSearch.Search(Context(map, 0), Exhaustive);
                Check(result.Status == MovementSearchStatus.Complete, "Tiny-map search must finish.");
                Check(result.NativePriority == reachable.Max(i => map.PixelPriority[i]), $"Exhaustive oracle mask={mask}, endpoint={preferred}.");
            }
    }

    private static void WarningRoutes()
    {
        var map = Grid(5, 3);
        var start = 5;
        map.PixelPriority[9] = 10;
        map.PixelMaxG[6] = 2.5f;
        map.PixelMaxG[7] = 3.5f;
        var plan = JointMovementSearch.Search(Context(map, start), Exhaustive);
        Check(plan.NativePriority == 10, "Successive warnings can be crossed before their exit deadlines.");
        Check(plan.UsefulDestination == Point(map, 9), "First selected destination includes the useful result.");
        Check(plan.Stages.All(s => TimedTraversal.Check(map, s.From, s.To, s.StartSeconds, s.EndSeconds - s.StartSeconds, 0, out _)), "All complete-route legs must be safe.");
        map.PixelMaxG[7] = -1;
        plan = JointMovementSearch.Search(Context(map, start), Exhaustive);
        Check(plan.NativePriority == 10 && plan.Stages.Any(s => s.To.Z != Point(map, start).Z), "Blocked direct route must take a safe detour.");

        var corridor = Grid(3, 1);
        corridor.PixelMaxG[0] = 2;
        corridor.PixelMaxG[1] = 3;
        corridor.PixelPriority[1] = 100;
        plan = JointMovementSearch.Search(Context(corridor, 0), Exhaustive);
        Check(plan.UsefulDestination == Point(corridor, 2), "A temporary warning stop is not a complete plan.");
        corridor.PixelMaxG[2] = -1;
        plan = JointMovementSearch.Search(Context(corridor, 0), Exhaustive);
        Check(plan.Status == MovementSearchStatus.NoVerifiedRoute, "No invented expiry for a trapped warning route.");
        corridor.PixelMaxG[0] = 0;
        plan = JointMovementSearch.Search(Context(corridor, 0), Exhaustive);
        Check(plan.Status == MovementSearchStatus.NoVerifiedRoute, "Active hazard requires native recovery, not a false elective safety proof.");
    }

    private static void IndependentRecipients()
    {
        var map = Grid(3, 1);
        var action = Action(map, 3, 3, [1, 3, 2], [100, 2, 1000]);
        var plan = JointMovementSearch.Search(Context(map, 0, [action]), Exhaustive);
        Check(plan.DeliveredActions == 1 && plan.ExecutionValues[0] == 2, "High aggregate healing value must not hide a missing recipient.");
        var delivery = plan.Stages.Single(s => s.Kind == MovementStageKind.Cast);
        Check(delivery.To == Point(map, 1), "Deliver only where both recipients are preserved.");
        var allowed = new[] { true, true, false };
        map.PixelPriority[2] = 10_000;
        plan = JointMovementSearch.Search(Context(map, 0, [action], destinations: allowed), Exhaustive);
        Check(plan.UsefulDestination != Point(map, 2), "An assignment cannot be outweighed by a goal score.");
        map.PixelMaxG[1] = -1;
        plan = JointMovementSearch.Search(Context(map, 0, [action], destinations: allowed), Exhaustive);
        Check(plan.DeliveredActions == 0, "Unavailable healing opportunity cannot justify walking through geometry.");

        var timing = Grid(3, 1);
        timing.PixelPriority[2] = 1_000_000;
        var current = Action(timing, 1, 1, [1, 0, 0], [1, 0, 0]);
        var retained = JointMovementSearch.Search(Context(timing, 0, [current]), Exhaustive);
        Check(retained.DeliveredActions == 1 && retained.DeliveryTimes[0] == 0,
            "An opaque native goal score cannot purchase a lost or delayed current action.");
        var extraCoverage = current with { RecipientsByCell = [1, 1, 1], ValueByCell = [1, 2, 3] };
        retained = JointMovementSearch.Search(Context(timing, 0, [extraCoverage]), Exhaustive);
        Check(retained.DeliveryTimes[0] == 0 && retained.ExecutionValues[0] == 1,
            "Additional healthy recipients cannot postpone the selected heal.");
        retained = JointMovementSearch.Search(Context(timing, 0, [extraCoverage with { Earliest = 3, Latest = 4 }]), Exhaustive);
        Check(retained.DeliveryTimes[0] == 3 && retained.ExecutionValues[0] == 3,
            "Use the evidenced ready-time window to improve coverage without delaying delivery.");
    }

    private static void ResourceLabels()
    {
        var map = Grid(4, 1);
        var action = Action(map, 2.4f, 1, [0, 0, 0, 1], [0, 0, 0, 10]);
        PlanningMobility[] edges = [
            new(0, 1, MovementStageKind.TargetDash, 11, 100, null, 0, 0.1f, 0, 0),
            new(1, 3, MovementStageKind.DirectionalMovement, 12, 0, default(Angle), 0, 0.1f, 0, 0)];
        var plan = JointMovementSearch.Search(Context(map, 0, [action], edges, 1), Exhaustive);
        Check(plan.DeliveredActions == 1, "Slower arrival preserving the required charge must survive dominance pruning.");
        Check(plan.Stages.Any(s => s.ActionId == 12) && plan.Stages.All(s => s.ActionId != 11), "Walk to the useful backstep instead of spending its charge too early.");
        action = action with { Earliest = 3, Latest = 5 };
        plan = JointMovementSearch.Search(Context(map, 0, [action], edges, 1), Exhaustive);
        Check(plan.DeliveredActions == 1 && plan.RemainingResources == 1, "Preserve charges when walking delivers the same action.");
        edges[0] = edges[0] with { SetupSeconds = 12 };
        action = action with { Earliest = 0, Latest = 2.4f };
        plan = JointMovementSearch.Search(Context(map, 0, [action], edges, 1), Exhaustive);
        Check(plan.DeliveredActions == 1, "Failure of one setup must not suppress another legal mobility action.");
    }

    private static void CastAndLocks()
    {
        var map = Grid(2, 1);
        var action = Action(map, 3, 1, [0, 1], [0, 5], cast: 1, earliest: 1);
        var plan = JointMovementSearch.Search(Context(map, 0, [action], unlock: 0.5f), Exhaustive);
        Check(plan.Stages[0].Kind == MovementStageKind.Wait && plan.Stages[0].EndSeconds == 0.5f, "Respect existing action lock before walking.");
        Check(plan.DeliveredActions == 1 && plan.DurationSeconds == 2.5f, "Score at cast delivery time, including travel and lock.");
        map.PixelMaxG[1] = 2;
        plan = JointMovementSearch.Search(Context(map, 0, [action]), Exhaustive);
        Check(plan.DeliveredActions == 0, "A cast cannot complete inside an activating warning.");
        map.PixelMaxG[0] = 0.25f;
        plan = JointMovementSearch.Search(Context(map, 0, [action], unlock: 0.5f), Exhaustive);
        Check(plan.Status == MovementSearchStatus.NoVerifiedRoute, "Mandatory recovery takes precedence over an unsafe cast lock.");
    }

    private static void StabilityAndBudget()
    {
        var map = Grid(3, 1);
        var committed = Point(map, 2);
        var context = Context(map, 0, committed: committed);
        var plan = JointMovementSearch.Search(context, Exhaustive);
        Check(plan.UsefulDestination == committed, "Keep an equivalent committed destination.");
        for (var i = 0; i < 5; ++i)
            Check(JointMovementSearch.Search(context, Exhaustive).UsefulDestination == committed, "Equivalent input must remain stable.");
        plan = JointMovementSearch.Search(context, new(0, 100));
        Check(plan.Status == MovementSearchStatus.Bounded && plan.UsefulDestination == context.Origin, "Zero budget retains the verified starting incumbent and reports bounded.");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Check(JointMovementSearch.Search(context, Exhaustive, cancelled.Token).Status == MovementSearchStatus.Invalidated, "Superseded workers cannot publish a plan.");
        var timestamp = context.Identity.CapturedTimestamp + Stopwatch.Frequency;
        Check(new MovementPlan(context.Identity, MovementSearchStatus.Complete, [], context.Origin, null, 2, 0, 0, 0, [], 0, 0, 0, "test").RemainingDepartureSeconds(timestamp) == 1,
            "Deadlines age from original capture, never from observation.");
    }

    private static void ImmutableSnapshot()
    {
        var map = Grid(2, 1);
        var action = Action(map, 2, 1, [0, 1], [0, 5]);
        var context = Context(map, 0, [action]);
        map.PixelMaxG[1] = -1;
        action.RecipientsByCell[1] = 0;
        action.ValueByCell[1] = 0;
        var plan = JointMovementSearch.Search(context, Exhaustive);
        Check(plan.DeliveredActions == 1, "Mutating live input arrays must not change the captured search.");
    }

    private static void FreshExecution()
    {
        var map = Grid(3, 1);
        map.PixelPriority[2] = 10;
        var input = Context(map, 0);
        var plan = JointMovementSearch.Search(input, Exhaustive);
        Check(MovementPlanValidation.Validate(plan, input, 0, true, input.Identity.CapturedTimestamp).Allowed, "Fresh complete route validates.");
        Check(!MovementPlanValidation.Validate(plan, input, 0, false, input.Identity.CapturedTimestamp).Allowed, "Manual input invalidates execution immediately.");
        Check(!MovementPlanValidation.Validate(plan, input, 0, true, input.Identity.CapturedTimestamp + Stopwatch.Frequency).Allowed, "Old worker result is not executable.");
        map.PixelMaxG[1] = 0;
        var current = Context(map, 0);
        Check(!MovementPlanValidation.Validate(plan, current, 0, true, current.Identity.CapturedTimestamp).Allowed, "Changed hazard invalidates the full route before movement.");
        map.PixelMaxG[1] = float.MaxValue;
        var action = Action(map, 1, 1, [0, 0, 1], [0, 0, 5]);
        PlanningMobility[] mobility = [new(0, 2, MovementStageKind.TargetDash, 13, 123, null, 0, 0.1f, 0.5f, 0)];
        input = Context(map, 0, [action], mobility, 1);
        plan = JointMovementSearch.Search(input, Exhaustive);
        Check(plan.Stages.Any(s => s.Kind == MovementStageKind.TargetDash), "Mobility fixture requires an actual dash.");
        Check(MovementPlanValidation.Validate(plan, input, 0, true, input.Identity.CapturedTimestamp).Allowed, "Exact anchor and landing validate.");
        mobility[0] = mobility[0] with { AnchorId = 999 };
        current = Context(map, 0, [action], mobility, 1);
        Check(!MovementPlanValidation.Validate(plan, current, 0, true, current.Identity.CapturedTimestamp).Allowed, "Changed anchor invalidates the exact executable dash.");
    }

    private static void WorkerOwnership()
    {
        var map = Grid(2, 1);
        var input = Context(map, 0);
        using var started = new ManualResetEventSlim();
        using var finish = new ManualResetEventSlim();
        var count = 0;
        var active = 0;
        var maximumActive = 0;
        var now = input.Identity.CapturedTimestamp;
        using var service = new MovementPlanningService((context, cancel) =>
        {
            var running = Interlocked.Increment(ref active);
            maximumActive = Math.Max(maximumActive, running);
            if (Interlocked.Increment(ref count) == 1)
            {
                started.Set();
                if (!finish.Wait(5000))
                    throw new TimeoutException("Test did not release blocked worker.");
            }
            try { return JointMovementSearch.Search(context, Exhaustive, cancel); }
            finally { Interlocked.Decrement(ref active); }
        }, () => now);
        service.Submit("normal", 1, input);
        Check(started.Wait(5000), "Worker started.");
        service.Submit("follow", 2, input);
        Check(service.Poll("normal", 1, input.Identity.WorldTime) == null, "Old owner cannot observe superseded work.");
        Check(Volatile.Read(ref count) == 1, "Cancelling work does not start a concurrent second worker.");
        finish.Set();
        MovementPlan? published = null;
        Check(SpinWait.SpinUntil(() => (published = service.Poll("follow", 2, input.Identity.WorldTime)) != null, 5000), "Newest owner eventually receives its result.");
        Check(maximumActive == 1 && published!.Status == MovementSearchStatus.Complete, "Only the latest completed worker publishes.");
        now += Stopwatch.Frequency;
        Check(service.Poll("follow", 2, input.Identity.WorldTime) == null, "Wall-clock freshness expires even when the world clock pauses.");
        service.Release("follow");
        now = input.Identity.CapturedTimestamp;
        Check(service.Poll("follow", 2, input.Identity.WorldTime) == null, "Release clears retained output.");
    }

    private static void FrozenNativeGoals()
    {
        var map = Grid(3, 2);
        var target = Point(map, 4);
        Func<WPos, float>[] goals = [p => (p - target).LengthSq() <= 1 ? 2 : 0, p => p.X + 5];
        var frozen = FrozenGoalField.Capture(map, goals);
        var reference = Grid(3, 2);
        NavigationDecision.RasterizeGoalZones(reference, goals);
        for (var i = 0; i < 6; ++i)
            Check(frozen.Aggregate(i) == reference.PixelPriority[i], "Frozen aggregate preserves the native four-corner raster exactly.");
        target = new WPos(100, 100);
        frozen.ApplyNativePriorities(map);
        Check(map.PixelPriority.Take(6).SequenceEqual(reference.PixelPriority.Take(6)), "A live closure cannot mutate an already frozen field.");
        map = Grid(2, 1);
        frozen = FrozenGoalField.Capture(map, [p => p.X < 0.1f ? 1 : 0, p => p.X > -0.1f ? 100 : 0]);
        Check(!frozen.PreservesEachLeaf(1, [0]), "Summed goal improvement cannot conceal loss of an independent leaf.");

        var configType = typeof(AIHints).Assembly.GetType("BossMod.AI.AIConfig", true)!;
        if (!Service.Config._nodes.ContainsKey(configType))
            Service.Config._nodes[configType] = (ConfigNode)Activator.CreateInstance(configType, nonPublic: true)!;
        var actor = (Actor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        actor.PosRot = new(1, 0, 1, 0);
        var goal = AIHints.GoalSingleTarget(actor, 1);
        actor.PosRot = new(100, 0, 100, 0);
        Check(goal(new(1, 1)) == 1 && goal(new(100, 100)) == 0, "Actor target goals capture a position instead of reading mutable state later.");
    }

    private static void ExhaustiveResourceRoutes()
    {
        // Independent enumeration of action sequences on a four-cell corridor.
        // No search labels or dominance implementation is reused by this oracle.
        for (var charges = 0; charges <= 2; ++charges)
            for (var quarter = 1; quarter <= 20; ++quarter)
            foreach (var ready in new[] { 0f, 3f })
            {
                var deadline = quarter * 0.25f;
                var bestCharges = -1;
                var bestDelivery = float.PositiveInfinity;
                void Enumerate(int cell, float time, int remaining, int depth)
                {
                    if (time > deadline || depth > 16)
                        return;
                    var delivery = Math.Max(time, ready);
                    if (cell == 3 && delivery <= deadline && (delivery < bestDelivery || delivery == bestDelivery && remaining > bestCharges))
                    {
                        bestDelivery = delivery;
                        bestCharges = remaining;
                    }
                    if (cell > 0)
                        Enumerate(cell - 1, time + 1, remaining, depth + 1);
                    if (cell < 3)
                        Enumerate(cell + 1, time + 1, remaining, depth + 1);
                    if (remaining > 0 && cell is 0 or 1)
                        Enumerate(cell + 2, time + 0.5f, remaining - 1, depth + 1);
                }
                Enumerate(0, 0, charges, 0);
                var map = Grid(4, 1);
                if (deadline < ready)
                    continue; // invalid input window, not a search case
                var action = Action(map, deadline, 1, [0, 0, 0, 1], [0, 0, 0, 10], earliest: ready);
                PlanningMobility[] edges = [new(0, 2, MovementStageKind.TargetDash, 11, 100, null, 0, 0.5f, 0, 0),
                    new(1, 3, MovementStageKind.DirectionalMovement, 12, 0, default(Angle), 0, 0.5f, 0, 0)];
                var plan = JointMovementSearch.Search(Context(map, 0, [action], edges, (ulong)charges), Exhaustive);
                Check(plan.Status == MovementSearchStatus.Complete, "Resource oracle search must complete.");
                Check((plan.DeliveredActions == 1) == (bestCharges >= 0), $"Timing oracle deadline={deadline}, charges={charges}.");
                if (bestCharges >= 0)
                {
                    Check((int)plan.RemainingResources == bestCharges, "Resource oracle must agree on preserved charges.");
                    Check(plan.DeliveryTimes[0] == bestDelivery, "Independent timing oracle must agree on earliest current execution.");
                }
            }
    }

    private static void BenchmarkBudget()
    {
        var map = Grid(80, 80);
        map.PixelPriority[40 * 80 + 60] = 10;
        var timings = new List<double>();
        var expansions = new List<int>();
        var bounded = 0;
        for (var i = 0; i < 12; ++i)
        {
            var plan = JointMovementSearch.Search(Context(map, 40 * 80 + 40, speed: 6));
            if (i < 2)
                continue;
            timings.Add(plan.SearchMilliseconds);
            expansions.Add(plan.ExpandedStates);
            if (plan.Status == MovementSearchStatus.Bounded)
                ++bounded;
            Check(plan.HasVerifiedRoute && (plan.Status != MovementSearchStatus.Bounded || plan.Bound != MovementSearchBound.None),
                "Search retains a verified incumbent and identifies the real stopping constraint.");
        }
        timings.Sort();
        Console.WriteLine($"Synthetic 6400-cell optimization, freshness/departure governed: median={timings[timings.Count / 2]:f3}ms max={timings[^1]:f3}ms expansions={expansions.Min()}..{expansions.Max()} bounded={bounded}/{timings.Count}; excludes snapshot capture and native recovery.");
    }

    private static void NativeIncumbentAndExactPositions()
    {
        var map = Grid(3, 1);
        map.PixelPriority[2] = 5;
        var context = Context(map, 0);
        var seed = NativeMovementIncumbent.CaptureRoute(context, [Point(map, 0), Point(map, 2)]);
        Check(seed != null, "Strict complete native route can seed optimization.");
        var result = JointMovementSearch.Search(context, new(0, 100), nativeIncumbent: seed);
        Check(result.Status == MovementSearchStatus.Bounded && result.UsefulDestination == Point(map, 2), "Budget exhaustion must not discard the already verified native recovery destination.");
        map.PixelMaxG[1] = -1;
        Check(NativeMovementIncumbent.CaptureRoute(Context(map, 0), [Point(map, 0), Point(map, 2)]) == null, "A blocked native witness is not elective safety evidence.");
        map.PixelMaxG[1] = float.MaxValue;
        var exact = Point(map, 2) + new WDir(0.2f, 0);
        context = Context(map, 0, committed: exact);
        result = JointMovementSearch.Search(context, Exhaustive);
        Check(result.UsefulDestination == exact, "Previously committed position must be considered without snapping it to a cell center.");
        var action = Action(map, 0.5f, 1, [0, 0, 1], [0, 0, 3]);
        PlanningMobility[] movements = [new(0, 2, MovementStageKind.TargetDash, 13, 99, null, 0, 0.1f, 0, 0, LandingPosition: exact)];
        context = Context(map, 0, [action], movements, 1);
        result = JointMovementSearch.Search(context, Exhaustive);
        Check(result.Stages.Single(s => s.Kind == MovementStageKind.TargetDash).To == exact, "Mobility uses the executable landing rather than a raster approximation.");
    }

    private static void NativeConstraintReview()
    {
        var map = Grid(3, 1);
        var goals = Enumerable.Range(0, 80).Select(_ => new Func<WPos, float>(_ => 1)).ToArray();
        var field = FrozenGoalField.Capture(map, goals);
        Check(field.Goals == 80 && field.Aggregate(0) == 80, "Native goals are not truncated to an IPC matrix limit.");
        var movedMap = new Map();
        movedMap.InitGrid(1, new WPos(10, 10), 3, 1);
        try
        {
            field.ApplyNativePriorities(movedMap);
            Check(false, "Same-size geometry from another position must be rejected.");
        }
        catch (ArgumentException) { Check(true, "Goal field checks map identity, not only cell count."); }

        map.PixelPriority[2] = 10;
        var context = Context(map, 0);
        var updates = new List<MovementPlan>();
        var plan = JointMovementSearch.Search(context, Exhaustive, publishBest: updates.Add);
        Check(plan.Status == MovementSearchStatus.Complete && updates.Count >= 2, "Search publishes improving complete routes before completion.");
        Check(updates.All(p => p.Status == MovementSearchStatus.Searching && p.HasVerifiedRoute
            && MovementPlanValidation.Validate(p, context, 0, true, context.Identity.CapturedTimestamp).Allowed),
            "Every interim publication is independently executable and accurately marked as still searching.");
        var bounded = JointMovementSearch.Search(context, new(CreatedStates: 1, RespectFreshness: false));
        Check(bounded.Status == MovementSearchStatus.Bounded && bounded.Bound == MovementSearchBound.StateMemory,
            "Discarded alternatives cannot be reported as an exhaustive search even when the queue becomes empty.");

        using var started = new ManualResetEventSlim();
        using var finish = new ManualResetEventSlim();
        using var service = new MovementPlanningService((input, cancel, publish) =>
        {
            publish(updates[^1]);
            started.Set();
            if (!finish.Wait(5000))
                throw new TimeoutException();
            return plan;
        }, () => context.Identity.CapturedTimestamp);
        service.Submit("normal", 1, context);
        try
        {
            Check(started.Wait(5000), "Incremental worker started.");
            Check(service.Poll("normal", 1, context.Identity.WorldTime) is { Status: MovementSearchStatus.Searching, HasVerifiedRoute: true }
                && service.Busy, "Framework obtains a verified plan without waiting for optimization to finish.");
            service.Release("normal");
            Check(service.Poll("normal", 1, context.Identity.WorldTime) == null, "Released owners cannot receive an interim plan.");
        }
        finally { finish.Set(); }
    }

    private sealed class MutableCircle : ShapeDistance
    {
        internal WPos Center;
        public override float Distance(in WPos point) => (point - Center).Length() - 1;
    }

    private static void NativeSnapshotParity()
    {
        var identity = new NavigationIdentity(1, DateTime.UtcNow, DateTimeOffset.UtcNow, Stopwatch.GetTimestamp(),
            new WPos(-2.5f, -2.5f), false, 6, 0, null, 7, 1, 1);
        var shape = new MutableCircle { Center = new(0.5f, 0.5f) };
        var target = new WPos(2.5f, 2.5f);
        var source = NavigationMapSource.Capture(new ArenaBoundsSquare(4, mapResolution: 1), default, null, default, false);
        var input = new NavigationDecision.CapturedInput(source, [], [(shape, identity.WorldTime.AddSeconds(4), 1)],
            new([point => (point - target).LengthSq() < 4 ? 10 : 0]), true, [], identity, true);
        var nativeContext = new NavigationDecision.Context();
        var native = NavigationDecision.BuildCaptured(nativeContext, input);
        var snapshot = NativeNavigationSnapshot.Capture(input);
        shape.Center = new(100, 100);
        target = new(100, 100);
        var frozenContext = new NavigationDecision.Context();
        var frozen = snapshot.Recover(frozenContext);
        Check(native.Destination == frozen.Destination && native.NextWaypoint == frozen.NextWaypoint
            && native.TimeToGoal == frozen.TimeToGoal && native.LeewaySeconds == frozen.LeewaySeconds,
            "Frozen native recovery exactly matches the original search despite subsequent shape and goal mutation.");
        Check(frozenContext.Map.PixelMaxG.Take(snapshot.Cells).SequenceEqual(nativeContext.Map.PixelMaxG.Take(snapshot.Cells))
            && frozenContext.Map.PixelPriority.Take(snapshot.Cells).SequenceEqual(nativeContext.Map.PixelPriority.Take(snapshot.Cells)),
            "Both planners consume the same native rasterization, with no separate hazard model.");
        var strict = snapshot.CopyStrictMap();
        Array.Fill(strict.PixelMaxG, -1);
        Check(snapshot.CopyStrictMap().PixelMaxG.Any(v => v == float.MaxValue), "Returned map cannot mutate captured native safety.");

        var trappedInput = input with { Warnings = [], Obstacles = [new SDCircle(identity.Origin, .6f)] };
        var trapped = NativeNavigationSnapshot.Capture(trappedInput);
        var escape = trapped.Recover(new());
        Check(escape.Publication is { Destination: not null } escapeRoute
            && trapped.CanExecuteRecovery(escapeRoute, identity.Origin),
            "A fresh native escape from an occupied blocked raster cell is executable; elective start-cell validation cannot deadlock recovery.");
        var stuckContext = new MovementPlanningContext(trappedInput.Identity, trapped.CopyStrictMap(), 0, 0,
            Enumerable.Repeat(true, trapped.Cells).ToArray(), new float[trapped.Cells], [], [], native: trapped);
        Check(!JointMovementSearch.Search(stuckContext, Exhaustive).HasVerifiedRoute,
            "Emergency native egress does not authorize elective movement from an impassable start.");
        var recoveryInput = input with { Warnings = [], Goals = new([point => point.X > 1 && point.Z > 1 ? 10 : 0]) };
        var recoverySnapshot = NativeNavigationSnapshot.Capture(recoveryInput);
        var rangeContext = new MovementPlanningContext(recoveryInput.Identity, recoverySnapshot.CopyStrictMap(), 0, 0,
            Enumerable.Repeat(true, recoverySnapshot.Cells).ToArray(), new float[recoverySnapshot.Cells], [], [], native: recoverySnapshot,
            range: new(10, new(2.5f, 2.5f), 2, RecoverExecution: true));
        var rangeRecovery = NativeMovementSearch.Recover(rangeContext, new());
        Check(NativeMovementHost.CanRecoverCombatRange(rangeContext, rangeRecovery),
            "Optional optimization cannot stall a complete verified native route restoring melee access.");
        Check(!NativeMovementHost.CanRecoverCombatRange(stuckContext, rangeRecovery),
            "Pending melee optimization cannot bypass a changed map or impassable start.");
        var partialRange = new MovementPlanningContext(recoveryInput.Identity, recoverySnapshot.CopyStrictMap(), 0, 0,
            Enumerable.Repeat(true, recoverySnapshot.Cells).ToArray(), new float[recoverySnapshot.Cells], [], [], native: recoverySnapshot,
            range: new(10, new(12.5f, 12.5f), 2, RecoverExecution: true));
        Check(NativeMovementHost.CanRecoverCombatRange(partialRange, rangeRecovery),
            "A complete safe native route making real progress toward combat range cannot be frozen until optimization finds an all-the-way endpoint.");
        var mobilityContext = new MovementPlanningContext(recoveryInput.Identity, recoverySnapshot.CopyStrictMap(), 0, 0,
            Enumerable.Repeat(true, recoverySnapshot.Cells).ToArray(), new float[recoverySnapshot.Cells], [], [], native: recoverySnapshot,
            range: new(10, new(2.5f, 2.5f), 1, RecoverExecution: true), resources: 1,
            mobilityMechanics: [new(7, MobilityMechanic.Target, 20, 0, 0, 0, .1f, 0, [new(10, new(2.5f, 2.5f), 0)])]);
        var publications = new List<MovementPlan>();
        NativeMovementSearch.Run(mobilityContext, default, publications.Add);
        Check(publications.Count > 1 && publications[1].Status == MovementSearchStatus.Searching
            && publications[1].Stages.Any(s => s.ActionId == 7),
            "A complete useful dash is published from the initial opportunity pass while its other alternatives are still being searched.");

        var urgent = input with { Warnings = [(new SDCircle(identity.Origin, 1.1f), identity.WorldTime.AddSeconds(1.8), 1)],
            Goals = new([point => point.X > 1 ? 10 : 0]) };
        var urgentSnapshot = NativeNavigationSnapshot.Capture(urgent);
        var recoveryMap = urgentSnapshot.CopyRecoveryMap();
        var jointMap = urgentSnapshot.CopyStrictMap();
        Check(recoveryMap.PixelPriority.Take(urgentSnapshot.Cells).Max() == 0
            && jointMap.PixelPriority.Take(urgentSnapshot.Cells).Any(v => v == 10),
            "urgent native recovery retains its baseline while joint planning captures combat goals before the first move");
        var urgentContext = new MovementPlanningContext(urgent.Identity, jointMap, 0, 0,
            Enumerable.Repeat(true, urgentSnapshot.Cells).ToArray(), urgentSnapshot.CopyPresentation(), [], [], native: urgentSnapshot);
        var urgentPlan = JointMovementSearch.Search(urgentContext, Exhaustive);
        Check(urgentPlan.HasVerifiedRoute && urgentPlan.NativePriority == 10 && urgentPlan.UsefulDestination.X > 1,
            "the first escape destination already satisfies native rotation goals even without RSR action evidence");
        var casting = NativeNavigationSnapshot.Capture(input with { Identity = identity with { Casting = true }, Warnings = [],
            Goals = new([point => point.X > 1 ? 10 : 0]) });
        Check(casting.CopyRecoveryMap().PixelPriority.Take(casting.Cells).All(v => v == 0)
            && casting.CopyStrictMap().PixelPriority.Take(casting.Cells).Any(v => v == 10),
            "casting does not hide the useful destination from a joint plan that respects cast completion");

        input = input with { Warnings = [(new SDCircle(default, 100), identity.WorldTime.AddSeconds(4), 1)] };
        snapshot = NativeNavigationSnapshot.Capture(input);
        frozen = snapshot.Recover(new());
        Check(frozen.Publication is { NativeRelaxedWarnings: true } && snapshot.CopyStrictMap().PixelMaxG.Take(snapshot.Cells).All(v => v == 3),
            "Native all-unsafe recovery retains its relaxation while elective planning retains the original deadline.");

        input = input with { Warnings = [], Teleporters = [new(new(-2.5f, -2.5f), new(2.5f, 2.5f), 0.4f, true)] };
        snapshot = NativeNavigationSnapshot.Capture(input);
        frozen = snapshot.Recover(new());
        Check(snapshot.HasTeleporters && frozen.Publication is { HasTeleporters: true }, "Native authored teleporters survive snapshot capture and recovery.");
    }

    private static void NativeOwnership()
    {
        long now = 0;
        var ownership = new OwnedModuleEnablement(() => now, 1000);
        var scope = ownership.AcquireLocal(10, true, false)!;
        now = 1_000_000;
        Check(scope.Active && ownership.Read(10) is { SavedEnabled: true, Enabled: false }, "Native scope needs no cross-plugin lease renewal.");
        Check(ownership.AcquireLocal(10, true, true) == null, "Local ownership remains exclusive.");
        ownership.UserIntent(10, false);
        Check(!scope.Active, "An explicit user choice revokes the local override even when its value is equal.");
        scope.Dispose();
        Check(ownership.Read(10) is { SavedEnabled: false, Enabled: false }, "Disposing a revoked scope cannot overwrite the user's saved choice.");
        scope = ownership.AcquireLocal(10, false, true)!;
        scope.Dispose();
        Check(ownership.Read(10) is { SavedEnabled: false, Enabled: false }, "Normal native scope disposal reveals the original saved setting.");
        var scopes = Enumerable.Range(100, 150).Select(i => ownership.AcquireLocal((uint)i, true, false)).ToArray();
        Check(scopes.All(s => s is { Active: true }), "Native ownership does not inherit remote owner/watch count limits.");
        foreach (var item in scopes) item!.Dispose();
    }

    private static void NativeProblemChanges()
    {
        var map = Grid(3, 1);
        map.PixelMaxG[1] = 3;
        var before = Context(map, 0);
        var identity = before.Identity with { Generation = 2, WorldTime = before.Identity.WorldTime.AddSeconds(0.1) };
        map.PixelMaxG[1] = 2.9f;
        var after = new MovementPlanningContext(identity, map, 0, 0, [true, true, true], [0, 0, 0], [], []);
        Check(after.SameProblem(before), "Equivalent deadline countdowns keep useful work rather than cancelling it every frame.");
        var cosmetic = new MovementPlanningContext(identity, map, 0, 0, [true, true, true], [2, 0, 1], [], []);
        Check(cosmetic.SameProblem(before), "cosmetic preference changes alone do not interrupt a committed walk or restart an arrived player");
        map.PixelMaxG[1] = 1;
        after = new(identity, map, 0, 0, [true, true, true], [0, 0, 0], [], []);
        Check(!after.SameProblem(before), "A genuinely earlier warning deadline supersedes the planning job.");
        map.PixelMaxG[1] = 2.9f;
        after = new(identity, map, 0, 0, [true, true, true], [0, 0, 0], [], [], targetId: 123);
        Check(!after.SameProblem(before), "A changed target invalidates work even at the same position.");

        var currentAction = Action(map, 12, 1, [1, 1, 1], [1, 1, 1]) with { Earliest = 1, HorizonLimited = true };
        // No finite warnings in this comparison: the action's forecast horizon is a
        // computational limit, whereas an authored execution deadline is absolute.
        Array.Fill(map.PixelMaxG, float.MaxValue);
        before = Context(map, 0, [currentAction]);
        after = new(before.Identity with { WorldTime = before.Identity.WorldTime.AddSeconds(0.1) }, map, 0, 0,
            [true, true, true], [0, 0, 0], [currentAction with { Earliest = 0.9f }], []);
        Check(after.SameProblem(before), "Rolling forecast limits do not cancel the current action search on every frame.");
        after = new(after.Identity, map, 0, 0, [true, true, true], [0, 0, 0],
            [currentAction with { Earliest = 0.9f, HorizonLimited = false }], []);
        Check(!after.SameProblem(before), "An authored action deadline remains distinct from a forecast limit.");

        map.PixelMaxG[1] = 1.6f;
        map.PixelPriority[2] = 2;
        var action = Action(map, 0.9f, 1, [0, 1, 0], [0, 10, 0]);
        PlanningMobility[] mobility = [new(0, 1, MovementStageKind.DirectionalMovement, 15, 0, default(Angle), 0.6f, 0.1f, 0, 0)];
        var context = Context(map, 0, [action], mobility, 1);
        var result = JointMovementSearch.Search(context, Exhaustive);
        Check(result.DeliveredActions == 1 && result.Stages.Any(s => s.ActionId == 15 && s.PreparationSeconds == 0.6f),
            "Deliberate facing/setup time remains part of the executable mobility stage.");
        Check(MovementPlanValidation.Validate(result, context, 0, true, context.Identity.CapturedTimestamp).Allowed,
            "Final validation counts action setup exactly once and retains the evidenced delivery window.");
    }

    private static void NativeHostLifecycle()
    {
        // This boundary only consumes frame time and territory identity. Constructing
        // ClientState would require Lumina action sheets unrelated to this fixture.
        var world = (WorldState)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldState));
        world.CurrentZone = 1;
        world.CurrentCFCID = 1;
        world.Frame.Timestamp = DateTime.UtcNow;
        var actor = (Actor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        typeof(Actor).GetField(nameof(Actor.InstanceID))!.SetValue(actor, 7UL);
        actor.PosRot = new(-2.5f, 0, -2.5f, 0);
        var hints = new AIHints { PathfindMapBounds = new ArenaBoundsSquare(4, mapResolution: 1) };
        hints.GoalZones.Add(p => p.X > 1 && p.Z > 1 ? 10 : 0);
        using var host = new NativeMovementHost(new MovementPlanningService((context, cancel) =>
        {
            cancel.WaitHandle.WaitOne();
            throw new OperationCanceledException(cancel);
        }));
        var inspection = new NavigationDecision.Context();
        var decision = host.Update("normal", world, hints, actor, 6, 0, inspection);
        Check(decision.Destination != null && host.Execution?.Kind == MovementExecutionKind.Walk,
            "Native movement recovers in the same frame even when the optimizer never publishes.");
        Check(decision.Publication is { PublishedUtc: not null } && host.CurrentPlan is { Status: MovementSearchStatus.NativeRecovery },
            "Intermediate native-only lifecycle truthfully reports recovery, not completed combat optimization.");
        hints.GoalZones.Clear();
        hints.ForbiddenZones.Add((new SDCircle(actor.Position, 1.5f), world.CurrentTime, 0));
        foreach (var intent in new MovementIntent?[] { null, new(ExplicitDestination: new(2, 2)) })
        {
            decision = host.Update("normal", world, hints, actor, 6, 0, inspection, intent);
            Check(decision.Destination != null && host.Execution?.Kind == MovementExecutionKind.Walk,
                "A newly active damaging area receives immediate native egress even during optimization while the worker is blocked.");
        }
        hints.ForbiddenZones.Clear();
        hints.GoalZones.Add(p => p.X > 1 && p.Z > 1 ? 10 : 0);
        hints.NoMovementAllowsManualInput = true;
        hints.ImminentSpecialMode = (AIHints.SpecialMode.NoMovement, world.CurrentTime, world.FutureTime(10));
        Check(host.Update("normal", world, hints, actor, 6, 0, inspection).Destination == null && host.CurrentPlan == null,
            "Unsupported projection/manual handoff clears native work immediately.");
        hints.NoMovementAllowsManualInput = false;
        hints.ImminentSpecialMode = default;
        actor.IsDead = true;
        Check(host.Update("follow", world, hints, actor, 6, 0, inspection).Destination == null, "Death cannot submit movement.");
        actor.IsDead = false;
        Check(host.Update("follow", world, hints, actor, 6, 0, inspection).Destination != null,
            "Following after resurrection recovers immediately while superseded work is still cancelling.");
        host.Release("follow");
        Check(host.CurrentPlan == null, "Follow unload releases its result.");
        var behavior = typeof(AIHints).Assembly.GetType("BossMod.AI.AIBehaviour", true)!;
        Check(behavior.GetMethod("Execute")!.ReturnType == typeof(void), "Follow control no longer continues asynchronously against live actors.");
    }

    private sealed class ThreadBoundShape(int owner) : ShapeDistance
    {
        public override float Distance(in WPos point)
        {
            if (Environment.CurrentManagedThreadId != owner)
                throw new InvalidOperationException("Mutable geometry callback escaped its capture thread.");
            return point.X + 30;
        }
    }

    private static void CaptureThreadAndCost()
    {
        var thread = Environment.CurrentManagedThreadId;
        var identity = new NavigationIdentity(1, DateTime.UtcNow, DateTimeOffset.UtcNow, Stopwatch.GetTimestamp(),
            default, false, 6, 0, null, 7, 1, 1);
        var source = NavigationMapSource.Capture(new ArenaBoundsSquare(40, mapResolution: 1), default, null, default, false);
        var input = new NavigationDecision.CapturedInput(source, [new ThreadBoundShape(thread)],
            [(new ThreadBoundShape(thread), identity.WorldTime.AddSeconds(5), 1)],
            new([point => Environment.CurrentManagedThreadId == thread ? (point.X > 10 ? 1 : 0)
                : throw new InvalidOperationException("Mutable goal escaped its capture thread.")]), true, [], identity, false);
        _ = NativeNavigationSnapshot.Capture(input);
        Check(true, "Opaque native goals and shapes are evaluated on the capture thread, including raster callbacks.");
        var timings = new List<double>();
        for (var i = 0; i < 12; ++i)
        {
            var watch = Stopwatch.StartNew();
            _ = NativeNavigationSnapshot.Capture(input);
            if (i >= 2) timings.Add(watch.Elapsed.TotalMilliseconds);
        }
        timings.Sort();
        Console.WriteLine($"Synthetic 6400-cell native capture, one goal/obstacle/warning: median={timings[timings.Count / 2]:f3}ms max={timings[^1]:f3}ms; framework capture only, not live frame-time acceptance.");
    }
}
