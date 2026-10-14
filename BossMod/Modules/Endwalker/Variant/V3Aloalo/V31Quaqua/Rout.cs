namespace BossMod.Endwalker.VariantCriterion.V3Aloalo.V31Quaqua;

sealed class Rout(BossModule module) : Components.GenericAOEs(module)
{
    private sealed class Charge(Actor helper, ActorCastInfo cast, AOEInstance aoe)
    {
        public readonly Actor Helper = helper;
        public readonly ActorCastInfo Cast = cast;
        public AOEInstance AOE = aoe;
    }

    private readonly List<Charge> charges = [];
    private readonly List<AOEInstance> visible = [];
    private readonly HashSet<uint> resolved = [];
    private static readonly AOEShapeRect shape = new(45f, 8f);
    private DateTime firstActivation;
    private int previews;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        visible.Clear();
        foreach (var charge in charges)
        {
            if (!charge.Helper.IsDestroyed)
            {
                var aoe = charge.AOE;
                // Resolved lanes become usable between dashes. The navigator
                // cannot represent that future retirement from an activation
                // deadline alone; guide the next dash, preview the rest.
                aoe.Risky = visible.Count == 0;
                aoe.Color = aoe.Risky ? Colors.Danger : Colors.AOE;
                visible.Add(aoe);
            }
        }
        return CollectionsMarshal.AsSpan(visible);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.Rout)
        {
            charges.Clear();
            resolved.Clear();
            previews = 0;
            firstActivation = Module.CastFinishAt(spell);
            // Bootstrap already-present helper previews if activation is late.
            foreach (var helper in Module.Enemies((uint)OID.Helper).Where(h => h.CastInfo?.Action.ID == (uint)AID.RoutPreview)
                .OrderByDescending(h => h.CastInfo!.ElapsedTime))
                AddPreview(helper, helper.CastInfo!);
        }
        else if (spell.Action.ID == (uint)AID.RoutPreview && firstActivation != default)
            AddPreview(caster, spell);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened)
            return;
        if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.Rout)
        {
            charges.Clear();
            firstActivation = default;
        }
        else
            charges.RemoveAll(c => c.Helper == caster && c.Cast == spell);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster != Module.PrimaryActor || spell.Action.ID is not ((uint)AID.Rout or (uint)AID.RoutRepeat) || firstActivation == default
            || !resolved.Add(spell.GlobalSequence))
            return;
        ++NumCasts;
        if (charges.Count != 0)
            charges.RemoveAt(0);
        // Correct subsequent predictions from the actual damaging dash, not
        // the earlier helper preview's finish. Order comes from preview casts.
        for (var i = 0; i < charges.Count; ++i)
            charges[i].AOE.Activation = WorldState.FutureTime(1.6d * (i + 1));
        if (resolved.Count == 4)
            firstActivation = default;
    }

    public override void OnActorDestroyed(Actor actor) => charges.RemoveAll(c => c.Helper == actor);

    private void AddPreview(Actor helper, ActorCastInfo spell)
    {
        if (charges.Any(c => c.Helper == helper && c.Cast == spell) || previews >= 4)
            return;
        charges.Add(new(helper, spell, new(shape, spell.LocXZ, spell.Rotation, firstActivation.AddSeconds(1.6d * previews), actorID: helper.InstanceID)));
        ++previews;
    }
}
