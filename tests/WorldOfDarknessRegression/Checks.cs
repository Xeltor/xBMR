using System.Numerics;
using BossMod;
using BossMod.RealmReborn.Alliance;
using Angra = BossMod.RealmReborn.Alliance.A31AngraMainyu;
using Dragon = BossMod.RealmReborn.Alliance.A32FiveheadedDragon;
using Cerberus = BossMod.RealmReborn.Alliance.A33Cerberus;
using Cloud = BossMod.RealmReborn.Alliance.A34CloudofDarkness;

static class Checks
{
    static readonly DateTime Now = new(2026, 10, 18, 18, 0, 0, DateTimeKind.Utc);
    static int assertions;
    static void Check(bool condition, string why) { if (!condition) throw new InvalidOperationException(why); ++assertions; }
    sealed class Fixture : IDisposable
    {
        public readonly WorldState World = new(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1), CurrentCFCID = 111 };
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
                new Vector4(pos.X, oid == 0xDF6 || oid is 0xDF9 or 0xDFA or 0xDF8 || type == ActorType.Player && Boss?.OID == 0xDF6 ? 129.3f : 0, pos.Z, 0), hitbox, new(100, 100, 0, 0, 0), true, false, 0, 0, 0));
            return World.Actors.Find(id)!;
        }
        public void Start(BossModule module) { Module = module; Module.Update(); }
        public void Cast(Actor caster, uint aid, float duration, Angle rotation = default, bool unsetLocation = false)
            => World.Execute(new ActorState.OpCastInfo(caster.InstanceID, new() { Action = new(ActionType.Spell, aid), TargetID = caster.InstanceID,
                Location = unsetLocation ? default : new(caster.Position.X, 0, caster.Position.Z), Rotation = rotation, TotalTime = duration - ActorCastInfo.NPCFinishDelay, Interruptible = true }));
        public void Move(Actor actor, WPos pos, float y = 0) => World.Execute(new ActorState.OpMove(actor.InstanceID, new(pos.X, y, pos.Z, 0)));
        public void Tick(double seconds) { World.Frame = new(Now.AddSeconds(seconds), 0, 0, 0, 0, 1); Module.Update(); }
        public void EState(Actor actor, ushort state) => World.Execute(new ActorState.OpEventObjectStateChange(actor.InstanceID, state));
        public void EAnim(Actor actor, ushort a, ushort b) => World.Execute(new ActorState.OpEventObjectAnimation(actor.InstanceID, a, b));
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
        var failures=new List<Exception>();
        foreach(var test in new Action[]{AngraColors,AngraLevelsAndDoom,DragonMarkers,CloudChase,CloudTowers,CloudGroups,CerberusFloorsAndBelly,CerberusChains,InterveningEncounters})
            try { test(); } catch(Exception e) { failures.Add(e); Console.Error.WriteLine(test.Method.Name+": "+e.Message); }
        if(failures.Count!=0)throw new AggregateException(failures);
        Console.WriteLine($"World of Darkness synthetic regression: {assertions} checks passed; physical collision and live gameplay remain separate.");
    }
    static BossMod.Components.GenericAOEs AOE(Fixture f, string name) => f.Module.Components.OfType<BossMod.Components.GenericAOEs>().Single(c => c.GetType().Name == name);
    static void AngraColors()
    {
        using var f = new Fixture(0xE00, new(-147.07f, 296.92f)); f.Start(new Angra.A31AngraMainyu(f.World, f.Boss));
        f.Status(f.Players[0], 636); f.Status(f.Players[1], 637); f.Cast(f.Boss, 3272, 2.5f);
        var aoe = AOE(f, "DoubleVision");
        Check(aoe.ActiveAOEs(0,f.Players[0]).Length==1, "prior Sullen gets a color response from cast start");
        var front=f.Boss.Position+new WDir(0,10);var back=f.Boss.Position+new WDir(0,-10);
        Check(aoe.ActiveAOEs(0,f.Players[0])[0].Check(front) && !aoe.ActiveAOEs(0,f.Players[0])[0].Check(back), "Sullen must leave the front half");
        Check(aoe.ActiveAOEs(1,f.Players[1])[0].Check(back) && !aoe.ActiveAOEs(1,f.Players[1])[0].Check(front), "Ireful must leave the rear half");
        Check(aoe.ActiveAOEs(2,f.Players[2]).Length==0,"an unbranded player can choose either color");
        Check(Math.Abs((aoe.ActiveAOEs(0,f.Players[0])[0].Activation-Now).TotalSeconds-2)<.001,"color response deadline precedes visual resolution");
        f.Event(f.Boss,3272,f.Boss.Position);f.Finish(f.Boss);
        Check(aoe.ActiveAOEs(0,f.Players[0]).Length==1,"visual completion does not prematurely retire helper hazards");
        f.Event(f.Boss,3273,f.Boss.Position,f.Players[0]);Check(aoe.ActiveAOEs(0,f.Players[0]).Length==0 && aoe.ActiveAOEs(1,f.Players[1]).Length==1,"first helper retires only resolved recipients");
        f.Event(f.Boss,3274,f.Boss.Position,f.Players[1]);Check(aoe.ActiveAOEs(1,f.Players[1]).Length==0,"both colors retire the wave");
        f.Cast(f.Boss,3272,2.5f);f.Finish(f.Boss);Check(aoe.ActiveAOEs(0,f.Players[0]).Length==0,"cancelled color cast clears state");
        Check(!f.Module.Arena.InBounds(new(-117.07f,310)),"corrected Angra floor does not extend beyond its collision perimeter");
    }
    static void AngraLevelsAndDoom()
    {
        using var f=new Fixture(0xE00,new(-147.07f,296.92f));f.Start(new Angra.A31AngraMainyu(f.World,f.Boss));
        f.Move(f.Players[3],new(-155,310)); f.Icon(f.Players[3],44);f.Cast(f.Boss,3275,4.5f);
        var z=f.Hints().ForbiddenZones.Single();Check(z.shapeDistance.Distance(new(-155,310))<0 && z.shapeDistance.Distance(new(-155,323))>0,"level circle follows bound player with12y radius");
        Check(f.Hints(3).ForbiddenZones.Count==0,"bound target is never directed away from itself");
        f.Event(f.Boss,3275,f.Boss.Position);f.Finish(f.Boss);Check(f.Hints().ForbiddenZones.Count==1,"level warning survives visual finish until helper hit");
        f.Event(f.Boss,3276,f.Players[3].Position,f.Players[3]);Check(f.Hints().ForbiddenZones.Count==0,"level helper resolution clears state");
        f.Icon(f.Players[3],45);f.Cast(f.Boss,3277,4.5f);f.Finish(f.Boss);Check(f.Hints().ForbiddenZones.Count==0,"cancelled second level variant clears state");
        f.Status(f.Players[0],210);Check(f.Hints().ForbiddenZones.Count==0,"Doom without known pad activation creates no fabricated safe destination");
        var pad=f.Create(501,2004753,new(-156.52f,287.48f),ActorType.EventObj);f.EState(pad,4);
        Check(f.Hints().ForbiddenZones.Single().shapeDistance.Distance(pad.Position)>0,"Doom seeks the currently lit pad");
        f.EAnim(pad,1,16);Check(f.Hints().ForbiddenZones.Count==0,"pad rotation clears obsolete destination");
        f.EState(pad,4);f.EState(pad,1);Check(f.Hints().ForbiddenZones.Count==0,"reset clears lit pad");
    }
    static void DragonMarkers()
    {
        using var f=new Fixture(0xDFC,new(200,179));f.Start(new Dragon.A32FiveheadedDragon(f.World,f.Boss));
        var markers=f.Module.Components.OfType<BossMod.Components.GenericStackSpread>().Single();
        f.Icon(f.Players[3],318);Check(markers.Stacks.Count==1 && markers.Stacks[0].Radius==6 && markers.Spreads.Count==0,"thunder is a6y stack");
        f.Icon(f.Players[4],46);Check(markers.Spreads.Count==1 && markers.Spreads[0].Radius==20 && markers.Stacks.Count==1,"Fermata is a separate20y spread");
        f.Event(f.Boss,3287,f.Players[3].Position,f.Players[5],f.Players[3]);Check(markers.Stacks.Count==1,"other listed recipients cannot retire a marker");
        f.Event(f.Boss,3287,f.Players[3].Position,f.Players[3],f.Players[5]);Check(markers.Stacks.Count==0 && markers.Spreads.Count==1,"first listed thunder target retires only the stack");
        f.Event(f.Boss,3283,f.Players[4].Position);Check(markers.Spreads.Count==0,"Fermata retires spread");
        f.Status(f.Players[0],639);Check(f.Hints().ImminentSpecialMode.mode==AIHints.SpecialMode.Pyretic,"actual Pyretic reaches AI no-action/no-movement intent");
        var head=f.Create(500,0xE13,new(200,170),ActorType.Part);f.World.Execute(new ActorState.OpCombat(head.InstanceID,true));
        var h=f.Hints(3,f.Boss,head);Check(h.PotentialTargets[1].Priority>h.PotentialTargets[0].Priority,"living heads take priority over boss");
        f.Icon(f.Players[3],318);f.Tick(8);Check(markers.Stacks.Count==0,"missed marker events cannot leave stale assignments indefinitely");
    }
    static void CloudChase()
    {
        using var f=new Fixture(0xCFA,new(-300,-400));f.Start(new Cloud.A34CloudofDarkness(f.World,f.Boss));var aoe=AOE(f,"FeintParticleBeam");
        f.Icon(f.Players[3],197);f.Cast(f.Boss,3298,3.5f);Check(aoe.ActiveAOEs(0,f.Players[0])[0].Shape is AOEShapeCircle {Radius:8},"initial chasing hit is8y");
        f.Event(f.Boss,3298,f.Boss.Position);f.Finish(f.Boss);f.Move(f.Players[3],new(-280,-400));
        var a=aoe.ActiveAOEs(0,f.Players[0])[0];Check(a.Shape is AOEShapeCircle {Radius:3} && a.Origin.AlmostEqual(new(-297,-400),.01f),"followup is3y and advances3y toward marked player");
        Check(Math.Abs((a.Activation-Now).TotalSeconds-3.1)<.001,"initial followup delay is distinct from repeat interval");
        for(var i=0;i<9;++i)f.Event(f.Boss,3299,new(-297,-400));Check(aoe.ActiveAOEs(0,f.Players[0]).Length==1,"nine followups do not retire the tenth");
        f.Event(f.Boss,3299,new(-294,-400));Check(aoe.ActiveAOEs(0,f.Players[0]).Length==0,"tenth followup retires chase");
        f.Icon(f.Players[4],197);f.Cast(f.Boss,3298,3.5f);f.Finish(f.Boss);Check(aoe.ActiveAOEs(0,f.Players[0]).Length==0,"cancelled initial beam clears predictions");
        f.Icon(f.Players[4],197);f.World.Execute(new ActorState.OpDead(f.Boss.InstanceID,true));f.Module.Update();Check(aoe.ActiveAOEs(0,f.Players[0]).Length==0,"boss death clears outstanding chase");
    }
    static void CloudTowers()
    {
        using var f=new Fixture(0xCFA,new(-300,-400));f.Start(new Cloud.A34CloudofDarkness(f.World,f.Boss));
        foreach(var player in f.Players)f.Move(player,new(-300,-380));
        var t1=f.Create(501,0x1E974E,new(-315.55f,-415.55f),ActorType.EventObj);var t2=f.Create(502,0x1E974E,new(-284.45f,-415.55f),ActorType.EventObj);f.Module.Update();
        var towers=f.Module.Components.OfType<BossMod.Components.GenericTowers>().Single();Check(towers.Towers.Count==2,"concurrent towers are retained separately");
        Check(towers.Towers.All(t=>Math.Abs((t.Activation-Now).TotalSeconds-9)<.001),"new towers use nine-second response deadline");
        var chosen=Enumerable.Range(0,24).Where(i=>f.Hints(i).ForbiddenZones.Count!=0).ToArray();Check(chosen.Length==2,"each tower gets a distinct volunteer");
        f.EState(t1,20);Check(towers.Towers.Count==2,"occupied state20 is not impact/resolution");
        f.Event(f.Boss,3301,t1.Position);Check(towers.Towers.Count==1 && towers.Towers[0].ActorID==t2.InstanceID,"impact retires the matching tower only");
        f.Event(f.Boss,3300,new(0,0));Check(towers.Towers.Count==1,"unlocated failure action cannot clear another tower");
        f.World.Execute(new ActorState.OpDestroy(t2.InstanceID));Check(towers.Towers.Count==0,"despawn clears tower and assignment");
        using var old=new Fixture(0xCFA,new(-300,-400));old.Create(501,0x1E974E,new(-300,-400),ActorType.EventObj);old.Start(new Cloud.A34CloudofDarkness(old.World,old.Boss));
        Check(old.Module.Components.OfType<BossMod.Components.GenericTowers>().Single().Towers[0].Activation==Now,"preexisting tower cannot get a fresh nine seconds");
    }
    static void CloudGroups()
    {
        using var f=new Fixture(0xCFA,new(-300,-400));f.Start(new Cloud.A34CloudofDarkness(f.World,f.Boss));
        WPos[] positions=[new(-313,-392.5f),new(-300,-415),new(-287,-392.5f)];
        for(var i=0;i<3;++i)f.Create((ulong)(501+i),0xE03,positions[i]);f.Module.Update();
        for(var i=0;i<3;++i)
        {
            var h=f.Hints(i*8);Check(h.ForbiddenZones.Count==1 && h.ForbiddenZones[0].shapeDistance.Distance(positions[i])>0,"each alliance enters its own hypercloud center");
            Check(Math.Abs((h.ForbiddenZones[0].activation-Now).TotalSeconds-7)<.001,"box entry warning precedes observed closure");
        }
        f.World.Party.SetAllianceGroups(-1,-1,-1);Check(f.Hints().ForbiddenZones.Count==0,"unknown alliance creates no arbitrary box assignment");
    }
    static void CerberusFloorsAndBelly()
    {
        Service.Config.Get<WorldOfDarknessConfig>().CerberusBelly=WorldOfDarknessConfig.BellyStrategy.AllDPSAndOneHealer;
        using var f=new Fixture(0xDF6,new(0,-198));f.Start(new Cerberus.A33Cerberus(f.World,f.Boss));var juice=f.Create(501,0xDF9,new(12,-190));f.Module.Update();f.Cast(juice,3249,3);
        var mini=AOE(f,"Mini");Check(mini.ActiveAOEs(3,f.Players[3]).Length==0 && mini.ActiveAOEs(0,f.Players[0]).Length==1,"assigned DPS accept Mini while outside tank avoids it");
        Check(Enumerable.Range(0,24).Count(i=>mini.ActiveAOEs(i,f.Players[i]).Length==0)==16,"modern belly plan sends15DPS plus one healer and leaves8outside");
        f.Finish(juice);var puddle=f.Create(502,0x1E968C,new(-7,-194),ActorType.EventObj);f.Status(f.Players[3],438);f.Module.Update();
        Check(f.Hints(3).ForbiddenZones.Any(z=>z.shapeDistance.Distance(puddle.Position)>0),"only Mini players get a puddle entry destination");
        Check(f.Hints(0).ForbiddenZones.Any(z=>z.shapeDistance.Contains(puddle.Position)),"outside team avoids live purple puddle: y="+f.Players[0].PosRot.Y+" layer="+f.Module.ResolveArenaProjectionLayer(f.Players[0])+" zones="+string.Join(",",f.Hints(0).ForbiddenZones.Select(z=>z.shapeDistance.Distance(puddle.Position))));
        var wall=f.Create(503,0xDFA,new(9,-196));f.Move(wall,wall.Position,-498);f.World.Execute(new ActorState.OpCombat(wall.InstanceID,true));
        f.Move(f.Players[3],new(0,-200),-500);Check(f.Module.ResolveArenaProjectionLayer(f.Players[3])!=f.Module.ResolveArenaProjectionLayer(f.Players[0]),"same XZ on stomach/surface resolves to separate physical floors before Digesting status");
        var hints=f.Hints(3,f.Boss,wall);Check(hints.PotentialTargets[0].Priority==AIHints.Enemy.PriorityForbidden && hints.PotentialTargets[1].Priority==5,"stomach player targets walls and never the surface boss");
        f.Cast(f.Boss,3250,1.8f);Check(f.Hints(3).ForbiddenZones.Count==0,"surface breath cannot leak into stomach AI");
        f.World.Execute(new ActorState.OpEventState(puddle.InstanceID,7));f.Move(f.Players[3],new(0,-198),129.3f);f.Finish(f.Boss);Check(!f.Hints(3).ForbiddenZones.Any(z=>z.shapeDistance.Distance(puddle.Position)>0),"spent puddle is no longer an entrance");
        Service.Config.Get<WorldOfDarknessConfig>().CerberusBelly=WorldOfDarknessConfig.BellyStrategy.Manual;
        using var manual=new Fixture(0xDF6,new(0,-198));manual.Start(new Cerberus.A33Cerberus(manual.World,manual.Boss));var j=manual.Create(501,0xDF9,new(12,-190));manual.Module.Update();manual.Cast(j,3249,3);Check(AOE(manual,"Mini").ActiveAOEs(3,manual.Players[3]).Length==1,"manual strategy never volunteers a player for ingestion");
    }
    static void CerberusChains()
    {
        Service.Config.Get<WorldOfDarknessConfig>().CerberusBelly=WorldOfDarknessConfig.BellyStrategy.AllDPSAndOneHealer;
        using var f=new Fixture(0xDF6,new(0,-198));f.Start(new Cerberus.A33Cerberus(f.World,f.Boss));
        var left=f.Create(501,0x1E9736,new(-21.5f,-239.5f),ActorType.EventObj);var right=f.Create(502,0x1E9737,new(21.5f,-239.5f),ActorType.EventObj);
        var openLeft=f.Create(503,0x1E9735,new(-18.38f,-215.29f),ActorType.EventObj);var openRight=f.Create(504,0x1E9733,new(-10.24f,-213.25f),ActorType.EventObj);
        Check(f.Hints(3).InteractWithTarget==null,"targetable chains before Reawakening do not cause early interaction");
        f.Cast(f.Boss,3507,50);f.Module.Update();var chosen=Enumerable.Range(0,24).Where(i=>f.Hints(i).InteractWithTarget!=null).ToArray();Check(chosen.Length==2,"two different outside players take the two chains");
        var player=f.Players[chosen[0]];f.Status(player,404);var helper=f.Create(505,0x19A,new(-19.28f,-237.25f));f.Tether(helper,34,player);f.Module.Update();
        Check(f.Hints(chosen[0]).InteractWithTarget==openLeft,"left chain uses observed tether provenance to choose its correct open fetter");
        f.Tether(helper,32,f.Boss);Check(f.Hints(chosen[0]).InteractWithTarget==null,"successful attachment cannot leave a stale carrier interaction");
        f.Finish(f.Boss);f.Module.Update();Check(Enumerable.Range(0,24).All(i=>f.Hints(i).InteractWithTarget==null),"phase cancellation clears all chain interactions");
    }
    static void InterveningEncounters()
    {
        using var eye=new Fixture(0xD9C,new(-77.06f,382.94f));eye.Start(new BossMod.RealmReborn.Alliance.A3TheEye.A3TheEye(eye.World,eye.Boss));
        Check(eye.Module.Arena.InBounds(eye.Boss.Position) && !eye.Module.Arena.InBounds(new(0,383)),"Eye floor constrains navigation to its collision-supported platform");
        eye.Cast(eye.Boss,3344,4.5f);var voice=eye.Hints().ForbiddenZones.Single();
        Check(voice.shapeDistance.Distance(eye.Boss.Position)>0 && voice.shapeDistance.Distance(eye.Boss.Position+new WDir(15,0))<0 && voice.shapeDistance.Distance(eye.Boss.Position+new WDir(31,0))>0,"Dragon Voice response permits close-center or out-of-range escape");
        eye.Finish(eye.Boss);Check(eye.Hints().ForbiddenZones.Count==0,"Dragon Voice cancellation releases response");
        using var nape=new Fixture(0xDA0,new(130,265));nape.Start(new BossMod.RealmReborn.Alliance.A3TheNape.A3TheNape(nape.World,nape.Boss));
        var gate=nape.Create(501,0xE74,new(130,260));nape.World.Execute(new ActorState.OpCombat(gate.InstanceID,true));nape.Cast(gate,3439,15);
        var h=nape.Hints(0,nape.Boss,gate);Check(h.PotentialTargets[1].Priority==4 && h.PotentialTargets[1].ShouldBeInterrupted,"large gates get priority and native-eligible interrupt intent");
        using var jaws=new Fixture(0xDA4,new(0,-5));foreach(var player in jaws.Players)jaws.Move(player,new(0,-5),95.25f);jaws.Move(jaws.Boss,new(0,-5),95.25f);
        jaws.Start(new BossMod.RealmReborn.Alliance.A3HowlingAtomos.A3HowlingAtomos(jaws.World,jaws.Boss));jaws.Move(jaws.Players[1],new(0,-5),90.25f);jaws.Move(jaws.Players[2],new(-31.177f,49),95.25f);
        Check(jaws.Module.ResolveArenaProjectionLayer(jaws.Players[0])!=jaws.Module.ResolveArenaProjectionLayer(jaws.Players[1]) && jaws.Module.ResolveArenaProjectionLayer(jaws.Players[0])!=jaws.Module.ResolveArenaProjectionLayer(jaws.Players[2]),"Atomos floors distinguish both altitude and disconnected platform");
        jaws.Cast(jaws.Boss,3416,4);Check(jaws.Hints().ForbiddenZones.Count==1 && jaws.Hints(1).ForbiddenZones.Count==0 && jaws.Hints(2).ForbiddenZones.Count==0,"Shockwave under-boss response applies only to the correct upper floor");
        jaws.Finish(jaws.Boss);Check(jaws.Hints().ForbiddenZones.Count==0,"Shockwave cancellation clears response");
    }

}
