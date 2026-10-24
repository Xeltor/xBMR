using BossMod;
using BossMod.Components;
using BossMod.Pathfinding;
using BossMod.CombatAssistance.Knockback;
using BossMod.CombatAssistance.Planning;
using System.Collections.Immutable;
using System.Diagnostics;

public static partial class Checks
{
    private static void JointKnockbackPlanning()
    {
        var now = DateTime.UtcNow;
        var identity = new NavigationIdentity(1, now, DateTimeOffset.UtcNow, Stopwatch.GetTimestamp(),
            new(-2.5f, .5f), false, 6, 0, null, 7, 1, 1);
        var source = NavigationMapSource.Capture(new ArenaBoundsSquare(4, mapResolution: 1), default, null, default, false);
        var origin = new WPos(-5, 0);
        var pure = new SDKnockbackInCircleAwayFromOrigin(default, origin, 2, 3.5f);
        var envelope = NativeLandingConstraints.ProjectionEnvelope(pure)!.Value;
        var raw = new AuthoredKnockbackSource(0, 0, "synthetic-native-source", origin, 2, now.AddSeconds(2),
            GenericKnockback.Kind.AwayFromOrigin, default, 0, 10, false, false, true, int.MaxValue,
            true, null, false, false, null, null, []);
        var inventory = new NativeDisplacementInventory(now, 7, 1, 1, [raw], 1, 0, 0, true, false, false, false)
        { HintsInspected = true, HintInspectionComplete = true };
        var afterInput = new NavigationDecision.CapturedInput(source, [], [], new([p => p.X > 1 && Math.Abs(p.Z) < 2 ? 100 : 0]),
            true, [], identity, false);
        var beforeInput = afterInput with { Warnings = [(pure, raw.Activation, 10)] };
        var before = NativeNavigationSnapshot.Capture(beforeInput);
        var after = NativeNavigationSnapshot.Capture(afterInput);
        var phase = new NativeKnockbackPhase(raw, after, [envelope], 1, 0)
        { Timing = new(now.AddSeconds(2), now.AddSeconds(2.1), now.AddSeconds(2.2), now.AddSeconds(3)) };
        MovementPlanningContext Capture(params NativeKnockbackPhase[] phases)
        {
            var map = before.CopyStrictMap();
            return new(identity, map, 0, 0, Enumerable.Repeat(true, map.Width * map.Height).ToArray(),
                new float[map.Width * map.Height], [], [], native: before,
                knockbackPhases: new(inventory, [.. phases], "synthetic corroborated timing"));
        }
        var context = Capture(phase);
        var finalMap = context.WithDisplacementMap(after);
        Check(Enumerable.Range(0, finalMap.Cells).Max(finalMap.NativePriority) == 100,
            "The reference goal covers a full native cell under conservative four-corner sampling.");
        var plan = JointMovementSearch.Search(context, Exhaustive);
        Check(plan.HasVerifiedRoute && plan.Stages.Count(stage => stage.Kind == MovementStageKind.Knockback) == 1
            && plan.NativePriority == 100 && plan.UsefulDestination.X > 1,
            $"Joint search selects preparation using the forced landing and subsequent native goal: {plan.Status}/{plan.Bound}, priority={plan.NativePriority}, destination={plan.UsefulDestination}, stages={string.Join(",", plan.Stages.Select(stage => stage.Kind))}, reason={plan.Evidence}.");
        Check(plan.Stages.Single(stage => stage.Kind == MovementStageKind.Knockback) is { StartSeconds: 2, EndSeconds: 3 },
            "A forced transition retains the original earliest onset and latest landing rather than pretending instantaneous motion.");
        Check(!MovementPlanValidation.Validate(plan, context, 0, true, identity.CapturedTimestamp).Allowed,
            "A geometry-only phase without native cast/source identity cannot actuate the new stage.");
        var boundSource = raw with { CastActionId = 100, SourceDataId = 222 };
        var boundPhase = phase with { Source = boundSource, CastGeneration = 1 };
        var boundPhases = new NativeKnockbackPhases(inventory with { Knockbacks = [boundSource] }, [boundPhase], "native cast binding");
        var boundContext = new MovementPlanningContext(identity, before.CopyStrictMap(), 0, 0,
            Enumerable.Repeat(true, context.Cells).ToArray(), new float[context.Cells], [], [], native: before, knockbackPhases: boundPhases);
        var boundPlan = JointMovementSearch.Search(boundContext, Exhaustive);
        Check(MovementPlanValidation.Validate(boundPlan, boundContext, 0, true, identity.CapturedTimestamp).Allowed,
            "Fresh native cast identity, complete phase sequence and exact swept corridor validate the complete planned route.");
        var immunity = new PlanningUtility(7559, PlanningUtilityEffect.KnockbackImmunity, 6, .6f, 0, 0, ApplicationSeconds: .5f);
        var throughPush = new PlanningAction(103, 0, 2.4f, 2.8f, 0, .1f, 1,
            Enumerable.Repeat(1ul, context.Cells).ToArray(), Enumerable.Repeat(1f, context.Cells).ToArray(), RecipientIds: [11]);
        MovementPlanningContext ImmunityContext(PlanningAction attack, PlanningUtility utility, bool ignores = false,
            ulong resources = 1, NativeNavigationSnapshot? afterOverride = null, bool planLandings = true)
        {
            var immuneSource = boundSource with { IgnoresImmunity = ignores };
            return new(identity, before.CopyStrictMap(), 0, 0,
                Enumerable.Repeat(true, context.Cells).ToArray(), new float[context.Cells], [attack], [],
                resources: resources, native: before, utilities: [utility], knockbackPhases: boundPhases with
                {
                    PlanLandings = planLandings,
                    Inventory = boundPhases.Inventory with { Knockbacks = [immuneSource] },
                    Phases = [boundPhase with { Source = immuneSource, After = afterOverride ?? after }]
                });
        }
        var immunityContext = ImmunityContext(throughPush, immunity);
        Check(NativeKnockbackPhases.SafePhysicalFallback(immunityContext, 0, context.Origin, .6f)
            && !NativeKnockbackPhases.SafePhysicalFallback(immunityContext, 0, context.Origin, 2.1f),
            "The unconfirmed-buff fallback uses native launch/flight/landing deadlines including the spent action lock.");
        var stationaryBefore = NativeNavigationSnapshot.Capture(beforeInput with { Goals = new([]) });
        var stationaryAfter = NativeNavigationSnapshot.Capture(afterInput with { Goals = new([]) });
        var stationaryImmunity = new MovementPlanningContext(identity, stationaryBefore.CopyStrictMap(), 0, 0,
            Enumerable.Repeat(true, context.Cells).ToArray(), new float[context.Cells], [throughPush], [], resources: 1,
            native: stationaryBefore, utilities: [immunity], knockbackPhases: boundPhases with
            { Phases = [boundPhase with { After = stationaryAfter }] });
        var stationaryImmunityPlan = JointMovementSearch.Search(stationaryImmunity, Exhaustive);
        var immunityExecutor = new MovementPlanExecutor();
        Check(immunityExecutor.Advance(stationaryImmunityPlan, stationaryImmunity, default, true, identity.CapturedTimestamp)
            is { Kind: MovementExecutionKind.Wait, ActionId: 7559 },
            "Native immunity waits for original rotation selection instead of submitting a separate action.");
        Check(immunityExecutor.Advance(stationaryImmunityPlan, stationaryImmunity,
            new(default, 7559, identity.ActorId, now), true, identity.CapturedTimestamp).Kind == MovementExecutionKind.Invalidated,
            "Observed ability delivery without native buff confirmation cannot unlock immunity-dependent walking.");
        Check(new MovementPlanExecutor().Advance(stationaryImmunityPlan, stationaryImmunity, default, false,
            identity.CapturedTimestamp).Kind == MovementExecutionKind.Invalidated,
            "Manual authority prevents a planned immunity request through the shared executor.");
        var immunityOnlyContext = ImmunityContext(throughPush, immunity, planLandings: false);
        var immunityOnly = JointMovementSearch.Search(immunityOnlyContext, Exhaustive);
        Check(immunityOnly.HasVerifiedRoute && immunityOnly.Stages.Any(stage => stage.PreventedByActionId == 7559)
            && !immunityOnly.Stages.Any(stage => stage.Kind == MovementStageKind.Knockback && stage.PreventedByActionId == 0)
            && MovementPlanValidation.Validate(immunityOnly, immunityOnlyContext, 0, true, identity.CapturedTimestamp).Allowed,
            "Immunity remains available when landing preparation is opted out, without silently enabling physical preparation.");
        Check(!JointMovementSearch.Search(ImmunityContext(throughPush, immunity, resources: 0, planLandings: false), Exhaustive).HasVerifiedRoute,
            "Unavailable immunity with landing preparation off retains native recovery rather than promising a physical plan.");
        Check(!MovementPlanValidation.Validate(boundPlan, immunityOnlyContext, 0, true, identity.CapturedTimestamp).Allowed,
            "A changed landing opt-out invalidates a previously selected physical transition immediately.");
        var immunePlan = JointMovementSearch.Search(immunityContext, Exhaustive);
        Check(immunePlan.HasVerifiedRoute && immunePlan.DeliveredActions == 1 && (immunePlan.RemainingResources & 15) == 0
            && immunePlan.Stages.Any(stage => stage.PreventedByActionId == 7559 && stage.From == stage.To)
            && MovementPlanValidation.Validate(immunePlan, immunityContext, 0, true, identity.CapturedTimestamp).Allowed,
            $"Immunity is part of the same route/resource search and preserves an action otherwise lost during flight: {immunePlan.Status}/{immunePlan.Bound}, actions={immunePlan.DeliveredActions}.");
        var immuneReference = JointMovementSearch.Search(immunityContext, Exhaustive, pruneExecutionInferiorStates: false,
            pruneResourceInferiorMobility: false);
        Check(immuneReference.DeliveredActions == immunePlan.DeliveredActions
            && immuneReference.RemainingResources == immunePlan.RemainingResources
            && immuneReference.DeliveryTimes.SequenceEqual(immunePlan.DeliveryTimes),
            "Reference search agrees on immunity, action delivery and spent resource.");
        MovementPlanningContext SuccessiveImmunity(bool ignoresSecond, float secondAt, PlanningAction attack)
        {
            var secondSource = boundSource with { Index = 1, Distance = 1, Activation = now.AddSeconds(secondAt), IgnoresImmunity = ignoresSecond };
            var secondPhase = boundPhase with { Source = secondSource, CastGeneration = 2,
                Timing = new(now.AddSeconds(secondAt), now.AddSeconds(secondAt + .1f),
                    now.AddSeconds(secondAt + .2f), now.AddSeconds(secondAt + 1)) };
            return new(identity, before.CopyStrictMap(), 0, 0,
                Enumerable.Repeat(true, context.Cells).ToArray(), new float[context.Cells], [attack], [], resources: 1,
                native: before, utilities: [immunity], knockbackPhases: boundPhases with
                { Inventory = boundPhases.Inventory with { Knockbacks = [boundSource, secondSource] }, Phases = [boundPhase, secondPhase] });
        }
        var doubleImmunityContext = SuccessiveImmunity(false, 4, throughPush);
        var doubleImmunity = JointMovementSearch.Search(doubleImmunityContext, Exhaustive);
        Check(doubleImmunity.DeliveredActions == 1 && doubleImmunity.Stages.Count(stage => stage.PreventedByActionId == 7559) == 2
            && MovementPlanValidation.Validate(doubleImmunity, doubleImmunityContext, 0, true, identity.CapturedTimestamp).Allowed,
            $"One original immunity window can cover successive sources without spending or refreshing it twice: {doubleImmunity.Status}/{doubleImmunity.Bound}, actions={doubleImmunity.DeliveredActions}, stages={string.Join(',', doubleImmunity.Stages.Select(s => $"{s.Kind}/{s.PreventedByActionId}"))}, validation={MovementPlanValidation.Validate(doubleImmunity, doubleImmunityContext, 0, true, identity.CapturedTimestamp)}.");
        Check(JointMovementSearch.Search(SuccessiveImmunity(true, 4, throughPush with { Earliest = 4.4f, Latest = 4.8f }),
            Exhaustive).DeliveredActions == 0,
            "An immunity-ignoring second source remains a physical event even after an earlier source could be prevented.");
        var expiredImmunityContext = SuccessiveImmunity(false, 8, throughPush);
        var expiredImmunity = JointMovementSearch.Search(expiredImmunityContext, Exhaustive);
        Check(expiredImmunity.DeliveredActions == 1
            && expiredImmunity.Stages.Count(stage => stage.PreventedByActionId == 7559) == 1
            && expiredImmunity.Stages.Count(stage => stage.Kind == MovementStageKind.Knockback && stage.PreventedByActionId == 0) == 1
            && MovementPlanValidation.Validate(expiredImmunity, expiredImmunityContext, 0, true, identity.CapturedTimestamp).Allowed,
            "A later event outside the original immunity lifetime still requires its actual safe launch, flight and landing.");
        Check(JointMovementSearch.Search(ImmunityContext(throughPush, immunity, ignores: true), Exhaustive).DeliveredActions == 0
            && JointMovementSearch.Search(ImmunityContext(throughPush, immunity, resources: 0), Exhaustive).DeliveredActions == 0
            && JointMovementSearch.Search(ImmunityContext(throughPush, immunity with { ReadyIn = 1.6f }), Exhaustive).DeliveredActions == 0,
            "Immunity-ignoring sources, unavailable charges and late application cannot preserve a flight-time action.");
        var equivalentPush = JointMovementSearch.Search(ImmunityContext(throughPush with { Earliest = 0, Latest = 1.8f }, immunity), Exhaustive);
        Check(equivalentPush.DeliveredActions == 1 && equivalentPush.RemainingResources == 1
            && equivalentPush.Stages.All(stage => stage.PreventedByActionId == 0),
            "A useful safe push retains the immunity resource when current execution is equivalent.");
        Check(!boundPhase.Preventable(boundContext, 2, 8) && !boundPhase.Preventable(boundContext, .5f, 2.2f),
            "Application at earliest onset or expiry at latest onset cannot certify immunity over the entire window.");
        Check(!boundPhase.Preventable(boundContext, .5f, 2.1f) && boundPhase.MayBePrevented(boundContext, 0, 2.6f),
            "A partially overlapping application/expiry window certifies neither prevention nor an ordinary physical push.");
        var impossiblePush = boundPlan with { Stages = ImmutableArray.Create(new MovementStage(MovementStageKind.UtilityAction,
            context.Origin, context.Origin, 0, immunity.LockSeconds, immunity.ActionId, Utility: immunity)).AddRange(boundPlan.Stages) };
        Check(!MovementPlanValidation.Validate(impossiblePush, immunityContext, 0, true, identity.CapturedTimestamp).Allowed,
            "Exact validation cannot accept a useful physical push while planned immunity may automatically prevent it.");
        Check(!(boundPhase with { Source = boundSource with { Activation = now.AddSeconds(7) } })
            .Preventable(boundContext, .5f, 6),
            "An earlier learned window cannot conceal a later authored activation outside the buff's lifetime.");
        var independentDanger = NativeNavigationSnapshot.Capture(afterInput with
            { Warnings = [(new SDCircle(default, 100), now.AddSeconds(1), 0)] });
        Check(!JointMovementSearch.Search(ImmunityContext(throughPush, immunity, afterOverride: independentDanger), Exhaustive).HasVerifiedRoute,
            "Preventing displacement cannot erase an independent lethal warning or manufacture an onward escape.");
        Check(!MovementPlanValidation.Validate(immunePlan, ImmunityContext(throughPush, immunity with { ReadyIn = 1.6f }),
            0, true, identity.CapturedTimestamp).Allowed,
            "Fresh native application/readiness changes invalidate the original immunity route.");
        var immuneUtility = Array.FindIndex(immunePlan.Stages.ToArray(), stage => stage.Utility?.Effect == PlanningUtilityEffect.KnockbackImmunity);
        Check(immuneUtility >= 0 && !MovementPlanValidation.Validate(immunePlan, immunityContext,
            immuneUtility + 1, true, identity.CapturedTimestamp).Allowed,
            "A consumed action request does not stand in for an observed native immunity buff.");
        var immuneOverlay = BossMod.CombatAssistance.UI.NativePlanOverlay.Capture(immunePlan, null, 0, [], 0);
        Check(immuneOverlay.Markers.Any(marker => marker.Label == "Immunity position")
            && immuneOverlay.Markers.All(marker => marker.Label != "Knockback landing"),
            "A planned prevented push uses the existing position marker without advertising a physical landing.");
        MovementPlanningContext WithActions(params PlanningAction[] actions) => new(identity, before.CopyStrictMap(), 0, 0,
            Enumerable.Repeat(true, context.Cells).ToArray(), new float[context.Cells], actions, [], native: before, knockbackPhases: boundPhases);
        var castBefore = new PlanningAction(101, 0, 0, 1.9f, 1.8f, 1.8f, 1,
            Enumerable.Repeat(1ul, context.Cells).ToArray(), Enumerable.Repeat(1f, context.Cells).ToArray(), RecipientIds: [11]);
        var castAfter = new PlanningAction(102, 0, 0, 6, 0, 0, 1,
            Enumerable.Repeat(1ul, context.Cells).ToArray(), Enumerable.Range(0, context.Cells)
                .Select(cell => context.Point(cell).X > 1 ? 1f : 0).ToArray(), RequiredEarlierActions: 1, RecipientIds: [11]);
        var overlapContext = WithActions(castBefore, castAfter);
        var overlapPlan = JointMovementSearch.Search(overlapContext, Exhaustive);
        var overlapCast = overlapPlan.Stages.FirstOrDefault(stage => stage.ActionId == 101);
        Check(overlapPlan.DeliveredActions == 3 && overlapCast != null && overlapCast.EndSeconds < 2
            && overlapCast.ActionLockUntil > 3 && overlapPlan.DeliveryTimes[1] >= overlapCast.ActionLockUntil
            && overlapPlan.Stages.Any(stage => stage.Kind == MovementStageKind.Walk && stage.StartSeconds >= 3
                && stage.StartSeconds < overlapCast.ActionLockUntil)
            && MovementPlanValidation.Validate(overlapPlan, overlapContext, 0, true, identity.CapturedTimestamp).Allowed,
            $"A delivered cast retains recipients through the native push, permits post-landing walking during its lock, and delays the next action: {overlapPlan.Status}/{overlapPlan.Bound}, actions={overlapPlan.DeliveredActions}.");
        Check(overlapPlan.ActionEvidence.First(evidence => evidence.Id == 101).CastSeconds == 1.8f,
            "Cast evidence records delivery time independently of an animation lock overlapping displacement.");
        var noPruningOverlap = JointMovementSearch.Search(overlapContext, Exhaustive, pruneExecutionInferiorStates: false);
        Check(noPruningOverlap.DeliveredActions == overlapPlan.DeliveredActions
            && noPruningOverlap.DeliveryTimes.SequenceEqual(overlapPlan.DeliveryTimes),
            "Execution pruning agrees with the reference search when a lock survives the native landing.");
        var lateCast = JointMovementSearch.Search(WithActions(castBefore with { CastSeconds = 2, Latest = 2 }), Exhaustive);
        Check(lateCast.DeliveredActions == 0,
            "Delivery at the earliest possible push cannot be credited as a completed pre-push cast.");
        var changedLock = WithActions(castBefore with { LockSeconds = 2 }, castAfter);
        Check(!MovementPlanValidation.Validate(overlapPlan, changedLock, 0, true, identity.CapturedTimestamp).Allowed,
            "A changed native animation lock invalidates the original cast/push/next-action promise.");
        Check(!MovementPlanValidation.Validate(overlapPlan, WithActions(castBefore with
            { RecipientsByCell = new ulong[context.Cells] }, castAfter), 0, true, identity.CapturedTimestamp).Allowed,
            "Animation-lock overlap cannot conceal the loss of an independently required cast recipient.");
        var overlapExecutor = new MovementPlanExecutor();
        Check(overlapExecutor.Advance(overlapPlan, overlapContext, default, true, identity.CapturedTimestamp)
            is { Kind: MovementExecutionKind.Wait, ActionId: 101 },
            "Predicted pre-push cast completion cannot advance the executor without observed action delivery.");
        var diagnostics = new BossMod.CombatAssistance.Diagnostics.NativeDiagnosticSnapshot();
        using (var overlapJson = System.Text.Json.JsonDocument.Parse(diagnostics.Read("{\"schemaVersion\":1,\"stream\":\"combat\"}",
            overlapPlan, true, true, "Ready", null, now)))
            Check(overlapJson.RootElement.GetProperty("snapshot").GetProperty("movement").GetProperty("stages").EnumerateArray()
                .Any(stage => stage.GetProperty("actionLockUntil").ValueKind == System.Text.Json.JsonValueKind.Number),
                "Current native diagnostics distinguish delivery from the animation lock surviving a push.");
        var overlay = BossMod.CombatAssistance.UI.NativePlanOverlay.Capture(boundPlan, null, 0, [], 0);
        Check(overlay.Markers.Any(marker => marker.Label == "Knockback launch")
            && overlay.Markers.Any(marker => marker.Label == "Knockback landing"),
            "Native knockback preparation and landing use the existing in-world decision marker primitives.");
        using (var document = System.Text.Json.JsonDocument.Parse(diagnostics.Read("{\"schemaVersion\":1,\"stream\":\"combat\"}",
            boundPlan, true, true, "Ready", null, now)))
        {
            var proof = document.RootElement.GetProperty("snapshot").GetProperty("movement").GetProperty("stages")
                .EnumerateArray().Single(stage => stage.GetProperty("kind").GetString() == "Knockback").GetProperty("knockback");
            Check(proof.GetProperty("CastGeneration").GetUInt64() == 1 && !proof.TryGetProperty("after", out _),
                "Current diagnostics expose bounded source/timing proof without serializing frozen phase maps.");
        }
        var executor = new MovementPlanExecutor();
        Check(executor.Advance(boundPlan, boundContext, default, true, identity.CapturedTimestamp).Kind == MovementExecutionKind.Walk,
            "The shared executor may prepare a fully bound native landing route before the authored deadline.");
        Check(executor.Advance(boundPlan, boundContext, default, false, identity.CapturedTimestamp).Kind == MovementExecutionKind.Invalidated,
            "Native knockback preparation retains manual movement authority.");
        var pendingContext = new MovementPlanningContext(identity, before.CopyStrictMap(), 0, 0,
            Enumerable.Repeat(true, context.Cells).ToArray(), new float[context.Cells], [], [], native: before,
            knockbackPhases: boundPhases with { Inventory = boundPhases.Inventory with { Pending = true } });
        Check(!MovementPlanValidation.Validate(boundPlan, pendingContext, 0, true, identity.CapturedTimestamp).Allowed,
            "A pending native effect cannot be reinterpreted as another preparation or ordinary movement window.");
        var pushStage = boundPlan.Stages.Single(stage => stage.Kind == MovementStageKind.Knockback);
        var resolvedIdentity = identity with { Generation = 2, WorldTime = now.AddSeconds(3.1), Origin = pushStage.To,
            CapturedTimestamp = Stopwatch.GetTimestamp() };
        var resolvedMap = NativeNavigationSnapshot.Capture(afterInput with { Identity = resolvedIdentity });
        var resolvedInventory = inventory with { At = resolvedIdentity.WorldTime, Knockbacks = [] };
        var receipt = new KnockbackCompletionEvidence(1, 7, 10, 222, 100, 101, 1,
            new(pushStage.From.X, pushStage.From.Z), new(pushStage.To.X, pushStage.To.Z),
            now.AddSeconds(2.1), now.AddSeconds(2.9), now.AddSeconds(3), 1);
        MovementPlanningContext Resolved(KnockbackCompletionEvidence? evidence, NativeDisplacementInventory? latest = null)
            => new(resolvedIdentity, resolvedMap.CopyStrictMap(), 0, 0,
                Enumerable.Repeat(true, context.Cells).ToArray(), new float[context.Cells], [], [], native: resolvedMap,
                knockbackPhases: new(latest ?? resolvedInventory, [], "native source resolved"), knockbackCompletion: evidence);
        Check(boundPhase.Completed(Resolved(receipt), pushStage),
            "A fresh native cast-generation/effect receipt and exact observed landing consume the corresponding forced stage.");
        Check(!boundPhase.Completed(Resolved(null), pushStage)
            && !boundPhase.Completed(Resolved(receipt), pushStage with { To = pushStage.To + new WDir(.2f, 0) }),
            "Source disappearance or a nearby mismatched position is not completion evidence.");
        var shiftedReceipt = receipt with { Landing = receipt.Landing + new System.Numerics.Vector2(.09f, 0) };
        Check(!boundPhase.Completed(resolvedIdentity with { Origin = pushStage.To + new WDir(.18f, 0) },
            pushStage.To + new WDir(.18f, 0), resolvedInventory, shiftedReceipt, pushStage.From, pushStage.To),
            "Actual-to-receipt and receipt-to-plan tolerances cannot add together to approve a displaced landing.");
        foreach (var invalid in new[] { receipt with { CastGeneration = 2 }, receipt with { EffectCounter = 0 },
            receipt with { CastActionId = 999 }, receipt with { SourceActor = 11 }, receipt with { SourceDataId = 223 },
            receipt with { StartedUtc = now.AddSeconds(2.3) }, receipt with { EndedUtc = now.AddSeconds(3.01) },
            receipt with { ObservedUtc = now.AddSeconds(2.7), EndedUtc = now.AddSeconds(2.6) } })
            Check(!boundPhase.Completed(Resolved(invalid), pushStage),
                "A wrong cast, effect identity, source, motion window or stale receipt cannot release forced-motion ownership.");
        Check(!boundPhase.Completed(Resolved(receipt, resolvedInventory with { Pending = true }), pushStage)
            && !boundPhase.Completed(Resolved(receipt, resolvedInventory with { Knockbacks = [boundSource] }), pushStage),
            "Pending effects or a still-published native push remain authoritative despite an apparent observed landing.");
        Check(!MovementPlanValidation.Validate(boundPlan, Resolved(receipt), 0, true,
            identity.CapturedTimestamp + (long)(3.1 * Stopwatch.Frequency)).Allowed,
            "Physical completion cannot refresh an expired search result; continuation requires fresh native input.");
        var transaction = new NativeKnockbackTransaction();
        var moduleToken = new object();
        NavigationIdentity At(float seconds, WPos position) => identity with { WorldTime = now.AddSeconds(seconds), Origin = position,
            Generation = 100 + (long)(seconds * 100), CapturedTimestamp = identity.CapturedTimestamp + (long)(seconds * Stopwatch.Frequency) };
        NativeDisplacementInventory InventoryAt(float seconds, bool published) => inventory with
        { At = now.AddSeconds(seconds), Knockbacks = published ? [boundSource] : [] };
        transaction.ObservePreparation("normal", moduleToken, At(0, pushStage.From), boundPhase, pushStage.From);
        Check(transaction.Capture("normal", moduleToken, At(1, pushStage.From), InventoryAt(1, true), null, true) is { Preparing: true },
            "A transaction tracks native preparation before its original activation.");
        Check(transaction.Capture("normal", moduleToken, At(1.9f, pushStage.From), InventoryAt(1.9f, false), null, true) is { Preparing: false }
            && Math.Abs(transaction.CastWindow(now.AddSeconds(1.9)) - .1f) < .001f,
            "Source removal before the effect retains the original event/cast window instead of reopening ordinary planning.");
        var mid = pushStage.From + .5f * (pushStage.To - pushStage.From);
        var held = transaction.Capture("normal", moduleToken, At(2.5f, mid), InventoryAt(2.5f, false), null, true);
        Check(held?.Phase.Timing?.LandingLatestUtc == now.AddSeconds(3) && held.Landing == pushStage.To,
            "Fresh active-motion captures do not extend the original landing deadline or apply displacement again from an intermediate pose.");
        var activeIdentity = At(2.5f, mid);
        var activeMap = NativeNavigationSnapshot.Capture(afterInput with { Identity = activeIdentity });
        var activeRoute = new PlanningForcedMovement("native-knockback:10:1", default, false,
            [new(mid, pushStage.To, 0, .5f)], true) { ConservativeCorridor = true, Knockback = boundSource };
        var activeContext = new MovementPlanningContext(activeIdentity, activeMap.CopyStrictMap(), 0, 0,
            Enumerable.Repeat(true, context.Cells).ToArray(), new float[context.Cells], [], [], native: activeMap,
            forcedMovement: new([activeRoute], false),
            knockbackPhases: new(InventoryAt(2.5f, false), [], "active native event"));
        var activePlan = JointMovementSearch.Search(activeContext, Exhaustive);
        Check(activePlan.HasVerifiedRoute && MovementPlanValidation.Validate(activePlan, activeContext, 0, true,
                activeIdentity.CapturedTimestamp).Allowed
            && executor.Advance(activePlan, activeContext, default, true, activeIdentity.CapturedTimestamp).Kind == MovementExecutionKind.Wait,
            "An in-flight refresh uses a fresh native forced prefix and the same search/executor, with no voluntary movement before completion.");
        var completion = receipt with { EndedUtc = now.AddSeconds(2.8), ObservedUtc = now.AddSeconds(2.95) };
        Check(transaction.Capture("normal", moduleToken, At(2.95f, pushStage.To), InventoryAt(2.95f, false), completion, true) == null
            && !transaction.Active, "Only correlated physical completion releases the transaction for a fresh onward plan.");
        transaction.ObservePreparation("normal", moduleToken, At(0, pushStage.From), boundPhase, pushStage.From);
        Check(transaction.Capture("normal", moduleToken, At(3.1f, pushStage.From), InventoryAt(3.1f, false), null, true)?.Failure != null
            && !transaction.Active, "Window expiry is a failed forecast, never a synthetic completed push.");
        transaction.ObservePreparation("normal", moduleToken, At(0, pushStage.From), boundPhase, pushStage.From);
        Check(transaction.Capture("follow", moduleToken, At(1, pushStage.From), InventoryAt(1, true), null, true) == null
            && !transaction.Active, "An ownership change cannot inherit another movement owner's armed event.");
        Check(!JointMovementSearch.Search(Capture(phase with { Timing = null }), Exhaustive).HasVerifiedRoute,
            "Native geometry without evidenced motion timing cannot become an ordinary or instantaneous action forecast.");
        Check(!JointMovementSearch.Search(Capture(), Exhaustive).HasVerifiedRoute,
            "An interrupting native inventory without supported phases cannot silently fall through to ordinary search.");
        var altered = phase with { Source = raw with { Distance = 20 } };
        Check(!JointMovementSearch.Search(Capture(altered), Exhaustive).HasVerifiedRoute,
            "A phase cannot invent a displacement absent from the complete native source inventory.");
        var frame = context.WithDisplacementMap(after);
        var delayed = phase with { Timing = new(now.AddSeconds(2.3), now.AddSeconds(2.35), now.AddSeconds(2.4), now.AddSeconds(3)) };
        Check(!delayed.TryTransition(context, frame, identity.Origin, 2.1f, out _, out _, out _, out _),
            "Observed onset estimates cannot postpone the native authored preparation deadline.");
        var instantaneous = phase with { Timing = new(now.AddSeconds(2), now.AddSeconds(2), now.AddSeconds(2), now.AddSeconds(2)) };
        Check(!instantaneous.TryTransition(context, frame, identity.Origin, 0, out _, out _, out _, out _),
            "A nonzero native push cannot obtain an invented zero-duration flight.");
        Check(!phase.TryTransition(context, frame, identity.Origin, 2.01f, out _, out _, out _, out _),
            "A preparation reached after the earliest possible push is rejected.");
        Check(!phase.TryTransition(context, frame, origin, 0, out _, out _, out _, out _),
            "An undefined radial direction at the source cannot manufacture a landing.");
        var warning = new SDCircle(new(-.5f, .5f), .6f);
        var warned = NativeNavigationSnapshot.Capture(afterInput with { Warnings = [(warning, now.AddSeconds(2.9), 0)] });
        var warnedPhase = phase with { After = warned };
        Check(!warnedPhase.TryTransition(context, context.WithDisplacementMap(warned), identity.Origin, 0,
            out _, out _, out _, out _),
            "An uncertain onset checks the entire forced corridor through latest landing, not just a fictional average-speed crossing.");
        var changedIdentity = afterInput with { Identity = identity with { Generation = 2 } };
        var rejected = false;
        try { _ = context.WithDisplacementMap(NativeNavigationSnapshot.Capture(changedIdentity)); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "A phase from another capture cannot refresh or replace the plan's original clock.");

        var escapeWarning = NativeNavigationSnapshot.Capture(afterInput with
        { Warnings = [(new SDCircle(default, 100), now.AddSeconds(4.5), 0)] });
        var temporary = phase with { After = escapeWarning };
        Check(temporary.TryTransition(context, context.WithDisplacementMap(escapeWarning), identity.Origin, 0,
                out var temporaryLanding, out _, out _, out _)
            && !context.WithDisplacementMap(escapeWarning).CanRemain(temporaryLanding)
            && !JointMovementSearch.Search(Capture(temporary), Exhaustive).HasVerifiedRoute,
            "A reachable temporary warning-area landing without any onward safe destination is never a complete plan.");

        var raster = before.CopyStrictMap();
        var launchForDash = new WPos(.5f, .5f);
        var dash = new PlanningMobility(context.Cell(identity.Origin), context.Cell(launchForDash),
            MovementStageKind.GroundMovement, 1, 0, null, 0, .1f, 0, 0,
            SourcePosition: identity.Origin, LandingPosition: launchForDash);
        var resourceContext = new MovementPlanningContext(identity, raster, 0, 0,
            Enumerable.Repeat(true, context.Cells).ToArray(), new float[context.Cells], [], [dash], resources: 1,
            native: before, knockbackPhases: context.KnockbackPhases);
        var resourcePlan = JointMovementSearch.Search(resourceContext, Exhaustive);
        var unpruned = JointMovementSearch.Search(resourceContext, Exhaustive, pruneResourceInferiorMobility: false);
        Check(resourcePlan.HasVerifiedRoute && resourcePlan.RemainingResources == 1
            && resourcePlan.NativePriority == unpruned.NativePriority && unpruned.RemainingResources == 1,
            "Walking preparation preserves a useful charge when mobility cannot improve execution through the same forced phase.");

        var second = raw with { Index = 1, Origin = new(5, 0), Activation = now.AddSeconds(5) };
        var secondPure = new SDKnockbackInCircleAwayFromOrigin(default, second.Origin, 2, 3.5f);
        var firstAfter = NativeNavigationSnapshot.Capture(afterInput with { Warnings = [(secondPure, second.Activation, 10)] });
        var twoBefore = NativeNavigationSnapshot.Capture(beforeInput with
        { Warnings = [(pure, raw.Activation, 10), (secondPure, second.Activation, 10)] });
        var phases = new NativeKnockbackPhases(inventory with { Knockbacks = [raw, second] },
            [phase with { After = firstAfter }, new(second, after, [NativeLandingConstraints.ProjectionEnvelope(secondPure)!.Value], 1, 0)
            { Timing = new(now.AddSeconds(5), now.AddSeconds(5.1), now.AddSeconds(5.2), now.AddSeconds(6)) }], "two native phases");
        var twoContext = new MovementPlanningContext(identity, twoBefore.CopyStrictMap(), 0, 0,
            Enumerable.Repeat(true, context.Cells).ToArray(), new float[context.Cells], [], [], native: twoBefore, knockbackPhases: phases);
        var twoPlan = JointMovementSearch.Search(twoContext, Exhaustive);
        Check(twoPlan.HasVerifiedRoute && twoPlan.Stages.Count(stage => stage.Kind == MovementStageKind.Knockback) == 2
            && twoPlan.NativePriority == 100,
            "Successive native pushes require both transitions and a useful final recovery rather than accepting the first landing.");

        var currentAction = new PlanningAction(42, 2, .2f, 1, 0, .1f, 0,
            new ulong[context.Cells], Enumerable.Repeat(1f, context.Cells).ToArray());
        var subsequentAction = new PlanningAction(43, 2, 3.2f, 4, .1f, .1f, 0,
            new ulong[context.Cells], Enumerable.Repeat(1f, context.Cells).ToArray(), RequiredEarlierActions: 1);
        var actionContext = new MovementPlanningContext(identity, raster, 0, 0,
            Enumerable.Repeat(true, context.Cells).ToArray(), new float[context.Cells], [currentAction, subsequentAction], [],
            native: before, knockbackPhases: context.KnockbackPhases);
        var actionPlan = JointMovementSearch.Search(actionContext, Exhaustive);
        Check(actionPlan.HasVerifiedRoute && actionPlan.DeliveredActions == 3 && actionPlan.NativePriority == 100
            && Math.Abs(actionPlan.DeliveryTimes[0] - .2f) < .001f
            && Math.Abs(actionPlan.DeliveryTimes[1] - 3.3f) < .001f,
            "The same search preserves the evidenced current action before the push and evaluates the subsequent cast after latest landing.");
        Check(actionPlan.Stages.First(stage => stage.Kind == MovementStageKind.Cast).ActionId == 42
            && actionPlan.Stages.Last(stage => stage.Kind == MovementStageKind.Cast).StartSeconds >= 3,
            "A future action cannot receive pre-push execution credit by crossing the forced interval.");

        // Independent graph enumeration uses the shared native raster/traversal,
        // but never the search's labels, dominance, transition or objective code.
        // Before/after walks have separate clocks; the forced interval is fixed.
        var reference = ReferenceKnockback(context, frame, phase, origin, 2, 2, 3);
        var walking = plan.Stages.Where(s => s.Kind == MovementStageKind.Walk).Sum(s => (s.To - s.From).Length());
        Check(reference.Priority == plan.NativePriority && Math.Abs(reference.Walking - walking) < .001f,
            $"Joint phase search matches independent complete graph enumeration: expected {reference}, got {plan.NativePriority}/{walking}.");
    }

