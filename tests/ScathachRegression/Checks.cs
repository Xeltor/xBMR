using System.Numerics;
using BossMod;
using BossMod.Components;
using BossMod.Heavensward.Alliance.A34Scathach;

static partial class Checks
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        ++assertions;
    }
    private static Actor Actor(ulong id, uint oid, WPos pos)
        => new(id, oid, 0, 0, "", 0, ActorType.Enemy, Class.None, 60, new Vector4(pos.X, 0, pos.Z, 0));
    private static ActorCastInfo Cast(uint aid, WPos pos, float seconds = 3.7f, Angle rotation = default)
        => new() { Action = new(ActionType.Spell, aid), Location = new(pos.X, 0, pos.Z), TotalTime = seconds, Rotation = rotation };
    private static ActorCastEvent Hit(uint aid, WPos pos = default, ulong target = 0)
        => new(new(ActionType.Spell, aid), target, 0, 0, new(pos.X, 0, pos.Z), 0, 0, default);
    private static T Component<T>(BossModule module, string name) where T : BossComponent
        => (T)module.Components.Single(c => c.GetType().Name == name);
    private static A34Scathach Module(params Actor[] actors)
    {
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        foreach (var actor in actors) world.Actors.Actors.Add(actor.InstanceID, actor);
        var primary = Actor(1, (uint)OID.Boss, new(0, -50));
        var module = new A34Scathach(world, primary);
        module.StateMachine.Start(Now);
        return module;
    }
    private static AIHints Hints(BossModule module, BossComponent component, Actor actor)
    {
        var hints = new AIHints { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
        component.AddAIHints(0, actor, default, hints);
        return hints;
    }

    public static void Run(string gameDirectory)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"), new Lumina.LuminaOptions());
        Service.LuminaGameData = game;
        Service.Config.Initialize();
        ShadespinBranches();
        ShadespinLifecycle();
        ThirtySickles();
        StackAndCleave();
        Nox();
        EmergenceAndPitfall();
        ShadowLinks();
        Limbs();
        UnhandWarning();
        Towers();
        MeleeWarnings();
        EnemyPriorities();
        Console.WriteLine($"{assertions} Scathach assertions passed (synthetic callbacks; physical geometry, server snapshots and live gameplay remain unverified).");
    }

    private static void ShadespinBranches()
    {
        foreach (var (aid, offset) in new[] { (7455u, 45f), (7454u, -45f) })
        {
            using var module = Module();
            var component = Component<GenericAOEs>(module, "Shadespin");
            var rotation = 23f.Degrees();
            var spell = Cast(aid, module.Center, rotation: rotation);
            component.OnCastStarted(module.PrimaryActor, spell);
            var aoes = component.ActiveAOEs(0, module.PrimaryActor).ToArray();
            Check(aoes.Length == 2, "both diagonal cones exist at the early boss visual");
            Check(aoes[0].Rotation.AlmostEqual(rotation + offset.Degrees(), 0.001f), "visual ID selects observed diagonal branch");
            Check(aoes[1].Rotation.AlmostEqual(rotation + (offset + 180).Degrees(), 0.001f), "opposite cone remains separately represented");
            Check(aoes.All(a => a.Activation == module.CastFinishAt(spell, 1.1d)), "prediction has future AI deadline instead of immediate movement");
            var hints = Hints(module, component, module.PrimaryActor);
            Check(hints.ForbiddenZones.Count == 2 && hints.ForbiddenZones.All(z => z.activation == aoes[0].Activation), "AI consumes both early cone deadlines");
            var unsafePoint = module.Center + aoes[0].Rotation.ToDirection() * 20;
            var safePoint = module.Center + (aoes[0].Rotation + 90.Degrees()).ToDirection() * 20;
            Check(aoes.Any(a => a.Check(unsafePoint)) && aoes.All(a => !a.Check(safePoint)), "early branch preserves opposite safe quadrants");
        }
    }

    private static void EmergenceAndPitfall()
    {
        var helper = Actor(20, (uint)OID.Connla, new(0, -50));
        using var module = Module(helper);
        var component = Component<BossComponent>(module, "BlindingShadow");
        module.WorldState.Actors.Actors.Add(module.PrimaryActor.InstanceID, module.PrimaryActor);
        foreach (var slot in new[] { 0, 8, 16 })
        {
            var player = Actor((ulong)(30 + slot), 0, module.Center);
            module.WorldState.Actors.Actors.Add(player.InstanceID, player);
            module.WorldState.Execute(new PartyState.OpModify(slot, new(0, player.InstanceID, false)));
        }
        var spell = Cast(7476, module.Center, 4.7f);
        module.WorldState.Execute(new ActorState.OpCastInfo(module.PrimaryActor.InstanceID, spell));
        var hints = Hints(module, component, module.PrimaryActor);
        Check(hints.PredictedDamage.Count == 1 && hints.PredictedDamage[0].Activation == module.CastFinishAt(module.PrimaryActor.CastInfo, 4d),
            "native emergence cast creates early damage guidance at corrected visual finish plus the bounded four-second estimate");
        Check(hints.PredictedDamage[0].Players[0] && hints.PredictedDamage[0].Players[8] && hints.PredictedDamage[0].Players[16],
            "Blinding Shadow prediction includes all three alliances");
        var global = new BossComponent.GlobalHints();
        component.AddGlobalHints(module.PrimaryActor, global);
        Check(global.Any(h => h.Contains("Blinding Shadow") && h.Contains("estimated")), "emergence warning exposes its estimated timing");
        module.WorldState.Frame = new(Now.AddSeconds(5.03), 0, 0, 0, 0, 1);
        module.WorldState.Execute(new ActorState.OpCastEvent(module.PrimaryActor.InstanceID, Hit(7476)));
        module.WorldState.Execute(new ActorState.OpCastInfo(module.PrimaryActor.InstanceID, null));
        Check(Hints(module, component, module.PrimaryActor).PredictedDamage.Count == 1,
            "native non-damaging visual resolution and following cast-end preserve the pending delayed raidwide");
        module.WorldState.Frame = new(Now.AddSeconds(9.05), 0, 0, 0, 0, 1);
        module.WorldState.Execute(new ActorState.OpCastEvent(helper.InstanceID, Hit(7598)));
        Check(Hints(module, component, module.PrimaryActor).PredictedDamage.Count == 0, "actual helper damage retires emergence guidance");

        foreach (var cleanup in new[] { "cancel", "phase", "dead", "destroyed", "stale" })
        {
            using var fresh = Module();
            var warning = Component<BossComponent>(fresh, "BlindingShadow");
            var cast = Cast(7476, fresh.Center, 4.7f);
            warning.OnCastStarted(fresh.PrimaryActor, cast);
            switch (cleanup)
            {
                case "cancel": warning.OnCastFinished(fresh.PrimaryActor, cast); break;
                case "phase": warning.OnActorUntargetable(fresh.PrimaryActor); break;
                case "dead": fresh.PrimaryActor.IsDead = true; break;
                case "destroyed": warning.OnActorDestroyed(fresh.PrimaryActor); break;
                case "stale":
                    fresh.WorldState.Frame = new(Now.AddSeconds(12), 0, 0, 0, 0, 1);
                    // A late visual cannot revive a cue even before an ordinary Update.
                    warning.OnEventCast(fresh.PrimaryActor, Hit(7476));
                    break;
            }
            global.Clear();
            warning.AddGlobalHints(fresh.PrimaryActor, global);
            Check(global.Count == 0, cleanup + " clears the delayed emergence warning");
        }
        using var reset = Module();
        global.Clear();
        Component<BossComponent>(reset, "BlindingShadow").AddGlobalHints(reset.PrimaryActor, global);
        Check(global.Count == 0, "fresh pull has no inherited emergence warning");
        var pitfall = Component<CastHint>(reset, "Pitfall");
        pitfall.OnCastStarted(helper, Cast(7377, reset.Center, 4.7f));
        global.Clear();
        pitfall.AddGlobalHints(reset.PrimaryActor, global);
        Check(global.Any(h => h.Contains("Proximity") && h.Contains("move away") && h.Contains("Connla")),
            "Pitfall expresses proximity response and incoming add without inventing a safe falloff radius");
    }

    private static void ShadespinLifecycle()
    {
        using var module = Module();
        var component = Component<GenericAOEs>(module, "Shadespin");
        var visual = Cast(7455, module.Center);
        component.OnCastStarted(module.PrimaryActor, visual);
        component.OnEventCast(module.PrimaryActor, Hit(7455));
        component.OnCastFinished(module.PrimaryActor, visual);
        Check(component.ActiveAOEs(0, module.PrimaryActor).Length == 2, "visual resolution does not prematurely clear predicted hits");
        var first = Actor(20, (uint)OID.Connla, module.Center);
        var second = Actor(21, (uint)OID.Connla, module.Center);
        var firstCast = Cast(7456, module.Center, 0.9f, 45.Degrees());
        var secondCast = Cast(7456, module.Center, 0.9f, 225.Degrees());
        component.OnCastStarted(first, firstCast);
        component.OnCastStarted(first, firstCast);
        Check(component.ActiveAOEs(0, first).Length == 2, "helper correction is idempotent and retains unstarted opposite prediction");
        component.OnCastStarted(second, secondCast);
        component.OnEventCast(first, Hit(7456));
        component.OnCastFinished(first, firstCast);
        Check(component.ActiveAOEs(0, first).Length == 1 && component.ActiveAOEs(0, first)[0].ActorID == second.InstanceID, "first helper hit and duplicate cleanup retain second helper");
        component.OnActorDestroyed(second);
        Check(component.ActiveAOEs(0, first).Length == 0, "despawn clears remaining helper");
        component.OnCastStarted(module.PrimaryActor, visual);
        component.OnCastFinished(module.PrimaryActor, visual);
        Check(component.ActiveAOEs(0, first).Length == 0, "cancelled boss visual clears predictions");
        component.OnCastStarted(module.PrimaryActor, visual);
        component.OnActorDestroyed(module.PrimaryActor);
        Check(component.ActiveAOEs(0, first).Length == 0, "boss destruction clears the pair");
        using var reset = Module();
        Check(Component<GenericAOEs>(reset, "Shadespin").ActiveAOEs(0, first).Length == 0, "fresh pull has no stale cone predictions");
    }

    private static void StackAndCleave()
    {
        using var module = Module();
        var stack = Component<IconStackSpread>(module, "ThirtyCries");
        var target = Actor(10, 0, new(5, -50));
        stack.OnEventIcon(target, 62, target.InstanceID);
        module.WorldState.Frame = new(Now.AddSeconds(1), 0, 0, 0, 0, 1);
        stack.OnEventIcon(target, 62, target.InstanceID);
        Check(stack.Stacks.Count == 1 && stack.Stacks[0].Radius == 12, "stack marker activates a single stack instead of an avoidable cleave");
        Check(stack.Stacks[0].Activation == Now.AddSeconds(5.1), "stack uses observed marker lead time");
        stack.OnEventCast(module.PrimaryActor, Hit(7475, target: target.InstanceID));
        Check(stack.Stacks.Count == 0, "stack resolution clears its marker");
        stack.OnEventIcon(target, 62, target.InstanceID);
        target.IsDead = true;
        stack.Update();
        Check(stack.Stacks.Count == 0, "dead target cannot leave stale stack guidance");
        target.IsDead = false;
        stack.OnEventIcon(target, 62, target.InstanceID);
        module.WorldState.Frame = new(Now.AddSeconds(9), 0, 0, 0, 0, 1);
        stack.Update();
        Check(stack.Stacks.Count == 0, "missing stack resolution expires rather than retaining an assignment indefinitely");
        stack.OnEventIcon(target, 62, target.InstanceID);
        module.PrimaryActor.IsDead = true;
        stack.Update();
        Check(stack.Stacks.Count == 0, "boss death cancels the second recorded unfinished stack marker");
        module.PrimaryActor.IsDead = false;
        using (var recovery = Module())
        {
            var recoveringStack = Component<IconStackSpread>(recovery, "ThirtyCries");
            recoveringStack.OnEventCast(recovery.PrimaryActor, Hit(7475, target: target.InstanceID));
            Check(recoveringStack.CastCounter == 0, "Thirty Cries without a marker cannot poison the next single-hit counter");
            recoveringStack.OnEventIcon(target, 62, target.InstanceID);
            recovery.WorldState.Frame = new(Now.AddSeconds(8), 0, 0, 0, 0, 1);
            recoveringStack.Update();
            recoveringStack.OnEventCast(recovery.PrimaryActor, Hit(7475, target: target.InstanceID));
            recoveringStack.OnEventIcon(target, 62, target.InstanceID);
            recoveringStack.OnEventCast(recovery.PrimaryActor, Hit(7475, target: target.InstanceID));
            Check(recoveringStack.Stacks.Count == 0 && recoveringStack.CastCounter == 0,
                "expired marker and late hit still allow the next fresh marker to resolve exactly once");
        }
        var cleave = Component<SimpleAOEs>(module, "Shadethrust");
        var spell = Cast(7459, new(80, 90), 2.7f);
        cleave.OnCastStarted(module.PrimaryActor, spell);
        cleave.OnCastStarted(module.PrimaryActor, spell);
        Check(cleave.Casters.Count == 1 && cleave.Casters[0].Origin == module.PrimaryActor.Position, "Shadethrust originates at caster, not distant aim point");
        Check(cleave.Casters[0].Check(new(2.4f, -40)) && !cleave.Casters[0].Check(new(2.6f, -40)), "full width5 is interpreted as half width2.5");
        cleave.OnCastFinished(module.PrimaryActor, spell);
        Check(cleave.Casters.Count == 0, "Shadethrust retires on cast completion");
    }

    private static void Nox()
    {
        var target = Actor(10, 0, new(5, -50));
        using var module = Module(target);
        var component = Component<GenericChasingAOEs>(module, "Nox");
        component.OnEventIcon(target, 197, target.InstanceID);
        Check(component.Chasers.Count == 1 && component.Chasers[0].NumRemaining == 6
            && component.Chasers[0].NextActivation == Now.AddSeconds(5), "Nox icon supplies early guidance for all six observed hits");
        module.WorldState.Frame = new(Now.AddSeconds(0.05), 0, 0, 0, 0, 1);
        component.OnEventIcon(target, 197, target.InstanceID);
        Check(component.Chasers.Count == 1 && component.Chasers[0].NextActivation == Now.AddSeconds(5), "duplicate Nox icon preserves its initial deadline");
        var helper = Actor(20, (uint)OID.Connla, new(-20, -60));
        var firstLocation = new WPos(6, -50);
        var first = Cast(7458, firstLocation, 4.7f);
        component.OnCastStarted(helper, first);
        var deadline = component.Chasers[0].NextActivation;
        Check(component.Chasers[0].PrevPos == firstLocation && component.Chasers[0].MoveDist == 0
            && deadline == module.CastFinishAt(first), "first helper location locks the first circle and replaces the icon timer");
        module.WorldState.Frame = new(Now.AddSeconds(0.1), 0, 0, 0, 0, 1);
        component.OnCastStarted(helper, first);
        Check(component.Chasers.Count == 1 && component.Chasers[0].NextActivation == deadline, "duplicate helper start cannot postpone the first Nox hit");
        var lockedOrigin = component.ActiveAOEs(0, target)[0].Origin;
        target.PosRot = new(12, 0, -50, 0);
        Check(component.ActiveAOEs(0, target)[0].Origin == lockedOrigin,
            "moving after the first helper starts does not drag its already locked circle");
        module.WorldState.Frame = new(Now.AddSeconds(5.1), 0, 0, 0, 0, 1);
        component.OnEventCast(helper, Hit(7458, firstLocation));
        component.OnCastFinished(helper, first);
        Check(component.Chasers.Count == 1 && component.Chasers[0].NumRemaining == 5
            && component.Chasers[0].MoveDist == 4.5f
            && Math.Abs((component.Chasers[0].NextActivation - Now.AddSeconds(6.05)).TotalSeconds) < 0.000001,
            "first Nox hit preserves five repeats with the observed step and conservative repeat deadline");
        var hints = Hints(module, component, target);
        Check(hints.ForbiddenZones.Count == 1 && hints.ForbiddenZones[0].activation == component.Chasers[0].NextActivation,
            "Nox AI receives the next explosion deadline rather than the old late 1.6s timer");
        for (var repeat = 1; repeat <= 4; ++repeat)
        {
            module.WorldState.Frame = new(Now.AddSeconds(5.1 + repeat * 1.02), 0, 0, 0, 0, 1);
            component.OnEventCast(Actor((ulong)(20 + repeat), (uint)OID.Connla, module.Center), Hit(7457, new(6 - repeat * 4.5f, -50)));
        }
        Check(component.Chasers.Count == 1 && component.Chasers[0].NumRemaining == 1, "Nox fifth explosion retains warning for the recorded sixth hit");
        module.WorldState.Frame = new(Now.AddSeconds(10.2), 0, 0, 0, 0, 1);
        component.OnEventCast(helper, Hit(7457, new(-16.5f, -50)));
        Check(component.Chasers.Count == 0, "only the sixth explosion retires the chase");

        foreach (var cleanup in new[] { "cancel", "helper destroyed", "target dead", "boss phase", "stale icon", "stale repeat" })
        {
            target.IsDead = false;
            using var fresh = Module(target);
            var nox = Component<GenericChasingAOEs>(fresh, "Nox");
            nox.OnEventIcon(target, 197, target.InstanceID);
            if (cleanup != "stale icon" && cleanup != "target dead")
                nox.OnCastStarted(helper, first);
            switch (cleanup)
            {
                case "cancel": nox.OnCastFinished(helper, first); break;
                case "helper destroyed": nox.OnActorDestroyed(helper); break;
                case "target dead": target.IsDead = true; nox.Update(); break;
                case "boss phase": nox.OnActorUntargetable(fresh.PrimaryActor); break;
                case "stale icon": fresh.WorldState.Frame = new(Now.AddSeconds(7.1), 0, 0, 0, 0, 1); nox.Update(); break;
                case "stale repeat":
                    nox.OnEventCast(helper, Hit(7458, firstLocation));
                    fresh.WorldState.Frame = new(Now.AddSeconds(3), 0, 0, 0, 0, 1);
                    nox.Update();
                    break;
            }
            Check(nox.Chasers.Count == 0, cleanup + " clears obsolete Nox guidance");
        }
        target.IsDead = false;
        using var reset = Module(target);
        Check(Component<GenericChasingAOEs>(reset, "Nox").Chasers.Count == 0, "fresh pull cannot inherit a Nox chase");

        var other = Actor(11, 0, new(-15, -50));
        using var concurrent = Module(target, other);
        var concurrentNox = Component<GenericChasingAOEs>(concurrent, "Nox");
        concurrentNox.OnEventIcon(target, 197, target.InstanceID);
        concurrentNox.OnEventIcon(other, 197, other.InstanceID);
        concurrentNox.OnCastStarted(helper, Cast(7458, target.Position, 4.7f));
        var otherHelper = Actor(21, (uint)OID.Connla, module.Center);
        concurrentNox.OnCastStarted(otherHelper, Cast(7458, other.Position, 4.7f));
        concurrentNox.OnCastFinished(helper, first);
        Check(concurrentNox.Chasers.Count == 1 && concurrentNox.Chasers[0].Target == other,
            "cancelling one target's first helper preserves the other target's chase");

        using var generations = Module(target);
        var generationNox = Component<GenericChasingAOEs>(generations, "Nox");
        generationNox.OnEventIcon(target, 197, target.InstanceID);
        generationNox.OnCastStarted(helper, Cast(7458, target.Position, 4.7f));
        target.IsDestroyed = true;
        var replacement = Actor(target.InstanceID, 0, new(-15, -50));
        var replacementHelper = Actor(helper.InstanceID, (uint)OID.Connla, module.Center);
        generationNox.OnEventIcon(replacement, 197, replacement.InstanceID);
        generationNox.OnCastStarted(replacementHelper, Cast(7458, replacement.Position, 4.7f));
        Check(generationNox.Chasers.Count == 2,
            "new target generation can receive a marker while the old generation's locked initial strike remains");
        generationNox.OnActorDestroyed(helper);
        generationNox.OnCastFinished(helper, first);
        Check(generationNox.Chasers.Count == 1 && generationNox.Chasers[0].Target == replacement,
            "late destruction and cancellation of an old helper generation preserve the replacement sequence");
    }

    private static void ThirtySickles()
    {
        using var module = Module();
        var component = Component<GenericKnockback>(module, "ThirtySickles");
        var visual = Cast(7455, module.Center);
        var helper = Actor(20, (uint)OID.Connla, module.Center);
        component.OnCastStarted(module.PrimaryActor, visual);
        Check(component.ActiveKnockbacks(0, module.PrimaryActor).Length == 0, "unresolved visual alone cannot start the conditional knockback prediction");
        component.OnEventCast(module.PrimaryActor, Hit(7455));
        component.OnCastFinished(module.PrimaryActor, visual);
        component.OnEventCast(helper, Hit(7456));
        Check(component.ActiveKnockbacks(0, module.PrimaryActor).Length == 1
            && component.ActiveKnockbacks(0, module.PrimaryActor)[0].Activation == Now.AddSeconds(3.9)
            && component.ActiveKnockbacks(0, module.PrimaryActor)[0].Distance == 10,
            "resolved known Shadespin branch predicts the sheet-supported radial knockback with an early deadline");
        component.OnEventCast(helper, Hit(7456));
        Check(component.ActiveKnockbacks(0, module.PrimaryActor).Length == 1, "second helper cannot duplicate a knockback pulse");

        // Off-center positioning creates both safe and lethal landing candidates inside the 15y effect.
        module.PrimaryActor.PosRot = new(20, 0, -50, 0);
        var player = Actor(30, 0, new(25, -50));
        var hints = Hints(module, component, player);
        Check(hints.ForbiddenZones.Count == 1 && hints.ForbiddenZones[0].activation == Now.AddSeconds(3.9), "AI receives the future knockback deadline");
        Check(hints.ForbiddenZones[0].shapeDistance.Distance(new(25, -50)) <= 0
            && hints.ForbiddenZones[0].shapeDistance.Distance(new(15, -50)) > 0,
            "radial landing outside authored arena is forbidden while safe inside landing remains available");
        Check(hints.ForbiddenZones[0].shapeDistance.Distance(new(-10, -50)) > 0,
            "positions beyond the 15y affected radius are not given invented displacement");
        var overlapping = new AIHints();
        overlapping.AddForbiddenZone(new SDCircle(new(15, -50), 2), Now.AddSeconds(2));
        component.AddAIHints(0, player, default, overlapping);
        Check(overlapping.ForbiddenZones.Count == 2
            && overlapping.ForbiddenZones[0].shapeDistance.Distance(new(15, -50)) <= 0,
            "knockback guidance preserves independent earlier hazards and their deadline");

        module.WorldState.Actors.Actors.Add(player.InstanceID, player);
        module.WorldState.Execute(new PartyState.OpModify(0, new(0, player.InstanceID, false)));
        var immune = new ActorStatus(1209, 0, Now.AddSeconds(10), player.InstanceID);
        component.OnStatusGain(player, ref immune);
        Check(Hints(module, component, player).ForbiddenZones.Count == 0, "knockback planning respects immunity that covers activation");
        component.OnStatusLose(player, ref immune);
        Check(Hints(module, component, player).ForbiddenZones.Count == 1, "expired immunity restores landing safety constraint");
        component.OnEventCast(module.PrimaryActor, Hit(7473));
        Check(component.ActiveKnockbacks(0, player).Length == 0, "actual Sickles retires predicted displacement");
        helper.PosRot = module.PrimaryActor.PosRot;
        component.OnCastStarted(module.PrimaryActor, visual);
        component.OnEventCast(module.PrimaryActor, Hit(7455));
        component.OnEventCast(helper, Hit(7456));
        Check(component.ActiveKnockbacks(0, player).Length == 0, "repeated later use of the same visual is outside first-pattern evidence and is not guessed");

        var alternate = Cast(7454, module.PrimaryActor.Position);
        component.OnCastStarted(module.PrimaryActor, alternate);
        component.OnEventCast(module.PrimaryActor, Hit(7454));
        component.OnEventCast(helper, Hit(7456));
        Check(component.ActiveKnockbacks(0, player).Length == 0, "unestablished later branch is not assigned a guessed knockback timer");
        using var cancelled = Module();
        var cancelledComponent = Component<GenericKnockback>(cancelled, "ThirtySickles");
        var cancelledHelper = Actor(20, (uint)OID.Connla, cancelled.Center);
        cancelledComponent.OnCastStarted(cancelled.PrimaryActor, visual);
        cancelledComponent.OnCastFinished(cancelled.PrimaryActor, visual);
        cancelledComponent.OnEventCast(cancelledHelper, Hit(7456));
        Check(cancelledComponent.ActiveKnockbacks(0, player).Length == 0, "cancelled visual cannot arm a later helper packet");

        foreach (var callbackWithoutUpdate in new[] { false, true })
        {
            using var missingHelper = Module();
            var pending = Component<GenericKnockback>(missingHelper, "ThirtySickles");
            pending.OnCastStarted(missingHelper.PrimaryActor, visual);
            pending.OnEventCast(missingHelper.PrimaryActor, Hit(7455));
            missingHelper.WorldState.Frame = new(Now.AddSeconds(2.1), 0, 0, 0, 0, 1);
            if (!callbackWithoutUpdate)
                pending.Update();
            pending.OnEventCast(Actor(20, (uint)OID.Connla, missingHelper.Center), Hit(7456));
            Check(pending.ActiveKnockbacks(0, player).Length == 0,
                "missing helper expires before a late collocated hit, including callbacks before the next update");
        }
        using (var missingVisual = Module())
        {
            var pending = Component<GenericKnockback>(missingVisual, "ThirtySickles");
            pending.OnCastStarted(missingVisual.PrimaryActor, visual);
            missingVisual.WorldState.Frame = new(Now.AddSeconds(7), 0, 0, 0, 0, 1);
            pending.OnEventCast(missingVisual.PrimaryActor, Hit(7455));
            pending.OnEventCast(Actor(20, (uint)OID.Connla, missingVisual.Center), Hit(7456));
            Check(pending.ActiveKnockbacks(0, player).Length == 0, "cast-start timeout prevents a stale visual and helper from reviving the cue");
        }
        using (var delayedVisual = Module())
        {
            var pending = Component<GenericKnockback>(delayedVisual, "ThirtySickles");
            pending.OnCastStarted(delayedVisual.PrimaryActor, visual);
            delayedVisual.WorldState.Frame = new(Now.AddSeconds(5.5), 0, 0, 0, 0, 1);
            pending.OnEventCast(delayedVisual.PrimaryActor, Hit(7455));
            delayedVisual.WorldState.Frame = new(Now.AddSeconds(6.7), 0, 0, 0, 0, 1);
            pending.Update();
            pending.OnEventCast(Actor(20, (uint)OID.Connla, delayedVisual.Center), Hit(7456));
            Check(pending.ActiveKnockbacks(0, player).Length == 1
                && Math.Abs((pending.ActiveKnockbacks(0, player)[0].Activation - Now.AddSeconds(10.6)).TotalSeconds) < 0.000001,
                "actual visual refreshes helper deadline and preserves a valid 1.2s helper resolution");
        }

        foreach (var cleanup in new[] { "untargetable", "destroyed", "stale", "changed cast" })
        {
            using var fresh = Module();
            var freshComponent = Component<GenericKnockback>(fresh, "ThirtySickles");
            var freshHelper = Actor(20, (uint)OID.Connla, fresh.Center);
            freshComponent.OnCastStarted(fresh.PrimaryActor, visual);
            freshComponent.OnEventCast(fresh.PrimaryActor, Hit(7455));
            freshComponent.OnEventCast(freshHelper, Hit(7456));
            Check(freshComponent.ActiveKnockbacks(0, player).Length == 1, cleanup + " fixture starts with an actual pending pulse");
            switch (cleanup)
            {
                case "untargetable": freshComponent.OnActorUntargetable(fresh.PrimaryActor); break;
                case "destroyed": freshComponent.OnActorDestroyed(fresh.PrimaryActor); break;
                case "changed cast": freshComponent.OnCastStarted(fresh.PrimaryActor, Cast(7474, fresh.Center)); break;
                case "stale":
                    fresh.WorldState.Frame = new(Now.AddSeconds(5), 0, 0, 0, 0, 1);
                    freshComponent.Update();
                    break;
            }
            Check(freshComponent.ActiveKnockbacks(0, player).Length == 0, cleanup + " retires pending displacement");
        }
        using var reset = Module();
        Check(Component<GenericKnockback>(reset, "ThirtySickles").ActiveKnockbacks(0, player).Length == 0, "fresh pull resets sequence state");
    }

    private static void ShadowLinks()
    {
        using var module = Module();
        var component = Component<StayMove>(module, "ShadowLinks");
        var helper = Actor(20, (uint)OID.ShadowLinksHelper, module.Center);
        component.OnActorEAnim(helper, 0x00040008);
        var hints = Hints(module, component, module.PrimaryActor);
        Check(hints.ImminentSpecialMode.mode == AIHints.SpecialMode.NoMovement && hints.ImminentSpecialMode.activation == Now, "floor animation stops AI movement before damage status appears");
        Check(component.PlayerStates.All(s => s.Requirement == StayMove.Requirement.Stay2), "movement-only requirement covers every alliance slot");
        component.OnActorEAnim(helper, 0x00010002);
        Check(Hints(module, component, module.PrimaryActor).ImminentSpecialMode == default, "floor clear releases movement immediately");
        component.OnActorEAnim(helper, 0x00040008);
        component.OnActorDestroyed(helper);
        Check(component.PlayerStates.All(s => s.Requirement == StayMove.Requirement.None), "floor destruction clears movement lock");
    }

    private static void Limbs()
    {
        var limb = Actor(20, (uint)OID.ShadowLimb, new(10, -50));
        var target = Actor(10, 0, new(0, -50));
        limb.Tether = new(1, target.InstanceID);
        using var module = Module(limb);
        var component = Component<GenericGaze>(module, "ShadowLimbs");
        Check(component.ActiveEyes(0, target).Length == 1 && component.ActiveEyes(0, target)[0].Inverted, "tethered player receives look-at-limb guidance");
        Check(component.ActiveEyes(0, module.PrimaryActor).Length == 0, "unassigned players receive no facing constraint");
        limb.Tether = new(2, module.PrimaryActor.InstanceID);
        Check(component.ActiveEyes(0, target).Length == 0 && component.ActiveEyes(0, module.PrimaryActor).Length == 1, "alternate tether state and retargeting update assignments");
        limb.Tether = default;
        Check(component.ActiveEyes(0, module.PrimaryActor).Length == 0, "tether clear removes facing constraint");
    }

    private static void Towers()
    {
        using var module = Module();
        var component = Component<GenericTowers>(module, "ParticleBeam");
        var first = Actor(20, (uint)OID.MeteorTower, new(0, -26));
        var second = Actor(21, (uint)OID.MeteorTower, new(24, -50));
        component.OnActorCreated(first);
        component.OnActorCreated(first);
        component.OnActorCreated(second);
        Check(component.Towers.Count == 2, "event-object tower creation is idempotent");
        Check(component.Towers.All(t => t.Activation == Now.AddSeconds(9.8) && t.MinSoakers == 1 && t.MaxSoakers == int.MaxValue), "tower deadline/minimum preserves valid multiple-player soaking");
        var soaker = Actor(30, 0, first.Position);
        module.WorldState.Actors.Actors.Add(soaker.InstanceID, soaker);
        module.WorldState.Execute(new PartyState.OpModify(0, new(0, soaker.InstanceID, false)));
        var outsider = Actor(31, 0, module.Center);
        var guidance = Hints(module, component, outsider);
        // Forbidden-zone predicates are negative in forbidden space. An inverted tower circle
        // therefore keeps its interior positive and forbids the space outside the required tower.
        Check(guidance.ForbiddenZones.Count == 1 && guidance.ForbiddenZones[0].shapeDistance.Distance(first.Position) < 0
            && guidance.ForbiddenZones[0].shapeDistance.Distance(second.Position) > 0,
            "unassigned AI chooses the empty tower rather than piling onto an occupied tower");
        var holding = Hints(module, component, soaker);
        Check(holding.ForbiddenZones.Count == 1 && holding.ForbiddenZones[0].shapeDistance.Distance(first.Position) > 0
            && holding.ForbiddenZones[0].shapeDistance.Distance(second.Position) < 0,
            "existing soaker holds their tower while another tower is empty");
        component.OnEventCast(module.PrimaryActor, Hit(7464, first.Position));
        Check(component.Towers.Count == 1 && component.Towers[0].ActorID == second.InstanceID, "successful tower hit only clears the matching location");
        component.OnEventCast(module.PrimaryActor, Hit(7464, first.Position));
        Check(component.Towers.Count == 1, "duplicate resolution cannot clear another tower");
        component.OnEventCast(module.PrimaryActor, Hit(7463, second.Position));
        Check(component.Towers.Count == 0, "failed tower also retires its location");
        component.OnActorCreated(first);
        component.OnActorDestroyed(first);
        Check(component.Towers.Count == 0, "despawn clears tower guidance");
    }

    private static void MeleeWarnings()
    {
        using var module = Module();
        var thorns = Component<SimpleAOEs>(module, "ThirtyThorns4");
        thorns.OnEventCast(module.PrimaryActor, Hit(7467));
        Check(thorns.Casters.Count == 1 && thorns.Casters[0].Activation == Now.AddSeconds(4.4), "combo opener gives early timed melee burst warning");
        var helper = Actor(20, (uint)OID.Connla, module.Center);
        var spell = Cast(7470, module.Center, 1.2f);
        thorns.OnCastStarted(helper, spell);
        Check(thorns.Casters.Count == 1 && thorns.Casters[0].ActorID == helper.InstanceID, "helper cast replaces combo prediction");
        thorns.OnCastFinished(helper, spell);
        Check(thorns.Casters.Count == 0, "combo burst resolves cleanly");
        var landing = Component<SimpleAOEs>(module, "Shadesmite1");
        var destination = new WPos(17, -70);
        landing.OnEventCast(module.PrimaryActor, Hit(7451, destination));
        Check(landing.Casters.Count == 1 && landing.Casters[0].Origin == destination && landing.Casters[0].Activation == Now.AddSeconds(4.9), "Soar action predicts its recorded landing target early");
        var landingSpell = Cast(7453, destination, 1.2f);
        landing.OnCastStarted(helper, landingSpell);
        Check(landing.Casters.Count == 1 && landing.Casters[0].Activation == module.CastFinishAt(landingSpell), "landing helper corrects prediction to native cast timer");
        landing.OnActorDestroyed(helper);
        Check(landing.Casters.Count == 0, "landing helper destruction retires warning");
    }

    private static void EnemyPriorities()
    {
        using var module = Module();
        var connla = new AIHints.Enemy(Actor(20, (uint)OID.Connla2, module.Center), 0, true);
        var limb = new AIHints.Enemy(Actor(21, (uint)OID.ShadowLimb, module.Center), 0, false);
        var houndActor = Actor(22, (uint)OID.ShadowcourtHound, module.Center);
        houndActor.CastInfo = Cast(3344, module.Center);
        houndActor.CastInfo.Interruptible = true;
        var hound = new AIHints.Enemy(houndActor, 0, true);
        var forbidden = new AIHints.Enemy(Actor(23, (uint)OID.Connla2, module.Center), AIHints.Enemy.PriorityForbidden, true);
        var hints = new AIHints();
        hints.PotentialTargets.AddRange([connla, limb, hound, forbidden]);
        typeof(A34Scathach).GetMethod("CalculateModuleAIHints", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(module, [0, module.PrimaryActor, default(PartyRolesConfig.Assignment), hints]);
        Check(connla.Priority == 2 && limb.Priority == 2 && hound.Priority == 1, "dangerous adds outrank the boss without changing positions");
        Check(hound.ShouldBeInterrupted, "native interruptible flag authorizes hound cast control");
        Check(forbidden.Priority == AIHints.Enemy.PriorityForbidden && forbidden.DesiredPosition == null && connla.DesiredPosition == null,
            "forbidden targets and tank position ownership remain preserved");
        houndActor.CastInfo.Interruptible = false;
        hound.ShouldBeInterrupted = false;
        typeof(A34Scathach).GetMethod("CalculateModuleAIHints", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(module, [0, module.PrimaryActor, default(PartyRolesConfig.Assignment), hints]);
        Check(!hound.ShouldBeInterrupted, "noninterruptible hound cast does not receive invented interrupt eligibility");
    }
}
