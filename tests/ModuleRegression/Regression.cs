using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using BossMod;
using BossMod.Components;

static partial class Regression
{
    private static readonly Assembly Bmr = typeof(BossModule).Assembly;
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        ++assertions;
    }
    private static void Set(object instance, string name, object? value)
    {
        for (var type = instance.GetType(); type != null; type = type.BaseType)
            if (type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is { } field)
            {
                field.SetValue(instance, value);
                return;
            }
        throw new MissingFieldException(name);
    }
    private static T Component<T>(BossModule module, string name) where T : BossComponent
    {
        var type = Bmr.GetType("BossMod." + name, true)!;
        typeof(BossModule).GetMethod("ActivateComponent")!.MakeGenericMethod(type).Invoke(module, null);
        var component = (T)module.Components.Single(component => component.GetType() == type);
        Check(module.Components.Contains(component), name + " generated factory activates its component");
        return component;
    }
    private static BossModule Module(string name, WPos center)
    {
        var module = (BossModule)RuntimeHelpers.GetUninitializedObject(Bmr.GetType("BossMod." + name, true)!);
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        Set(module, "WorldState", world);
        Set(module, "Arena", new MiniArena(center, new ArenaBoundsCircle(30)));
        Set(module, "Components", new List<BossComponent>());
        return module;
    }
    private static Actor Actor(ulong id, uint oid, WPos pos, bool player = false)
        => new(id, oid, 0, 0, "", 0, player ? ActorType.Player : ActorType.Enemy, Class.None, 100,
            new Vector4(pos.X, 0, pos.Z, 0), hpmp: new(100, 100, 0, 100, 100));
    private static ActorCastInfo Cast(uint id, WPos pos)
        => new() { Action = new ActionID(ActionType.Spell, id), Location = new(pos.X, 0, pos.Z), TotalTime = 10 };

    private static ActorCastEvent Effect(uint id) => new(new ActionID(ActionType.Spell, id), 0, 0, 0, default, 0, 0, default);

    public static void Run(string gameDirectory)
    {
        // The cases below are synthetic callback/lifecycle regressions, not physical evidence.
        // Config initialization supplies native arena/config defaults without a live plugin.
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"), new Lumina.LuminaOptions());
        Service.LuminaGameData = game;
        Service.Config.Initialize();
        List<string> failures = [];
        foreach (var test in new Action[] { Elm, Queen, Acclaim, Book, Windpipe, Roulette, ShinryuDirections, ShinryuResolution, ShinryuAssignments, ShinryuBootstrap, PromathiaGenesis, PromathiaKnockback, PromathiaRingDeadlines, PromathiaTowerPuddles, PromathiaIslandSupport, PromathiaLinkedPuddles, PromathiaRebirthKnockback, SerpentLabyrinth, AlZahbiKnockback, AlZahbiExaflares, AlZahbiWave1, CleaveReach, CleaveAim, CleaveRecipients, CleavePetsAndLifecycle, CleaveOtherHazards, CleaveVictimGeometry })
        {
            try { test(); }
            catch (Exception error) { failures.Add(test.Method.Name + ": " + error); }
        }
        if (failures.Count != 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
        Console.WriteLine($"{assertions} native module assertions passed (synthetic; no live game actions).");
    }
    private static void Elm()
    {
        const string prefix = "Dawntrail.Foray.CriticalEngagement.CE208FamiliarTactics.";
        var module = Module(prefix + "CE208FamiliarTactics", new(-390, 700));
        var component = Component<GenericAOEs>(module, prefix + "UnbowedSpirit");
        var first = Actor(1, 0x4BDA, new(-380, 700));
        var second = Actor(2, 0x4BDA, new(-370, 700));
        component.OnActorCreated(first);
        component.OnActorCreated(second);
        component.OnActorCreated(second);
        component.Update();
        Check(component.ActiveAOEs(0, first).Length == 2, "duplicate actor creation cannot duplicate a puddle");
        component.OnEventCast(second, Effect(47534));
        component.Update();
        Check(component.ActiveAOEs(0, first) is var aoes && aoes.Length == 1 && aoes[0].Origin == first.Position,
            "second puddle resolving first leaves the first puddle warning");
        component.OnEventCast(second, Effect(47534));
        component.Update();
        Check(component.ActiveAOEs(0, first).Length == 1, "duplicate effect cannot consume another puddle");
        component.OnActorDestroyed(first);
        component.Update();
        Check(component.ActiveAOEs(0, first).Length == 0, "despawn removes its moving warning");
    }
    private static void Queen()
    {
        const string prefix = "Dawntrail.Trial.T03QueenEternal.";
        var module = Module(prefix + "T03QueenEternal", new(100, 92.5f));
        Component<GenericAOEs>(module, prefix + "Besiegement");
        var component = Component<GenericAOEs>(module, prefix + "LegitimateForce");
        var actor = Actor(1, 0, new(100, 92.5f), true);
        var type = Bmr.GetType("BossMod." + prefix + "T03QueenEternal", true)!;
        var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        module.Arena.Bounds = (ArenaBounds)type.GetMethod("GetXArena", flags)!.Invoke(null, null)!;
        var hints = new AIHints();
        component.AddAIHints(0, actor, default, hints);
        Check(hints.ForbiddenZones.Count == 0, "idle X arena has no split-platform restriction");
        module.Arena.Center = new(100, 94);
        module.Arena.Bounds = (ArenaBounds)type.GetMethod("GetSplitArena", flags)!.Invoke(null, null)!;
        component.AddAIHints(0, actor, default, hints);
        Check(hints.ForbiddenZones.Count == 1, "split arena retains its platform restriction");
    }
    private static void Acclaim()
    {
        const string prefix = "Dawntrail.Foray.CriticalEngagement.CE213QuarriedAway.";
        var module = Module(prefix + "CE213QuarriedAway", new(-519, -641));
        var component = Component<GenericAOEs>(module, prefix + "Acclaim");
        var visual = Actor(1, 0x4BBF, new(-529, -641));
        var turner = Actor(2, 0x4EBD, visual.Position);
        component.OnCastStarted(visual, Cast(47157, visual.Position));
        component.OnCastStarted(visual, Cast(47157, visual.Position));
        Check(component.ActiveAOEs(0, visual).Length == 1, "initial cone is visible before its arrow status");
        var status = new ActorStatus(2056, 0x43B, Now.AddSeconds(13), 0);
        component.OnStatusGain(turner, ref status);
        component.Update();
        component.OnStatusGain(turner, ref status);
        component.Update();
        var pending = (ICollection)component.GetType().GetField("aoes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(component)!;
        Check(pending.Count == 4, "status refresh cannot expand forecasts again");
        for (var i = 0; i < 4; ++i)
            component.OnEventCast(visual, Effect(i == 0 ? 47157u : 47158u));
        Check(component.ActiveAOEs(0, visual).Length == 0, "wave resolves completely");
        component.OnCastStarted(visual, Cast(47157, visual.Position));
        component.OnStatusGain(turner, ref status);
        component.Update();
        Check(pending.Count == 4, "a fresh wave can reuse the same golems");
    }
    private static void Book()
    {
        const string prefix = "Dawntrail.Foray.CriticalEngagement.CE209ForbiddenFolios.";
        var module = Module(prefix + "CE209ForbiddenFolios", new(659, 659));
        var component = Component<CastTowersOpenWorld>(module, prefix + "BookDrop");
        var alive = Actor(1, 0, new(659, 659), true);
        var dead = Actor(2, 0, new(659, 659), true);
        dead.IsDead = true;
        module.WorldState.Actors.Actors.Add(1, alive);
        module.WorldState.Actors.Actors.Add(2, dead);
        component.OnCastStarted(Actor(3, 0x4BD8, new(659, 659)), Cast(47322, new(659, 659)));
        component.Update();
        Check(component.Towers[0].NumInside(module) == 1, "dead occupants do not count");
        var arrival = Actor(4, 0, new(659, 659), true);
        module.WorldState.Actors.Actors.Add(4, arrival);
        component.Update();
        Check(component.Towers[0].NumInside(module) == 2, "arrivals are included after the first query");
        alive.HPMP.CurHP = 0;
        component.Update();
        Check(component.Towers[0].NumInside(module) == 1, "zero-HP occupants do not count before the dead flag");
        Check(component.Towers[0].MinSoakers == 3 && component.Towers[0].MaxSoakers == 5,
            "existing minimum and capacity are preserved");
    }
    private static void Windpipe()
    {
        // CarGeometry reconstructs the current car from the primary actor and
        // enemy inventory; use the real initialized module rather than a partial
        // uninitialized-object fixture that has neither contract.
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        using var module = new BossMod.Dawntrail.Trial.T07Doomtrain.T07Doomtrain(world, Actor(99, (uint)BossMod.Dawntrail.Trial.T07Doomtrain.OID.Doomtrain, new(100, 75)));
        module.StateMachine.Start(Now);
        var geometry = module.Components.Single(c => c.GetType().Name == "CarGeometry");
        var component = (SimpleKnockbacks)module.Components.Single(c => c.GetType().Name == "WindpipeDrawIn");
        Check(geometry != null, "Doomtrain geometry activates through the generated state machine");
        Check(component != null, "Doomtrain draw-in activates through the generated state machine");
        var caster = Actor(1, 0x233C, new(100, 85));
        var player = Actor(2, 0, new(100, 100), true);
        foreach (var car in new[] { 1, 2, 5 })
        {
            geometry.GetType().GetMethod("SetCar")!.Invoke(geometry, [car]);
            var cast = Cast(45667, caster.Position);
            component.OnCastStarted(caster, cast);
            Check(component.ActiveKnockbacks(7, player).Length == 1, "all slots see a single draw-in source");
            component.AddAIHints(7, player, default, new AIHints());
            component.OnCastFinished(caster, cast);
            Check(component.ActiveKnockbacks(7, player).Length == 0, "cast finish removes its source");
        }
    }
    private static void Roulette()
    {
        const string prefix = "Dawntrail.Foray.CriticalEngagement.CE204AppallingBehavior.";
        var module = Module(prefix + "CE204AppallingBehavior", new(807, -562));
        var component = Component<GenericAOEs>(module, prefix + "Roulette");
        var caster = Actor(1, 0x4D8F, new(807, -562));
        component.OnCastFinished(caster, Cast(49787, caster.Position));
        var aoes = component.ActiveAOEs(0, caster);
        Check(aoes.Length == 1 && aoes[0].Activation == Now.AddSeconds(14.3),
            "circle forecast uses the recorded visual-to-effect interval");
        Check(aoes[0].Shape is AOEShapeCircle { Radius: 5 }, "circle size is unchanged");
        component.OnEventCast(caster, Effect(49788));
        Check(component.ActiveAOEs(0, caster).Length == 0, "circle effect still clears Roulette");
    }

}
