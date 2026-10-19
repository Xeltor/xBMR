namespace BossMod.RealmReborn.Alliance.A3HowlingAtomos;

class JumpPads(BossModule module) : BossComponent(module)
{
    private readonly HashSet<Actor> _lit = [];
    public static bool IsUpper(Actor actor) => actor.OID is >= 2004792 and <= 2004803;
    // Local VFX placements in the floor SGBs, corroborated by terrain pad protrusions.
    // Arrival radius below is a response policy, not a measured launch trigger radius.
    public static WPos[] LowerPads(int group) => Enumerable.Range(0, 4).Select(i => A3HowlingAtomos.PlatformCenters[group]
        + new WDir(0, 25.657f).Rotate((i * 90 + (group == 0 ? 30 : group == 2 ? -30 : 0)).Degrees())).ToArray();
    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (!IsUpper(actor))
            return;
        if (state == 0x00800100)
            _lit.Add(actor);
        else if (state == 0x00040008)
            _lit.Remove(actor);
    }
    public override void OnActorDestroyed(Actor actor) => _lit.Remove(actor);
    private bool Blocked(int slot, Actor actor, WPos position)
    {
        foreach (var c in Module.Components.OfType<Components.GenericAOEs>())
            foreach (ref readonly var aoe in c.ActiveAOEs(slot, actor))
                if (aoe.Risky && aoe.Check(position))
                    return true;
        foreach (var c in Module.Components.OfType<Components.GenericBaitAway>())
            foreach (var bait in c.CurrentBaits)
                if (bait.Target != actor && Module.ActorMatchesArenaProjectionLayer(actor, bait.ResolveArenaProjectionLayer(Module), true)
                    && bait.Shape.Check(position, c.CenterAtTarget ? bait.Target.Position : bait.Source.Position, bait.Rotation))
                    return true;
        return false;
    }
    private WPos? Destination(int slot, Actor actor)
    {
        if (Module.FindComponent<BlackHole>() is { } pull && (pull.Marked(actor) || pull.Captured(actor)))
            return null;
        var group = A3HowlingAtomos.Group(actor);
        if (actor.PosRot.Y < 93)
        {
            var pads = LowerPads(group).Where(p => !Blocked(slot, actor, p)).ToArray();
            return pads.Length != 0 ? pads.MinBy(p => (p - actor.Position).LengthSq()) : null;
        }
        if (Module.Enemies((uint)OID.Boss).Any(b => A3HowlingAtomos.Group(b) == group && b.CastInfo?.Action.ID is 3415 or 3416))
            return null;
        foreach (var pad in _lit.Where(p => !p.IsDeadOrDestroyed && A3HowlingAtomos.Group(p) == group).OrderBy(p => (p.Position - actor.Position).LengthSq()))
        {
            if (Blocked(slot, actor, pad.Position))
                continue;
            var chosen = Raid.WithoutSlot().Where(p => p.Type == ActorType.Player && !p.IsDeadOrDestroyed && p.PosRot.Y >= 93
                    && A3HowlingAtomos.Group(p) == group && Module.FindComponent<BlackHole>()?.Marked(p) != true
                    && Module.FindComponent<BlackHole>()?.Captured(p) != true && !Module.Enemies((uint)OID.Boss).Any(b => b.TargetID == p.InstanceID))
                .Where(p => !Module.Enemies((uint)OID.SacrificedSoldier).Any(s => !s.IsDeadOrDestroyed && s.TargetID == p.InstanceID))
                .OrderBy(p => (p.Position - pad.Position).LengthSq()).ThenBy(p => p.InstanceID).FirstOrDefault();
            if (chosen == actor)
                return pad.Position;
        }
        return null;
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (actor.PosRot.Y < 93 && Destination(slot, actor) != null)
            hints.Add("Wait on a return pad; another alliance activates it!", false);
        else if (Destination(slot, actor) != null)
            hints.Add("Stand on the lit upper pad to enable another alliance's return!", false);
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // The shared grid cannot represent vertical walls. Preserve that partition;
        // only Black Hole's explicitly directed descent may cross it.
        var center = A3HowlingAtomos.PlatformCenters[A3HowlingAtomos.Group(actor)];
        hints.TemporaryObstacles.Add(new AOEShapeDonut(17.8f, 19.4f).Distance(center, default));
        if (Destination(slot, actor) is { } pad)
            hints.AddForbiddenZone(new SDInvertedCircle(pad, 1), WorldState.CurrentTime, arenaProjectionLayer: Module.ResolveArenaProjectionLayer(actor));
    }
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        var group = A3HowlingAtomos.Group(pc);
        var lower = pc.PosRot.Y < 93;
        using (Arena.WorldProjectionLayer(group + (lower ? 3 : 0), true))
        {
            if (lower)
                foreach (var pad in LowerPads(group))
                    Arena.AddCircleUnfilled(pad, 2.6f, Colors.Object);
            else
                foreach (var pad in WorldState.Actors.Where(p => IsUpper(p) && !p.IsDeadOrDestroyed && A3HowlingAtomos.Group(p) == group))
                    Arena.AddCircleUnfilled(pad.Position, 1, _lit.Contains(pad) ? Colors.Safe : Colors.Object);
            if (Destination(pcSlot, pc) is { } destination)
                Arena.AddCircleUnfilled(destination, 1, Colors.Safe, 3);
        }
    }
}
