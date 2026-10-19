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
        public Actor Create(ulong id, uint oid, WPos pos, ActorType type = ActorType.Enemy, Class job = Class.None, float hitbox = .5f, float? y = null)
        {
            World.Execute(new ActorState.OpCreate(id, oid, (int)id, 0, "", 0, type, job, 50,
                new Vector4(pos.X, y ?? (oid == 0xDF6 || oid is 0xDF9 or 0xDFA or 0xDF8 || type == ActorType.Player && Boss?.OID == 0xDF6 ? 129.3f : 0), pos.Z, 0), hitbox, new(100, 100, 0, 0, 0), true, false, 0, 0, 0));
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
        foreach(var test in new Action[]{AngraColors,AngraLevelsAndDoom,AngraDoomRotation,AngraRoulette,AngraAddAOE,DragonMarkers,DragonResponses,DragonIce,DragonAssignments,CloudChase,CloudTowers,CloudGroups,CerberusFloorsAndBelly,CerberusChains,InterveningEncounters,AtomosMechanics,AtomosAdds,FrontalResponses})
            try { test(); } catch(Exception e) { failures.Add(e); Console.Error.WriteLine(test.Method.Name+": "+e.Message); }
        if(failures.Count!=0)throw new AggregateException(failures);
        Console.WriteLine($"World of Darkness synthetic regression: {assertions} checks passed; physical collision and live gameplay remain separate.");
    }
    static BossMod.Components.GenericAOEs AOE(Fixture f, string name) => f.Module.Components.OfType<BossMod.Components.GenericAOEs>().Single(c => c.GetType().Name == name);
    static void FrontalResponses()
    {
        using(var f=new Fixture(0xDFC,new(200,179)))
        {
            f.Move(f.Players[0],new(200,185));f.World.Execute(new ActorState.OpTarget(1,100));
            f.Start(new Dragon.A32FiveheadedDragon(f.World,f.Boss));
            var response=f.Module.Components.OfType<BossMod.Components.FrontalPositioning>().Single();
            AIHints H(){var h=new AIHints();response.AddAIHints(1,f.Players[1],default,h);return h;}
            Check(H().ForbiddenZones.Count==1,"dragon autoattack frontal receives early positioning guidance");
            f.Icon(f.Players[2],318);Check(H().ForbiddenZones.Count==0,"thunder stack response overrides quiet-phase frontal positioning");
            f.Event(f.Boss,3287,f.Players[2].Position,f.Players[2]);Check(H().ForbiddenZones.Count==1,"frontal positioning resumes after the actual stack resolves");
            f.Status(f.Players[1],639);Check(H().ForbiddenZones.Count==0,"Pyretic cannot receive a contradictory movement response");
        }
        using(var f=new Fixture(0xDF6,new(-28.5f,-300)))
        {
            f.Move(f.Boss,f.Boss.Position,-180);f.Move(f.Players[0],f.Boss.Position+new WDir(0,14),-180);
            f.Move(f.Players[1],f.Boss.Position+new WDir(0,12),-180);f.World.Execute(new ActorState.OpTarget(1,100));
            f.Start(new Cerberus.A33Cerberus(f.World,f.Boss));
            var response=f.Module.Components.OfType<BossMod.Components.FrontalPositioning>().Single();
            AIHints H(){var h=new AIHints();response.AddAIHints(1,f.Players[1],default,h);return h;}
            Check(H().ForbiddenZones.Count==1,"surface player receives Predator Claws positioning");
            f.Move(f.Players[1],f.Players[1].Position,-500);Check(H().ForbiddenZones.Count==0,"surface frontal cannot leak into the stomach");
            f.Move(f.Players[1],f.Players[1].Position,-180);f.Status(f.Players[1],438);
            Check(H().ForbiddenZones.Count==0,"miniature ingestion positioning overrides the quiet-phase frontal policy");
        }
    }
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
        Check(AOE(f,"LevelMechanics").ActiveAOEs(0,f.Players[0]).Length==1,"player circle publishes a filled radar hazard and forecast");
        Check(AOE(f,"LevelMechanics").ActiveAOEs(3,f.Players[3]).Length==0,"marked recipient has no self-avoidance hazard");
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
    static void AngraRoulette()
    {
        using var f = new Fixture(0xE00, new(-147.07f,296.92f)); f.Start(new Angra.A31AngraMainyu(f.World,f.Boss));
        var quarter=f.Create(501,2004776,f.Boss.Position,ActorType.EventObj);
        var glass=f.Create(502,0xE02,f.Boss.Position);
        var roulette=AOE(f,"Roulette");
        f.EState(quarter,8); f.Tick(.5);
        Check(roulette.ActiveAOEs(0,f.Players[0]).Length==0,"spinning Roulette does not forbid a transient quadrant");
        f.World.Execute(new ActorState.OpDead(glass.InstanceID,true)); f.Tick(.7);
        var hazard=roulette.ActiveAOEs(0,f.Players[0]);
        Check(hazard.Length==1,"last hourglass death warns before the late attack animation");
        Check(hazard[0].Check(f.Boss.Position+new WDir(-10,10)) && !hazard[0].Check(f.Boss.Position+new WDir(10,10)),"recorded final quadrant is southwest");
        Check(f.Hints().ForbiddenZones.Count==1,"Roulette radar shape also constrains AI");
        f.EAnim(quarter,1,16);Check(roulette.ActiveAOEs(0,f.Players[0]).Length==1,"lock animation preserves early warning");
        var reaper=f.Create(503,0xE01,f.Boss.Position);f.Event(reaper,3279,f.Boss.Position);
        Check(roulette.ActiveAOEs(0,f.Players[0]).Length==0,"Death resolves the selected quadrant");
        f.EState(quarter,4);f.EState(quarter,8);f.Tick(2.3);
        Check(roulette.ActiveAOEs(0,f.Players[0]).Length==1,"stopped rotation has a fallback without hourglass death");
        f.EAnim(quarter,1,2);Check(roulette.ActiveAOEs(0,f.Players[0]).Length==0,"end animation clears when no Death packet is recorded");
        f.EState(quarter,8);f.Tick(4);f.EState(quarter,1);
        Check(roulette.ActiveAOEs(0,f.Players[0]).Length==0,"reset removes the selected quadrant");
    }
    static void AngraDoomRotation()
    {
        using var f=new Fixture(0xE00,new(-147.07f,296.92f));f.Start(new Angra.A31AngraMainyu(f.World,f.Boss));
        var nw=f.Create(501,2004753,new(-156.52f,287.48f),ActorType.EventObj);
        var ne=f.Create(502,2004754,new(-137.59f,287.47f),ActorType.EventObj);
        var se=f.Create(503,2004755,new(-137.59f,306.38f),ActorType.EventObj);
        f.EState(nw,4);f.Tick(5);f.EAnim(nw,1,16);f.EState(ne,4);
        f.Status(f.Players[0],210);f.Move(f.Players[0],new(-156.51f,306.39f));f.Tick(9);
        var zone=f.Hints().ForbiddenZones.Single();
        Check(zone.shapeDistance.Distance(se.Position)>0 && zone.shapeDistance.Distance(ne.Position)<0,"late Doom targets next clockwise pad instead of chasing an expiring light");
        f.Tick(10);f.EAnim(ne,1,16);f.EState(se,4);
        Check(f.Hints().ForbiddenZones.Single().shapeDistance.Distance(se.Position)>0,"next observed light confirms the prepared destination");
        f.EState(se,1);Check(f.Hints().ForbiddenZones.Count==0,"reset invalidates light forecast");
        f.EState(nw,4);f.Tick(15);f.EState(se,4);f.Tick(19);
        Check(f.Hints().ForbiddenZones.Single().shapeDistance.Distance(se.Position)>0,"unexpected order drops the cycle model and follows observed light");
        f.Tick(20.4);Check(f.Hints().ForbiddenZones.Count==0,"missing light events expire stale destinations");
    }
    static void DragonResponses()
    {
        using var f=new Fixture(0xDFC,new(200,179));f.Start(new Dragon.A32FiveheadedDragon(f.World,f.Boss));
        f.Move(f.Players[0],new(200,190));f.Move(f.Players[1],new(200,189));
        f.Status(f.Players[0],420);
        Check(f.Hints().ForbiddenZones.Single().shapeDistance.Distance(f.Players[1].Position)>0,"Prey requires contact with the closest eligible recipient");
        f.Status(f.Players[1],475);
        Check(f.Hints().ForbiddenZones.Single().shapeDistance.Distance(f.Players[1].Position)<0,"Slippery Prey excludes the nearest recipient");
        f.Status(f.Players[0],639,1);
        Check(f.Hints().ForbiddenZones.Count==0 && f.Hints().ImminentSpecialMode.mode==AIHints.SpecialMode.Pyretic,"Pyretic takes precedence over Prey movement");
        f.World.Execute(new ActorState.OpStatus(f.Players[0].InstanceID,0,default));
        f.World.Execute(new ActorState.OpStatus(f.Players[0].InstanceID,1,default));
        f.Cast(f.Boss,3294,1.5f);
        var mode=f.Hints().ImminentSpecialMode;
        Check(mode.mode==AIHints.SpecialMode.Pyretic && Math.Abs((mode.activation-Now).TotalSeconds-1)<.001,"Heat Wave predicts no movement/actions before status delivery");
        f.Finish(f.Boss);Check(f.Hints().ImminentSpecialMode.mode==AIHints.SpecialMode.Normal,"cancelled Heat Wave clears preparation");
        f.Cast(f.Boss,3294,1.5f);f.Event(f.Boss,3294,f.Boss.Position);f.Finish(f.Boss);
        Check(f.Hints().ImminentSpecialMode.mode==AIHints.SpecialMode.Pyretic,"resolved Heat Wave retains the guide-supported five-second response");
        f.Tick(7);Check(f.Hints().ImminentSpecialMode.mode==AIHints.SpecialMode.Normal,"Heat Wave preparation expires");
        var pool=f.Create(510,2004780,new(210,190),ActorType.EventObj);
        Check(f.Hints().ForbiddenZones.Count==0,"poison suppression never sends an unprotected player into poison");
        f.Status(f.Players[0],640);
        Check(f.Hints().ForbiddenZones.Single().shapeDistance.Distance(pool.Position)>0,"poison resistance permits required center entry");
        f.Status(f.Players[2],640);f.Move(f.Players[2],pool.Position);
        Check(f.Hints().ForbiddenZones.Count==0,"existing resistant occupant suppresses duplicate assignment");
        f.Move(f.Players[2],new(180,180));
        f.Icon(f.Players[4],318);
        var suppression=f.Module.Components.Single(c=>c.GetType().Name=="PoisonSuppression");
        var suppressed=new AIHints();suppression.AddAIHints(0,f.Players[0],default,suppressed);
        Check(suppressed.ForbiddenZones.Count==0,"thunder stack assignment takes precedence over poison entry");
        f.Event(f.Boss,3287,f.Players[4].Position,f.Players[4]);
        var helper=f.Create(511,0x1B2,pool.Position);f.Cast(helper,3285,3);
        Check(f.Hints().ForbiddenZones.Count==1,"known fire overlap retains only the fire hazard and cancels poison entry");
        Check(f.Hints().ForbiddenZones[0].shapeDistance.Distance(pool.Position)<0,"poison assignment cannot force entry into an overlapping fire hazard");
        f.Finish(helper);f.World.Execute(new ActorState.OpDestroy(pool.InstanceID));
        Check(f.Hints().ForbiddenZones.Count==0,"destroyed poison puddle clears center-entry assignment");
        pool=f.Create(512,2004780,new(210,190),ActorType.EventObj);f.Cast(f.Boss,3294,1.5f);
        Check(f.Hints().ForbiddenZones.Count==0,"upcoming Heat Wave cancels poison movement assignment");
    }
    static void AngraAddAOE()
    {
        using var f=new Fixture(0xE00,new(-147.07f,296.92f));f.Start(new Angra.A31AngraMainyu(f.World,f.Boss));
        var add=f.Create(501,0xE4E,new(-162,296.92f));f.World.Execute(new ActorState.OpCombat(add.InstanceID,true));f.Cast(add,3358,4);
        var aoe=AOE(f,"EyesOnMe").ActiveAOEs(0,f.Players[0]);
        Check(aoe.Length==1 && aoe[0].Check(new(-130.21f,296.92f)) && !aoe[0].Check(new(-130.19f,296.92f)),"Eyes on Me is an escapable add-centered31.8y circle");
        Check(aoe[0].ActorID==add.InstanceID,"add AOE retains source identity");
        var h=f.Hints(0,add);Check(h.PotentialTargets[0].ShouldBeInterrupted && h.ForbiddenZones.Count==1,"interrupt hint retains hazard until cast actually cancels");
        f.Finish(add);Check(AOE(f,"EyesOnMe").ActiveAOEs(0,f.Players[0]).Length==0,"interrupted add cast clears only its hazard");
    }
    static void DragonIce()
    {
        using var f=new Fixture(0xDFC,new(200,179));f.Start(new Dragon.A32FiveheadedDragon(f.World,f.Boss));
        var helper=f.Create(501,0x1B2,new(200,185));var ice=AOE(f,"IceFloor");
        f.Cast(helper,3286,3);
        Check(ice.ActiveAOEs(0,f.Players[0]).Length==1,"ice growth is predicted from cast start");
        var a=ice.ActiveAOEs(0,f.Players[0])[0];
        Check(a.Shape is AOEShapeCircle {Radius:12} && a.ActorID==helper.InstanceID,"final ice footprint uses reviewed12y reference and cast identity");
        Check(Math.Abs((a.Activation-Now).TotalSeconds-10.4)<.001,"final-size forecast has its own delayed activation");
        var h=new AIHints();ice.AddAIHints(0,f.Players[0],default,h);
        Check(Math.Abs((h.ForbiddenZones.Single().activation-Now).TotalSeconds-5.5)<.001,"AI clears growth area with margin before earliest pool, separately from final-size activation");
        Check(a.Color==Colors.AOE,"growing ice is a subdued preview");
        f.Finish(helper);Check(ice.ActiveAOEs(0,f.Players[0]).Length==0,"cancelled ice prediction is removed");
        f.Cast(helper,3286,3);f.Tick(3);f.Event(helper,3286,helper.Position);f.Finish(helper);
        Check(ice.ActiveAOEs(0,f.Players[0]).Length==1,"successful ice cast bridges delayed object creation");
        f.Tick(6.4);var small=f.Create(510,2004777,helper.Position,ActorType.EventObj);
        Check(ice.ActiveAOEs(0,f.Players[0]).Length==1,"small stage consumes one cast prediction");
        f.Tick(8.4);var medium=f.Create(511,2004778,helper.Position,ActorType.EventObj);
        Check(ice.ActiveAOEs(0,f.Players[0]).Length==1,"overlapping medium stage remains one patch");
        f.Tick(10.4);var large=f.Create(512,2004779,helper.Position,ActorType.EventObj);
        a=ice.ActiveAOEs(0,f.Players[0])[0];
        Check(ice.ActiveAOEs(0,f.Players[0]).Length==1 && a.Color==Colors.Danger && a.ActorID==large.InstanceID,"observed large stage advances preview and retains actual source");
        Check(a.Check(helper.Position+new WDir(11.99f,0)) && !a.Check(helper.Position+new WDir(12.01f,0)),"final reference footprint has sharp12y boundary");
        ice.OnActorCreated(large);Check(ice.ActiveAOEs(0,f.Players[0]).Length==1,"duplicate create callback is idempotent");
        f.Event(helper,3286,helper.Position);Check(ice.ActiveAOEs(0,f.Players[0]).Length==1,"duplicate resolution after spawn does not add a phantom patch");
        f.World.Execute(new ActorState.OpDestroy(small.InstanceID));f.World.Execute(new ActorState.OpDestroy(medium.InstanceID));
        Check(ice.ActiveAOEs(0,f.Players[0]).Length==1,"old stage destruction cannot clear a live large patch");
        var other=f.Create(513,2004777,new(190,185),ActorType.EventObj);
        Check(ice.ActiveAOEs(0,f.Players[0]).Length==2,"simultaneous separate patch retains independent lifecycle");
        f.World.Execute(new ActorState.OpDestroy(large.InstanceID));
        Check(ice.ActiveAOEs(0,f.Players[0]).Length==1 && ice.ActiveAOEs(0,f.Players[0])[0].ActorID==other.InstanceID,"destroying one patch preserves the other");
        f.World.Execute(new ActorState.OpDestroy(other.InstanceID));Check(ice.ActiveAOEs(0,f.Players[0]).Length==0,"last source removal clears patch");
        f.Cast(helper,3286,3);f.Event(helper,3286,helper.Position);f.Finish(helper);f.Tick(22);
        Check(ice.ActiveAOEs(0,f.Players[0]).Length==0,"never-spawned successful cast prediction expires");
        small=f.Create(510,2004777,new(195,185),ActorType.EventObj);f.Tick(23);
        other=f.Create(514,2004777,small.Position,ActorType.EventObj);
        Check(ice.ActiveAOEs(0,f.Players[0]).Length==2,"coincident same-stage sources remain distinct generations");
        f.World.Execute(new ActorState.OpDestroy(small.InstanceID));Check(ice.ActiveAOEs(0,f.Players[0]).Length==1,"recreated actor identity and coincident lifecycles stay separate");
        using var b=new Fixture(0xDFC,new(200,179));
        b.Create(520,2004779,new(195,185),ActorType.EventObj);b.Create(521,2004777,new(195,185),ActorType.EventObj);b.Create(522,2004778,new(195,185),ActorType.EventObj);
        b.Start(new Dragon.A32FiveheadedDragon(b.World,b.Boss));
        Check(AOE(b,"IceFloor").ActiveAOEs(0,b.Players[0]).Length==1 && AOE(b,"IceFloor").ActiveAOEs(0,b.Players[0])[0].Activation==Now,"bootstrap unordered preexisting stages retains largest stage without duplicate patches");
    }
    static void DragonAssignments()
    {
        using var f=new Fixture(0xDFC,new(200,179));f.Start(new Dragon.A32FiveheadedDragon(f.World,f.Boss));
        foreach(var player in f.Players)f.Move(player,new(180,179));
        f.Move(f.Players[0],new(205,179));f.Move(f.Players[1],new(206,179));f.Move(f.Players[2],new(208,179));f.Status(f.Players[0],420);
        Check(f.Hints().ForbiddenZones.Single().shapeDistance.Distance(f.Players[1].Position)>0,"Prey initially picks nearest eligible recipient");
        f.Move(f.Players[2],new(205.1f,179));
        Check(f.Hints().ForbiddenZones.Single().shapeDistance.Distance(f.Players[1].Position)>0,"small distance changes do not churn the handoff destination");
        f.Status(f.Players[1],639);
        Check(f.Hints().ForbiddenZones.Single().shapeDistance.Distance(f.Players[2].Position)>0,"Pyretic recipient is excluded");
        f.Cast(f.Boss,3294,1.5f);Check(f.Hints().ForbiddenZones.Count==0,"Heat Wave prediction suspends Prey movement before status delivery");
        f.Event(f.Boss,3294,f.Boss.Position);f.Finish(f.Boss);
        Check(f.Hints().ForbiddenZones.Count==0,"resolved Heat Wave keeps Prey suspended for the response window");
        f.Tick(7);var ice=f.Create(530,2004779,f.Players[2].Position,ActorType.EventObj);
        var prey=f.Module.Components.Single(c=>c.GetType().Name=="AddsAndPrey");var hints=new AIHints();prey.AddAIHints(0,f.Players[0],default,hints);
        Check(hints.ForbiddenZones.Single().shapeDistance.Distance(new(180,179))>0,"Prey rejects a nearby recipient inside ice and retains safe alternative");
        f.Status(f.Players[0],487,1);hints=new();prey.AddAIHints(0,f.Players[0],default,hints);
        Check(hints.ForbiddenZones.Count==0,"frozen Prey cannot receive an impossible movement assignment");
        f.World.Execute(new ActorState.OpStatus(f.Players[0].InstanceID,1,default));
        f.Icon(f.Players[0],46);hints=new();prey.AddAIHints(0,f.Players[0],default,hints);
        Check(hints.ForbiddenZones.Count==0,"Fermata-marked Prey never forces contact with other players");
        using var p=new Fixture(0xDFC,new(200,179));p.Start(new Dragon.A32FiveheadedDragon(p.World,p.Boss));
        var pool=p.Create(540,2004780,new(210,179),ActorType.EventObj);
        p.Move(p.Players[0],new(209,179));p.Move(p.Players[1],new(205,179));p.Status(p.Players[0],640);p.Status(p.Players[1],640);
        var suppression=p.Module.Components.Single(c=>c.GetType().Name=="PoisonSuppression");
        AIHints Suppress(int slot) { var v=new AIHints();suppression.AddAIHints(slot,p.Players[slot],default,v);return v; }
        Check(Suppress(0).ForbiddenZones.Count==1 && Suppress(1).ForbiddenZones.Count==0,"one resistant occupant is assigned per poison pool");
        p.Move(p.Players[0],new(207,179));p.Move(p.Players[1],new(208.5f,179));
        Check(Suppress(0).ForbiddenZones.Count==1 && Suppress(1).ForbiddenZones.Count==0,"poison assignment remains stable while approaching");
        p.Move(p.Players[1],pool.Position);
        Check(Suppress(0).ForbiddenZones.Count==0 && Suppress(1).ForbiddenZones.Count==1,"actual resistant occupant takes precedence over approaching assignee");
        var pool2=p.Create(541,2004780,new(190,179),ActorType.EventObj);
        Check(Suppress(0).ForbiddenZones.Single().shapeDistance.Distance(pool2.Position)>0,"second pool receives a different eligible player");
        p.Move(p.Players[2],pool2.Position);Check(Suppress(0).ForbiddenZones.Count==0,"an existing unprotected occupant also prevents duplicate entry");
        p.Move(p.Players[2],new(200,179));
        p.Status(p.Players[0],420,1);Check(Suppress(0).ForbiddenZones.Count==0,"Prey excludes poison assignment");
        p.World.Execute(new ActorState.OpStatus(p.Players[0].InstanceID,1,default));
        p.Cast(p.Boss,3294,1.5f);p.Event(p.Boss,3294,p.Boss.Position);p.Finish(p.Boss);
        Check(Suppress(0).ForbiddenZones.Count==0 && Suppress(1).ForbiddenZones.Count==0,"resolved Heat Wave suppresses poison movement even before status arrives");
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
        jaws.Cast(jaws.Boss,3416,4);Check(jaws.Hints().ForbiddenZones.Count==1 && jaws.Hints(1).ForbiddenZones.Count==1 && jaws.Hints(2).ForbiddenZones.Count==0,"Shockwave stays on the correct upper floor while a lower player retains return-pad guidance");
        jaws.Finish(jaws.Boss);Check(jaws.Hints().ForbiddenZones.Count==0,"Shockwave cancellation clears response");
    }

    static void AtomosMechanics()
    {
        using var f=new Fixture(0xDA4,new(0,-5));
        foreach(var p in f.Players)f.Move(p,new(0,-5),95.25f);f.Move(f.Boss,new(0,-5),95.25f);
        f.Start(new BossMod.RealmReborn.Alliance.A3HowlingAtomos.A3HowlingAtomos(f.World,f.Boss));
        f.Move(f.Players[0],new(10,-5),95.25f);f.Move(f.Players[2],new(-31.177f,49),95.25f);
        f.Icon(f.Players[0],1);var pull=AOE(f,"BlackHole");
        Check(pull.ActiveAOEs(1,f.Players[1]).Length==1 && pull.ActiveAOEs(0,f.Players[0]).Length==0,"Black Hole warns other upper players without forbidding the marked player against their own cone");
        var cone=pull.ActiveAOEs(1,f.Players[1])[0];
        Check(cone.Check(new(10,-5)) && !cone.Check(new(0,5)) && cone.Shape is AOEShapeCone {Radius:19},"pull cone follows actual marked direction with reviewed19y range");
        Check(pull.ActiveAOEs(2,f.Players[2]).Length==0,"pull warning stays on its source platform");
        var h=f.Hints();Check(h.Teleporters.Count==1,"marked player receives a directed drop across the vertical boundary");
        Check(h.ForbiddenZones.Single().shapeDistance.Distance(new(20.5f,-5))>0 && h.ForbiddenZones[0].shapeDistance.Distance(new(0,-5))<0,"marked destination lies on the lower ring");
        Check(Math.Abs((h.ForbiddenZones[0].activation-Now).TotalSeconds-4.5)<.001,"pull departure deadline is earlier than observed5.26s resolution");
        Check(h.TemporaryObstacles.Single().Distance(new(18.5f,-5))<0,"ordinary walking cannot cross the platform wall");
        Check(f.Hints(1).Teleporters.Count==0,"unmarked players receive no downward transfer");
        f.Move(f.Players[0],new(20.5f,-5),90.25f);
        Check(f.Hints().Teleporters.Count==0 && f.Hints().ForbiddenZones.Count==0,"marked player waits below without chasing a landing point until pull resolves");
        Check(pull.ActiveAOEs(1,f.Players[1]).Length==1,"a target dropping does not retire the cone early for remaining upper players");
        f.Event(f.Boss,3414,f.Boss.Position,f.Players[1]);
        Check(pull.ActiveAOEs(1,f.Players[1]).Length==0,"only source Black Hole action retires its assignment");
        Check(f.Hints().ForbiddenZones.Single().shapeDistance.Distance(new(25.657f,-5))>0,"lower player seeks an inactive fixed return pad without requiring an activation event");
        f.Move(f.Players[1],new(0,20.5f),90.25f);
        Check(f.Hints(1).ForbiddenZones.Count==0,"captured players are not assigned pad movement before spit-out even before the status arrives");
        f.Event(f.Boss,3438,f.Boss.Position,f.Players[1]);
        Check(f.Hints(1).ForbiddenZones.Count==1,"spit-out releases captured player to wait on a return pad");
        f.Move(f.Players[3],new(9,-13),95.25f);
        var pad=f.Create(601,2004796,new(9.191285f,-14.19129f),ActorType.EventObj);f.Move(pad,pad.Position,95.25f);f.EAnim(pad,128,256);
        var pads=f.Module.Components.Single(c=>c.GetType().Name=="JumpPads");var ph=new AIHints();pads.AddAIHints(3,f.Players[3],default,ph);
        Check(ph.ForbiddenZones.Count==1 && ph.ForbiddenZones[0].shapeDistance.Distance(pad.Position)>0,"upper lit pad selects an eligible local occupant");
        ph=new();pads.AddAIHints(4,f.Players[4],default,ph);Check(ph.ForbiddenZones.Count==0,"other upper players are not redundantly assigned the same pad");
        f.Cast(f.Boss,3415,6);ph=new();pads.AddAIHints(3,f.Players[3],default,ph);
        Check(ph.ForbiddenZones.Count==0,"White Hole takes precedence over upper pad occupancy");
        Check(AOE(f,"WhiteHole").ActiveAOEs(3,f.Players[3]).Length==1,"spit-out forecast appears at cast start");
        f.Finish(f.Boss);f.EAnim(pad,4,8);ph=new();pads.AddAIHints(3,f.Players[3],default,ph);
        Check(ph.ForbiddenZones.Count==0,"upper pad off event removes occupancy assignment");
        f.Move(f.Players[0],new(10,-5),95.25f);f.Icon(f.Players[0],1);f.Tick(8);
        Check(pull.ActiveAOEs(1,f.Players[1]).Length==0,"missing pull resolution cannot leave a permanent marker");
        f.Move(f.Players[1],new(0,-5),95.25f);f.Icon(f.Players[0],1);f.World.Execute(new ActorState.OpDead(f.Boss.InstanceID,true));f.Tick(8.1);
        Check(pull.ActiveAOEs(1,f.Players[1]).Length==0,"boss death clears pull assignment");
    }
    static void AtomosAdds()
    {
        using var f=new Fixture(0xDA4,new(0,-5));foreach(var p in f.Players)f.Move(p,new(0,-5),95.25f);f.Move(f.Boss,new(0,-5),95.25f);
        f.Start(new BossMod.RealmReborn.Alliance.A3HowlingAtomos.A3HowlingAtomos(f.World,f.Boss));
        var soldier=f.Create(610,0xDA5,new(0,-5),hitbox:3);f.Move(soldier,soldier.Position,95.25f);f.Move(f.Players[3],new(0,3),95.25f);
        f.World.Execute(new ActorState.OpCombat(soldier.InstanceID,true));f.World.Execute(new ActorState.OpTarget(soldier.InstanceID,f.Players[3].InstanceID));f.Tick(0);
        var cleave=f.Module.Components.OfType<BossMod.Components.GenericBaitAway>().Single(c=>c.GetType().Name=="AstralPunch");
        var aura=f.Module.Components.OfType<BossMod.Components.GenericBaitAway>().Single(c=>c.GetType().Name=="Aura");
        Check(cleave.CurrentBaits.Count==1 && cleave.CurrentBaits[0].Shape is AOEShapeCone {Radius:11},"soldier frontal covers8y plus3y hitbox before an instant attack");
        Check(aura.CurrentBaits.Count==1 && aura.CurrentBaits[0].Shape is AOEShapeCircle {Radius:8} && aura.CenterAtTarget,"Aura maintains8y separation around current soldier recipient");
        var helper=f.Create(611,0x1B2,new(10,-5));f.Move(helper,helper.Position,95.25f);
        f.World.Execute(new ActorState.OpCastEvent(soldier.InstanceID,new(new(ActionType.Spell,2345),helper.InstanceID,0,24,new(10,95.25f,-5),1,0,default)));
        var moon=AOE(f,"HuntersMoon");Check(moon.ActiveAOEs(0,f.Players[0]).Length==1,"Hunter's Moon warns from its helper cue before blade spawn");
        Check(Math.Abs((moon.ActiveAOEs(0,f.Players[0])[0].Activation-Now).TotalSeconds-1.8)<.001,"blade warning uses a conservative early deadline");
        var blade=f.Create(612,0xBD8,new(10,-5),y:95.25f);f.Move(blade,blade.Position,95.25f);
        Check(moon.ActiveAOEs(0,f.Players[0]).Length==1 && moon.ActiveAOEs(0,f.Players[0])[0].ActorID==blade.InstanceID,"actual blade replaces one matching helper prediction");
        f.Move(blade,new(12,-5),95.25f);
        Check(moon.ActiveAOEs(0,f.Players[0])[0].Origin==blade.Position && moon.ActiveAOEs(0,f.Players[0])[0].Shape is AOEShapeCircle {Radius:5},"moving disc follows its actual source with5y radius");
        f.Move(f.Players[1],new(12,-5),90.25f);Check(moon.ActiveAOEs(1,f.Players[1]).Length==0,"upper blade does not contaminate lower-floor guidance");
        f.World.Execute(new ActorState.OpDestroy(blade.InstanceID));Check(moon.ActiveAOEs(0,f.Players[0]).Length==0,"blade destruction clears its hazard");
        f.Move(f.Players[3],f.Boss.Position,95.25f);f.Cast(f.Boss,3416,4);f.Tick(0);
        Check(aura.CurrentBaits.Count==0,"ongoing Aura separation cannot forbid the mandatory Shockwave stack");
        var platform=f.Module.Components.Single(c=>c.GetType().Name=="Platforms");var sh=new AIHints();platform.AddAIHints(0,f.Players[0],default,sh);
        Check(sh.ForbiddenZones.Single().shapeDistance.Distance(f.Boss.Position)>0 && sh.ForbiddenZones[0].shapeDistance.Distance(f.Boss.Position+new WDir(2,0))<0,"Shockwave requires arrival almost directly under Atomos");
        Check(Math.Abs((sh.ForbiddenZones[0].activation-Now).TotalSeconds-3.5)<.001,"Shockwave response precedes cast completion by half a second");
        var urgent=f.Hints(0,soldier).PotentialTargets.Single();
        Check(urgent.Priority==5 && urgent.ShouldBeStunned,"a living same-platform soldier receives urgent native kill/control intent during Shockwave");
        f.Finish(f.Boss);f.Tick(0);Check(aura.CurrentBaits.Count==1,"Aura separation resumes after Shockwave");
        var normal=f.Hints(0,soldier).PotentialTargets.Single();
        Check(normal.Priority==3 && !normal.ShouldBeStunned,"Shockwave completion retires the temporary control request");
        f.World.Execute(new ActorState.OpDead(soldier.InstanceID,true));f.Tick(1);
        Check(cleave.CurrentBaits.Count==0 && aura.CurrentBaits.Count==0,"dead soldier clears both splash and frontal warnings");
    }

}
