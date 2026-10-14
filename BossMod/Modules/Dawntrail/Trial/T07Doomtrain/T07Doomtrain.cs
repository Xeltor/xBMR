namespace BossMod.Dawntrail.Trial.T07Doomtrain;

abstract class GroundElectray(BossModule module, uint aid, float length) : Components.SimpleAOEs(module, aid, new AOEShapeRect(length, 2.5f))
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        ArenaProjectionLayer = Arena.Bounds is ArenaBoundsCustom { WorldProjectionLayers.Length: > 0 } ? 0 : null;
        base.OnCastStarted(caster, spell);
    }
}
sealed class ElectrayShort(BossModule module) : GroundElectray(module, (uint)AID.ElectrayShort, 10f);
sealed class ElectrayMedium(BossModule module) : GroundElectray(module, (uint)AID.ElectrayMedium, 15f);
sealed class ElectrayLong(BossModule module) : GroundElectray(module, (uint)AID.ElectrayLong, 25f);
sealed class LightningBurstTankBuster(BossModule module) : Components.BaitAwayIcon(module, 5f, (uint)IconID.LightningBurstIcon, (uint)AID.LightningBurst, 5f, centerAtTarget: true, tankbuster: true, damageType: AIHints.PredictedDamageType.Tankbuster);

// For first pass the knockback distance is estimated.
sealed class LightningExpress(BossModule module) : Components.SimpleKnockbacks(module, (uint)AID.LightningExpress, 16f, kind: Kind.DirForward);

// Spawn gives a useful conservative warning before the one-second cast.
// The real cast replaces the estimate; upper portals never forbid ground space.
sealed class LevinSignal(BossModule module) : Components.GenericAOEs(module)
{
    private readonly CarGeometry _car = module.FindComponent<CarGeometry>()!;
    private readonly List<AOEInstance> _aoes = [];
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_aoes);

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID != (uint)OID.LevinSignal || _aoes.Any(a => a.ActorID == actor.InstanceID))
            return;
        var ground = actor.PosRot.Y < 2f;
        if (!ground && _car.Car is not (3 or 5))
            return;
        var length = 30f;
        if (ground && _car.Car == 2)
            length = actor.Position.X is > 100f and < 105f ? 10f : actor.Position.X is > 95f and < 100f ? 20f : 30f;
        _aoes.Add(new(new AOEShapeRect(length, 2.5f), actor.Position, actor.Rotation, WorldState.FutureTime(7),
            actorID: actor.InstanceID, arenaProjectionLayer: _car.Car is 3 or 5 ? ground ? 0 : 1 : null, restrictToArenaProjectionLayer: true));
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        var length = spell.Action.ID switch
        {
            (uint)AID.PlasmaBeamUpper or (uint)AID.PlasmaBeamLower => 30f,
            (uint)AID.PlasmaBeamMedium => 20f,
            (uint)AID.PlasmaBeamShort => 10f,
            _ => 0f
        };
        if (length == 0f)
            return;
        _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
        var ground = caster.PosRot.Y < 2f;
        if (ground || _car.Car is 3 or 5)
            _aoes.Add(new(new AOEShapeRect(length, 2.5f), spell.LocXZ, spell.Rotation, Module.CastFinishAt(spell),
                actorID: caster.InstanceID, arenaProjectionLayer: _car.Car is 3 or 5 ? ground ? 0 : 1 : null, restrictToArenaProjectionLayer: true));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell) => _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
    public override void OnActorDestroyed(Actor actor) => _aoes.RemoveAll(a => a.ActorID == actor.InstanceID);
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.PlasmaBeamLower or (uint)AID.PlasmaBeamMedium or (uint)AID.PlasmaBeamShort or (uint)AID.PlasmaBeamUpper)
        {
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
            ++NumCasts;
        }
    }
}

// TODO: Show the indicator arrow.
sealed class WindpipeDrawIn(BossModule module) : Components.SimpleKnockbacks(module, (uint)AID.WindpipeDrawIn, 20f)
{
    private readonly CarGeometry _car = module.FindComponent<CarGeometry>()!;

