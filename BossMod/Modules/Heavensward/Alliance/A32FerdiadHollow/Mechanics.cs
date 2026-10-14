namespace BossMod.Heavensward.Alliance.A32FerdiadHollow;

// The three marked players are hit in an order that differs from icon arrival order.
class BlackWind(BossModule module) : Components.GenericBaitAway(module, (uint)AID.BlackWind, centerAtTarget: true)
{
    private static readonly AOEShapeCircle _shape = new(5);

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID == (uint)IconID.Icon23)
        {
            var target = WorldState.Actors.Find(targetID) ?? actor;
            CurrentBaits.RemoveAll(b => b.Target.InstanceID == target.InstanceID);
            // Both recorded sets begin resolving after 5.15-5.27s; later targets retain the early conservative deadline.
            CurrentBaits.Add(new(Module.PrimaryActor, target, _shape, WorldState.FutureTime(5)));
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (spell.Action.ID == (uint)AID.BlackWind)
            CurrentBaits.RemoveAll(b => b.Target.InstanceID == spell.MainTargetID);
    }

    public override void Update() => CurrentBaits.RemoveAll(b => b.Target.IsDead || b.Target.IsDestroyed);
}

class DeathScythe(BossModule module) : Components.Voidzone(module, 4, m => m.Enemies((uint)OID.AbyssalScythe).Where(a => !a.IsDead && !a.IsDestroyed));

// Visual casts apply no damage. Helper actions resolve after the visual ends.
class FerdiadRaidwides(BossModule module) : BossComponent(module)
{
    private DateTime _activation;
    private DateTime _visualFinish;
    private uint _visual;
    private bool _jig;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.Flameflow1 or (uint)AID.JestersJig1)
        {
            _visual = spell.Action.ID;
            _jig = _visual == (uint)AID.JestersJig1;
            _visualFinish = Module.CastFinishAt(spell);
            // Observed helper actions: Flameflow +0.81s, Jester's Jig +2.00s after the visual action.
            // These are client observations, not verified server snapshots; use slightly earlier deadlines.
            _activation = _visualFinish.AddSeconds(_jig ? 1.9 : 0.8);
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == _visual && WorldState.CurrentTime < _visualFinish.AddSeconds(-0.1))
            _activation = default;
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.Flameflow2 or (uint)AID.Flameflow3 or (uint)AID.JestersJig2)
            _activation = default;
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_activation != default)
            hints.Add(_jig ? "Raidwide: Jester's Jig" : "Raidwide: Flameflow");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_activation == default)
            return;
        BitMask affected = default;
        foreach (var (s, _) in Raid.WithSlot())
            affected.Set(s);
        if (affected != default)
            hints.AddPredictedDamage(affected, _activation);
    }
}

class Wormhole(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<AOEInstance> _aoes = [];
    private readonly Dictionary<ulong, DateTime> _visualFinish = [];
    private static readonly AOEShapeCircle _circle = new(20);
    // Keep the existing inner-radius estimate; the outer range follows the actual Explosion action, not the portal visual.
    private static readonly AOEShapeDonut _donut = new(6, 75);

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_aoes);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        var id = spell.Action.ID;
        if (id is (uint)AID.AtmosAOE2 or (uint)AID.AtmosDonut)
        {
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
            var finish = Module.CastFinishAt(spell);
            _visualFinish[caster.InstanceID] = finish;
            // All four observed projectiles stop 4.1y inside their exit portal, before exploding.
            var origin = caster.Position + 4.1f * caster.Rotation.ToDirection();
            _aoes.Add(new(id == (uint)AID.AtmosDonut ? _donut : _circle, origin, activation: finish.AddSeconds(3.4), actorID: caster.InstanceID));
        }
        else if (id is (uint)AID.Explosion or (uint)AID.Explosion2)
        {
            // Associate only the nearby matching forecast; simultaneous exits remain independent.
            _aoes.RemoveAll(a => a.Origin.AlmostEqual(caster.Position, 1) || a.ActorID == caster.InstanceID);
            _aoes.Add(new(id == (uint)AID.Explosion2 ? _donut : _circle, caster.Position, activation: Module.CastFinishAt(spell), actorID: caster.InstanceID));
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (_visualFinish.TryGetValue(caster.InstanceID, out var finish) && WorldState.CurrentTime < finish.AddSeconds(-0.1))
        {
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
            _visualFinish.Remove(caster.InstanceID);
        }
        // Successful portal completion is transport, not explosion expiry. Actual casts also retain their warning through the action.
        else if (spell.Action.ID is (uint)AID.Explosion or (uint)AID.Explosion2 && _aoes.Any(a => a.ActorID == caster.InstanceID && WorldState.CurrentTime < a.Activation.AddSeconds(-0.1)))
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.Explosion or (uint)AID.Explosion2)
        {
            ++NumCasts;
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        _aoes.RemoveAll(a => a.ActorID == actor.InstanceID);
        _visualFinish.Remove(actor.InstanceID);
    }
}
