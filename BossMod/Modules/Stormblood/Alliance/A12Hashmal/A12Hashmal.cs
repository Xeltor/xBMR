namespace BossMod.Stormblood.Alliance.A12Hashmal;

class TowerAppear(BossModule module) : Components.SimpleAOEs(module, (uint)AID.ControlTower2, 6f);
class CommandTowerAppear(BossModule module) : Components.SimpleAOEs(module, (uint)AID.CommandTower4, 10f);
class JaggedEdge(BossModule module) : HashmalWaveAOEs(module, (uint)AID.JaggedEdge, new AOEShapeCircle(8f));
class EarthShaker(BossModule module) : HashmalWaveAOEs(module, (uint)AID.EarthShaker, new AOEShapeCone(60.5f, 30f.Degrees()))
{
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        base.AddHints(slot, actor, hints);
        if (EscapeBudget() is { } budget && Casters.Skip(3).Any(cone => cone.ShapeDistance!.Distance(actor.Position) < -budget))
            hints.Add("Move near the edge of this gap. Cross after the first Earth Shaker volley.");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (EscapeBudget() is not { } budget)
            return;

        // Keep both sets of exact cast warnings. While waiting for the first
        // volley, leave parts of the next cone that are too deep to exit in time.
        var firstImpact = Casters[0].Activation;
        for (var i = 3; i < Casters.Count; ++i)
            hints.AddForbiddenZone(new DeepCone(Casters[i].ShapeDistance!, budget), firstImpact);
    }

    private float? EscapeBudget()
    {
        if (Casters.Count != 6 || Casters[2].Activation > Casters[0].Activation.AddSeconds(SameWaveWindow)
            || Casters[5].Activation > Casters[3].Activation.AddSeconds(SameWaveWindow))
            return null;
        var seconds = (float)(Casters[3].Activation - Casters[2].Activation).TotalSeconds - Pathfinding.NavigationDecision.ActivationTimeCushion;
        var speed = WorldState.Client.MoveSpeed;
        return seconds > 0f && speed > 0f ? seconds * Math.Min(speed, 6f) : null;
    }

    private sealed class DeepCone(ShapeDistance cone, float budget) : ShapeDistance
    {
        public override float ConservativeLipschitz => cone.ConservativeLipschitz;
        public override float Distance(in WPos position) => cone.Distance(position) + budget;
    }
}

abstract class HashmalWaveAOEs(BossModule module, uint aid, AOEShape shape) : Components.SimpleAOEs(module, aid, shape)
{
    protected const double SameWaveWindow = 0.25d;
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var aoes = CollectionsMarshal.AsSpan(Casters);
        if (!aoes.IsEmpty)
        {
            var firstWaveEnd = aoes[0].Activation.AddSeconds(SameWaveWindow);
            foreach (ref var aoe in aoes)
                aoe.Color = aoe.Activation <= firstWaveEnd ? Colors.Danger : Colors.AOE;
        }
        return aoes;
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            base.OnCastStarted(caster, spell);
            SortHelpers.SortAOEByActivation(Casters);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (spell.Action.ID == WatchedAction)
            Casters.RemoveAll(aoe => aoe.ActorID == caster.InstanceID);
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            Casters.Clear();
        else
            Casters.RemoveAll(aoe => aoe.ActorID == actor.InstanceID);
    }

    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}
class FallingRock(BossModule module) : Components.SpreadFromCastTargets(module, (uint)AID.FallingRock, 8f)
{
    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID == (uint)IconID.FallingRock && actor.Type == ActorType.Player && !actor.IsDeadOrDestroyed && !IsSpreadTarget(actor))
            AddSpread(actor, WorldState.FutureTime(5.75d));
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == SpreadAction)
            Spreads.RemoveAll(spread => spread.Target.InstanceID == spell.TargetID);
        base.OnCastStarted(caster, spell);
    }

    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            Spreads.Clear();
        else
            Spreads.RemoveAll(spread => spread.Target == actor);
    }

    public override void Update() => Spreads.RemoveAll(spread => WorldState.CurrentTime > spread.Activation.AddSeconds(0.75d));
}

class WideTowerfall(BossModule module) : HashmalWaveAOEs(module, (uint)AID.Towerfall2, new AOEShapeRect(40.5f, 8f));
class HashmalRaidwides(BossModule module) : Components.RaidwideCasts(module, [(uint)AID.QuakeIV, (uint)AID.ControlTower1, (uint)AID.SubmissionTower]);
class Sanction(BossModule module) : Components.RaidwideCastsDelay(module, [(uint)AID.ControlTower1], [(uint)AID.Sanction1, (uint)AID.Sanction2], 3.1d, "Sanction follows Control Tower: prepare for another raidwide.")
{
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (Activation != default && spell.Action.ID == (uint)AID.ControlTower1 && !spell.EventHappened
            && WorldState.CurrentTime < Activation.AddSeconds(-Delay - 0.25d))
            Activation = default;
    }

    public override void Update()
    {
        if (Activation != default && WorldState.CurrentTime > Activation.AddSeconds(0.75d))
            Activation = default;
    }
}
class Landwaster(BossModule module) : Components.RaidwideCast(module, (uint)AID.Landwaster2, "Landwaster incoming. Prepare mitigation and healing.")
{
    private DateTime _expectedImpact;