    // need safe walls for car 2 and car 5
    private readonly List<SafeWall> safeWalls2 = [new(new(100.1f, 150.1f), new(104.9f, 150.1f)), new(new(95.1f, 160.1f), new(99.9f, 160.1f))];
    private readonly List<SafeWall> safeWalls5 = [new(new(105.1f, 305.1f), new(109.9f, 305.1f))];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.WindpipeDrawIn)
        {
            var walls = _car.Car switch
            {
                2 => safeWalls2,
                5 => safeWalls5,
                _ => null
            };
            Casters.Add(new(spell.LocXZ, 20f, Module.CastFinishAt(spell), direction: spell.Rotation,
                safeWalls: walls, kind: Kind.DirBackward, actorID: caster.InstanceID));
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (Casters.Count != 0 && _car.Car is 2 or 5)
        {
            ref readonly var kb = ref Casters.Ref(0);
            if (!IsImmune(slot, kb.Activation))
            {
                // It is a draw in, we use mirrorZ to make safe zones to keep us from moving towards boss.
                var dir = kb.Direction.ToDirection().MirrorZ();
                var _walls = _car.Car == 2 ? safeWalls2 : safeWalls5;
                var len = _walls.Count;
                if (len > 0)
                {
                    var swalls = new SafeWall[len];
                    for (var i = 0; i < _walls.Count; ++i)
                    {
                        swalls[i] = _walls[i];
                    }
                    hints.AddForbiddenZone(new SDKnockbackFixedDirectionAgainstSafewalls(dir, swalls, 20f, len), kb.Activation);
                }
            }
        }
    }
}

sealed class Blastpipe(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Blastpipe, new AOEShapeRect(10f, 10f), riskyWithSecondsLeft: 3);

sealed class UnlimitedExpress(BossModule module) : Components.RaidwideCast(module, (uint)AID.UnlimitedExpress);

sealed class HeadOnEmission(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<AOEInstance> _aoes = [];
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_aoes);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is not ((uint)AID.ThunderousBreathLowerDeck or (uint)AID.HeadlightUpperDeck))
            return;
        _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
        var layer = spell.Action.ID == (uint)AID.ThunderousBreathLowerDeck ? 0 : 1;
        // Full car, clipped to the physical floor; activation is shared by radar and AI.
        _aoes.Add(new(new AOEShapeRect(15f, 10f, 15f), Arena.Center, activation: Module.CastFinishAt(spell),
            actorID: caster.InstanceID, arenaProjectionLayer: layer, restrictToArenaProjectionLayer: true));
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
            hints.Add(aoe.ArenaProjectionLayer == 0 ? "Move onto a raised platform!" : "Move to the lower deck!",
                ArenaProjectionLayerParticipantApplies(actor, aoe.ArenaProjectionLayer, true));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell) => _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
    public override void OnActorDestroyed(Actor actor) => _aoes.RemoveAll(a => a.ActorID == actor.InstanceID);
}

// Aether surge : multiple cone aoe from center outwards. 6.0s cast, range 30 45.000-degree cone
sealed class AetherSurge(BossModule module) : Components.SimpleAOEs(module, (uint)AID.AetherSurge, new AOEShapeCone(30f, 22.5f.Degrees()));

// Follow the moving train; the source is not locked until it stops. The three
// recorded Horn-to-Ray delays are 7.65-7.67s; use 7.6s conservatively.
sealed class AetherialRay(BossModule module) : Components.GenericBaitAway(module, (uint)AID.AetherialRay, damageType: AIHints.PredictedDamageType.Tankbuster)
{
    private Actor? _nextTarget;
    private DateTime _nextActivation;

    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        if (actor.OID == (uint)OID.GhostTrain && (SID)status.ID == SID.Stop)
        {
            foreach (ref var bait in CollectionsMarshal.AsSpan(CurrentBaits))
                bait.Source = actor;
        }
    }

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (actor.OID == (uint)OID.GhostTrain && (IconID)iconID == IconID.Horn)
        {
            _nextTarget = WorldState.Actors.Find(targetID);
            _nextActivation = WorldState.FutureTime(7.6f);
        }
        // source is GhostTrain, _nextTarget is the party member that is dragging the cone marker
        if (actor.OID == (uint)OID.GhostTrain && (IconID)iconID == IconID.Horn && _nextTarget != null)
        {
            CurrentBaits.RemoveAll(b => b.Source.InstanceID == actor.InstanceID);
            CurrentBaits.Add(new(actor, _nextTarget, new AOEShapeCone(50, 17.5f.Degrees()), _nextActivation));
        }
    }

    public override void Update() => CurrentBaits.RemoveAll(b => b.Source.IsDeadOrDestroyed || b.Target.IsDeadOrDestroyed);

    public override void OnActorDestroyed(Actor actor) => CurrentBaits.RemoveAll(b => b.Source == actor || b.Target == actor);

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            NumCasts++;
            CurrentBaits.Clear();
        }
    }
}

sealed class RunawayTrainRaidwide(BossModule module) : Components.RaidwideCastDelay(module, (uint)AID.RunawayTrainVisual, (uint)AID.RunawayTrainRaidwide, 15f);

