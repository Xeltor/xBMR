using System.Numerics;
using BossMod;
using BossMod.Components;
using BossMod.RealmReborn.Quest.Job.Dragoon.IntoTheDragonsMaw;

static class Checks
{
    private static int assertions;
    private static readonly DateTime Now = new(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        ++assertions;
    }
    private static Actor Create(WorldState ws, ulong id, uint oid, WPos pos, bool ally = false, bool targetable = true)
    {
        ws.Execute(new ActorState.OpCreate(id, oid, (int)id, 0, "", 0, oid == (uint)OID.Fire ? ActorType.EventObj : ActorType.Enemy,
            Class.None, 50, new Vector4(pos.X, 359.1f, pos.Z, 0), .55f, new(100, 100, 0, 0, 0), targetable, ally, 0, 0, 0));
        return ws.Actors.Find(id)!;
    }
    private static ActorCastInfo Cast(WorldState ws, Actor boss, uint aid, WPos pos)
    {
        ws.Execute(new ActorState.OpCastInfo(boss.InstanceID, new ActorCastInfo
        {
            Action = new(ActionType.Spell, aid), Location = new(pos.X, 359.1f, pos.Z), TotalTime = 2.2f
        }));
        return boss.CastInfo!;
    }
    private static GenericAOEs Fire(BossModule module) => module.Components.OfType<GenericAOEs>().Single(c => c.GetType().Name == "Fire");
    private static void Hit(WorldState ws, Actor boss, WPos pos)
        => ws.Execute(new ActorState.OpCastEvent(boss.InstanceID, new(new(ActionType.Spell, (uint)AID.SkydragonDive), 0, 0, 0, new(pos.X, 359.1f, pos.Z), 1, 0, default)));

    public static void Run(string gameDirectory)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"));
        Service.LuminaGameData = game;
        Service.Config.Initialize();
        var ws = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1), CurrentZone = 316, CurrentCFCID = 365 };
        var center = new WPos(265, -680);
        var boss = Create(ws, 1, (uint)OID.Boss, center);
        var oldFire = Create(ws, 2, (uint)OID.Fire, center, ally: true, targetable: false);
        oldFire.EventState = 7;
        boss.InCombat = true;
        Cast(ws, boss, (uint)AID.Jump, center);
        using var module = new IntoTheDragonsMaw(ws, boss);
        module.Update();
        Check(BossModuleRegistry.FindByOID((uint)OID.Boss)?.ModuleType == module.GetType(), "generated registry includes Estinien");
        Check(module.Components.Count == 4, "generated phase activates all components");
        var jump = module.Components.OfType<SimpleAOEs>().Single(c => c.WatchedAction == (uint)AID.Jump);
        Check(jump.Casters.Count == 1, "phase initializes an already active cast");
        Check(Math.Abs((jump.Casters[0].Activation - Now).TotalSeconds - 2.5) < .001, "NPC correction contributes to the avoidance deadline");
        Check(Fire(module).ActiveAOEs(0, boss).Length == 0, "hidden initial fire object is excluded");
        ws.Execute(new ActorState.OpCastInfo(boss.InstanceID, null));
        Check(jump.Casters.Count == 0, "cast cancellation clears warning");
        foreach (var (aid, radius) in new[] { ((uint)AID.Jump, 3f), ((uint)AID.SkydragonDive, 5f) })
        {
            Cast(ws, boss, aid, center);
            var aoe = module.Components.OfType<SimpleAOEs>().Single(c => c.WatchedAction == aid).Casters.Single();
            Check(aoe.Check(center + new WDir(radius - .05f, 0)), "nominal circle includes inner boundary probe");
            Check(!aoe.Check(center + new WDir(radius + .05f, 0)), "nominal circle excludes outer boundary probe");
            ws.Execute(new ActorState.OpCastInfo(boss.InstanceID, null));
            Check(Fire(module).ActiveAOEs(0, boss).Length == 0, "interrupted cast creates no fire prediction");
        }
        Cast(ws, boss, (uint)AID.Heartstopper, center);
        var rectangle = module.Components.OfType<SimpleAOEs>().Single(c => c.WatchedAction == (uint)AID.Heartstopper).Casters.Single();
        Check(rectangle.Check(center + new WDir(1.49f, 3.54f)), "Heartstopper uses nominal front and half width");
        Check(!rectangle.Check(center + new WDir(1.51f, 3f)) && !rectangle.Check(center + new WDir(0, -.1f)), "Heartstopper preserves side and rear safe space");
        ws.Execute(new ActorState.OpCastInfo(boss.InstanceID, null));

        oldFire.EventState = 1;
        var next = center + new WDir(1, 0);
        Hit(ws, boss, next);
        module.Update();
        Check(Fire(module).ActiveAOEs(0, boss).Length == 2, "older nearby fire cannot consume a new prediction");
        Check(Fire(module).ActiveAOEs(0, boss).ToArray().Any(a => a.Check(center + new WDir(5.5f, 0))), "new fringe remains covered before object spawn");
        var freshFire = Create(ws, 3, (uint)OID.Fire, next, ally: true, targetable: false);
        module.Update();
        Check(Fire(module).ActiveAOEs(0, boss).Length == 2, "new object replaces only its own prediction");
        oldFire.EventState = 7;
        Check(Fire(module).ActiveAOEs(0, boss).Length == 1, "individual deactivation preserves other fire");
        ws.Execute(new ActorState.OpDestroy(freshFire.InstanceID));
        Check(Fire(module).ActiveAOEs(0, boss).Length == 0, "object destruction clears its hazard");
        var recreated = Create(ws, 3, (uint)OID.Fire, next, ally: true, targetable: false);
        Check(Fire(module).ActiveAOEs(0, boss).Length == 1, "recreated object has its own lifetime");
        recreated.EventState = 7;

        var eye = Create(ws, 4, (uint)OID.DragonsEye, center);
        var friendly = Create(ws, 5, (uint)OID.DragonsEye, center, ally: true);
        var hidden = Create(ws, 6, (uint)OID.DragonsEye, center, targetable: false);
        var dead = Create(ws, 7, (uint)OID.DragonsEye, center); dead.IsDead = true;
        var unrelated = Create(ws, 8, 0x1234, center);
        var hints = new AIHints();
        foreach (var a in new[] { boss, eye, friendly, hidden, dead, unrelated }) hints.PotentialTargets.Add(new(a, 9, false));
        module.CalculateAIHints(0, boss, default, hints);
        Check(hints.PotentialTargets[0].Priority == 0 && hints.PotentialTargets[1].Priority == 1, "eligible eye outranks boss");
        Check(hints.PotentialTargets.Skip(2).All(t => t.Priority == 9), "friendly, hidden, dead and unrelated inputs retain priority");
        hints.PotentialTargets[1].Priority = -1;
        module.CalculateAIHints(0, boss, default, hints);
        Check(hints.PotentialTargets[1].Priority == -1, "negative priority remains authoritative");
        Hit(ws, boss, center);
        ws.Execute(new ActorState.OpDead(boss.InstanceID, true));
        module.Update();
        Check(module.Components.Count == 0, "encounter completion clears pending and active components");
        boss.IsDead = false; boss.CastInfo = null;
        using var retry = new IntoTheDragonsMaw(ws, boss); retry.Update();
        Check(Fire(retry).ActiveAOEs(0, boss).Length == 0, "fresh module does not inherit pending fire");
        Console.WriteLine($"Into the Dragon's Maw synthetic regression: {assertions} checks passed; physical geometry and live play unverified.");
    }
}
