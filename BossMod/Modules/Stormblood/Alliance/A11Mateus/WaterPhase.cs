namespace BossMod.Stormblood.Alliance.A11Mateus;

class Froth(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<Actor> _bubbles = [with(6)];
    private readonly List<AOEInstance> _visible = [with(6)];
    private readonly Dictionary<ulong, DateTime> _recoveryBy = [];
    internal IReadOnlyList<Actor> Bubbles => _bubbles;
    internal bool NeedsAirSource(Actor actor)
        => _bubbles.Count == 0 && !actor.IsDeadOrDestroyed && actor.FindStatus((uint)SID.Breathless) is { Extra: >= 5 };
    internal static float AirSourceApproachRadius(Actor actor, Actor toad)
        => toad.HitboxRadius + (actor.Role is Role.Tank or Role.Melee ? 3f : 24.5f);

    internal static float BubbleRadius(Actor bubble) => Math.Clamp(bubble.HitboxRadius, 0.7f, 1.4f);

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        if (actor.FindStatus((uint)SID.Breathless) is not { Extra: >= 5 })
            return [];

        _visible.Clear();
        foreach (var bubble in _bubbles)
            _visible.Add(new(new AOEShapeCircle(BubbleRadius(bubble)), bubble.Position, color: Colors.SafeFromAOE, risky: false, actorID: bubble.InstanceID));
        return CollectionsMarshal.AsSpan(_visible);
    }

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.Froth && !actor.IsDeadOrDestroyed && !_bubbles.Any(bubble => bubble.InstanceID == actor.InstanceID))
            _bubbles.Add(actor);
    }

    public override void OnActorDeath(Actor actor)
    {
        _bubbles.Remove(actor);
        _recoveryBy.Remove(actor.InstanceID);
    }

    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);

    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        if (status.ID != (uint)SID.Breathless)
            return;
        if (status.Extra < 5)
            _recoveryBy.Remove(actor.InstanceID);
        else
        {
            // Keep the conservative estimate anchored even if the same stack is refreshed.
            var estimate = EstimateRecovery(status.Extra);
            if (!_recoveryBy.TryGetValue(actor.InstanceID, out var previous) || estimate < previous)
                _recoveryBy[actor.InstanceID] = estimate;
        }
    }

    public override void OnStatusLose(Actor actor, ref ActorStatus status)
    {
        if (status.ID == (uint)SID.Breathless)
            _recoveryBy.Remove(actor.InstanceID);
    }

    private DateTime EstimateRecovery(ushort stacks) => WorldState.FutureTime(Math.Max(0, 10 - stacks));

    internal DateTime? RecoveryDeadline(Actor actor, DateTime snowpierceAt)
    {
        if (actor.IsDeadOrDestroyed || _bubbles.Count == 0 || actor.FindStatus((uint)SID.Breathless) is not { Extra: >= 5 } breathless)
            return null;

        // The inherited one-second-per-stack estimate has no full phase recording.
        var airDeadline = _recoveryBy.TryGetValue(actor.InstanceID, out var estimate) ? estimate : EstimateRecovery(breathless.Extra);
        if (snowpierceAt == default)
            return airDeadline;

        var now = WorldState.CurrentTime;
        var leaveBy = snowpierceAt.AddSeconds(-2d);
        if (RecoveryBubbles(actor, now).Any(bubble => now.AddSeconds(TravelTime(actor, bubble)) <= leaveBy))
            return airDeadline < leaveBy ? airDeadline : leaveBy;

        // Waiting is viable only if a bubble remains on the projected
        // route after the actor's own charge has finished.
        return RecoveryBubbles(actor, snowpierceAt, afterOwnCharge: true).Any(bubble => snowpierceAt.AddSeconds(TravelTime(actor, bubble)) < airDeadline)
            ? null : airDeadline;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (NeedsAirSource(actor))
        {
            var approaches = hints.PotentialTargets
                .Where(enemy => enemy.Actor.OID == (uint)OID.FlumeToad && A11Mateus.CanEngagePhaseAdd(enemy, Arena))
                .Select(enemy => (enemy.Actor.Position, Radius: AirSourceApproachRadius(actor, enemy.Actor))).ToArray();
            if (approaches.Length != 0)
                hints.GoalZones.Add(position => approaches.Any(approach => position.InCircle(approach.Position, approach.Radius)) ? 12f : 0f);
            return;
        }

        var snowpierceAt = Module.FindComponent<Snowpierce>()?.FirstActivation(actor) ?? default;
        if (RecoveryDeadline(actor, snowpierceAt) is not { } deadline)
            return;

        var bubbles = RecoveryBubbles(actor, WorldState.CurrentTime);
        // If all are threatened, keep looking for an immediate recovery.
        // The other baits can move or be cancelled before they resolve.
        if (bubbles.Count == 0)
            bubbles = _bubbles;
        var orbs = new ShapeDistance[bubbles.Count];
        var centers = new WPos[bubbles.Count];
        for (var i = 0; i < orbs.Length; ++i)
        {
            var bubble = bubbles[i];
            centers[i] = bubble.Position;
            orbs[i] = new SDInvertedCircle(centers[i], BubbleRadius(bubble));
        }
        hints.ActionsToExecute.Push(ActionID.MakeSpell(ClassShared.AID.Sprint), actor, ActionQueue.Priority.High);
        hints.AddForbiddenZone(new SDIntersection(orbs), deadline);
        hints.GoalZones.Add(position =>
        {
            foreach (var center in centers)
                if (position.InCircle(center, 0.5f))
                    return 8f;
            return 0f;
        });
    }

    private IReadOnlyList<Actor> RecoveryBubbles(Actor actor, DateTime startAt, bool afterOwnCharge = false)
    {
        if (Module.FindComponent<Snowpierce>() is not { } snowpierce)
            return _bubbles;
        var others = snowpierce.ActiveBaitsNotOn(actor);
        var own = afterOwnCharge ? snowpierce.ActiveBaitsOn(actor) : [];
        if (others.Count == 0 && own.Count == 0)
            return _bubbles;

        return _bubbles.Where(bubble =>
        {
            var arrival = startAt.AddSeconds(TravelTime(actor, bubble));
            return !Snowpierce.ThreatenedBefore(bubble, others, arrival)
                && !own.Any(bait => bait.Activation <= arrival && Snowpierce.StartsInsideBubble(bait.Source.Position, bubble));
        }).ToArray();
    }

    private static double TravelTime(Actor actor, Actor bubble)
        => (actor.Position - bubble.Position).Length() / 6d + Pathfinding.NavigationDecision.ActivationTimeCushion;

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (actor.FindStatus((uint)SID.Breathless) is not { Extra: >= 5 })
            return;

        var snowpierceAt = Module.FindComponent<Snowpierce>()?.FirstActivation(actor) ?? default;
        if (_bubbles.Count == 0)
            hints.Add("Defeat a Flume Toad to create an air bubble!");
        else if (RecoveryDeadline(actor, snowpierceAt) != null)
            hints.Add(snowpierceAt != default ? "Get air, then move out to aim Snowpierce!" : "Stand in an air bubble!");
        else
            hints.Add("Aim Snowpierce away, then get air!");
    }
}

