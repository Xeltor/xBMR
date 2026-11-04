namespace BossMod.Stormblood.Alliance.A12Hashmal;

class ToDust(BossModule module) : Components.RaidwideCast(module, (uint)AID.ToDust, "Destroy the Sand Spheres before To Dust!")
{
    private static readonly string[] Sectors = ["northwest", "south", "northeast"];
    // Staging positions from the retained clear's waymarks, rounded to the nearest half yalm.
    private static readonly WDir[] StagingOffsets = [new(-14.5f, -11f), new(0f, 20f), new(14f, -11.5f)];
    private const double StagingTime = 5.7d;
    private DateTime _prepareUntil;
    private bool _commandResolved;
    private BitMask _seenSectors;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster.OID == (uint)OID.SandSphere)
        {
            OnActorTargetable(caster);
            base.OnCastStarted(caster, spell);
        }
        else if (caster == Module.PrimaryActor && spell.IsSpell(AID.CommandTower3))
        {
            _prepareUntil = Module.CastFinishAt(spell, StagingTime);
            _commandResolved = false;
            _seenSectors = default;
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        base.OnCastFinished(caster, spell);
        if (caster == Module.PrimaryActor && spell.IsSpell(AID.CommandTower3) && !_commandResolved && !spell.EventHappened)
            _prepareUntil = default;
    }

    public override void OnActorCreated(Actor actor) => OnActorTargetable(actor);

    public override void OnActorTargetable(Actor actor)
    {
        if (actor.OID == (uint)OID.SandSphere && Available(actor))
            _seenSectors.Set(Sector(actor.Position));
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            Casters.Clear();
        else
            Casters.Remove(actor);

        if (actor == Module.PrimaryActor || actor.OID == (uint)OID.CommandTower2)
            _prepareUntil = default;
    }

    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (spell.Action.ID == WatchedAction)
            Casters.Remove(caster);
        else if (caster == Module.PrimaryActor && spell.IsSpell(AID.CommandTower3))
        {
            _commandResolved = true;
            if (_prepareUntil != default)
                _prepareUntil = WorldState.FutureTime(StagingTime);
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        var target = Assigned(slot, actor, Module.Enemies((uint)OID.SandSphere).Where(Available));
        if (target == null)
        {
            if (StagingSector(slot) is var group && group >= 0)
                hints.Add($"Prepare for the {Sectors[group]} Sand Sphere. Dodge tower attacks on the way.", false);
            else if (Module.Enemies((uint)OID.CommandTower2).Any(Available))
                hints.Add("Destroy the Command Tower before its gauge fills!");
            return;
        }
        var sector = Sectors[Sector(target.Position)];
        var text = $"Destroy the {sector} Sand Sphere";
        if (target.CastInfo is { EventHappened: false } cast && cast.IsSpell(AID.ToDust))
            text += $" before To Dust ({cast.NPCRemainingTime:f1}s)";
        hints.Add(text + "!");
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        var spheres = Module.Enemies((uint)OID.SandSphere).Where(Available).ToArray();
        var target = Assigned(pcSlot, pc, spheres);
        foreach (var sphere in spheres)
            Arena.Actor(sphere, sphere == target ? Colors.Object : Colors.Enemy);
        if (target == null && StagingSector(pcSlot) is var group && group >= 0)
            Arena.ZoneCircleOutline(AllianceStagingPosition(Arena.Center, group), 3f, Colors.Object);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);

        // The targetable spheres are required even before a player has engaged them.
        var targets = hints.PotentialTargets.Where(enemy => enemy.Actor.OID == (uint)OID.SandSphere
            && (enemy.Priority >= 0 || enemy.Priority == AIHints.Enemy.PriorityUndesirable) && Available(enemy.Actor)).ToArray();
        if (Assigned(slot, actor, targets.Select(enemy => enemy.Actor)) is not { } target)
        {
            if (StagingSector(slot) is var group && group >= 0)
                hints.GoalZones.Add(AIHints.GoalProximity(AllianceStagingPosition(Arena.Center, group), 28f, 4f));
            return;
        }

        foreach (var enemy in targets)
            enemy.Priority = enemy.Actor == target ? 4 : 3;

        var range = actor.Role is Role.Tank or Role.Melee ? 3f : 24.5f;
        hints.GoalZones.Add(AIHints.GoalSingleTarget(target.Position, target.HitboxRadius + range, 5f));
    }

    private Actor? Assigned(int slot, Actor actor, IEnumerable<Actor> candidates)
    {
        var group = Raid.AllianceGroupForSlot(slot);
        if (StagingSector(slot) >= 0)
            candidates = candidates.Where(sphere => Sector(sphere.Position) == group);
        return candidates.MinBy(sphere => (group >= 0 && Sector(sphere.Position) != group, (sphere.Position - actor.Position).LengthSq(), sphere.InstanceID));
    }

    internal static WPos AllianceStagingPosition(WPos center, int group) => center + StagingOffsets[group];
    internal static string AllianceSectorName(int group) => Sectors[group];

    private bool Available(Actor objective) => objective.IsTargetable && !objective.IsDeadOrDestroyed && Arena.InBounds(objective.Position);

    internal int StagingSector(int slot)
    {
        var group = Raid.AllianceGroupForSlot(slot);
        return group >= 0 && !_seenSectors[group] && WorldState.CurrentTime < _prepareUntil ? group : -1;
    }

    private int Sector(WPos position) => position.Z > Arena.Center.Z ? 1 : position.X < Arena.Center.X ? 0 : 2;
}
