using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using BossMod;
using BossMod.Components;
using Quest = BossMod.RealmReborn.Quest.MSQ.EscapeFromCastrumCentri;

static class Checks
{
    // Synthetic state/policy checks; authored planning bounds are not measured floor geometry.
    private const uint Rope = 0x1E8DFB, Optio = 0x879, Eques = 0x87A, Signifer = 0x87B;
    private const uint Medicus = 0x87F, Hoplomachus = 0x880, Vanguard = 0x881, Colossus = 0x882, Generator = 0x883, Healer = 0x884;
    private static readonly uint[] Enemies = [Optio, Eques, Signifer, Medicus, Hoplomachus, Vanguard, Colossus, Generator, Healer];
    private static readonly DateTime Now = new(2026, 10, 17, 15, 0, 0, DateTimeKind.Utc);
    private static int assertions;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        ++assertions;
    }

    private static WorldState World() => new(1, "synthetic")
    {
        Frame = new(Now, 0, 0, 0, 0, 1), CurrentZone = 305, CurrentCFCID = 352
    };

    private static Actor Create(WorldState ws, ulong id, uint oid, bool ally = false, bool targetable = true,
        WPos? position = null, float hitbox = .5f, ActorType type = ActorType.Enemy)
    {
        var pos = position ?? new WPos(-453, -258);
        ws.Execute(new ActorState.OpCreate(id, oid, (int)id, 0, "", 0, type, Class.None, 46,
            new Vector4(pos.X, 0, pos.Z, 0), hitbox, new(100, 100, 0, 0, 0), targetable, ally, 0, 0, 0));
        return ws.Actors.Find(id)!;
    }

    private static AIHints Hints(BossModule module, Actor pc, params (Actor Actor, int Priority)[] inputs)
    {
        var hints = new AIHints();
        foreach (var (actor, priority) in inputs) hints.PotentialTargets.Add(new(actor, priority, false));
        module.CalculateAIHints(0, pc, default, hints);
        return hints;
    }

    private static AIHints DirectHints(BossModule module, Actor pc)
    {
        var hints = new AIHints();
        foreach (var component in module.Components) component.AddAIHints(0, pc, default, hints);
        return hints;
    }

    private static GenericAOEs.AOEInstance[] Hazards(BossModule module, Actor pc)
        => module.Components.OfType<GenericAOEs>().SelectMany(c => c.ActiveAOEs(0, pc).ToArray()).ToArray();

    private static ActorCastInfo Cast(WorldState ws, Actor source, uint action, ulong? target = null,
        Vector3? location = null, Angle rotation = default, ActionType type = ActionType.Spell)
    {
        ws.Execute(new ActorState.OpCastInfo(source.InstanceID, new ActorCastInfo
        {
            Action = new(type, action), TargetID = target ?? source.InstanceID,
            Location = location ?? source.PosRot.AsVector3(), Rotation = rotation, TotalTime = 2.2f
        }));
        return source.CastInfo!; // The operation clones its input; exercise actual native identity.
    }

    public static void Run(string gameDirectory)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"));
        Service.LuminaGameData = game;
        Service.Config.Initialize();
        ActivationAndPriorities(); StageAndDutyLifecycle(); CastGeometryAndLifecycle(); FireIIBaits(); QuestBattleHandoff();
        Console.WriteLine($"Escape from Castrum Centri synthetic regression: {assertions} checks passed.");
    }

    private static void ActivationAndPriorities()
    {
        var ws = World();
        var rope = Create(ws, 1, Rope, targetable: false, type: ActorType.EventObj);
        var pc = Create(ws, 2, 0, ally: true, type: ActorType.Player);
        var enemies = Enemies.Select((oid, index) => Create(ws, (ulong)index + 10, oid)).ToArray();
        var unrelated = Create(ws, 30, 0x1234);
        var friendly = Create(ws, 31, Signifer, ally: true);
        var untargetable = Create(ws, 32, Generator, targetable: false);
        var dead = Create(ws, 33, Medicus); ws.Execute(new ActorState.OpDead(dead.InstanceID, true));
        using var module = new Quest.EscapeFromCastrumCentri(ws, rope); module.Update();
        Check(Rope == 2002427, "rope anchor hexadecimal identity is correct");
        Check(BossModuleRegistry.FindByOID(Rope)?.ModuleType == module.GetType(), "generated registry supplies quest module for stable rope anchor");
        Check(module.StateMachine.ActiveState == null, "passive enemies alone do not start combat phase");
        unrelated.InCombat = true; friendly.InCombat = true; module.Update();
        Check(module.StateMachine.ActiveState == null, "unrelated or friendly combat cannot activate the encounter");
        enemies[0].InCombat = true; module.Update();
        Check(module.StateMachine.ActiveState != null && module.Components.Count > 0, "known hostile combat activates despite non-combat untargetable event-object anchor");
        Check(module.ShouldPrioritizeAllEnemies, "quest objectives remain available even when not already fighting the player");
        Check(module.GetDefaultTarget(0) == null, "rotation fallback never attacks rope anchor");
        var seeded = enemies.Select(a => (a, 8)).Concat(new[] { (unrelated, 11), (friendly, 11), (untargetable, 11), (dead, 11), (rope, 11) }).ToArray();
        var hints = Hints(module, pc, seeded);
        int[] priorities = [3, 3, 5, 6, 3, 2, 2, 4, 6];
        for (var i = 0; i < priorities.Length; ++i)
            Check(hints.PotentialTargets[i].Priority == priorities[i], $"eligible OID{Enemies[i]:X} gets authored priority {priorities[i]}");
        Check(hints.PotentialTargets.Skip(9).All(e => e.Priority == 11), "unrelated, friendly, untargetable, dead and event-object entries preserve incoming priorities");
        Check(!enemies[7].InCombat && hints.PotentialTargets[7].Priority == 4, "passive targetable field generator can receive its objective priority");
        for (var priority = -4; priority < 0; ++priority)
        {
            var negatives = Hints(module, pc, enemies.Append(unrelated).Append(rope).Select(a => (a, priority)).ToArray());
            Check(negatives.PotentialTargets.All(e => e.Priority == priority), $"all incoming negative exclusions {priority} survive for soldiers, robots, objectives and unrelated actors");
            Check(module.GetDefaultTarget(0) == null, $"all-negative target set cannot cause rope fallback ({priority})");
        }
        Check(hints.PotentialTargets.All(e => !e.ShouldBeStunned && !e.ShouldBeInterrupted && !e.ForbidDOTs), "target strategy does not invent control or DOT restrictions");
        Check(DirectHints(module, pc).ActionsToExecute.Entries.Count == 0, "module queues no unsolicited abilities or consumables");
        foreach (var oid in Enemies)
        {
            var isolated = World(); var anchor = Create(isolated, 1, Rope, targetable: false, type: ActorType.EventObj);
            var hostile = Create(isolated, 2, oid); hostile.InCombat = true;
            using var single = new Quest.EscapeFromCastrumCentri(isolated, anchor); single.Update();
            Check(single.StateMachine.ActiveState != null, $"known OID{oid:X} combat supports bootstrap activation independently");
        }
    }

    private static void StageAndDutyLifecycle()
    {
        var ws = World(); var rope = Create(ws, 1, Rope, type: ActorType.EventObj);
        var enemy = Create(ws, 2, Signifer); enemy.InCombat = true;
        var pc = Create(ws, 3, 0, ally: true, type: ActorType.Player);
        var ally = Create(ws, 4, 0x1234, ally: true);
        using var module = new Quest.EscapeFromCastrumCentri(ws, rope); module.Update();
        var components = module.Components.ToArray();
        Check(module.CalculateGlobalHints(pc).Any(h => h.Contains("Rope Bonds")), "prison objective directs player to both bonds");
        Check(module.GetDefaultTarget(0) == null, "targetable rope is still never a fallback combat target");
        ws.Execute(new ActorState.OpDead(enemy.InstanceID, true)); ws.Execute(new ActorState.OpDestroy(enemy.InstanceID)); module.Update();
        Check(module.StateMachine.ActiveState != null && components.SequenceEqual(module.Components), "prison enemy disappearance preserves components through stage gap");
        ws.Frame = new(Now.AddSeconds(20), 0, 0, 0, 0, 1); module.Update();
        Check(module.StateMachine.ActiveState != null, "empty later frame does not imply duty completion");
        ws.Execute(new ActorState.OpDead(ally.InstanceID, true)); module.Update();
        Check(module.StateMachine.ActiveState != null, "optional ally KO cannot end the duty");
        ws.Execute(new ActorState.OpTargetable(rope.InstanceID, false)); rope.InCombat = false;
        ws.Execute(new ActorState.OpDead(rope.InstanceID, true)); module.Update();
        Check(module.StateMachine.ActiveState != null && components.SequenceEqual(module.Components), "rope death flag and targetability changes are not phase identities");
        Check(module.CheckReset(), "empty stage gap yields to existing QuestBattle objective and route guidance");
        var exterior = Create(ws, 2, Generator); module.Update();
        Check(components.SequenceEqual(module.Components), "later exterior generator generation shares same stable encounter components");
        Check(Hints(module, pc, (exterior, 0)).PotentialTargets[0].Priority == 4, "field generator spawned during prison-stage lifetime remains available");
        Check(module.GetDefaultTarget(0) == null, "stage gap and new target generation never restore rope fallback");
        Create(ws, 5, 0x7FC, ally: true); module.Update();
        Check(module.CalculateGlobalHints(pc).Any(h => h.Contains("field generators")), "exterior friendly roster enables generator strategy hint");
        ws.CurrentCFCID = 0; Check(module.CheckReset(), "leaving exact content condition requests reset");
        ws.CurrentCFCID = 352; ws.CurrentZone = 306; Check(module.CheckReset(), "leaving exact territory requests reset");
        ws.CurrentZone = 305; Check(module.CheckReset(), "exact duty alone does not retain module while quest combat is absent");
        exterior.InCombat = true; Check(!module.CheckReset(), "later known combat restores exact-duty eligibility");
        ws.Execute(new ActorState.OpDestroy(rope.InstanceID)); module.Update();
        Check(module.Components.Count == 0 && Hazards(module, pc).Length == 0, "rope destruction completes phase and removes hazard state");
        foreach (var wrongZone in new[] { false, true })
        {
            var outside = World();
            if (wrongZone) outside.CurrentZone = 306; else outside.CurrentCFCID = 0;
            var anchor = Create(outside, 1, Rope, type: ActorType.EventObj);
            var hostile = Create(outside, 2, Vanguard); hostile.InCombat = true;
            using var wrong = new Quest.EscapeFromCastrumCentri(outside, anchor); wrong.Update();
            Check(wrong.StateMachine.ActiveState == null, "known enemy combat cannot activate outside exact quest duty");
        }
        var next = World(); var nextRope = Create(next, 1, Rope, targetable: false, type: ActorType.EventObj);
        var nextEnemy = Create(next, 2, Vanguard);
        Cast(next, nextEnemy, 525);
        using var resumed = new Quest.EscapeFromCastrumCentri(next, nextRope); resumed.Update();
        Check(resumed.StateMachine.ActiveState != null && !resumed.CheckReset(), "existing cast bootstraps next-wave module even before InCombat flag arrives");
        next.Execute(new ActorState.OpCastInfo(nextEnemy.InstanceID, null));
        Check(resumed.CheckReset(), "cast-only activation yields again when cast and combat are absent");
    }

    private sealed record AOETest(uint SourceOID, uint Action, float Range, float HalfAngle = 0, float HalfWidth = 0, bool Ground = false);

    private static void CastGeometryAndLifecycle()
    {
        AOETest[] cases = [new(Vanguard, 525, 30, HalfWidth: 2.5f), new(Vanguard, 526, 6, HalfWidth: 2.5f),
            new(Vanguard, 527, 8, HalfAngle: 60), new(Colossus, 534, 6, HalfAngle: 60), new(Colossus, 537, 6, Ground: true),
            new(Optio, 720, 6, HalfAngle: 45), new(Optio, 1135, 30, HalfWidth: 1), new(Hoplomachus, 1098, 3, HalfWidth: 1.5f),
            new(Signifer, 1304, 5, Ground: true)];
        foreach (var c in cases)
        {
            var ws = World(); var anchor = Create(ws, 1, Rope, targetable: false, type: ActorType.EventObj);
            var source = Create(ws, 2, c.SourceOID, hitbox: 2.8f); source.InCombat = true;
            var second = Create(ws, 3, c.SourceOID, position: new(-420, -240), hitbox: 1.2f);
            var pc = Create(ws, 4, 0, ally: true, type: ActorType.Player);
            var friendly = Create(ws, 5, c.SourceOID, ally: true);
            var unrelated = Create(ws, 6, 0x1234);
            var wrongSource = Create(ws, 7, Generator);
            var nonSpell = Create(ws, 8, c.SourceOID);
            var origin = c.Ground ? new Vector3(-440, 0, -230) : source.PosRot.AsVector3();
            var rotation = new Angle(.4f);
            var cast = Cast(ws, source, c.Action, location: origin, rotation: rotation);
            Cast(ws, second, c.Action, rotation: new Angle(-.7f));
            Cast(ws, friendly, c.Action); Cast(ws, unrelated, c.Action); Cast(ws, wrongSource, c.Action);
            Cast(ws, nonSpell, c.Action, type: ActionType.Ability);
            using var module = new Quest.EscapeFromCastrumCentri(ws, anchor); module.Update();
            var hazards = Hazards(module, pc);
            Check(hazards.Length == 2, $"{c.Action}: bootstrap retains simultaneous valid casts and rejects friendly, unknown, wrong source and non-spell casts");
            var first = hazards.Single(a => a.ActorID == source.InstanceID);
            Check(first.Origin == new WPos(origin.X, origin.Z), $"{c.Action}: current cast origin is captured");
            Check(Math.Abs(first.Rotation.Rad - rotation.Rad) < .00001f, $"{c.Action}: native cast rotation is captured");
            Check(first.Activation == Now.AddSeconds(2.5), $"{c.Action}: native NPC finish correction is included");
            var range = c.Range + (c.Ground ? 0 : source.HitboxRadius);
            Check(first.Check(first.Origin + rotation.ToDirection() * (range - .02f)), $"{c.Action}: inside nominal forward boundary is forbidden");
            Check(!first.Check(first.Origin + rotation.ToDirection() * (range + .02f)), $"{c.Action}: outside nominal forward boundary remains available");
            Check(first.Check(first.Origin - rotation.ToDirection()) == c.Ground, $"{c.Action}: ground circle versus forward shape rear semantics");
            if (c.HalfAngle > 0)
            {
                Check(first.Check(first.Origin + (rotation + (c.HalfAngle - .2f).Degrees()).ToDirection() * 2), $"{c.Action}: supported cone half-angle interior");
                Check(!first.Check(first.Origin + (rotation + (c.HalfAngle + .2f).Degrees()).ToDirection() * 2), $"{c.Action}: supported cone half-angle exterior");
            }
            if (c.HalfWidth > 0)
            {
                var side = (rotation + 90.Degrees()).ToDirection();
                Check(first.Check(first.Origin + rotation.ToDirection() * 2 + side * (c.HalfWidth - .02f)), $"{c.Action}: supported rectangle half-width interior");
                Check(!first.Check(first.Origin + rotation.ToDirection() * 2 + side * (c.HalfWidth + .02f)), $"{c.Action}: supported rectangle half-width exterior");
            }
            if (c.Ground)
                Check(first.Shape is AOEShapeCircle circle && circle.Radius == c.Range, $"{c.Action}: fixed ground circle does not add caster hitbox");
            Check(Hints(module, pc).ForbiddenZones.Count == 2, $"{c.Action}: both hazard deadlines reach AI consumers");
            ws.Execute(new ActorState.OpMove(source.InstanceID, new Vector4(-470, 0, -270, 2)));
            Check(Hazards(module, pc).Single(a => a.ActorID == source.InstanceID).Origin == first.Origin, $"{c.Action}: actor movement cannot drag captured cast origin");
            foreach (var component in module.Components) component.OnCastStarted(source, cast);
            Check(Hazards(module, pc).Length == 2, $"{c.Action}: duplicate start cannot duplicate hazard");
            ws.Frame = new(Now.AddSeconds(2.25), 0, 0, 0, 0, 1); cast.ElapsedTime = 2.25f;
            Check(Hazards(module, pc).Single(a => a.ActorID == source.InstanceID).Activation == first.Activation, $"{c.Action}: raw elapsed tail cannot move captured deadline");
            ws.Execute(new ActorState.OpCastInfo(source.InstanceID, null));
            Check(Hazards(module, pc).Length == 1 && Hazards(module, pc)[0].ActorID == second.InstanceID, $"{c.Action}: cancellation retires only matching source");
            var replacement = Cast(ws, source, c.Action, location: origin, rotation: rotation);
            foreach (var component in module.Components) component.OnCastFinished(source, cast);
            Check(Hazards(module, pc).Length == 2 && Hazards(module, pc).Single(a => a.ActorID == source.InstanceID).Activation > first.Activation, $"{c.Action}: obsolete finish cannot retire fresh cast identity");
            ws.Execute(new ActorState.OpDead(source.InstanceID, true));
            Check(Hazards(module, pc).Length == 1, $"{c.Action}: dead source clears its hazard");
            ws.Execute(new ActorState.OpDestroy(source.InstanceID));
            var recreated = Create(ws, 2, c.SourceOID, hitbox: 2.8f);
            Check(Hazards(module, pc).Length == 1, $"{c.Action}: recycled numeric actor ID cannot resurrect prior hazard");
            Cast(ws, recreated, c.Action, location: origin, rotation: rotation);
            Check(Hazards(module, pc).Length == 2, $"{c.Action}: new actor generation can produce fresh hazard");
            ws.Execute(new ActorState.OpDestroy(recreated.InstanceID));
            Check(Hazards(module, pc).Length == 1, $"{c.Action}: destruction without cast finish clears matching generation");
            ws.Execute(new ActorState.OpCastInfo(second.InstanceID, null));
            Check(Hazards(module, pc).Length == 0 && Hints(module, pc).ForbiddenZones.Count == 0, $"{c.Action}: final cancellation clears all zones");
            Cast(ws, second, 9999);
            Check(Hazards(module, pc).Length == 0, $"{c.Action}: unknown action is not fabricated as a hazard");
            Cast(ws, second, c.Action);
            ws.Execute(new ActorState.OpDestroy(anchor.InstanceID)); module.Update();
            Check(module.Components.Count == 0 && Hazards(module, pc).Length == 0, $"{c.Action}: anchor destruction clears active cast state");
        }
    }

    private static void FireIIBaits()
    {
        var ws = World(); var rope = Create(ws, 1, Rope, targetable: false, type: ActorType.EventObj);
        var source = Create(ws, 2, Signifer, position: new(-480, -280)); source.InCombat = true;
        var second = Create(ws, 3, Signifer, position: new(-470, -280));
        var pc = Create(ws, 4, 0, ally: true, type: ActorType.Player);
        var other = Create(ws, 5, 0, ally: true, position: new(-440, -240), type: ActorType.Player);
        ws.Execute(new PartyState.OpModify(0, new(0, pc.InstanceID, false)));
        ws.Execute(new PartyState.OpModify(1, new(0, other.InstanceID, false)));
        var friendly = Create(ws, 6, Signifer, ally: true);
        var unknown = Create(ws, 7, 0x1234);
        var wrong = Create(ws, 8, Optio);
        var nonSpell = Create(ws, 9, Signifer);
        var cast = Cast(ws, source, 147, target: pc.InstanceID);
        Cast(ws, second, 147, target: other.InstanceID);
        Cast(ws, friendly, 147, target: pc.InstanceID); Cast(ws, unknown, 147, target: pc.InstanceID);
        Cast(ws, wrong, 147, target: pc.InstanceID); Cast(ws, nonSpell, 147, target: pc.InstanceID, type: ActionType.Ability);
        using var module = new Quest.EscapeFromCastrumCentri(ws, rope); module.Update();
        var component = module.Components.OfType<GenericBaitAway>().Single();
        Check(component.CurrentBaits.Count == 2, "FireII bootstrap retains concurrent Signifers and rejects allied, unknown, wrong-source and non-spell casters");
        var first = component.CurrentBaits.Single(b => b.Source == source);
        Check(first.Target == pc && component.CenterAtTarget, "FireII uses actual target as circle center");
        Check(first.Shape is AOEShapeCircle circle && circle.Radius == 5, "FireII target-following circle has nominal radius5");
        Check(component.BaitOrigin(ref first) == pc.Position && component.BaitOrigin(ref first) != source.Position, "FireII origin is target position rather than distant caster");
        Check(first.Activation == Now.AddSeconds(2.5), "FireII captures corrected native deadline");
        Check(Hazards(module, pc).Length == 0, "FireII is not also represented as a generic ground hazard");
        var ownHints = Hints(module, pc);
        Check(!ownHints.ForbiddenZones.Any(z => z.shapeDistance.Distance(pc.Position) < 0), "own FireII does not demand escape from its target-following circle");
        Check(ownHints.ForbiddenZones.Any(z => z.shapeDistance.Distance(other.Position) < 0), "other party member's FireII circle is a forbidden region");
        ws.Execute(new ActorState.OpMove(pc.InstanceID, new Vector4(-450, 0, -245, 0)));
        Check(component.BaitOrigin(ref first) == pc.Position, "FireII origin follows movement of its actual target");
        foreach (var c in module.Components) c.OnCastStarted(source, cast);
        Check(component.CurrentBaits.Count == 2, "duplicate FireII start cannot duplicate bait");
        ws.Frame = new(Now.AddSeconds(2.25), 0, 0, 0, 0, 1); cast.ElapsedTime = 2.25f; module.Update();
        Check(component.CurrentBaits.Single(b => b.Source == source).Activation == first.Activation, "FireII raw elapsed tail cannot drift deadline");
        ws.Execute(new ActorState.OpCastInfo(source.InstanceID, null)); module.Update();
        Check(component.CurrentBaits.Count == 1 && component.CurrentBaits[0].Source == second, "FireII cancellation retires only matching source");
        Cast(ws, source, 147, target: pc.InstanceID);
        foreach (var c in module.Components) c.OnCastFinished(source, cast);
        Check(component.CurrentBaits.Count == 2, "obsolete FireII finish cannot erase new cast identity");
        ws.Execute(new ActorState.OpDead(source.InstanceID, true)); module.Update();
        Check(component.CurrentBaits.Count == 1, "FireII source death clears its bait");
        ws.Execute(new ActorState.OpDestroy(source.InstanceID));
        var recreated = Create(ws, 2, Signifer);
        Check(component.CurrentBaits.Count == 1, "recycled FireII actor ID cannot revive old bait");
        Cast(ws, recreated, 147, target: pc.InstanceID); module.Update();
        Check(component.CurrentBaits.Count == 2, "new FireII actor generation supports new bait");
        ws.Execute(new ActorState.OpDestroy(pc.InstanceID)); module.Update();
        Check(component.CurrentBaits.Count == 1 && component.CurrentBaits[0].Target == other, "disappearing target clears only matching FireII bait");
        ws.Execute(new ActorState.OpDead(other.InstanceID, true)); module.Update();
        Check(component.CurrentBaits.Count == 0, "dead target cannot retain FireII bait");
        ws.Execute(new ActorState.OpCastInfo(recreated.InstanceID, null));
        Cast(ws, recreated, 147, target: 0xABCDEF); module.Update();
        Check(component.CurrentBaits.Count == 0, "missing FireII target creates no fabricated target or ground circle");
        Check(DirectHints(module, other).ActionsToExecute.Entries.Count == 0, "FireII guidance queues no unsolicited mitigation or healing");
    }

    private sealed class NavigationFixture(WorldState ws) : BossMod.QuestBattle.QuestBattle(ws);

    private static void SetField(object target, string field, object? value)
        => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.SetValue(target, value);

    private static void Waypoints(BossMod.QuestBattle.QuestBattle quest, params (Vector3 Position, bool Specified)[] values)
    {
        var type = typeof(BossMod.QuestBattle.QuestBattle).GetNestedType("NavigationWaypoint", BindingFlags.NonPublic)!;
        var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type))!;
        foreach (var value in values)
            list.Add(Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, [value.Position, value.Specified], null));
        typeof(BossMod.QuestBattle.QuestBattle).GetField("CurrentWaypoints", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(quest, list);
    }

    private static AIHintsBuilder Builder(WorldState ws, BossModule module, ZoneModule quest)
    {
        // Exercise real Update dispatch with explicit owners, without constructing live managers,
        // plugin IPC or obstacle-map services. The active-module branch uses only these fields.
        var bossManager = (BossModuleManager)RuntimeHelpers.GetUninitializedObject(typeof(BossModuleManager));
        SetField(bossManager, "_activeModule", module);
        var zoneManager = (ZoneModuleManager)RuntimeHelpers.GetUninitializedObject(typeof(ZoneModuleManager));
        zoneManager.ActiveModule = quest;
        var builder = (AIHintsBuilder)RuntimeHelpers.GetUninitializedObject(typeof(AIHintsBuilder));
        SetField(builder, "_ws", ws); SetField(builder, "_bmm", bossManager); SetField(builder, "_zmm", zoneManager);
        return builder;
    }

    private static void QuestBattleHandoff()
    {
        var config = Service.Config.Get<ZoneModuleConfig>();
        var oldEnabled = config.EnableQuestBattles; var oldDash = config.UseDash; var oldShow = config.ShowWaypoints;
        var oldMovement = BossMod.Autorotation.MiscAI.NormalMovement.Instance;
        try
        {
            // Presence enables the normal QuestBattle guard; this inert placeholder is never executed.
            BossMod.Autorotation.MiscAI.NormalMovement.Instance = (BossMod.Autorotation.MiscAI.NormalMovement)
                RuntimeHelpers.GetUninitializedObject(typeof(BossMod.Autorotation.MiscAI.NormalMovement));
            config.EnableQuestBattles = true; config.UseDash = true; config.ShowWaypoints = false;
            var ws = World(); var rope = Create(ws, 1, Rope, targetable: false, type: ActorType.EventObj);
            var enemy = Create(ws, 2, Signifer); enemy.InCombat = true;
            var pc = Create(ws, 3, 0, ally: true, type: ActorType.Player); pc.InCombat = true; pc.Class = Class.NIN;
            ws.Execute(new PartyState.OpModify(0, new(0, pc.InstanceID, false)));
            using var module = new Quest.EscapeFromCastrumCentri(ws, rope); module.Update();
            Check(module.KeepQuestBattleHints, "Centri opts into combat QuestBattle handoff explicitly");
            var unknownAnchor = Create(ws, 99, 0xFFFFFFFE, targetable: false, type: ActorType.EventObj);
            using var defaultModule = new DefaultHintPolicy(ws, unknownAnchor);
            SetField(defaultModule, "StateMachine", new StateMachine([new(new() { ID = 1, Duration = 10000 }, "fixture", 10000)]));
            defaultModule.Update();
            Check(!defaultModule.KeepQuestBattleHints, "unrelated modules retain opt-out default");
            using var quest = new NavigationFixture(ws);
            var navCompletions = 0; var nextObjectiveHints = 0;
            var destination = pc.PosRot.AsVector3() + new Vector3(20, 0, 0);
            var firstObjective = new BossMod.QuestBattle.QuestObjective(ws).PauseForCombat(false).CompleteAtDestination();
            firstObjective.OnNavigationComplete += () => ++navCompletions;
            quest.Objectives = [firstObjective, new BossMod.QuestBattle.QuestObjective(ws).Hints((_, _) => ++nextObjectiveHints)];
            Waypoints(quest, (pc.PosRot.AsVector3(), false), (destination, true));
            var hints = new AIHints();
            hints.AddForbiddenZone(new AOEShapeCircle(1), new WPos(destination), default, Now.AddSeconds(1));
            quest.CalculateCombatAIHints(0, pc, hints);
            var target = new WPos(destination);
            Check(hints.GoalZones.Count == 1 && hints.GoalZones[0](target) > hints.GoalZones[0](pc.Position), "combat waypoint produces a navigation preference after recursively skipping reached waypoint");
            Check(hints.GoalZones[0](target + new WDir(.3f, 0)) > hints.GoalZones[0](target + new WDir(5, 0))
                && hints.GoalZones[0](target + new WDir(5, 0)) > hints.GoalZones[0](target + new WDir(20, 0)), "combat waypoint gradient remains informative across map cell centers rather than disappearing outside quarter-yalm disk");
            Check(hints.ForcedMovement == null && hints.ActionsToExecute.Entries.Count == 0, "combat navigation emits neither forced movement nor dash despite dash being enabled");
            Check(hints.ForbiddenZones.Count == 1 && hints.ForbiddenZones[0].shapeDistance.Distance(target) < 0, "waypoint preference preserves existing timed hazard at destination");
            Check(!firstObjective.Completed && quest.CurrentObjectiveIndex == 0, "waypoint goal alone cannot complete objective before arrival");
            ws.Execute(new ActorState.OpMove(pc.InstanceID, new Vector4(destination.X + .8f, destination.Y, destination.Z, 0)));
            quest.CalculateCombatAIHints(0, pc, new());
            Check(!firstObjective.Completed, "combat waypoint does not complete outside three-quarter-yalm arrival tolerance");
            ws.Execute(new ActorState.OpMove(pc.InstanceID, new Vector4(destination.X + .7f, destination.Y, destination.Z, 0)));
            quest.CalculateCombatAIHints(0, pc, new());
            Check(firstObjective.Completed && navCompletions == 1, "arrival advances recursive waypoint state and completes objective once");
            quest.CalculateCombatAIHints(0, pc, new());
            Check(quest.CurrentObjectiveIndex == 1 && nextObjectiveHints == 1, "combat path advances same shared objective progression as normal QuestBattle");
            quest.CalculateCombatAIHints(0, pc, new());
            Check(navCompletions == 1, "completed waypoint callback is not repeated on later frames");

            var restricted = new AIHints();
            foreach (var priority in new[] { -4, -3, -2, -1 }) restricted.PotentialTargets.Add(new(enemy, priority, false));
            restricted.PotentialTargets[0].ForbidDOTs = true;
            restricted.PotentialTargets[0].ShouldBeInterrupted = true;
            restricted.PotentialTargets[0].ShouldBeStunned = true;
            using var priorityQuest = new NavigationFixture(ws);
            priorityQuest.Objectives = [new BossMod.QuestBattle.QuestObjective(ws).PauseForCombat(false)
                .Hints((_, h) => { h.PrioritizeAll(); h.PrioritizeTargetsByOID(Signifer, 1); })];
            priorityQuest.CalculateCombatAIHints(0, pc, restricted);
            Check(restricted.PotentialTargets.Select(e => e.Priority).SequenceEqual(new[] { -4, -3, -2, -1 }), "combat objective strategy cannot erase any incoming native negative exclusion");
            Check(restricted.PotentialTargets[0].ForbidDOTs && restricted.PotentialTargets[0].ShouldBeInterrupted && restricted.PotentialTargets[0].ShouldBeStunned, "combat objective priority handoff preserves explicit control and DOT fields");

            using var dispatchQuest = new NavigationFixture(ws);
            dispatchQuest.Objectives = [new BossMod.QuestBattle.QuestObjective(ws).PauseForCombat(false).Hints((_, h) => h.PrioritizeAll())];
            destination = pc.PosRot.AsVector3() + new Vector3(-10, 0, 0);
            Waypoints(dispatchQuest, (destination, true));
            var generator = Create(ws, 4, Generator); var highGenerator = Create(ws, 6, Generator);
            ws.Execute(new ActorState.OpMove(highGenerator.InstanceID, new Vector4(-453, 20, -258, 0)));
            Cast(ws, enemy, 1304, location: new(destination.X, 0, destination.Z));
            var builder = Builder(ws, module, dispatchQuest); var built = new AIHints(); builder.Update(built, 0, false);
            Check(built.GoalZones.Any(g => g(new WPos(destination)) > 0), "real AIHintsBuilder active-module branch retains quest waypoint goals");
            Check(built.PotentialTargets.Single(e => e.Actor == generator).Priority == 4 && built.PotentialTargets.Single(e => e.Actor == enemy).Priority == 5, "builder applies quest hints before final encounter priorities");
            Check(built.PotentialTargets.Single(e => e.Actor == highGenerator).Priority == AIHints.Enemy.PriorityInvincible, "builder's native height exclusion survives quest PrioritizeAll and module objective priority");
            Check(built.ForbiddenZones.Count == 1 && built.ForcedMovement == null && built.ActionsToExecute.Entries.Count == 0, "builder combines encounter hazards with non-forcing quest goals");
            var existingQuestType = typeof(BossModule).Assembly.GetType("BossMod.QuestBattle.ARealmReborn.MSQ.EscapeFromCastrumCentri", true)!;
            using var existingQuest = (BossMod.QuestBattle.QuestBattle)Activator.CreateInstance(existingQuestType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [ws], null)!;
            ws.Execute(new ActorState.OpMove(pc.InstanceID, new Vector4(-480, 0, -258, 0)));
            existingQuest.CalculateCombatAIHints(0, pc, new());
            Check(existingQuest.CurrentObjectiveIndex == 1, "real Centri quest progresses prison-to-exterior condition through shared combat hint path");
            var actualBuilder = Builder(ws, module, existingQuest); var actualHints = new AIHints(); actualBuilder.Update(actualHints, 0, false);
            Check(actualHints.PotentialTargets.Single(e => e.Actor == generator).Priority == 4
                && actualHints.PotentialTargets.Single(e => e.Actor == highGenerator).Priority == AIHints.Enemy.PriorityInvincible,
                "real Centri generator objective preserves native exclusion before final module reprioritization");
            ws.Execute(new ActorState.OpDead(generator.InstanceID, true));
            existingQuest.CalculateCombatAIHints(0, pc, new());
            Check(existingQuest.CurrentObjectiveIndex == 2, "real generator death callback advances to Vanguard objective during active module");
            existingQuest.CalculateCombatAIHints(0, pc, new());
            Check(existingQuest.CurrentObjectiveIndex == 2, "same completed generator cannot skip the subsequent Vanguard objective");
            var optOut = Builder(ws, defaultModule, dispatchQuest); var omitted = new AIHints(); optOut.Update(omitted, 0, false);
            Check(omitted.GoalZones.Count == 0, "builder does not inject quest navigation into modules without opt-in");
            config.EnableQuestBattles = false;
            var disabled = new AIHints(); dispatchQuest.CalculateCombatAIHints(0, pc, disabled);
            Check(disabled.GoalZones.Count == 0 && disabled.ForcedMovement == null, "combat handoff respects disabled quest automation configuration");
        }
        finally
        {
            config.EnableQuestBattles = oldEnabled; config.UseDash = oldDash; config.ShowWaypoints = oldShow;
            BossMod.Autorotation.MiscAI.NormalMovement.Instance = oldMovement;
        }
    }

    private sealed class DefaultHintPolicy(WorldState ws, Actor primary) : BossModule(ws, primary, new(-453, -258), new ArenaBoundsSquare(20))
    {
        protected override bool CheckPull() => true;
    }
}
