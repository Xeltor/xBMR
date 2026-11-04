namespace BossMod.Stormblood.Alliance.A11Mateus;

class AquaSpheres(BossModule module) : BossComponent(module)
{
    private static readonly WDir[] StagingOffsets = [new(-16.8f, 9.3f), new(0f, -26f), new(20.2f, 12.8f)];
    private DateTime _prepareUntil;
    private BitMask _seenSectors;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster != Module.PrimaryActor)
            return;
        if (spell.IsSpell(AID.Unbind))
        {
            _seenSectors = default;
            _prepareUntil = Module.CastFinishAt(spell, 8d);
        }
        else if (spell.IsSpell(AID.Rebind))
            Clear();
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.IsSpell(AID.Unbind) && !spell.EventHappened)
            Clear();
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster == Module.PrimaryActor && spell.IsSpell(AID.Unbind))
            _prepareUntil = WorldState.FutureTime(8d);
    }

    public override void OnActorTargetable(Actor actor)
    {
        if (actor.OID == (uint)OID.AquaSphere && IsAvailable(actor))
            _seenSectors.Set(A11Mateus.AllianceSector(Arena.Center, actor.Position));
    }

    public override void OnActorCreated(Actor actor) => OnActorTargetable(actor);

    public override void OnActorDeath(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            Clear();
    }

    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (AssignedSphere(slot, actor) != null)
            hints.Add("Destroy the marked Aqua Sphere before the Skating Fiend reaches it!");
        else if (StagingPosition(slot) != null)
            hints.Add("Move toward your alliance's Aqua Sphere.", false);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (AssignedSphere(pcSlot, pc) is { } sphere)
            Arena.Actor(sphere, Colors.Object);
        else if (StagingPosition(pcSlot) is { } stage)
            Arena.ZoneCircleOutline(stage, 4f, Colors.Object);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (StagingPosition(slot) is { } stage
            && !hints.PotentialTargets.Any(enemy => enemy.Actor.OID == (uint)OID.BlizzardIII && A11Mateus.CanEngagePhaseAdd(enemy, Arena)))
            hints.GoalZones.Add(AIHints.GoalProximity(stage, 30f, 4f));
    }

    internal WPos? StagingPosition(int slot)
    {
        var group = Raid.AllianceGroupForSlot(slot);
        return group >= 0 && !_seenSectors[group] && _prepareUntil > WorldState.CurrentTime ? Arena.Center + StagingOffsets[group] : null;
    }

    private Actor? AssignedSphere(int slot, Actor actor)
    {
        if (Module.PrimaryActor.IsDeadOrDestroyed)
            return null;
        var group = Raid.AllianceGroupForSlot(slot);
        var spheres = Module.Enemies((uint)OID.AquaSphere).Where(IsAvailable);
        if (StagingPosition(slot) != null)
            spheres = spheres.Where(sphere => A11Mateus.AllianceSector(Arena.Center, sphere.Position) == group);
        return spheres
            .MinBy(sphere => (group >= 0 && A11Mateus.AllianceSector(Arena.Center, sphere.Position) != group,
                (sphere.Position - actor.Position).LengthSq(), sphere.InstanceID));
    }

    private bool IsAvailable(Actor sphere) => sphere.IsTargetable && !sphere.IsDeadOrDestroyed && Arena.InBounds(sphere.Position);

    private void Clear()
    {
        _seenSectors = default;
        _prepareUntil = default;
    }
}
