using BossMod;
using BossMod.Components;
using BossMod.Dawntrail.Raid.M11NTheTyrant;

static partial class Checks
{
    private static int AssignedSide(BossComponent component, Actor actor)
        => (int)component.GetType().GetMethod("LandingSide")!.Invoke(component, [actor])!;
    private static ulong AssignedTower(BossComponent component, Actor actor)
        => (ulong)component.GetType().GetMethod("RequiredTower")!.Invoke(component, [actor])!;
    private static void LaunchCoverage()
    {
        // Each client sees itself as slot zero. Rotate all eight party lists and verify one plan.
        for (var client = 0; client < 8; ++client)
        {
            using var m = Module();
            var players = new Actor[8];
            for (var slot = 0; slot < 8; ++slot)
            {
                var identity = (client + slot) % 8;
                players[identity] = Player(m, (ulong)(200 + identity), new(100, 100), slot: slot);
            }
            var helper = Create(m, Actor(10, (uint)OID.Helper, new(100, 100)));
            Start(m, helper, 47759, 5.7f);
            var flat = Component<SimpleKnockbacks>(m, "FlatlinerKnockUp");
            for (var identity = 0; identity < 8; ++identity)
                Check(AssignedSide(flat, players[identity]) == ((identity & 1) == 0 ? -1 : 1), "Flatliner assignment is identical across all local-slot-zero clients");
            var markers = Component<GenericAOEs>(m, "LaunchPositionHints");
            CheckMarkers(markers, flat, players[client], client, 15);
            Finish(m, helper);
            Check(markers.ActiveAOEs(0, players[client]).Length == 0, "Flatliner green guides clear with their source");
        }
        foreach (var westCount in new[] { 0, 1, 2, 4, 6, 7, 8 })
        {
            using var m = Module(); Director(m, 2);
            var players = Enumerable.Range(0,8).Select(i => Player(m,(ulong)(200+i),new(i<westCount?84:116,i%2==0?90:110),slot:i)).ToArray();
            var helpers = new[] { new WPos(84,89),new WPos(84,111),new WPos(116,89),new WPos(116,111) }
                .Select((pos,i)=>Create(m,Actor((ulong)(10+i),(uint)OID.Helper,pos))).ToArray();
            foreach(var helper in helpers)Start(m,helper,46061,9.7f);
            var kb=Component<SimpleKnockbacks>(m,"ExplosionKnockUp");
            var tower=Component<GenericTowers>(m,"ExplosionTower");
            var markers=Component<GenericAOEs>(m,"LaunchPositionHints");
            var sides=players.Select(p=>AssignedSide(kb,p)).ToArray();
            Check(sides.Count(s=>s<0)>=2 && sides.Count(s=>s>0)>=2, "ordinary launches preserve/repair two players per island from split "+westCount+"/"+(8-westCount));
            if(westCount is >=2 and <=6)
                Check(players.All(p=>AssignedSide(kb,p)==Math.Sign(p.Position.X-100)), "sufficient island coverage uses same-island landings");
            foreach(var p in players.Where(p=>AssignedTower(kb,p)!=0))
            {
                var required=AssignedTower(kb,p);
                var viewed=tower.ActiveTowers(m.Raid.FindSlot(p.InstanceID),p).ToArray();
                Check(viewed.Length==1 && viewed[0].ActorID==required && viewed[0].MaxSoakers==8, "assigned soaker can enter its tower even when occupied");
                CheckMarkers(markers,kb,p,m.Raid.FindSlot(p.InstanceID),25);
            }
            // Move actors as if they crossed: the wave's plan must not reverse mid-flight.
            var mover=players.First();var expected=AssignedSide(kb,mover);
            mover.PosRot=new(expected<0?84:116,5,100,0);
            Check(AssignedSide(kb,mover)==expected,"landing plans remain stable while actors fly between islands");
            foreach(var helper in helpers){Event(m,helper,46061);Finish(m,helper);}
            Check(markers.ActiveAOEs(0,players[0]).Length==0,"tower resolution clears green guides");
            // A second wave uses new positions and has no stale required tower IDs.
            foreach(var helper in helpers)Start(m,helper,46061,9.7f);
            Check(players.Where(p=>AssignedTower(kb,p)!=0).All(p=>helpers.Any(h=>h.InstanceID==AssignedTower(kb,p))),"next wave rebuilds current tower assignments");
        }
        foreach(var destroyed in new[]{-1,1})
        {
            using var m=Module();Director(m,2);
            var players=Enumerable.Range(0,8).Select(i=>Player(m,(ulong)(200+i),new(i<4?84:116,i%2==0?90:110),slot:i)).ToArray();
            var west=Create(m,Actor(10,(uint)OID.Helper,new(84,111)));var east=Create(m,Actor(11,(uint)OID.Helper,new(116,89)));
            Start(m,west,46061,9.7f);Start(m,east,46061,9.7f);
            var kb=Component<SimpleKnockbacks>(m,"ExplosionKnockUp");
            _=AssignedSide(kb,players[0]);
            Start(m,m.PrimaryActor,46063,7.7f,(destroyed<0?-90f:90f).Degrees());
            Check(players.All(p=>AssignedSide(kb,p)==-destroyed),"destruction overrides minimum occupancy on the doomed island");
            Check(players.Where(p=>Math.Sign(p.Position.X-100)==destroyed).All(p=>AssignedTower(kb,p)!=0),"every doomed-side player must take a meteor");
            foreach(var p in players.Where(p=>AssignedTower(kb,p)!=0))
                CheckMarkers(Component<GenericAOEs>(m,"LaunchPositionHints"),kb,p,m.Raid.FindSlot(p.InstanceID),25);
            m.WorldState.Execute(new ActorState.OpDestroy(west.InstanceID));m.WorldState.Execute(new ActorState.OpDestroy(east.InstanceID));
            Check(kb.Casters.Count==0 && Component<GenericAOEs>(m,"LaunchPositionHints").ActiveAOEs(0,players[0]).Length==0,"destroyed helpers leave no stale launches or green guides");
        }
        using(var m=Module())
        {
            Director(m,2);
            var players=Enumerable.Range(0,8).Select(i=>Player(m,(ulong)(200+i),new(84,i%2==0?90:110),slot:i)).ToArray();
            for(var i=4;i<8;++i)m.WorldState.Execute(new ActorState.OpDead(players[i].InstanceID,true));
            var north=Create(m,Actor(10,(uint)OID.Helper,new(84,89)));var south=Create(m,Actor(11,(uint)OID.Helper,new(84,111)));
            Start(m,north,46061,9.7f);Start(m,south,46061,9.7f);
            var kb=Component<SimpleKnockbacks>(m,"ExplosionKnockUp");
            Check(players.Take(4).Count(p=>AssignedSide(kb,p)<0)==2 && players.Take(4).Count(p=>AssignedSide(kb,p)>0)==2,"dead players are excluded while four living players can still balance islands");
            m.WorldState.Execute(new ActorState.OpDead(players[3].InstanceID,true));
            var hints=new BossComponent.GlobalHints();kb.AddGlobalHints(players[0],hints);
            Check(hints.Count!=0,"fewer than four living players reports impossible minimum coverage");
        }
        for(var client=0;client<8;++client)
        {
            using var m=Module();Director(m,2);var players=new Actor[8];
            for(var slot=0;slot<8;++slot)
            {
                var id=(client+slot)%8;
                players[id]=Player(m,(ulong)(200+id),new(id<4?84:116,id%2==0?90:110),slot:slot);
            }
            foreach(var (id,pos) in new (ulong,WPos)[]{(10,new(84,89)),(11,new(84,111)),(12,new(116,89)),(13,new(116,111))})
                Start(m,Create(m,Actor(id,(uint)OID.Helper,pos)),46061,9.7f);
            var kb=Component<SimpleKnockbacks>(m,"ExplosionKnockUp");
            Check(AssignedTower(kb,players[0])==10 && AssignedTower(kb,players[1])==11 && AssignedTower(kb,players[4])==12 && AssignedTower(kb,players[5])==13,"tower identities are consistent across every client party order");
        }
        using(var m=Module())
        {
            Director(m,2);
            var players=Enumerable.Range(0,8).Select(i=>Player(m,(ulong)(200+i),new(i<4?84:116,100),slot:i)).ToArray();
            var north=Create(m,Actor(10,(uint)OID.Helper,new(84,89)));
            var south=Create(m,Actor(11,(uint)OID.Helper,new(84,111)));
            Start(m,north,46061,9.7f);Start(m,south,46061,9.7f);
            var kb=Component<SimpleKnockbacks>(m,"ExplosionKnockUp");
            Check(AssignedTower(kb,players[0])==10,"cancellation fixture has an initial north assignment");
            m.WorldState.Execute(new ActorState.OpCastInfo(north.InstanceID,null));
            Check(players.All(p=>AssignedTower(kb,p)!=10) && AssignedTower(kb,players[0])==11,"cancelled tower is removed from the cached plan");
            m.WorldState.Execute(new ActorState.OpCastInfo(south.InstanceID,null));
            Check(Component<GenericAOEs>(m,"LaunchPositionHints").ActiveAOEs(0,players[0]).Length==0,"cancelled wave clears green launch guides");
            m.StateMachine.Reset();m.StateMachine.Start(Now);
            Check(Component<GenericAOEs>(m,"LaunchPositionHints").ActiveAOEs(0,players[0]).Length==0 && Component<SimpleKnockbacks>(m,"ExplosionKnockUp").Casters.Count==0,"fresh pull has no cached launch assignments or guides");
        }
        foreach(var homeSide in new[]{-1,1})
        foreach(var homeZ in new[]{89f,111f})
        using(var m=Module())
        {
            Director(m,2);
            var players=Enumerable.Range(0,8).Select(i=>Player(m,(ulong)(200+i),new(i<4?84:116,100),slot:i)).ToArray();
            var slot=homeSide<0?3:7;
            var p=players[slot];p.PosRot=new(homeSide<0?74.5f:125.5f,0,homeZ<100?119.5f:80.5f,0);
            var west=Create(m,Actor(10,(uint)OID.Helper,new(84,homeSide<0?homeZ:200-homeZ)));
            var east=Create(m,Actor(11,(uint)OID.Helper,new(116,homeSide>0?homeZ:200-homeZ)));
            var home=homeSide<0?west:east;
            Start(m,west,46061,9.7f);Start(m,east,46061,9.7f);
            var kb=Component<SimpleKnockbacks>(m,"ExplosionKnockUp");
            Check(AssignedTower(kb,p)==0,"preparation also covers players who were not the initial tower soaker");
            var h=Hints(kb,p,slot);h.PathfindMapBounds=m.Bounds;h.PathfindMapCenter=m.Center;h.Normalize();
            Check(h.ForbiddenZones.Any(z=>Math.Abs((z.activation-Now).TotalSeconds-4.7)<0.01),"two-tower preparation arrives before the later facing cue");
            var decision=BossMod.Pathfinding.NavigationDecision.Build(new(),Now,h,p);
            Check(decision.TimeToGoal<3.7f,"far-corner preparation fits the deadline with the native reserve");
            var elapsed=0f;
            for(var step=0;step<32 && (p.Position-home.Position).LengthSq()>144;++step)
            {
                decision=BossMod.Pathfinding.NavigationDecision.Build(new(),Now.AddSeconds(elapsed),h,p);
                Check(decision.Destination is {} d && Math.Sign(d.X-100)==homeSide && m.Arena.InBounds(d),"native preparation stays on the current platform");
                var next=decision.Destination!.Value;
                elapsed+=(next-p.Position).Length()/6f;p.PosRot=new(next.X,0,next.Z,0);
            }
            Check((p.Position-home.Position).LengthSq()<=144 && elapsed<3.7f,"native preparation reaches the meteor area before its deadline");
            m.WorldState.Frame=new(Now.AddSeconds(5.14),0,0,0,0,1);
            Start(m,m.PrimaryActor,46063,7.7f,(90f*homeSide).Degrees());
            h=Hints(kb,p,slot);h.PathfindMapBounds=m.Bounds;h.PathfindMapCenter=m.Center;h.Normalize();
            decision=BossMod.Pathfinding.NavigationDecision.Build(new(),m.WorldState.CurrentTime,h,p);
            Check(decision.TimeToGoal<3.56f,"prepared evacuation fits the recorded remaining launch window");
            elapsed=0f;
            for(var step=0;step<32 && Forbidden(h,p.Position);++step)
            {
                decision=BossMod.Pathfinding.NavigationDecision.Build(new(),m.WorldState.CurrentTime.AddSeconds(elapsed),h,p);
                Check(decision.Destination is {} d && Math.Sign(d.X-100)==homeSide && m.Arena.InBounds(d),"evacuation approaches the launch point on the current platform");
                var next=decision.Destination!.Value;
                elapsed+=(next-p.Position).Length()/6f;p.PosRot=new(next.X,0,next.Z,0);
            }
            Check(!Forbidden(h,p.Position) && elapsed<3.56f,"facing cue refines preparation into a reachable cross-island launch");
        }
    }
    private static void CheckMarkers(GenericAOEs markers, SimpleKnockbacks kb, Actor player, int slot, float distance)
    {
        var aoes=markers.ActiveAOEs(slot,player).ToArray();
        Check(aoes.Length==1 && aoes.All(a=>!a.Risky && a.Color==Colors.SafeFromAOE),"launch guide recommends one green wedge and never a hazard");
        var text=new BossComponent.TextHints();markers.AddHints(slot,player,text);
        Check(text.Count!=0,"green launch wedge has an actionable stand-here hint");
        Check(Hints(markers,player,slot).ForbiddenZones.Count==0,"green display does not become inverted AI danger");
        var side=AssignedSide(kb,player);
        foreach(var aoe in aoes)
        {
            var source=kb.Casters.Single(k=>k.ActorID==aoe.ActorID);
            Check(aoe.Activation==source.Activation,"green guide keeps its source deadline");
            var sector=(AOEShapeDonutSector)aoe.Shape;
            foreach(var radius in new[]{sector.InnerRadius,2.5f,sector.OuterRadius})
            for(var i=0;i<9;++i)
            {
                var point=aoe.Origin+radius*(aoe.Rotation+((i-4)*1.25f).Degrees()).ToDirection();
                var landing=GenericKnockback.AwayFromSource(point,source.Origin,distance);
                Check(landing.X >= (side<0?74.75f:106.75f) && landing.X <= (side<0?93.25f:125.25f) && landing.Z>=80.75f && landing.Z<=119.25f,"green wedge boundary lands inside the assigned island margin");
                Check(!Forbidden(Hints(kb,player,slot),point),"green guide agrees with the actual knockback AI constraints");
            }
        }
    }
}
