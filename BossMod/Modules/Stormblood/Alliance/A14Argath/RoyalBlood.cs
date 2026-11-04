namespace BossMod.Stormblood.Alliance.A14Argath;

class RoyalBlood(BossModule module) : Components.CastHint(module, (uint)AID.RoyalBlood, "Gather the Shades and destroy them before they explode!")
{
    private readonly Dictionary<Actor, ulong> _assignedPlayers = [];
    private DateTime _expectSpawnsUntil;

    private bool Preparing
    {
        get
        {
            if (Module.PrimaryActor?.IsDeadOrDestroyed == true)
                return false;
            if (Active || _expectSpawnsUntil > WorldState.CurrentTime)
                return true;
            var shades = Module.Enemies((uint)OID.Shade);
            return shades.Any(shade => !shade.IsDeadOrDestroyed && !shade.IsTargetable)
                && !shades.Any(shade => !shade.IsDeadOrDestroyed && shade.IsTargetable);
        }
    }

    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (source.OID == (uint)OID.Shade && !source.IsDeadOrDestroyed && tether.ID == (uint)TetherID.Tether17 && tether.Target != 0)
            _assignedPlayers[source] = tether.Target;
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (caster == Module.PrimaryActor && spell.IsSpell(AID.RoyalBlood))
            _expectSpawnsUntil = WorldState.FutureTime(1d);
        if (caster.OID == (uint)OID.Shade && !caster.IsDeadOrDestroyed && spell.IsSpell(AID.AutoAttack)
            && spell.Targets.Count == 1 && WorldState.Actors.Find(spell.Targets[0].ID) is { Type: ActorType.Player } victim)
            _assignedPlayers[caster] = victim.InstanceID;
    }

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.Shade)
            _expectSpawnsUntil = default;
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor == Module.PrimaryActor)
        {
            Casters.Clear();
            _assignedPlayers.Clear();
            _expectSpawnsUntil = default;
        }
        else
        {
            _assignedPlayers.Remove(actor);
        }
    }

    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);

    private bool Follows(Actor shade, Actor actor) => Module.PrimaryActor?.IsDeadOrDestroyed != true && !shade.IsDeadOrDestroyed && !actor.IsDeadOrDestroyed
        && (shade.Tether.ID == (uint)TetherID.Tether17
            ? shade.Tether.Target == actor.InstanceID
            : shade.TargetID == actor.InstanceID && _assignedPlayers.GetValueOrDefault(shade) == actor.InstanceID);

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Preparing)
            hints.Add(Hint);
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Module.Enemies((uint)OID.Shade).Any(shade => Follows(shade, actor)))
            hints.Add("Bring your Shade to the group!");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (actor.IsDeadOrDestroyed)
            return;
        if (Module.Enemies((uint)OID.Shade).Any(shade => Follows(shade, actor)))
            hints.GoalZones.Add(AIHints.GoalProximity(Arena.Center, 34f, 14f));
        else if (Preparing)
            hints.GoalZones.Add(AIHints.GoalProximity(Arena.Center, 34f, 6f));
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var shade in Module.Enemies((uint)OID.Shade))
            if (Follows(shade, pc))
            {
                Arena.Actor(shade, Colors.Object);
                Arena.AddLine(pc.Position, shade.Position, Colors.Object);
            }
    }
}

