namespace BossMod.Shadowbringers.Raid.E01EdenPrime;

sealed class Heavensunder(BossModule module) : Components.SingleTargetCastDelay(module, (uint)AID.SpearOfParadise, (uint)AID.Heavensunder, 1.46d, "Tankbuster: second hit")
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.SpearOfParadise)
        {
            Targets.RemoveAll(t => t.caster == caster);
            if (Raid.FindSlot(spell.TargetID) >= 0)
                base.OnCastStarted(caster, spell);
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.SpearOfParadise && !spell.EventHappened)
            Targets.RemoveAll(t => t.caster == caster);
    }

    public override void Update()
    {
        Targets.RemoveAll(t => t.caster.IsDeadOrDestroyed || t.target.IsDeadOrDestroyed
            || WorldState.Actors.Find(t.instanceID) != t.target);
    }
}

sealed class ViceOfApathy(BossModule module) : Components.SpreadFromIcon(module, (uint)IconID.ViceOfApathy, (uint)AID.ViceOfApathy, 6f, 6.1d)
{
    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID == (uint)IconID.ViceOfApathy)
            Spreads.RemoveAll(s => s.Target.InstanceID == actor.InstanceID);
        base.OnEventIcon(actor, iconID, targetID);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.ViceAndVirtueApathy && !spell.EventHappened)
            Spreads.Clear();
    }

    public override void Update()
    {
        base.Update();
        Spreads.RemoveAll(s => s.Target.IsDestroyed || WorldState.Actors.Find(s.Target.InstanceID) != s.Target);
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        base.AddHints(slot, actor, hints);
        if (Spreads.Any(s => s.Target == actor))
            hints.Add("Drop the black hole near an edge!", false);
    }
}

sealed class ViceOfVanity(BossModule module) : Components.GenericBaitAway(module, (uint)AID.ViceOfVanity, tankbuster: true, damageType: AIHints.PredictedDamageType.Tankbuster)
{
    private static readonly AOEShapeRect Shape = new(100f, 3f);
    private readonly HashSet<ulong> _resolvedHelpers = [];

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID != (uint)IconID.ViceOfVanity)
            return;
        CurrentBaits.RemoveAll(b => b.Target.InstanceID == actor.InstanceID);
        if (CurrentBaits.Count == 0)
            _resolvedHelpers.Clear();
        CurrentBaits.Add(new(Module.PrimaryActor, actor, Shape, WorldState.FutureTime(6.6d)));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        // Successful visual cast ends ~0.8s before the helpers hit. Keep both baits.
        if (spell.Action.ID == (uint)AID.ViceAndVirtueVanity && !spell.EventHappened)
            CurrentBaits.Clear();
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != (uint)AID.ViceOfVanity || !_resolvedHelpers.Add(caster.InstanceID))
            return;
        ++NumCasts;
        // Each helper is self-targeted, so MainTargetID cannot identify the marked tank.
        // Retain the pair through the first packet rather than remove an arbitrary tank.
        if (_resolvedHelpers.Count >= 2)
            CurrentBaits.Clear();
    }

    public override void Update()
    {
        CurrentBaits.RemoveAll(b => b.Target.IsDeadOrDestroyed || b.Source.IsDeadOrDestroyed
            || WorldState.Actors.Find(b.Target.InstanceID) != b.Target);
    }
}

sealed class Guardians(BossModule module) : BossComponent(module)
{
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        var adds = Module.Enemies((uint)OID.GuardianOfParadise);
        if (adds.Any(a => a.IsTargetable && !a.IsDeadOrDestroyed))
            hints.Add("Kill the adds; tanks face them away from the party!");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var enemy in hints.PotentialTargets)
            if (enemy.Actor.OID == (uint)OID.GuardianOfParadise)
                enemy.Priority = 2;
    }
}
