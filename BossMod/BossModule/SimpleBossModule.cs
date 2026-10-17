namespace BossMod;

// base class for simple boss modules (hunts, fates, dungeons, etc.)
// these always center map around PC
public abstract class SimpleBossModule(WorldState ws, Actor primary) : BossModule(ws, primary, primary.Position,
    new ArenaBoundsCircle(30f, AllowObstacleMap: true, AllowDrawing3DArenaBounds: false) { WorldProjectionHeight = 10f })
{
    private WPos _prevFramePathfindCenter;
    private bool? _usesInheritedSearchFrame;

    public override bool CheckReset() => !PrimaryActor.InCombat;

    protected override void UpdateModule()
    {
        Arena.Center = WorldState.Party.Player()?.Position ?? default;
        // we don't want to change pathfinding map origin every time player slightly moves, it makes movement jittery
        // instead, (a) quantize origin and (b) only update it when player moves sufficiently far away
        if (!_prevFramePathfindCenter.AlmostEqual(Arena.Center, 5f))
        {
            _prevFramePathfindCenter = Arena.Center.Rounded();
        }
    }

    protected override void CalculateModuleAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        hints.PathfindMapCenter = _prevFramePathfindCenter;
        hints.PathfindMapBounds = AIHints.DefaultBounds;
        // Custom update/hint overrides own their frame policy. Correct only the inherited
        // default (including ordinary FATE modules), without reframing authored geometry.
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        _usesInheritedSearchFrame ??= GetType().GetMethod(nameof(UpdateModule), flags, null, Type.EmptyTypes, null)?.DeclaringType == typeof(SimpleBossModule)
            && GetType().GetMethod(nameof(CalculateModuleAIHints), flags, null,
                [typeof(int), typeof(Actor), typeof(PartyRolesConfig.Assignment), typeof(AIHints)], null)?.DeclaringType == typeof(SimpleBossModule);
        if (!_usesInheritedSearchFrame.Value || hints.PathfindMapObstacles.Bitmap == null)
            return;
        var center = hints.PathfindMapCenter;
        var (entry, bitmap) = Obstacles.Find(new(center.X, actor.PosRot.Y, center.Z));
        if (Pathfinding.DefaultTerrainFrame.Align(hints, entry, bitmap) is { } frame)
        {
            hints.PathfindMapCenter = frame.Center;
            hints.PathfindMapObstacles = frame.Region;
            hints.DefaultTerrainFrameAlignment = frame;
        }
    }
}

public abstract class OpenWorldFate(WorldState ws, Actor primary) : SimpleBossModule(ws, primary)
{
    // only activate module when close and deactivate it if player leaves area
    protected override bool CheckPull() => base.CheckPull() && Raid.Player()!.Position.InCircle(Arena.Center, 30f);
    public override bool CheckReset() => base.CheckReset() || !Raid.Player()!.Position.InCircle(Arena.Center, 40f);
}
