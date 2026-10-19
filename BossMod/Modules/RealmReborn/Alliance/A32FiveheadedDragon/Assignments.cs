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
    private static bool HasPrey(Actor a) => a.FindStatus(SID.Prey1) != null || a.FindStatus(SID.Prey2) != null;
    private static bool Immune(Actor a) => a.FindStatus(SID.SlipperyPrey1) != null || a.FindStatus(SID.SlipperyPrey2) != null;
    private Actor? Handoff(Actor actor) => Raid.WithoutSlot().Where(p => p.Type == ActorType.Player && p != actor && !p.IsDeadOrDestroyed
        && Arena.InBounds(p.Position) && !HasPrey(p) && !Immune(p) && p.FindStatus(SID.DeepFreeze) == null && p.InstanceID != Module.PrimaryActor.TargetID)
        .OrderBy(p => (p.Position - actor.Position).LengthSq()).ThenBy(p => p.InstanceID).FirstOrDefault();
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Module.PrimaryActor.CastInfo?.Action.ID == (uint)AID.Discordance)
            hints.Add("Destroy all four heads before Discordance finishes!");
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (HasPrey(actor))
            hints.Add(Handoff(actor) == null ? "Prey: no eligible handoff player found!" : "Pass Prey to a player without Slippery Prey!");
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
        if (HasPrey(actor) && actor.FindStatus(SID.Pyretic) == null && Handoff(actor) is { } recipient)
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
        if (_activation != default && WorldState.CurrentTime < _finish)
            hints.Add("Heat Wave: stop moving and acting!", WorldState.CurrentTime >= _activation);
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_activation != default && WorldState.CurrentTime < _finish && actor.FindStatus(SID.Pyretic) == null)
            hints.AddSpecialMode(AIHints.SpecialMode.Pyretic, _activation, _finish);
    }
}

class PoisonSuppression(BossModule module) : BossComponent(module)
{
    private IEnumerable<Actor> Puddles => WorldState.Actors.Where(p => p.OID == 2004780 && !p.IsDeadOrDestroyed);
    private bool Resistant(Actor actor) => actor.FindStatus(SID.PoisonResistanceUp) is { } resistance && resistance.ExpireAt > WorldState.FutureTime(3);
    private Actor? Destination(int slot, Actor actor)
    {
        if (!Resistant(actor) || actor.InstanceID == Module.PrimaryActor.TargetID || actor.FindStatus(SID.Pyretic) != null
            || actor.FindStatus(SID.DeepFreeze) != null || actor.FindStatus(SID.Prey1) != null || actor.FindStatus(SID.Prey2) != null
            || Module.PrimaryActor.CastInfo?.Action.ID == (uint)AID.HeatWave
            || Module.Components.OfType<Markers>().Any(m => m.Active))
            return null;
        foreach (var puddle in Puddles.OrderBy(p => (p.Position - actor.Position).LengthSq()))
        {
            // One person can suppress the pool. Do not displace an existing occupant.
            if (Raid.WithoutSlot().Any(p => p != actor && !p.IsDeadOrDestroyed && Resistant(p) && p.Position.InCircle(puddle.Position, 1)))
                continue;
            var travel = (puddle.Position - actor.Position).Length() / Math.Clamp(WorldState.Client.MoveSpeed, 0.1f, 6) + 3;
            if (actor.FindStatus(SID.PoisonResistanceUp)!.Value.ExpireAt <= WorldState.FutureTime(travel))
                continue;
            // A known overlapping hazard takes precedence over this assignment.
            var blocked = false;
            foreach (var component in Module.Components.OfType<Components.GenericAOEs>())
                foreach (ref readonly var aoe in component.ActiveAOEs(slot, actor))
                    blocked |= aoe.Risky && aoe.Check(puddle.Position);
            if (blocked)
                continue;
            return puddle;
        }
        return null;
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Puddles.Any())
            hints.Add(Resistant(actor) ? "Poison resistance: occupy the poison puddle to suppress slimes!" : "Kill the poison slime and gain poison resistance!", false);
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // A center arrival is an interior response policy, not a claimed puddle-edge radius.
        if (Destination(slot, actor) is { } puddle)
            hints.AddForbiddenZone(new SDInvertedCircle(puddle.Position, 1), WorldState.CurrentTime);
    }
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (Destination(pcSlot, pc) is { } puddle)
            Arena.AddCircleUnfilled(puddle.Position, 1, Colors.Safe);
    }
}
