using BossMod.Components;

namespace BossMod.CombatAssistance.Planning;

internal sealed record ForcedMovementLeg(WPos From, WPos To, float Start, float End, Angle? Facing = null);

// A compulsory native projection, not a walk or an invented mobility action.
// The encounter component alone supplies direction changes, wall stops and timing.
internal sealed record PlanningForcedMovement(string Source, Angle Heading, bool CanTurn,
    ImmutableArray<ForcedMovementLeg> Legs, bool Active = false)
{
    internal AuthoredKnockbackSource? Knockback { get; init; }
    internal bool ConservativeCorridor { get; init; }
    internal WPos Landing => Legs[^1].To;
    internal float End => Legs[^1].End;
    internal float StartsIn => Legs.FirstOrDefault(l => l.From != l.To)?.Start ?? 0;

    internal bool Evaluate(MovementPlanningContext context, out float slack)
    {
        slack = float.MaxValue;
        if (Legs.IsEmpty || !context.Origin.AlmostEqual(Legs[0].From, 0.001f)
            || CanTurn && (StartsIn < Math.Max(context.UnlockSeconds, 0.1f)
                || !context.ExecutionRules.PermitsAction(Heading, 0, StartsIn)
                || !context.ExecutionRules.PermitsMovement(0, 0.1f))) return false;
        var previous = context.Origin;
        var time = 0f;
        var facing = Heading;
        foreach (var leg in Legs)
        {
            facing = leg.Facing ?? (leg.From != leg.To ? Angle.FromDirection(leg.To - leg.From) : facing);
            if (context.ExecutionRules.ForbiddenFacing.Any(d => d.Activation <= leg.End
                && facing.AlmostEqual(d.Center, d.HalfWidth.Rad))) return false;
            if (leg.From != previous || !float.IsFinite(leg.Start) || !float.IsFinite(leg.End)
                || leg.Start < time || leg.End < leg.Start || leg.End > MovementPlanningContext.MaximumHorizonSeconds
                || !context.Traverse(previous, previous, time, leg.Start - time, out var waitSlack)
                || !context.TraverseOrdinary(leg.From, leg.To, ConservativeCorridor ? leg.End : leg.Start,
                    ConservativeCorridor ? 0 : leg.End - leg.Start, out var legSlack)) return false;
            slack = Math.Min(slack, Math.Min(waitSlack, legSlack));
            previous = leg.To;
            time = leg.End;
        }
        return context.Cell(Landing) >= 0;
    }

    internal bool Same(PlanningForcedMovement old, float age)
        => Knockback?.SameGeometry(old.Knockback ?? Knockback) != false && (Knockback == null) == (old.Knockback == null)
            && ConservativeCorridor == old.ConservativeCorridor && Source == old.Source && Heading == old.Heading && CanTurn == old.CanTurn && Active == old.Active && Legs.Length == old.Legs.Length
            && Legs.Select((leg, i) => leg.From == old.Legs[i].From && leg.To == old.Legs[i].To && leg.Facing == old.Legs[i].Facing
                && Math.Abs(leg.Start - Math.Max(0, old.Legs[i].Start - age)) < 0.002f
                && Math.Abs(leg.End - Math.Max(0, old.Legs[i].End - age)) < 0.002f).All(same => same);
}

internal sealed record ForcedMovementChoices(ImmutableArray<PlanningForcedMovement> Routes, bool SampledHeadings)
{
    internal bool Same(ForcedMovementChoices other, float age) => SampledHeadings == other.SampledHeadings
        && Routes.Length == other.Routes.Length && Routes.Select((route, i) => route.Same(other.Routes[i], age)).All(same => same);
}

internal static class NativeForcedMovementCapture
{
    // Five degree alternatives are an explicit angular search approximation. They
    // are unrelated to IPC capacity and never establish global optimality.
    internal const int HeadingSamples = 72;

