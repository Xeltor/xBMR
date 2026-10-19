namespace BossMod.RealmReborn.Alliance.A3HowlingAtomos;

class BlackHole(BossModule module) : Components.GenericAOEs(module, warningText: "Leave the marked player's pull-in cone!")
{
    private sealed record Pull(Actor Source, Actor Target, WDir DropDirection, DateTime Deadline);
    private readonly Dictionary<Actor, Pull> _pulls = [];
    private readonly Dictionary<Actor, DateTime> _captured = [];
    private readonly List<AOEInstance> _aoes = [];
    private static readonly AOEShapeCone Cone = new(19, 45.Degrees());
    public bool Marked(Actor actor) => _pulls.Values.Any(p => p.Target == actor);
    public bool Captured(Actor actor) => _captured.ContainsKey(actor) || actor.FindStatus(421) != null;
    public WPos? Landing(Actor actor) => actor.PosRot.Y >= 93 && _pulls.Values.FirstOrDefault(p => p.Target == actor) is { } p
        ? A3HowlingAtomos.PlatformCenters[A3HowlingAtomos.Group(p.Source)] + 20.5f * p.DropDirection : null;
    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID != 1 || actor.PosRot.Y < 93)
            return;
        var source = Module.Enemies((uint)OID.Boss).Where(b => !b.IsDeadOrDestroyed && A3HowlingAtomos.Group(b) == A3HowlingAtomos.Group(actor))
            .MinBy(b => (b.Position - actor.Position).LengthSq());
        if (source == null)
            return;
        var dir = actor.Position - A3HowlingAtomos.PlatformCenters[A3HowlingAtomos.Group(source)];
        _pulls[source] = new(source, actor, dir.LengthSq() > 0.01f ? dir.Normalized() : new(0, 1), WorldState.FutureTime(4.5));
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 3414)
        {
            _pulls.Remove(caster);
            foreach (var target in spell.Targets)
                if (WorldState.Actors.Find(target.ID) is { } victim)
                    _captured[victim] = WorldState.FutureTime(15);
        }
        else if (spell.Action.ID == 3438)
            foreach (var target in spell.Targets)
                if (WorldState.Actors.Find(target.ID) is { } victim)
                    _captured.Remove(victim);
    }
    public override void OnActorDestroyed(Actor actor)
    {
        _pulls.Remove(actor);
        _captured.Remove(actor);
        foreach (var source in _pulls.Where(p => p.Value.Target == actor).Select(p => p.Key).ToArray())
            _pulls.Remove(source);
    }
    public override void Update()
    {
        foreach (var actor in _captured.Where(p => p.Key.IsDeadOrDestroyed || WorldState.CurrentTime > p.Value).Select(p => p.Key).ToArray())
            _captured.Remove(actor);
        foreach (var source in _pulls.Where(p => p.Key.IsDeadOrDestroyed || p.Value.Target.IsDeadOrDestroyed
            || WorldState.CurrentTime > p.Value.Deadline.AddSeconds(3)).Select(p => p.Key).ToArray())
            _pulls.Remove(source);
    }
    public override void OnStatusLose(Actor actor, ref ActorStatus status)
    {
        if (status.ID == 421)
            _captured.Remove(actor);
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.Clear();
        foreach (var p in _pulls.Values)
            if (p.Target != actor && Module.ActorsMatchArenaProjectionLayer(actor, p.Source))
                _aoes.Add(new(Cone, p.Source.Position, Angle.FromDirection(p.Target.Position - p.Source.Position), p.Deadline,
                    actorID: p.Source.InstanceID, arenaProjectionLayer: Module.ResolveArenaProjectionLayer(p.Source), restrictToArenaProjectionLayer: true));
        return CollectionsMarshal.AsSpan(_aoes);
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        base.AddHints(slot, actor, hints);
        if (Marked(actor))
            hints.Add(actor.PosRot.Y < 93 ? "Wait below until Black Hole resolves!" : "Black Hole on you: step off onto the lower ring now!");
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        foreach (var p in _pulls.Values)
            if (p.Target == actor && actor.PosRot.Y >= 93)
            {
                var center = A3HowlingAtomos.PlatformCenters[A3HowlingAtomos.Group(p.Source)];
                var landing = center + 20.5f * p.DropDirection;
                // A directed descent, never an upward route through the wall.
                // One second is a conservative transfer policy, not measured fall time.
                // Once below, stop the approach; waiting need not chase this exact landing.
                hints.Teleporters.Add(new(center + 17.2f * p.DropDirection, landing, 0.6f, false, 1));
                hints.AddForbiddenZone(new SDInvertedCircle(landing, 1), p.Deadline, p.Source.InstanceID);
            }
    }
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (Landing(pc) is { } landing)
        {
            using (Arena.WorldProjectionLayer(A3HowlingAtomos.Group(pc) + 3, true))
                Arena.AddCircleUnfilled(landing, 1, Colors.Safe, 2);
            Arena.AddLine(pc.Position, landing, Colors.Safe, 2);
        }
    }
}
