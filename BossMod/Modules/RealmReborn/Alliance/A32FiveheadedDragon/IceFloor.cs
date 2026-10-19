namespace BossMod.RealmReborn.Alliance.A32FiveheadedDragon;

class IceFloor(BossModule module) : Components.GenericAOEs(module, warningText: "Ice expanding: leave its growth area!")
{
    // Reviewed upstream final footprint. Intermediate visual scales do not establish
    // their hit radii, so preview the final area and clear it before growth starts.
    private static readonly AOEShapeCircle FullSize = new(12);
    private sealed class Patch(Actor? caster, WPos origin, DateTime fullAt, DateTime clearAt)
    {
        public readonly Actor? Caster = caster;
        public WPos Origin = origin;
        public DateTime FullAt = fullAt, ClearAt = clearAt;
        public bool Resolved, Spawned;
        public DateTime FirstSeen;
        public uint HighestStage;
        public readonly List<Actor> Stages = [];
    }
    private readonly List<Patch> _patches = [];
    private readonly List<AOEInstance> _aoes = [];
    private static bool IsIce(Actor actor) => actor.OID is (uint)OID.IceSmall or (uint)OID.IceMedium or (uint)OID.IceLarge;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.BreathOfIce)
            return;
        _patches.RemoveAll(p => p.Caster == caster && !p.Resolved && !p.Spawned);
        _patches.Add(new(caster, spell.LocXZ, Module.CastFinishAt(spell, 7.4), Module.CastFinishAt(spell, 2.5)));
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != (uint)AID.BreathOfIce)
            return;
        var patch = _patches.LastOrDefault(p => p.Caster == caster);
        if (patch?.Resolved == true)
            return;
        if (patch == null)
        {
            patch = new(caster, spell.TargetXZ, WorldState.FutureTime(7.4), WorldState.FutureTime(2.5));
            _patches.Add(patch);
        }
        patch.Origin = spell.TargetXZ;
        patch.Resolved = true;
        patch.FullAt = WorldState.FutureTime(7.4);
        patch.ClearAt = WorldState.FutureTime(2.5);
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.BreathOfIce)
            _patches.RemoveAll(p => p.Caster == caster && !p.Resolved && !p.Spawned);
    }
    public override void OnActorCreated(Actor actor)
    {
        if (!IsIce(actor) || _patches.Any(p => p.Stages.Contains(actor)))
            return;
        // Match one stage per patch, including simultaneous patches at the same point.
        // Old stage actors overlap their successors and must not create duplicate AOEs.
        var patch = _patches.Where(p => p.Origin.InCircle(actor.Position, 0.5f) && !p.Stages.Any(s => s.OID == actor.OID)
                && (actor.OID > p.HighestStage || WorldState.CurrentTime <= p.FirstSeen.AddSeconds(0.2)))
            .MinBy(p => (p.Origin - actor.Position).LengthSq());
        var delay = actor.OID == (uint)OID.IceSmall ? 4 : actor.OID == (uint)OID.IceMedium ? 2 : 0;
        if (patch == null)
        {
            patch = new(null, actor.Position, WorldState.FutureTime(delay), WorldState.CurrentTime);
            _patches.Add(patch);
        }
        patch.Origin = actor.Position;
        if (!patch.Spawned)
            patch.FirstSeen = WorldState.CurrentTime;
        patch.Spawned = patch.Resolved = true;
        patch.Stages.Add(actor);
        patch.ClearAt = WorldState.CurrentTime;
        if (actor.OID > patch.HighestStage)
        {
            patch.HighestStage = actor.OID;
            patch.FullAt = WorldState.FutureTime(delay);
        }
    }
    public override void OnActorDestroyed(Actor actor)
    {
        foreach (var patch in _patches)
            patch.Stages.Remove(actor);
        _patches.RemoveAll(p => p.Spawned && p.Stages.Count == 0 || !p.Resolved && p.Caster == actor);
    }
    public override void Update()
    {
        // Bound missing-spawn predictions; extant stage actors own actual lifetime.
        _patches.RemoveAll(p => !p.Spawned && WorldState.CurrentTime > p.FullAt.AddSeconds(3));
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.Clear();
        foreach (var patch in _patches)
        {
            var source = patch.Stages.MaxBy(s => s.OID) ?? patch.Caster;
            _aoes.Add(new(FullSize, patch.Origin, activation: patch.FullAt,
                color: WorldState.CurrentTime < patch.FullAt ? Colors.AOE : Colors.Danger, actorID: source?.InstanceID ?? 0));
        }
        return CollectionsMarshal.AsSpan(_aoes);
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var patch in _patches)
            // Early response policy, separate from the final-size forecast activation.
            // Avoid an unverified greed window inside the intermediate growing ice.
            hints.AddForbiddenZone(FullSize.Distance(patch.Origin, default), patch.ClearAt,
                (patch.Stages.MaxBy(s => s.OID) ?? patch.Caster)?.InstanceID ?? 0);
    }
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var patch in _patches)
            if (WorldState.CurrentTime < patch.FullAt)
                Arena.AddCircleUnfilled(patch.Origin, 12, Colors.Danger);
    }
}
