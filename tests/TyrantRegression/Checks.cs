using System.Numerics;
using BossMod;
using BossMod.Components;
using BossMod.Dawntrail.Raid.M11NTheTyrant;

static partial class Checks
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
    private static int assertions;
    private static uint sequence = 1;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        ++assertions;
    }
    private static Actor Actor(ulong id, uint oid, WPos pos, Class job = Class.None, float radius = 0.5f)
        => new(id, oid, 0, 0, "", 0, job != Class.None ? ActorType.Player : ActorType.Enemy, job, 100,
            new Vector4(pos.X, 0, pos.Z, 0), radius, new(100, 100, 0, 100, 100));
    private static ActorCastInfo Cast(uint action, WPos position, float duration = 5.7f, Angle rotation = default, ulong target = 0)
        => new() { Action = new(ActionType.Spell, action), Location = new(position.X, 0, position.Z), TotalTime = duration, Rotation = rotation, TargetID = target };
    private static ActorCastEvent Hit(uint action, ulong target = 0) => new(new(ActionType.Spell, action), target, 0, 0, default, sequence++, 0, default);
    private static T Component<T>(BossModule module, string name) where T : BossComponent => (T)module.Components.Single(c => c.GetType().Name == name);
    private static M11NTheTyrant Module()
    {
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var boss = Actor(100, (uint)OID.Boss, new(100, 100), radius: 5);
        world.Actors.Actors.Add(boss.InstanceID, boss);
        var module = new M11NTheTyrant(world, boss);
        module.StateMachine.Start(Now);
        return module;
    }
    private static Actor Create(BossModule module, Actor actor)
    {
        module.WorldState.Execute(new ActorState.OpCreate(actor.InstanceID, actor.OID, actor.SpawnIndex, actor.LayoutID, actor.Name, actor.NameID,
            actor.Type, actor.Class, actor.Level, actor.PosRot, actor.HitboxRadius, actor.HPMP, true, false, 0, 0, 0));
        return module.WorldState.Actors.Find(actor.InstanceID)!;
    }
    private static Actor Player(BossModule module, ulong id, WPos pos, Class job = Class.SGE, int slot = 0)
    {
        var actor = Create(module, Actor(id, 0, pos, job));
        module.WorldState.Execute(new PartyState.OpModify(slot, new(0, id, false)));
        return actor;
    }
    private static AIHints Hints(BossComponent component, Actor player, int slot = 0)
    {
        var hints = new AIHints(); component.AddAIHints(slot, player, default, hints); return hints;
    }
    private static void Start(BossModule module, Actor caster, uint action, float duration = 5.7f, Angle rotation = default, ulong target = 0)
        => module.WorldState.Execute(new ActorState.OpCastInfo(caster.InstanceID, Cast(action, caster.Position, duration, rotation, target)));
    private static void Finish(BossModule module, Actor caster)
    {
        if (caster.CastInfo != null) caster.CastInfo.ElapsedTime = caster.CastInfo.TotalTime;
        module.WorldState.Execute(new ActorState.OpCastInfo(caster.InstanceID, null));
    }
    private static void Event(BossModule module, Actor caster, uint action, ulong target = 0)
        => module.WorldState.Execute(new ActorState.OpCastEvent(caster.InstanceID, Hit(action, target)));
    private static void Director(BossModule module, uint state)
        => module.WorldState.Execute(new WorldState.OpDirectorUpdate(0, 0x8000000D, state, 0, 0, 0));
    private static bool Forbidden(AIHints hints, WPos p) => hints.ForbiddenZones.Any(z => z.shapeDistance.Contains(p));

    public static void Run(string gameDirectory, string? projectionPath)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"), new Lumina.LuminaOptions());
        Service.LuminaGameData = game;
        Service.Config.Initialize();
        ArenaAndLaunches(); AvalancheVariants(); Weapons(); Portals(); Stardust(); TowersAndExplosions(); Tethers(); Shelter(); Reset();
        RadarWaves(); RecordedGustWaves();
        if (projectionPath != null) Projection(projectionPath);
        Console.WriteLine($"{assertions} Tyrant assertions passed (synthetic and optional projected callbacks; no physical or live gameplay proof).");
    }
    private static void ArenaAndLaunches()
    {
        using var m = Module(); var p = Player(m, 1, new(84, 100));
        Check(m.Arena.InBounds(new(100, 100)) && !m.Arena.InBounds(new(126, 100)), "initial 40y arena");
        var flatHelper = Create(m, Actor(9, (uint)OID.Helper, new(100,100)));
        Start(m,flatHelper,47759,5.7f);
        var flat = Component<SimpleKnockbacks>(m,"FlatlinerKnockUp");
        Check(!Forbidden(Hints(flat,p,0),new(90,100)) && Forbidden(Hints(flat,p,0),new(110,100)), "even party slots land west for later tower coverage");
        Check(Forbidden(Hints(flat,p,1),new(90,100)) && !Forbidden(Hints(flat,p,1),new(110,100)), "odd party slots land east instead of all AIs choosing one side");
        Finish(m,flatHelper);
        Director(m, 2);
        foreach (var point in new WPos[] { new(75, 81), new(93, 119), new(107, 81), new(125, 119) })
            Check(m.Arena.InBounds(point), "split floor contains independently specified interior " + point);
        foreach (var point in new WPos[] { new(100, 100), new(95, 100), new(105, 100), new(73, 100), new(127, 100), new(84, 121) })
            Check(!m.Arena.InBounds(point), "split arena excludes gap and lethal edge " + point);
        var map = new BossMod.Pathfinding.Map(); m.Arena.Bounds.PathfindMap(map, m.Center);
        var gap = map.WorldToGrid(new(100, 100));
        Check(!map.InBounds(gap.x, gap.y) || map.PixelMaxG[map.GridToIndex(gap)] < 0, "gap is impassable in the native pathfinding raster");
        var helper = Create(m, Actor(10, (uint)OID.Helper, new(84, 89)));
        Start(m, helper, 46061, 9.7f);
        var towers = Component<GenericTowers>(m, "ExplosionTower");
        var kb = Component<SimpleKnockbacks>(m, "ExplosionKnockUp");
        Check(towers.Towers.Count == 1 && towers.Towers[0].Activation == Now.AddSeconds(10), "launch tower has a real future deadline");
        Check(kb.Casters.Count == 1 && kb.Casters[0].IgnoreImmunes, "mandatory launch ignores immunity");
        var before = Hints(kb, p);
        Check(before.ActionsToExecute.Entries.Count == 0, "launch never queues immunity abilities");
        Check(!Forbidden(before, new(84, 91.8f)), "north tower permits safe same-platform south launch");
        Check(Forbidden(before, new(86, 88.25f)), "observed fall direction is forbidden by landing constraint");
        Check(!Forbidden(before, new(116, 91)), "west tower does not constrain unrelated east points");
        Start(m, m.PrimaryActor, 46065, 7.7f, -90f.Degrees());
        Check(Forbidden(Hints(kb, p), new(84, 91.8f)), "Avalanche rejects landing on doomed west platform");
        Check(!Forbidden(Hints(kb, p), new(87.2f, 89)), "Avalanche allows eastward cross-gap launch");
        Check(Forbidden(Hints(kb, p), new(90, 100)), "everyone on doomed side must enter launch tower");
        p.PosRot = new(90,0,100,0);
        var navigationHints = Hints(kb,p);
        navigationHints.PathfindMapBounds = m.Bounds;
        navigationHints.PathfindMapCenter = m.Center;
        navigationHints.Normalize();
        var context = new BossMod.Pathfinding.NavigationDecision.Context();
        var decision = BossMod.Pathfinding.NavigationDecision.Build(context,Now,navigationHints,p);
        Check(decision.Destination is {} destination && m.Arena.InBounds(destination) && destination.X < 94f,
            "native navigation prepositions on current platform rather than trying to walk the gap");
        Check(decision.TimeToGoal < 9f, "native launch approach fits conservative tower deadline");
        Event(m, helper, 46061); Check(towers.Towers.Count == 0, "individual launch hit retires matching tower");
        Finish(m, helper); Check(kb.Casters.Count == 0, "launch finishes without stale knockback");
        Director(m, 8); Check(!m.Arena.InBounds(new(84, 100)) && m.Arena.InBounds(new(116, 100)), "west platform destruction removes physical floor");
        Director(m, 1); Check(m.Arena.InBounds(new(100, 100)) && !m.Arena.InBounds(new(126, 100)), "repair restores original floor");
        Director(m, 2); Start(m, m.PrimaryActor, 46069, 7.7f, 90f.Degrees()); Director(m, 16);
        Check(m.Arena.InBounds(new(84, 100)) && !m.Arena.InBounds(new(116, 100)), "mirrored Avalanche removes east floor");
    }
    private static void AvalancheVariants()
    {
        foreach (var (visual, toss, west) in new (uint, uint, bool)[] { (46063, 46064, true), (46065, 46066, true), (46067, 46068, false), (46069, 46070, false) })
        {
            using var m = Module();
            var p = Player(m, 1, west ? new(77.56f, 107.76f) : new(122.44f, 92.24f));
            Director(m, 2);
            var origin = west ? new WPos(84, 111) : new WPos(116, 89);
            var helper = Create(m, Actor(10, (uint)OID.Helper, origin));
            // At the recorded visual cue the ten-second tower has only 4.85s remaining.
            Start(m, helper, 46061, 4.55f);
            Player(m, 2, west ? new(86.84f, 109.54f) : new(113.16f, 90.46f), Class.WAR, 1);
            var tower = Component<GenericTowers>(m, "ExplosionTower");
            var kb = Component<SimpleKnockbacks>(m, "ExplosionKnockUp");
            Start(m, m.PrimaryActor, visual, 7.7f, (west ? -90f : 90f).Degrees());
            Check(tower.ActiveTowers(0, p)[0].MaxSoakers == 8, $"Avalanche {visual} lets everyone use an occupied escape tower");
            var hints = Hints(kb, p);
            tower.AddAIHints(0, p, default, hints);
            Check(Forbidden(hints, p.Position), $"Avalanche {visual} requires evacuation before removal");
            var launch = west ? new WPos(87.2f, 111) : new WPos(112.8f, 89);
            Check(!Forbidden(hints, launch), $"Avalanche {visual} allows launch onto surviving platform");
            Check(Forbidden(hints, west ? new(84, 108) : new(116, 92)), $"Avalanche {visual} rejects launch back onto doomed platform");
            Check(hints.ForbiddenZones.All(z => z.activation <= Now.AddSeconds(4.851)), $"Avalanche {visual} uses tower deadline, not later visual finish");
            hints.PathfindMapBounds = m.Bounds;
            hints.PathfindMapCenter = m.Center;
            hints.Normalize();
            var decision = BossMod.Pathfinding.NavigationDecision.Build(new(), Now, hints, p);
            Check(decision.Destination is {} destination && !Forbidden(hints, destination) && m.Arena.InBounds(destination), $"Avalanche {visual} native navigation reaches a valid launch point");
            Check(decision.TimeToGoal < 3.85f, $"Avalanche {visual} launch approach fits deadline with native cushion");
            var tossHelper = Create(m, Actor(11, (uint)OID.Helper, new(95.66f, 97.49f)));
            Start(m, tossHelper, toss, 17.2f, 60f.Degrees());
            var aoes = Component<GenericAOEs>(m, "ArcadionAvalancheToss");
            Check(aoes.ActiveAOEs(0, p).Length == 1, $"Avalanche {toss} advertises the thrown-platform hazard");
            Finish(m, tossHelper);
            Check(aoes.ActiveAOEs(0, p).Length == 0, $"Avalanche {toss} clears on resolution");
        }
    }
    private static void Weapons()
    {
        using var m = Module(); var p = Player(m, 1, new(100, 100));
        var aoe = Component<GenericAOEs>(m, "WeaponAOEs");
        Start(m, m.PrimaryActor, 46007, 1.7f);
        Check(aoe.ActiveAOEs(0, p).Length == 1 && aoe.ActiveAOEs(0,p)[0].Check(p.Position), "Draw Steel axe supplies hazard before Smashdown telegraph");
        Check(aoe.ActiveAOEs(0,p)[0].Activation == Now.AddSeconds(11.1d), "draw warning keeps future activation for uptime");
        Finish(m, m.PrimaryActor);
        Check(aoe.ActiveAOEs(0,p).Length == 1, "normal draw finish retains prediction");
        var helper = Create(m, Actor(10, (uint)OID.Helper, new(100, 100)));
        Start(m, helper, 46011, 2.7f);
        Check(aoe.ActiveAOEs(0,p).Length == 1 && aoe.ActiveAOEs(0,p)[0].ActorID == 10, "helper replaces early forecast without duplicate hazard");
        Event(m, helper, 46011); Finish(m, helper); Check(aoe.ActiveAOEs(0,p).Length == 0, "helper resolution clears axe");
        Start(m, m.PrimaryActor, 46008, 1.7f); m.WorldState.Execute(new ActorState.OpCastInfo(100, null));
        Check(aoe.ActiveAOEs(0,p).Length == 0, "interrupted draw clears prediction");
        Start(m, m.PrimaryActor, 46028, 2.7f); Finish(m, m.PrimaryActor);
        var sword = Create(m, Actor(11, (uint)OID.Sword, new(91.515f,108.485f)));
        var axe = Create(m, Actor(12, (uint)OID.Axe, new(96.894f,88.409f)));
        var scythe = Create(m, Actor(13, (uint)OID.Scythe, new(111.591f,103.106f)));
        foreach (var (a, timeline) in new[] { (sword, (ushort)4561), (axe, (ushort)4562), (scythe, (ushort)4563) })
            m.WorldState.Execute(new ActorState.OpPlayActionTimelineEvent(a.InstanceID, timeline));
        m.WorldState.Execute(new ActorState.OpTether(sword.InstanceID, new(249, axe.InstanceID)));
        m.WorldState.Execute(new ActorState.OpTether(axe.InstanceID, new(249, scythe.InstanceID)));
        m.WorldState.Execute(new ActorState.OpTether(100, new(249, sword.InstanceID)));
        Start(m, m.PrimaryActor, 46029, 4.7f);
        var planned = aoe.ActiveAOEs(0,p).ToArray();
        Check(planned.Length == 3 && planned.Count(a=>a.Risky)==1 && planned.Single(a=>a.Risky).ActorID==11, "Evolved forecasts tether order with next weapon owning AI movement");
        Check(planned.Single(a=>a.ActorID==11).Color==Colors.Danger && planned.Where(a=>a.ActorID!=11).All(a=>a.Color==Colors.AOE), "Evolved distinguishes the next weapon from later previews");
        Check(planned[0].ActorID==13 && planned[^1].ActorID==11, "later weapons draw below the next weapon");
        Check(planned.Count(a=>a.Activation<=AOESequence.PreviewDeadline(planned))==2, "weapon radar shows only current attack and one preview while retaining all three predictions");
        Check(planned.Single(a=>a.ActorID==13).Activation - planned.Single(a=>a.ActorID==11).Activation == TimeSpan.FromSeconds(8.3), "Evolved retains staggered weapon deadlines");
        Finish(m, m.PrimaryActor); Start(m, helper, 46032, 1.7f);
        // Synthetic helper at a different position must not silently remove an unrelated forecast.
        Check(aoe.ActiveAOEs(0,p).Length == 4, "mismatched helper origin cannot consume weapon forecast");
        var active = aoe.ActiveAOEs(0,p).ToArray();
        Check(active.Single(a=>a.ActorID==10).Color==Colors.Danger && active.Where(a=>a.ActorID!=10).All(a=>a.Color==Colors.AOE), "an actual weapon helper is highlighted above queued forecasts");
        Check(active.Count(a=>a.Risky)==1 && Hints(aoe,p).ForbiddenZones.Count==1, "weapon preview styling preserves existing AI ownership");
        Finish(m,helper);
        Check(aoe.ActiveAOEs(0,p).ToArray().Single(a=>a.ActorID==11).Color==Colors.Danger, "weapon forecasts promote when the active helper retires");
    }
    private static void Portals()
    {
        using var m = Module(); var p = Player(m, 1, new(84,100));
        var aoe = Component<GenericAOEs>(m, "MajesticMeteorain");
        m.WorldState.Execute(new WorldState.OpMapEffect(2,0x00020001));
        m.WorldState.Execute(new WorldState.OpMapEffect(7,0x00020001));
        var a = aoe.ActiveAOEs(0,p).ToArray();
        Check(a.Length==2 && a.All(v=>v.Activation==Now.AddSeconds(10.2)), "portal spawn warns before helper cast with future deadline");
        Check(a.Any(v=>v.Origin.AlmostEqual(new(79,75),0.01f)) && a.Any(v=>v.Origin.AlmostEqual(new(131,95),0.01f)), "split normal portal coordinates bind correct row/column");
        var helper=Create(m,Actor(10,(uint)OID.Helper,new(79,75)));
        Start(m,helper,46059);
        Check(aoe.ActiveAOEs(0,p).Length==2, "helper replaces matching portal forecast");
        var styled=aoe.ActiveAOEs(0,p).ToArray();
        Check(styled.Single(v=>v.ActorID==10).Color==Colors.Danger && styled.Single(v=>v.ActorID!=10).Color==Colors.AOE, "portal lanes with later activations remain translucent previews");
        Check(Hints(aoe,p).ForbiddenZones.Count==2 && styled.All(v=>v.Risky), "portal previews retain all timed AI constraints");
        Finish(m,helper); m.WorldState.Execute(new WorldState.OpMapEffect(2,0x00080004));
        Check(aoe.ActiveAOEs(0,p).Length==1, "one lane resolves without clearing sibling");
        Check(aoe.ActiveAOEs(0,p)[0].Color==Colors.Danger, "remaining portal lane promotes after earlier helper resolves");
        m.WorldState.Execute(new WorldState.OpMapEffect(7,0x00080004));
        m.WorldState.Execute(new WorldState.OpMapEffect(24,0x00020001));
        Check(aoe.ActiveAOEs(0,p)[0].Origin==new WPos(105,75), "restored arena portal uses full-arena coordinates");
    }
    private static void Stardust()
    {
        using var m=Module(); var p=Player(m,1,new(100,100)); var q=Player(m,2,new(110,100),slot:1);
        var c=Component<GenericStackSpread>(m,"VoidStardust");
        m.WorldState.Execute(new ActorState.OpIcon(1,630,1)); m.WorldState.Execute(new ActorState.OpIcon(2,630,2));
        var h=Create(m,Actor(10,(uint)OID.Helper,new(100,100)));
        Start(m,h,46025,4.7f,target:1);
        Check(c.Spreads.Count==2 && c.Spreads.Single(s=>s.Target==p).Activation==Now.AddSeconds(5), "target cast refines icon deadline without duplicating spread");
        var repeated=Hit(46025,1);
        m.WorldState.Execute(new ActorState.OpCastEvent(10,repeated));
        m.WorldState.Execute(new ActorState.OpCastEvent(10,repeated));
        Event(m,h,46027,1);
        for(var i=0;i<24;i++)Event(m,h,46026);
        Check(c.Spreads.Count==2, "ground explosions cannot resolve moving spreads");
        Event(m,h,46027,1);
        Check(c.Spreads.Count==1 && c.Spreads[0].Target==q, "third hit clears only its own target");
        Event(m,h,46025,2);Event(m,h,46027,2);Event(m,h,46027,2);
        Check(c.Spreads.Count==0, "both three-hit target sequences retire independently");
        m.WorldState.Execute(new ActorState.OpIcon(1,630,1));
        Start(m,m.PrimaryActor,46024,4.7f);
        Check(c.Spreads.Count==0,"new Stardust windup clears an interrupted old sequence");
    }
    private static void TowersAndExplosions()
    {
        using var m=Module(); var p=Player(m,1,new(100,100)); var boss=m.PrimaryActor;
        var c=Component<GenericTowers>(m,"HeartbreakKick");
        var helper=Create(m,Actor(10,(uint)OID.Helper,new(100,100)));
        for(var wave=0;wave<2;wave++)
        {
            Start(m,boss,46079,4.7f);
            Check(c.Towers.Count==1 && c.Towers[0].MinSoakers==8, "Heartbreak calls full party to share damage");
            Event(m,boss,46079);Finish(m,boss);
            Check(c.Towers.Count==1, "visual finish cannot remove multi-hit tower");
            for(var i=0;i<5;i++)
            {
                var ev=Hit(46080);m.WorldState.Execute(new ActorState.OpCastEvent(10,ev));m.WorldState.Execute(new ActorState.OpCastEvent(10,ev));
                Check(c.Towers.Count==(i<4?1:0), "five distinct helper pulses retain then clear tower; duplicate packet does not advance");
            }
        }
        Start(m,boss,46079,4.7f);m.WorldState.Execute(new ActorState.OpCastInfo(100,null));Check(c.Towers.Count==0,"cancelled Heartbreak clears tower");
        var dance=Component<BossComponent>(m,"DanceOfDomination");
        Start(m,helper,46034,4.7f);
        Check(Hints(dance,p).PredictedDamage.Count==7,"Dance forecasts all seven observed helper pulses for healing");
        Event(m,helper,46034);Finish(m,helper);
        Check(Hints(dance,p).PredictedDamage.Count==6,"initial helper finish retains later Dance pulses");
        for(var i=0;i<5;i++)Event(m,helper,46036);
        Event(m,helper,47081);
        Check(Hints(dance,p).PredictedDamage.Count==0,"last Dance helper clears all repeated damage predictions");
        var lines=Component<GenericAOEs>(m,"Explosion1");
        Start(m,helper,47033,6.2f); Check(lines.ActiveAOEs(0,p).Length==1,"second Dance explosion ID is handled");Finish(m,helper);
        var wall=Component<GenericAOEs>(m,"WallOfFireExplosion");
        Start(m,helper,46077,2.7f);Check(wall.ActiveAOEs(0,p).Length==1,"Wall of Fire aftermath uses its own correct action");Finish(m,helper);
        var gust=Component<GenericAOEs>(m,"PowerfulGust");
        Start(m,helper,46041,5.7f);var h2=Create(m,Actor(11,(uint)OID.Helper,new(90,100)));Start(m,h2,46041,5.7f,45f.Degrees());
        Check(gust.ActiveAOEs(0,p).Length==2 && gust.ActiveAOEs(0,p).ToArray().All(a=>a.Activation>Now),"consecutive gust waves carry deadlines instead of immediate full-arena union");
        Finish(m,h2);Check(gust.ActiveAOEs(0,p).Length==1 && gust.ActiveAOEs(0,p)[0].ActorID==10,"out-of-order gust finish removes its own helper");
    }
    private static void Tethers()
    {
        using var m=Module();var tank1=Player(m,1,new(100,100),Class.PLD);var tank2=Player(m,2,new(100,100),Class.WAR,1);
        var healer=Player(m,3,new(100,100),slot:2);
        var north=Create(m,Actor(10,(uint)OID.TheTyrant,new(100,75)));var south=Create(m,Actor(11,(uint)OID.TheTyrant,new(100,125)));
        var rock1=Create(m,Actor(12,(uint)OID.Comet,new(106,93)));var rock2=Create(m,Actor(13,(uint)OID.Comet,new(94,106)));
        m.WorldState.Execute(new ActorState.OpTether(10,new(356,12)));m.WorldState.Execute(new ActorState.OpTether(11,new(356,13)));
        var c=Component<BossComponent>(m,"ForegoneFatality");
        Check(Hints(c,tank1).ForbiddenZones.Count==1 && !Forbidden(Hints(c,tank1),new(103,84)), "first tank intercepts north tether while original target is a non-party rock");
        Check(Hints(c,tank2,1).ForbiddenZones.Count==1 && !Forbidden(Hints(c,tank2,1),new(97,115.5f)), "second tank receives distinct south tether");
        Check(Hints(c,healer,2).ForbiddenZones.Count==0,"single-target intercept invents no splash avoidance");
        m.WorldState.Execute(new ActorState.OpTether(10,new(356,1)));
        Check(Hints(c,tank1).ForbiddenZones.Count==0 && Hints(c,tank1).PredictedDamage.Count==1,"intercepted tank holds tether and receives mitigation prediction");
        var originalDeadline=Hints(c,tank1).PredictedDamage[0].Activation;
        m.WorldState.Frame=new(Now.AddSeconds(3),0,0,0,0,1);
        m.WorldState.Execute(new ActorState.OpTether(10,new(356,3)));
        Check(Hints(c,healer,2).PredictedDamage.Single().Activation==originalDeadline,"retarget preserves original portal deadline");
        m.WorldState.Execute(new ActorState.OpDestroy(11));
        Check(Hints(c,tank2,1).ForbiddenZones.Count==0,"destroyed source withdraws its assignment");
    }
    private static void Shelter()
    {
        using var m=Module();var p=Player(m,1,new(114,86));
        var near=Create(m,Actor(10,(uint)OID.Comet,new(106,94),radius:2.16f));
        var far=Create(m,Actor(11,(uint)OID.Comet,new(112,88),radius:2.16f));
        var wrong=Create(m,Actor(12,(uint)OID.Comet,new(94,94),radius:2.16f));
        foreach(var r in new[]{near,far,wrong})Event(m,r,46046);
        Start(m,m.PrimaryActor,46053,7.7f);
        var c=Component<GenericAOEs>(m,"Tyrannhilation");
        Check(!Forbidden(Hints(c,p),new(115,85)), "two-rock shelter permits far shadow before first blast");
        Check(Forbidden(Hints(c,p),new(91,91)), "isolated rock is not safe for the double blast");
        Event(m,m.PrimaryActor,46053);Finish(m,m.PrimaryActor);
        Check(c.ActiveAOEs(0,p).Length==1,"first visual finish retains shelter guidance through delayed blast");
        var h=Create(m,Actor(13,(uint)OID.Helper,new(100,100)));Start(m,h,46215,4.2f);
        Event(m,h,46054);
        Check(Forbidden(Hints(c,p),new(91,91)), "first shockwave cannot briefly advertise doomed rocks as second-hit cover");
        Event(m,near,46055);Event(m,wrong,46055);
        Check(!Forbidden(Hints(c,p),new(115,85)), "far shelter remains after nearest rocks explode");
        Event(m,h,46215);Finish(m,h);Check(c.ActiveAOEs(0,p).Length==0,"second helper action clears shelter");
    }
    private static void Reset()
    {
        using var m=Module();var p=Player(m,1,new(100,100));
        Start(m,m.PrimaryActor,46007,1.7f);Director(m,2);
        m.StateMachine.Reset();m.StateMachine.Start(Now);
        Check(m.Arena.InBounds(new(100,100)),"new phase instance restores arena and clears split state");
        m.WorldState.Execute(new ActorState.OpCastInfo(100,null));
        Check(Component<GenericAOEs>(m,"WeaponAOEs").ActiveAOEs(0,p).Length==0,"interrupted bootstrap draw cannot leak into fresh pull");
    }
}
