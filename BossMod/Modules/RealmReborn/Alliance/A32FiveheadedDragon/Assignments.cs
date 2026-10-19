namespace BossMod.RealmReborn.Alliance.A32FiveheadedDragon;

class Markers(BossModule module) : Components.GenericStackSpread(module)
{
    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID == (uint)IconID.Icon318)
        {
            Stacks.RemoveAll(s => s.Target == actor);
            // Six-second marker; four participants is a response policy, not a proven lethal threshold.
            Stacks.Add(new(actor, 6, 4, activation: WorldState.FutureTime(5.5)));
        }
        else if (iconID == (uint)IconID.Icon46)
        {
            Spreads.RemoveAll(s => s.Target == actor);
            Spreads.Add(new(actor, 20, WorldState.FutureTime(8)));
        }
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.BreathOfThunder && spell.Targets.Count != 0)
            Stacks.RemoveAll(s => s.Target.InstanceID == spell.Targets[0].ID);
        else if (spell.Action.ID == (uint)AID.Fermata)
            Spreads.Clear();
    }
    public override void Update()
    {
        Stacks.RemoveAll(s => s.Target.IsDeadOrDestroyed || WorldState.CurrentTime > s.Activation.AddSeconds(2));
        Spreads.RemoveAll(s => s.Target.IsDeadOrDestroyed || WorldState.CurrentTime > s.Activation.AddSeconds(2));
    }
}

class AddsAndPrey(BossModule module) : BossComponent(module)
{
    private readonly Dictionary<Actor, Actor> _handoffs = [];
    private static bool HasPrey(Actor a) => a.FindStatus(SID.Prey1) != null || a.FindStatus(SID.Prey2) != null;
    private static bool Immune(Actor a) => a.FindStatus(SID.SlipperyPrey1) != null || a.FindStatus(SID.SlipperyPrey2) != null;
    private bool Eligible(Actor actor, Actor recipient) => recipient.Type == ActorType.Player && recipient != actor && !recipient.IsDeadOrDestroyed
        && WorldState.Actors.Find(recipient.InstanceID) == recipient && Arena.InBounds(recipient.Position) && !HasPrey(recipient) && !Immune(recipient)
        && recipient.FindStatus(SID.DeepFreeze) == null && recipient.FindStatus(SID.Pyretic) == null && recipient.InstanceID != Module.PrimaryActor.TargetID;
    private bool SafeDestination(int slot, Actor actor, Actor recipient)
    {
        foreach (var component in Module.Components.OfType<Components.GenericAOEs>())
            foreach (ref readonly var aoe in component.ActiveAOEs(slot, actor))
                if (aoe.Risky && aoe.Check(recipient.Position))
                    return false;
        if (Module.FindComponent<Markers>() is { } markers)
            foreach (var spread in markers.Spreads)
                if (spread.Target == actor || spread.Target == recipient || recipient.Position.InCircle(spread.Target.Position, spread.Radius))
                    return false;
        return true;
    }
    private Actor? Handoff(int slot, Actor actor)
    {
        if (_handoffs.TryGetValue(actor, out var current) && Eligible(actor, current) && SafeDestination(slot, actor, current))
            return current;
        var recipient = Raid.WithoutSlot().Where(p => Eligible(actor, p) && SafeDestination(slot, actor, p))
            .OrderBy(p => (p.Position - actor.Position).LengthSq()).ThenBy(p => p.InstanceID).FirstOrDefault();
        if (recipient != null)
            _handoffs[actor] = recipient;
        else
            _handoffs.Remove(actor);
        return recipient;
    }
    public override void OnStatusLose(Actor actor, ref ActorStatus status)
    {
        if (status.ID is (uint)SID.Prey1 or (uint)SID.Prey2)
            _handoffs.Remove(actor);
    }
    public override void OnActorDestroyed(Actor actor)
    {
        _handoffs.Remove(actor);
        foreach (var source in _handoffs.Where(p => p.Value == actor).Select(p => p.Key).ToArray())
            _handoffs.Remove(source);
    }
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Module.PrimaryActor.CastInfo?.Action.ID == (uint)AID.Discordance)
            hints.Add("Destroy all four heads before Discordance finishes!");
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (HasPrey(actor))
            hints.Add(Handoff(slot, actor) == null ? "Prey: no safe eligible handoff player found!" : "Pass Prey to a player without Slippery Prey!");
        if (actor.FindStatus(SID.Pyretic) != null)
            hints.Add("Pyretic: stop moving and acting!");
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var target in hints.PotentialTargets)
            if (target.Actor.InCombat && target.Priority >= 0)
                target.Priority = target.Actor.OID switch
                {
                    (uint)OID.HeadOfFire or (uint)OID.HeadOfIce or (uint)OID.HeadOfPoison or (uint)OID.HeadOfThunder => 5,
                    (uint)OID.Prominence => 4,
                    (uint)OID.ToxicSlime => 4,
                    (uint)OID.PoisonSlime => 3,
                    (uint)OID.Boss => 1,
                    _ => target.Priority
                };
        if (HasPrey(actor) && actor.FindStatus(SID.Pyretic) == null && actor.FindStatus(SID.DeepFreeze) == null
            && Module.FindComponent<HeatWavePreparation>()?.Active != true
            && Handoff(slot, actor) is { } recipient)
            // Required contact response, not an optional uptime preference. The 0.5y
            // arrival tolerance aims through the recipient; it is not a measured transfer radius.
            hints.AddForbiddenZone(new SDInvertedCircle(recipient.Position, 0.5f), WorldState.CurrentTime);
        if (actor.FindStatus(SID.Pyretic) is { } pyretic)
            hints.AddSpecialMode(AIHints.SpecialMode.Pyretic, WorldState.CurrentTime, pyretic.ExpireAt);
    }
}