class EmptySoul(BossModule module) : Components.RaidwideCast(module, (uint)AID.DarkUltima, "Dark Ultima incoming. Group up for mitigation and healing.")
{
    private readonly HashSet<Actor> _shards = [];
    private bool _summoning;
    private bool _sawWave;
    private bool _waveInterrupted;
    private DateTime _expectedUltima;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster != Module.PrimaryActor)
            return;

        base.OnCastStarted(caster, spell);
        if (spell.Action.ID == (uint)AID.EmptySoul)
        {
            ResetWave();
            _summoning = true;
        }
        else if (spell.Action.ID == WatchedAction)
        {
            ResetWave();
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        base.OnCastFinished(caster, spell);
        if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.EmptySoul)
            _summoning = false;
    }

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.ShardOfEmptiness && !actor.IsDeadOrDestroyed && !Active && _shards.Add(actor))
            _sawWave |= _shards.Count >= 2;
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor == Module.PrimaryActor)
        {
            ResetWave();
            Casters.Clear();
        }
        else if (_shards.Remove(actor) && _shards.Count == 0 && _sawWave && !_waveInterrupted)
        {
            // The observed last death precedes the effect by 5.56s.
            _expectedUltima = WorldState.FutureTime(5.25d);
            _sawWave = false;
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            OnActorDeath(actor);
        else if (_shards.Remove(actor))
            _waveInterrupted = true;
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (caster == Module.PrimaryActor && spell.Action.ID == WatchedAction)
        {
            ResetWave();
            Casters.Remove(caster);
        }
    }

    public override void Update()
    {
        if (_expectedUltima != default && WorldState.CurrentTime > _expectedUltima.AddSeconds(2d))
            _expectedUltima = default;
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        base.AddGlobalHints(actor, hints);
        if (_expectedUltima != default)
            hints.Add(Hint);
        else if (_summoning || _shards.Count != 0)
            hints.Add("Destroy the Shards of Emptiness before the gauge fills!");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (_expectedUltima != default)
        {
            var affected = AffectedPlayers();
            if (affected != default)
                hints.AddPredictedDamage(affected, _expectedUltima);
        }
        if (Active || _expectedUltima != default)
            hints.GoalZones.Add(AIHints.GoalProximity(Arena.Center, 34f, 8f));
        else if (_shards.Count > 0 && !_shards.Any(shard => shard.IsTargetable))
        {
            var range = actor.Role is Role.Tank or Role.Melee ? 3f : 24.5f;
            var approaches = _shards.Where(shard => !shard.IsDeadOrDestroyed && Arena.InBounds(shard.Position))
                .Select(shard => (shard.Position, Range: shard.HitboxRadius + range)).ToArray();
            if (approaches.Length > 0)
                hints.GoalZones.Add(position => approaches.Any(approach => position.InCircle(approach.Position, approach.Range)) ? 6f : 0f);
        }
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var shard in _shards)
            if (!shard.IsDeadOrDestroyed && Arena.InBounds(shard.Position))
                Arena.Actor(shard, shard.IsTargetable ? Colors.Object : Colors.AOE);
    }

    private void ResetWave()
    {
        _shards.Clear();
        _summoning = _sawWave = _waveInterrupted = false;
        _expectedUltima = default;
    }
}

class LethalRim(BossModule module) : BossComponent(module)
{
    private bool _active;
    private DateTime _closeAt;

    public override void OnLegacyMapEffect(byte seq, byte param, byte[] data)
    {
        if (seq != 1 || param != 0 || data.Length < 12
            || !data.AsSpan(0, 9).SequenceEqual((ReadOnlySpan<byte>)[0, 1, 0, 8, 0, 0, 34, 2, 127]))
            return;

        if (data[10] == 2)
            ActivateRim();
        else if (data[9] == 1 && data[10] == 1 && data[11] == 1)
            Reset();
        else if (!_active && _closeAt == default && data[10] == 0 && data[11] == 0 && data[9] is 0 or 2 or 4 or 8)
            Arena.Bounds = A14Argath.FullPlatform;
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (!_active && caster == Module.PrimaryActor && spell.IsSpell(AID.DarkUltima))
            _closeAt = Module.CastFinishAt(spell, -0.25d);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.IsSpell(AID.DarkUltima))
            _closeAt = default;
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster == Module.PrimaryActor && spell.Action == ActionID.MakeSpell(AID.DarkUltima))
            ActivateRim();
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            Reset();
    }

    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (_closeAt != default && Arena.Bounds == A14Argath.FullPlatform)
        {
            const float halfWidth = A14Argath.InnerHalfWidth;
            Arena.AddRect(Arena.Center, new(0, 1), halfWidth, halfWidth, halfWidth, Colors.Safe);
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_closeAt != default && Arena.Bounds == A14Argath.FullPlatform)
        {
            const float halfWidth = A14Argath.InnerHalfWidth;
            hints.AddForbiddenZone(new SDInvertedRect(Arena.Center, default(Angle), halfWidth, halfWidth, halfWidth), _closeAt);
        }
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_active)
            hints.Add("The outer rim inflicts Transfiguration. Stay inside the marked arena.");
        else if (_closeAt != default)
            hints.Add("The outer rim will activate after Dark Ultima. Move inward.");
    }

    private void ActivateRim()
    {
        Arena.Bounds = A14Argath.InnerPlatform;
        _active = true;
        _closeAt = default;
    }

    private void Reset()
    {
        Arena.Bounds = A14Argath.InnerPlatform;
        _active = false;
        _closeAt = default;
    }
}
