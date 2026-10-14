namespace BossMod.Heavensward.Alliance.A36DiabolosHollow;

// The original records portal participants near (350,-300,-445), while this
// module's main floor is at (-350,1,-445). This classifies the separate region;
// it does not assert a safe portal radius, floor stage or route between floors.
class PortalSafety(BossModule module) : BossComponent(module)
{
    public const string Instruction = "Portal arena: move manually; automatic movement is paused.";
    public static bool InPortal(Actor actor) => actor.PosRot.Y < -100 && actor.Position.X > 0;

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (InPortal(actor))
        {
            hints.Clear();
            hints.Add(Instruction, false);
        }
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (InPortal(actor))
        {
            hints.Clear();
            hints.Add(Instruction);
        }
    }

    public static void HoldAutomaticMovement(Actor actor, AIHints hints, DateTime now)
    {
        if (!InPortal(actor))
            return;
        // No invented replacement arena. The inherited main-floor metadata is
        // unusable here: withdraw its guidance and hold automated navigation.
        hints.ForbiddenZones.Clear();
        hints.GoalZones.Clear();
        hints.TemporaryObstacles.Clear();
        hints.Teleporters.Clear();
        hints.ForbiddenDirections.Clear();
        hints.PredictedDamage.Clear();
        hints.PathfindMapObstacles = default;
        hints.GoalZonesEnabled = false;
        hints.RecommendedPositional = default;
        hints.ImminentSpecialMode = (AIHints.SpecialMode.NoMovement, now, default);
        hints.NoMovementAllowsManualInput = true;
        hints.ForbidDashes = true;
    }
}
