namespace BossMod.Stormblood.Alliance.A12Hashmal;

class ExtremeEdge(BossModule module) : Components.SimpleAOEs(module, (uint)AID.ExtremeEdge1, new AOEShapeRect(66f, 18f));
class TowerAppear(BossModule module) : Components.SimpleAOEs(module, (uint)AID.ControlTower2, 6f);
class CommandTowerAppear(BossModule module) : Components.SimpleAOEs(module, (uint)AID.CommandTower4, 10f);
class EarthHammer(BossModule module) : Components.CastHint(module, (uint)AID.EarthHammer, "Move away from the hammer shadows!");

// The direction-bearing object appears ~9.1-9.4s before the uncast helper hit.
class Towerfall(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<AOEInstance> _aoes = [];
    private static readonly AOEShapeRect Shape = new(40.5f, 5f);
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_aoes);

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.FallingTower && !_aoes.Any(a => a.ActorID == actor.InstanceID))
            _aoes.Add(new(Shape, actor.Position, actor.Rotation, WorldState.FutureTime(9d), actorID: actor.InstanceID));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.Towerfall1)
            _aoes.RemoveAll(a => a.Origin.AlmostEqual(caster.Position, 1f) && a.Rotation.AlmostEqual(caster.Rotation, 0.05f));
    }

    public override void OnActorDestroyed(Actor actor) => _aoes.RemoveAll(a => a.ActorID == actor.InstanceID);
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "The Combat Reborn Team, Xel", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 281, NameID = 6922)]
public class A12Hashmal(WorldState ws, Actor primary) : BossModule(ws, primary, new(-320f, -46f), new ArenaBoundsCircle(29.5f));
