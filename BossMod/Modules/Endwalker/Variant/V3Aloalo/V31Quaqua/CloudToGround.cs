namespace BossMod.Endwalker.VariantCriterion.V3Aloalo.V31Quaqua;

sealed class CloudToGround(BossModule module) : Components.GenericAOEs(module)
{
    private sealed class Line(Actor source)
    {
        public Actor Source = source;
        public ActorCastInfo? Cast;
        public WPos Next;
        public WDir Advance;
        public DateTime Activation;
        public int Remaining = 7;
        public readonly HashSet<uint> Resolved = [];
    }

    private readonly List<Line> lines = [];
    private readonly List<AOEInstance> visible = [];
    private static readonly AOEShapeCircle shape = new(6f);

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        visible.Clear();
        foreach (var line in lines)
        {
            if (line.Source.IsDestroyed)
                continue;
            for (var i = 0; i < Math.Min(3, line.Remaining); ++i)
            {
                // First-to-second hit differs from subsequent pulse cadence.
                var delay = i == 0 ? 0d : line.Remaining == 7 ? 3.1d + 2.1d * (i - 1) : 2.1d * i;
                visible.Add(new(shape, line.Next + i * line.Advance, default, line.Activation.AddSeconds(delay),
                    i == 0 ? Colors.Danger : Colors.AOE, actorID: line.Source.InstanceID));
            }
        }
        return CollectionsMarshal.AsSpan(visible);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster.OID != (uint)OID.DrakeFamiliar || spell.Action.ID is not ((uint)AID.CloudToGroundFirst or (uint)AID.CloudToGroundRest))
            return;
        if (lines.Any(l => l.Source == caster && l.Cast == spell))
            return;
        // At the end of the recorded wave, the first pair of helpers is reused
        // to finish the OTHER pair's lines. Match the expected pulse position,
        // not the caster alias; their own original lines have already ended.
        var line = spell.Action.ID == (uint)AID.CloudToGroundRest
            ? lines.Find(l => l.Next.AlmostEqual(spell.LocXZ, 1f)) : null;
        if (line == null)
        {
            lines.RemoveAll(l => l.Source.InstanceID == caster.InstanceID);
            lines.Add(line = new(caster));
            // With a late first observation, only the current cast is known.
            if (spell.Action.ID == (uint)AID.CloudToGroundRest)
                line.Remaining = 1;
        }
        line.Source = caster;
        line.Cast = spell;
        line.Next = spell.LocXZ;
        line.Advance = 6f * spell.Rotation.ToDirection();
        line.Activation = Module.CastFinishAt(spell);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is not ((uint)AID.CloudToGroundFirst or (uint)AID.CloudToGroundRest)
            || lines.Find(l => l.Source == caster && l.Cast == caster.CastInfo) is not { } line || !line.Resolved.Add(spell.GlobalSequence))
            return;
        ++NumCasts;
        --line.Remaining;
        if (line.Remaining == 0)
            lines.Remove(line);
        else
        {
            line.Next += line.Advance;
            line.Activation = WorldState.FutureTime(spell.Action.ID == (uint)AID.CloudToGroundFirst ? 3.1d : 2.1d);
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (!spell.EventHappened)
            lines.RemoveAll(l => l.Source == caster && l.Cast == spell);
    }

    public override void OnActorDestroyed(Actor actor) => lines.RemoveAll(l => l.Source == actor);
}
