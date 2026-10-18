namespace BossMod.RealmReborn.Alliance.A21Scylla;

class ElementalSouls(BossModule module) : BossComponent(module)
{
    // Lightning receivers from the boss-one EventRange layout (3.375y radius).
    public static readonly WPos[] Pillars = [new(-25.75797f, -206.9802f), new(-0.008f, -162.4192f), new(25.71078f, -206.9875f)];
    public const float PillarArrivalRadius = 3;
    // Conservative destination tolerance at the actual water object's center, not its measured boundary.
    public const float WaterArrivalRadius = 1;
    private readonly List<Actor> _pools = module.Enemies((uint)OID.Water);
    private DateTime _flare;

    private IEnumerable<Actor> Pools() => _pools.Where(p => !p.IsDeadOrDestroyed && p.EventState != 7);
    private Actor? Soul(Actor player) => WorldState.Actors.FirstOrDefault(a => !a.IsDeadOrDestroyed && a.Tether.Target == player.InstanceID
        && a.OID is (uint)OID.ShudderingSoul or (uint)OID.ShiveringSoul or (uint)OID.SmolderingSoul);
    private Actor? Frozen(Actor player) => Raid.WithoutSlot().Where(p => !p.IsDeadOrDestroyed
        && (p.FindStatus(SID.DeepFreeze) != null || p.FindStatus(SID.DeepFreezeLegacy) != null)
        && p.FindStatus(SID.FireResistanceUp) == null && Arena.InBounds(p.Position))
        .OrderBy(p => (p.Position - player.Position).LengthSq()).ThenBy(p => p.InstanceID).FirstOrDefault();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.AncientFlare)
            _flare = Module.CastFinishAt(spell, -0.5);
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.AncientFlare)
            _flare = default;
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        var soul = Soul(actor);
        if (soul?.OID == (uint)OID.ShudderingSoul)
        {
            var group = SyrcusAlliance.Group(Raid, slot);
            hints.Add(group < 0 ? "Lightning orb: alliance unknown - deliver to a lightning pillar" : $"Deliver lightning orb to alliance {(char)('A' + group)} pillar",
                group < 0 || !actor.Position.InCircle(Pillars[group], PillarArrivalRadius));
        }
        else if (soul?.OID == (uint)OID.ShiveringSoul)
            hints.Add("Ice orb: stay out of water until it resolves!", actor.FindStatus(SID.FireResistanceUp) != null);
        else if (soul?.OID == (uint)OID.SmolderingSoul)
            hints.Add("Fire orb: thaw a frozen player; avoid existing water!", false);
        if (_flare != default)
            hints.Add(soul?.OID is (uint)OID.ShiveringSoul or (uint)OID.SmolderingSoul
                ? "Resolve your elemental orb, then shelter in water for Ancient Flare!"
                : "Ancient Flare: shelter in water!", actor.FindStatus(SID.FireResistanceUp) == null);
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var soul = Soul(actor);
        if (soul?.OID == (uint)OID.ShudderingSoul)
        {
            var group = SyrcusAlliance.Group(Raid, slot);
            if (group >= 0)
                hints.AddForbiddenZone(new SDInvertedCircle(Pillars[group], PillarArrivalRadius));
        }
        else if (soul?.OID == (uint)OID.SmolderingSoul && Frozen(actor) is { } frozen)
            hints.GoalZones.Add(AIHints.GoalProximity(frozen.Position, 1, 10));
        // Carrying fire/ice into water destroys shelter or freezes everyone in it. Never impose
        // contradictory orb and shelter constraints; the requirement starts when that orb resolves.
        if (_flare != default && soul == null)
        {
            var safe = Pools().Select(p => (ShapeDistance)new SDInvertedCircle(p.Position, WaterArrivalRadius)).ToList();
            if (safe.Count != 0)
                hints.AddForbiddenZone(new SDIntersection([.. safe]), _flare);
        }
        foreach (var enemy in hints.PotentialTargets)
            if (enemy.Actor.OID is (uint)OID.ShudderingSoul or (uint)OID.ShiveringSoul or (uint)OID.SmolderingSoul or (uint)OID.Water)
                enemy.Priority = AIHints.Enemy.PriorityForbidden;
    }
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (Soul(pc)?.OID == (uint)OID.ShudderingSoul && SyrcusAlliance.Group(Raid, pcSlot) is var group && group >= 0)
            Arena.AddCircleUnfilled(Pillars[group], 3.375f, Colors.Safe);
        if (_flare != default)
            foreach (var pool in Pools())
                Arena.AddCircleUnfilled(pool.Position, WaterArrivalRadius, Colors.Safe);
    }
}