    // This is the existing correction window, not an invented movement duration.
    // Compulsory authority is independent of whether the player enabled optional
    // facing/heading corrections. Far countdowns keep their existing attack window.
    internal static bool NeedsExclusivePlanning(WorldState world, BossModule? module, Actor actor)
        => module?.Components.OfType<GenericForcedMarch>().Any(march => NeedsExclusivePlanning(world, march, actor)) == true;

    internal static bool NeedsExclusivePlanning(WorldState world, GenericForcedMarch march, Actor actor)
    {
        if (!march.HasForcedMovements(actor) || !march.State.TryGetValue(actor.InstanceID, out var state)) return false;
        var next = state.PendingMoves.Select(move => (move.activation - world.CurrentTime).TotalSeconds)
            .Where(seconds => seconds >= -0.35).DefaultIfEmpty(double.PositiveInfinity).Min();
        return new Runtime.ForcedMarchTiming(next, (state.ForcedEnd - world.CurrentTime).TotalSeconds, 0).ProtectFacing;
    }

    internal static ForcedMovementChoices? Capture(WorldState world, BossModule? module, Actor actor, bool headingAllowed)
    {
        if (module == null) return null;
        var marches = module.Components.OfType<GenericForcedMarch>().Where(m => m.HasForcedMovements(actor)).ToArray();
        if (marches.Length == 0) return null;
        // Independent simultaneous projection owners need an authored composition;
        // never select one and silently discard the other.
        if (marches.Length != 1) return new([], false);
        var march = marches[0];
        if (!march.State.TryGetValue(actor.InstanceID, out var state) || march.OverrideDirection
            || !float.IsFinite(march.MovementSpeed) || march.MovementSpeed <= 0) return new([], false);
        var active = state.ForcedEnd > world.CurrentTime;
        var turn = !active && headingAllowed;
        var headings = turn ? Enumerable.Range(0, HeadingSamples).Select(i => (i * 360f / HeadingSamples).Degrees())
            .Prepend(actor.Rotation).Distinct() : [actor.Rotation];
        var routes = ImmutableArray.CreateBuilder<PlanningForcedMovement>();
        foreach (var heading in headings)
        {
            var pose = actor.CopyForProjection(actor.Position, heading);
            var projection = march.ProjectFromPose(pose, pose.Position, heading);
            if (projection is not { Count: > 0 }) continue;
            var legs = ImmutableArray.CreateBuilder<ForcedMovementLeg>();
            var previous = actor.Position;
            var elapsed = 0f;
            var previousFacing = heading;
            var valid = true;
            foreach (var step in projection)
            {
                var start = (float)(step.Activation - world.CurrentTime).TotalSeconds;
                var end = (float)(step.End - world.CurrentTime).TotalSeconds;
                var arrival = start + (step.To - step.From).Length() / march.MovementSpeed;
                if (step.From != previous || !float.IsFinite(start) || !float.IsFinite(end) || !float.IsFinite(arrival)
                    || start < elapsed || end < start || end > MovementPlanningContext.MaximumHorizonSeconds || arrival > end + 0.01f
                    || march.DestinationUnsafe(PartyState.PlayerSlot, pose, step.To)) { valid = false; break; }
                arrival = Math.Min(arrival, end);
                if (start > elapsed) legs.Add(new(previous, previous, elapsed, start, previousFacing));
                legs.Add(new(step.From, step.To, start, arrival, step.Direction));
                if (arrival < end) legs.Add(new(step.To, step.To, arrival, end, step.Direction));
                previous = step.To;
                previousFacing = step.Direction;
                elapsed = end;
            }
            if (valid && legs.Count != 0)
                routes.Add(new(march.GetType().FullName ?? march.GetType().Name, heading, turn && !heading.AlmostEqual(actor.Rotation, 0.01f), legs.ToImmutable(), active));
        }
        return new(routes.ToImmutable(), turn);
    }
}
