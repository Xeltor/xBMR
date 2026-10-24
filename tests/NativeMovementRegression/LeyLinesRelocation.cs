using BossMod;
using BossMod.CombatAssistance.Config;
using BossMod.CombatAssistance.Integrations;
using BossMod.CombatAssistance.Planning;
using System.Collections.Immutable;

public static partial class Checks
{
    private static void LeyLinesRelocation()
    {
        var map = Grid(7, 1);
        var origin = Context(map, 0);
        var field = new PlanningPersonalField(origin.Identity.ActorId, 10, Point(map, 6), 0.4f, 10);
        var timing = new PlanningCastTiming(3, 3, float.MaxValue, field.EndsIn, field.Center, field.Radius,
            ObservedFieldCast: 1, FieldEntryByAbility: true, ObservedOutsideCast: 3, NativeRecast: 3,
            ObservedFieldRecast: 2, ObservedOutsideRecast: 3);
        var action = Action(map, 12, 0, new ulong[7], Enumerable.Repeat(1f, 7).ToArray(), 3, 1) with { Timing = timing };
        var relocate = new PlanningUtility(36988, PlanningUtilityEffect.RelocatePersonalField, 20, 0.6f, 0, 0, Field: field);
        var zone = new PlanningZone(3573, field.Owner, "Ley Lines", field.Center, 0, field.Radius, field.EndsIn, PlanningZoneBenefit.PersonalCasting);
        MovementPlanningContext Capture(PlanningUtility? utility = null, PlanningPersonalField? currentField = null,
            PlanningAction? currentAction = null, ulong resources = 1, PlanningExecutionRules? rules = null,
            MobilityMechanics[]? mobility = null)
            => new(origin.Identity, map, 0, 0, Enumerable.Repeat(true, 7).ToArray(), new float[7], [currentAction ?? action], [], resources,
                utilities: utility == null ? [] : [utility], personalField: currentField ?? field,
                executionRules: rules, mobilityMechanics: mobility, zones: [zone]);
        var context = Capture(relocate);
        var plan = JointMovementSearch.Search(context, Exhaustive);
        Check(plan.DeliveryTimes[0] == 2 && plan.UsefulDestination == origin.Origin && plan.RemainingResources == 0
            && plan.Stages.Any(s => s.Kind == MovementStageKind.UtilityAction && s.ActionId == 36988),
            "Retrace relocates the observed effect before the selected hardcast without a separate correction walk.");
        Check(plan.UsefulZones is [{ Center: var center }] && center == origin.Origin
            && MovementPlanValidation.Validate(plan, context, 0, true, origin.Identity.CapturedTimestamp).Allowed,
            "The field's useful destination and complete route validator agree on the relocated center.");
        var walkingPass = JointMovementSearch.Search(context, Exhaustive, includeMobilityActions: false, stopAtCurrentOpportunity: true);
        Check(walkingPass.DeliveryTimes[0] == 2 && walkingPass.RemainingResources == 0,
            "Retrace participates before preliminary publication rather than correcting a walking endpoint afterward.");
        var bounded = JointMovementSearch.Search(context, Exhaustive with { Expansions = 0 }, nativeIncumbent: plan);
        Check(bounded.DeliveryTimes[0] == 2 && bounded.UsefulZones.Single().Center == origin.Origin,
            "A verified seed keeps its original relocation state and resource use when continuation is bounded.");
        var first = Array.FindIndex(plan.Stages.ToArray(), s => s.Kind == MovementStageKind.UtilityAction);
        Check(first >= 0, "The effect relocation is represented by a real executable action stage.");
        var executor = new MovementPlanExecutor();
        var execute = executor.Advance(plan, context, new(default), true, origin.Identity.CapturedTimestamp);
        Check(execute.Kind == MovementExecutionKind.Action && execute.ActionId == 36988 && execute.Waypoint == origin.Origin,
            "The existing single executor submits Retrace at the exact safe casting position.");
        Check(!MovementPlanValidation.Validate(plan, Capture(resources: 0), first + 1, true, origin.Identity.CapturedTimestamp).Allowed,
            "An action-effect packet alone cannot prove that the native field really moved.");
        var moved = field with { Center = origin.Origin };
        var observedAction = action with { Timing = timing with { HasteCenter = origin.Origin, FieldEntryByAbility = false } };
        var after = Capture(currentField: moved, currentAction: observedAction, resources: 0);
        Check(MovementPlanValidation.Validate(plan, after, first + 1, true, origin.Identity.CapturedTimestamp).Allowed,
            "After the native object corroborates relocation, remaining execution uses the moved field without needing another charge.");
        Check(MovementPlanValidation.Validate(plan, Capture(currentField: moved with { Object = 11, Effect = field.Object },
            currentAction: observedAction, resources: 0), first + 1, true, origin.Identity.CapturedTimestamp).Allowed,
            "A live Retrace replacement object retains the consumed prefix only with the native observer's original-effect proof.");
        var readyNow = observedAction with { Earliest = 0 };
        var nextCast = Array.FindIndex(plan.Stages.ToArray(), s => s.Kind == MovementStageKind.Cast);
        Check(!MovementPlanValidation.Validate(plan, Capture(currentField: moved, currentAction: readyNow, resources: 0),
            nextCast, true, origin.Identity.CapturedTimestamp).Allowed,
            "When the spell is immediately ready, an object move alone cannot replace native cast-speed readback.");
        Check(MovementPlanValidation.Validate(plan, Capture(currentField: moved,
            currentAction: readyNow with { CastSeconds = 1, Timing = readyNow.Timing! with { NativeRecast = 2 } }, resources: 0),
            nextCast, true, origin.Identity.CapturedTimestamp).Allowed,
            "An immediately ready spell retains the route once its native timing actually corroborates the faster cast.");
        var overlay = BossMod.CombatAssistance.UI.NativePlanOverlay.Capture(plan, execute, first, [], 0);
        Check(overlay.Markers.Any(m => m.Label == "Retrace"),
            "The existing utility marker identifies the pending field relocation without introducing another overlay primitive.");
        Check(!MovementPlanValidation.Validate(plan, Capture(currentField: moved with { Object = 11 }, currentAction: observedAction),
            first + 1, true, origin.Identity.CapturedTimestamp).Allowed,
            "Another effect identity cannot inherit an already consumed relocation proof.");
        Check(!MovementPlanValidation.Validate(plan, Capture(relocate, currentAction: action with { Timing = timing with { ObservedFieldCast = null, FieldEntryByAbility = false } }),
            0, true, origin.Identity.CapturedTimestamp).Allowed,
            "Lost current-spell speed evidence invalidates an old relocation promise.");
        var noEvidence = JointMovementSearch.Search(Capture(relocate, currentAction: action with { Timing = timing with { ObservedFieldCast = null, FieldEntryByAbility = false } }), Exhaustive);
        Check(noEvidence.RemainingResources == 1 && noEvidence.Stages.All(s => s.ActionId != 36988),
            "A known field with no matching cast-speed observation cannot invent a reason to consume Retrace.");
        var instant = JointMovementSearch.Search(Capture(relocate, currentAction: action with { CastSeconds = 0 }), Exhaustive);
        Check(instant.DeliveryTimes[0] == 1 && instant.RecoveryTimes[0] == 3 && instant.RemainingResources == 0,
            "Retrace can improve an instant spell's evidenced recast without delaying its current delivery.");
        var noWeave = JointMovementSearch.Search(Capture(relocate, currentAction: action with { CastSeconds = 0, Earliest = 0 }), Exhaustive);
        Check(noWeave.DeliveryTimes[0] == 0 && noWeave.RemainingResources == 1,
            "A faster recovery cannot purchase delaying an immediately available current instant spell.");
        Check(!MovementPlanValidation.Validate(instant,
            Capture(relocate, currentAction: action with { CastSeconds = 0, Timing = timing with { ObservedFieldRecast = 3 } }),
            0, true, origin.Identity.CapturedTimestamp).Allowed,
            "Losing the evidenced recast gain invalidates an instant-spell relocation even though its cast remains zero.");
        Check(!MovementPlanValidation.Validate(instant,
            Capture(relocate, currentAction: action with { CastSeconds = 0, Earliest = 0.2f }),
            0, true, origin.Identity.CapturedTimestamp).Allowed,
            "A shortened current weave invalidates old field setup that would now delay an available spell.");
        var expired = field with { EndsIn = 0.5f };
        var expiredPlan = JointMovementSearch.Search(Capture(relocate with { Field = expired }, expired,
            action with { Timing = timing with { HasteUntil = expired.EndsIn } }), Exhaustive);
        Check(expiredPlan.Stages.All(s => s.ActionId != 36988) && expiredPlan.DeliveryTimes[0] == 4,
            "Relocation cannot renew the field or finish after its observed expiry.");
        var prohibited = JointMovementSearch.Search(Capture(relocate, rules: new(float.MaxValue, 0, [])), Exhaustive);
        Check(prohibited.Stages.All(s => s.ActionId != 36988), "Retrace cannot bypass native action prohibitions.");
        Check(!MovementPlanValidation.Validate(plan, Capture(), 0, true, origin.Identity.CapturedTimestamp).Allowed,
            "A disabled or unavailable relocation action invalidates only its old route.");
        var rejected = context.Excluding([new(36988, 0, origin.Origin, origin.Origin, null, "failed setup")]);
        var fallback = JointMovementSearch.Search(rejected, Exhaustive);
        Check(fallback.DeliveryTimes[0] < 4 && fallback.Stages.Where(s => s.ActionId == 36988).All(s => s.From != origin.Origin),
            "A failed setup rejects that candidate without suppressing a legal relocation from another position.");
        var everySetupRejected = context.Excluding(Enumerable.Range(0, 7)
            .Select(i => new RejectedMobility(36988, 0, Point(map, i), Point(map, i), null, "failed setup")).ToImmutableArray());
        var ordinary = JointMovementSearch.Search(everySetupRejected, Exhaustive);
        Check(ordinary.DeliveryTimes[0] == 4 && ordinary.RemainingResources == 1,
            "When every relocation is unavailable, the ordinary selected hardcast and its resources remain available.");

        var returnAction = new MobilityMechanics(7419, MobilityMechanic.Return, 25, 0, 0, 0, 0.2f, 1, returnPosition: field.Center);
        var relocated = field with { Center = Point(map, 2) };
        var displaced = Point(map, 4);
        var returnContext = Capture(relocate, mobility: [returnAction], resources: 0x11);
        var edges = returnContext.MobilityFrom(displaced, relocatedField: relocated).ToArray();
        Check(edges is [{ LandingPosition: var landing }] && landing == relocated.Center,
            "After a prospective Retrace, Between the Lines resolves to the new center rather than the old native anchor.");
        var returnStage = new MovementStage(MovementStageKind.Return, displaced, relocated.Center, 0, 0.2f, 7419);
        Check(returnContext.ResolveMobility(displaced, returnStage, relocated) != null
            && returnContext.ResolveMobility(displaced, returnStage) == null,
            "Exact route revalidation uses the same state-dependent return mechanics as search.");
        var settings = new AssistancePreferences { UseRetrace = true, ManageLeylines = true, UseGapCloser = false, ReturnToLeylines = false };
        Check(NativeMobilityCapture.RetraceEnabled(settings, Class.BLM),
            "Retrace has its original independent saved control and does not require walking return or gap-closer permission.");
        settings.UseRetrace = false;
        Check(!NativeMobilityCapture.RetraceEnabled(settings, Class.BLM), "The saved Retrace opt-out remains binding.");
        settings.UseRetrace = true;
        settings.ManageLeylines = false;
        Check(!NativeMobilityCapture.RetraceEnabled(settings, Class.BLM)
            && !NativeMobilityCapture.RetraceEnabled(new(), Class.RDM), "Job and Ley Lines master gates retain authority.");
        Check(RsrActionEvidenceSource.FieldActionMayWeave(100, true, 100, 200)
            && RsrActionEvidenceSource.FieldActionMayWeave(200, false, 100, 200),
            "An elective field action can coexist with the actually modeled current GCD or immediate party action.");
        Check(RsrActionEvidenceSource.FieldActionMayWeave(PlanningFieldPlacement.ActionId, false, 100, 0),
            "RSR selecting Ley Lines itself is direct placement evidence, not an unsupported competing ability that disables its own planning.");
        Check(!RsrActionEvidenceSource.FieldActionMayWeave(300, false, 100, 200)
            && !RsrActionEvidenceSource.FieldActionMayWeave(200, true, 100, 200)
            && !RsrActionEvidenceSource.FieldActionMayWeave(100, false, 100, 200)
            && !RsrActionEvidenceSource.FieldActionMayWeave(0, false, 0, 0),
            "An unsupported selected ability, stale slot or unknown selection keeps its weave instead of funding speculative field gain.");
        ReferenceLeyLinesRelocation();
    }

