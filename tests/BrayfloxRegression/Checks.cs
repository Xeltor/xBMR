using System.Numerics;
using System.Reflection;
using BossMod;
using BossMod.Components;
using Pelican = BossMod.RealmReborn.Dungeon.D07Brayflox.D071GreatYellowPelican;
using Drake = BossMod.RealmReborn.Dungeon.D07Brayflox.D072InfernoDrake;
using Hell = BossMod.RealmReborn.Dungeon.D07Brayflox.D073Hellbender;
using Aiatar = BossMod.RealmReborn.Dungeon.D07Brayflox.D074Aiatar;

static partial class Checks
{
    // Synthetic lifecycle and guidance checks. Nominal shapes are implementation
    // contracts, not measured physical boundaries or live gameplay acceptance.
    static int assertions;
    static readonly DateTime Now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
    delegate ReadOnlySpan<GenericAOEs.AOEInstance> Preview(int slot, Actor actor);

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        ++assertions;
    }
    static WorldState World() => new(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1), CurrentZone = 1041, CurrentCFCID = 8 };
    static Actor Create(WorldState ws, ulong id, uint oid, WPos? position = null, bool ally = false, bool targetable = true, ActorType type = ActorType.Enemy)
    {
        var p = position ?? new WPos(-26, -236);
        ws.Execute(new ActorState.OpCreate(id, oid, (int)id, 0, "", 0, type, Class.None, 30,
            new Vector4(p.X, 0, p.Z, 0), .5f, new(100, 100, 0, 0, 0), targetable, ally, 0, 0, 0));
        return ws.Actors.Find(id)!;
    }
    static Actor Player(WorldState ws, ulong id = 100, WPos? position = null, int slot = 0)
    {
        var actor = Create(ws, id, 0, position, type: ActorType.Player);
        ws.Execute(new PartyState.OpModify(slot, new(0, id, false)));
        return actor;
    }
    static void Combat(WorldState ws, Actor actor) => ws.Execute(new ActorState.OpCombat(actor.InstanceID, true));
    static ActorCastInfo Cast(WorldState ws, Actor source, uint action, float remaining, Actor? target = null, Angle rotation = default, ActionType type = ActionType.Spell)
    {
        var cast = new ActorCastInfo { Action = new(type, action), TargetID = target?.InstanceID ?? source.InstanceID,
            Location = source.PosRot.AsVector3(), Rotation = rotation, TotalTime = remaining };
        ws.Execute(new ActorState.OpCastInfo(source.InstanceID, cast));
        return cast;
    }
    static void Finish(WorldState ws, Actor source) => ws.Execute(new ActorState.OpCastInfo(source.InstanceID, null));
    static AIHints Hints(BossModule module, Actor pc, params (Actor Actor, int Priority)[] targets)
    {
        var hints = new AIHints();
        foreach (var (actor, priority) in targets) hints.PotentialTargets.Add(new(actor, priority, false));
        module.CalculateAIHints(0, pc, default, hints);
        return hints;
    }
    static SimpleAOEs AOE(BossModule module, uint action) => module.Components.OfType<SimpleAOEs>().Single(c => c.WatchedAction == action);
    static bool HasRisk(BossModule module, Actor pc) => module.CalculateHintsForRaidMember(0, pc).Any(h => h.Item2);

    public static void Run(string gameDirectory)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"));
        Service.LuminaGameData = game;
        Service.Config.Initialize();
        Priorities(); BurstCohorts(); BurstLifecycle(); FollowupLifecycle(); SlumberLifecycle(); LevinshowerGuidance();
        Console.WriteLine($"Brayflox synthetic regression: {assertions} checks passed.");
    }

    static void Priorities()
    {
        var specs = new (uint Boss, uint Add, int AddPriority, Func<WorldState, Actor, BossModule> Factory)[]
        {
            (0x1A7, 0x1A9, 3, (w,a) => new Pelican.D071GreatYellowPelican(w,a)),
            (0x3DE, 0x3DF, 2, (w,a) => new Drake.D072InfernoDrake(w,a)),
            (0x1AB, 0x428, 2, (w,a) => new Hell.D120Hellbender(w,a))
        };
        foreach (var spec in specs)
        {
            var ws = World(); var boss = Create(ws, 1, spec.Boss); var add = Create(ws, 2, spec.Add); var pc = Player(ws);
            var unrelated = Create(ws, 3, 0x9999); var friendly = Create(ws, 4, spec.Add, ally: true);
            var hidden = Create(ws, 5, spec.Add, targetable: false); var dead = Create(ws, 6, spec.Add);
            ws.Execute(new ActorState.OpDead(dead.InstanceID, true));
            Combat(ws, boss);
            using var module = spec.Factory(ws, boss); module.Update();
            Check(module.StateMachine.ActiveState != null, $"0x{spec.Boss:X} fixture activates");
            foreach (var priority in new[] { -4, -3, -2, -1 })
            {
                var h = Hints(module, pc, (boss, priority), (add, priority));
                Check(h.PotentialTargets.All(t => t.Priority == priority), $"0x{spec.Boss:X} preserves native exclusion {priority} for boss and add");
            }
            var positive = Hints(module, pc, (boss, 0), (add, 0), (unrelated, 9), (friendly, 6), (hidden, 7), (dead, 8));
            Check(positive.PotentialTargets[0].Priority == 1 && positive.PotentialTargets[1].Priority == spec.AddPriority, $"0x{spec.Boss:X} eligible targets receive encounter priorities");
            Check(positive.PotentialTargets[2].Priority == 9, $"0x{spec.Boss:X} unrelated positive priority survives");
            Check(positive.PotentialTargets[3].Priority == 6 && positive.PotentialTargets[4].Priority == 7 && positive.PotentialTargets[5].Priority == 8, $"0x{spec.Boss:X} friendly hidden and dead priorities survive");
        }
    }

    static void BurstCohorts()
    {
        var ws = World(); var boss = Create(ws, 1, 0x38C5); var pc = Player(ws, position: new(-11, -228)); Combat(ws, boss);
        var casters = new Actor[7];
        // The later cohort arrives first to detect accidental list-order truncation.
        for (var i = 0; i < 7; ++i)
        {
            casters[i] = Create(ws, (ulong)i + 10, 0x233C, i < 3 ? new(-11 + i, -228) : new(-35 + i - 3, -240));
            Cast(ws, casters[i], 28658, i < 3 ? 14 + i * .02f : 9 + (i - 3) * .02f);
        }
        using var module = new Aiatar.D074Aiatar(ws, boss); module.Update();
        var burst = AOE(module, 28658);
        var preview = burst.GetType().GetMethod("PreviewAOEs", BindingFlags.Public | BindingFlags.Instance)!.CreateDelegate<Preview>(burst);
        Check(burst.ActiveAOEs(0, pc).Length == 7, "all seven overlapping Burst casts remain in internal predictions");
        Check(Hints(module, pc).ForbiddenZones.Count == 7, "all seven Burst casts supply AI constraints");
        var before = preview(0, pc).ToArray();
        Check(before.Length == 4 && before.All(a => a.Activation <= Now.AddSeconds(9.55)), "radar shows the earliest four deadline cohort");
        Check(before.Select(a => a.ActorID).Order().SequenceEqual(casters.Skip(3).Select(c => c.InstanceID).Order()), "preview chooses earliest deadlines even when later casts arrived first");
        Check(!HasRisk(module, pc), "later cohort position is not an immediate human GTFO warning");
        var internalAOEs = burst.ActiveAOEs(0, pc).ToArray();
        Check(internalAOEs.All(a => a.Risky) && internalAOEs.Any(a => a.Check(pc.Position) && a.Activation > Now.AddSeconds(14)), "later cohort retains its future timed AI hazard");
        for (var i = 3; i < 7; ++i) Finish(ws, casters[i]);
        Check(burst.ActiveAOEs(0, pc).Length == 3 && Hints(module, pc).ForbiddenZones.Count == 3, "first cohort resolution removes exactly four internal and AI hazards");
        Check(preview(0, pc).Length == 3, "remaining three Burst hazards become the next radar cohort");
        Check(HasRisk(module, pc), "newly immediate cohort position receives human warning");
        for (var i = 0; i < 3; ++i) Finish(ws, casters[i]);
        Check(burst.ActiveAOEs(0, pc).Length == 0 && preview(0, pc).Length == 0 && Hints(module, pc).ForbiddenZones.Count == 0, "all resolved Burst state and previews clear");
    }

    static void BurstLifecycle()
    {
        var ws = World(); var boss = Create(ws, 1, 0x38C5); var helper = Create(ws, 2, 0x233C); var pc = Player(ws); Combat(ws, boss);
        var cast = Cast(ws, helper, 28658, 9);
        using var module = new Aiatar.D074Aiatar(ws, boss); module.Update();
        var burst = AOE(module, 28658);
        Check(burst.Casters.Count == 1, "activation bootstraps an existing Burst cast");
        burst.OnCastStarted(helper, cast);
        Check(burst.Casters.Count == 1, "repeated Burst callback does not duplicate hazard");
        var friendly = Create(ws, 3, 0x233C, ally: true); Cast(ws, friendly, 28658, 9);
        var unrelated = Create(ws, 4, 0x9999); Cast(ws, unrelated, 28658, 9);
        Cast(ws, boss, 28658, 9);
        Check(burst.Casters.Count == 1, "same numeric action from friendly helper unrelated actor or boss is excluded");
        var nonSpell = new ActorCastInfo { Action = new(ActionType.Ability, 28658), TotalTime = 9 };
        burst.OnCastStarted(helper, nonSpell);
        Check(burst.Casters.Count == 1, "nonspell numeric Burst ID cannot replace current hazard");
        Finish(ws, helper);
        Check(burst.Casters.Count == 0 && Hints(module, pc).ForbiddenZones.Count == 0, "cancellation removes matching hazard and AI constraint");
        Cast(ws, helper, 28658, 9); ws.Execute(new ActorState.OpDead(helper.InstanceID, true));
        Check(burst.Casters.Count == 0, "caster death clears hazard before delayed cast finish");
        ws.Execute(new ActorState.OpDead(helper.InstanceID, false)); Finish(ws, helper); Cast(ws, helper, 28658, 9);
        ws.Execute(new ActorState.OpDestroy(helper.InstanceID));
        Check(burst.Casters.Count == 0, "caster destruction clears hazard");
        var replacement = Create(ws, 2, 0x233C);
        Check(burst.Casters.Count == 0, "numeric actor reuse does not restore old hazard");
        Cast(ws, replacement, 28658, 9);
        Check(burst.Casters.Count == 1, "replacement actor fresh cast creates a fresh hazard");
        ws.Execute(new ActorState.OpDead(boss.InstanceID, true)); module.Update();
        Check(module.Components.Count == 0 && Hints(module, pc).ForbiddenZones.Count == 0, "phase completion clears all Burst constraints");
    }

    static void FollowupLifecycle()
    {
        var info = BossModuleRegistry.FindByOID(0x1AD);
        Check(info != null && info.ModuleType.FullName == "BossMod.RealmReborn.Dungeon.D07Brayflox.D073AiatarFollowup.D073AiatarFollowup", "first Aiatar has a distinct generated module registration");
        var ws = World(); var boss = Create(ws, 1, 0x1AD, new(-112, -33), targetable: false); var pc = Player(ws, position: new(-112, -31));
        using var module = (BossModule)Activator.CreateInstance(info!.ModuleType, ws, boss)!;
        module.Update();
        Check(module.StateMachine.ActiveState == null, "followup hidden arrival does not activate");
        ws.Execute(new ActorState.OpTargetable(boss.InstanceID, true)); Combat(ws, boss);
        Cast(ws, boss, 29618, 2.7f);
        module.Update();
        var breath = AOE(module, 29618);
        Check(breath.Casters.Count == 1, "followup activation bootstraps already-running Dragon Breath");
        Check(breath.Shape is AOEShapeRect r && r.LengthFront == 30 && r.HalfWidth == 4 && r.LengthBack == 0, "followup breath nominal dimensions preserve thirty front and four half-width");
        var cast = boss.CastInfo!;
        breath.OnCastStarted(boss, cast);
        Check(breath.Casters.Count == 1, "duplicate followup breath callback does not duplicate hazard");
        var friendly = Create(ws, 2, 0x1AD, ally: true); Cast(ws, friendly, 29618, 2.7f);
        var impostor = Create(ws, 3, 0x1AD); Cast(ws, impostor, 29618, 2.7f);
        Check(breath.Casters.Count == 1, "followup hazard belongs to actual primary actor and excludes friendly or same-OID impostor");
        ws.Execute(new ActorState.OpTargetable(boss.InstanceID, false)); module.Update();
        Check(module.StateMachine.ActiveState != null && breath.Casters.Count == 1 && Hints(module, pc).ForbiddenZones.Count == 1, "midcast targetability loss keeps phase and breath guidance active");
        Finish(ws, boss);
        Check(breath.Casters.Count == 0, "followup breath cancellation clears hazard");
        ws.Execute(new ActorState.OpTargetable(boss.InstanceID, true));
        Cast(ws, boss, 29617, 4.7f, pc);
        var h = Hints(module, pc);
        Check(h.PredictedDamage.Count == 1 && h.PredictedDamage[0].Players.Raw == 1 && h.PredictedDamage[0].Type == AIHints.PredictedDamageType.Tankbuster, "followup Salivous Snap predicts damage only to actual party target");
        ws.Execute(new ActorState.OpTargetable(boss.InstanceID, false)); module.Update();
        Check(module.StateMachine.ActiveState != null && Hints(module, pc).PredictedDamage.Count == 1, "untargetability during followup tankbuster does not prematurely unload guidance");
        Finish(ws, boss);
        Check(Hints(module, pc).PredictedDamage.Count == 0, "followup tankbuster cancellation clears prediction");
        ws.CurrentCFCID = 0; Check(module.CheckReset(), "followup resets on duty exit");
        ws.CurrentCFCID = 8; ws.Execute(new ActorState.OpDead(boss.InstanceID, true)); module.Update();
        Check(module.Components.Count == 0, "followup primary death clears mechanics");
    }

    static partial void SlumberLifecycle();
    static partial void LevinshowerGuidance();
}
