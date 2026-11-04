namespace BossMod.Stormblood.Alliance.A11Mateus;

class AzureGuards(BossModule module) : Components.RaidwideInstant(module, (uint)AID.Frostwave, hint: "Frostwave incoming. Group up for shields and healing.")
{
    private const float AnchorTolerance = 2f;
    private readonly Dictionary<Actor, WPos> _spawns = [];
    private bool _sawGuardWave;

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.AzureGuard && _spawns.TryAdd(actor, actor.Position))
            _sawGuardWave |= _spawns.Keys.Count(guard => !guard.IsDeadOrDestroyed) >= 3;
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (_spawns.Remove(actor) && !actor.IsDead)
            _sawGuardWave = false;
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor.OID == (uint)OID.Boss)
        {
            Clear();
        }
        else if (_sawGuardWave && _spawns.ContainsKey(actor) && _spawns.Keys.All(guard => guard == actor || guard.IsDeadOrDestroyed))
        {
            _sawGuardWave = false;
            // The last kill triggers the short ultimate transition. This is an
            // imminent mitigation cue, without a measured effect deadline.
            Activation = WorldState.FutureTime(1d);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (spell.Action.ID == WatchedAction)
            Clear();
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.Unbind)
            Clear();
    }

    public override void Update()
    {
        if (Activation != default && WorldState.CurrentTime > Activation.AddSeconds(5d))
            Clear();
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_sawGuardWave && _spawns.Keys.Count(IsActive) == 1 && _spawns.Keys.Count(guard => !guard.IsDeadOrDestroyed) == 1)
            hints.Add("Last Azure Guard. Prepare for Frostwave after it falls!", false);
        else if (_spawns.Keys.Any(IsActive))
            hints.Add("Defeat the Azure Guards before the gauge fills. Keep them apart!", false);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (pc.Role != Role.Tank)
            return;
        foreach (var (guard, spawn) in _spawns)
            if (IsActive(guard) && guard.TargetID == pc.InstanceID && !guard.Position.InCircle(spawn, AnchorTolerance))
                Arena.ZoneCircleOutline(spawn, AnchorTolerance, Colors.Object);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (Activation != default)
            hints.GoalZones.Add(AIHints.GoalProximity(Arena.Center, 40f, 12f));

        // Respect established tank ownership and split unclaimed adds by alliance.
        var group = Raid.AllianceGroupForSlot(slot);
        var own = hints.PotentialTargets
            .Where(enemy => _spawns.ContainsKey(enemy.Actor) && A11Mateus.CanEngagePhaseAdd(enemy, Arena))
            .OrderBy(enemy => AssignmentPriority(enemy.Actor, actor, slot))
            .ThenBy(enemy => group < 0 || A11Mateus.AllianceSector(Arena.Center, _spawns[enemy.Actor]) == group ? 0 : 1)
            .ThenBy(enemy => (enemy.Actor.Position - actor.Position).LengthSq())
            .FirstOrDefault();
        if (own == null)
            return;

        foreach (var enemy in hints.PotentialTargets)
        {
            if (!_spawns.TryGetValue(enemy.Actor, out var spawn) || !A11Mateus.CanEngagePhaseAdd(enemy, Arena))
                continue;

            enemy.Priority = enemy == own ? 6 : 3;
            if (enemy == own && AssignmentPriority(enemy.Actor, actor, slot) < 3)
            {
                enemy.ShouldBeTanked = true;
                enemy.DesiredPosition = spawn;
            }
        }

        var anchor = _spawns[own.Actor];
        if (actor.Role == Role.Tank && own.CanMove && own.Actor.TargetID == actor.InstanceID && !own.Actor.Position.InCircle(anchor, AnchorTolerance))
        {
            var lead = own.TankDistance + own.Actor.HitboxRadius + actor.HitboxRadius;
            var tankPosition = Arena.ClampToBounds(anchor + (anchor - own.Actor.Position).Normalized() * lead);
            hints.GoalZones.Add(AIHints.GoalProximity(tankPosition, 6f, 10f));
        }
        else
        {
            var range = actor.Role is Role.Tank or Role.Melee ? 3f : 24.5f;
            hints.GoalZones.Add(AIHints.GoalSingleTarget(own.Actor.Position, own.Actor.HitboxRadius + range, 5f));
        }
    }

    private void Clear()
    {
        _spawns.Clear();
        _sawGuardWave = false;
        Activation = default;
    }

    private int AssignmentPriority(Actor add, Actor actor, int slot)
    {
        if (add.TargetID == actor.InstanceID)
            return 0;
        if (WorldState.Actors.Find(add.TargetID) is not { Role: Role.Tank, IsDeadOrDestroyed: false } tank || !Arena.InBounds(tank.Position))
            return 2;

        var tankSlot = Raid.FindSlot(tank.InstanceID);
        return tankSlot is >= 0 and < PartyState.MaxAllianceSize && tankSlot / PartyState.MaxPartySize == slot / PartyState.MaxPartySize ? 1 : 3;
    }

    private static bool IsActive(Actor actor) => actor.IsTargetable && !actor.IsDeadOrDestroyed;
}
