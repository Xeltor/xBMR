namespace BossMod.Stormblood.Alliance.A12Hashmal;

class ExtremeEdge(BossModule module) : Components.SimpleAOEGroups(module, [(uint)AID.ExtremeEdge1, (uint)AID.ExtremeEdge2], new AOEShapeRect(66f, 18f))
{
    private const ushort Departure = 7737;
    private const float StagingHalfWidth = 2.5f;
    private DateTime _prepareUntil;
    private bool Preparing => _prepareUntil > WorldState.CurrentTime;

    public override void OnActorPlayActionTimelineEvent(Actor actor, ushort id)
    {
        if (actor == Module.PrimaryActor && !actor.IsTargetable && id == Departure && Casters.Count == 0)
            _prepareUntil = WorldState.FutureTime(3d);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        base.OnCastStarted(caster, spell);
        if (spell.Action.ID is (uint)AID.ExtremeEdge1 or (uint)AID.ExtremeEdge2)
            _prepareUntil = default;
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (spell.Action.ID is (uint)AID.ExtremeEdge1 or (uint)AID.ExtremeEdge2)
            Casters.RemoveAll(lane => lane.ActorID == caster.InstanceID);
    }

    public override void OnActorTargetable(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            _prepareUntil = default;
    }

    public override void OnActorDestroyed(Actor actor)
    {
        OnActorTargetable(actor);
        Casters.RemoveAll(lane => lane.ActorID == actor.InstanceID);
    }

    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Preparing)
            hints.Add("Extreme Edge incoming. Move toward the middle when safe, then dodge the burning lane.");
        else if (Casters.Count != 0)
            hints.Add("Extreme Edge: leave the marked lane.");
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (Preparing)
            Arena.ZoneRectOutline(Arena.Center, 90f.Degrees(), Arena.Bounds.Radius, Arena.Bounds.Radius, StagingHalfWidth, Colors.Object);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (Preparing)
        {
            var middle = Arena.Center.Z;
            var radius = Arena.Bounds.Radius;
            var falloff = radius - StagingHalfWidth;
            // When a tower covers the staging band, prefer a safe position
            // nearer the middle so either later dodge stays within reach.
            hints.GoalZones.Add(position => 8f * Math.Clamp((radius - Math.Abs(position.Z - middle)) / falloff, 0f, 1f));
        }
    }
}
