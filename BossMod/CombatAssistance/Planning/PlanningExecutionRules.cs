namespace BossMod.CombatAssistance.Planning;

internal sealed record PlanningForbiddenFacing(Angle Center, Angle HalfWidth, float Activation);

// Authored prohibitions retain their identity and timing. No expiry is inferred;
// a later capture must explicitly remove a constraint before it can be ignored.
internal sealed record PlanningExecutionRules(float MovementStopsAt, float ActionsStopAt,
    ImmutableArray<PlanningForbiddenFacing> ForbiddenFacing,
    float MovementResumesAt = float.MaxValue, float ActionsResumeAt = float.MaxValue)
{
    internal static readonly PlanningExecutionRules None = new(float.MaxValue, float.MaxValue, []);
    internal static PlanningExecutionRules Capture(WorldState world, AIHints hints)
        => Capture(world.CurrentTime, hints);

    internal static PlanningExecutionRules Capture(DateTime now, AIHints hints)
    {
        var (mode, activation, finish) = hints.ImminentSpecialMode;
        var deadline = Math.Max(0, (float)(activation - now).TotalSeconds);
        var release = finish > activation ? Math.Max(0, (float)(finish - now).TotalSeconds) : float.MaxValue;
        return new(mode is AIHints.SpecialMode.NoMovement or AIHints.SpecialMode.Pyretic ? deadline : float.MaxValue,
            mode == AIHints.SpecialMode.Pyretic ? deadline : float.MaxValue,
            hints.ForbiddenDirections.Select(d => new PlanningForbiddenFacing(d.center, d.halfWidth,
                Math.Max(0, (float)(d.activation - now).TotalSeconds))).ToImmutableArray(),
            mode is AIHints.SpecialMode.NoMovement or AIHints.SpecialMode.Pyretic ? release : float.MaxValue,
            mode == AIHints.SpecialMode.Pyretic ? release : float.MaxValue);
    }
    internal bool PermitsMovement(float start, float end) => end < MovementStopsAt || start >= MovementResumesAt;
    internal bool PermitsAction(Angle? facing, float start, float end)
        => (end < ActionsStopAt || start >= ActionsResumeAt)
            && (facing == null || !ForbiddenFacing.Any(d => d.Activation <= end && facing.Value.AlmostEqual(d.Center, d.HalfWidth.Rad)));
    internal IEnumerable<float> ReleasesAfter(float time)
        => new[] { MovementResumesAt, ActionsResumeAt }.Distinct().Where(t => t > time && t <= MovementPlanningContext.MaximumHorizonSeconds);
    internal bool SameTemporalPhase(float a, float b)
    {
        // This is called for pairs of frontier labels. Enumerators and closures
        // here multiply allocation pressure throughout the search.
        if ((a >= MovementStopsAt) != (b >= MovementStopsAt)
            || (a >= MovementResumesAt) != (b >= MovementResumesAt)
            || (a >= ActionsStopAt) != (b >= ActionsStopAt)
            || (a >= ActionsResumeAt) != (b >= ActionsResumeAt)) return false;
        foreach (var direction in ForbiddenFacing)
            if ((a >= direction.Activation) != (b >= direction.Activation)) return false;
        return true;
    }
    internal bool SameRules(PlanningExecutionRules previous, float age)
        => Countdown(MovementStopsAt, previous.MovementStopsAt, age) && Countdown(ActionsStopAt, previous.ActionsStopAt, age)
            && Countdown(MovementResumesAt, previous.MovementResumesAt, age) && Countdown(ActionsResumeAt, previous.ActionsResumeAt, age)
            && ForbiddenFacing.Length == previous.ForbiddenFacing.Length && ForbiddenFacing.Select((d, i) =>
                d.Center == previous.ForbiddenFacing[i].Center && d.HalfWidth == previous.ForbiddenFacing[i].HalfWidth
                    && Countdown(d.Activation, previous.ForbiddenFacing[i].Activation, age)).All(match => match);
    private static bool Countdown(float now, float previous, float age)
        => now == float.MaxValue || previous == float.MaxValue ? now == previous : Math.Abs(now - Math.Max(0, previous - age)) <= 0.002f;
}
