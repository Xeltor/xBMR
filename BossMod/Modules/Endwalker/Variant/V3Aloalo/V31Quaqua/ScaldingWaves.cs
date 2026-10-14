namespace BossMod.Endwalker.VariantCriterion.V3Aloalo.V31Quaqua;

sealed class ScaldingWaves(BossModule module) : Components.GenericAOEs(module)
{
    private sealed class Beam(Actor source, ActorCastInfo cast, AOEInstance aoe)
    {
        public readonly Actor Source = source;
        public readonly ActorCastInfo Cast = cast;
        public readonly AOEInstance AOE = aoe;
    }

    private sealed class Cascade(ActorCastInfo initial, WPos next, WDir advance, Angle rotation, DateTime activation, int remaining)
    {
        public readonly ActorCastInfo Initial = initial;
        public WPos Next = next;
        public readonly WDir Advance = advance;
        public readonly Angle Rotation = rotation;
        public DateTime Activation = activation;
        public int Remaining = remaining;
    }

    private readonly List<Beam> beams = [];
    private readonly List<Cascade> cascades = [];
    private readonly List<AOEInstance> visible = [];
    private readonly HashSet<uint> resolved = [];
    private static readonly AOEShapeRect firstShape = new(50f, 4f);
    private static readonly AOEShapeRect repeatShape = new(25f, 2f, 25f);

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        visible.Clear();
        foreach (var beam in beams)
            visible.Add(beam.AOE);
        foreach (var line in cascades)
            for (var i = 0; i < Math.Min(2, line.Remaining); ++i)
                visible.Add(new(repeatShape, line.Next + i * line.Advance, line.Rotation, line.Activation.AddSeconds(2.1d * i),
                    i == 0 ? Colors.Danger : Colors.AOE, risky: beams.Count == 0 && i == 0));
        return CollectionsMarshal.AsSpan(visible);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster.OID != (uint)OID.AnalaFamiliarLarge || spell.Action.ID != (uint)AID.ScaldingWavesFirst || beams.Any(b => b.Cast == spell))
            return;
        if (beams.Count == 0 && cascades.Count == 0)
            resolved.Clear();
        var finish = Module.CastFinishAt(spell);
        beams.Add(new(caster, spell, new(firstShape, spell.LocXZ, spell.Rotation, finish, actorID: caster.InstanceID)));
        var center = spell.LocXZ + 25f * spell.Rotation.ToDirection();
        var side = spell.Rotation.ToDirection().OrthoL();
        foreach (var sign in new[] { -1f, 1f })
        {
            var advance = 4f * sign * side;
            var inward = (Module.Center - center).Dot(advance) > 0;
            // Both recorded waves: inward branches cross the arena in seven
            // pulses; outward branches finish after three, including edge hits.
            cascades.Add(new(spell, center + 6f * sign * side, advance, spell.Rotation, finish.AddSeconds(2.8d), inward ? 7 : 3));
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.ScaldingWavesFirst)
        {
            if (beams.RemoveAll(b => b.Source == caster) != 0)
                ++NumCasts;
        }
        else if (spell.Action.ID == (uint)AID.ScaldingWavesRest && caster.OID == (uint)OID.AnalaFamiliar && !resolved.Contains(spell.GlobalSequence))
        {
            // Opposite inward branches can share a position with an already
            // advanced branch in the same frame. Only consume a due pulse.
            var line = cascades.Find(c => c.Activation <= WorldState.FutureTime(0.2d) && c.Next.AlmostEqual(caster.Position, 1f));
            if (line == null)
                return;
            resolved.Add(spell.GlobalSequence);
            ++NumCasts;
            if (--line.Remaining == 0)
                cascades.Remove(line);
            else
            {
                line.Next = caster.Position + line.Advance;
                line.Activation = WorldState.FutureTime(2.1d);
            }
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (!spell.EventHappened)
        {
            beams.RemoveAll(b => b.Cast == spell);
            cascades.RemoveAll(c => c.Initial == spell);
        }
    }
}
