namespace BossMod.Stormblood.Alliance.A12Hashmal;

class FallingBoulder(BossModule module) : Components.UniformStackSpread(module, 6f, 0f, 2, PartyState.MaxPartySize, raidwideOnResolve: false)
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (!spell.IsSpell(AID.FallingBoulder) || WorldState.Actors.Find(spell.TargetID) is not { } target)
            return;

        var slot = Raid.FindSlot(target.InstanceID);
        if (slot is < 0 or >= PartyState.MaxAllianceSize || IsStackTarget(target))
            return;

        BitMask otherAlliances = default;
        var group = slot / PartyState.MaxPartySize;
        for (var i = 0; i < PartyState.MaxAllianceSize; ++i)
        {
            if (i / PartyState.MaxPartySize != group)
                otherAlliances.Set(i);
        }
        AddStack(target, Module.CastFinishAt(spell), otherAlliances);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.IsSpell(AID.FallingBoulder))
            Stacks.RemoveAll(stack => stack.Target.InstanceID == spell.TargetID);
    }

    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            Stacks.Clear();
        else
            Stacks.RemoveAll(stack => stack.Target == actor);
    }

    protected override bool RequireStackGathering(int slot, Actor actor, in Stack stack)
    {
        foreach (var other in Stacks)
            if (other.ForbiddenPlayers[slot] && !other.Target.IsDeadOrDestroyed
                && stack.Target.Position.InCircle(other.Target.Position, stack.Radius + other.Radius + 1f))
                return false;
        return true;
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        var sector = SeparationSector(slot, actor);
        if (sector >= 0)
            hints.Add($"Separate your party's Falling Boulder stack toward the {ToDust.AllianceSectorName(sector)}.");
        else
            base.AddHints(slot, actor, hints);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (!EnableHints || Stacks.Count == 0)
            return;

        base.AddAIHints(slot, actor, assignment, hints);

        var participants = Raid.WithSlot(excludeNPCs: true);
        foreach (var stack in Stacks)
        {
            if (stack.Target.IsDeadOrDestroyed)
                continue;
            BitMask party = default;
            foreach (var (partySlot, _) in participants)
                if (!stack.ForbiddenPlayers[partySlot])
                    party.Set(partySlot);
            if (party != default)
                hints.AddPredictedDamage(party, stack.Activation, AIHints.PredictedDamageType.Shared);
        }

        if (SeparationSector(slot, actor) is var sector && sector >= 0)
            hints.GoalZones.Add(AIHints.GoalProximity(ToDust.AllianceStagingPosition(Arena.Center, sector), Arena.Bounds.Radius, 16f));
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        base.DrawArenaForeground(pcSlot, pc);
        if (SeparationSector(pcSlot, pc) is var sector && sector >= 0)
            Arena.ZoneCircleOutline(ToDust.AllianceStagingPosition(Arena.Center, sector), 3f, Colors.Safe);
    }

    private int SeparationSector(int slot, Actor actor)
    {
        if (slot is < 0 or >= PartyState.MaxAllianceSize)
            return -1;

        foreach (var own in Stacks)
        {
            if (own.ForbiddenPlayers[slot] || own.Target.IsDeadOrDestroyed || RequireStackGathering(slot, actor, own))
                continue;

            var group = Raid.AllianceGroupForSlot(slot);
            return group >= 0 ? group : slot / PartyState.MaxPartySize;
        }
        return -1;
    }
}