class Snowpierce(BossModule module) : Components.BaitAwayChargeCast(module, (uint)AID.Snowpierce, HalfWidth)
{
    private const float HalfWidth = 1.5f;
    private Froth? Air => Module.FindComponent<Froth>();

    internal DateTime FirstActivation(Actor actor)
    {
        var first = DateTime.MaxValue;
        foreach (var bait in ActiveBaitsOn(actor))
            if (bait.Activation < first)
                first = bait.Activation;
        return first == DateTime.MaxValue ? default : first;
    }

    internal static bool ThreatenedBefore(Actor bubble, List<Bait> baits, DateTime arrival)
    {
        var radius = Froth.BubbleRadius(bubble);
        foreach (var bait in baits)
        {
            if (bait.Activation > arrival)
                continue;
            var origin = bait.Source.Position;
            var direction = bait.Target.Position - origin;
            if (new SDRect(origin, direction.ToAngle(), direction.Length(), 0f, HalfWidth).Distance(bubble.Position) <= radius)
                return true;
        }
        return false;
    }

    internal static bool StartsInsideBubble(WPos origin, Actor bubble) => origin.InCircle(bubble.Position, Froth.BubbleRadius(bubble));

    public override void OnActorDestroyed(Actor actor) => CurrentBaits.RemoveAll(bait => bait.Source == actor || bait.Target == actor);

    protected override void AddTargetSpecificHints(Actor actor, ref Bait bait, AIHints hints)
    {
        if (!RecoveringAir(actor))
            base.AddTargetSpecificHints(actor, ref bait, hints);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        var air = Air;
        if (air == null || air.Bubbles.Count == 0 || RecoveringAir(actor))
            return;

        foreach (var bait in ActiveBaitsOn(actor))
        {
            var origin = bait.Source.Position;
            var zones = new List<ShapeDistance>(air.Bubbles.Count);
            foreach (var bubble in air.Bubbles)
            {
                var offset = bubble.Position - origin;
                var distance = offset.Length();
                var radius = Froth.BubbleRadius(bubble);
                // A line starting inside a bubble cannot protect that bubble by changing aim.
                if (StartsInsideBubble(origin, bubble))
                    continue;

                var clearance = HalfWidth + radius;
                var halfAngle = Angle.Asin(Math.Min(1f, clearance / distance));
                // The line ends at the target. A target short of the bubble cannot pop it.
                zones.Add(new SDDonutSector(origin, Math.Max(0f, distance - clearance), 100f, Angle.FromDirection(offset), halfAngle));
            }
            if (zones.Count > 0)
                hints.AddForbiddenZone(new SDUnion([.. zones]), bait.Activation);
        }
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        base.DrawArenaForeground(pcSlot, pc);
        if (IsBaitTarget(pc) && Air is { } air)
            foreach (var bubble in air.Bubbles)
                Arena.ZoneCircleOutline(bubble.Position, Froth.BubbleRadius(bubble));
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        base.AddHints(slot, actor, hints);
        if (IsBaitTarget(actor) && !RecoveringAir(actor))
            hints.Add("Aim Snowpierce away from air bubbles!", false);
    }

    private bool RecoveringAir(Actor actor)
    {
        var activation = FirstActivation(actor);
        return activation != default && Air?.RecoveryDeadline(actor, activation) != null;
    }
}
