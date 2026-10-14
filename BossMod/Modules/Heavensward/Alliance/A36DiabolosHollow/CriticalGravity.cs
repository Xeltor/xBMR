namespace BossMod.Heavensward.Alliance.A36DiabolosHollow;

// Contact resolves these moving orbs; there is no cast or evidenced fixed timer.
// Do not send the whole alliance toward a single orb or invent a tank assignment.
class CriticalGravity(BossModule module) : Components.GenericAOEs(module, (uint)AID.CriticalGravity)
{
    private readonly Dictionary<ulong, Actor> _orbs = [];

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.Shadowsphere)
            _orbs[actor.InstanceID] = actor;
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (_orbs.GetValueOrDefault(actor.InstanceID) == actor)
            _orbs.Remove(actor.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction && _orbs.GetValueOrDefault(caster.InstanceID) == caster)
        {
            ++NumCasts;
            _orbs.Remove(caster.InstanceID);
        }
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        List<AOEInstance> aoes = [];
        foreach (var orb in _orbs.Values)
            // Conventional source-hitbox addition, not a calibrated contact trigger.
            // The guide requires deliberate interception, so this is an informational
            // footprint rather than a party-wide avoidance rule that prevents soaking.
            aoes.Add(new(new AOEShapeCircle(8 + orb.HitboxRadius), orb.Position, risky: false, actorID: orb.InstanceID));
        return aoes.ToArray();
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        // All four recorded effects reference Knockback1:10y away from source.
        // Contact timing/eligibility remain unknown, so this adds no arrival rule.
        if (_orbs.Count > 0)
            hints.Add("Critical Gravity: if intercepting, check the 10y knockback landing and keep the blast away from the party!");
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_orbs.Values.Any(orb => actor.Position.InCircle(orb.Position, 8 + orb.HitboxRadius)))
            hints.Add("Orb contact explodes and knocks you 10y away: check your landing!", false);
    }

    // Interception eligibility and contact timing are not established by the data.
    // Preserve manual assignment rather than create a false hard arrival constraint.
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints) { }

    public override void Update()
    {
        foreach (var id in _orbs.Where(p => p.Value.IsDeadOrDestroyed || WorldState.Actors.Find(p.Key) != p.Value).Select(p => p.Key).ToArray())
            _orbs.Remove(id);
    }
}

class DoubleEdge(BossModule module) : Components.CastHint(module, (uint)AID.DoubleEdge, "Double Edge: increasing boss damage; prepare mitigation!");

// Query current actor statuses so a mid-fight module load can recover these windows.
class DamageBuffs(BossModule module) : BossComponent(module)
{
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Module.PrimaryActor.FindStatus(777) != null) // Blessing of the Void, exact current Status row
            hints.Add("Void barrier: boss attacks critically; mitigate tank damage!");
        if (Module.PrimaryActor.FindStatus((uint)SID.KeenEdge) != null)
            hints.Add("Keen Edge: boss damage increased; maintain mitigation!");
    }
}