// The indicator is harmless in transit. Forecast its final diamond vertex from
// the two/three-step cue and observed initial travel direction, not its current
// position. Car four's first explosion is stationary (also described by the guide).
sealed class ArcaneRevelation(BossModule module) : Components.GenericAOEs(module)
{
    private readonly CarGeometry _car = module.FindComponent<CarGeometry>()!;
    private readonly List<Actor> _indicators = [];
    private readonly HashSet<ulong> _resolved = [];
    private readonly HashSet<ulong> _stationaryFirst = [];
    private DateTime _revelation;
    private Actor? _moving;
    private WPos _start;
    private int _startIndex;
    private int _steps;
    private DateTime _activation;
    private WPos? _destination;

    // Observed stopping vertices, rounded independently from the 16y blast radius.
    // These are a recorded trajectory model, not physical arena-edge measurements.
    private WPos Vertex(int index) => index switch
    {
        0 => new(100f, Arena.Center.Z - 10f),
        1 => new(95.2f, Arena.Center.Z),
        2 => new(100f, Arena.Center.Z + 10f),
        _ => new(104.8f, Arena.Center.Z)
    };

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.ArcaneRevelation)
        {
            if (_indicators.Contains(actor))
                return;
            _indicators.RemoveAll(a => a.InstanceID == actor.InstanceID);
            _resolved.Remove(actor.InstanceID);
            _stationaryFirst.Remove(actor.InstanceID);
            // A late component bootstrap cannot establish which explosion is first.
            if (_car.Car == 4 && _revelation != default && WorldState.CurrentTime <= _revelation.AddSeconds(5))
                _stationaryFirst.Add(actor.InstanceID);
            _indicators.Add(actor);
        }
    }
    public override void OnActorDestroyed(Actor actor)
    {
        _indicators.Remove(actor);
        _resolved.Remove(actor.InstanceID);
        _stationaryFirst.Remove(actor.InstanceID);
        if (_moving == actor) ClearPrediction();
    }
    private void ClearPrediction()
    {
        _moving = null;
        _destination = null;
    }
    public override void Update()
    {
        _indicators.RemoveAll(a => a.IsDeadOrDestroyed);
        if (_moving is not { } moving)
            return;
        if (moving.IsDeadOrDestroyed)
        {
            ClearPrediction();
            return;
        }
        if (_destination != null)
            return;
        var travel = moving.Position - _start;
        // Ignore spawn quantization/turn-in-place before selecting the direction.
        if (travel.LengthSq() < 0.04f)
            return;
        var forward = Vertex((_startIndex + 1) % 4) - _start;
        var backward = Vertex((_startIndex + 3) % 4) - _start;
        var discriminator = travel.Dot(forward.Normalized() - backward.Normalized());
        if (Math.Abs(discriminator) > 0.05f)
            _destination = Vertex((_startIndex + (discriminator > 0f ? _steps : 4 - _steps)) % 4);
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster.OID == (uint)OID.Doomtrain && spell.Action.ID == (uint)AID.ArcaneRevelationVisual)
            _revelation = WorldState.CurrentTime;
        else if (caster.OID == (uint)OID.Doomtrain && spell.Action.ID is (uint)AID._Ability_HailOfThunder or (uint)AID._Ability_HailOfThunder2)
        {
            ClearPrediction();
            // No guess when source identity or starting vertex is ambiguous.
            if (_indicators.Count != 1 || _car.Car is not (4 or 5) || _car.Intermission)
                return;
            var actor = _indicators[0];
            _start = actor.Position;
            _startIndex = Enumerable.Range(0, 4).MinBy(i => (Vertex(i) - _start).LengthSq());
            if ((_start - Vertex(_startIndex)).LengthSq() > 0.25f)
                return;
            _moving = actor;
            _steps = spell.Action.ID == (uint)AID._Ability_HailOfThunder ? 2 : 3;
            // Cue-to-impact: 7.98-8.03s / 10.95-10.99s in the authoring source.
            // 0.2s is a conservative margin, not a verified server snapshot offset.
            _activation = WorldState.FutureTime(3 * _steps + 2 - 0.2f);
            if (_car.Car == 4 && _steps == 2 && _stationaryFirst.Contains(actor.InstanceID) && !_resolved.Contains(actor.InstanceID))
                _destination = Vertex(_startIndex);
        }
        else if (spell.Action.ID == (uint)AID.HailOfThunder)
        {
            foreach (var actor in _indicators)
                _resolved.Add(actor.InstanceID);
            ClearPrediction();
        }
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.HailOfThunder)
        {
            if (_destination is { } destination && !spell.LocXZ.InCircle(destination, 0.5f))
                ReportError("Hail of Thunder locked away from its forecast destination");
            // The actual helper locks the location and replaces the forecast.
            ClearPrediction();
        }
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
        => _destination is { } destination ? new AOEInstance[] { new(new AOEShapeCircle(16f), destination, activation: _activation,
            actorID: _moving!.InstanceID, restrictToArenaProjectionLayer: null) } : [];
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_moving != null && _destination == null)
            hints.Add("Waiting for lightning's travel direction");
    }
}

