using BossMod;
using BossMod.Components;
using BossMod.Pathfinding;

public static partial class Checks
{
    private sealed class FacingMarch(BossModule module) : GenericForcedMarch(module)
    {
        internal float? SeenFacing;
        public override bool DestinationUnsafe(int slot, Actor actor, WPos pos)
        { SeenFacing = actor.Rotation.Rad; return pos.X > 4 || base.DestinationUnsafe(slot, actor, pos); }
    }

    private static void ForcedMarch()
    {
        var (world, actor, module) = ProjectionFixture();
        actor.PosRot = default;
        var hints = new AIHints { PathfindMapBounds = new ArenaBoundsSquare(8) };
        var march = new FacingMarch(module);
        march.AddForcedMovement(actor, default, 1, world.FutureTime(1));
        var map = Grid(20, 20);
        var terrain = new NativeRouteValidation.Source(new(map, null, new byte[400]), default, [], [], world.CurrentTime);
        NativeMarchSafety.Result Evaluate(float facing = 0, NativeRouteValidation.Budget? budget = null)
            => NativeMarchSafety.Evaluate(new(hints, module, march, actor, 0, terrain), facing, budget ?? new());
        var clear = Evaluate();
        Check(clear.Status == "clear" && clear.Route.Length == 2 && clear.TimedLegs.Length == 3,
            "Native march proves the complete wait, leg and landing interval without serialized transport.");
        Check(Evaluate(MathF.PI / 2).Status == "blocked" && march.SeenFacing == MathF.PI / 2 && actor.Rotation.Rad == 0,
            "Authored destination rules see the projected facing without mutating the real actor.");
        hints.AddForbiddenZone(new SDCircle(default, 1), world.FutureTime(0.5));
        Check(Evaluate().Status == "blocked", "March preparation must clear warnings before activation.");
        hints.ForbiddenZones.Clear();
        hints.AddForbiddenZone(new SDCircle(new(0, 6), 1), world.FutureTime(2.4));
        Check(Evaluate().Status == "blocked", "A warning covering the landing hold cannot be ignored.");
        hints.ForbiddenZones.Clear();
        hints.TemporaryObstacles.Add(new SDCircle(new(0, 3), 1));
        Check(Evaluate().Status == "blocked", "The full forced leg respects physical-shaped obstacles.");
        hints.TemporaryObstacles.Clear();
        Check(Evaluate(budget: new(0)).Status == "unknown", "Cancellation or exhausted explicit work limit never proves safety.");
        march.State.Clear();
        for (var i = 0; i < 40; ++i)
            march.AddForcedMovement(actor, i == 0 ? default : 180f.Degrees(), 0.1f, world.FutureTime(1 + i));
        Check(march.ProjectFromPose(actor, actor.Position, actor.Rotation)?.Count == 40,
            "Native projection no longer truncates a mechanic at the old IPC pending-move cap.");
        Check(Evaluate().Status == "clear", "A finite authored sequence is not rejected by old transport count/time caps.");
    }
}
