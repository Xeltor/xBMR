namespace BossMod.Heavensward.Alliance.A33ProtoUltima;

// The guide and recording establish an outer-region hazard, not a full-arena pull.
// Its inner eligibility boundary is unverified; never invent a safe annulus.
class AetherialPool(BossModule module) : Components.CastHint(module, (uint)AID.AetherialPool, "Aetherial Pool: avoid the outer region!")
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction)
            Casters.RemoveAll(c => c.InstanceID == caster.InstanceID);
        base.OnCastStarted(caster, spell);
    }

    public override void OnActorDestroyed(Actor actor) => Casters.RemoveAll(c => c == actor);
}
class AetherochemicalFlare(BossModule module) : Components.RaidwideCast(module, (uint)AID.AetherochemicalFlare);

class CitadelBuster2(BossModule module) : Components.SimpleAOEs(module, (uint)AID.CitadelBuster2, new AOEShapeRect(65.5f, 5));
// Proximity damage reaches the whole platform; its sheet range is not a lethal exclusion radius.
class FlareStar(BossModule module) : Components.RaidwideCast(module, (uint)AID.FlareStar, "Proximity: move away from Flare Star; avoid the outer pool!")
{
    // Both observed proximity waves overlap Pool, which resolves ~1.65s earlier.
    // Maximizing distance would reward its unverified outer region. Keep the
    // damage deadline and manual instruction until combined safe geometry is known.

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var caster in Casters)
            Arena.ZoneCircleOutline(caster.Position, 1, Colors.Danger);
    }
}

class LightPillar(BossModule module) : Components.SimpleAOEs(module, (uint)AID.LightPillar2, 3);
class DiffractiveLaser(BossModule module) : Components.SimpleAOEs(module, (uint)AID.DiffractiveLaser2, new AOEShapeCone(20.6f, 30.Degrees()));
class Supernova(BossModule module) : Components.RaidwideCast(module, (uint)AID.Supernova1);
class SupernovaEnrage(BossModule module) : Components.CastHint(module, (uint)AID.SupernovaEnrage, "Enrage: destroy Aether Collectors, then defeat Proto Ultima!", true);
class ProtoUltimaAdds(BossModule module) : Components.AddsMulti(module, [(uint)OID.AllaganDreadnaught, (uint)OID.ProtoBit, (uint)OID.AetherCollector], 1);

class Rotoswipe(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Rotoswipe, new AOEShapeCone(11, 60.Degrees()));

class WreckingBall(BossModule module) : Components.SimpleAOEs(module, (uint)AID.WreckingBall, 8);

[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "The Combat Reborn Team", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 220, NameID = 3780)]
public class A33ProtoUltima(WorldState ws, Actor primary) : BossModule(ws, primary, new(-350, -50), new ArenaBoundsCircle(30))
{
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.AllaganDreadnaught));
        Arena.Actors(Enemies((uint)OID.AetherCollector));
    }
}