// Once the helper cast appears, use its exact locked location and cast timer.
// Keep the same explicit 0.2s conservative margin as the early forecast.
sealed class HailOfThunder(BossModule module) : Components.SimpleAOEs(module, (uint)AID.HailOfThunder, new AOEShapeCircle(16f), restrictToArenaProjectionLayer: null)
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        base.OnCastStarted(caster, spell);
        if (spell.Action.ID == WatchedAction && Casters.Count != 0)
            CollectionsMarshal.AsSpan(Casters)[^1].Activation = Module.CastFinishAt(spell, -0.2f);
    }
}

sealed class Electray3(BossModule module) : GroundElectray(module, (uint)AID.Electray3, 20f);

sealed class ElectrayUpper(BossModule module) : Components.SimpleAOEs(module, (uint)AID.ElectrayUpper, new AOEShapeRect(25f, 2.5f), arenaProjectionLayer: 1);

sealed class RunawayTrain(BossModule module) : Components.CastHint(module, (uint)AID.RunawayTrain, "Intermission: defeat the aether add!");

sealed class Shockwave(BossModule module) : Components.RaidwideCastDelay(module, (uint)AID.ShockwaveVisual, (uint)AID.Shockwave, 5.6f);

// The whole old car is lethal, including the pad if the transfer has not completed.
sealed class Derail(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Derail, new AOEShapeRect(30f, 10f));

// Shared tower: its long cast ends after all three damage pulses. The helper is
// not a player stack target, and arrival is due at the FIRST pulse, not cast end.
sealed class DerailmentSiegeCircle(BossModule module) : Components.GenericTowers(module)
{
    private readonly Dictionary<ulong, int> _hits = [];
    private readonly Dictionary<ulong, uint> _lastSequence = [];
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.DerailmentSiegeCircle || _hits.ContainsKey(caster.InstanceID))
            return;
        _hits[caster.InstanceID] = 0;
        Towers.Add(new(spell.LocXZ, 5f, 8, 8, activation: Module.CastFinishAt(spell, -2.9f), actorID: caster.InstanceID));
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is not ((uint)AID.DerailmentSiegeStack or (uint)AID._Ability_DerailmentSiege2 or (uint)AID._Ability_BatteringArms1 or (uint)AID._Ability_BatteringArms2))
            return;
        for (var i = 0; i < Towers.Count; ++i)
        {
            ref var tower = ref CollectionsMarshal.AsSpan(Towers)[i];
            if (!caster.Position.InCircle(tower.Position, 1f))
                continue;
            if (spell.GlobalSequence != 0 && _lastSequence.GetValueOrDefault(tower.ActorID) == spell.GlobalSequence)
                return;
            _lastSequence[tower.ActorID] = spell.GlobalSequence;
            ++NumCasts;
            var hits = ++_hits[tower.ActorID];
            if (spell.Action.ID is (uint)AID._Ability_DerailmentSiege2 or (uint)AID._Ability_BatteringArms2)
            {
                _hits.Remove(tower.ActorID);
                _lastSequence.Remove(tower.ActorID);
                Towers.RemoveAt(i);
            }
            else
                tower.Activation = WorldState.FutureTime(hits == 1 ? 1f : 1.5f);
            return;
        }
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.DerailmentSiegeCircle)
            OnActorDestroyed(caster);
    }
    public override void OnActorDestroyed(Actor actor)
    {
        _hits.Remove(actor.InstanceID);
        _lastSequence.Remove(actor.InstanceID);
        Towers.RemoveAll(t => t.ActorID == actor.InstanceID);
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed, PrimaryActorOID = (uint)OID.Doomtrain, Contributors = "wen, Xan", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 1076u, NameID = 14284u)]
public class T07Doomtrain(WorldState ws, Actor primary) : BossModule(ws, primary, new(100f, 100f), new ArenaBoundsRect(10f, 15f))
{
    public Actor? AetherIntermission;
    private Actor? ghostTrain;

    protected override void UpdateModule()
    {
        AetherIntermission = Enemies((uint)OID.AetherIntermission).FirstOrDefault(a => !a.IsDeadOrDestroyed && a.IsTargetable);
        ghostTrain = Enemies((uint)OID.GhostTrain).FirstOrDefault(a => !a.IsDeadOrDestroyed);
    }

    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actor(AetherIntermission);
        Arena.Actor(ghostTrain, Colors.Object, true);
        // use this to see where the arcane revelation aoe center is floating.
        Arena.Actors(Enemies((uint)OID.ArcaneRevelation), Colors.Object, true);
    }
}
