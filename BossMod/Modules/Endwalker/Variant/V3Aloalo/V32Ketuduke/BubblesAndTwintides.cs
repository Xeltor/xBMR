namespace BossMod.Endwalker.VariantCriterion.V3Aloalo.V32Ketuduke;

sealed class StrewnBubbles(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<(Actor Marker, AOEInstance AOE)> lanes = [];
    private readonly List<AOEInstance> visible = [];
    private static readonly AOEShapeRect shape = new(20f, 5f);
    private DateTime activation;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        visible.Clear();
        foreach (var lane in lanes)
            visible.Add(lane.AOE);
        return CollectionsMarshal.AsSpan(visible);
    }

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.BubbleStrewer && activation != default && !lanes.Any(l => l.Marker == actor))
            lanes.Add((actor, new(shape, actor.Position, actor.Rotation, activation, actorID: actor.InstanceID)));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.StrewnBubbles)
        {
            lanes.Clear();
            activation = WorldState.FutureTime(7d);
        }
        else if (spell.Action.ID == (uint)AID.SphereShatter)
        {
            // Each helper resolves one lane. The marker moves while its trail
            // remains at the original position; marker despawn is not resolution.
            var matches = lanes.FindAll(l => l.AOE.Origin.AlmostEqual(caster.Position, 1f));
            if (matches.Count == 1)
            {
                lanes.Remove(matches[0]);
                ++NumCasts;
            }
        }
    }
}

sealed class Twintides(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<(uint Action, AOEInstance AOE)> sequence = [];
    private readonly AOEInstance[] visible = new AOEInstance[1];
    private static readonly AOEShapeCircle outside = new(14f);
    private static readonly AOEShapeDonut inside = new(8f, 60f);
    private Actor? source;
    private ActorCastInfo? initialCast;
    private ActorCastInfo? currentCast;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        if (sequence.Count == 0)
            return [];
        visible[0] = sequence[0].AOE;
        return visible;
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        var id = spell.Action.ID;
        if (id is (uint)AID.RecedingTwintides or (uint)AID.EncroachingTwintides)
        {
            if (initialCast == spell)
                return;
            initialCast = spell;
            currentCast = spell;
            source = caster;
            sequence.Clear();
            var outFirst = id == (uint)AID.RecedingTwintides;
            sequence.Add((id, new(outFirst ? outside : inside, spell.LocXZ, default, Module.CastFinishAt(spell), actorID: caster.InstanceID)));
            sequence.Add((outFirst ? (uint)AID.FarTide : (uint)AID.NearTide,
                new(outFirst ? inside : outside, spell.LocXZ, default, Module.CastFinishAt(spell, 4.1d), actorID: caster.InstanceID)));
        }
        else if (caster == source && id is (uint)AID.FarTide or (uint)AID.NearTide && sequence.Count != 0 && sequence[0].Action == id)
        {
            currentCast = spell;
            var aoe = sequence[0].AOE;
            aoe.Origin = spell.LocXZ;
            aoe.Activation = Module.CastFinishAt(spell);
            sequence[0] = (id, aoe);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster != source || sequence.Count == 0 || sequence[0].Action != spell.Action.ID)
            return;
        sequence.RemoveAt(0);
        ++NumCasts;
        if (sequence.Count != 0)
        {
            var next = sequence[0].AOE;
            next.Activation = WorldState.FutureTime(4.1d);
            sequence[0] = (sequence[0].Action, next);
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == source)
            sequence.Clear();
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == source && spell == currentCast && !spell.EventHappened)
            sequence.Clear();
    }
}

sealed class BlowingBubbles(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<Actor> bubbles = [];
    private readonly List<AOEInstance> visible = [];
    // Reference avoidance policy, not a measured capture radius or trajectory.
    private static readonly AOEShapeCircle shape = new(5f);

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        visible.Clear();
        foreach (var bubble in bubbles)
            if (Live(bubble))
                visible.Add(new(shape, bubble.Position, actorID: bubble.InstanceID));
        return CollectionsMarshal.AsSpan(visible);
    }

    private bool Live(Actor actor) => !actor.IsDestroyed && WorldState.Actors.Find(actor.InstanceID) == actor;
    public override void Update() => bubbles.RemoveAll(b => !Live(b));
    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.AiryBubble && !bubbles.Contains(actor))
            bubbles.Add(actor);
    }
    public override void OnActorDestroyed(Actor actor) => bubbles.Remove(actor);
}
