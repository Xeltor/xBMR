namespace BossMod.Endwalker.VariantCriterion.V3Aloalo.V31Quaqua;

sealed class ArcaneArmaments(BossModule module) : Components.GenericAOEs(module)
{
    private sealed class Prediction(Actor orb, AOEInstance aoe, ActorCastInfo? cast = null)
    {
        public readonly Actor Orb = orb;
        public AOEInstance AOE = aoe;
        public readonly ActorCastInfo? Cast = cast;
    }

    private readonly List<Prediction> predictions = [];
    private readonly List<AOEInstance> visible = [];
    private static readonly AOEShapeCircle axe = new(14f);
    private static readonly AOEShapeDonut quoit = new(5f, 18f);
    private DateTime activation;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        visible.Clear();
        foreach (var prediction in predictions)
            if (!prediction.Orb.IsDestroyed)
            {
                var aoe = prediction.AOE;
                // Keep the full tether preview, but don't turn an ~13-second
                // forecast into immediate avoidance/dash-safety pressure.
                // Five seconds allows travel plus the native one-second reserve;
                // the actual impact deadline and hazard dimensions stay intact.
                aoe.Risky = aoe.Activation <= WorldState.FutureTime(5d);
                visible.Add(aoe);
            }
        return CollectionsMarshal.AsSpan(visible);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.ArcaneArmaments)
        {
            predictions.Clear();
            // Three recorded waves: orb hits ~4.8s after the 8s visual ends.
            activation = Module.CastFinishAt(spell, 4.8d);
            // Tethers can arrive BEFORE the visual's CastStarted callback.
            foreach (var orb in Module.Enemies((uint)OID.AethericCharge))
                if (orb.Tether.Target == caster.InstanceID)
                    PredictTether(orb, orb.Tether.ID);
        }
        else if (caster.OID == (uint)OID.AethericCharge && spell.Action.ID is (uint)AID.RavagingAxe or (uint)AID.RingingQuoits)
        {
            var shape = spell.Action.ID == (uint)AID.RavagingAxe ? (AOEShape)axe : quoit;
            Upsert(caster, new(shape, spell.LocXZ, spell.Rotation, Module.CastFinishAt(spell), actorID: caster.InstanceID), spell);
        }
    }

    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (source.OID == (uint)OID.AethericCharge && tether.Target == Module.PrimaryActor.InstanceID)
            PredictTether(source, tether.ID);
    }

    // The visual clears its tethers BEFORE the spheres cast. An untether is
    // not resolution; retain each prediction until its own hit/cancellation.
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened)
            return;
        if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.ArcaneArmaments)
        {
            predictions.RemoveAll(p => p.Cast == null);
            activation = default;
        }
        else
            predictions.RemoveAll(p => p.Orb == caster && p.Cast == spell);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.RavagingAxe or (uint)AID.RingingQuoits && predictions.RemoveAll(p => p.Orb == caster) != 0)
        {
            ++NumCasts;
            if (predictions.Count == 0)
                activation = default;
        }
    }

    public override void OnActorDestroyed(Actor actor) => predictions.RemoveAll(p => p.Orb == actor);

    private void PredictTether(Actor orb, uint id)
    {
        AOEShape? shape = id switch
        {
            (uint)TetherID.Axe => axe,
            (uint)TetherID.Quoit => quoit,
            _ => null
        };
        if (activation != default && shape != null && !orb.IsDestroyed && !predictions.Any(p => p.Orb == orb && p.Cast != null))
            Upsert(orb, new(shape, orb.Position, default, activation, actorID: orb.InstanceID));
    }

    private void Upsert(Actor orb, AOEInstance aoe, ActorCastInfo? cast = null)
    {
        predictions.RemoveAll(p => p.Orb.InstanceID == orb.InstanceID);
        predictions.Add(new(orb, aoe, cast));
    }
}
