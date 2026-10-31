namespace BossMod.Stormblood.Alliance.A13Rofocale;

class Maverick(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Maverick1, new AOEShapeRect(60f, 8f));
class CryOfVictory(BossModule module) : Components.SimpleAOEGroups(module, [(uint)AID.CryOfVictory1, (uint)AID.CryOfVictory2], new AOEShapeCone(60f, 90f.Degrees()));
class Trample(BossModule module) : Components.CastHint(module, (uint)AID.Trample1, "Avoid the figure-eight charge; stand inside a loop or outside its path!");

[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "The Combat Reborn Team, Xel", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 281, NameID = 6941)]
public class A13Rofocale(WorldState ws, Actor primary) : BossModule(ws, primary, new(106f, -191f), new ArenaBoundsCircle(29.5f));
