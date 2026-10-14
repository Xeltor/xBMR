using BossMod;
using BossMod.Components;
using BossMod.Pathfinding;

static partial class Checks
{
    private static (NavigationDecision decision, NavigationDecision.Context context, AIHints hints) Route(BossModule module, Actor player, WPos? goal = null, float goalRadius = 0.5f)
    {
        var hints = new AIHints
        {
            PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center,
            PathfindMapArenaProjectionLayer = module.ResolveArenaProjectionLayer(player)
        };
        foreach (var component in module.Components)
            component.AddAIHints(0, player, default, hints);
        if (goal is { } target)
            hints.GoalZones.Add(AIHints.GoalSingleTarget(target, goalRadius));
        hints.Normalize();
        var context = new NavigationDecision.Context();
        return (NavigationDecision.Build(context, Now, hints, player), context, hints);
    }

    private static List<WPos> RoutePoints(NavigationDecision.Context context)
    {
        List<WPos> result = [];
        var index = context.ThetaStar.BestIndex();
        for (var count = 0; count < 1000; ++count)
        {
            result.Add(context.ThetaStar.CellCenter(index));
            var parent = context.ThetaStar.NodeByIndex(index).ParentIndex;
            if (parent == index) break;
            index = parent;
        }
        result.Reverse();
        return result;
    }

    private static void MovementRoutes()
    {
        foreach (var (car, left, z) in new[] { (3, true, 205f), (3, false, 205f), (5, true, 310f), (5, false, 300f) })
        {
            using var module = Module(car);
            var emission = Component<GenericAOEs>(module, "HeadOnEmission");
            var source = Actor(20, 0x233C, 100, 0, module.Center.Z - 35);
            var pad = new WPos(left ? 96.5f : 103.5f, z);
            // Include the exact side of the reported ground-level pillar stall.
            var player = Actor(30, 0, left ? 95.32f : 104.68f, 0, z - 0.28f);
            emission.OnCastStarted(source, Cast(45635, source.Position));
            var route = Route(module, player, new(left ? 92.5f : 107.5f, z));
            Console.WriteLine($"up {car}/{left}: destination={route.decision.Destination} next={route.decision.NextWaypoint} time={route.decision.TimeToGoal} leeway={route.decision.LeewaySeconds}");
            Check(route.decision.Destination is { } approach && approach.InCircle(pad, 0.76f), "ground route approaches the actual pad instead of the pillar/upper destination");
            Check(route.decision.LeewaySeconds > 0 && route.context.Map.HasTeleporters, "up route retains positive warning/travel reserve");
            var points = RoutePoints(route.context);
            Check(points.Any(p => p.InCircle(pad, 0.76f)) && points.Any(p => left ? p.X < 94.5f : p.X > 105.5f), "native parent chain uses pad and reaches raised floor");
            emission.OnCastFinished(source, Cast(45635, source.Position));
            emission.OnCastStarted(source, Cast(45637, source.Position));
            player.PosRot = new(left ? 92.5f : 107.5f, 5, z, 0);
            route = Route(module, player);
            var end = route.context.ThetaStar.CellCenter(route.context.ThetaStar.BestIndex());
            Check(route.decision.Destination != null && !Forbidden(route.hints, end) && end.X > 95 && end.X < 105, "upper hazard produces an inward drop to safe ground on each platform");
            Check(route.decision.TimeToGoal >= 0.8f && route.decision.LeewaySeconds > 0, "drop consumes fall time and retains a safe departure margin");
            Check(!end.InCircle(pad, 1.25f), "drop landing avoids triggering the up pad again");
            points = RoutePoints(route.context);
            Check(points.All(p => module.Bounds.Contains(p - module.Center)), "transfer parent chain stays inside lethal outer bounds");
        }
        using (var module = Module(2))
        {
            var player = Actor(30, 0, 94.787544f, 0, 160.52727f);
            var route = Route(module, player, new(94.75f, 153.25f));
            Check(route.decision.Destination is { } clear && (clear.X < 94.5f || clear.Z >= 160.75f), "recorded box-corner stall first backs away from collision");
            // A position already inside the newly added clearance first escapes the hard
            // obstacle. Replanning from that waypoint can then score the destination goal.
            var escape = route.decision.Destination!.Value;
            player.PosRot = new(escape.X, 0, escape.Z, 0);
            route = Route(module, player, new(94.75f, 153.25f));
            var points = RoutePoints(route.context);
            // Check the walked segments, not just the destination behind the box.
            var obstacle = new SDRect(new(97.5f, 157.5f), new WDir(0, 1), 2.5f, 2.5f, 2.5f);
            foreach (var pair in points.Zip(points.Skip(1)))
                for (var i = 1; i <= 20; ++i)
                {
                    var p = pair.First + (pair.Second - pair.First) * (i / 20f);
                    Check(obstacle.Distance(p) >= 0.5f, "box route clears physical corner along the whole segment");
                }
            Check(route.context.ThetaStar.CellCenter(route.context.ThetaStar.BestIndex()).Z < 155, "corner escape reaches the requested side of the box");
        }
        using (var module = Module(5))
        {
            var player = Actor(30, 0, 106.75014f, 0, 285.53268f);
            var route = Route(module, player, new(106.75f, 285.25f));
            var end = route.context.ThetaStar.CellCenter(route.context.ThetaStar.BestIndex());
            Check(end.Z >= 285.75f, "car-five north wall does not attract the actor into its collision margin");
        }
        using (var module = Module(4))
        {
            var geometry = Component<BossComponent>(module, "CarGeometry");
            var derail = Component<SimpleAOEs>(module, "Derail");
            var player = Actor(30, 0, 100, 0, 250);
            var cast = Cast(45654, new(100, 235));
            derail.OnCastStarted(module.PrimaryActor, cast);
            var text = new BossComponent.TextHints();
            geometry.AddHints(0, player, text);
            Check(text.Count == 1 && text[0].Item1.Contains("rear jump pad"), "rear-pad warning appears at Derail cast start");
            Check(Forbidden(Hints(module, derail, player), new(100, 262.5f)), "rear marker does not carve a false safe hole in Derail");
            geometry.OnMapEffect(10, 0x00020001);
            var route = Route(module, player);
            Check(route.decision.Destination is { } pad && pad.Z > player.Position.Z
                && RoutePoints(route.context).Any(p => p.InCircle(new(100, 262.5f), 0.76f)), "native Derail escape heads toward and uses the rear pad");
            Check(route.context.ThetaStar.CellCenter(route.context.ThetaStar.BestIndex()).Z > 285, "Derail escape reaches the next car across a directed edge");
            derail.OnCastFinished(module.PrimaryActor, cast);
            geometry.OnMapEffect(10, 0x00080004);
            text = new();
            geometry.AddHints(0, player, text);
            Check(text.Count == 0, "rear warning clears after transfer closes and cast ends");
            player.PosRot = new(100, 0, 291.5f, 0);
            route = Route(module, player, new(100, 300), 1f);
            Check(route.context.ThetaStar.CellCenter(route.context.ThetaStar.BestIndex()).InCircle(new(100, 300), 1f)
                && route.decision.LeewaySeconds > 0, "landing car remains navigable between pad closure and the car-five event");
        }
    }
}
