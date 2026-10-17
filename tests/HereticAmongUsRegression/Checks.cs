using System.Numerics;
using BossMod;
using BossMod.Components;
using Quest = BossMod.RealmReborn.Quest.MSQ.TheHereticAmongUs;

static class Checks
{
    // Synthetic policy/lifecycle fixtures, not physical geometry or live evidence.
    private const uint Drillemont = 0x6D6, Human = 0x6D2, Dragon = 0x6D3, Wyrmhound = 0x6D4, Wyvern = 0x6D5;
    private static readonly DateTime Now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
    private static int assertions;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        ++assertions;
    }

    private static WorldState World() => new(1, "synthetic")
    {
        Frame = new(Now, 0, 0, 0, 0, 1), CurrentZone = 302, CurrentCFCID = 344
    };

    private static Actor Create(WorldState ws, ulong id, uint oid, bool ally = false, bool targetable = true,
        WPos pos = default, float hitbox = .5f, ActorType type = ActorType.Enemy)
    {
        ws.Execute(new ActorState.OpCreate(id, oid, (int)id, 0, "", 0, type, Class.None, 40,
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

    private static GenericAOEs.AOEInstance[] Hazards(BossModule module, Actor pc)
    {
        var result = new List<GenericAOEs.AOEInstance>();
        foreach (var component in module.Components.OfType<GenericAOEs>())
            result.AddRange(component.ActiveAOEs(0, pc).ToArray());
        return result.ToArray();
    }

    private static AIHints DirectHints(BossModule module, Actor pc)
    {
        var hints = new AIHints();
        foreach (var component in module.Components) component.AddAIHints(0, pc, default, hints);
        return hints;
    }

    public static void Run(string gameDirectory)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"));
        Service.LuminaGameData = game;
        Service.Config.Initialize();
        ActivationAndPriorities(); TransformationAndDutyLifecycle(); CastGeometryAndLifecycle(); TargetedForecasts();
        Console.WriteLine($"The Heretic among Us synthetic regression: {assertions} checks passed.");
    }

    private static void ActivationAndPriorities()
    {
        var ws = World();
        var anchor = Create(ws, 1, Drillemont, ally: true, targetable: false);
        var human = Create(ws, 2, Human);
        var dragon = Create(ws, 3, Dragon);
        var hound = Create(ws, 4, Wyrmhound);
        var wyvern = Create(ws, 5, Wyvern);
        var pc = Create(ws, 6, 0, ally: true, type: ActorType.Player);
        var unrelated = Create(ws, 7, 0x1234);
        var friendly = Create(ws, 8, Wyvern, ally: true);
        var hidden = Create(ws, 9, Wyrmhound, targetable: false);
        var dead = Create(ws, 10, Human);
        ws.Execute(new ActorState.OpDead(dead.InstanceID, true));
        using var module = new Quest.TheHereticAmongUs(ws, anchor); module.Update();
        Check(BossModuleRegistry.FindByOID(Drillemont)?.ModuleType == module.GetType(), "generated registry uses stable Drillemont anchor");
        Check(module.StateMachine.ActiveState == null, "ambient enemies without combat do not start the encounter");
        unrelated.InCombat = true; module.Update();
        Check(module.StateMachine.ActiveState == null, "unrelated enemy combat does not activate quest");
        human.InCombat = true; module.Update();
        Check(module.StateMachine.ActiveState != null, "known hostile combat activates even with untargetable friendly anchor");
        Check(!module.ShouldPrioritizeAllEnemies, "quest does not globally promote passive unrelated enemies");
        var h = Hints(module, pc, (human, 0), (dragon, 7), (hound, 0), (wyvern, 7), (unrelated, 8), (friendly, 6), (hidden, 6), (dead, 6));
        Check(h.PotentialTargets[0].Priority == 1 && h.PotentialTargets[1].Priority == 1, "both boss forms have eligible priority one");
        Check(h.PotentialTargets[2].Priority == 3 && h.PotentialTargets[3].Priority == 3, "both reinforcement types outrank boss");
        Check(h.PotentialTargets[4].Priority == 8, "unrelated positive target priority preserved");
        Check(h.PotentialTargets[5].Priority == 6, "friendly same-OID target preserved");
        Check(h.PotentialTargets[6].Priority == 6, "untargetable reinforcement is not promoted");
        Check(h.PotentialTargets[7].Priority == 6, "dead boss form is not reprioritized");
        ws.Execute(new ActorState.OpTargetable(anchor.InstanceID, true));
        Check(module.GetDefaultTarget(0) == null, "friendly Drillemont is never supplied as rotation's fallback target");
        foreach (var priority in new[] { -4, -3, -2, -1 })
        {
            var negative = Hints(module, pc, (human, priority), (dragon, priority), (hound, priority), (wyvern, priority), (unrelated, priority));
            Check(negative.PotentialTargets.All(e => e.Priority == priority), $"all native exclusions {priority} survive on boss, adds and unrelated actors");
            Check(module.GetDefaultTarget(0) == null, $"no friendly fallback when every eligible known enemy has exclusion {priority}");
        }
        Check(h.PotentialTargets.All(e => !e.ShouldBeStunned && !e.ShouldBeInterrupted && !e.ForbidDOTs), "priorities do not invent control or DOT restrictions");
        Check(DirectHints(module, pc).ActionsToExecute.Entries.Count == 0, "module components queue no unsolicited abilities or consumables");
    }

    private static void TransformationAndDutyLifecycle()
    {
        var ws = World();
        var anchor = Create(ws, 1, Drillemont, ally: true, targetable: false);
        var human = Create(ws, 2, Human); human.InCombat = true;
        var pc = Create(ws, 3, 0, ally: true, type: ActorType.Player);
        using var module = new Quest.TheHereticAmongUs(ws, anchor); module.Update();
        var components = module.Components.ToArray();
        Check(module.StateMachine.ActiveState != null && components.Length != 0, "quest has generated active components");
        ws.Execute(new ActorState.OpDead(human.InstanceID, true)); module.Update();
        Check(module.StateMachine.ActiveState != null, "human form death is not whole-duty completion");
        ws.Execute(new ActorState.OpDestroy(human.InstanceID)); module.Update();
        Check(module.StateMachine.ActiveState != null && components.SequenceEqual(module.Components), "human disappearance preserves existing components through empty transformation gap");
        ws.Frame = new(Now.AddSeconds(5), 0, 0, 0, 0, 1); module.Update();
        Check(module.StateMachine.ActiveState != null, "empty enemy gap on later frame cannot end encounter");
        ws.Execute(new ActorState.OpTargetable(anchor.InstanceID, true));
        Check(module.GetDefaultTarget(0) == null, "empty transformation gap cannot select targetable friendly anchor");
        var dragon = Create(ws, 2, Dragon); dragon.InCombat = true; module.Update();
        Check(module.StateMachine.ActiveState != null && components.SequenceEqual(module.Components), "replacement dragon generation continues same encounter");
        var add = Create(ws, 4, Wyvern); add.InCombat = true;
        ws.Execute(new ActorState.OpDead(add.InstanceID, true)); ws.Execute(new ActorState.OpDestroy(add.InstanceID)); module.Update();
        Check(module.StateMachine.ActiveState != null, "reinforcement death does not end duty");
        ws.Execute(new ActorState.OpDead(dragon.InstanceID, true)); ws.Execute(new ActorState.OpDestroy(dragon.InstanceID)); module.Update();
        Check(module.StateMachine.ActiveState != null, "boss death does not prematurely discard pending reinforcement guidance");
        anchor.InCombat = false;
        ws.Execute(new ActorState.OpTargetable(anchor.InstanceID, false));
        Check(!module.CheckReset(), "anchor combat loss and untargetability do not request reset");
        ws.CurrentCFCID = 0;
        Check(module.CheckReset(), "leaving exact content condition requests reset");
        ws.CurrentCFCID = 344; ws.CurrentZone = 303;
        Check(module.CheckReset(), "leaving exact territory requests reset");
        ws.CurrentZone = 302;
        Check(!module.CheckReset(), "return to exact duty clears wrong-duty reset condition");
        ws.Execute(new ActorState.OpDead(anchor.InstanceID, true)); module.Update();
        Check(module.Components.Count == 0 && Hazards(module, pc).Length == 0, "Drillemont death clears every component and hazard");
        var next = World(); var nextAnchor = Create(next, 1, Drillemont, ally: true, targetable: false); var nextHuman = Create(next, 2, Human); nextHuman.InCombat = true;
        using var retry = new Quest.TheHereticAmongUs(next, nextAnchor); retry.Update();
        Check(retry.StateMachine.ActiveState != null, "new attempt has fresh active lifecycle");
        next.Execute(new ActorState.OpDestroy(nextAnchor.InstanceID)); retry.Update();
        Check(retry.Components.Count == 0, "anchor destruction clears phase even without explicit death");
        foreach (var wrongZone in new[] { false, true })
        {
            var outside = World();
            if (wrongZone) outside.CurrentZone = 303; else outside.CurrentCFCID = 0;
            var outsideAnchor = Create(outside, 1, Drillemont, ally: true, targetable: false);
            var outsideEnemy = Create(outside, 2, Human); outsideEnemy.InCombat = true;
            using var wrong = new Quest.TheHereticAmongUs(outside, outsideAnchor); wrong.Update();
            Check(wrong.StateMachine.ActiveState == null, "matching enemy combat cannot activate outside exact duty");
        }
    }

    private sealed record AOETest(uint SourceOID, uint Action, float Hitbox, float Radius, float HalfAngle = 0, bool Ground = false);

    private static ActorCastInfo Cast(WorldState ws, Actor source, uint aid, ulong? target = null,
        Vector3? location = null, Angle rotation = default, ActionType type = ActionType.Spell)
    {
        var cast = new ActorCastInfo
        {
            Action = new(type, aid), TargetID = target ?? source.InstanceID,
            Location = location ?? source.PosRot.AsVector3(), Rotation = rotation, TotalTime = 2.2f
        };
        ws.Execute(new ActorState.OpCastInfo(source.InstanceID, cast));
        // OpCastInfo clones the request object before dispatching callbacks.
        // Duplicate/tail/identity fixtures must use that actual native instance.
        return source.CastInfo!;
    }

    private static void CastGeometryAndLifecycle()
    {
        AOETest[] cases = [new(Wyrmhound, 497, 2.2f, 5.2f), new(Wyrmhound, 499, 2.2f, 8.2f, 45),
            new(Wyvern, 1136, 2.88f, 10.88f, 45), new(Dragon, 738, 4.5f, 5f, Ground: true)];
        foreach (var c in cases)
        {
            var ws = World();
            var anchor = Create(ws, 1, Drillemont, ally: true, targetable: false);
            var source = Create(ws, 2, c.SourceOID, hitbox: c.Hitbox); source.InCombat = true;
            var second = Create(ws, 3, c.SourceOID, pos: new(40, 0), hitbox: c.Hitbox);
            var pc = Create(ws, 4, 0, ally: true, type: ActorType.Player);
            var friendly = Create(ws, 5, c.SourceOID, ally: true, pos: new(80, 0), hitbox: c.Hitbox);
            var unrelated = Create(ws, 6, 0x1234, pos: new(120, 0));
            var wrongSource = Create(ws, 7, c.SourceOID == Human ? Dragon : Human, pos: new(160, 0));
            var nonSpell = Create(ws, 8, c.SourceOID, pos: new(200, 0));
            var origin = c.Ground ? new Vector3(15, 0, 10) : source.PosRot.AsVector3();
            var rotation = new Angle(.4f);
            var cast = Cast(ws, source, c.Action, location: origin, rotation: rotation);
            var otherCast = Cast(ws, second, c.Action, rotation: new Angle(-.7f));
            Cast(ws, friendly, c.Action); Cast(ws, unrelated, c.Action); Cast(ws, wrongSource, c.Action);
            Cast(ws, nonSpell, c.Action, type: ActionType.Ability);
            using var module = new Quest.TheHereticAmongUs(ws, anchor); module.Update();
            var initial = Hazards(module, pc);
            Check(initial.Length == 2, $"{c.Action}: bootstrap keeps simultaneous valid casts, excludes ally/unknown/wrong-source/non-spell");
            var first = initial.Single(a => a.ActorID == source.InstanceID);
            Check(first.Origin == new WPos(origin.X, origin.Z), $"{c.Action}: captured correct cast origin");
            Check(Math.Abs(first.Rotation.Rad - rotation.Rad) < .00001, $"{c.Action}: captured cast rotation");
            Check(Math.Abs((first.Activation - Now).TotalSeconds - 2.5) < .001, $"{c.Action}: native NPC correction included in captured deadline");
            Check(first.Check(first.Origin + rotation.ToDirection() * (c.Radius - .02f)), $"{c.Action}: nominal inner forward boundary forbidden");
            Check(!first.Check(first.Origin + rotation.ToDirection() * (c.Radius + .02f)), $"{c.Action}: nominal outer forward boundary available");
            Check(first.Check(first.Origin - rotation.ToDirection()) == (c.HalfAngle == 0), $"{c.Action}: circle/cone rear semantics");
            if (c.HalfAngle > 0)
            {
                Check(first.Check(first.Origin + (rotation + (c.HalfAngle - .2f).Degrees()).ToDirection() * 2), $"{c.Action}: inside supported half-angle");
                Check(!first.Check(first.Origin + (rotation + (c.HalfAngle + .2f).Degrees()).ToDirection() * 2), $"{c.Action}: outside supported half-angle");
            }
            if (c.Ground)
            {
                Check(first.Shape is AOEShapeCircle circle && circle.Radius == 5f, "738: ground circle does not add dragon hitbox");
                Check(!first.Check(source.Position), "738: distant cast location is used rather than caster position");
                ws.Execute(new ActorState.OpMove(source.InstanceID, new Vector4(-15, 0, -10, 2)));
                Check(Hazards(module, pc).Single(a => a.ActorID == source.InstanceID).Origin == first.Origin, "738: caster movement cannot drag fixed ground circle");
            }
            Check(Hints(module, pc).ForbiddenZones.Count == 2, $"{c.Action}: independent timed constraints delivered to AI");
            if (c.Action == 499)
                Check(Hints(module, pc).ForbiddenDirections.Count == 0, "499: Stone Gaze is a cone to avoid, not an invented look-away mechanic");
            foreach (var component in module.Components) component.OnCastStarted(source, cast);
            Check(Hazards(module, pc).Length == 2, $"{c.Action}: repeated start callback does not duplicate instance");
            ws.Frame = new(Now.AddSeconds(2.25), 0, 0, 0, 0, 1); cast.ElapsedTime = 2.25f;
            Check(Hazards(module, pc).Single(a => a.ActorID == source.InstanceID).Activation == first.Activation, $"{c.Action}: raw elapsed tail cannot drift captured deadline");
            ws.Execute(new ActorState.OpCastInfo(source.InstanceID, null));
            Check(Hazards(module, pc).Length == 1 && Hazards(module, pc)[0].ActorID == second.InstanceID, $"{c.Action}: cancellation retires matching caster only");
            Check(Hazards(module, pc)[0].Activation == Now.AddSeconds(2.5), $"{c.Action}: concurrent caster deadline survives another cancellation");
            var replacementCast = Cast(ws, source, c.Action, location: origin, rotation: rotation);
            Check(Hazards(module, pc).Length == 2 && Hazards(module, pc).Single(a => a.ActorID == source.InstanceID).Activation > first.Activation, $"{c.Action}: fresh cast has independent later deadline");
            foreach (var component in module.Components) component.OnCastFinished(source, cast);
            Check(Hazards(module, pc).Length == 2, $"{c.Action}: stale prior-cast finish cannot remove the replacement cast");
            ws.Execute(new ActorState.OpDead(source.InstanceID, true));
            Check(Hazards(module, pc).Length == 1, $"{c.Action}: source death removes its hazard before finish");
            ws.Execute(new ActorState.OpDestroy(source.InstanceID));
            var recreated = Create(ws, 2, c.SourceOID, hitbox: c.Hitbox);
            Check(Hazards(module, pc).Length == 1, $"{c.Action}: recreated numeric ID does not revive prior actor's cast");
            Cast(ws, recreated, c.Action, location: origin, rotation: rotation);
            Check(Hazards(module, pc).Length == 2, $"{c.Action}: recreated actor can create a fresh hazard");
            ws.Execute(new ActorState.OpDestroy(recreated.InstanceID));
            Check(Hazards(module, pc).Length == 1, $"{c.Action}: destruction without cast finish removes only recreated source");
            ws.Execute(new ActorState.OpCastInfo(second.InstanceID, null));
            Check(Hazards(module, pc).Length == 0 && Hints(module, pc).ForbiddenZones.Count == 0, $"{c.Action}: final cancellation clears all hazards and constraints");
            Cast(ws, second, 9999);
            Check(Hazards(module, pc).Length == 0, $"{c.Action}: unknown action on known source is not fabricated as a hazard");
            Check(DirectHints(module, pc).ActionsToExecute.Entries.Count == 0, $"{c.Action}: hazards queue no abilities or forced consumables");
            ws.Execute(new ActorState.OpDead(anchor.InstanceID, true)); module.Update();
            Check(module.Components.Count == 0 && Hazards(module, pc).Length == 0, $"{c.Action}: phase completion clears remaining component state");
        }
    }

    private static void TargetedForecasts()
    {
        var ws = World();
        var anchor = Create(ws, 1, Drillemont, ally: true);
        var human = Create(ws, 2, Human); human.InCombat = true;
        var dragon = Create(ws, 3, Dragon);
        var pc = Create(ws, 4, 0, ally: true, type: ActorType.Player);
        var other = Create(ws, 5, 0, ally: true, type: ActorType.Player);
        ws.Execute(new PartyState.OpModify(0, new(0, pc.InstanceID, false)));
        ws.Execute(new PartyState.OpModify(1, new(0, other.InstanceID, false)));
        ws.Execute(new PartyState.OpModify(24, new(0, anchor.InstanceID, false)));
        var ruin = Cast(ws, human, 31085, target: anchor.InstanceID);
        var dart = Cast(ws, dragon, 1164, target: pc.InstanceID);
        var friendly = Create(ws, 6, Human, ally: true); Cast(ws, friendly, 31085, target: pc.InstanceID);
        var wrong = Create(ws, 7, Wyrmhound); Cast(ws, wrong, 31085, target: pc.InstanceID);
        var unknown = Create(ws, 8, 0x1234); Cast(ws, unknown, 1164, target: pc.InstanceID);
        using var module = new Quest.TheHereticAmongUs(ws, anchor); module.Update();
        var initial = Hints(module, pc);
        Check(initial.PredictedDamage.Count == 2, "targeted damage bootstraps both correct casts, excludes allied/wrong-source/unknown casters");
        Check(initial.PredictedDamage.Any(d => d.Players.Raw == (1ul << 24)) && initial.PredictedDamage.Any(d => d.Players.Raw == 1), "damage forecasts target Drillemont and player independently, without unaffected player");
        Check(initial.PredictedDamage.All(d => d.Type != AIHints.PredictedDamageType.Tankbuster), "ordinary targeted magic does not invent tankbuster classification");
        Check(initial.PredictedDamage.All(d => d.Activation == Now.AddSeconds(2.5)), "both targeted forecasts capture native corrected deadline");
        Check(initial.ForbiddenZones.Count == 0 && Hazards(module, pc).Length == 0, "single-target damage does not create a dodgeable area");
        foreach (var component in module.Components) { component.OnCastStarted(human, ruin); component.OnCastStarted(dragon, dart); }
        Check(Hints(module, pc).PredictedDamage.Count == 2, "repeated targeted cast callbacks do not duplicate damage forecasts");
        ws.Frame = new(Now.AddSeconds(2.25), 0, 0, 0, 0, 1); ruin.ElapsedTime = dart.ElapsedTime = 2.25f;
        Check(Hints(module, pc).PredictedDamage.All(d => d.Activation == Now.AddSeconds(2.5)), "Ruin and Spirit Dart forecast deadlines stay fixed through raw timer tail");
        Cast(ws, human, 31085, target: anchor.InstanceID);
        foreach (var component in module.Components) component.OnCastFinished(human, ruin);
        Check(Hints(module, pc).PredictedDamage.Count == 2, "obsolete Ruin finish cannot clear fresh replacement forecast");
        ws.Execute(new ActorState.OpCastInfo(human.InstanceID, null));
        Check(Hints(module, pc).PredictedDamage.Count == 1 && Hints(module, pc).PredictedDamage[0].Players.Raw == 1, "human cast cancellation removes only Drillemont's forecast");
        ws.Execute(new ActorState.OpDead(dragon.InstanceID, true));
        Check(Hints(module, pc).PredictedDamage.Count == 0, "dead dragon cannot retain Spirit Dart prediction");
        ws.Execute(new ActorState.OpDestroy(dragon.InstanceID));
        var recreated = Create(ws, 3, Dragon);
        Check(Hints(module, pc).PredictedDamage.Count == 0, "numeric caster ID reuse does not resurrect old damage forecast");
        Cast(ws, recreated, 1164, target: other.InstanceID);
        Check(Hints(module, pc).PredictedDamage.Count == 1 && Hints(module, pc).PredictedDamage[0].Players.Raw == 2, "new caster generation forecasts only its actual new target");
        ws.Execute(new ActorState.OpDestroy(other.InstanceID));
        Check(Hints(module, pc).PredictedDamage.Count == 0, "missing target cannot retain an actionable damage forecast");
        ws.Execute(new ActorState.OpCastInfo(recreated.InstanceID, null));
        Cast(ws, recreated, 1164, target: pc.InstanceID, type: ActionType.Ability);
        Check(Hints(module, pc).PredictedDamage.Count == 0, "numeric action ID with non-spell type is excluded from forecasts");
        Cast(ws, human, 1085, target: pc.InstanceID);
        Check(Hints(module, pc).PredictedDamage.Count == 0, "same-name unrelated Ruin action1085 is not substituted for recorded31085");
        Cast(ws, human, 31085, target: anchor.InstanceID);
        ws.Execute(new PartyState.OpModify(24, default));
        Check(Hints(module, pc).PredictedDamage.Count == 0 && module.CalculateGlobalHints(pc).Contains("Ruin"), "unregistered ally target retains incoming text without invented party mask");
        ws.Execute(new PartyState.OpModify(24, new(0, anchor.InstanceID, false)));
        Check(Hints(module, pc).PredictedDamage.Count == 1 && Hints(module, pc).PredictedDamage[0].Players.Raw == (1ul << 24), "restoring observed party membership restores only exact ally forecast");
        Check(DirectHints(module, pc).ActionsToExecute.Entries.Count == 0, "damage guidance does not directly queue healing or mitigation");
    }
}
