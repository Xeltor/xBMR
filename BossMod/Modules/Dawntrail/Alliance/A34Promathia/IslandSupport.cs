namespace BossMod.Dawntrail.Alliance.A34Promathia;

sealed class IslandSupport(BossModule module) : BossComponent(module)
{
    // Boss3_Gimmick_03 EventRange/PopRange pairs in z6r3/level/planmap.lgb.
    // The two map effects on each island arrive together; require both rather
    // than guessing which individual effect identifies which of its two pads.
    private static readonly byte[][] PadEffects = [[0x5F, 0x60], [0x0F, 0x5E], [0x0D, 0x0E]];
    private static readonly WPos[,] Pads =
    {
        { default, new(-816f, -812f), new(-824f, -812f) },
        { new(-811.0718f, -820.5359f), default, new(-815.0718f, -827.4641f) },
        { new(-828.9281f, -820.5359f), new(-824.9281f, -827.4641f), default }
    };
    private static readonly AOEShapeRect Platform = new(6.5f, 6.5f, 6.5f);
    private readonly List<Actor> _receptacles = module.Enemies((uint)OID.MemoryReceptacle);
    private readonly HashSet<byte> _activePads = [];
    private readonly Dictionary<ulong, (Actor Player, int Island, Actor Target)> _assignments = [];
    private bool _islands;

    private static int IslandAt(WPos position)
    {
        for (var i = 0; i < A34Promathia.Platforms.Length; ++i)
            if (Platform.Check(position, A34Promathia.Platforms[i].Center, A34Promathia.Platforms[i].Rotation))
                return i;
        return -1;
    }

    private bool PadsEnabled(int island) => PadEffects[island].All(_activePads.Contains);
    private Actor? LiveReceptacle(int island) => _receptacles.Find(a => !a.IsDeadOrDestroyed && a.IsTargetable && IslandAt(a.Position) == island);

    private (Actor Target, WPos Entrance)? SupportTarget(Actor player)
    {
        var island = IslandAt(player.Position);
        if (!_islands || island < 0 || player.IsDeadOrDestroyed || !PadsEnabled(island)
            || LiveReceptacle(island) != null || player.FindStatus((uint)SID.SystemLock) != null)
            return null;

        if (_assignments.TryGetValue(player.InstanceID, out var previous) && ReferenceEquals(previous.Player, player)
            && previous.Island == island && !previous.Target.IsDeadOrDestroyed && previous.Target.IsTargetable
            && IslandAt(previous.Target.Position) is var destination && destination >= 0 && destination != island)
            return (previous.Target, Pads[island, destination]);

        Actor? target = null;
        var entrance = default(WPos);
        var distance = float.MaxValue;
        for (var i = 0; i < 3; ++i)
        {
            if (i == island || LiveReceptacle(i) is not { } add)
                continue;
            var pad = Pads[island, i];
            var approach = (pad - player.Position).LengthSq();
            if (approach < distance)
            {
                target = add;
                entrance = pad;
                distance = approach;
            }
        }
        if (target == null)
        {
            _assignments.Remove(player.InstanceID);
            return null;
        }
        _assignments[player.InstanceID] = (player, island, target);
        return (target, entrance);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (!_islands || actor.IsDeadOrDestroyed || !_receptacles.Any(a => !a.IsDeadOrDestroyed && a.IsTargetable))
            return;

        // Lock is observed as roughly four seconds in this recording. Use the
        // actual status presence, not an assumed three-second countdown.
        var island = IslandAt(actor.Position);
        var support = SupportTarget(actor);
        var waitingForTransfer = support is { } selected && actor.Position.InCircle(selected.Entrance, 1f);
        // Offer only one hop from the current cleared island to a live add.
        // A graph of every enabled pad could invent a second hop before the
        // System Lock from the first transfer has actually been received.
        if (!waitingForTransfer && island >= 0 && PadsEnabled(island) && LiveReceptacle(island) == null && actor.FindStatus((uint)SID.SystemLock) == null)
            for (var destination = 0; destination < 3; ++destination)
                if (island != destination && LiveReceptacle(destination) != null)
                    hints.Teleporters.Add(new(Pads[island, destination], Pads[destination, island], 1f, false, 0.5f));

        if (island >= 0 && LiveReceptacle(island) is { } local)
            hints.SetPriority(local, 1);
        else if (support is { } route)
        {
            hints.SetPriority(route.Target, 1);
            hints.ForbidDashes = true;
            // A preference on the destination island lets native pathfinding
            // choose the pad approach and respect hazards on both ends.
            // Once on the pad, wait for the real transfer; an entrance-to-exit
            // graph edge must not become a direct movement request over the void.
            hints.GoalZones.Add(waitingForTransfer ? AIHints.GoalSingleTarget(route.Entrance, 0.5f)
                : AIHints.GoalSingleTarget(route.Target, 3f));
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (SupportTarget(actor) != null)
            hints.Add("Use teleporter to support another island!", false);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (SupportTarget(pc) is { } support)
            Arena.ZoneCircleOutline(support.Entrance, 1f, Colors.Safe);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.FalseGenesis)
            Reset();
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.FalseGenesis)
            _islands = true;
    }

    public override void OnMapEffect(byte index, uint state)
    {
        if (index == 0x0C)
        {
            if (state == 0x00020001u)
                _islands = true;
            else if (state == 0x00080004u)
                Reset();
        }
        else if (PadEffects.Any(pair => pair.Contains(index)))
        {
            if (_islands && state == 0x00020001u)
                _activePads.Add(index);
            else if (state == 0x00080004u)
                _activePads.Remove(index);
        }
    }

    private void Reset()
    {
        _islands = false;
        _activePads.Clear();
        _assignments.Clear();
    }
}
