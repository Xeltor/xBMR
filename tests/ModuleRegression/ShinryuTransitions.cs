using System.Numerics;
using BossMod;
using BossMod.Components;
using BossMod.Pathfinding;

static partial class Regression
{
    private static void ShinryuTransitions()
    {
        foreach (var upper in new[] { false, true })
        {
            var (module, floor, player) = Shinryu();
            player.PosRot = new Vector4(810, upper ? -879 : -900, -826, 0);
            AIHints Read()
            {
                var hints = new AIHints
                {
                    PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center,
                    PathfindMapArenaProjectionLayer = player.PosRot.Y < -890 ? 0 : 1
                };
                floor.AddAIHints(0, player, default, hints);
                return hints;
            }
            var radius = upper ? 6 : 2;
            var idle = Read();
            Check(idle.ForbiddenZones.Count == 1 && idle.ForbiddenZones[0].activation <= Now,
                "ordinary P1 movement has an immediate transition exclusion before any telegraph");
            Check(idle.ForbiddenZones[0].shapeDistance.Distance(ShinryuPad + new WDir(radius - 0.1f, 0)) < 0
                && idle.ForbiddenZones[0].shapeDistance.Distance(ShinryuPad + new WDir(radius + 0.1f, 0)) > 0,
                "current floor uses its existing transition radius without enlarging line hazards");
            // Native goals require all four cell corners inside; use a goal
            // large enough to survive the 0.5-yalm grid's corner sampling.
            idle.GoalZones.Add(AIHints.GoalSingleTarget(new WPos(830, -826), 1.5f));
            var context = new NavigationDecision.Context();
            var route = NavigationDecision.Build(context, Now, idle, player);
            var padCell = context.Map.WorldToGrid(ShinryuPad);
            Check(context.Map.PixelMaxG[context.Map.GridToIndex(padCell)] <= 0,
                "native navigation raster receives the immediate transition restriction");
            Check(route.Destination is { } next && !next.InCircle(ShinryuPad, radius),
                "native first waypoint avoids the transition on a goal across the centre");
            Check(route.Destination is { } step
                && !new SDCircle(ShinryuPad, radius).RowIntersectsShape(player.Position, step - player.Position, 1),
                "native first movement segment goes around the transition rather than across it");

            foreach (var id in new[] { 49107u, 49110u, 49130u })
            {
                var cast = Cast(id, ShinryuPad);
                floor.OnCastStarted(player, cast);
                var mustChange = upper == (id == 49107);
                var hints = Read();
                Check(hints.ForbiddenZones.Count == (mustChange ? 1 : 2),
                    "Breath/Tail/Atomic Tail permit only their required crossing");
                Check(hints.ForbiddenZones.Any(z => z.activation <= Now && z.shapeDistance.Contains(ShinryuPad)) == !mustChange,
                    "already-correct floor blocks immediate bounce-back while wrong floor keeps the route open");
                if (mustChange)
                {
                    player.PosRot.Y = upper ? -900 : -879;
                    var arrived = Read();
                    Check(arrived.ForbiddenZones.Count == 2
                        && arrived.ForbiddenZones.Any(z => z.activation <= Now && z.shapeDistance.Contains(ShinryuPad)),
                        "arrival immediately closes the reverse transition without waiting for the swipe");
                    player.PosRot.Y = upper ? -879 : -900;
                }
                floor.OnCastFinished(player, cast);
                Check(Read().ForbiddenZones.Count == 1, "swipe end restores ordinary floor preservation");
            }

            foreach (var light in new[] { false, true })
            {
                var status = new ActorStatus(light ? 5352u : 5353u, 0, Now.AddSeconds(20), 0);
                floor.OnStatusGain(player, ref status);
                floor.OnCastStarted(player, Cast(49113, ShinryuPad));
                Check(Read().ForbiddenZones.Count == (light == upper ? 2 : 1),
                    "both Twilight colours preserve required transitions on both starting floors");
                floor.OnEventCast(player, Effect(49115));
                floor.OnEventCast(player, Effect(49116));
                Check(Read().ForbiddenZones.Count == 1, "both Twilight helpers restore ordinary floor preservation");
            }

            // Synthetic two-wave patterns based on the two observed checkerboard
            // orientations. No physical boundary or live survival claim is made.
            var lines = Component<SimpleAOEGroups>(module, ShinryuPrefix + "StarflareP1");
            foreach (var swapped in new[] { false, true })
            {
                var helpers = new List<(Actor Actor, ActorCastInfo Cast)>();
                foreach (var y in new[] { -900f, -879f })
                foreach (var vertical in new[] { false, true })
                for (var i = 0; i < (vertical ? 6 : 4); ++i)
                {
                    var fast = (i % 2 == 0) != swapped;
                    var pos = vertical ? new WPos(795 + i * 10, -840) : new WPos(790, -835 + i * 10);
                    var helper = Actor((ulong)(100 + helpers.Count), 0x233C, pos);
                    var cast = Cast(fast ? 49126u : 49127u, pos);
                    cast.Location = new(pos.X, y, pos.Z);
                    cast.Rotation = vertical ? default : 90f.Degrees();
                    cast.TotalTime = fast ? 5 : 7;
                    lines.OnCastStarted(helper, cast);
                    helpers.Add((helper, cast));
                }
                var first = Read();
                lines.AddAIHints(0, player, default, first);
                Check(first.ForbiddenZones.Count == 6 && first.ForbiddenZones.Any(z => z.activation <= Now && z.shapeDistance.Contains(ShinryuPad)),
                    "first Starflare wave retains five floor-specific lines plus the immediate transition guard");
                var tail = Cast(49110, ShinryuPad);
                floor.OnCastStarted(player, tail);
                var overlap = Read();
                lines.AddAIHints(0, player, default, overlap);
                Check(overlap.ForbiddenZones.Count == (upper ? 7 : 6)
                    && overlap.ForbiddenZones.Any(z => z.activation <= Now && z.shapeDistance.Contains(ShinryuPad)) == upper,
                    "overlapping Cosmic Tail keeps every line while releasing only the needed lower-to-upper crossing");
                floor.OnCastFinished(player, tail);
                foreach (var (helper, cast) in helpers.Where(h => h.Cast.Action.ID == 49126)) lines.OnCastFinished(helper, cast);
                var second = Read();
                lines.AddAIHints(0, player, default, second);
                Check(second.ForbiddenZones.Count == 6 && second.ForbiddenZones.Any(z => z.activation <= Now && z.shapeDistance.Contains(ShinryuPad)),
                    "second Starflare wave retains the transition guard with the alternate five lines");
                foreach (var (helper, cast) in helpers.Where(h => h.Cast.Action.ID == 49127)) lines.OnCastFinished(helper, cast);
                Check(Read().ForbiddenZones.Count == 1 && lines.Casters.Count == 0, "line cleanup cannot remove the floor guard");
            }
            module.Arena.Bounds = new ArenaBoundsRect(30, 20);
            Check(Read().ForbiddenZones.Count == 0, "single-platform P2 has no phantom hole or teleporter restriction");
            typeof(BossModule).GetMethod("DeactivateComponent")!.MakeGenericMethod(floor.GetType()).Invoke(module, null);
            var fresh = Component<GenericAOEs>(module, ShinryuPrefix + "FloorAOEs");
            var freshHints = new AIHints();
            fresh.AddAIHints(0, player, default, freshHints);
            Check(freshHints.ForbiddenZones.Count == 0, "reactivation on P2 cannot recreate a transition guard");
        }
    }
}
