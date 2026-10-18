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
        && !HasPrey(p) && !Immune(p) && p.FindStatus(SID.DeepFreeze) == null && p.InstanceID != Module.PrimaryActor.TargetID)
        .OrderBy(p => p.InstanceID).FirstOrDefault();
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
        if (HasPrey(actor) && Handoff(actor) is { } recipient)
            // Proximity preference only: the exact transfer collision boundary is unverified.
            hints.GoalZones.Add(p => p.InCircle(recipient.Position, 1) ? 2 : 0);
        if (actor.FindStatus(SID.Pyretic) is { } pyretic)
            hints.AddSpecialMode(AIHints.SpecialMode.Pyretic, WorldState.CurrentTime, pyretic.ExpireAt);
    }
}
