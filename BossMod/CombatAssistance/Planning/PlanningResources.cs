namespace BossMod.CombatAssistance.Planning;

// One entry per native cooldown group. Gauge costs deliberately have no clock:
// future gauge generation requires an evidenced action, never elapsed time alone.
internal sealed record PlanningRecharge(int Resource, int Capacity, float Interval, float NextIn)
{
    internal bool Valid => Resource is >= 0 and < 16 && Capacity is > 0 and <= 15
        && float.IsFinite(Interval) && Interval > 0 && float.IsFinite(NextIn) && NextIn >= 0;
    internal bool Same(PlanningRecharge previous, float age)
        => Resource == previous.Resource && Capacity == previous.Capacity && Interval == previous.Interval
            && (NextIn == float.MaxValue ? previous.NextIn == float.MaxValue
                : Math.Abs(NextIn - Math.Max(0, previous.NextIn - age)) < 0.02f);
}

internal sealed record PlanningResourceState(ulong Charges, ImmutableArray<float> Next)
{
    internal static PlanningResourceState Initial(ulong charges, ImmutableArray<PlanningRecharge> recharges)
        => new(charges, recharges.Select(r => MovementPlanningContext.ChargeCount(charges, r.Resource) == r.Capacity
            ? float.MaxValue : r.NextIn).ToImmutableArray());

    internal PlanningResourceState At(float time, ImmutableArray<PlanningRecharge> recharges)
    {
        var charges = Charges;
        var next = Next;
        for (var i = 0; i < recharges.Length; ++i)
        {
            var r = recharges[i];
            var count = MovementPlanningContext.ChargeCount(charges, r.Resource);
            var due = next[i];
            while (count < r.Capacity && due <= time)
            {
                ++count;
                charges += 1ul << (r.Resource * 4);
                due = count == r.Capacity ? float.MaxValue : due + r.Interval;
            }
            if (due != next[i]) next = next.SetItem(i, due);
        }
        return charges == Charges && next == Next ? this : new(charges, next);
    }

    internal bool Ready(ulong cost, float earliest, ImmutableArray<PlanningRecharge> recharges, out float ready)
    {
        ready = earliest;
        var state = At(earliest, recharges);
        for (var resource = 0; resource < 16; ++resource)
        {
            var missing = MovementPlanningContext.ChargeCount(cost, resource)
                - MovementPlanningContext.ChargeCount(state.Charges, resource);
            if (missing <= 0) continue;
            var index = -1;
            for (var i = 0; i < recharges.Length; ++i)
                if (recharges[i].Resource == resource) { index = i; break; }
            if (index < 0 || MovementPlanningContext.ChargeCount(cost, resource) > recharges[index].Capacity) return false;
            ready = Math.Max(ready, state.Next[index] + (missing - 1) * recharges[index].Interval);
        }
        return float.IsFinite(ready) && ready <= MovementPlanningContext.MaximumHorizonSeconds;
    }

    internal PlanningResourceState Spend(ulong cost, float at, ImmutableArray<PlanningRecharge> recharges)
    {
        var state = At(at, recharges);
        if (!MovementPlanningContext.NoFewerResources(state.Charges, cost))
            throw new InvalidOperationException("Cannot spend an unobserved future resource.");
        var next = state.Next;
        for (var i = 0; i < recharges.Length; ++i)
            if (MovementPlanningContext.ChargeCount(cost, recharges[i].Resource) != 0
                && MovementPlanningContext.ChargeCount(state.Charges, recharges[i].Resource) == recharges[i].Capacity)
                next = next.SetItem(i, at + recharges[i].Interval);
        return new(state.Charges - cost, next);
    }

    internal PlanningResourceState Grant(ulong grant)
    {
        if (grant == 0) return this;
        for (var slot = 0; slot < 16; ++slot)
            if (MovementPlanningContext.ChargeCount(Charges, slot) + MovementPlanningContext.ChargeCount(grant, slot) > 15)
                throw new InvalidOperationException("An action grant exceeds the represented resource capacity.");
        return new(Charges + grant, Next);
    }

    // Conservative pruning: unequal replenishing inventories remain distinct.
    // Equal counts with an earlier next charge can reproduce the later schedule.
    internal bool Dominates(PlanningResourceState other, float at, ImmutableArray<PlanningRecharge> recharges)
    {
        var current = At(at, recharges);
        if (!MovementPlanningContext.NoFewerResources(current.Charges, other.Charges)) return false;
        for (var i = 0; i < recharges.Length; ++i)
            if (MovementPlanningContext.ChargeCount(current.Charges, recharges[i].Resource)
                    != MovementPlanningContext.ChargeCount(other.Charges, recharges[i].Resource)
                || current.Next[i] > other.Next[i]) return false;
        return true;
    }
}
