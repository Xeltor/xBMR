using System.Numerics;
using BossMod;
using BossMod.Components;
using BossMod.Stormblood.Quest.Job.Dragoon.DarkAsTheNightSky;

static class Checks
{
    private static int _assertions;
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        ++_assertions;
    }

    private static Actor Create(WorldState ws, ulong id, OID oid, WPos pos, bool ally = false, bool targetable = true)
    {
        ws.Execute(new ActorState.OpCreate(id, (uint)oid, (int)id, 0, "", 0, ActorType.Enemy, Class.None, 68,
            new Vector4(pos.X, 69.75f, pos.Z, 0), oid == OID.Boss ? 6.84f : 0.5f,
            new(100, 100, 0, 0, 0), targetable, ally, 0, 0, 0));
        return ws.Actors.Find(id)!;
    }

    public static void Run(string gameDirectory)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"));
        Service.LuminaGameData = game;
        Service.Config.Initialize();
        var ws = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1), CurrentZone = 713, CurrentCFCID = 267 };
        var boss = Create(ws, 1, OID.Boss, new(-340f, -393f), targetable: false);
        var player = Create(ws, 2, (OID)0, new(-336f, -398f), ally: true);
        var orn = Create(ws, 3, OID.OrnKhai, new(-335f, -366f), ally: true, targetable: false);
        var helper = Create(ws, 4, OID.Helper, new(-320f, -363f), targetable: false);
        ws.Execute(new PartyState.OpModify(0, new(0, player.InstanceID, false)));
        boss.InCombat = true;

        ActorCastInfo Cast(AID action, float duration, WPos location, ulong target = 0, Angle rotation = default) => new()
        {
            Action = ActionID.MakeSpell(action), TotalTime = duration, Location = new(location.X, 69.75f, location.Z), TargetID = target, Rotation = rotation
        };
        void Start(Actor actor, ActorCastInfo cast) => ws.Execute(new ActorState.OpCastInfo(actor.InstanceID, cast));
        void End(Actor actor) => ws.Execute(new ActorState.OpCastInfo(actor.InstanceID, null));

        // Opening cast is already running while Faunehm is untargetable.
        Start(boss, Cast(AID.ChaoticVerse, 1.7f, orn.Position, orn.InstanceID));
        using var module = new Faunehm(ws, boss);
        module.Update();
        Check(BossModuleRegistry.FindByOID((uint)OID.Boss)?.ModuleType == typeof(Faunehm), "generated module registration");
        Check(BossModuleRegistry.FindByOID(0x1CDD)?.ModuleType != typeof(Faunehm), "Dragon Sound remains a distinct module");
        Check(module.StateMachine.ActiveState != null && module.Components.Count == 6, "untargetable opening activates generated components");
        var hints = new AIHints();
        module.CalculateAIHints(0, player, default, hints);
        Check(hints.PredictedDamage.Count == 0, "scripted NPC does not become party slot 63");
        End(boss);
        ws.Execute(new ActorState.OpDead(orn.InstanceID, true));
        module.Update();
        Check(module.StateMachine.ActiveState != null, "scripted Orn Khai knockout does not end the fight");

        Start(boss, Cast(AID.AbyssicBuster, 1.7f, boss.Position, boss.InstanceID, 90f.Degrees()));
        var cone = module.Components.OfType<SimpleAOEs>().Single();
        Check(cone.Casters.Count == 1, "cone appears at cast start");
        var aoe = cone.Casters[0];
        Check(Math.Abs((aoe.Activation - Now).TotalSeconds - 2) < 0.001, "native NPC correction preserves two-second warning");
        Check(aoe.Check(boss.Position + new WDir(31.8f, 0)) && !aoe.Check(boss.Position + new WDir(31.9f, 0)), "authored cone radial boundary");
        Check(!aoe.Check(boss.Position + new WDir(-5f, 0)), "opposite side of rotated cone stays available");
        hints = new();
        module.CalculateAIHints(0, player, default, hints);
        Check(hints.ForbiddenZones.Count == 1, "AI receives timed cone");
        End(boss);
        Check(cone.Casters.Count == 0, "cancel clears cone");

        Start(boss, Cast(AID.Comet, 4.7f, boss.Position, boss.InstanceID));
        Check(module.CalculateGlobalHints(player).Count == 1, "Comet warns before its helper starts");
        var impactPosition = new WPos(-342.55f, -382.59f);
        Start(helper, Cast(AID.CometImpact, 5.7f, impactPosition));
        Check(module.CalculateGlobalHints(player).Count == 1, "visual and helper produce one readable warning");
        End(boss);
        var impact = module.Components.OfType<RaidwideCast>().Single(c => c.WatchedAction == (uint)AID.CometImpact);
        Check(impact.Casters.Count == 1, "visual finish retains helper impact");
        hints = new();
        module.CalculateAIHints(0, player, default, hints);
        Check(hints.PredictedDamage.Count == 1 && hints.ForbiddenZones.Count == 0, "Comet predicts damage without inventing a 50y lethal zone");
        End(helper);
        Check(impact.Casters.Count == 0, "helper cancel clears impact warning");

        Start(boss, Cast(AID.ChaoticVerse, 1.7f, player.Position, player.InstanceID));
        hints = new();
        module.CalculateAIHints(0, player, default, hints);
        Check(hints.PredictedDamage.Count == 1, "player-targeted Chaotic Verse predicts damage");
        End(boss);
        Start(boss, Cast(AID.DarkStar, 1.7f, boss.Position, boss.InstanceID));
        hints = new();
        module.CalculateAIHints(0, player, default, hints);
        Check(hints.PredictedDamage.Count == 1, "Dark Star predicts raidwide damage");
        End(boss);

        var left = Create(ws, 5, OID.LeftWingOfTragedy, new(-340f, -392f));
        var right = Create(ws, 6, OID.RightWingOfInjury, new(-335f, -383f));
        hints = new();
        hints.PotentialTargets.Add(new(left, 0, false));
        hints.PotentialTargets.Add(new(right, AIHints.Enemy.PriorityForbidden, false));
        module.CalculateAIHints(0, player, default, hints);
        Check(hints.PotentialTargets[0].Priority == 1, "targetable wing receives priority");
        Check(hints.PotentialTargets[1].Priority == AIHints.Enemy.PriorityForbidden, "wing priority preserves native exclusion");
        ws.Execute(new ActorState.OpDead(right.InstanceID, true));
        module.Update();
        Check(module.StateMachine.ActiveState != null, "first wing death keeps module active");
        ws.Execute(new ActorState.OpDead(left.InstanceID, true));
        module.Update();
        Check(module.Components.Count == 0 && !boss.IsDead, "both wings finish encounter while Faunehm survives");

        left.IsDead = right.IsDead = false;
        using var retry = new Faunehm(ws, boss);
        retry.Update();
        Check(retry.Components.Count == 6, "new module bootstraps already-present live wings");
        Check(retry.Components.OfType<SimpleAOEs>().Single().Casters.Count == 0, "fresh module has no stale cone");
        boss.IsDead = true;
        retry.Update();
        Check(retry.Components.Count == 0, "primary death clears components");
        Console.WriteLine($"Dark as the Night Sky synthetic regression: {_assertions} checks passed.");
    }
}
