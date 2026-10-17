using System.Numerics;
using BossMod;
using BossMod.Components;
using Spirithold = BossMod.RealmReborn.Quest.MSQ.SpiritholdBroken;
using Leia = BossMod.RealmReborn.Quest.MSQ.LeiasLegacy;
using Guardian = BossMod.RealmReborn.Quest.MSQ.ToGuardAGuardian;
using Copper = BossMod.RealmReborn.Quest.MSQ.IntoACopperHell;

static class Checks
{
    private static int assertions;
    private static readonly DateTime Now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        ++assertions;
    }

    private static WorldState World(ushort zone, ushort duty) => new(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1), CurrentZone = zone, CurrentCFCID = duty };
    private static Actor Create(WorldState ws, ulong id, uint oid, WPos pos, bool ally = false, float radius = 0.5f)
    {
        ws.Execute(new ActorState.OpCreate(id, oid, (int)id, 0, "", 0, ActorType.Enemy, Class.None, 15,
            new Vector4(pos.X, 21, pos.Z, 0), radius, new(100, 100, 0, 0, 0), true, ally, 0, 0, 0));
        var actor = ws.Actors.Find(id)!;
        actor.InCombat = true;
        return actor;
    }

    private static void Cast(WorldState ws, Actor actor, uint aid, float duration = 2.2f, Actor? target = null)
        => ws.Execute(new ActorState.OpCastInfo(actor.InstanceID, new() { Action = new(ActionType.Spell, aid), TargetID = target?.InstanceID ?? actor.InstanceID,
            Location = (target ?? actor).PosRot.AsVector3(), TotalTime = duration }));

    private static AIHints Hints(BossModule module, params Actor[] actors)
    {
        var hints = new AIHints();
        foreach (var actor in actors) hints.PotentialTargets.Add(new(actor, 0, false));
        module.CalculateAIHints(0, actors[0], default, hints);
        return hints;
    }

    public static void Run(string gameDirectory)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"));
        Service.LuminaGameData = game;
        Service.Config.Initialize();
        Golem(); Thieves(); Bodyguards(); GuardianTree();
        Console.WriteLine($"Early ARR quest synthetic regression: {assertions} checks passed.");
    }

    private static void Golem()
    {
        var ws = World(225, 297);
        var boss = Create(ws, 1, (uint)Spirithold.OID.Boss, new(251, 191), radius: 2.2f);
        Cast(ws, boss, (uint)Spirithold.AID.TrueGrit, 3.7f);
        using var module = new Spirithold.SpiritholdBroken(ws, boss);
        module.Update();
        Check(BossModuleRegistry.FindByOID(boss.OID)?.ModuleType == module.GetType(), "golem registration");
        var cone = module.Components.OfType<SimpleAOEs>().Single(c => c.WatchedAction == (uint)Spirithold.AID.TrueGrit);
        Check(cone.Casters.Count == 1, "golem preexisting cast bootstrap");
        Check(Math.Abs((cone.Casters[0].Activation - Now).TotalSeconds - 4) < 0.001, "golem warning activation includes native correction");
        Check(cone.Shape.Check(boss.Position + new WDir(0, 8.19f), boss.Position, default), "golem cone inner radius");
        Check(!cone.Shape.Check(boss.Position + new WDir(0, 8.21f), boss.Position, default), "golem cone outer radius");
        Check(!cone.Shape.Check(boss.Position + new WDir(0, -4), boss.Position, default), "golem rear safe");
        Check(Hints(module, boss).ForbiddenZones.Count == 1, "golem cone reaches AI");
        ws.Execute(new ActorState.OpCastInfo(boss.InstanceID, null));
        Check(cone.Casters.Count == 0, "golem cancellation cleanup");
        Cast(ws, boss, (uint)Spirithold.AID.Plaincracker, 3.2f);
        var circle = module.Components.OfType<SimpleAOEs>().Single(c => c.WatchedAction == (uint)Spirithold.AID.Plaincracker);
        Check(circle.Casters.Count == 1 && Hints(module, boss).ForbiddenZones.Count == 1, "plaincracker replaces cone with circle");
        ws.Execute(new ActorState.OpDead(boss.InstanceID, true)); module.Update();
        Check(module.Components.Count == 0, "golem death clears phase");
    }

    private static void Thieves()
    {
        var ws = World(227, 299);
        var boss = Create(ws, 1, (uint)Leia.OID.Boss, new(-178, 290));
        var archer = Create(ws, 2, (uint)Leia.OID.HedgeKnightArcher, new(-175, 289));
        var healer = Create(ws, 3, (uint)Leia.OID.HedgeKnightHealer, new(-175, 292));
        using var module = new Leia.LeiasLegacy(ws, boss); module.Update();
        Check(BossModuleRegistry.FindByOID(boss.OID)?.ModuleType == module.GetType(), "thieves registration");
        var hints = Hints(module, boss, archer, healer);
        Check(hints.PotentialTargets.Select(t => t.Priority).SequenceEqual([1, 2, 3]), "thieves healer then archers then boss priority");
        hints.PotentialTargets[2].Priority = -4; module.CalculateAIHints(0, boss, default, hints);
        Check(hints.PotentialTargets[2].Priority == -4, "thieves preserves forbidden priority");
        Cast(ws, boss, (uint)Leia.AID.Heartstopper);
        Check(Hints(module, boss).ForbiddenZones.Count == 1, "heartstopper warning reaches AI");
        ws.Execute(new ActorState.OpCastInfo(boss.InstanceID, null));
        ws.Execute(new ActorState.OpDead(boss.InstanceID, true)); module.Update();
        Check(module.StateMachine.ActiveState != null, "boss death retains surviving thieves");
        ws.Execute(new ActorState.OpDead(healer.InstanceID, true));
        ws.Execute(new ActorState.OpDead(archer.InstanceID, true)); module.Update();
        Check(module.Components.Count == 0, "all thieves dead completes phase");
    }

    private static void Bodyguards()
    {
        var ws = World(274, 338);
        var boss = Create(ws, 1, (uint)Copper.OID.Boss, new(39, -145));
        var mage = Create(ws, 2, (uint)Copper.OID.RaucousBodyguard, new(43, -148));
        var gladiator = Create(ws, 3, (uint)Copper.OID.BoastfulBodyguard, new(37, -149));
        using var module = new Copper.IntoACopperHell(ws, boss); module.Update();
        Check(BossModuleRegistry.FindByOID(boss.OID)?.ModuleType == module.GetType(), "bodyguards registration");
        Check(Hints(module, boss, mage, gladiator).PotentialTargets.Select(t => t.Priority).SequenceEqual([2, 3, 1]), "bodyguards caster priority");
        mage.IsTargetable = false;
        Check(Hints(module, mage).PotentialTargets[0].Priority == 0, "hidden reserve is not promoted");
        mage.IsTargetable = true;
        ws.Execute(new ActorState.OpDead(boss.InstanceID, true)); module.Update();
        Check(module.StateMachine.ActiveState != null, "bodyguards survive primary death");
        ws.Execute(new ActorState.OpDead(mage.InstanceID, true));
        ws.Execute(new ActorState.OpDead(gladiator.InstanceID, true)); module.Update();
        Check(module.Components.Count == 0, "all bodyguards dead completes phase");
    }

    private static void GuardianTree()
    {
        var ws = World(226, 298);
        var galfrid = Create(ws, 1, (uint)Guardian.OID.Galfrid, new(-302, 162), ally: true);
        var quzal = Create(ws, 2, (uint)Guardian.OID.QuzalHuatotl, new(-310, 176));
        var caller = Create(ws, 3, (uint)Guardian.OID.IxaliSincaller, new(-310, 170));
        using var module = new Guardian.ToGuardAGuardian(ws, galfrid); module.Update();
        Check(BossModuleRegistry.FindByOID(galfrid.OID)?.ModuleType == module.GetType(), "guardian ally anchor registration");
        Check(Hints(module, quzal, caller).PotentialTargets.Select(t => t.Priority).SequenceEqual([1, 4]), "guardian caster priority");
        caller.IsAlly = true;
        Check(Hints(module, caller).PotentialTargets[0].Priority == 0, "friendly actor never promoted");
        ws.Execute(new ActorState.OpDead(quzal.InstanceID, true)); module.Update();
        Check(module.StateMachine.ActiveState != null, "quzal cutscene does not complete module");
        ws.Execute(new ActorState.OpDestroy(galfrid.InstanceID)); module.Update();
        Check(module.Components.Count == 0, "first cutscene removes first-battle guidance");
        var gargoyle = Create(ws, 4, (uint)Guardian.OID.LesserGargoyle, new(-310, 176));
        var mage = Create(ws, 5, (uint)Guardian.OID.MaskedMage, new(-315, 178));
        var papalymo = Create(ws, 6, 0x399, new(-313, 175), ally: true);
        using var secondModule = new Guardian.GuardianMage(ws, mage); secondModule.Update();
        Check(BossModuleRegistry.FindByOID(mage.OID)?.ModuleType == secondModule.GetType(), "second guardian battle registration");
        Cast(ws, papalymo, (uint)Guardian.AID.FireII, target: mage);
        Check(secondModule.Components.OfType<BaitAwayCast>().Single().CurrentBaits.Count == 0, "friendly fire does not create a bait");
        Cast(ws, mage, (uint)Guardian.AID.FireII, target: papalymo);
        Check(secondModule.Components.OfType<BaitAwayCast>().Single().CurrentBaits.Count == 1, "hostile fire follows actual target");
        ws.Execute(new ActorState.OpCastInfo(mage.InstanceID, null));
        Check(secondModule.Components.OfType<BaitAwayCast>().Single().CurrentBaits.Count == 0, "targeted fire cancellation cleanup");
        Cast(ws, mage, (uint)Guardian.AID.BlizzardII);
        Check(Hints(secondModule, quzal).ForbiddenZones.Count == 1, "mage blizzard avoidance reaches AI");
        ws.Execute(new ActorState.OpCastInfo(mage.InstanceID, null));
        Check(Hints(secondModule, gargoyle, mage).PotentialTargets.Select(t => t.Priority).SequenceEqual([5, 1]), "gargoyle before mage");
        ws.Execute(new ActorState.OpDead(gargoyle.InstanceID, true)); secondModule.Update();
        Check(secondModule.StateMachine.ActiveState != null, "gargoyle death retains mage mechanics");
        ws.Execute(new ActorState.OpDead(mage.InstanceID, true)); secondModule.Update();
        Check(secondModule.Components.Count == 0, "second wave death clears module");
    }
}
