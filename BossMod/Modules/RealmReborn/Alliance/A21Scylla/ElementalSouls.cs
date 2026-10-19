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
    private readonly HashSet<Actor> _resolvedSouls = [];

    private IEnumerable<Actor> Pools() => _pools.Where(p => !p.IsDeadOrDestroyed && p.EventState != 7);
    private Actor? Soul(Actor player) => WorldState.Actors.FirstOrDefault(a => !a.IsDeadOrDestroyed && !_resolvedSouls.Contains(a) && a.Tether.Target == player.InstanceID
        && a.OID is (uint)OID.ShudderingSoul or (uint)OID.ShiveringSoul or (uint)OID.SmolderingSoul);
    private Actor? Frozen(Actor player) => Raid.WithoutSlot().Where(p => !p.IsDeadOrDestroyed
        && (p.FindStatus(SID.DeepFreeze) != null || p.FindStatus(SID.DeepFreezeLegacy) != null)
        && p.FindStatus(SID.FireResistanceUp) == null && Arena.InBounds(p.Position))
        .OrderBy(p => (p.Position - player.Position).LengthSq()).ThenBy(p => p.InstanceID).FirstOrDefault();
    private WPos? LeaveWater(Actor actor)
    {
        if (actor.FindStatus(SID.FireResistanceUp) == null || Pools().MinBy(p => (p.Position - actor.Position).LengthSq()) is not { } pool)
            return null;
        var delta = actor.Position - pool.Position;
        var direction = delta.LengthSq() > 0.01f ? delta.Normalized() : new WDir(0, 1);
        // Status520 confirms actual water contact. Step out using that feedback;
        // neither this2y step nor the0.5y arrival tolerance claims the pool's radius.
        foreach (var turn in new[] { 0, 45, -45, 90, -90 })
        {
            var destination = actor.Position + 2 * direction.Rotate(turn.Degrees());
            if (Arena.InBounds(destination) && (destination - pool.Position).LengthSq() > delta.LengthSq())
                return destination;
        }
        return null;
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.Shiver or (uint)AID.Smolder or (uint)AID.Shudder)
            _resolvedSouls.Add(caster);
    }
    public override void OnActorDestroyed(Actor actor) => _resolvedSouls.Remove(actor);
    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (tether.Target != 0)
            _resolvedSouls.Remove(source);
    }

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
        if (actor.FindStatus(SID.DeepFreeze) != null || actor.FindStatus(SID.DeepFreezeLegacy) != null)
            return;
        if (soul?.OID is (uint)OID.ShiveringSoul or (uint)OID.SmolderingSoul && LeaveWater(actor) != null)
            hints.Add("Elemental orb: leave the water now!");
        else if (soul?.OID == (uint)OID.ShudderingSoul)
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
        foreach (var enemy in hints.PotentialTargets)
            if (enemy.Actor.OID is (uint)OID.ShudderingSoul or (uint)OID.ShiveringSoul or (uint)OID.SmolderingSoul or (uint)OID.Water)
                enemy.Priority = AIHints.Enemy.PriorityForbidden;
        if (actor.FindStatus(SID.DeepFreeze) != null || actor.FindStatus(SID.DeepFreezeLegacy) != null)
            return;
        if (soul?.OID is (uint)OID.ShiveringSoul or (uint)OID.SmolderingSoul && LeaveWater(actor) is { } exit)
            hints.AddForbiddenZone(new SDInvertedCircle(exit, 0.5f));
        else if (soul?.OID == (uint)OID.ShudderingSoul)
        {
            var group = SyrcusAlliance.Group(Raid, slot);
            if (group >= 0)
                hints.AddForbiddenZone(new SDInvertedCircle(Pillars[group], PillarArrivalRadius));
        }
        else if (soul?.OID == (uint)OID.SmolderingSoul && Frozen(actor) is { } frozen)
            // Required interception from the tether cue; optional uptime goals can
            // be suppressed by casting and must not delay thawing the player.
            hints.AddForbiddenZone(new SDInvertedCircle(frozen.Position, 1));
        // Carrying fire/ice into water destroys shelter or freezes everyone in it. Never impose
        // contradictory orb and shelter constraints; the requirement starts when that orb resolves.
        if (_flare != default && soul == null)
        {
            var safe = Pools().Select(p => (ShapeDistance)new SDInvertedCircle(p.Position, WaterArrivalRadius)).ToList();
            if (safe.Count != 0)
                hints.AddForbiddenZone(new SDIntersection([.. safe]), _flare);
        }
    }
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (pc.FindStatus(SID.DeepFreeze) != null || pc.FindStatus(SID.DeepFreezeLegacy) != null)
            return;
        if (Soul(pc)?.OID == (uint)OID.ShudderingSoul && SyrcusAlliance.Group(Raid, pcSlot) is var group && group >= 0)
            Arena.AddCircleUnfilled(Pillars[group], 3.375f, Colors.Safe);
        if (Soul(pc)?.OID is (uint)OID.ShiveringSoul or (uint)OID.SmolderingSoul && LeaveWater(pc) is { } exit)
        {
            Arena.AddCircleUnfilled(exit, 0.5f, Colors.Safe);
            Arena.AddLine(pc.Position, exit, Colors.Safe);
        }
        else if (Soul(pc)?.OID == (uint)OID.SmolderingSoul && Frozen(pc) is { } frozen)
        {
            Arena.AddCircleUnfilled(frozen.Position, 1, Colors.Safe);
            Arena.AddLine(pc.Position, frozen.Position, Colors.Safe);
        }
        if (_flare != default)
            foreach (var pool in Pools())
                Arena.AddCircleUnfilled(pool.Position, WaterArrivalRadius, Colors.Safe);
    }
}
