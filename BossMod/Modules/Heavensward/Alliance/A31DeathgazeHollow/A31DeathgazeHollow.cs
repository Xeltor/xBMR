namespace BossMod.Heavensward.Alliance.A31DeathgazeHollow;

class DarkII(BossModule module) : Components.SimpleAOEs(module, (uint)AID.DarkII, new AOEShapeCone(50, 30.Degrees()));
class BoltOfDarkness3(BossModule module) : Components.SimpleAOEs(module, (uint)AID.BoltOfDarkness3, new AOEShapeRect(31.5f, 10));
class VoidDeath(BossModule module) : Components.SimpleAOEs(module, (uint)AID.VoidDeath, 10);
class VoidAeroIIIAOE(BossModule module) : Components.SimpleAOEs(module, (uint)AID.VoidAeroIIIAOE, 10);
class BitingWind(BossModule module) : Components.Voidzone(module, 4, m => m.Enemies((uint)OID.FierceWind).Where(a => !a.IsDead && !a.IsDestroyed));
class Doom(BossModule module) : Components.CleansableDebuff(module, (uint)SID.Doom);

// Each alliance has one target; keep actor identities instead of a local-party mask.
class VoidAeroII(BossModule module) : Components.UniformStackSpread(module, 0, 5, raidwideOnResolve: false)
{
    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID == (uint)IconID.WindSpread && !Spreads.Any(s => s.Target.InstanceID == actor.InstanceID))
            AddSpread(actor, WorldState.FutureTime(5.5)); // observed hit at 5.94-5.99s; retain a conservative response margin
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.VoidAeroII)
            Spreads.RemoveAll(s => s.Target.InstanceID == spell.MainTargetID);
    }

    public override void Update()
    {
        Spreads.RemoveAll(s => s.Target.IsDead || s.Target.IsDestroyed || WorldState.Actors.Find(s.Target.InstanceID) != s.Target
            || WorldState.CurrentTime > s.Activation.AddSeconds(2)); // bounded fallback for cancelled/missing helper resolution
    }
}

class VoidBlizzardIIIAOE(BossModule module) : Components.SimpleAOEs(module, (uint)AID.VoidBlizzardIIIAOE, new AOEShapeCone(40.5f, 10f.Degrees()));

// Both pull actions have no cast bar. The lethal centre helper supplies their early cue.
// Timings are observed predictions; neither action delivery nor cast finish establishes a server snapshot.
class VoidDeathPull(BossModule module) : Components.GenericKnockback(module)
{
    private readonly List<Knockback> _pulls = [];
    private ulong _deathCaster;
    private WPos _origin;
    private DateTime _secondPull;

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor) => CollectionsMarshal.AsSpan(_pulls);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.VoidDeath)
            return;
        _deathCaster = caster.InstanceID;
        _origin = spell.LocXZ;
        var finish = Module.CastFinishAt(spell);
        _secondPull = finish.AddSeconds(-3);
        _pulls.Clear();
        var minimum = caster.HitboxRadius + (Raid.Player()?.HitboxRadius ?? 0.5f);
        if (finish.AddSeconds(-8) > WorldState.CurrentTime)
            _pulls.Add(new(_origin, 40, finish.AddSeconds(-8), kind: Kind.TowardsOrigin, minDistance: minimum));
        if (_secondPull > WorldState.CurrentTime)
            _pulls.Add(new(_origin, 15, _secondPull, kind: Kind.TowardsOrigin, minDistance: minimum));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.VoidDeathKB)
            _pulls.RemoveAll(p => p.Distance == 40);
        else if (spell.Action.ID == (uint)AID.VoidDeathKB2)
        {
            _pulls.Clear();
            _secondPull = default;
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.VoidDeath && caster.InstanceID == _deathCaster)
        {
            _pulls.Clear();
            _secondPull = default;
            _deathCaster = 0;
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.InstanceID == _deathCaster)
        {
            _pulls.Clear();
            _secondPull = default;
            _deathCaster = 0;
        }
    }

    // The first pull is recoverable. The second must leave the player outside the lethal 10y centre.
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_secondPull != default && !IsImmune(slot, _secondPull))
            hints.Add("Keep running away from the centre!", actor.Position.InCircle(_origin, 25));
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_secondPull != default && !IsImmune(slot, _secondPull))
            hints.AddForbiddenZone(new SDCircle(_origin, 25), _secondPull.AddSeconds(-0.3));
    }
}

