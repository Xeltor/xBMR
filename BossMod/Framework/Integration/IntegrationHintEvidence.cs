namespace BossMod.Integration;

// Captured before consumer goals/rotation, independently of scorer values and
// GoalZonesEnabled. Missing or wrong-generation evidence remains unknown.
internal sealed record IntegrationHintEvidence(IntegrationSession.Context Source, int GoalCount)
{
    internal const string GoalScope = "native-hints-before-integration-and-rotation";
    internal int? CountFor(IntegrationSession.Context context)
        => Source.SchemaVersion == 1 && Source.Stage == "hints-ready" && context.SchemaVersion == 1
            && Source.InstanceGeneration == context.InstanceGeneration && Source.FrameId == context.FrameId
            && context.Stage is "hints-ready" or "movement-proposed" or "movement-applied" && GoalCount >= 0 ? GoalCount : null;
    internal bool? HasGoalsFor(IntegrationSession.Context context) => CountFor(context) is { } count ? count > 0 : null;
    internal static bool? AggroPlayer(Actor? actor) => actor is { IsDestroyed: false } ? actor.AggroPlayer : null;
}
