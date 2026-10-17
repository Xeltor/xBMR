using System.Numerics;
using BossMod;
using BossMod.Components;
using Wolves = BossMod.RealmReborn.Quest.Job.Lancer.ADangerousProposition.ADangerousProposition;
using Training = BossMod.RealmReborn.Quest.Job.Lancer.LanceOfDestinyTraining.LanceOfDestinyTraining;
using Boar = BossMod.RealmReborn.Quest.Job.Lancer.LanceOfDestiny.LanceOfDestiny;
using Duel = BossMod.RealmReborn.Quest.Job.Lancer.ProofOfMightFoulques.ProofOfMightFoulques;

static class Checks
{
    private static int assertions;
    private static readonly DateTime Now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        ++assertions;
    }
    private static WorldState World(ushort zone, ushort cfc) => new(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1), CurrentZone = zone, CurrentCFCID = cfc };
    private static Actor Create(WorldState ws, ulong id, uint oid, WPos pos, float radius = 0.5f, bool ally = false)
    {
        ws.Execute(new ActorState.OpCreate(id, oid, (int)id, 0, "", 0, ActorType.Enemy, Class.None, 30, new Vector4(pos.X, 0, pos.Z, 0), radius, new(100, 100, 0, 0, 0), true, ally, 0, 0, 0));
        return ws.Actors.Find(id)!;
    }
    private static void Cast(WorldState ws, Actor caster, uint aid, float total = 2.2f, ulong target = 0) => ws.Execute(new ActorState.OpCastInfo(caster.InstanceID, new() { Action = new(ActionType.Spell, aid), Location = caster.PosRot.AsVector3(), TotalTime = total, TargetID = target }));
    private static AIHints Hints(BossModule module, Actor player, params Actor[] enemies)
    {
        var hints = new AIHints();
        foreach (var e in enemies) hints.PotentialTargets.Add(new(e, 0, false));
        module.CalculateAIHints(0, player, default, hints);
        return hints;
    }
    public static void Run(string gameDirectory)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"));
        Service.LuminaGameData = game;
        Service.Config.Initialize();
        foreach (var (oid, type) in new (uint, Type)[] { (0x666, typeof(Wolves)), (0x220, typeof(Training)), (0x6CD, typeof(Boar)), (0x223, typeof(Duel)) })
            Check(BossModuleRegistry.FindByOID(oid)?.ModuleType == type, "full-source generated module registration");

        var ws = World(237, 307);
        var anchor = Create(ws, 1, 0x666, new(305, -285), ally: true);
        var alpha = Create(ws, 2, 0x21F, new(305, -285), 2.2f);
        var pack = Create(ws, 3, 0x599, new(310, -285), 1.65f);
        pack.InCombat = true;
        Cast(ws, alpha, 999); Cast(ws, pack, 975);
        using (var m = new Wolves(ws, anchor))
        {
            m.Update();
            Check(m.StateMachine.ActiveState != null, "pack combat activates stable ally anchor");
            var h = Hints(m, pack, alpha, pack, anchor);
            Check(h.ForbiddenZones.Count == 2, "independent alpha and pack casts feed AI");
            Check(h.PotentialTargets[0].Priority == 2 && h.PotentialTargets[1].Priority == 1 && h.PotentialTargets[2].Priority == 0, "victory target priority and friendly exclusion");
            h.PotentialTargets[0].Priority = -4; m.CalculateAIHints(0, pack, default, h);
            Check(h.PotentialTargets[0].Priority == -4, "wolf priorities preserve native exclusion");
            var neck = m.Components.OfType<SimpleAOEs>().Single(c => c.WatchedAction == 975).Casters.Single();
            Check(neck.Shape.Check(new(310, -280.4f), neck.Origin, neck.Rotation) && !neck.Shape.Check(new(310, -280.3f), neck.Origin, neck.Rotation), "pack hitbox plus action range");
            ws.Execute(new ActorState.OpCastInfo(pack.InstanceID, null));
            Check(Hints(m, pack).ForbiddenZones.Count == 1, "cancellation removes only one caster");
            ws.Execute(new ActorState.OpDead(pack.InstanceID, true)); m.Update();
            Check(m.StateMachine.ActiveState != null, "pack death does not end alpha phase");
            ws.Execute(new ActorState.OpDead(alpha.InstanceID, true)); m.Update();
            Check(m.Components.Count == 0, "alpha death clears phase and overlapping cast");
        }

        ws = World(238, 308);
        anchor = Create(ws, 1, 1003039, new(171, -273), ally: true);
        var first = Create(ws, 2, 0x220, new(160, -273)); first.InCombat = true;
        using (var m = new Training(ws, first))
        {
            m.Update(); Check(m.StateMachine.ActiveState != null, "guild first opponent activates before third spawn");
            ws.Execute(new ActorState.OpDead(first.InstanceID, true)); m.Update();
            Check(m.StateMachine.ActiveState != null, "guild first death keeps trial active");
            ws.Execute(new ActorState.OpDestroy(first.InstanceID)); m.Update();
            Check(m.StateMachine.ActiveState != null, "guild destroyed first anchor keeps trial active");
            var grizzled = Create(ws, 3, 0x221, new(160, -273)); grizzled.InCombat = true; Cast(ws, grizzled, 866);
            Check(Hints(m, grizzled).ForbiddenZones.Count == 1, "second opponent telegraph after first death");
            ws.Execute(new ActorState.OpCastInfo(grizzled.InstanceID, null)); ws.Execute(new ActorState.OpDead(grizzled.InstanceID, true)); m.Update();
            Check(m.StateMachine.ActiveState != null, "guild second death keeps trial active");
            var third = Create(ws, 4, 0x222, new(160, -273)); Cast(ws, third, 866);
            Check(Hints(m, third).ForbiddenZones.Count == 1, "third opponent telegraph");
            ws.Execute(new ActorState.OpDead(third.InstanceID, true)); m.Update();
            Check(m.Components.Count == 0, "guild final death clears all components");
        }

        ws = World(290, 355);
        var boss = Create(ws, 1, 0x6CD, new(-235, 21), 3); boss.InCombat = true;
        var hog = Create(ws, 2, 0x6CE, new(-240, 21), 2.4f);
        var water = Create(ws, 3, 0x6CF, new(-235, 15), 0.8f);
        var fire = Create(ws, 4, 0x6D0, new(-230, 15), 0.8f);
        Cast(ws, boss, 334); Cast(ws, hog, 334);
        using (var m = new Boar(ws, boss))
        {
            m.Update(); var h = Hints(m, boss, boss, hog, water, fire);
            Check(h.ForbiddenZones.Count == 2, "both hog sizes predicted simultaneously");
            var c = m.Components.OfType<SimpleAOEs>().Single(x => x.WatchedAction == 334);
            Check(((AOEShapeRect)c.Casters[0].Shape).LengthFront == 6 && Math.Abs(((AOEShapeRect)c.Casters[1].Shape).LengthFront - 5.4f) < 0.001, "per-caster hitbox geometry");
            Check(h.PotentialTargets[2].Priority > h.PotentialTargets[3].Priority && h.PotentialTargets[3].Priority > h.PotentialTargets[1].Priority, "healer and caster adds prioritized");
            h.PotentialTargets[2].Priority = -4; m.CalculateAIHints(0, boss, default, h);
            Check(h.PotentialTargets[2].Priority == -4, "sprite priorities preserve native exclusion");
            ws.Execute(new ActorState.OpDead(hog.InstanceID, true)); m.Update();
            Check(m.StateMachine.ActiveState != null, "hog death keeps boss mechanics active");
            ws.Execute(new ActorState.OpDestroy(boss.InstanceID)); m.Update();
            Check(m.Components.Count == 0, "scripted boss departure ends battle");
        }

        ws = World(240, 310);
        boss = Create(ws, 1, 0x223, new(-285, 292)); boss.InCombat = true;
        first = Create(ws, 2, 0x5FB, new(-280, 292));
        var second = Create(ws, 3, 0x5FC, new(-275, 292));
        var frost = Create(ws, 4, 0x5FD, new(-285, 292)); frost.IsTargetable = false; frost.EventState = 1;
        Cast(ws, boss, 1316, 2.7f); Cast(ws, first, 866); Cast(ws, second, 147, 2.7f, boss.InstanceID);
        using (var m = new Duel(ws, boss))
        {
            m.Update(); var h = Hints(m, boss, boss, first, second);
            Check(h.ForbiddenZones.Count == 3, "ice and dodgeable casts constrain AI without chasing a player-targeted spell");
            Check(h.PotentialTargets[2].Priority > h.PotentialTargets[1].Priority && h.PotentialTargets[1].Priority > h.PotentialTargets[0].Priority, "souls before Foulques");
            Check(h.PotentialTargets.All(x => !x.ShouldBeInterrupted && !x.ShouldBeStunned), "no speculative progression-breaking stun");
            Check(m.Components.OfType<BaitAwayCast>().Single().CurrentBaits.Single().Target == boss, "Fire II keeps the actual actor target");
            h.PotentialTargets[1].Priority = -4; m.CalculateAIHints(0, boss, default, h);
            Check(h.PotentialTargets[1].Priority == -4, "soul priorities preserve native exclusion");
            ws.Execute(new ActorState.OpCastInfo(boss.InstanceID, null));
            Check(Hints(m, boss).ForbiddenZones.Count == 2, "cast cancellation retains actual persistent ice");
            ws.Execute(new ActorState.OpDestroy(frost.InstanceID));
            Check(Hints(m, boss).ForbiddenZones.Count == 1, "destroying ice removes only that persistent hazard");
            ws.Execute(new ActorState.OpCastEvent(boss.InstanceID, new(new(ActionType.Spell, 1316), boss.InstanceID, 0, 0, boss.PosRot.AsVector3(), 1, 0, default)));
            Check(Hints(m, boss).ForbiddenZones.Count == 2, "Ring of Frost event bridges the pre-spawn gap");
            var replacement = Create(ws, 5, 0x5FD, boss.Position); replacement.IsTargetable = false; replacement.EventState = 1;
            m.Update(); Check(Hints(m, boss).ForbiddenZones.Count == 2, "spawn replaces prediction without duplicate avoidance");
            replacement.EventState = 7;
            Check(Hints(m, boss).ForbiddenZones.Count == 1, "retired object event state removes ice");
            ws.Execute(new ActorState.OpDead(boss.InstanceID, true)); m.Update();
            Check(m.Components.Count == 0, "duel completion clears overlapping adds and ice");
        }
        using (var retry = new Duel(ws, boss))
        {
            boss.IsDead = false; boss.CastInfo = first.CastInfo = second.CastInfo = null;
            retry.Update(); Check(Hints(retry, boss).ForbiddenZones.Count == 0, "fresh attempt does not retain ice or casts");
        }
        Console.WriteLine($"ARR Lancer synthetic regression: {assertions} checks passed.");
    }
}
