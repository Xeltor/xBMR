using System.Numerics;
using BossMod;
using BossMod.Components;
using Quest = BossMod.RealmReborn.Quest.MSQ.TheLominsanWay;

static class Checks
{
    // These synthetic fixtures exercise the observed branches and edge cases;
    // they are not additional physical geometry, timing or live evidence.
    private const uint Large = 0x6EA, Small = 0x6EB, Trachtoum = 0x6E9, Bomb = 0x6EC, Rive = 1299, Paralyze = 1118;
    private const string IncomingParalyze = "Paralyze incoming: prepare to cleanse if possible.";
    private const string BombWarning = "Bomb active: finish the Large Boulder before it explodes!";
    private static int assertions;
    private static readonly DateTime Now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        ++assertions;
    }

    private static WorldState World() => new(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1), CurrentZone = 307, CurrentCFCID = 354 };

    private static Actor Create(WorldState ws, ulong id, uint oid, bool targetable = true, bool ally = false, ActorType type = ActorType.Enemy)
    {
        ws.Execute(new ActorState.OpCreate(id, oid, (int)id, 0, "", 0, type, Class.None, 30,
            new Vector4(695, 0, -292, 0), 0.5f, new(100, 100, 0, 0, 0), targetable, ally, 0, 0, 0));
        return ws.Actors.Find(id)!;
    }

    private static ActorCastInfo Cast(WorldState ws, Actor actor, float remaining = 2.2f, uint action = Rive, ulong? target = null, ActionType type = ActionType.Spell)
    {
        var cast = new ActorCastInfo { Action = new(type, action), TargetID = target ?? actor.InstanceID,
            Location = actor.PosRot.AsVector3(), TotalTime = remaining };
        ws.Execute(new ActorState.OpCastInfo(actor.InstanceID, cast));
        return cast;
    }

    private static AIHints DirectHints(BossModule module, Actor pc, int slot)
    {
        var hints = new AIHints();
        foreach (var component in module.Components)
            component.AddAIHints(slot, pc, default, hints);
        return hints;
    }

    private static bool HasIncoming(BossModule module, Actor pc) => module.CalculateHintsForRaidMember(0, pc).Any(h => h.Item1 == IncomingParalyze && h.Item2);

    private static AIHints Hints(BossModule module, Actor pc, params (Actor Actor, int Priority)[] inputs)
    {
        var hints = new AIHints();
        foreach (var (actor, priority) in inputs) hints.PotentialTargets.Add(new(actor, priority, false));
        module.CalculateAIHints(0, pc, default, hints);
        return hints;
    }

    public static void Run(string gameDirectory)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"));
        Service.LuminaGameData = game;
        Service.Config.Initialize();
        ActivationAndPriorities(); CastLifecycle(); RaceLifecycle(); ParalyzeAndCleanse(); BombLifecycle();
        Console.WriteLine($"The Lominsan Way synthetic regression: {assertions} checks passed.");
    }

    private static void ActivationAndPriorities()
    {
        var ws = World();
        var large = Create(ws, 1, Large, targetable: false);
        var small = Create(ws, 2, Small);
        var trachtoum = Create(ws, 3, Trachtoum);
        var pc = Create(ws, 4, 0, type: ActorType.Player);
        var unrelated = Create(ws, 5, 0x1234);
        var ally = Create(ws, 6, Small, ally: true);
        var bomb = Create(ws, 7, Bomb);
        using var module = new Quest.TheLominsanWay(ws, large);
        module.Update();
        Check(BossModuleRegistry.FindByOID(Large)?.ModuleType == module.GetType(), "registered on large boulder");
        Check(module.StateMachine.ActiveState == null, "hidden large boulder does not activate race");
        ws.Execute(new ActorState.OpTargetable(large.InstanceID, true)); module.Update();
        Check(module.StateMachine.ActiveState != null, "targetability activates race without combat flags");
        Check(!module.ShouldPrioritizeAllEnemies, "race does not broadly promote unrelated enemies");
        var h = Hints(module, pc, (large, -3), (small, -1), (trachtoum, 4), (unrelated, 7), (ally, 5), (bomb, -1));
        Check(h.PotentialTargets[0].Priority == 1, "passive large boulder becomes attackable priority");
        Check(h.PotentialTargets[1].Priority == -4, "wrong small boulder remains forbidden even when pending death");
        Check(h.PotentialTargets[2].Priority == -4, "Trachtoum cannot be hit incidentally");
        Check(h.PotentialTargets[1].ForbidDOTs && h.PotentialTargets[2].ForbidDOTs, "both wrong targets forbid DOTs");
        Check(h.PotentialTargets[3].Priority == 7 && !h.PotentialTargets[3].ForbidDOTs, "unrelated enemy untouched");
        Check(h.PotentialTargets[4].Priority == 5 && !h.PotentialTargets[4].ForbidDOTs, "friendly actor untouched");
        Check(h.PotentialTargets[5].Priority == -4 && h.PotentialTargets[5].ForbidDOTs, "unobserved bomb branch is excluded from attacks and DOTs");
        foreach (var priority in new[] { -4, -2, -1, 0, 6 })
            Check(Hints(module, pc, (large, priority)).PotentialTargets[0].Priority == (priority < 0 ? priority : 1), $"large priority {priority} contract");
        foreach (var priority in new[] { -4, -3, -2, -1, 0 })
            Check(Hints(module, pc, (small, priority)).PotentialTargets[0].Priority == -4, $"strict small-boulder exclusion from priority {priority}");
        pc.TargetID = small.InstanceID;
        Check(module.CalculateHintsForRaidMember(0, pc).Any(), "wrong current target produces player guidance");
        pc.TargetID = bomb.InstanceID;
        Check(module.CalculateHintsForRaidMember(0, pc).Any(), "unobserved bomb target produces player guidance");
        pc.TargetID = large.InstanceID;
        var baseline = module.CalculateHintsForRaidMember(0, pc).Count;
        ws.Execute(new ActorState.OpStatus(pc.InstanceID, 0, new ActorStatus(17, 0, Now.AddSeconds(10), trachtoum.InstanceID)));
        Check(module.CalculateHintsForRaidMember(0, pc).Count > baseline, "present paralysis adds warning");
        var directHints = new AIHints();
        foreach (var component in module.Components)
            component.AddAIHints(0, pc, default, directHints);
        Check(directHints.ActionsToExecute.Entries.Count == 0, "paralysis warning queues no automatic consumables or abilities before module queue filtering");
        ws.Execute(new ActorState.OpStatus(pc.InstanceID, 0, default));
        Check(module.CalculateHintsForRaidMember(0, pc).Count == baseline, "paralysis loss removes warning");
    }

    private static void CastLifecycle()
    {
        var ws = World();
        var large = Create(ws, 1, Large);
        var trachtoum = Create(ws, 2, Trachtoum);
        var pc = Create(ws, 3, 0, type: ActorType.Player);
        var cast = Cast(ws, trachtoum);
        using var module = new Quest.TheLominsanWay(ws, large); module.Update();
        var rive = module.Components.OfType<SimpleAOEs>().Single();
        Check(rive.Casters.Count == 1, "activation bootstraps already-running Rive");
        Check(Math.Abs((rive.Casters[0].Activation - Now).TotalSeconds - 2.5) < .001, "Rive deadline includes native NPC correction");
        Check(rive.Shape.Check(trachtoum.Position + new WDir(.99f, 30.49f), trachtoum.Position, default), "Rive inner corner dangerous");
        Check(!rive.Shape.Check(trachtoum.Position + new WDir(1.01f, 20f), trachtoum.Position, default), "Rive half-width is one yalm");
        Check(!rive.Shape.Check(trachtoum.Position + new WDir(0, 30.51f), trachtoum.Position, default), "Rive forward boundary is 30.5 yalms");
        Check(!rive.Shape.Check(trachtoum.Position + new WDir(0, -1), trachtoum.Position, default), "Rive rear stays available");
        Check(Hints(module, pc).ForbiddenZones.Count == 1, "Rive supplies timed AI constraint");
        rive.OnCastStarted(trachtoum, cast);
        Check(rive.Casters.Count == 1, "duplicate cast callback does not duplicate Rive");
        ws.Frame = new(Now.AddSeconds(2.25), 0, 0, 0, 0, 1);
        cast.ElapsedTime = 2.25f;
        Check(rive.Casters[0].Activation == Now.AddSeconds(2.5), "raw timer tail does not shift Rive deadline");
        Check(Hints(module, pc).ForbiddenZones.Count == 1, "cast tail remains dangerous until actual cancellation or completion");
        var friendly = Create(ws, 4, Trachtoum, ally: true);
        Cast(ws, friendly);
        Check(rive.Casters.Count == 1, "friendly same-OID cast excluded");
        var stranger = Create(ws, 5, 0x1234);
        Cast(ws, stranger);
        Check(rive.Casters.Count == 1, "unrelated same-action cast excluded");
        ws.Execute(new ActorState.OpCastInfo(trachtoum.InstanceID, null));
        Check(rive.Casters.Count == 0 && Hints(module, pc).ForbiddenZones.Count == 0, "cancellation clears matching hazard and AI constraint");
        Cast(ws, trachtoum, action: 1135);
        Check(rive.Casters.Count == 0 && Hints(module, pc).ForbiddenZones.Count == 0, "same-name Rive 1135 from another variant does not create a hazard");
        ws.Execute(new ActorState.OpCastInfo(trachtoum.InstanceID, null));
        Cast(ws, trachtoum); ws.Execute(new ActorState.OpDestroy(trachtoum.InstanceID)); module.Update();
        Check(rive.Casters.Count == 0, "caster disappearance clears hazard");
        var replacement = Create(ws, 2, Trachtoum);
        Check(rive.Casters.Count == 0, "recreated numeric actor ID does not revive old cast");
        Cast(ws, replacement);
        Check(rive.Casters.Count == 1 && rive.Casters[0].ActorID == replacement.InstanceID, "new actor cast creates fresh hazard");
        ws.Execute(new ActorState.OpDead(replacement.InstanceID, true)); module.Update();
        Check(rive.Casters.Count == 0, "caster death clears hazard even before finish callback");
    }

    private static void RaceLifecycle()
    {
        var ws = World();
        var large = Create(ws, 1, Large);
        using var module = new Quest.TheLominsanWay(ws, large); module.Update();
        Check(module.StateMachine.ActiveState != null, "small boulder absence at startup is not a loss");
        module.Update();
        Check(module.StateMachine.ActiveState != null, "small boulder absence on later updates remains harmless");
        var small = Create(ws, 2, Small); module.Update();
        Check(module.StateMachine.ActiveState != null, "small boulder arriving does not end race");
        ws.Execute(new ActorState.OpDestroy(small.InstanceID)); module.Update();
        Check(module.Components.Count == 0, "previously seen small boulder disappearing ends lost race");
        using var retry = new Quest.TheLominsanWay(ws, large); retry.Update();
        Check(retry.StateMachine.ActiveState != null, "retry without small boulder starts with fresh lifecycle state");
        ws.Execute(new ActorState.OpDead(large.InstanceID, true)); retry.Update();
        Check(retry.Components.Count == 0, "large boulder death ends successful race");
        var nextWorld = World();
        var nextLarge = Create(nextWorld, 1, Large);
        var nextSmall = Create(nextWorld, 2, Small);
        using var loss = new Quest.TheLominsanWay(nextWorld, nextLarge); loss.Update();
        ws = nextWorld;
        ws.Execute(new ActorState.OpDead(nextSmall.InstanceID, true)); loss.Update();
        Check(loss.Components.Count == 0, "observed small-boulder death ends loss");
        var finalWorld = World();
        var finalLarge = Create(finalWorld, 1, Large);
        using var fresh = new Quest.TheLominsanWay(finalWorld, finalLarge); fresh.Update();
        finalWorld.CurrentCFCID = 0;
        Check(fresh.CheckReset(), "leaving the exact duty requests reset");
        finalWorld.CurrentCFCID = 354;
        Check(!fresh.CheckReset(), "correct duty does not reset active race");
        finalWorld.Execute(new ActorState.OpDestroy(finalLarge.InstanceID)); fresh.Update();
        Check(fresh.Components.Count == 0, "large boulder disappearance clears race guidance");
    }

    private static void ParalyzeAndCleanse()
    {
        var ws = World();
        var large = Create(ws, 1, Large);
        var trachtoum = Create(ws, 2, Trachtoum);
        var pc = Create(ws, 3, 0, type: ActorType.Player);
        var other = Create(ws, 4, 0, type: ActorType.Player);
        Cast(ws, trachtoum, remaining: 3.7f, action: Paralyze, target: pc.InstanceID);
        using var module = new Quest.TheLominsanWay(ws, large); module.Update();
        Check(HasIncoming(module, pc), "already-running targeted Paralyze produces early warning on activation");
        Check(!HasIncoming(module, other), "Paralyze warning is limited to its actual target");
        Check(Hints(module, pc).ForbiddenZones.Count == 0, "single-target Paralyze does not invent an avoidable AOE");
        Check(DirectHints(module, pc, 0).ShouldCleanse.None(), "cast warning alone does not request cleansing an absent status");
        Check(DirectHints(module, pc, 0).ActionsToExecute.Entries.Count == 0, "Paralyze cast warning queues no actions directly");
        Cast(ws, trachtoum, action: Paralyze, target: other.InstanceID);
        Check(!HasIncoming(module, pc) && HasIncoming(module, other), "replacement cast target updates player-specific warning");
        Cast(ws, trachtoum, action: Paralyze, target: pc.InstanceID, type: ActionType.Ability);
        Check(!HasIncoming(module, pc), "non-spell action with Paralyze numeric ID does not warn");
        Cast(ws, trachtoum, action: 9999, target: pc.InstanceID);
        Check(!HasIncoming(module, pc), "unrelated spell by Trachtoum does not warn as Paralyze");
        ws.Execute(new ActorState.OpCastInfo(trachtoum.InstanceID, null));
        var ally = Create(ws, 5, Trachtoum, ally: true);
        Cast(ws, ally, action: Paralyze, target: pc.InstanceID);
        Check(!HasIncoming(module, pc), "friendly Trachtoum-like caster does not produce hostile warning");
        var stranger = Create(ws, 6, 0x1234);
        Cast(ws, stranger, action: Paralyze, target: pc.InstanceID);
        Check(!HasIncoming(module, pc), "unrelated enemy using same action does not create quest-specific warning");
        Cast(ws, trachtoum, action: Paralyze, target: pc.InstanceID);
        Check(HasIncoming(module, pc), "new hostile Paralyze cast produces warning");
        ws.Execute(new ActorState.OpCastInfo(trachtoum.InstanceID, null));
        Check(!HasIncoming(module, pc), "Paralyze cancellation promptly clears warning");
        Cast(ws, trachtoum, action: Paralyze, target: pc.InstanceID);
        ws.Execute(new ActorState.OpDead(trachtoum.InstanceID, true));
        Check(!HasIncoming(module, pc), "dead caster cannot retain incoming Paralyze warning");
        ws.Execute(new ActorState.OpDead(trachtoum.InstanceID, false));
        ws.Execute(new ActorState.OpDestroy(trachtoum.InstanceID));
        Check(!HasIncoming(module, pc), "destroyed caster cannot retain incoming Paralyze warning");
        var replacement = Create(ws, 2, Trachtoum);
        Check(!HasIncoming(module, pc), "recreated caster ID does not revive former warning");
        Cast(ws, replacement, action: Paralyze, target: pc.InstanceID);
        Check(HasIncoming(module, pc), "recreated actor's fresh targeted cast creates a fresh warning");
        ws.Execute(new ActorState.OpCastInfo(replacement.InstanceID, null));
        ws.Execute(new ActorState.OpStatus(pc.InstanceID, 0, new ActorStatus(17, 0, Now.AddSeconds(10), replacement.InstanceID)));
        Check(module.CalculateHintsForRaidMember(0, pc).Any(h => h.Item1 == "Paralyzed: cleanse if possible." && h.Item2), "present status retains exact current-paralysis warning");
        Check(DirectHints(module, pc, 5).ShouldCleanse.Raw == (1ul << 5), "paralysis requests cleansing only supplied affected party slot");
        Check(DirectHints(module, other, 4).ShouldCleanse.None(), "unaffected player does not receive cleanse request");
        Check(DirectHints(module, pc, -1).ShouldCleanse.None() && DirectHints(module, pc, 64).ShouldCleanse.None(), "invalid party slots cannot wrap cleanse bit");
        var merged = new AIHints(); merged.ShouldCleanse.Set(3);
        foreach (var component in module.Components) component.AddAIHints(5, pc, default, merged);
        Check(merged.ShouldCleanse.Raw == ((1ul << 3) | (1ul << 5)), "cleanse guidance preserves another component's existing request");
        Check(merged.ActionsToExecute.Entries.Count == 0, "cleanse request does not directly queue Esuna or consumables");
        ws.Execute(new ActorState.OpDead(pc.InstanceID, true));
        Check(DirectHints(module, pc, 5).ShouldCleanse.None(), "dead affected player does not request cleanse");
        ws.Execute(new ActorState.OpDead(pc.InstanceID, false));
        ws.Execute(new ActorState.OpStatus(pc.InstanceID, 0, default));
        Check(DirectHints(module, pc, 5).ShouldCleanse.None(), "actual paralysis loss removes cleanse request");
        ws.Execute(new ActorState.OpStatus(pc.InstanceID, 0, new ActorStatus(7, 0, Now.AddSeconds(10), replacement.InstanceID)));
        Check(DirectHints(module, pc, 5).ShouldCleanse.None(), "Silence status7 does not masquerade as quest paralysis17");
    }

    private static void BombLifecycle()
    {
        var ws = World();
        var large = Create(ws, 1, Large);
        var pc = Create(ws, 2, 0, type: ActorType.Player);
        var bomb = Create(ws, 3, Bomb, targetable: false);
        using var module = new Quest.TheLominsanWay(ws, large); module.Update();
        Check(module.CalculateGlobalHints(pc).Count(h => h == BombWarning) == 1, "already-present bomb warns even before targetable");
        Check(Hints(module, pc).ForbiddenZones.Count == 0, "bomb objective race does not invent an explosion dodge area");
        Check(DirectHints(module, pc, 0).ActionsToExecute.Entries.Count == 0, "bomb warning queues no attack interrupt or stun");
        ws.Execute(new ActorState.OpDead(bomb.InstanceID, true));
        Check(!module.CalculateGlobalHints(pc).Contains(BombWarning), "dead bomb no longer produces active warning");
        ws.Execute(new ActorState.OpDead(bomb.InstanceID, false));
        Check(module.CalculateGlobalHints(pc).Contains(BombWarning), "living bomb restores active warning");
        ws.Execute(new ActorState.OpDestroy(bomb.InstanceID));
        Check(!module.CalculateGlobalHints(pc).Contains(BombWarning), "bomb destruction clears warning promptly");
        var ally = Create(ws, 4, Bomb, ally: true);
        Check(!module.CalculateGlobalHints(pc).Contains(BombWarning), "friendly matching-OID bomb does not warn");
        var stranger = Create(ws, 5, 0x1234);
        Check(!module.CalculateGlobalHints(pc).Contains(BombWarning), "unrelated living enemy does not warn as bomb");
        var replacement = Create(ws, 3, Bomb, targetable: false);
        Check(module.CalculateGlobalHints(pc).Count(h => h == BombWarning) == 1, "new spawned bomb creates one current warning");
        Create(ws, 6, Bomb);
        Check(module.CalculateGlobalHints(pc).Count(h => h == BombWarning) == 1, "concurrent bombs do not duplicate global urgency text");
        ws.Execute(new ActorState.OpDead(large.InstanceID, true)); module.Update();
        Check(module.Components.Count == 0 && !module.CalculateGlobalHints(pc).Contains(BombWarning), "objective phase completion clears bomb warning while bomb still lives");
    }
}
