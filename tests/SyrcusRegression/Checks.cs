using System.Numerics;
using BossMod;
using BossMod.RealmReborn.Alliance;
using Scylla = BossMod.RealmReborn.Alliance.A21Scylla;
using Glasya = BossMod.RealmReborn.Alliance.A22Glasya;
using Amon = BossMod.RealmReborn.Alliance.A23Amon;
using Xande = BossMod.RealmReborn.Alliance.A24Xande;

static class Checks
{
    static readonly DateTime Now = new(2026, 10, 18, 18, 0, 0, DateTimeKind.Utc);
    static int assertions;
    static void Check(bool condition, string why) { if (!condition) throw new InvalidOperationException(why); ++assertions; }
    sealed class Fixture : IDisposable
    {
        public readonly WorldState World = new(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1), CurrentCFCID = 102 };
        public readonly Actor Boss;
        public readonly Actor[] Players = new Actor[24];
        public BossModule Module = null!;
        public Fixture(uint oid, WPos pos)
        {
            Boss = Create(1, oid, pos, hitbox: 4);
            Class[] jobs = [Class.PLD, Class.WHM, Class.SCH, Class.BLM, Class.BRD, Class.DRG, Class.SAM, Class.MNK];
            for (var i = 0; i < 24; ++i)
            {
                Players[i] = Create((ulong)(100 + i), 0, pos, ActorType.Player, jobs[i % 8]);
                World.Execute(new PartyState.OpModify(i, new(0, Players[i].InstanceID, false)));
            }
            World.Party.SetAllianceGroups(0, 1, 2);
            World.Execute(new ActorState.OpCombat(1, true));
        }
        public Actor Create(ulong id, uint oid, WPos pos, ActorType type = ActorType.Enemy, Class job = Class.None, float hitbox = .5f)
        {
            World.Execute(new ActorState.OpCreate(id, oid, (int)id, 0, "", 0, type, job, 50,
                new Vector4(pos.X, 0, pos.Z, 0), hitbox, new(100, 100, 0, 0, 0), true, false, 0, 0, 0));
            return World.Actors.Find(id)!;
        }
        public void Start(BossModule module) { Module = module; Module.Update(); }
        public void Cast(Actor caster, uint aid, float duration, Angle rotation = default, bool unsetLocation = false)
            => World.Execute(new ActorState.OpCastInfo(caster.InstanceID, new() { Action = new(ActionType.Spell, aid), TargetID = caster.InstanceID,
                Location = unsetLocation ? default : new(caster.Position.X, 0, caster.Position.Z), Rotation = rotation, TotalTime = duration }));
        public void Finish(Actor caster) => World.Execute(new ActorState.OpCastInfo(caster.InstanceID, null));
        public void Icon(Actor actor, uint id) => World.Execute(new ActorState.OpIcon(actor.InstanceID, id, actor.InstanceID));
        public void Status(Actor actor, uint status, int slot = 0) => World.Execute(new ActorState.OpStatus(actor.InstanceID, slot, new(status, 0, Now.AddSeconds(30), 1)));
        public void Tether(Actor actor, uint id, Actor target) => World.Execute(new ActorState.OpTether(actor.InstanceID, new(id, target.InstanceID)));
        public void Event(Actor caster, uint aid, WPos position, params Actor[] targets)
        {
            var action = new ActorCastEvent(new(ActionType.Spell, aid), 0, 0, 24, new(position.X, 0, position.Z), 1, 0, default);
            foreach (var target in targets) action.Targets.Add(new(target.InstanceID, default));
            World.Execute(new ActorState.OpCastEvent(caster.InstanceID, action));
        }
        public AIHints Hints(int slot = 0, params Actor[] targets)
        {
            var hints = new AIHints();
            foreach (var target in targets) hints.PotentialTargets.Add(new(target, 0, false));
            Module.CalculateAIHints(slot, Players[slot], default, hints);
            Check(hints.ActionsToExecute.Entries.Count == 0, "encounter hints execute no game actions");
            return hints;
        }
        public void Dispose() => Module?.Dispose();
    }
    public static void Run(string gameDirectory)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"));
        Service.LuminaGameData = game; Service.Config.Initialize();
        ScyllaOrbsAndWater(); OrbInterceptionAndToads(); GlasyaCone(); GlasyaPunch(); FrontalResponses(); AmonIceAndAdds(); XandeTowers(); XandeExistingTowers(); XandeMeteors(); XandeStacksAndGravity();
        Console.WriteLine($"Syrcus synthetic regression: {assertions} checks passed; physical collision and live gameplay remain separate.");
    }
    static void ScyllaOrbsAndWater()
    {
        Service.Config.Get<SyrcusConfig>().Alliance = SyrcusConfig.AllianceGroup.Automatic;
        WPos[] pillars = [new(-25.75797f, -206.9802f), new(0, -162.4192f), new(25.71078f, -206.9875f)];
        for (var group = 0; group < 3; ++group)
        {
            using var f = new Fixture(0xBC1, new(0, -192));
            f.World.Party.SetAllianceGroups(group, (group + 1) % 3, (group + 2) % 3);
            var lightning = f.Create(10, 0xBC6, new(0, -180)); f.Tether(lightning, 6, f.Players[0]);
            f.Start(new Scylla.A21Scylla(f.World, f.Boss));
            var zone = f.Hints().ForbiddenZones.Single().shapeDistance;
            Check(zone.Distance(pillars[group]) > 0 && zone.Distance(pillars[(group + 1) % 3]) < 0, "lightning uses actual alliance A/B/C receiver, including a preexisting tether");
            f.World.Execute(new ActorState.OpTether(10, default));
            Check(f.Hints().ForbiddenZones.Count == 0, "tether resolution releases the lightning assignment");
            f.World.Party.SetAllianceGroups(-1, -1, -1); f.Tether(lightning, 6, f.Players[0]);
            Check(f.Hints().ForbiddenZones.Count == 0, "unknown alliance does not guess a lightning receiver");
        }
        using var water = new Fixture(0xBC1, new(0, -192));
        water.Start(new Scylla.A21Scylla(water.World, water.Boss));
        var pool = water.Create(10, 0x1E950D, new(10, -192), ActorType.EventObj);
        water.Cast(water.Boss, 2317, 9.7f);
        var hint = water.Hints().ForbiddenZones.Single();
        Check(hint.shapeDistance.Distance(pool.Position) > 0 && hint.shapeDistance.Distance(water.Boss.Position) < 0, "Flare requires a real water center, not standing outside every water pool");
        Check(Math.Abs((hint.activation - Now).TotalSeconds - 9.5) < .001, "Flare has a future arrival deadline with margin");
        var second = water.Create(11, 0x1E950D, new(-10, -192), ActorType.EventObj);
        Check(water.Hints().ForbiddenZones.Single().shapeDistance.Distance(second.Position) > 0, "water appearing mid-cast becomes an additional valid shelter");
        var ice = water.Create(12, 0xBC4, new(0, -180)); water.Tether(ice, 8, water.Players[0]);
        Check(water.Hints().ForbiddenZones.Count == 0, "pending ice never receives a contradictory water assignment");
        water.World.Execute(new ActorState.OpTether(12, default));
        water.World.Execute(new ActorState.OpEventState(10, 7));
        Check(water.Hints().ForbiddenZones.Single().shapeDistance.Distance(pool.Position) < 0, "expired water cannot remain a false safe destination");
        water.Finish(water.Boss); Check(water.Hints().ForbiddenZones.Count == 0, "cancelled or resolved Flare releases shelter");
        water.Status(water.Players[1], 3479);
        var fire = water.Create(13, 0xBC5, new(0, -180)); water.Tether(fire, 5, water.Players[0]);
        Check(water.Hints().ForbiddenZones.Single().shapeDistance.Distance(water.Players[1].Position)>0,
            "modern Deep Freeze creates required fire-orb interception instead of a suppressible uptime goal");
    }
    static void OrbInterceptionAndToads()
    {
        using(var f=new Fixture(0xBC1,new(0,-192)))
        {
            f.Start(new Scylla.A21Scylla(f.World,f.Boss));
            var pool=f.Create(10,0x1E950D,new(10,-192),ActorType.EventObj);
            var ice=f.Create(11,0xBC4,new(5,-192));f.Tether(ice,8,f.Players[0]);
            f.World.Execute(new ActorState.OpMove(100,new(10,0,-192,0)));f.Status(f.Players[0],520);
            var h=f.Hints();Check(h.ForbiddenZones.Count==1 && h.ForbiddenZones[0].shapeDistance.Distance(f.Players[0].Position)<0,
                "actual water-contact status gives an ice carrier an immediate exit response");
            f.World.Execute(new ActorState.OpStatus(100,0,default));Check(f.Hints().ForbiddenZones.Count==0,
                "observed water-contact loss stops the feedback movement without inventing a water radius");
            f.Status(f.Players[0],3479);Check(f.Hints().ForbiddenZones.Count==0,"frozen carriers cannot be told to move");
            f.World.Execute(new ActorState.OpStatus(100,0,default));f.World.Execute(new ActorState.OpTether(11,default));
            var fire=f.Create(12,0xBC5,new(0,-190));f.Tether(fire,5,f.Players[0]);f.Status(f.Players[1],3479);
            Check(f.Hints().ForbiddenZones.Count==1,"fire tether requires reaching the frozen player from its earliest cue");
            f.Event(fire,2322,fire.Position,f.Players[1]);Check(f.Hints().ForbiddenZones.Count==0,
                "Smolder resolution retires interception before delayed actor removal or status callbacks");
        }
        using(var f=new Fixture(0xBE0,new(0,-200)))
        {
            f.Start(new Amon.A23Amon(f.World,f.Boss));
            var orb=f.Create(10,0xBE2,new(0,-190));f.Tether(orb,1,f.Players[0]);
            var first=f.Create(11,0xBE7,new(10,-190),hitbox:2);var second=f.Create(12,0xBE7,new(-10,-190),hitbox:2);
            f.World.Execute(new ActorState.OpCombat(11,true));f.World.Execute(new ActorState.OpCombat(12,true));
            var h=f.Hints();var z=h.ForbiddenZones.Single().shapeDistance;
            Check(z.Distance(new(13.5f,-190))>0 && z.Distance(new(6,-190))<0,
                "shrinking-orb recipient stands beyond the add so the orb crosses it first");
            Check(h.GoalZones.Count==0,"required shrinking interception cannot lose to optional uptime goals");
            f.Status(first,438);z=f.Hints().ForbiddenZones.Single().shapeDistance;
            Check(z.Distance(new(-13.5f,-190))>0,"an already shrunk add is replaced by an eligible unshrunk one");
            f.Event(orb,2371,orb.Position,second);Check(f.Hints().ForbiddenZones.Count==0,
                "Mini action resolution immediately retires the route even before status and destroy callbacks");
            var snake=f.Create(13,0xBE6,new(0,-195),hitbox:1.2f);f.World.Execute(new ActorState.OpCombat(13,true));f.Status(f.Players[0],511);
            z=f.Hints().ForbiddenZones.Single().shapeDistance;
            Check(z.Distance(snake.Position)<0 && z.Distance(new(0,-205))>0,"fire toad gets contact-avoidance guidance for the living snake");
            f.World.Execute(new ActorState.OpDead(13,true));Check(f.Hints().ForbiddenZones.Count==0,"a defeated snake cannot keep a toad kiting");
            f.World.Execute(new ActorState.OpDead(13,false));f.Status(f.Players[0],3479,1);
            Check(f.Hints().ForbiddenZones.Count==0,"freeze supersedes an impossible toad movement assignment");
        }
    }
    static void GlasyaCone()
    {
        using var f = new Fixture(0xBD7, new(0, -200)); f.Start(new Glasya.A22Glasya(f.World, f.Boss));
        f.Cast(f.Boss, 2353, 2.7f);
        var zone = f.Hints().ForbiddenZones.Single().shapeDistance;
        Check(zone.Distance(new(0, -145)) < 0 && zone.Distance(new(40, -200)) > 0, "Vile Utterance covers the distant frontal cone while preserving side safety");
        f.Finish(f.Boss); Check(f.Hints().ForbiddenZones.Count == 0, "cancelled cone clears guidance");
        var wright = f.Create(10, 0xBD9, new(24, -200));
        Check(f.Hints(0, wright).PotentialTargets[0].Priority == AIHints.Enemy.PriorityUndesirable, "AI does not approach a Clockwork Wright merely to attack it");
    }
    static void GlasyaPunch()
    {
        using var f = new Fixture(0xBD7, new(0, -200));
        f.World.Execute(new ActorState.OpMove(f.Players[0].InstanceID, new(0, 0, -194, 0)));
        f.World.Execute(new ActorState.OpMove(f.Players[1].InstanceID, new(0, 0, -192, 0)));
        f.World.Execute(new ActorState.OpMove(f.Players[2].InstanceID, new(8, 0, -200, 0)));
        f.World.Execute(new ActorState.OpTarget(1, f.Players[0].InstanceID));
        f.Start(new Glasya.A22Glasya(f.World, f.Boss));
        var punch = f.Module.Components.OfType<BossMod.Components.Cleave>().Single();
        Check(punch.OriginsAndTargets().Count == 1, "Astral Punch warns from a preexisting target before any hit");
        var zone = f.Hints(1).ForbiddenZones.Single();
        Check(zone.activation == default, "an untelegraphed cleave uses immediate avoidance rather than an invented timer");
        Check(zone.shapeDistance.Distance(new(0, -188.1f)) < 0 && zone.shapeDistance.Distance(new(0, -187.9f)) > 0,
            "Astral Punch reach is8y plus the4y boss hitbox");
        Check(zone.shapeDistance.Distance(new(5, -194)) < 0 && zone.shapeDistance.Distance(new(6, -195)) > 0,
            "Astral Punch preserves the90-degree fan's side safety");
        Check(zone.shapeDistance.Distance(new(0, -208)) > 0, "rear remains outside the current recipient's cleave");
        var tankHints = new BossComponent.TextHints(); punch.AddHints(0, f.Players[0], tankHints);
        Check(tankHints.Count == 0, "the current cleave recipient is not told to flee their own frontal");
        f.World.Execute(new ActorState.OpTarget(1, f.Players[2].InstanceID));
        var changed = f.Hints(1).ForbiddenZones.Single().shapeDistance;
        Check(changed.Distance(new(8, -200)) < 0 && changed.Distance(new(0, -192)) > 0, "a target switch immediately corrects the predicted aim");
        f.Cast(f.Boss, 2353, 2.7f);
        Check(punch.OriginsAndTargets().Count == 0 && f.Hints(1).ForbiddenZones.Count == 1, "the instant punch yields to the active Vile Utterance cast");
        f.Finish(f.Boss);
        Check(punch.OriginsAndTargets().Count == 1, "punch preview resumes when the other cast ends");
        f.World.Execute(new ActorState.OpDead(f.Players[2].InstanceID, true));
        Check(punch.OriginsAndTargets().Count == 0, "a dead recipient cannot leave stale aim guidance");
        f.World.Execute(new ActorState.OpTarget(1, f.Players[0].InstanceID));
        f.World.Execute(new ActorState.OpCombat(1, false));
        Check(punch.OriginsAndTargets().Count == 0, "an out-of-combat boss produces no pull-inducing cleave guidance");
        f.World.Execute(new ActorState.OpCombat(1, true));
        f.World.Execute(new ActorState.OpDead(1, true));
        Check(punch.OriginsAndTargets().Count == 0, "boss death clears the continuous prediction");
        f.Module.StateMachine.Reset(); Check(f.Module.Components.Count == 0, "reset removes the punch component");
    }
    static void FrontalResponses()
    {
        foreach (var oid in new uint[]{0xBC1,0xBE0})
        {
            using var f = new Fixture(oid,new(0,-200));
            f.World.Execute(new ActorState.OpMove(100,new(0,0,-194,0)));
            f.World.Execute(new ActorState.OpTarget(1,100));
            f.Start(oid==0xBC1 ? new Scylla.A21Scylla(f.World,f.Boss) : new Amon.A23Amon(f.World,f.Boss));
            var response=f.Module.Components.OfType<BossMod.Components.FrontalPositioning>().Single();
            var h=new AIHints();response.AddAIHints(1,f.Players[1],default,h);
            Check(h.ForbiddenZones.Count==1,"a recorded instant frontal receives advance standing-position guidance");
            var z=h.ForbiddenZones.Single().shapeDistance;
            Check(z.Distance(new(0,-194))<0 && z.Distance(new(0,-206))>0,"frontal response retains rear safety without claiming an unknown damage angle");
            h=new();response.AddAIHints(0,f.Players[0],default,h);Check(h.ForbiddenZones.Count==0,"the recipient is not sent behind their own attack");
            f.Cast(f.Boss,oid==0xBC1?2317u:2441u,10);h=new();response.AddAIHints(1,f.Players[1],default,h);
            Check(h.ForbiddenZones.Count==0,"active shelter mechanics replace the standing-position policy");
            f.Finish(f.Boss);f.Status(f.Players[1],3479);h=new();response.AddAIHints(1,f.Players[1],default,h);
            Check(h.ForbiddenZones.Count==0,"a frozen player receives no impossible positioning response");
            f.World.Execute(new ActorState.OpStatus(101,0,default));
            var orb=f.Create(10,oid==0xBC1?0xBC5u:0xBE2u,new(0,-190));f.Tether(orb,5,f.Players[1]);
            h=new();response.AddAIHints(1,f.Players[1],default,h);Check(h.ForbiddenZones.Count==0,"required orb interception takes priority over quiet-phase positioning");
        }
    }
    static void AmonIceAndAdds()
    {
        using var f = new Fixture(0xBE0, new(0, -200)); f.Start(new Amon.A23Amon(f.World, f.Boss));
        f.Cast(f.Boss, 2441, 14.7f, unsetLocation: true);
        Check(f.Hints().ForbiddenZones.Single().shapeDistance.Distance(new(0, -180)) < 0, "no ice means no fabricated safe cover");
        var ice = f.Create(10, 0xBE1, new(0, -190), hitbox: 1.8f); f.Module.Update();
        var zone = f.Hints().ForbiddenZones.Single().shapeDistance;
        Check(zone.Distance(new(0, -180)) > 0 && zone.Distance(new(10, -180)) <= 0, "Curtain Call uses actual boss position despite an unset cast location");
        Check(zone.Distance(ice.Position) <= 0, "the ice body is excluded from walkable shelter");
        f.Status(f.Players[1], 3479); f.Module.Update();
        Check(f.Hints(1).ForbiddenZones.Count == 0, "a frozen player cannot receive an impossible move out of its own ice block");
        f.World.Execute(new ActorState.OpDead(10, true)); f.Module.Update();
        Check(f.Hints().ForbiddenZones.Single().shapeDistance.Distance(new(0, -180)) < 0, "melted ice loses its shadow immediately");
        f.Finish(f.Boss); Check(f.Hints().ForbiddenZones.Count == 0, "Curtain Call cancellation clears the hazard");
        f.Icon(f.Players[0], 3); Check(f.Hints().ForbiddenZones.Single().shapeDistance.Distance(f.Boss.Position) < 0, "ice marker gives early outside-hitbox placement guidance");
        f.Status(f.Players[0], 3479); f.Module.Update(); Check(f.Hints().ForbiddenZones.Count == 0, "freezing retires the placement marker");
        var add = f.Create(11, 0xBE4, new(10, -200)); f.World.Execute(new ActorState.OpCombat(11, true));
        var h = f.Hints(0, ice, add); Check(h.PotentialTargets[0].Priority == AIHints.Enemy.PriorityForbidden && h.PotentialTargets[1].Priority == 4 && h.PotentialTargets[1].ShouldBeStunned, "ice is protected while marching adds receive kill/stun priority");
        f.Module.StateMachine.Reset(); Check(f.Module.Components.Count == 0, "reset clears all ice and assignment state");
    }
    static void XandeTowers()
    {
        using var f = new Fixture(0xBDB, new(-400, -200)); f.Start(new Xande.A24Xande(f.World, f.Boss));
        f.World.Execute(new ActorState.OpTarget(1, f.Players[0].InstanceID));
        WPos[] points = [new(-412,-200),new(-388,-179.5f),new(-388,-200),new(-412,-179.5f),new(-412,-220.5f),new(-424,-200),new(-400,-176),new(-420.7f,-188)];
        var mines = points.Select((p, i) => f.Create((ulong)(10+i), 0xBDF, p)).ToArray(); f.Module.Update();
        var assigned = new List<int>(); var destinations = new HashSet<int>();
        for (var slot = 0; slot < 24; ++slot)
        {
            var h = f.Hints(slot);
            if (h.ForbiddenZones.Count == 0) continue;
            assigned.Add(slot);var z = h.ForbiddenZones.Single();
            destinations.Add(Array.FindIndex(points, p => z.shapeDistance.Distance(p) > 0));
            Check(Math.Abs((z.activation-Now).TotalSeconds-9) < .001, "tower arrival is predicted at spawn rather than damage time");
        }
        Check(assigned.Count == 8 && destinations.Count == 8 && !assigned.Contains(0), "eight distinct volunteers cover eight towers without moving the tank holding Xande");
        f.World.Execute(new ActorState.OpDead(f.Players[assigned[0]].InstanceID, true)); f.Module.Update();
        Check(Enumerable.Range(0,24).Count(i => f.Hints(i).ForbiddenZones.Count != 0) == 8, "a dead volunteer is replaced");
        Check(f.Hints(0, mines[0]).PotentialTargets[0].Priority == AIHints.Enemy.PriorityForbidden, "non-combat tower actors cannot be automatically attacked");
        foreach (var mine in mines) f.Event(mine, 2366, mine.Position);
        Check(f.Hints(assigned[1]).ForbiddenZones.Count == 0, "tower actions remove all resolved assignments");
    }
    static void XandeExistingTowers()
    {
        using var f = new Fixture(0xBDB, new(-400, -200));
        var mine = f.Create(10, 0xBDF, new(-412, -200));
        var deadMine = f.Create(11, 0xBDF, new(-388, -200));
        f.World.Execute(new ActorState.OpDead(deadMine.InstanceID, true));
        f.Start(new Xande.A24Xande(f.World, f.Boss));
        var zones = Enumerable.Range(0, 24).SelectMany(i => f.Hints(i).ForbiddenZones).ToArray();
        Check(zones.Length == 1 && zones[0].activation == Now, "a mine present before component activation is urgent, without inventing another nine-second lifetime");
        var global = f.Module.CalculateGlobalHints(f.Players[0]);
        Check(global.Any(s => s.Contains("timing unknown")), "unknown existing-mine timing is explicit");
        f.Event(mine, 2366, mine.Position);
        Check(!f.Module.CalculateGlobalHints(f.Players[0]).Any(s => s.Contains("timing unknown")), "resolved mine clears the timing warning");
    }
    static void XandeMeteors()
    {
        Service.Config.Get<SyrcusConfig>().Alliance = SyrcusConfig.AllianceGroup.Automatic;
        for (var group = 0; group < 3; ++group)
        {
            using var f = new Fixture(0xBDB, new(-400, -200));
            f.World.Party.SetAllianceGroups(group, (group + 1) % 3, (group + 2) % 3);
            f.Start(new Xande.A24Xande(f.World, f.Boss));
            var small = new[] { f.Create(10, 0xBDD, new(-422, -187)), f.Create(11, 0xBDD, new(-400, -226)), f.Create(12, 0xBDD, new(-377, -187)) };
            var large = f.Create(13, 0xBDC, new(-400, -200));
            foreach (var actor in small.Append(large)) f.World.Execute(new ActorState.OpCombat(actor.InstanceID, true));
            var h = f.Hints(0, [.. small, large]);
            Check(h.PotentialTargets[group].Priority == 3 && h.PotentialTargets[3].Priority == 0, "each alliance prioritizes its small meteor before the large meteor");
            Check(h.PotentialTargets.Take(3).Count(t => t.Priority == 3) == 1, "meteor assignment does not send every alliance to the same small meteor");
            Check(h.PotentialTargets.Take(3).All(t => t.Priority > h.PotentialTargets[3].Priority), "remaining small meteors always take precedence over the large meteor");
            f.World.Execute(new ActorState.OpDead(small[group].InstanceID, true));
            Check(f.Hints(0, [.. small.Where(a => !a.IsDead), large]).PotentialTargets.Where(t => t.Actor != large).All(t => t.Priority == 2), "after its own small meteor dies, the alliance helps the remaining small meteors");
            foreach (var actor in small) f.World.Execute(new ActorState.OpDead(actor.InstanceID, true));
            Check(f.Hints(0, large).PotentialTargets.Single().Priority == 2, "large meteor becomes the objective after all small meteors die");
        }
    }
    static void XandeStacksAndGravity()
    {
        using var f = new Fixture(0xBDB, new(-400,-200)); f.Start(new Xande.A24Xande(f.World, f.Boss));
        f.Icon(f.Players[1],317);f.Icon(f.Players[9],317);f.Icon(f.Players[17],317);
        var s = f.Hints().ForbiddenZones.Single(); Check(Math.Abs((s.activation-Now).TotalSeconds-6.5)<.001, "Imperium uses observed seven-second marker timing with margin");
        f.Cast(f.Boss,2361,4.7f); Check(f.Hints().ForbiddenZones.Count==2, "Quaga starting before Imperium does not discard the pending stack");
        f.Event(f.Boss,2367,new(-395,-200),f.Players[1]);
        Check(f.Hints().ForbiddenZones.Count==1 && f.Hints(8).ForbiddenZones.Count==2, "one helper resolves only its own party's stack");
        Check(f.Hints().ForbiddenZones.Single().shapeDistance.Distance(new(-395,-200))>0, "Imperium impact predicts shelter before the gravity object appears");
        var pool=f.Create(10,0x1E9582,new(-395,-200),ActorType.EventObj);f.Module.Update();
        f.Status(f.Players[0],12);
        Check(f.Hints().ForbiddenZones.Count==1, "a levitating player still receives the stay-in-field requirement through Quaga");
        f.World.Execute(new ActorState.OpEventState(pool.InstanceID,7));f.Module.Update();
        Check(f.Hints().ForbiddenZones.Single().shapeDistance.Distance(pool.Position)<0, "an expired gravity object does not remain false shelter");
        f.Finish(f.Boss);Check(f.Hints().ForbiddenZones.Count==0,"Quaga cancellation retires shelter while other party stacks remain independent");
        f.Module.StateMachine.Reset();Check(f.Module.Components.Count==0,"phase reset clears stacks and predicted fields");
    }
}
