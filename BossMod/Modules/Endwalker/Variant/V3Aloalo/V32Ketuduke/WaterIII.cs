namespace BossMod.Endwalker.VariantCriterion.V3Aloalo.V32Ketuduke;

sealed class WaterIII(BossModule module) : BossComponent(module)
{
    private readonly Dictionary<ulong, (Actor Source, Actor Target, uint ID)> tethers = [];
    private readonly Dictionary<ulong, (Actor Source, Actor Target, ActorCastInfo Cast, DateTime Activation)> pending = [];

    private bool Live(Actor actor) => !actor.IsDestroyed && WorldState.Actors.Find(actor.InstanceID) == actor;
    private void Prune()
    {
        foreach (var key in tethers.Where(t => !Live(t.Value.Source) || !Live(t.Value.Target)).Select(t => t.Key).ToArray())
            tethers.Remove(key);
        foreach (var key in pending.Where(p => !Live(p.Value.Source) || !Live(p.Value.Target)).Select(p => p.Key).ToArray())
            pending.Remove(key);
    }

    public override void Update() => Prune();

    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        Prune();
        if (source.OID != (uint)OID.SummonedApa || tether.ID is not ((uint)TetherID.WaterIIIGood or (uint)TetherID.WaterIIIStretch)
            || !Live(source) || WorldState.Actors.Find(tether.Target) is not { } target)
            return;
        tethers[source.InstanceID] = (source, target, tether.ID);
        if (pending.TryGetValue(source.InstanceID, out var hit))
            pending[source.InstanceID] = (source, target, hit.Cast, hit.Activation);
    }

    public override void OnUntethered(Actor source, in ActorTetherInfo tether) => tethers.Remove(source.InstanceID);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster.OID != (uint)OID.SummonedApa || spell.Action.ID != (uint)AID.WaterIII || !Live(caster))
            return;
        Prune();
        pending.Remove(caster.InstanceID);
        if (tethers.TryGetValue(caster.InstanceID, out var tether))
            pending[caster.InstanceID] = (caster, tether.Target, spell, Module.CastFinishAt(spell));
        else if (caster.Tether.ID is (uint)TetherID.WaterIIIGood or (uint)TetherID.WaterIIIStretch
            && WorldState.Actors.Find(caster.Tether.Target) is { } target)
            pending[caster.InstanceID] = (caster, target, spell, Module.CastFinishAt(spell));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (!spell.EventHappened && pending.TryGetValue(caster.InstanceID, out var hit) && hit.Cast == spell)
            pending.Remove(caster.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        Prune();
        if (spell.Action.ID is (uint)AID.WaterIIIApply or (uint)AID.WaterIIIApplyUnstretched
            && pending.TryGetValue(caster.InstanceID, out var hit) && hit.Source == caster
            && spell.Targets.Any(t => t.ID == hit.Target.InstanceID))
            pending.Remove(caster.InstanceID);
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        Prune();
        foreach (var tether in tethers.Values)
            if (tether.Target == actor)
                hints.Add(tether.ID == (uint)TetherID.WaterIIIStretch ? "Stretch Water III tether!" : "Water III tether stretched", tether.ID == (uint)TetherID.WaterIIIStretch);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        Prune();
        foreach (var tether in tethers.Values)
            if (tether.Target == actor && tether.ID == (uint)TetherID.WaterIIIStretch)
            {
                var origin = tether.Source.Position;
                // The observed tether state controls this preference; an assumed
                // 20-yalm circle would claim a safe boundary we haven't measured.
                hints.GoalZones.Add(p => { var d = (p - origin).Length(); return d / (1f + d); });
            }
        foreach (var hit in pending.Values)
        {
            var targetSlot = Raid.FindSlot(hit.Target.InstanceID);
            if (targetSlot >= 0)
                hints.AddPredictedDamage(new BitMask(1ul << targetSlot), hit.Activation, type: AIHints.PredictedDamageType.None);
        }
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        Prune();
        foreach (var tether in tethers.Values)
            Arena.AddLine(tether.Source.Position, tether.Target.Position, tether.ID == (uint)TetherID.WaterIIIGood ? Colors.Safe : Colors.Danger);
    }
}
