namespace BossMod.RealmReborn.Alliance.A1AllaganBomb;

public enum OID : uint { Boss = 0x967, Balloon = 0x968, Napalm = 0x969, Vassago = 0x96A }
class Desolation(BossModule module) : Components.SimpleAOEs(module, 1774, new AOEShapeRect(59.6f, 3));
class GrimFatePositioning(BossModule module) : Components.FrontalPositioning(module, 8, [(uint)OID.Vassago]);

class AllianceTargets(BossModule module) : BossComponent(module)
{
    private static readonly WPos[] HoldingPoints = [new(-128.3f, -155), new(-110, -186.8f), new(-91.7f, -155)];
    private readonly Dictionary<ulong, int> _groups = [];
    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.Vassago)
        {
            for (var i = 0; i < HoldingPoints.Length; ++i)
                if (actor.Position.InCircle(HoldingPoints[i], 4))
                    _groups[actor.InstanceID] = i;
        }
    }
    public override void OnActorDestroyed(Actor actor) => _groups.Remove(actor.InstanceID);
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        var group = LabyrinthAlliance.Group(Raid, slot);
        hints.Add(group < 0 ? "Alliance unknown - follow your alliance's Vassago" : $"Alliance {(char)('A' + group)}: keep your Vassago separate; Napalm > Balloons > Vassago", false);
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var group = LabyrinthAlliance.Group(Raid, slot);
        var addsAlive = Module.Enemies((uint)OID.Vassago).Any(a => !a.IsDeadOrDestroyed);
        foreach (var enemy in hints.PotentialTargets)
        {
            var target = enemy.Actor;
            if (target.OID == (uint)OID.Boss && addsAlive)
                enemy.Priority = AIHints.Enemy.PriorityInvincible;
            else if (target.OID == (uint)OID.Vassago && (group < 0 || !_groups.TryGetValue(target.InstanceID, out var owner) || owner != group))
            {
                enemy.Priority = AIHints.Enemy.PriorityUndesirable;
                enemy.ShouldBeTanked = enemy.PreferProvoking = false;
            }
            else if (target.InCombat && enemy.Priority >= 0)
            {
                var holdingVassago = target.OID == (uint)OID.Vassago && actor.Role == Role.Tank && target.TargetID == actor.InstanceID;
                enemy.Priority = target.OID switch
                {
                    (uint)OID.Napalm => 4,
                    (uint)OID.Balloon => 3,
                    (uint)OID.Vassago => target.FindStatus(350u) != null ? AIHints.Enemy.PriorityInvincible : holdingVassago ? 5 : 2,
                    (uint)OID.Boss => 1,
                    _ => enemy.Priority
                };
                if (target.OID == (uint)OID.Vassago)
                {
                    var anchor = HoldingPoints[group];
                    var outward = (anchor - Module.Center).Normalized();
                    enemy.DesiredPosition = anchor;
                    enemy.DesiredRotation = outward.ToAngle();
                    if (holdingVassago)
                    {
                        enemy.ShouldBeTanked = true;
                        // The three recorded spawn positions form separate holding points. This is a
                        // positioning preference, subordinate to Desolation and all other hazards.
                        var tankPosition = anchor + outward * (target.HitboxRadius + actor.HitboxRadius + enemy.TankDistance);
                        hints.GoalZones.Add(AIHints.GoalProximity(tankPosition, 2, 10));
                    }
                }
            }
        }
    }
}
class A1AllaganBombStates : StateMachineBuilder
{
    public A1AllaganBombStates(BossModule module) : base(module)
    {
        TrivialPhase().ActivateOnEnter<Desolation>().ActivateOnEnter<AllianceTargets>().ActivateOnEnter<GrimFatePositioning>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.CFC, GroupID = 92, NameID = 1873, ConfigType = typeof(LabyrinthConfig))]
public class A1AllaganBomb(WorldState ws, Actor primary) : BossModule(ws, primary, new(-110, -165.6f), new ArenaBoundsCircle(33.2f))
{
    protected override bool CheckPull() => base.CheckPull() || Enemies((uint)OID.Vassago).Any(a => a.InCombat && !a.IsDeadOrDestroyed);
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.Vassago));
        Arena.Actors(Enemies((uint)OID.Balloon));
        Arena.Actors(Enemies((uint)OID.Napalm));
    }
}