    public override void OnActorDeath(Actor actor)
    {
        if (actor == Module.PrimaryActor)
        {
            _expectedImpact = default;
            Casters.Clear();
        }
        else if (actor.OID == (uint)OID.CommandTower2 && !Active)
        {
            // The tower death precedes the observed damage by 10.67s.
            _expectedImpact = WorldState.FutureTime(10.4d);
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            OnActorDeath(actor);
        else
            Casters.Remove(actor);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        base.OnCastStarted(caster, spell);
        if (spell.Action.ID == WatchedAction)
            _expectedImpact = default;
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (spell.Action.ID == WatchedAction)
        {
            _expectedImpact = default;
            Casters.Remove(caster);
        }
    }

    public override void Update()
    {
        if (_expectedImpact != default && WorldState.CurrentTime > _expectedImpact.AddSeconds(1d))
            _expectedImpact = default;
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        base.AddGlobalHints(actor, hints);
        if (_expectedImpact != default)
            hints.Add(Hint);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (_expectedImpact != default)
        {
            var affected = AffectedPlayers();
            if (affected != default)
                hints.AddPredictedDamage(affected, _expectedImpact);
        }
    }
}
class CommandTowerKnockback(BossModule module) : Components.SimpleKnockbacks(module, (uint)AID.CommandTower3, 50f, stopAtWall: true);
class CommandTowerRaidwide(BossModule module) : Components.RaidwideCast(module, (uint)AID.CommandTower3);

// The direction-bearing object appears ~9.1-9.4s before the uncast helper hit.
class Towerfall(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<AOEInstance> _aoes = [];
    private static readonly AOEShapeRect Shape = new(40.5f, 5f);
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_aoes);

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.FallingTower && !_aoes.Any(a => a.ActorID == actor.InstanceID))
            _aoes.Add(new(Shape, actor.Position, actor.Rotation, WorldState.FutureTime(9d), actorID: actor.InstanceID));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.Towerfall1)
            _aoes.RemoveAll(a => a.Origin.AlmostEqual(caster.Position, 1f) && a.Rotation.AlmostEqual(caster.Rotation, 0.05f));
    }

    public override void OnActorDestroyed(Actor actor) => _aoes.RemoveAll(a => a.ActorID == actor.InstanceID);
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "The Combat Reborn Team, Xel", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 281, NameID = 6922)]
public class A12Hashmal(WorldState ws, Actor primary) : BossModule(ws, primary, new(-320f, -46f), new ArenaBoundsCircle(29.5f))
{
    public override string[] PrePullHints =>
    [
        "Move away from the falling towers, hammer shadows, and the glowing side of the dash.",
        "Rock Cutter cleaves toward the tank. Stay clear of the cone.",
        "Switch to your alliance's Sand Sphere when it spawns. Stack with your own party for Falling Boulder.",
        "Destroy the Command Tower and Golems before the raid damage escalates.",
    ];

    protected override void CalculateModuleAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        Actor? tower = null;
        foreach (var enemy in hints.PotentialTargets)
        {
            if (enemy.Actor.OID == (uint)OID.CommandTower2)
            {
                if ((enemy.Priority >= 0 || enemy.Priority == AIHints.Enemy.PriorityUndesirable)
                    && enemy.Actor is { IsTargetable: true, IsDeadOrDestroyed: false, PendingDead: false }
                    && Arena.InBounds(enemy.Actor.Position))
                {
                    enemy.Priority = 2;
                    tower = enemy.Actor;
                }
                continue;
            }
            if (enemy.Priority < 0 || enemy.Actor.OID is (uint)OID.SandSphere or (uint)OID.PennantstoneGolem)
                continue;
            enemy.Priority = 0;
        }

        if (tower != null && FindComponent<ToDust>()?.StagingSector(slot) is not >= 0
            && !hints.PotentialTargets.Any(enemy => enemy.Priority > 2))
        {
            var range = actor.Role is Role.Tank or Role.Melee ? 3f : 24.5f;
            hints.GoalZones.Add(AIHints.GoalSingleTarget(tower.Position, tower.HitboxRadius + range, 5f));
        }
    }
}