    private static void ReferenceLeyLinesRelocation()
    {
        // Independent finite-state oracle on a static line. Enumerate every
        // obstacle mask, both return modes, expiry and current-action windows.
        // Earlier time can dominate here only with the same center, entry proof
        // and unspent actions: static safe cells can wait without another cost.
        foreach (var walking in new[] { false, true })
            foreach (var expires in new[] { 1f, 3f, 8f })
                foreach (var earliest in new[] { 0f, 1f, 3f })
                    for (var mask = 0; mask < 8; ++mask)
                    {
                        var map = Grid(4, 1);
                        for (var i = 1; i < 4; ++i) if ((mask & (1 << (i - 1))) != 0) map.PixelMaxG[i] = -1;
                        var origin = Context(map, 0);
                        var field = new PlanningPersonalField(origin.Identity.ActorId, 10, Point(map, 3), 0.4f, expires);
                        var timing = new PlanningCastTiming(3, 3, float.MaxValue, expires, field.Center, field.Radius,
                            ObservedFieldCast: 1, FieldEntryByAbility: !walking, ObservedOutsideCast: 3,
                            NativeRecast: 3, ObservedFieldRecast: 2, ObservedOutsideRecast: 3);
                        var action = Action(map, 6.5f, 0, new ulong[4], [1, 1, 1, 1], 3, earliest) with { Timing = timing };
                        var context = new MovementPlanningContext(origin.Identity, map, 0, 0, [true, true, true, true], new float[4], [action], [], 0x11,
                            utilities: [new(36988, PlanningUtilityEffect.RelocatePersonalField, 20, 0.6f, 0, 0, Field: field)],
                            mobilityMechanics: [new(7419, MobilityMechanic.Return, 25, 0, 0, 0, 0.2f, 1, returnPosition: field.Center)],
                            personalField: field);
                        var best = float.PositiveInfinity;
                        var bestRecovery = float.PositiveInfinity;
                        var seen = new Dictionary<(int Cell, int Center, bool Entry, bool Retrace, bool Return), float>();
                        void Visit(int cell, int center, bool entry, bool retrace, bool availableReturn, float time)
                        {
                            if (time > 6.5f || time >= best) return;
                            var key = (cell, center, entry, retrace, availableReturn);
                            if (seen.TryGetValue(key, out var prior) && prior <= time) return;
                            seen[key] = time;
                            var start = Math.Max(time, earliest);
                            var covered = start < expires && cell == center && (walking || entry);
                            var cast = covered ? 1 : 3;
                            var delivery = start + cast;
                            var recovery = Math.Max(delivery, start + (covered ? 2 : 3));
                            if (delivery <= 6.5f && delivery <= earliest + 3 && recovery <= earliest + 3)
                            {
                                if (delivery < best) { best = delivery; bestRecovery = recovery; }
                                else if (delivery == best) bestRecovery = Math.Min(bestRecovery, recovery);
                            }
                            for (var step = -1; step <= 1; step += 2)
                            {
                                var next = cell + step;
                                if (next >= 0 && next < 4 && map.PixelMaxG[next] >= 0)
                                    Visit(next, center, entry && next == center && time + 1 < expires, retrace, availableReturn, time + 1);
                            }
                            if (retrace && time + 0.6f < expires)
                                Visit(cell, cell, true, false, availableReturn, time + 0.6f);
                            if (availableReturn && cell != center && time + 0.2f < expires
                                && Enumerable.Range(Math.Min(cell, center), Math.Abs(cell - center) + 1).All(i => map.PixelMaxG[i] >= 0))
                                Visit(center, center, true, retrace, false, time + 0.2f);
                        }
                        Visit(0, 3, false, true, true, 0);
                        var actual = JointMovementSearch.Search(context, Exhaustive);
                        Check(actual.DeliveredActions == 1 && Math.Abs(actual.DeliveryTimes[0] - best) < 0.0001f
                            && Math.Abs(actual.RecoveryTimes[0] - bestRecovery) < 0.0001f,
                            $"Relocation exhaustive oracle: walk={walking}, expiry={expires}, ready={earliest}, obstacles={mask}.");
                        Check(MovementPlanValidation.Validate(actual, context, 0, true, origin.Identity.CapturedTimestamp).Allowed,
                            "The independently optimal small-map field sequence is also exactly executable.");
                    }
    }
}
