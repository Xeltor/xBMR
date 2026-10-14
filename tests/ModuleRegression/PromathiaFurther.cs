using System.Numerics;
using BossMod;
using BossMod.Components;

static partial class Regression
{
    private static void PromathiaRingDeadlines()
    {
        foreach (var (name, visual, hit) in new[] { ("WheelofImpregnability", 50321u, 50323u), ("BastionOfTwilight", 50322u, 50324u) })
        {
            var module = Module(PromathiaPrefix + "A34Promathia", new(-820, -820));
            var boss = Actor(100, 0x4DEE, new(-820, -820));
            Set(module, "PrimaryActor", boss);
            var actor = Actor(1, 0, new(-810, -820), true);
            var component = Component<GenericAOEs>(module, PromathiaPrefix + name);
            var cast = Cast(visual, boss.Position);
            cast.TotalTime = 1.7f;
            cast.ElapsedTime = 0.02f;
            component.OnCastStarted(boss, cast);
            var initial = component.ActiveAOEs(0, actor).ToArray().Single();
            Check(initial.Activation == module.CastFinishAt(cast, 10.6), "ring forecast is bound to its visual cast");
            module.WorldState.Frame = new(Now.AddSeconds(10), 0, 0, 0, 0, 1);
            component.OnCastStarted(boss, cast);
            var later = component.ActiveAOEs(0, actor).ToArray().Single();
            Check(later.Activation == initial.Activation && later.Origin == initial.Origin,
                "forecast does not drift or move on repeated reads and duplicate starts");
            var hints = new AIHints();
            component.AddAIHints(0, actor, default, hints);
            Check(hints.ForbiddenZones.Count == 1 && hints.ForbiddenZones[0].activation == initial.Activation,
                "the AI receives the same fixed ring deadline as the drawing");
            cast.EventHappened = true;
            component.OnCastFinished(boss, cast);
            Check(component.ActiveAOEs(0, actor).Length == 1, "visual finish does not retire the later ring hit");
            component.OnEventCast(boss, Effect(hit));
            Check(component.ActiveAOEs(0, actor).Length == 0 && component.NumCasts == 1, "each ring clears on its own helper hit");
            var cancelled = Cast(visual, boss.Position);
            component.OnCastStarted(boss, cancelled);
            component.OnCastFinished(boss, cancelled);
            Check(component.ActiveAOEs(0, actor).Length == 0, "interrupted visual does not leave a phantom ring");
            component.OnCastStarted(boss, Cast(visual, boss.Position));
            component.OnCastFinished(boss, cancelled);
            Check(component.ActiveAOEs(0, actor).Length == 1, "old finish cannot cancel a newer ring");
        }
    }

