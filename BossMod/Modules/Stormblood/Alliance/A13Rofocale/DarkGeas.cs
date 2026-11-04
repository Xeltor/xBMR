namespace BossMod.Stormblood.Alliance.A13Rofocale;

class DarkGeas(BossModule module) : Components.CastHint(module, (uint)AID.DarkGeas, "Interact with the Dark Circles!")
{
    // Interaction pauses movement in the native client. Allow time to leave before an imminent hit.
    private const double InteractionMargin = Pathfinding.NavigationDecision.ActivationTimeCushion + 1.1d;
    private const double CircleArrivalGrace = 0.75d;
    private const float ApproachRadius = 1f;
    private const float PreparationRadius = 2f;

    // LVD_BOSS_03 EventObject placements for the twelve Dark Circle rows.
    private static readonly WPos[] CirclePlacements =
    [
        new(83.1475f, -193.2679f), new(83.0572f, -182.9463f), new(89.5468f, -172.2780f),
        new(96.8378f, -165.1901f), new(105.9906f, -164.6963f), new(117.2166f, -169.0602f),
        new(125.2045f, -175.8115f), new(130.9696f, -185.2656f), new(130.9999f, -195.3280f),
        new(124.7983f, -204.2808f), new(114.9943f, -210.5770f), new(103.2217f, -213.1518f),
    ];

    private DateTime _awaitingCirclesUntil;
    private bool Preparing => Active || WorldState.CurrentTime < _awaitingCirclesUntil;

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Module.PrimaryActor.IsDeadOrDestroyed)
            return;

        if (Circles().Any())
            hints.Add(Hint);
        else if (Preparing)
            hints.Add("Move toward a Dark Circle and interact when it appears.");
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (Module.PrimaryActor.IsDeadOrDestroyed)
            return;

        var circles = Circles().ToArray();
        if (circles.Length > 0)
        {
            foreach (var circle in circles)
                Arena.ZoneCircleOutline(circle.Position, ApproachRadius, Colors.Safe);
        }
        else if (Preparing)
        {
            foreach (var position in CirclePlacements)
                Arena.ZoneCircleOutline(position, PreparationRadius, Colors.AOE);
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (Module.PrimaryActor.IsDeadOrDestroyed || actor.IsDeadOrDestroyed || hints.InteractWithTarget != null)
            return;

        var circles = Circles().ToArray();
        if (circles.Length == 0)
        {
            if (Preparing)
                hints.GoalZones.Add(position => CirclePlacements.Any(center => position.InCircle(center, PreparationRadius)) ? 8f : 0f);
            return;
        }

        var start = actor.Position;
        var clearance = actor.HitboxRadius;
        var readyAt = WorldState.FutureTime(InteractionMargin);
        var hazards = hints.ForbiddenZones.ToArray();
        if (!Unsafe(start, readyAt))
        {
            var nearest = ApproachRadius * ApproachRadius;
            foreach (var circle in circles)
            {
                var distance = (circle.Position - start).LengthSq();
                if (distance <= nearest)
                {
                    hints.InteractWithTarget = circle;
                    nearest = distance;
                }
            }
            if (hints.InteractWithTarget is { } target)
            {
                hints.PriorityInteraction = target;
                return;
            }
        }

        // Score every interaction approach equally. Native navigation chooses the
        // reachable one and continues to enforce terrain and timed hazards.
        var positions = circles.Select(circle => circle.Position).ToArray();
        hints.GoalZones.Add(position =>
        {
            foreach (var center in positions)
            {
                if (position.InCircle(center, ApproachRadius))
                    return Unsafe(position, readyAt.AddSeconds((position - start).Length() / 6f)) ? 0f : 100f;
            }
            return 0f;
        });

        bool Unsafe(WPos position, DateTime before)
        {
            foreach (var zone in hazards)
                if ((zone.activation <= before || zone.activation == DateTime.MaxValue) && zone.shapeDistance.Distance(position) <= clearance)
                    return true;
            return false;
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster != Module.PrimaryActor || spell.Action.ID != WatchedAction)
            return;

        _awaitingCirclesUntil = default;
        base.OnCastStarted(caster, spell);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster != Module.PrimaryActor || spell.Action.ID != WatchedAction || caster.IsDeadOrDestroyed)
            return;

        base.OnEventCast(caster, spell);
        // The effect precedes targetable objects. Keep the staging goal through that handoff.
        _awaitingCirclesUntil = Circles().Any() ? default : WorldState.FutureTime(CircleArrivalGrace);
    }

    public override void OnActorCreated(Actor actor) => OnActorTargetable(actor);

    public override void OnActorTargetable(Actor actor)
    {
        if (_awaitingCirclesUntil != default && EligibleCircle(actor))
            _awaitingCirclesUntil = default;
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor == Module.PrimaryActor)
        {
            Casters.Clear();
            _awaitingCirclesUntil = default;
        }
    }

    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);

    private IEnumerable<Actor> Circles() => WorldState.Actors.Where(EligibleCircle);

    private bool EligibleCircle(Actor actor) => actor.Type == ActorType.EventObj && actor.IsTargetable
        && !actor.IsDeadOrDestroyed && IsDarkCircle(actor.OID) && Arena.InBounds(actor.Position);

    private static bool IsDarkCircle(uint oid) => oid is (uint)OID.DarkCircle3 or >= (uint)OID.DarkCircle4 and <= (uint)OID.DarkCircle7;
}
