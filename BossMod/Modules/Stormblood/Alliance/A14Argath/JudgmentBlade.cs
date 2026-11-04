namespace BossMod.Stormblood.Alliance.A14Argath;

class JudgmentBlade(BossModule module) : BossComponent(module)
{
    private readonly Dictionary<ulong, DateTime> _marked = [];

    private bool IsMarked(Actor actor) => !actor.IsDeadOrDestroyed && _marked.TryGetValue(actor.InstanceID, out var expiry) && WorldState.CurrentTime < expiry;

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID == (uint)IconID.Icon125)
            _marked[actor.InstanceID] = WorldState.FutureTime(8.5d);
    }

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.Heartless)
            _marked.Clear();
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            _marked.Clear();
        else
            _marked.Remove(actor.InstanceID);
    }

    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (IsMarked(pc))
            Arena.ZoneCircleOutline(Arena.Center, 3f, Colors.Object);
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (IsMarked(actor))
            hints.Add("Bait the sword cross near the middle!");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (IsMarked(actor))
            hints.GoalZones.Add(AIHints.GoalProximity(Arena.Center, 48f, 16f));
    }
}

class BleedingTiles(BossModule module) : Components.GenericAOEs(module, warningText: "Move off the bleeding floor square!")
{
    // The matching EventRange boxes use 5.5-yalm half sides. Allow a small margin at the edge.
    private static readonly AOEShapeRect Shape = new(5.75f, 5.75f, 5.75f);
    private readonly List<AOEInstance> _tiles = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_tiles);

    public override void OnActorCreated(Actor actor)
    {
        if (IsTile(actor) && !_tiles.Any(tile => tile.ActorID == actor.InstanceID))
        {
            // In the recorded pattern the tiles start their active animation 3.23s after spawning.
            // A tile seen after the cast may already be live, including when the module attaches late.
            var delay = Module.PrimaryActor.CastInfo?.Action.ID == (uint)AID.JudgmentBlade ? 2.8d : 0d;
            _tiles.Add(new(Shape, actor.Position, activation: WorldState.FutureTime(delay),
                color: delay == 0d ? Colors.Danger : default, actorID: actor.InstanceID));
        }
    }

    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (!IsTile(actor))
            return;

        if (state == 0x00040008)
        {
            OnActorCreated(actor);
            var index = _tiles.FindIndex(tile => tile.ActorID == actor.InstanceID);
            var tile = _tiles[index];
            tile.Activation = WorldState.CurrentTime;
            tile.Color = Colors.Danger;
            _tiles[index] = tile;
        }
        else if (state == 0x00010040)
        {
            OnActorDestroyed(actor);
        }
    }

    public override void OnActorDestroyed(Actor actor) => _tiles.RemoveAll(tile => tile.ActorID == actor.InstanceID);

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.JudgmentBlade && !spell.EventHappened)
            _tiles.Clear();
    }

    public override void Update() => _tiles.RemoveAll(tile => WorldState.CurrentTime > tile.Activation.AddSeconds(18d));

    private bool IsTile(Actor actor)
    {
        if (actor.OID != (uint)OID.BleedingTile)
            return false;
        var offset = actor.Position - Arena.Center;
        var x = MathF.Abs(offset.X);
        var z = MathF.Abs(offset.Z);
        return (x < 0.2f || MathF.Abs(x - 16f) < 0.2f)
            && (z < 0.2f || MathF.Abs(z - 16f) < 0.2f)
            && Math.Max(x, z) > 15.8f;
    }
}

class Heartless(BossModule module) : Components.GenericAOEs(module, (uint)AID.Heartless)
{
    private static readonly AOEShapeRect Shape = new(61f, 5.5f);
    private readonly List<AOEInstance> _rays = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_rays);

    internal bool ResolvesBefore(DateTime activation) => _rays.Any(ray => ray.Activation < activation.AddSeconds(-0.25d));

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.Heartless && actor.CastInfo == null && !_rays.Any(ray => ray.ActorID == actor.InstanceID))
        {
            // The helper steps backward by ~5.15 units before its visible cast.
            var origin = actor.Position - 5.15f * actor.Rotation.ToDirection();
            _rays.Add(new(Shape, origin, actor.Rotation, WorldState.FutureTime(6.5d), actorID: actor.InstanceID));
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != WatchedAction)
            return;

        var ray = new AOEInstance(Shape, spell.LocXZ, spell.Rotation, Module.CastFinishAt(spell), Colors.Danger, actorID: caster.InstanceID)
        {
            SourceCast = new(spell)
        };
        var index = _rays.FindIndex(a => a.ActorID == caster.InstanceID);
        if (index >= 0)
            _rays[index] = ray;
        else
            _rays.Add(ray);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction)
            _rays.RemoveAll(ray => ray.ActorID == caster.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (spell.IsSpell(AID.Heartless))
            _rays.RemoveAll(ray => ray.ActorID == caster.InstanceID);
    }

    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            _rays.Clear();
        else
            _rays.RemoveAll(ray => ray.ActorID == actor.InstanceID);
    }

    public override void Update() => _rays.RemoveAll(ray => ray.Activation.AddSeconds(1d) < WorldState.CurrentTime);
}
