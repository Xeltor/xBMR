namespace BossMod.RealmReborn.Alliance.A24Xande;

class ImperiumAndQuaga(BossModule module) : BossComponent(module)
{
    private readonly List<(Actor Target, DateTime Activation)> _stacks = [];
    private readonly List<(WPos Position, DateTime Expires)> _predictedPools = [];
    private readonly List<Actor> _pools = module.Enemies((uint)OID.NullGravity);
    private DateTime _quaga;
    // A destination tolerance around each observed pool center, not an asserted outer VFX boundary.
    public const float PoolArrivalRadius = 1;
    private IEnumerable<Actor> Pools() => _pools.Where(p => !p.IsDeadOrDestroyed && p.EventState != 7);
    private Actor? PartyStack(int slot) => slot is >= 0 and < PartyState.MaxAllianceSize
        ? _stacks.Where(s => Raid.FindSlot(s.Target.InstanceID) is var targetSlot && targetSlot >= 0 && targetSlot / 8 == slot / 8)
            .Select(s => s.Target).FirstOrDefault() : null;

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID == (uint)IconID.Stackmarker)
        {
            _stacks.RemoveAll(s => s.Target == actor);
            // Both observed waves resolve about7.05s after Icon317, with no penalty for overlapping stacks.
            _stacks.Add((actor, WorldState.FutureTime(6.5)));
        }
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != (uint)AID.Imperium)
            return;
        // In both waves the first listed target is the independently observed Icon317 recipient.
        // Only retire the matching stack; other helpers may still be pending.
        if (spell.Targets.Count != 0)
            _stacks.RemoveAll(s => s.Target.InstanceID == spell.Targets[0].ID);
        if (Arena.InBounds(spell.TargetXZ))
            _predictedPools.Add((spell.TargetXZ, WorldState.FutureTime(5)));
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.AncientQuaga)
            _quaga = Module.CastFinishAt(spell, -0.5);
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.AncientQuaga)
        {
            _quaga = default;
            _predictedPools.Clear();
        }
    }
    public override void Update()
    {
        _stacks.RemoveAll(s => s.Target.IsDeadOrDestroyed || WorldState.Actors.Find(s.Target.InstanceID) != s.Target
            || WorldState.CurrentTime > s.Activation.AddSeconds(3));
        _predictedPools.RemoveAll(p => WorldState.CurrentTime > p.Expires || Pools().Any(a => a.Position.InCircle(p.Position, 1)));
    }
    private List<WPos> Shelter(int slot)
    {
        var result = Pools().Select(p => p.Position).Concat(_predictedPools.Select(p => p.Position)).ToList();
        // Quaga can start before the stack resolves. Remain near the party marker, then use the
        // impact location while the corresponding event object is still being created.
        if (PartyStack(slot) is { } pending)
            result.Add(pending.Position);
        return result;
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (PartyStack(slot) is { } stack)
            hints.Add("Stack with your party for Imperium!", !actor.Position.InCircle(stack.Position, 6));
        if (_quaga != default)
            hints.Add("Ancient Quaga: remain in a levitation field!", actor.FindStatus(SID.Levitation) == null);
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (PartyStack(slot) is { } stack)
        {
            var activation = _stacks.First(s => s.Target == stack).Activation;
            hints.AddForbiddenZone(new SDInvertedCircle(stack.Position, 5.5f), activation);
        }
        if (_quaga == default)
            return;
        var safe = Shelter(slot).Select(p => (ShapeDistance)new SDInvertedCircle(p, PoolArrivalRadius)).ToList();
        hints.AddForbiddenZone(safe.Count != 0 ? new SDIntersection([.. safe]) : new SDCircle(Module.PrimaryActor.Position, 100), _quaga);
        BitMask exposed = default;
        foreach (var (index, player) in Raid.WithSlot())
            if (player.FindStatus(SID.Levitation) == null)
                exposed.Set(index);
        hints.AddPredictedDamage(exposed, _quaga, AIHints.PredictedDamageType.Raidwide);
    }
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (PartyStack(pcSlot) is { } stack)
            Arena.AddCircleUnfilled(stack.Position, 6, Colors.Safe);
        if (_quaga != default)
            foreach (var center in Shelter(pcSlot))
                Arena.AddCircleUnfilled(center, PoolArrivalRadius, Colors.Safe);
    }
}
