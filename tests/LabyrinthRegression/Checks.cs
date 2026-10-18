using System.Numerics;
using BossMod;
using BossMod.RealmReborn.Alliance;
using Bone = BossMod.RealmReborn.Alliance.A11BoneDragon;
using Thanatos = BossMod.RealmReborn.Alliance.A12Thanatos;
using Behemoth = BossMod.RealmReborn.Alliance.A13KingBehemoth;
using Phlegethon = BossMod.RealmReborn.Alliance.A14Phlegethon;
using Atomos = BossMod.RealmReborn.Alliance.A1Atomos;
using Bomb = BossMod.RealmReborn.Alliance.A1AllaganBomb;

static class Checks
{
    static readonly DateTime Now = new(2026, 10, 18, 18, 0, 0, DateTimeKind.Utc);
    static int assertions;
    static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
        ++assertions;
    }
    sealed class Fixture : IDisposable
    {
        public readonly WorldState World = new(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1), CurrentCFCID = 92 };
        public readonly Actor Boss;
        public readonly Actor[] Players = new Actor[8];
        public BossModule Module = null!;
        public Fixture(uint oid, WPos pos)
        {
            Boss = Create(1, oid, pos, hitbox: oid == 0x932 ? 8.7f : 5);
            Class[] jobs = [Class.PLD, Class.WHM, Class.SCH, Class.BLM, Class.BRD, Class.DRG, Class.SAM, Class.MNK];
            for (var i = 0; i < 8; ++i)
            {
                Players[i] = Create((ulong)(100 + i), 0, pos, ActorType.Player, jobs[i]);
                World.Execute(new PartyState.OpModify(i, new(0, Players[i].InstanceID, false)));
            }
            World.Execute(new ActorState.OpCombat(Boss.InstanceID, true));
        }
        public Actor Create(ulong id, uint oid, WPos pos, ActorType type = ActorType.Enemy, Class job = Class.None, float hitbox = .5f)
        {
            World.Execute(new ActorState.OpCreate(id, oid, (int)id, 0, "", 0, type, job, 50,
                new Vector4(pos.X, 0, pos.Z, 0), hitbox, new(100, 100, 0, 0, 0), true, false, 0, 0, 0));
            return World.Actors.Find(id)!;
        }
        public void Start(BossModule module) { Module = module; Module.Update(); }
        public void Cast(Actor caster, uint aid, float duration, WPos? location = null)
        {
            var p = location ?? caster.Position;
            World.Execute(new ActorState.OpCastInfo(caster.InstanceID, new() { Action = new(ActionType.Spell, aid), TargetID = location != null ? 0 : caster.InstanceID,
                Location = new(p.X, 0, p.Z), Rotation = caster.Rotation, TotalTime = duration }));
        }
        public void Finish(Actor caster) => World.Execute(new ActorState.OpCastInfo(caster.InstanceID, null));
        public void Event(Actor caster, uint aid, params Actor[] targets)
        {
            var action = new ActorCastEvent(new(ActionType.Spell, aid), caster.InstanceID, 0, 1, caster.PosRot.AsVector3(), 1, 0, default);
            foreach (var target in targets) action.Targets.Add(new(target.InstanceID, default));
            World.Execute(new ActorState.OpCastEvent(caster.InstanceID, action));
        }
        public AIHints Hints(int slot = 0, params Actor[] enemies)
        {
            var h = new AIHints();
            foreach (var enemy in enemies) h.PotentialTargets.Add(new(enemy, 0, false));
            Module.CalculateAIHints(slot, Players[slot], default, h);
            Check(h.ActionsToExecute.Entries.Count == 0, "encounter guidance executes no game actions");
            return h;
        }
        public void Dispose() => Module?.Dispose();
    }
    public static void Run(string gameDirectory)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"));
        Service.LuminaGameData = game; Service.Config.Initialize();
        Labels(); BoneAndThanatos(); Meteor(); Flare(); AtomosPlatforms(); BombTargets();
        Console.WriteLine($"Labyrinth synthetic regression: {assertions} checks passed; live gameplay and physical geometry are separate.");
    }
    static void Labels()
    {
        var raid = new PartyState(new ActorState());
        Check(raid.AllianceGroupForSlot(0) == -1, "legacy/unknown group labels are explicit");
        foreach (var a in new[] { 0, 1, 2 })
            foreach (var b in new[] { 0, 1, 2 }.Where(x => x != a))
            {
                var c = 3 - a - b; raid.SetAllianceGroups(a, b, c);
                Check(raid.AllianceGroupForSlot(0) == a && raid.AllianceGroupForSlot(7) == a, "own eight slots share their actual label");
                Check(raid.AllianceGroupForSlot(8) == b && raid.AllianceGroupForSlot(15) == b, "first displayed alliance group is independently mapped");
                Check(raid.AllianceGroupForSlot(16) == c && raid.AllianceGroupForSlot(23) == c, "second displayed group is independently mapped");
            }
        raid.SetAllianceGroups(0, 0, 2);
        Check(raid.AllianceGroupForSlot(0) == -1 && raid.AllianceGroupForSlot(16) == -1, "invalid/repeated labels invalidate the entire mapping");
        Check(raid.AllianceGroupForSlot(-1) == -1 && raid.AllianceGroupForSlot(24) == -1, "NPC/invalid slots never acquire an alliance");
    }
    static void BoneAndThanatos()
    {
        using (var f = new Fixture(0x92B, new(-451.2f, -15)))
        {
            f.Start(new Bone.A11BoneDragon(f.World, f.Boss));
            Check(f.Module.Arena.InBounds(new(-451.2f, -15)) && f.Module.Arena.InBounds(new(-495, 23.93f)), "dragon arena contains north boss and west platforms");
            Check(!f.Module.Arena.InBounds(new(-451.2f, 75)), "dragon arena excludes space beyond its nominal outer edge");
            f.World.Execute(new ActorState.OpDead(1, true)); f.Module.Update();
            Check(f.Module.Components.Count > 0, "first dragon death must not end the resurrection encounter");
            var skeleton = f.Create(2, 0x92D, f.Boss.Position); f.World.Execute(new ActorState.OpCombat(2, true));
            Check(f.Hints(0, f.Boss, skeleton).PotentialTargets[1].Priority == 3, "revived skeletons retain priority during dragon death");
        }
        using (var f = new Fixture(0x92E, new(440, 280)))
        {
            f.Start(new Thanatos.A12Thanatos(f.World, f.Boss));
            Check(f.Hints(0, f.Boss).PotentialTargets[0].Priority == AIHints.Enemy.PriorityInvincible, "unbuffed players must not attack Thanatos");
            f.World.Execute(new ActorState.OpStatus(100, 0, new ActorStatus(398, 0, Now.AddSeconds(60), 0)));
            Check(f.Hints(0, f.Boss).PotentialTargets[0].Priority == 1, "Astral Realignment permits the boss for that player");
            Check(f.Hints(1, f.Boss).PotentialTargets[0].Priority == AIHints.Enemy.PriorityInvincible, "one player's buff cannot authorize another player");
            var pot = f.Create(2, 0x931, new(420, 245)); f.Cast(pot, 1759, 2.7f);
            Check(f.Hints().ForbiddenZones.Count == 0, "Astral Light buff must not create an avoidance zone");
            f.World.Execute(new ActorState.OpStatus(100, 0, default));
            Check(f.Hints(0, f.Boss).PotentialTargets[0].Priority < 0, "buff removal immediately restores immunity guidance");
        }
    }
    static void Meteor()
    {
        using var f = new Fixture(0x932, new(-110, -375));
        f.Start(new Behemoth.A13KingBehemoth(f.World, f.Boss));
        f.Cast(f.Boss, 1756, 9.7f, new(0, 0));
        var empty = f.Hints(); Check(empty.ForbiddenZones.Count == 1, "no comets means no fabricated safe cover");
        var rock = f.Create(2, 0x936, new(-110, -360), hitbox: 2.4f); f.Module.Update();
        var hints = f.Hints();
        Check(hints.ForbiddenZones.Count == 1, "comet appearing during meteor restores cover constraint");
        var zone = hints.ForbiddenZones[0].shapeDistance;
        Check(zone.Distance(new(-110, -350)) > 0 && zone.Distance(new(-95, -350)) <= 0, "meteor uses actual boss origin and correct comet shadow: " + zone.Distance(new(-110, -350)) + ", " + zone.Distance(new(-95, -350)));
        Check(zone.Distance(rock.Position) <= 0, "standing inside the rock is not shelter");
        f.World.Execute(new ActorState.OpDead(2, true)); f.Module.Update();
        Check(f.Hints().ForbiddenZones[0].shapeDistance.Distance(new(-110, -350)) < 0, "destroyed cover cannot retain a safe shadow");
        f.Finish(f.Boss); Check(f.Hints().ForbiddenZones.Count == 0, "cancelled/finished meteor clears cover guidance");
        f.Cast(f.Boss, 1756, 9.7f); var inside = f.Create(3, 0x936, new(-110, -372), hitbox: 2.4f); f.Module.Update();
        Check(f.Hints().ForbiddenZones[0].shapeDistance.Distance(new(-110, -350)) < 0, "comet inside boss hitbox cannot provide cover");
        f.Finish(f.Boss);
        f.Cast(f.Boss, 1751, 2.7f, new(-100, -360));
        Check(f.Hints().ForbiddenZones.Any(z => z.shapeDistance.Distance(new(-100, -360)) < 0), "Charybdis warns during its visual cast, before the puddle actor");
        Check(f.Hints(0, inside).PotentialTargets[0].Priority == AIHints.Enemy.PriorityForbidden, "friendly cover cannot be automatically attacked");
        f.Finish(f.Boss);
        f.World.Execute(new ActorState.OpVFX(f.Players[0].InstanceID, 298, f.Players[0].InstanceID));
        var unmarked = f.Players[2];
        f.World.Execute(new ActorState.OpMove(unmarked.InstanceID, new Vector4(-95, 0, -360, 0)));
        Check(f.Hints().ForbiddenZones.Any(z => z.shapeDistance.Distance(unmarked.Position) < 0 && z.activation == Now.AddSeconds(12)),
            "a marked player receives timed AI separation from unmarked players, not only a text spread warning");
        f.World.Execute(new ActorState.OpVFX(f.Players[1].InstanceID, 298, f.Players[1].InstanceID));
        var markers = f.Module.Components.OfType<BossMod.Components.GenericBaitAway>().Single();
        Check(markers.CurrentBaits.Count == 2, "simultaneous comet markers remain separate");
        f.Event(rock, 768, f.Players[0], f.Players[1]);
        Check(markers.CurrentBaits.Count == 1 && markers.CurrentBaits[0].Target == f.Players[1], "one impact retires its marked recipient without retiring a still-pending marker caught in the same blast");
        f.Event(inside, 768, f.Players[1]);
        Check(markers.CurrentBaits.Count == 0, "last impact removes resolved comet bait guidance immediately");
        f.Event(inside, 768, f.Players[1]);
        Check(markers.CurrentBaits.Count == 0, "duplicate impact is harmless");
        f.Finish(f.Boss); f.Module.StateMachine.Reset(); Check(f.Module.Components.Count == 0, "phase reset clears meteor, markers and puddle state");
    }
    static void Flare()
    {
        WPos[] pads = [new(-148.65f, 191.99f), new(-110, 221.59f), new(-71.36f, 191.98f)];
        Service.Config.Get<LabyrinthConfig>().Alliance = LabyrinthConfig.AllianceGroup.Automatic;
        for (var group = 0; group < 3; ++group)
        {
            using var f = new Fixture(0x938, new(-110, 181.6f));
            f.World.Party.SetAllianceGroups(group, (group + 1) % 3, (group + 2) % 3);
            f.Cast(f.Boss, 1730, 6.7f); // activation must recover an already active cast
            f.Start(new Phlegethon.A14Phlegethon(f.World, f.Boss));
            var hints = f.Hints();
            Check(hints.ForbiddenZones.Count == 1, "one mandatory assigned-pad constraint, without an arena-sized false AOE");
            Check(Math.Abs((hints.ForbiddenZones[0].activation - Now).TotalSeconds - 6.5) < .001, "flare gives a future arrival deadline with an explicit conservative margin");
            Check(hints.ForbiddenZones[0].shapeDistance.Distance(pads[group]) > 0, "assigned alliance platform is allowed");
            Check(hints.ForbiddenZones[0].shapeDistance.Distance(pads[(group + 1) % 3]) < 0, "another alliance's platform cannot satisfy the assignment");
            Check(f.Module.Arena.InBounds(pads[group]), "all alliance platforms participate in logical/pathfinding bounds");
            f.Event(f.Boss, 1730); f.Finish(f.Boss); f.Event(f.Boss, 1748);
            Check(f.Hints().ForbiddenZones.Count == 1, "successful cast retains platform guidance through the shield/add phase");
            var pad = f.Create(2, 0x1E8F88, pads[1], ActorType.EventObj);
            f.World.Execute(new ActorState.OpEventObjectAnimation(pad.InstanceID, 1, 2));
            Check(f.Hints().ForbiddenZones.Count == 0, "platform reset retires successful flare state");
            f.Cast(f.Boss, 1730, 6.7f); f.Finish(f.Boss);
            Check(f.Hints().ForbiddenZones.Count == 0, "cancelled flare does not leave a mandatory platform zone");
            Check(!f.Module.CalculateHintsForRaidMember(0, f.Players[0]).Any(h => h.Item1.Contains("Ancient Flare")), "cancelled flare also clears its platform preview warning");
            f.World.Party.SetAllianceGroups(-1, -1, -1); f.Cast(f.Boss, 1730, 6.7f);
            Check(f.Hints().ForbiddenZones.Count == 0, "unknown alliance must not select the nearest or another platform");
            Check(f.Module.CalculateHintsForRaidMember(0, f.Players[0]).Any(h => h.Item1.Contains("alliance unknown")), "unknown assignment is visible to the player");
        }
    }
    static void AtomosPlatforms()
    {
        using var f = new Fixture(0x963, new(253.68f, 244.8f));
        var b = f.Create(2, 0x963, new(253.68f, 280)); var c = f.Create(3, 0x963, new(253.68f, 315.2f));
        f.World.Party.SetAllianceGroups(0, 1, 2); f.Start(new Atomos.A1Atomos(f.World, f.Boss));
        Check(BossModuleRegistry.FindByType(typeof(Atomos.A1Atomos))?.PrimaryActorOID == 0x963, "Atomos is registered by its actual primary OID");
        Check(f.Module.Arena.InBounds(new(213.68f, 244.8f)) && f.Module.Arena.InBounds(new(213.68f, 280))
            && f.Module.Arena.InBounds(new(213.68f, 315.2f)) && !f.Module.Arena.InBounds(new(213.68f, 262.4f)),
            "the single Atomos module contains all three floors but excludes the gaps between them");
        var holders = 0;
        for (var i = 0; i < 8; ++i) holders += f.Hints(i).ForbiddenZones.Count > 0 ? 1 : 0;
        Check(holders == 4, "exactly four members of the full party receive a mandatory rear-pad assignment");
        Check(f.Hints(0, f.Boss, b, c).PotentialTargets.Skip(1).All(e => e.Priority == AIHints.Enemy.PriorityUndesirable), "Atomos targets in other alliance lanes are excluded");
        f.World.Execute(new ActorState.OpDead(f.Boss.InstanceID, true)); f.Module.Update();
        Check(f.Hints(1).ForbiddenZones.Count == 1, "own Atomos dying must not release the linked alliance's rear pad");
        f.World.Execute(new ActorState.OpDead(b.InstanceID, true)); f.Module.Update();
        Check(f.Hints(1).ForbiddenZones.Count == 0, "linked Atomos death releases the rear-pad assignment");
        f.World.Party.SetAllianceGroups(-1, -1, -1);
        Check(f.Hints(0, c).PotentialTargets[0].Priority == AIHints.Enemy.PriorityUndesirable, "unknown alliance cannot automatically select another lane's boss");
    }
    static void BombTargets()
    {
        using var f = new Fixture(0x967, new(-110, -165.6f));
        var a = f.Create(2, 0x96A, new(-128.3f, -155)); var b = f.Create(3, 0x96A, new(-110, -186.8f)); var c = f.Create(4, 0x96A, new(-91.7f, -155));
        var balloon = f.Create(5, 0x968, a.Position); var napalm = f.Create(6, 0x969, a.Position);
        foreach (var e in new[] { a, b, c, balloon, napalm }) f.World.Execute(new ActorState.OpCombat(e.InstanceID, true));
        f.World.Party.SetAllianceGroups(0, 1, 2); f.Start(new Bomb.A1AllaganBomb(f.World, f.Boss));
        Check(BossModuleRegistry.FindByType(typeof(Bomb.A1AllaganBomb))?.PrimaryActorOID == 0x967, "Allagan Bomb encounter has generated registration");
        for (var group = 0; group < 3; ++group)
        {
            f.World.Party.SetAllianceGroups(group, (group + 1) % 3, (group + 2) % 3);
            var h = f.Hints(0, f.Boss, a, b, c, balloon, napalm);
            Check(h.PotentialTargets[0].Priority == AIHints.Enemy.PriorityInvincible, "central bomb stays immune while Vassagos live");
            Check(h.PotentialTargets[group + 1].Priority == 2, "each alliance gets its own Vassago");
            Check(h.PotentialTargets.Skip(1).Take(3).Count(e => e.Priority == AIHints.Enemy.PriorityUndesirable) == 2, "two other alliance Vassagos are excluded");
            Check(h.PotentialTargets[5].Priority > h.PotentialTargets[4].Priority && h.PotentialTargets[4].Priority > 2, "Napalm > Balloon > own Vassago priority");
            Check(h.PotentialTargets[group + 1].DesiredPosition != null && h.PotentialTargets[group + 1].DesiredRotation != null,
                "each alliance publishes its separate Vassago holding point and outward facing");
        }
        f.World.Party.SetAllianceGroups(0, 1, 2);
        f.World.Execute(new ActorState.OpTarget(a.InstanceID, f.Players[0].InstanceID));
        var tank = f.Hints(0, a, b, c, balloon, napalm);
        Check(tank.PotentialTargets[0].Priority == 5 && tank.GoalZones.Count == 1, "a tank already holding its Vassago stays with it rather than chasing bombs");
        var anchor = tank.PotentialTargets[0].DesiredPosition!.Value;
        var tankSpot = anchor + (anchor - f.Module.Center).Normalized() * (a.HitboxRadius + f.Players[0].HitboxRadius + tank.PotentialTargets[0].TankDistance);
        Check(tank.GoalZones[0](tankSpot) > tank.GoalZones[0](f.Module.Center) && f.Module.Arena.InBounds(tankSpot),
            "tank preference holds the Vassago facing outward at an in-bounds location");
        Check(f.Hints(1, a).GoalZones.Count == 0, "a healer never receives the tank holding preference");
        f.World.Execute(new ActorState.OpTarget(a.InstanceID, f.Players[1].InstanceID));
        Check(f.Hints(0, a).GoalZones.Count == 0, "a tank cannot take positioning ownership of another player's Vassago");
        f.World.Party.SetAllianceGroups(-1, -1, -1);
        Check(f.Hints(0, a).PotentialTargets[0].DesiredPosition == null, "unknown alliance never chooses an arbitrary tank anchor");
        f.Cast(a, 1774, 2.7f);
        Check(f.Hints().ForbiddenZones.Count == 1, "Desolation emits a timed rectangle");
        f.Finish(a); Check(f.Hints().ForbiddenZones.Count == 0, "Desolation cancellation clears the rectangle");
    }
}