class HeatWavePreparation(BossModule module) : BossComponent(module)
{
    private DateTime _activation, _finish;
    private bool _resolved;
    public bool Active => _activation != default && WorldState.CurrentTime < _finish;
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.HeatWave)
        {
            // Prepare before the cast ends, rather than waiting for the Pyretic status packet.
            _activation = Module.CastFinishAt(spell, -0.5);
            _finish = Module.CastFinishAt(spell, 5);
            _resolved = false;
        }
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.HeatWave)
            _resolved = true;
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.HeatWave && !_resolved)
            _activation = default;
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Active)
            hints.Add("Heat Wave: stop moving and acting!", WorldState.CurrentTime >= _activation);
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (Active && actor.FindStatus(SID.Pyretic) == null)
            hints.AddSpecialMode(AIHints.SpecialMode.Pyretic, _activation, _finish);
    }
}

class PoisonSuppression(BossModule module) : BossComponent(module)
{
    private readonly Dictionary<Actor, Actor> _assignees = [];
    private IEnumerable<Actor> Puddles => WorldState.Actors.Where(p => p.OID == 2004780 && !p.IsDeadOrDestroyed);
    private bool Resistant(Actor actor) => actor.FindStatus(SID.PoisonResistanceUp) is { } resistance && resistance.ExpireAt > WorldState.FutureTime(3);
    private bool Eligible(Actor actor, Actor puddle)
    {
        if (actor.Type != ActorType.Player || actor.IsDeadOrDestroyed || !Arena.InBounds(actor.Position) || !Resistant(actor)
            || actor.InstanceID == Module.PrimaryActor.TargetID || actor.FindStatus(SID.Pyretic) != null
            || actor.FindStatus(SID.DeepFreeze) != null || actor.FindStatus(SID.Prey1) != null || actor.FindStatus(SID.Prey2) != null
            || Module.FindComponent<HeatWavePreparation>()?.Active == true
            || Module.Components.OfType<Markers>().Any(m => m.Active))
            return false;
        var travel = (puddle.Position - actor.Position).Length() / Math.Clamp(WorldState.Client.MoveSpeed, 0.1f, 6) + 3;
        if (actor.FindStatus(SID.PoisonResistanceUp)!.Value.ExpireAt <= WorldState.FutureTime(travel))
            return false;
        // A known overlapping hazard takes precedence over this assignment.
        var slot = Raid.FindSlot(actor.InstanceID);
        foreach (var component in Module.Components.OfType<Components.GenericAOEs>())
            foreach (ref readonly var aoe in component.ActiveAOEs(slot, actor))
                if (aoe.Risky && aoe.Check(puddle.Position))
                    return false;
        return true;
    }
    private Actor? Destination(Actor actor)
    {
        var claimed = new HashSet<Actor>();
        Actor? destination = null;
        foreach (var puddle in Puddles.OrderBy(p => p.InstanceID))
        {
            var occupant = Raid.WithoutSlot().Where(p => p.Type == ActorType.Player && !p.IsDeadOrDestroyed && p.Position.InCircle(puddle.Position, 1))
                .OrderBy(p => p.InstanceID).FirstOrDefault();
            Actor? selected = null;
            if (occupant != null)
                selected = !claimed.Contains(occupant) && Eligible(occupant, puddle) ? occupant : null;
            else if (_assignees.TryGetValue(puddle, out var existing) && !claimed.Contains(existing) && Eligible(existing, puddle))
                selected = existing;
            else
                selected = Raid.WithoutSlot().Where(p => !claimed.Contains(p) && Eligible(p, puddle))
                    .OrderBy(p => (p.Position - puddle.Position).LengthSq()).ThenBy(p => p.InstanceID).FirstOrDefault();
            if (selected == null)
            {
                _assignees.Remove(puddle);
                continue;
            }
            _assignees[puddle] = selected;
            claimed.Add(selected);
            if (selected == actor)
                destination = puddle;
        }
        return destination;
    }
    public override void OnActorDestroyed(Actor actor)
    {
        _assignees.Remove(actor);
        foreach (var puddle in _assignees.Where(p => p.Value == actor).Select(p => p.Key).ToArray())
            _assignees.Remove(puddle);
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Puddles.Any())
            hints.Add(Destination(actor) != null ? "Poison resistance: occupy the marked center to suppress slimes!"
                : Resistant(actor) ? "One resistant player per poison puddle!" : "Kill the poison slime and gain poison resistance!", false);
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // A center arrival is an interior response policy, not a claimed puddle-edge radius.
        if (Destination(actor) is { } puddle)
            hints.AddForbiddenZone(new SDInvertedCircle(puddle.Position, 1), WorldState.CurrentTime);
    }
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (Destination(pc) is { } puddle)
            Arena.AddCircleUnfilled(puddle.Position, 1, Colors.Safe);
    }
}
