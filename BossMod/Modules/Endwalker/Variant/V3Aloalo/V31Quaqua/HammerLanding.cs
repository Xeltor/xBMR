namespace BossMod.Endwalker.VariantCriterion.V3Aloalo.V31Quaqua;

sealed class HammerLanding(BossModule module) : Components.GenericKnockback(module)
{
    private readonly Actor?[] beacons = new Actor?[3];
    private readonly HashSet<uint> hits = [];
    private ActorCastInfo? firstCast;
    private WPos firstOrigin;
    private DateTime activation;
    private int next;
    private Knockback[] visible = [];

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        if (activation == default || next >= 3 || next > 0 && (beacons[next] == null || beacons[next]!.IsDestroyed))
            return [];
        var origin = next == 0 ? firstOrigin : beacons[next]!.Position;
        // Walking between impacts is required; don't pretend all three pushes
        // happen sequentially without intervening player movement.
        visible = [new(origin, 20f, activation, actorID: Module.PrimaryActor.InstanceID)];
        return visible;
    }

    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        // Numbered beacon appearances in the new recording: 0x272/273/274.
        if (actor.OID == (uint)OID.HammerBeacon && status.ID == 2056u && status.Extra is >= 626 and <= 628)
            beacons[status.Extra - 626] = actor;
        base.OnStatusGain(actor, ref status);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster != Module.PrimaryActor)
            return;
        if (spell.Action.ID == (uint)AID.ArcaneArmamentsHammers)
        {
            Array.Clear(beacons);
            activation = default;
            firstCast = null;
        }
        else if (spell.Action.ID == (uint)AID.HammerLanding && firstCast != spell)
        {
            firstCast = spell;
            firstOrigin = spell.LocXZ;
            activation = Module.CastFinishAt(spell);
            next = 0;
            hits.Clear();
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster != Module.PrimaryActor || activation == default
            || spell.Action.ID is not ((uint)AID.HammerLanding or (uint)AID.HammerLandingRepeat) || !hits.Add(spell.GlobalSequence))
            return;
        ++NumCasts;
        ++next;
        activation = next < 3 ? WorldState.FutureTime(2.1d) : default;
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell == firstCast && !spell.EventHappened)
            activation = default;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var sources = ActiveKnockbacks(slot, actor);
        if (sources.Length != 0 && !IsImmune(slot, sources[0].Activation))
            hints.AddForbiddenZone(new SDKnockbackInCircleAwayFromOrigin(Module.Center, sources[0].Origin, 20f, Module.Bounds.Radius), sources[0].Activation);
    }
}
