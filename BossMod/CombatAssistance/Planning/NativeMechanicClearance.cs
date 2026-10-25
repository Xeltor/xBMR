using BossMod.Pathfinding;
using System.Diagnostics;
using System.Threading;

namespace BossMod.CombatAssistance.Planning;

// A frozen destination preference, never a hazard or route-safety certificate.
internal sealed record PlanningMechanicClearance(float Distance, ImmutableArray<float> Values)
{
    // Soft arrival preference only: moving allies do not cancel a safe worker.
    internal PlanningRange? PartyRegion { get; init; }
    internal bool Same(PlanningMechanicClearance other)
        => Distance == other.Distance && Values.AsSpan().SequenceEqual(other.Values.AsSpan());

    internal static PlanningMechanicClearance? Capture(Map map, AIHints hints, float requested)
    {
        if (!float.IsFinite(requested) || requested <= 0 || hints.ForbiddenZones.Count == 0) return null;
        var distance = Math.Min(requested, 3);
        var shapes = hints.ForbiddenZones.Select(z => z.shapeDistance).ToArray();
        if (shapes.Any(s => !float.IsFinite(s.ConservativeLipschitz) || s.ConservativeLipschitz <= 0)) return null;
        var values = ImmutableArray.CreateBuilder<float>(map.Width * map.Height);
        for (var cell = 0; cell < values.Capacity; ++cell)
        {
            var point = map.GridToWorld(cell % map.Width, cell / map.Width, .5f, .5f);
            var clearance = distance;
            foreach (var shape in shapes)
            {
                var value = shape.Distance(point) / shape.ConservativeLipschitz;
                if (!float.IsFinite(value)) return null;
                clearance = Math.Min(clearance, Math.Max(0, value));
            }
            values.Add(clearance);
        }
        return new(distance, values.MoveToImmutable());
    }
}

