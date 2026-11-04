using BossMod;
using BossMod.Autorotation.MiscAI;
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

    private static void ArgathMisdirectionHeading()
    {
        var center = new WPos(105.97388f, -406.02734f);
        var map = new Map();
        map.InitGrid(0.5f, new(106f, -386f), 96, 96);
        var position = new WPos(106f, -395f);
        var towardCircle = Angle.FromDirection(center - position);
        var arrow = towardCircle + 25f.Degrees();
        var heading = BossMod.AI.AIController.SelectMisdirectionHeading(map, position, center, arrow);
        Check(heading is { } accepted && Angle.FromDirection(accepted).AlmostEqual(arrow, 0.001f)
            && !Angle.FromDirection(accepted).AlmostEqual(towardCircle, 15f.Degrees().Rad),
            "Follow forwards a checked oblique arrow heading even when the input hook would reject the waypoint bearing.");
        Check(BossMod.AI.AIController.SelectMisdirectionHeading(map, position, center, towardCircle + 90f.Degrees()) == null
            && BossMod.AI.AIController.SelectMisdirectionHeading(map, position, position, arrow) == null,
            "Follow waits when the arrow does not point toward the Coldblood opening or the waypoint has already been reached.");

        var start = map.WorldToGrid(position);
        var next = map.WorldToGrid(position + arrow.ToDirection());
        Check(start != next, "The blocked-heading fixture marks a cell ahead of the player.");
        map.PixelMaxG[map.GridToIndex(next)] = float.NegativeInfinity;
        Check(BossMod.AI.AIController.SelectMisdirectionHeading(map, position, center, arrow) == null,
            "Follow waits when the usable arrow crosses a nearby blocked cell.");

        var (world, actor, _) = ProjectionFixture();
        var route = new NavigationDecision.Context();
        var startOfFirstLeg = new WPos(101.21441f, -391.76056f);
        var startOfSecondLeg = new WPos(102.93152f, -399.442f);
        var safe = new WPos(105.171715f, -403.90292f);
        var impact = world.FutureTime(11.85);
        CheckLeg(startOfFirstLeg, startOfSecondLeg, world.CurrentTime);
        CheckLeg(startOfSecondLeg, safe, world.FutureTime(1.329));
        Check(safe.InCircle(center, 4.5f), "The two recorded movement legs end inside the buffered Coldblood opening.");

        void CheckLeg(WPos from, WPos to, DateTime now)
        {
            actor.PosRot = from.ToVec4();
            var hints = new AIHints { PathfindMapCenter = new(106, -386), PathfindMapBounds = new ArenaBoundsSquare(24) };
            hints.AddForbiddenZone(new SDDonut(center, 5f, 66f), impact);
            hints.GoalZones.Add(AIHints.GoalSingleTarget(center, 4.5f, 8f));
            var decision = NavigationDecision.Build(route, now, hints, actor, forbiddenZoneCushion: 0.5f);
            Check(decision.Destination is { } && decision.TimeToGoal + NavigationDecision.ActivationTimeCushion < (impact - now).TotalSeconds,
                "Native navigation can reach the Coldblood opening from the start of the recorded leg.");
            var waypoint = decision.Destination!.Value;
            var inferredArrow = Angle.FromDirection(to - from);
            Check(BossMod.AI.AIController.SelectMisdirectionHeading(route.Map, from, waypoint, inferredArrow) != null,
                "Follow accepts the heading inferred from the recorded movement leg.");
            var checkedHeading = BossMod.AI.AIController.SelectMisdirectionHeading(route.Map, from, waypoint, inferredArrow, 30f, 4f);
            Check(checkedHeading != null, "Normal movement accepts the heading inferred from the recorded movement leg.");

            hints.MaxCastTime = 0;
            hints.ForcedMovement = checkedHeading?.ToVec3();
            Check(NormalMovement.ApplyCastLeeway(hints, waypoint - from, true, 2f)
                && hints.ForceCancelCastOther && hints.ForcedMovement == checkedHeading?.ToVec3(),
                "An urgent cast departure keeps the validated arrow heading.");
            hints.ForcedMovement = BossMod.AI.AIController.SelectMisdirectionHeading(route.Map, from, waypoint, inferredArrow + 180f.Degrees(), 30f, 4f)?.ToVec3();
            Check(hints.ForcedMovement == null && !NormalMovement.ApplyCastLeeway(hints, waypoint - from, true, 2f) && hints.ForcedMovement == null,
                "An urgent cast departure still waits when the arrow points away from the opening.");
            Check(NormalMovement.ApplyCastLeeway(hints, waypoint - from, false, 2f) && hints.ForcedMovement == (waypoint - from).ToVec3(),
                "Ordinary movement can still depart for the waypoint as soon as the arrow expires.");
        }
    }

    private static void ForcedMarch()
    {
        ArgathMisdirectionHeading();
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