    private static (float Priority, float Walking) ReferenceKnockback(MovementPlanningContext before,
        MovementPlanningContext after, NativeKnockbackPhase phase, WPos origin, float distance, float onset, float end, Func<WPos, WPos?>? projection = null)
    {
        var best = (Priority: float.MinValue, Walking: float.PositiveInfinity);
        var launches = Walk(before, before.Origin, 0);
        foreach (var (launch, walked) in launches)
        {
            var arrived = walked / before.Identity.Speed;
            var offset = launch - origin;
            if (arrived > onset || projection == null && offset.LengthSq() < 1e-8f
                || !before.Traverse(launch, launch, arrived, onset - arrived, out _)) continue;
            var projected = projection == null ? launch + distance * offset.Normalized() : projection(launch);
            if (projected == null) continue;
            var landing = projected.Value;
            if (after.Cell(landing) < 0 || !phase.LandingAllowed(landing, before.Radius)
                || !after.Traverse(launch, landing, end, 0, out _)) continue;
            foreach (var (destination, recovery) in Walk(after, landing, end))
            {
                if (!after.CanRemain(destination)) continue;
                var priority = after.NativePriority(after.Cell(destination));
                var total = walked + recovery;
                if (priority > best.Priority || priority == best.Priority && total < best.Walking)
                    best = (priority, total);
            }
        }
        return best;

        static List<(WPos Position, float Distance)> Walk(MovementPlanningContext frame, WPos start, float begins)
        {
            var result = new Dictionary<WPos, float> { [start] = 0 };
            var queue = new PriorityQueue<WPos, float>();
            queue.Enqueue(start, 0);
            while (queue.TryDequeue(out var position, out var travelled))
            {
                if (travelled != result[position]) continue;
                var cell = frame.Cell(position);
                var x = cell % frame.Width;
                var y = cell / frame.Width;
                for (var dy = -1; dy <= 1; ++dy)
                for (var dx = -1; dx <= 1; ++dx)
                {
                    if (x + dx < 0 || x + dx >= frame.Width || y + dy < 0 || y + dy >= frame.Height) continue;
                    var next = frame.Point((y + dy) * frame.Width + x + dx);
                    var step = (next - position).Length();
                    var total = travelled + step;
                    if (step < 1e-6f || begins + total / frame.Identity.Speed > MovementPlanningContext.MaximumHorizonSeconds
                        || result.TryGetValue(next, out var old) && old <= total
                        || !frame.TraverseOrdinary(position, next, begins + travelled / frame.Identity.Speed,
                            step / frame.Identity.Speed, out _)) continue;
                    result[next] = total;
                    queue.Enqueue(next, total);
                }
            }
            return result.Select(pair => (pair.Key, pair.Value)).ToList();
        }
    }
}