// The actual helper faces in the knockback direction. Boss visuals finish 0.5s earlier.
// Ice faces come from installed collision assets, rather than the event objects' hitbox radii.
class VoidAeroIV(BossModule module) : Components.SimpleKnockbacks(module, (uint)AID.VoidAeroIV2, 37, kind: Kind.DirForward)
{
    private readonly IceBlocks? _ice = module.FindComponent<IceBlocks>();
    private readonly List<Knockback> _withIce = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction)
            Casters.RemoveAll(s => s.ActorID == caster.InstanceID);
        base.OnCastStarted(caster, spell);
    }

    public override void OnActorDestroyed(Actor actor) => Casters.RemoveAll(s => s.ActorID == actor.InstanceID);

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        _withIce.Clear();
        var walls = _ice?.Walls() ?? [];
        foreach (var source in Casters)
        {
            var direction = source.Direction.ToDirection();
            var stoppingWalls = walls.Any(w => Intersect.RaySegment(actor.Position, direction, w.Vertex1, w.Vertex2) <= source.Distance) ? walls : [];
            _withIce.Add(new(source.Origin, source.Distance, source.Activation, source.Shape, source.Direction, source.Kind,
                source.MinDistance, stoppingWalls, source.ActorID, source.IgnoreImmunes, source.ArenaProjectionLayer, source.RestrictToArenaProjectionLayer));
        }
        return CollectionsMarshal.AsSpan(_withIce);
    }

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos)
        => base.DestinationUnsafe(slot, actor, pos) || Module.Enemies((uint)OID.FierceWind).Any(w => !w.IsDeadOrDestroyed && pos.InCircle(w.Position, 4));

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Retain a player-radius lateral margin at each block corner; this is a response margin, not a collider measurement.
        var walls = _ice?.Walls(actor.HitboxRadius) ?? [];
        if (walls.Length == 0)
            return; // missing animation evidence must not make the entire arena forbidden
        foreach (var source in Casters)
            if (!IsImmune(slot, source.Activation))
                hints.AddForbiddenZone(new IceLanding(this, slot, actor, source.Direction.ToDirection(), walls, source.Distance), source.Activation.AddSeconds(-0.3));
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Casters.Count != 0 && !IsImmune(slot, Casters[0].Activation))
        {
            if ((_ice?.Walls().Length ?? 0) != 0)
                base.AddHints(slot, actor, hints);
            hints.Add("Line up with an ice block to stop the knockback!", false);
        }
    }

    private sealed class IceLanding(VoidAeroIV component, int slot, Actor actor, WDir direction, SafeWall[] walls, float distance) : ShapeDistance
    {
        public override bool Contains(in WPos p)
        {
            var intersection = float.MaxValue;
            foreach (var wall in walls)
                intersection = Math.Min(intersection, Intersect.RaySegment(p, direction, wall.Vertex1, wall.Vertex2));
            return intersection > distance || component.DestinationUnsafe(slot, actor, p + Math.Max(0, intersection - 0.01f) * direction);
        }
        public override float Distance(in WPos p) => Contains(p) ? 0 : 1;
    }
}

class VoidDeathIV(BossModule module) : Components.CastHints(module, [(uint)AID.VoidDeathIV1, (uint)AID.VoidDeathIV2], "Leave the flashing dark floor squares!");

[ModuleInfo(BossModuleInfo.Maturity.Verified, Contributors = "The Combat Reborn Team", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 220, NameID = 5507)]
public class A31DeathgazeHollow(WorldState ws, Actor primary) : BossModule(ws, primary, ArenaGeometry.Deck.Center, ArenaGeometry.Deck)
{
    protected override void CalculateModuleAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Remove the adds before their cones constrain the ship's knockback routes.
        foreach (var enemy in hints.PotentialTargets)
            if (enemy.Priority >= 0 && enemy.Actor.OID == (uint)OID.VoidSprite
                && enemy.Actor.IsTargetable && !enemy.Actor.IsDeadOrDestroyed)
                enemy.Priority = Math.Max(enemy.Priority, 2);
    }

    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.VoidSprite));
    }
}
