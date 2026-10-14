using System.Numerics;
using BossMod;
using BossMod.Pathfinding;

static partial class Regression
{
    private static void PromathiaIslandSupport()
    {
        var module = Module(PromathiaPrefix + "A34Promathia", new(-820, -820));
        Set(module, "RelevantEnemies", new Dictionary<uint, List<Actor>>());
        var adds = PromathiaPlatforms.Select((p, i) => Actor((ulong)(200 + i), 0x4DF0, p.Center)).ToArray();
        for (var i = 0; i < adds.Length; ++i)
        {
            adds[i].IsTargetable = true;
            Set(adds[i], "SpawnIndex", i * 2);
        }
        module.RelevantEnemies[0x4DF0] = [.. adds];
        var component = Component<BossComponent>(module, PromathiaPrefix + "IslandSupport");
        var changes = Component<BossComponent>(module, PromathiaPrefix + "ArenaChanges");
        var boss = Actor(100, 0x4DEE, new(-820, -820));
        var player = Actor(1, 0, adds[0].Position, true);
        var hints = new AIHints();
        component.AddAIHints(0, player, default, hints);
        Check(hints.Teleporters.Count == 0 && hints.GoalZones.Count == 0, "ordinary arena has no island support routing");
        component.OnEventCast(boss, Effect(50343));
        changes.OnEventCast(boss, Effect(50343));
        AIHints Read()
        {
            var result = new AIHints { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
            foreach (var add in adds)
            {
                var enemy = new AIHints.Enemy(add, 0, false);
                result.PotentialTargets.Add(enemy);
                result.Enemies[add.CharacterSpawnIndex] = enemy;
            }
            component.AddAIHints(0, player, default, result);
            result.Normalize();
            return result;
        }
        component.OnMapEffect(0x5F, 0x00020001);
        component.OnMapEffect(0x60, 0x00020001);
        hints = Read();
        Check(hints.Teleporters.Count == 0 && hints.FindEnemy(adds[0])!.Priority == 1,
            "live local add wins even if an early pad signal is received");
        adds[0].IsDead = true;
        component.OnMapEffect(0x60, 0x00080004);
        Check(Read().Teleporters.Count == 0, "half an activation pair cannot guess a pad route");
        component.OnMapEffect(0x60, 0x00020001);
        component.OnMapEffect(0x60, 0x00020001);
        hints = Read();
        Check(hints.Teleporters.Count == 2 && hints.Teleporters.All(t => !t.Bidirectional),
            "cleared northern island exposes two directed transfers to live adds exactly once");
        Check(hints.GoalZones.Count == 1 && hints.ForbidDashes && hints.GoalZones[0](adds[1].Position) > 0,
            "support goal prefers a living destination without requesting a dash over the gap");
        Check(hints.ForbiddenZones.Count == 0 && !module.Arena.InBounds(new(-820, -820)),
            "support is a preference and does not relax disconnected arena bounds");
        var context = new NavigationDecision.Context();
        var decision = NavigationDecision.Build(context, Now, hints, player);
        Check(decision.Destination is { } approach && approach.InCircle(new(-816, -812), 1.5f)
            && module.Arena.InBounds(approach), "native navigation first approaches the pad on its own island");
        Check(context.Map.HasTeleporters && decision.TimeToGoal > 0.5f,
            "native navigation consumes the directed edge and its transfer duration");
        player.PosRot = new Vector4(-816, -900, -812, 0);
        hints = Read();
        Check(hints.Teleporters.Count == 0 && hints.GoalZones[0](player.Position) > 0
            && hints.GoalZones[0](adds[1].Position) == 0, "standing on the pad waits there instead of walking toward its remote exit");
        decision = NavigationDecision.Build(new(), Now, hints, player);
        Check(decision.Destination == null || decision.Destination.Value.InCircle(player.Position, 1f),
            "native movement remains at the entrance while waiting for transfer");
        player.Statuses[0] = new(2578, 0, Now.AddSeconds(4), 0);
        hints = Read();
        Check(hints.Teleporters.Count == 0 && hints.GoalZones.Count == 0,
            "observed System Lock suppresses support routing without an assumed three-second expiry");
        Check(hints.ForbiddenZones.Count == 2 && hints.ForbiddenZones.Any(z => z.shapeDistance.Contains(player.Position)),
            "System Lock explicitly moves off the physical pad before it can retrigger");
        decision = NavigationDecision.Build(new(), Now, hints, player);
        Check(decision.Destination is { } leave && !leave.InCircle(player.Position, 1f) && module.Arena.InBounds(leave),
            "native navigation leaves the pad during Lock without leaving its island");
        player.Statuses[0] = default;
        player.PosRot = new Vector4(-820, -900, -807, 0);
        adds[1].IsDead = true;
        hints = Read();
        Check(hints.Teleporters.Count == 1 && hints.GoalZones[0](adds[2].Position) > 0,
            "destination death selects the remaining live island");
        component.OnMapEffect(0x0D, 0x00020001);
        component.OnMapEffect(0x0E, 0x00020001);
        Check(Read().Teleporters.Count == 1, "extra enabled islands cannot introduce a cooldown-free second hop");
        player.PosRot = new Vector4(adds[2].Position.X, -900, adds[2].Position.Z, 0);
        hints = Read();
        Check(hints.Teleporters.Count == 0 && hints.GoalZones.Count == 0 && hints.FindEnemy(adds[2])!.Priority == 1,
            "arrival resumes the local live add instead of bouncing back");
        adds[2].IsDead = true;
        var text = new BossComponent.TextHints();
        component.AddHints(0, player, text);
        Check(Read().Teleporters.Count == 0 && text.Count == 0, "no support request remains after all adds die");
        player.PosRot = new Vector4(-828.9281f, -900, -820.5359f, 0);
        hints = Read();
        Check(hints.Teleporters.Count == 0 && hints.GoalZones.Count == 0 && hints.ForbiddenZones.Count == 2
            && hints.ForbiddenZones.Any(z => z.shapeDistance.Contains(player.Position)),
            "all-cleared wait avoids both still-active pads even without System Lock");
        decision = NavigationDecision.Build(new(), Now, hints, player);
        Check(decision.Destination is { } wait && !wait.InCircle(player.Position, 1f) && module.Arena.InBounds(wait),
            "native all-cleared wait steps off the pad instead of passively repeating transfers");
        adds[2].IsDead = false;
        player.PosRot = new Vector4(-820, -900, -807, 0);
        component.OnMapEffect(0x0C, 0x00080004);
        Check(Read().Teleporters.Count == 0 && Read().GoalZones.Count == 0, "restored arena retires all transfer state");
        Check(Read().ForbiddenZones.Count == 0, "arena restore also removes physical pad avoidance");
        component.OnEventCast(boss, Effect(50343));
        Check(Read().Teleporters.Count == 0, "a new island phase cannot reuse previously enabled pads");
        component.OnMapEffect(0x5F, 0x00020001);
        component.OnMapEffect(0x60, 0x00020001);
        component.OnCastStarted(boss, Cast(50343, boss.Position));
        Check(Read().Teleporters.Count == 0, "replacement or cancelled Genesis keeps support inactive until successful resolution");
        // Independently listed layout positions cover all six directed pairs.
        foreach (var (source, destination, entrance, exit) in new[]
        {
            (0, 1, new WPos(-816, -812), new WPos(-811.0718f, -820.5359f)),
            (0, 2, new WPos(-824, -812), new WPos(-828.9281f, -820.5359f)),
            (1, 0, new WPos(-811.0718f, -820.5359f), new WPos(-816, -812)),
            (1, 2, new WPos(-815.0718f, -827.4641f), new WPos(-824.9281f, -827.4641f)),
            (2, 0, new WPos(-828.9281f, -820.5359f), new WPos(-824, -812)),
            (2, 1, new WPos(-824.9281f, -827.4641f), new WPos(-815.0718f, -827.4641f))
        })
        {
            component.OnCastStarted(boss, Cast(50343, boss.Position));
            component.OnEventCast(boss, Effect(50343));
            foreach (var index in new byte[] { 0x0D, 0x0E, 0x0F, 0x5E, 0x5F, 0x60 })
                component.OnMapEffect(index, 0x00020001);
            for (var i = 0; i < 3; ++i) adds[i].IsDead = i != destination;
            player.PosRot = new Vector4(adds[source].Position.X, -900, adds[source].Position.Z, 0);
            hints = Read();
            Check(hints.Teleporters.Count == 1 && hints.Teleporters[0].Entrance == entrance && hints.Teleporters[0].Exit == exit,
                "each directed transfer uses its separately listed layout entrance and destination");
            decision = NavigationDecision.Build(new(), Now, hints, player);
            Check(decision.Destination is { } next && next.InCircle(entrance, 1.5f) && module.Arena.InBounds(next),
                "every directed pair approaches the correct pad without walking off its starting island");
        }
    }
}
