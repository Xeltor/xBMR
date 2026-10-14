namespace BossMod.Heavensward.Alliance.A33ProtoUltima;

// The marker belongs to an orb helper, not a player or Aether Collector.
// Its unnamed radius8 action has no recorded targets; do not assert that radius as stack eligibility.
class Eikonizer(BossModule module) : BossComponent(module)
{
    private Actor? _orb;
    private Actor? _expiredOrb;
    private DateTime _activation;

    public override void OnActorCreated(Actor actor)
    {
        if (_orb is { } orb && orb.InstanceID == actor.InstanceID && orb != actor)
            ClearPending();
        if (_expiredOrb is { } expired && expired.InstanceID == actor.InstanceID && expired != actor)
            _expiredOrb = null;
    }

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID == (uint)IconID.Icon62 && actor.OID == (uint)OID.ProtoUltimaHelper2 && targetID == actor.InstanceID
            && !actor.IsDeadOrDestroyed && WorldState.Actors.Find(actor.InstanceID) == actor && actor != _orb && actor != _expiredOrb)
        {
            _orb = actor;
            // Single training instance: marker to named Eikonizer hit 5.5446s; conservative client deadline.
            _activation = WorldState.FutureTime(5.4);
        }
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_orb != null)
            hints.Add("Eikonizer: stack at the marked orb!");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_orb is not { } orb)
            return;
        var position = orb.Position;
        // A preference supports gathering without inventing an independently unverified eligibility boundary.
        hints.GoalZones.Add(p => 5 / (1 + (p - position).Length()));
        BitMask affected = default;
        foreach (var (s, _) in Raid.WithSlot())
            affected.Set(s);
        if (affected != default)
            hints.AddPredictedDamage(affected, _activation);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (_orb is { } orb)
            Arena.Actor(orb, Colors.Safe, allowDeadAndUntargetable: true);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.UnknownWeaponskill4)
        {
            if (caster == _orb)
                ClearPending();
            if (caster == _expiredOrb)
                _expiredOrb = null; // A late actual resolution also permits a genuinely new marker wave.
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == _orb)
            ClearPending();
        if (actor == _expiredOrb)
            _expiredOrb = null;
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor == _orb)
            ClearPending();
    }

    public override void Update()
    {
        if (_orb is not { } orb)
            return;
        if (orb.IsDeadOrDestroyed || WorldState.Actors.Find(orb.InstanceID) != orb)
            ClearPending();
        else if (WorldState.CurrentTime >= _activation.AddSeconds(2))
        {
            _expiredOrb = orb; // Lost-resolution cleanup, not a claim about physical hazard duration.
            ClearPending(); // A repeated unresolved marker from this generation cannot restart the stale forecast.
        }
    }

    private void ClearPending()
    {
        _orb = null;
        _activation = default;
    }
}
