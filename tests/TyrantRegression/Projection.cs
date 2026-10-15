using System.Text.Json;
using System.Numerics;
using BossMod;
using BossMod.Components;
using BossMod.Dawntrail.Raid.M11NTheTyrant;

static partial class Checks
{
    // Deliberately labelled projection: this has no original frames, action effects or omitted operations.
    // It checks recorded callback order against the actual native dispatcher, not full replay or gameplay.
    private static void Projection(string path)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(path));
        var events=doc.RootElement.GetProperty("events").EnumerateObject().SelectMany(p=>p.Value.EnumerateArray()).OrderBy(e=>e.GetProperty("seq").GetInt32()).ToArray();
        var world=new WorldState(1,"archive-projection") { Frame=new(Now,0,0,0,0,1) };
        M11NTheTyrant? module=null;
        ulong[] players=[53,112,62,82,63,91,124,125];
        long ticks=0;int processed=0;int checks=0;
        foreach(var e in events)
        {
            var next=e.GetProperty("ticks").GetInt64();
            var delta=(float)((next-ticks)/1e7);ticks=next;
            foreach(var actor in world.Actors.Actors.Values)
                if(actor.CastInfo is {} ci)ci.ElapsedTime+=delta;
            world.Frame=new(Now.AddTicks(ticks),0,0,0,0,1);
            var id=(ulong)e.GetProperty("actor").GetInt32();
            var actorNow=world.Actors.Find(id);
            var aid=e.GetProperty("action").GetUInt32();
            var target=(ulong)e.GetProperty("target").GetInt32();
            var pars=e.GetProperty("params").EnumerateArray().Select(v=>v.GetUInt32()).ToArray();
            var pos=e.GetProperty("position");
            WPos point=pos.ValueKind==JsonValueKind.Object?new(pos.GetProperty("x").GetSingle(),pos.GetProperty("z").GetSingle()):default;
            Angle rotation=pos.ValueKind==JsonValueKind.Object?new(pos.GetProperty("facing").GetSingle()):default;
            var kind=e.GetProperty("kind").GetString();
            switch(kind)
            {
                case "create":
                    var created=Actor(id,e.GetProperty("oid").GetUInt32(),point,pars.Length>1?(Class)pars[1]:Class.None,e.GetProperty("hitbox_radius").GetSingle());
                    created.Type=(ActorType)pars[0];created.PosRot=new(point.X,pos.GetProperty("y").GetSingle(),point.Z,rotation.Rad);
                    // Uses native creation semantics. The helper method requires a module, so spell out initial creates here.
                    world.Execute(new ActorState.OpCreate(id,created.OID,0,0,"",0,created.Type,created.Class,100,created.PosRot,created.HitboxRadius,created.HPMP,true,false,0,0,0));
                    var slot=Array.IndexOf(players,id);if(slot>=0)world.Execute(new PartyState.OpModify(slot,new(0,id,false)));
                    if(created.OID==(uint)OID.Boss && module==null){module=new(world,world.Actors.Find(id)!);module.StateMachine.Start(world.CurrentTime);}
                    break;
                case "move" when actorNow!=null:
                    actorNow.PosRot=new(point.X,pos.GetProperty("y").GetSingle(),point.Z,rotation.Rad);break;
                case "cast_start" when actorNow!=null:
                    if(id==87)actorNow.PosRot=new(point.X,pos.GetProperty("y").GetSingle(),point.Z,rotation.Rad);
                    var ci=Cast(aid,point,e.GetProperty("cast_total").GetSingle(),rotation,target);
                    ci.ElapsedTime=e.GetProperty("cast_elapsed").GetSingle();world.Execute(new ActorState.OpCastInfo(id,ci));break;
                case "cast_end" when actorNow!=null:world.Execute(new ActorState.OpCastInfo(id,null));break;
                case "action" when actorNow!=null:
                    world.Execute(new ActorState.OpCastEvent(id,new(new(ActionType.Spell,aid),target,0,0,default,pars.Length>0?pars[0]:0,0,rotation)));break;
                case "timeline" when actorNow!=null:world.Execute(new ActorState.OpPlayActionTimelineEvent(id,(ushort)pars[0]));break;
                case "tether" when actorNow!=null:world.Execute(new ActorState.OpTether(id,new(pars[0],target)));break;
                case "icon" when actorNow!=null:world.Execute(new ActorState.OpIcon(id,pars[0],target));break;
                case "map_effect":world.Execute(new WorldState.OpMapEffect((byte)pars[0],pars[1]));break;
                case "director":world.Execute(new WorldState.OpDirectorUpdate(pars[4],pars[5],pars[0],pars[1],pars[2],pars[3]));break;
                case "destroy" when actorNow!=null:world.Execute(new ActorState.OpDestroy(id));break;
            }
            processed++;
            if(module==null)continue;
            foreach(var c in module.Components)c.Update();
            var player=world.Actors.Find(53);
            if(player==null)continue;
            var seq=e.GetProperty("seq").GetInt32();
            var weapons=Component<GenericAOEs>(module,"WeaponAOEs");
            if(seq==36984){Check(weapons.ActiveAOEs(0,player).Length==3,"recorded first Evolved tether chain predicts all three jumps");checks++;}
            if(seq==48204){Check(weapons.ActiveAOEs(0,player).Length==3,"recorded alternate Evolved order predicts all three jumps");checks++;}
            if(seq==91347){Check(weapons.ActiveAOEs(0,player).ToArray().Any(a=>a.Shape is AOEShapeCross),"last Apex sword remains handled during overlapping gust windups");checks++;}
            if(seq==123719){Check(Hints(Component<GenericAOEs>(module,"Tyrannhilation"),player).ForbiddenZones.Count>0,"recorded double blast publishes shelter constraints from initial windup");checks++;}
            if(seq==133439){Check(!module.Arena.InBounds(new(100,100)),"recorded director split removes gap");checks++;}
            if(seq==137807){Check(Component<GenericAOEs>(module,"MajesticMeteorain").ActiveAOEs(0,player).Length==4,"recorded portal spawn predicts four lanes before helper casts");checks++;}
            if(seq==147029){Check(Component<GenericTowers>(module,"ExplosionTower").Towers.Count==4,"recorded four launch towers coexist with separate identities");checks++;}
            if(seq==151873){Check(Component<GenericTowers>(module,"ExplosionTower").Towers.Count==0,"recorded staggered launch hits retire all four towers");checks++;}
            if(seq==168634){Check(Hints(Component<SimpleKnockbacks>(module,"ExplosionKnockUp"),player).ForbiddenZones.Count>0,"recorded Avalanche facing affects pending launch landing constraints");checks++;}
            if(seq==173790){Check(!module.Arena.InBounds(new(84,100)) && module.Arena.InBounds(new(116,100)),"recorded west destruction retains only east floor");checks++;}
            if(seq==179847){Check(module.Arena.InBounds(new(100,100)),"recorded repair restores connected floor");checks++;}
            if(seq==186076 || seq==189923){Check(Component<GenericTowers>(module,"HeartbreakKick").Towers.Count==0,"recorded fifth Heartbreak pulse retires that sequence");checks++;}
        }
        Check(checks==13,"all expected recorded callback checkpoints were reached");
        Console.WriteLine($"{processed} selected projected events, {checks} recorded checkpoints passed; omitted native state remains unavailable.");
        module?.Dispose();
    }
}
