namespace BossMod.RealmReborn.Alliance.A1Atomos;

public enum OID : uint { Boss = 0x963, Valefor = 0x964, GreaterDemon = 0x965, Dira = 0x966 }
class VoidFireII(BossModule module) : Components.SimpleAOEs(module, 1829, 5);
class TailDrive(BossModule module) : Components.SimpleAOEs(module, 1792, new AOEShapeCone(35, 45.Degrees()));
class RottenBreath(BossModule module) : Components.CastHint(module, 1790, "Frontal breath - move behind the add");

class Platforms(BossModule module) : BossComponent(module)
{
    private readonly HashSet<int> _completedLanes = [];
    public override void OnActorDeath(Actor actor)
    {
        if (actor.OID == (uint)OID.Boss)
            _completedLanes.Add((int)Math.Clamp(MathF.Round((actor.Position.Z - 244.8f) / 35.2f), 0, 2));
    }
    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.IsDead)
            OnActorDeath(actor);
    }
    public static WPos Platform(int group) => new(213.68f, 244.8f + 35.2f * group);
    public static float LaneZ(int group) => 244.8f + 35.2f * group;
    public static bool HoldsPlatform(PartyState raid, int slot)
    {
        var party = raid.WithSlot(includeDead: true, excludeAlliance: true, excludeNPCs: true);
        // Do not assign a sparse/unknown roster to arbitrary jobs.
        return party.Length == PartyState.MaxPartySize && party.Where(p => !p.Item2.IsDeadOrDestroyed).OrderBy(p => p.Item2.Role switch
            { Role.Healer => 0, Role.Ranged => 1, Role.Melee => 2, _ => 3 }).ThenBy(p => p.Item2.InstanceID).Take(4).Any(p => p.Item1 == slot);
    }

    public bool Required(int group)
    {
        // Layout switch1 A/B/C feeds switch2 A/B/C in the next lane (A -> B -> C -> A).
        var linkedGroup = (group + 1) % 3;
        if (_completedLanes.Contains(linkedGroup))
            return false;
        var linkedZ = LaneZ(linkedGroup);
        var linked = Module.Enemies((uint)OID.Boss).FirstOrDefault(a => Math.Abs(a.Position.Z - linkedZ) < 5);
        return linked == null || !linked.IsDeadOrDestroyed;
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        var group = LabyrinthAlliance.Group(Raid, slot);
        if (group < 0)
        {
            hints.Add("Alliance unknown - follow your alliance's lane; four players must hold its rear platform", false);
            return;
        }
        var correctLane = Math.Abs(actor.Position.Z - LaneZ(group)) < 13;
        hints.Add($"Alliance {(char)('A' + group)} lane", !correctLane);
        if (Required(group))
            hints.Add(HoldsPlatform(Raid, slot) ? "Hold rear platform for the linked alliance" : "Four players hold rear platform; kill your lane's adds and Atomos",
                HoldsPlatform(Raid, slot) && !actor.Position.InCircle(Platform(group), 3.2f));
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var group = LabyrinthAlliance.Group(Raid, slot);
        if (group < 0)
        {
            foreach (var enemy in hints.PotentialTargets)
                if (enemy.Actor.OID is (uint)OID.Boss or (uint)OID.Valefor or (uint)OID.GreaterDemon or (uint)OID.Dira)
                    enemy.Priority = AIHints.Enemy.PriorityUndesirable;
            return;
        }
        var laneZ = LaneZ(group);
        // Never direct a crossing through the empty space between the three lanes.
        if (Math.Abs(actor.Position.Z - laneZ) < 13 && Required(group) && HoldsPlatform(Raid, slot))
            hints.AddForbiddenZone(new SDInvertedCircle(Platform(group), 3.2f));
        foreach (var enemy in hints.PotentialTargets)
        {
            if (enemy.Actor.OID is not ((uint)OID.Boss or (uint)OID.Valefor or (uint)OID.GreaterDemon or (uint)OID.Dira))
                continue;
            if (Math.Abs(enemy.Actor.Position.Z - laneZ) > 13)
                enemy.Priority = AIHints.Enemy.PriorityUndesirable;
            else if (enemy.Actor.InCombat && enemy.Priority >= 0)
                enemy.Priority = enemy.Actor.OID == (uint)OID.Boss ? 1 : 2;
        }
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        var group = LabyrinthAlliance.Group(Raid, pcSlot);
        if (group >= 0 && Required(group))
            Arena.AddCircleUnfilled(Platform(group), 3.75f, Colors.Safe);
    }
}

class A1AtomosStates : StateMachineBuilder
{
    public A1AtomosStates(BossModule module) : base(module)
    {
        TrivialPhase().ActivateOnEnter<Platforms>().ActivateOnEnter<VoidFireII>().ActivateOnEnter<TailDrive>().ActivateOnEnter<RottenBreath>()
            .Raw.Update = () => Module.Enemies((uint)OID.Boss).All(a => a.IsDeadOrDestroyed);
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.CFC, GroupID = 92, NameID = 1872, ConfigType = typeof(LabyrinthConfig))]
public class A1Atomos(WorldState ws, Actor primary) : BossModule(ws, primary, arena.Center, arena)
{
    // Only one module instance is loaded per OID: the first Atomos seen may belong to any alliance.
    // All three floors must be available to pathfinding, with the gaps remaining impassable.
    private static readonly ArenaBoundsCustom arena = new([new Rectangle(new(232.4f, 244.8f), 38.4f, 12.8f),
        new Rectangle(new(232.4f, 280), 38.4f, 12.8f), new Rectangle(new(232.4f, 315.2f), 38.4f, 12.8f)]);

    protected override bool CheckPull() => Enemies((uint)OID.Boss).Any(a => a.InCombat && !a.IsDeadOrDestroyed);
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        foreach (var actor in WorldState.Actors)
            if (actor.OID is (uint)OID.Boss or (uint)OID.Valefor or (uint)OID.GreaterDemon or (uint)OID.Dira)
                Arena.Actor(actor);
    }
}
