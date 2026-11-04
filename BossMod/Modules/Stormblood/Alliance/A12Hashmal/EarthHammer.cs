namespace BossMod.Stormblood.Alliance.A12Hashmal;

class EarthHammer(BossModule module) : Components.GenericAOEs(module, (uint)AID.Hammerfall, "Move away from the hammer shadows!")
{
    // Inner high-damage region used for routing. The actual damage falls off with distance.
    private static readonly AOEShapeCircle InnerDanger = new(18f);
    private const double SameWaveWindow = 0.25d;
    private readonly record struct Hammer(AOEInstance AOE, int Stage = 0, ulong CasterID = 0);
    private readonly List<Hammer> _shadows = [];
    private readonly List<AOEInstance> _visible = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _visible.Clear();
        if (_shadows.Count == 0)
            return [];

        var firstWaveEnd = _shadows[0].AOE.Activation.AddSeconds(SameWaveWindow);
        foreach (var (aoe, _, _) in _shadows)
            _visible.Add(aoe with { Color = aoe.Activation <= firstWaveEnd ? Colors.Danger : Colors.AOE });
        return CollectionsMarshal.AsSpan(_visible);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (_shadows.Count == 0)
            return;

        var origins = _shadows.Select(shadow => shadow.AOE.Origin).ToArray();
        hints.GoalZones.Add(p => Math.Min(origins.Min(origin => (p - origin).Length()), 35f));
        var affected = Raid.WithSlot().Mask();
        var waveEnd = DateTime.MinValue;
        foreach (var (shadow, _, _) in _shadows)
        {
            if (shadow.Activation > waveEnd)
            {
                hints.AddPredictedDamage(affected, shadow.Activation);
                waveEnd = shadow.Activation.AddSeconds(SameWaveWindow);
            }
        }
    }

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.HammerShadow && !_shadows.Any(s => s.AOE.ActorID == actor.InstanceID))
        {
            _shadows.Add(new(new(InnerDanger, actor.Position, activation: WorldState.FutureTime(12.5d), actorID: actor.InstanceID)));
            SortShadows();
        }
    }

    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (actor.OID != (uint)OID.HammerShadow)
            return;

        // The recorded animation stages precede impact by 7.28, 4.27 and 1.32 seconds.
        var (stage, lead) = state switch
        {
            0x00100020 => (1, 7.05d),
            0x00400080 => (2, 4.05d),
            0x01000200 => (3, 1.1d),
            _ => (0, 0d),
        };
        var index = _shadows.FindIndex(s => s.AOE.ActorID == actor.InstanceID && s.Stage < stage && s.CasterID == 0);
        if (index < 0)
            return;

        var shadow = _shadows[index];
        _shadows[index] = shadow with { AOE = shadow.AOE with { Activation = WorldState.FutureTime(lead) }, Stage = stage };
        SortShadows();
    }

    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.OID == (uint)OID.Boss)
            _shadows.Clear();
        else
            _shadows.RemoveAll(s => s.CasterID == actor.InstanceID || s.CasterID == 0 && s.AOE.ActorID == actor.InstanceID);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != WatchedAction)
            return;

        var origin = spell.LocXZ;
        var index = _shadows.FindIndex(s => s.CasterID == caster.InstanceID);
        if (index < 0)
            index = _shadows.FindIndex(s => s.CasterID == 0 && s.AOE.Origin.AlmostEqual(origin, 0.5f));
        var finish = Module.CastFinishAt(spell);
        if (index >= 0)
        {
            var shadow = _shadows[index];
            _shadows[index] = shadow with { AOE = shadow.AOE with { Activation = finish, Origin = origin }, CasterID = caster.InstanceID };
        }
        else
        {
            _shadows.Add(new(new(InnerDanger, origin, activation: finish, actorID: caster.InstanceID), CasterID: caster.InstanceID));
        }
        SortShadows();
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != WatchedAction)
            return;

        base.OnEventCast(caster, spell);
        _shadows.RemoveAll(s => s.CasterID == caster.InstanceID);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction)
            _shadows.RemoveAll(s => s.CasterID == caster.InstanceID);
    }

    public override void Update() => _shadows.RemoveAll(s => s.AOE.Activation.AddSeconds(1d) < WorldState.CurrentTime);

    private void SortShadows() => _shadows.Sort(static (a, b) => a.AOE.Activation.CompareTo(b.AOE.Activation));
}
