namespace BossMod.Shadowbringers.Dungeon.D02DohnMheg.D022Griaule;

public enum OID : uint
{
    Boss = 0x98E, // R=3.18-12.72
    PaintedRoot = 0xF08, // R=1.48
    PaintedSapling = 0xEFB // R=0.9
}

public enum AID : uint
{
    AutoAttack = 870, // Boss->player, no cast, single-target

    Rake = 10355, // Boss->player, no cast, single-target
    Swinge = 8906, // Boss->self, 4.0s cast, range 50+R 60-degree cone
    Fodder = 8897, // Boss->self, 5.0s cast, single-target
    Tiiimbeeer = 8915, // Boss->self, 6.0s cast, range 50 circle
    FeedingTime = 8899, // PaintedSapling->player/Boss, no cast, single-target
    CoilingIvy = 8901 // Boss->self, 3.0s cast, single-target
}

sealed class FeedingTime(BossModule module) : Components.CastHint(module, (uint)AID.Fodder, "Sapling tethers incoming")
{
    private const uint FeedingTether = 84;
    private const uint AlternateFeedingTether = 17;
    private const double FeedingDelay = 10.9;
    private const float LaneHalfWidth = 0.5f;

    private readonly Dictionary<Actor, (DateTime Activation, ulong Target)> _saplings = [];

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.PaintedSapling)
            _saplings.TryAdd(actor, (WorldState.FutureTime(FeedingDelay), 0));
    }

    public override void OnActorDestroyed(Actor actor) => _saplings.Remove(actor);

    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (tether.ID is FeedingTether or AlternateFeedingTether && _saplings.TryGetValue(source, out var lane))
            _saplings[source] = (lane.Activation, tether.Target);
    }

    public override void OnUntethered(Actor source, in ActorTetherInfo tether)
    {
        if (_saplings.TryGetValue(source, out var lane) && lane.Target == tether.Target)
            _saplings[source] = (lane.Activation, 0);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.FeedingTime)
            _saplings.Remove(caster);
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (AssignedLane(actor) is { } lane)
            hints.Add("Intercept the sapling tether!", lane.Shape.Contains(actor.Position));
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (actor.FindStatus(CoilingIvy.Fetters) == null && AssignedLane(actor) is { } lane)
            hints.AddForbiddenZone(lane.Shape, lane.Activation, source: lane.Source.InstanceID);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        var assigned = AssignedLane(pc)?.Source;
        foreach (var (source, (_, targetID)) in _saplings)
            if (targetID != 0 && WorldState.Actors.Find(targetID) is { } target)
                Arena.AddLine(source.Position, target.Position, source == assigned ? Colors.Safe : default);
    }

    private (Actor Source, SDInvertedRect Shape, DateTime Activation)? AssignedLane(Actor actor)
    {
        if (_saplings.Count == 0 || actor.IsDead || actor.Type != ActorType.Player)
            return null;

        var boss = Module.PrimaryActor;
        var freeLanes = new List<(Actor Source, SDInvertedRect Shape, DateTime Activation)>();
        foreach (var (source, (activation, target)) in _saplings)
        {
            if (source.IsDead || (target != boss.InstanceID && target != actor.InstanceID))
                continue;

            var offset = source.Position - boss.Position;
            var distance = offset.Length();
            var innerRadius = boss.HitboxRadius + 0.1f;
            if (distance <= innerRadius)
                continue;

            var from = boss.Position + (innerRadius / distance) * offset;
            var lane = (source, new SDInvertedRect(from, source.Position, LaneHalfWidth), activation);
            if (target == actor.InstanceID)
                return lane;
            freeLanes.Add(lane);
        }

        if (freeLanes.Count == 0)
            return null;

        // Actual tether recipients reserve their lanes, including Trust companions.
        // Assign the remaining lanes in party order without waiting for companions to move.
        foreach (var member in Raid.WithoutSlot(excludeAlliance: true, excludeNPCs: true))
        {
            if (member.Type != ActorType.Player || _saplings.Values.Any(lane => lane.Target == member.InstanceID))
                continue;

            var nearest = freeLanes.MinBy(lane => (-lane.Shape.Distance(member.Position), lane.Source.InstanceID));
            if (member == actor)
                return nearest;

            freeLanes.Remove(nearest);
            if (freeLanes.Count == 0)
                break;
        }

        return null;
    }
}

sealed class CoilingIvy(BossModule module) : Components.CastHint(module, (uint)AID.CoilingIvy, "Roots incoming")
{
    public const uint Fetters = 1153;

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (actor.FindStatus(Fetters) != null)
            hints.Add("Destroy your Painted Root to break free!");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var fettered = actor.FindStatus(Fetters) != null;
        if (fettered)
        {
            // This status blocks movement but still allows attacks on the root.
            hints.AddSpecialMode(AIHints.SpecialMode.NoMovement, WorldState.CurrentTime);
            if (hints.ImminentSpecialMode.mode == AIHints.SpecialMode.NoMovement)
                hints.NoMovementAllowsManualInput = true;
            hints.ForbidDashes = true;
        }

        AIHints.Enemy? ownRoot = null;
        var nearestDistanceSq = 1f; // the root spawns at the bound player's feet
        foreach (var enemy in hints.PotentialTargets)
        {
            var root = enemy.Actor;
            if (root.OID != (uint)OID.PaintedRoot || !root.IsTargetable || root.IsDeadOrDestroyed || root.PendingDead)
                continue;

            enemy.Priority = 1;
            var distanceSq = (root.Position - actor.Position).LengthSq();
            if (fettered && distanceSq < nearestDistanceSq)
            {
                ownRoot = enemy;
                nearestDistanceSq = distanceSq;
            }
        }

        if (ownRoot != null)
        {
            ownRoot.Priority = 2;
            ownRoot.ShouldBeTargeted = true;
        }
    }
}

sealed class Tiiimbeeer(BossModule module) : Components.RaidwideCast(module, (uint)AID.Tiiimbeeer);
sealed class Swinge(BossModule module) : Components.GenericAOEs(module)
{
    private AOEInstance[] _aoe = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _aoe;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.Swinge)
        {
            _aoe = [new(new AOEShapeCone(50f + caster.HitboxRadius, 30.Degrees()), caster.Position, spell.Rotation, Module.CastFinishAt(spell))];
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.Swinge)
        {
            _aoe = [];
        }
    }
}

sealed class D022GriauleStates : StateMachineBuilder
{
    public D022GriauleStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<FeedingTime>()
            .ActivateOnEnter<Tiiimbeeer>()
            .ActivateOnEnter<CoilingIvy>()
            .ActivateOnEnter<Swinge>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Verified, Contributors = "The Combat Reborn Team (Malediktus)", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 649, NameID = 8143)]
public sealed class D022Griaule : BossModule
{
    public D022Griaule(WorldState ws, Actor primary) : this(ws, primary, BuildArena()) { }

    private D022Griaule(WorldState ws, Actor primary, (WPos center, ArenaBoundsCustom arena) a) : base(ws, primary, a.center, a.arena) { }

    private static (WPos center, ArenaBoundsCustom arena) BuildArena()
    {
        var arena = new ArenaBoundsCustom([new Polygon(new(7.156f, -339.132f), 24.5f * CosPI.Pi32th, 32)], [new Rectangle(new(7f, -363.5f), 20f, 1.1f), new Rectangle(new(7f, -315), 20f, 0.75f)]);
        return (arena.Center, arena);
    }

    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.PaintedRoot));
    }
}
