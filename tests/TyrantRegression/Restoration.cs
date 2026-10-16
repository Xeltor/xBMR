using BossMod;

static partial class Checks
{
    private static void Restoration()
    {
        foreach (var side in new[] { -1, 1 })
        {
            using var m = Module();
            Director(m, 2);
            Director(m, side < 0 ? 16u : 8u);
            // October 15: WHM survived west, then fell at restoration from (75.956, 89.545).
            var p = Player(m, 1, new(side < 0 ? 75.956154f : 124.043846f, 89.544624f));
            Start(m, m.PrimaryActor, 46006, 4.7f);
            var arena = Component<BossComponent>(m, "ArenaChanges");
            var h = Hints(arena, p);
            Check(Forbidden(h, p.Position), "restoring Crown warns before the outer floor disappears");
            Check(!Forbidden(h, new(side < 0 ? 84 : 116, 90)), "restoration keeps the shared old/new floor safe");
            Check(h.ForbiddenZones.All(z => Math.Abs((z.activation - Now).TotalSeconds - 4.7) < 0.001), "restoration uses an early conservative deadline, not the later director update");
            Check(m.Arena.InBounds(p.Position) && !m.Arena.InBounds(new(100, 100)), "warning does not prematurely restore the gap or arena bounds");
            h.PathfindMapBounds = m.Bounds; h.PathfindMapCenter = m.Center; h.Normalize();
            var decision = BossMod.Pathfinding.NavigationDecision.Build(new(), Now, h, p);
            Check(decision.Destination is { } d && !Forbidden(h, d) && m.Arena.InBounds(d) && Math.Sign(d.X - 100) == side,
                "native movement reaches inward safety on the surviving island without crossing the gap");
            Check(decision.TimeToGoal < 3.7f, "restoration route fits conservative deadline with native reserve");
            Finish(m, m.PrimaryActor);
            Check(Forbidden(Hints(arena, p), p.Position), "cast finish retains the warning until actual floor restoration");
            Director(m, 1);
            Check(Hints(arena, p).ForbiddenZones.Count == 0 && m.Arena.InBounds(new(100, 100)), "director restoration clears the prediction and restores the floor");

            Director(m, 2); Director(m, side < 0 ? 16u : 8u);
            Start(m, m.PrimaryActor, 46006, 5f);
            m.WorldState.Execute(new ActorState.OpCastInfo(m.PrimaryActor.InstanceID, null));
            Check(Hints(arena, p).ForbiddenZones.Count == 0, "cancelled Crown clears the restoration prediction");
            Start(m, m.PrimaryActor, 46006, 5f);
            Director(m, 0);
            Check(Hints(arena, p).ForbiddenZones.Count == 0, "reset clears pending restoration");
        }
        using (var m = Module())
        {
            var p = Player(m, 1, new(82, 90));
            Start(m, m.PrimaryActor, 46006, 5f);
            Check(Hints(Component<BossComponent>(m, "ArenaChanges"), p).ForbiddenZones.Count == 0,
                "ordinary Crown on the full arena creates no restoration movement");
        }
    }
}
