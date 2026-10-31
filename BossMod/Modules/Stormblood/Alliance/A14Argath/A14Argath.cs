namespace BossMod.Stormblood.Alliance.A14Argath;

class Heartless(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Heartless, new AOEShapeRect(61f, 5.5f));
class Soulfix(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Soulfix, 12f);
class ColdbloodProximity(BossModule module) : Components.CastHint(module, (uint)AID.Coldblood2, "Move far away from Argath!", true);
class ColdbloodDonut(BossModule module) : Components.CastHint(module, (uint)AID.Coldblood, "Get inside the small safe circle at Argath!", true);
class GnawingDread(BossModule module) : Components.TemporaryMisdirection(module, (uint)AID.GnawingDread);

// Conservative inner platform; the outer rim still needs lethal-edge calibration.
// See docs/encounters/rabanastre.md before changing this margin.
[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "The Combat Reborn Team, Xel", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 281, NameID = 6925)]
public class A14Argath(WorldState ws, Actor primary) : BossModule(ws, primary, new(106f, -386f), new ArenaBoundsSquare(24f));