// XCAI-style bounded arrival refinement in the one movement worker. Preserve
// the proved escape, then continue into nearby usable space before stopping.
internal static class NativeMechanicClearance
{
    internal static MovementPlan Refine(MovementPlanningContext context, MovementPlan plan,
        CancellationToken cancel = default, bool respectFreshness = true)
    {
        if (context.MechanicClearance is not { } preference || !plan.HasVerifiedRoute || !context.Optimize
            || context.StationaryOnly || context.RequiredDestination != null || context.ForcedMovement != null
            || context.KnockbackPhases?.Phases.IsEmpty == false || !context.Teleports.IsEmpty
            || context.CanRemain(context.Origin) || !context.PreservesPresent(plan.DeliveredActions)) return plan;
        var endpoint = context.Cell(plan.UsefulDestination);
        if (endpoint < 0 || preference.Values[endpoint] >= preference.Distance && preference.PartyRegion == null) return plan;
        var lastMove = -1;
        for (var i = 0; i < plan.Stages.Length; ++i)
            if (plan.Stages[i].From != plan.Stages[i].To) lastMove = i;
        if (lastMove < 0 || plan.Stages[lastMove].To != plan.UsefulDestination
            || plan.Stages.Skip(lastMove + 1).Any(s => s.From != plan.UsefulDestination || s.To != plan.UsefulDestination
                || s.Kind is not (MovementStageKind.Wait or MovementStageKind.Cast))) return plan;

        var map = context.CopyMap();
        var extent = (int)Math.Ceiling(preference.Distance / map.Resolution);
        var center = map.WorldToGrid(plan.UsefulDestination);
        var best = plan;
        var bestClearance = preference.Values[endpoint];
        var bestTravel = float.MaxValue;
        var bestPartyLoss = preference.PartyRegion is { } party ? (plan.UsefulDestination - party.Center).Length() : 0;
        for (var y = Math.Max(0, center.y - extent); y <= Math.Min(map.Height - 1, center.y + extent); ++y)
            for (var x = Math.Max(0, center.x - extent); x <= Math.Min(map.Width - 1, center.x + extent); ++x)
            {
                cancel.ThrowIfCancellationRequested();
                if (respectFreshness && Stopwatch.GetElapsedTime(context.Identity.CapturedTimestamp).TotalSeconds >= .25) return best;
                var cell = map.GridToIndex(x, y);
                var point = context.Point(cell);
                var travel = (point - plan.UsefulDestination).Length();
                var clearance = preference.Values[cell];
                var partyLoss = preference.PartyRegion is { } region ? (point - region.Center).Length() : 0;
                var betterClearance = clearance > preference.Values[endpoint] + .1f;
                var betterParty = preference.PartyRegion != null && clearance >= preference.Values[endpoint]
                    && partyLoss < (plan.UsefulDestination - preference.PartyRegion.Center).Length() - .1f;
                if (travel > preference.Distance || travel <= MovementPlanValidation.ArrivalTolerance
                    || !betterClearance && !betterParty
                    || clearance < bestClearance || clearance == bestClearance && (partyLoss > bestPartyLoss
                        || partyLoss == bestPartyLoss && travel >= bestTravel)
                    || !context.DestinationAllowed(cell) || !context.CanRemain(point)
                    || context.NativePriority(cell) < plan.NativePriority
                    || context.PreferredEnemyEndpoint(plan.UsefulDestination) && !context.PreferredEnemyEndpoint(point)
                    || context.PreferredArenaEndpoint(plan.UsefulDestination) && !context.PreferredArenaEndpoint(point)
                    || context.Range is { } range && range.Loss(point) > range.Loss(plan.UsefulDestination)
                    || context.PartyContact is { } contact && contact.Loss(point) > contact.Loss(plan.UsefulDestination)
                    || context.HealingCoverage?.Compare(point, plan.UsefulDestination) < 0
                    || context.Presentation(cell) < context.Presentation(endpoint)) continue;

                var start = plan.Stages[lastMove].EndSeconds;
                var duration = (plan.EndTravel ?? context.Travel).Seconds(travel, start);
                var time = start + duration;
                if (time > MovementPlanningContext.MaximumHorizonSeconds) continue;
                var stages = plan.Stages.Take(lastMove + 1).ToImmutableArray().ToBuilder();
                stages.Add(new(MovementStageKind.Walk, plan.UsefulDestination, point, start, time));
                var preservesTiming = true;
                foreach (var suffix in plan.Stages.Skip(lastMove + 1))
                {
                    if (suffix.Kind == MovementStageKind.Wait)
                    {
                        if (time < suffix.EndSeconds)
                            stages.Add(suffix with { From = point, To = point, StartSeconds = time });
                        time = Math.Max(time, suffix.EndSeconds);
                    }
                    else
                    {
                        // Do not spend current attack/heal delivery for comfort.
                        if (PlanningMovementValue.Compare(time, suffix.StartSeconds) > 0) { preservesTiming = false; break; }
                        stages.Add(suffix with { From = point, To = point });
                        time = suffix.EndSeconds;
                    }
                }
                if (!preservesTiming) continue;
                // Party preparation alone uses already available stationary-action
                // slack/GCD time; it cannot extend a tight required movement window.
                if (!betterClearance && time > plan.DurationSeconds
                    && start + duration > Math.Max(start, context.MovementValue.ReadyIn)) continue;
                var route = stages.ToImmutable();
                if (PhaseCount(route) > MovementPlanningContext.MaximumPhases) continue;
                var inventory = plan.EndInventory?.At(time, context.Recharges);
                var refined = plan with
                {
                    Status = MovementSearchStatus.Bounded, Bound = MovementSearchBound.SearchInProgress,
                    Stages = route, UsefulDestination = point, DurationSeconds = time,
                    NativePriority = context.NativePriority(cell), DestinationPresentation = context.Presentation(cell),
                    MovementLossSeconds = context.MovementValue.Accumulate(context, route, 0, 0, 0).Loss,
                    EndInventory = inventory, RemainingResources = inventory?.Charges ?? plan.RemainingResources,
                    Evidence = plan.Evidence + "; bounded mechanic arrival; original escape and current execution retained"
                        + (betterParty ? "; healing posture improved within existing movement window" : "")
                };
                var proof = MovementPlanValidation.Validate(refined, context, 0, true, context.Identity.CapturedTimestamp);
                if (!proof.Allowed) continue;
                best = refined with { PlanSlackSeconds = Math.Min(plan.PlanSlackSeconds, proof.DepartureSeconds),
                    FirstDepartureDeadline = Math.Min(plan.DepartureDeadlineSeconds, proof.DepartureSeconds) };
                bestClearance = clearance;
                bestTravel = travel;
                bestPartyLoss = partyLoss;
            }
        return best;
    }

    private static int PhaseCount(ImmutableArray<MovementStage> stages)
    {
        var phases = 0;
        MovementStageKind? previous = null;
        foreach (var stage in stages)
        {
            if (stage.Kind is not (MovementStageKind.Walk or MovementStageKind.Wait) || stage.Kind != previous) ++phases;
            previous = stage.Kind;
        }
        return phases;
    }
}