    private static void PromathiaTowerPuddles()
    {
        var module = Module(PromathiaPrefix + "A34Promathia", new(-820, -820));
        var boss = Actor(100, 0x4DEE, new(-820, -820));
        Set(module, "PrimaryActor", boss);
        var actor = Actor(1, 0, new(-820, -835), true);
        module.WorldState.Party.Members[0] = new(0, actor.InstanceID, false);
        var party = new Actor?[PartyState.MaxAllies];
        party[0] = actor;
        Set(module.WorldState.Party, "_actors", party);
        var towers = Component<CastTowers>(module, PromathiaPrefix + "InfernalDeliveranceTower");
        var puddles = Component<GenericAOEs>(module, PromathiaPrefix + "EmptySalvationPuddles");
        var salvation = Cast(50317, boss.Position);
        salvation.TotalTime = 4.7f;
        boss.CastInfo = salvation;
        puddles.OnCastStarted(boss, salvation);
        var pools = Enumerable.Range(0, 6).Select(i => Actor((ulong)(200 + i), 0x1EC097,
            boss.Position + (i * 60f).Degrees().ToDirection() * 15)).ToArray();
        foreach (var pool in pools)
        {
            puddles.OnActorCreated(pool);
            puddles.OnActorCreated(pool);
        }
        puddles.OnActorCreated(Actor(210, 0x1EC097, boss.Position));
        var predicted = puddles.ActiveAOEs(0, actor).ToArray();
        Check(predicted.Length == 6, "six outer pools are tracked once, without the central link visual");
        Check(predicted.All(a => a.Activation == module.CastFinishAt(salvation, 0.8)), "pool spawn warning has an event-derived forecast");
        puddles.OnCastStarted(boss, salvation);
        Check(puddles.ActiveAOEs(0, actor).Length == 6, "duplicate Salvation start cannot discard existing pool objects");
        salvation.EventHappened = true;
        puddles.OnCastFinished(boss, salvation);
        foreach (var pool in pools) puddles.OnActorEAnim(pool, 0x00010002);
        Check(puddles.ActiveAOEs(0, actor).ToArray().All(a => a.Activation == Now && a.Risky), "active pools are immediately risky");
        var hints = new AIHints();
        puddles.AddAIHints(0, actor, default, hints);
        Check(hints.ForbiddenZones.Count == 6, "ordinary navigation avoids all six Heavy puddles");
        var selectedPool = pools[3];
        var first = Actor(300, 0x233C, selectedPool.Position);
        var second = Actor(301, 0x233C, pools[1].Position);
        var firstCast = Cast(50335, first.Position);
        firstCast.TotalTime = 6.7f;
        towers.OnCastStarted(first, firstCast);
        towers.OnCastStarted(first, firstCast);
        towers.OnCastStarted(second, Cast(50335, second.Position));
        Check(towers.Towers.Count == 2, "tower sources are idempotent and independent");
        var soakHints = new AIHints();
        towers.AddAIHints(0, actor, default, soakHints);
        Check(soakHints.ForbiddenZones.Any(z => z.activation == default && !z.shapeDistance.Contains(first.Position)
            && z.shapeDistance.Contains(boss.Position) && z.shapeDistance.Contains(second.Position)),
            "once inside, the actor must remain in its particular tower immediately");
        var soakerAOEs = puddles.ActiveAOEs(0, actor).ToArray();
        Check(soakerAOEs.Length == 6 && soakerAOEs.Count(a => !a.Risky) == 1,
            "required soak exempts exactly its own pool while keeping all puddles drawn");
        hints = new();
        puddles.AddAIHints(0, actor, default, hints);
        Check(hints.ForbiddenZones.Count == 5, "other puddles stay forbidden during a tower soak");
        actor.PosRot = new Vector4(-820, -900, -830, 0);
        soakHints = new();
        towers.AddAIHints(0, actor, default, soakHints);
        Check(soakHints.ForbiddenZones.Any(z => z.activation == default && z.shapeDistance.Contains(actor.Position)),
            "an uptime detour cannot release a committed tower assignment");
        firstCast.EventHappened = true;
        towers.OnEventCast(first, Effect(50335));
        towers.OnCastFinished(first, firstCast);
        Check(towers.Towers.Count == 1, "one tower resolving leaves its neighbour active");
        towers.OnActorDestroyed(second);
        Check(towers.Towers.Count == 0, "destroyed source clears only its remaining tower");
        puddles.OnActorEAnim(selectedPool, 0x04000800);
        var expanded = puddles.ActiveAOEs(0, actor).ToArray().Single(a => a.ActorID == selectedPool.InstanceID);
        Check(expanded.Risky && expanded.Shape is AOEShapeCircle { Radius: 8 }, "soak exception ends before the expanded follow-up");
        hints = new();
        puddles.AddAIHints(0, actor, default, hints);
        Check(hints.ForbiddenZones.Any(z => z.shapeDistance.Contains(first.Position + new WDir(7, 0))), "expanded pool rejects an otherwise tempting exit point");
        puddles.OnActorEAnim(selectedPool, 0x00011000);
        var small = puddles.ActiveAOEs(0, actor).ToArray().Single(a => a.ActorID == selectedPool.InstanceID);
        Check(small.Risky && small.Shape is AOEShapeCircle { Radius: 4 }, "small pool returns after the follow-up");
        puddles.OnActorEAnim(selectedPool, 0x00800100);
        Check(puddles.ActiveAOEs(0, actor).Length == 6, "link animation does not erase a persistent pool");
        puddles.OnActorEAnim(selectedPool, 0x00040008);
        Check(puddles.ActiveAOEs(0, actor).Length == 5, "fade event removes a puddle before object destruction");
        puddles.OnActorDestroyed(selectedPool);
        Check(puddles.ActiveAOEs(0, actor).Length == 5, "later destruction is idempotent");
        var replacement = Actor(pools[0].InstanceID, 0x1EC097, pools[0].Position);
        puddles.OnActorCreated(replacement);
        puddles.OnActorDestroyed(pools[0]);
        Check(puddles.ActiveAOEs(0, actor).Length == 5, "stale destruction cannot delete a recreated pool");
        puddles.OnCastStarted(boss, Cast(50343, boss.Position));
        Check(puddles.ActiveAOEs(0, actor).Length == 0, "False Genesis clears the old arena's pool generation");
    }
}
