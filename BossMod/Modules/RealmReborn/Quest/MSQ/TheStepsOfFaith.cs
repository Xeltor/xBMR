namespace BossMod.RealmReborn.Quest.MSQ.TheStepsOfFaith;

public enum OID : uint
{
    Boss = 0x3A5F, // R30.0
    HordeWyvern1 = 0x3CD5, // R3.6
    HordeWyvern2 = 0x3AA2, // R3.6
    HordeWyvern3 = 0x3AA4, // R3.6
    HordeWyvern4 = 0x3AA5, // R3.6
    HordeWyvern5 = 0x3AAC, // R3.6
    HordeWyvern6 = 0x3AA7, // R3.6
    HordeWyvern7 = 0x3AAF, // R3.6
    HordeWyvern8 = 0x3ABF, // R3.6
    HordeWyvern9 = 0x3AA8, // R3.6
    HordeWyvern10 = 0x3AA9, // R3.6
    HordeDragonfly1 = 0x3A94, // R0.8
    HordeDragonfly2 = 0x3A93, // R0.8
    HordeDragonfly3 = 0x3A95, // R0.8
    HordeDragonfly4 = 0x3A96, // R0.8
    HordeDragonfly5 = 0x3A97, // R0.8
    HordeDragonfly6 = 0x3ABB, // R0.8
    HordeDragonfly7 = 0x3ABC, // R0.8
    HordeDragonfly8 = 0x3AB0, // R0.8
    HordeAevis1 = 0x3A9A, // R2.2
    HordeAevis2 = 0x3A9B, // R2.2
    HordeAevis3 = 0x3A9C, // R2.2
    HordeAevis4 = 0x3A9D, // R2.2
    HordeAevis5 = 0x3AA0, // R2.2
    HordeAevis6 = 0x3A9E, // R2.2
    HordeAevis7 = 0x3A9F, // R2.2
    HordeAevis8 = 0x3AC0, // R2.2
    HordeAevis9 = 0x3AB1, // R2.2
    HordeBiast1 = 0x3AB2, // R2.7
    HordeBiast2 = 0x3AB3, // R2.7
    HordeBiast3 = 0x3AB4, // R2.7
    HordeBiast4 = 0x3AAB, // R2.7
    HordeBiast5 = 0x3AB6, // R2.7
    HordeBiast6 = 0x3AC2, // R2.7
    HordeBiast7 = 0x3AB8, // R2.7
    HordeBiast8 = 0x3AB9, // R2.7
    HordeBiast9 = 0x3AB7, // R2.7
    HordeArmoredDragon = 0x3ABA, // R6.25
    HordeTranscendent = 0x3ABD, // R3.4
    HordeShieldDragon = 0x3AC1, // R5.3
    HordeWyvern11 = 0x3CD4,
    HordeAevis10 = 0x3CD6,
    HordeAevis11 = 0x3CD7,
    HordeBiast10 = 0x3CF0,
    HordeBiast11 = 0x3CF1,
    Helper = 0x233C
}

public enum AID : uint
{
    AutoAttack1 = 6499, // horde dragons->allies, no cast, single-target

    BlazingShriekVisual = 30882, // Boss->self, no cast, single-target
    BlazingShriek = 26407, // Helper->self, 0.8s cast, range 100 width 44 rect
    FlameBreathVisual = 30877, // Boss->self, 3.3+1,7s cast, single-target
    FlameBreath = 26812, // Helper->self, 35.0s cast, range 1 width 2 rect
    FlameBreath1 = 30185, // Helper->self, 5.0s cast, range 1 width 2 rect
    FlameBreath2 = 26411, // Boss->self, 3.8+1.2s cast, range 60 width 20 rect
    FlameBreath3 = 30186, // Helper->self, 5.0s cast, range 60 width 20 rect
    FlameBreathChannel = 30884, // Helper->self, no cast, range 40 width 20 rect
    CauterizeVisual = 30878, // Boss->self, 30.5+4.5s cast, single-target
    Cauterize = 30885, // Helper->self, no cast, range 40 width 44 rect
    Touchdown = 26408, // Helper->self, 6.0s cast, range 80 circle
    FireballVisual1 = 30874, // Boss->self, 3.0+3,0s cast, single-target
    FireballVisual2 = 30876, // Boss->self, 3.0s cast, single-target
    FireballVisual3 = 28975, // Boss->self, 3.0s cast, single-target
    FireballSpread = 30875, // Helper->allies, 6.0s cast, range 6 circle
    FireballAOE = 30894, // HordeTranscendent->location, 3.5s cast, range 6 circle
    BlazingFire = 30211, // Boss->location, no cast, range 10 circle

    BodySlamVisual = 26400, // Boss->self, 4.7+1,3s cast, single-target
    BodySlam = 26401, // Helper->self, 6.0s cast, range 80 width 44 rect
    Flamisphere = 30883, // Helper->location, 8.0s cast, range 10 circle

    RipperClaw = 31262, // HordeTranscendent->self, 3.7s cast, range 9 90-degree cone
    EarthshakerAOE = 30880, // Boss->self, 4.5s cast, range 31 circle
    Earthshaker = 30887, // Helper->self, 6.5s cast, range 80 30-degree cone
    EarthrisingAOE = 26410, // Boss->self, 4.5s cast, range 31 circle
    EarthrisingCast = 30888, // Helper->self, 7.0s cast, range 8 circle
    EarthrisingRepeat = 26412, // Helper->self, no cast, range 8 circle
    SidewiseSlice = 30879, // Boss->self, 8.0s cast, range 50 120-degree cone
    ScorchingBreathVisual = 29785, // Boss->self, 15.0+5.0s cast, single-target
    ScorchingBreath = 29789, // Helper->self, no cast, range 40 width 20 rect
    SeismicShriekVisual = 30881, // Boss->self, no cast, single-target
    SeismicShriek1 = 26405, // Boss->self, no cast, range 100 circle
    SeismicShriek2 = 26406, // Boss->self, no cast, range 80 circle
    Twingaze = 28971, // Boss->self, no cast, single-target
    Levinshower = 30892, // HordeBiast2/HordeBiast4/HordeBiast5/HordeBiast6/HordeBiast7->self, no cast, range 6 120-degree cone
    DragonStomp = 30893, // HordeArmoredDragon->self, 2.0s cast, range 40 circle
    BoneShaker = 31258, // HordeTranscendent->self, no cast, range 50 circle
    MagmaticSpell = 28974, // Boss->self, no cast, single-target
    Rake = 30898, // HordeShieldDragon->player, no cast, single-target
    ShieldFlameBreath = 30890, // HordeShieldDragon->self, no cast, range 12 cone
    FallOfMan = 30187, // Helper->self, 20.0s cast, range 90 width 20 rect
}

class RipperClaw(BossModule module) : Components.SimpleAOEs(module, (uint)AID.RipperClaw, new AOEShapeCone(9f, 45f.Degrees()));
class Levinshower(BossModule module) : Components.Cleave(module, (uint)AID.Levinshower, new AOEShapeCone(6f, 60f.Degrees()),
[(uint)OID.HordeBiast2, (uint)OID.HordeBiast4, (uint)OID.HordeBiast5, (uint)OID.HordeBiast6, (uint)OID.HordeBiast7]);
// No omen supplies an angle for this instant cone. Retain WIP for the unverified
// 120-degree Horde-cone convention; range 12 and the recorded frontal hit are confirmed.
class ShieldFlameBreath(BossModule module) : Components.Cleave(module, (uint)AID.ShieldFlameBreath, new AOEShapeCone(12f, 60f.Degrees()), [(uint)OID.HordeShieldDragon]);
class EarthShakerAOE(BossModule module) : Components.SimpleAOEs(module, (uint)AID.EarthshakerAOE, 31f);
class Earthshaker(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Earthshaker, new AOEShapeCone(80f, 15f.Degrees()))
{
    // Keep all three deadlines for navigation; draw only the immediate cone and next preview.
    public override void DrawArenaBackground(int pcSlot, Actor pc)
    {
        var aoes = ActiveAOEs(pcSlot, pc);
        for (var i = 0; i < Math.Min(2, aoes.Length); ++i)
            aoes[i].Shape.Draw(Arena, aoes[i].Origin, aoes[i].Rotation, i == 0 ? Colors.Danger : Colors.AOE);
    }
}

class EarthrisingAOE(BossModule module) : Components.SimpleAOEs(module, (uint)AID.EarthrisingAOE, 31f);
class Earthrising(BossModule module) : Components.Exaflare(module, 8f)
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.EarthrisingCast)
        {
            Lines.Add(new(caster.Position, 7.5f * spell.Rotation.ToDirection(), Module.CastFinishAt(spell), 1.0667d, 5, 2));
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var line in Lines)
            for (var i = 0; i < line.ExplosionsLeft; ++i)
                hints.AddForbiddenZone(Shape, line.Next + i * line.Advance, activation: line.NextExplosion.AddSeconds(i * line.TimeToMove));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.EarthrisingCast)
            Lines.RemoveAll(l => l.Next.AlmostEqual(caster.Position, 1f));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.EarthrisingRepeat or (uint)AID.EarthrisingCast)
        {
            var count = Lines.Count;
            var pos = caster.Position;
            for (var i = 0; i < count; ++i)
            {
                var line = Lines[i];
                if (line.Next.AlmostEqual(pos, 1f))
                {
                    AdvanceLine(line, pos);
                    if (line.ExplosionsLeft == 0)
                        Lines.RemoveAt(i);
                    return;
                }
            }
        }
    }
}

class SidewiseSlice(BossModule module) : Components.SimpleAOEs(module, (uint)AID.SidewiseSlice, new AOEShapeCone(50f, 60f.Degrees()));

class FireballSpread(BossModule module) : Components.SpreadFromCastTargets(module, (uint)AID.FireballSpread, 6f);
class FireballAOE(BossModule module) : Components.SimpleAOEs(module, (uint)AID.FireballAOE, 6f);
class Flamisphere(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Flamisphere, 10f);

class BodySlam(BossModule module) : Components.SimpleKnockbacks(module, (uint)AID.BodySlam, 20f, kind: Kind.DirForward, stopAtWall: true)
{
    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos)
        => Module.FindComponent<Flamisphere>()?.ActiveAOEs(slot, actor).ToArray().Any(a => a.Check(pos)) == true;

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var fire = Module.FindComponent<Flamisphere>();
        if (fire == null)
            return;
        foreach (var kb in Casters)
        {
            if (IsImmune(slot, kb.Activation))
                continue;
            var aoes = fire.ActiveAOEs(slot, actor).ToArray().Where(a => a.Activation >= kb.Activation && a.Activation <= kb.Activation.AddSeconds(3)).ToArray();
            if (aoes.Length != 0)
                hints.AddForbiddenZone(new SlamLanding(Arena.Center, Arena.Bounds, kb.Direction.ToDirection() * kb.Distance, aoes), kb.Activation);
        }
    }

    private sealed class SlamLanding(WPos center, ArenaBounds bounds, WDir displacement, Components.GenericAOEs.AOEInstance[] fire) : ShapeDistance, IKnockbackPreparationConstraint
    {
        public override float Distance(in WPos p)
        {
            var landing = center + bounds.ClampToBounds(p + displacement - center);
            var distance = float.MaxValue;
            foreach (var aoe in fire)
                distance = Math.Min(distance, aoe.Shape.Distance(aoe.Origin, aoe.Rotation).Distance(landing));
            return distance;
        }
    }
}

class FlameBreath(BossModule module) : Components.GenericAOEs(module)
{
    private AOEInstance[] _aoes = [];
    private Actor? _source;
    private DateTime _expiry;
    private bool _resolved;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _aoes;

    public override void Update()
    {
        if (WorldState.CurrentTime > _expiry || _source?.IsDeadOrDestroyed == true || Module.PrimaryActor.IsDeadOrDestroyed)
            _aoes = [];
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        var initial = spell.Action.ID is (uint)AID.FlameBreathVisual or (uint)AID.FlameBreath1;
        var ward = spell.Action.ID is (uint)AID.FlameBreath2 or (uint)AID.FlameBreath3;
        if (!initial && !ward)
            return;
        // The helper's five-second warning is authoritative; boss visuals precede it by 1.2/1.7s.
        var delay = spell.Action.ID == (uint)AID.FlameBreathVisual ? 1.7d : spell.Action.ID == (uint)AID.FlameBreath2 ? 1.2d : 0d;
        var activation = Module.CastFinishAt(spell, delay);
        _source = caster;
        _resolved = false;
        _aoes = [new(new AOEShapeRect(initial ? 220f : 60f, 10f), caster.Position, spell.Rotation, activation, actorID: caster.InstanceID)];
        _expiry = activation.AddSeconds(5);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == _source && !_resolved && (spell.RemainingTime > 0.5f
            || spell.Action.ID is (uint)AID.FlameBreath1 or (uint)AID.FlameBreath3 or (uint)AID.FlameBreath or (uint)AID.FallOfMan))
            _aoes = [];
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.FlameBreath1 or (uint)AID.FlameBreath3)
            _resolved = true;
        else if (spell.Action.ID == (uint)AID.FlameBreathChannel && _aoes.Length != 0)
        {
            ++NumCasts;
            _resolved = true;
            // Channel helpers deliver interleaved pulses. A local grace covers the observed <0.6s gaps.
            _expiry = WorldState.FutureTime(0.8d);
        }
    }
}

class Cauterize(BossModule module) : Components.GenericAOEs(module)
{
    private AOEInstance[] _aoes = [];
    private Actor? _source;
    private DateTime _expiry;
    private bool _resolved;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _aoes;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is not ((uint)AID.CauterizeVisual or (uint)AID.FlameBreath))
            return;
        var activation = Module.CastFinishAt(spell, spell.Action.ID == (uint)AID.CauterizeVisual ? 3.5d : -1d);
        _source = caster;
        _resolved = false;
        _aoes = [new(new AOEShapeRect(160f, 22f), Module.PrimaryActor.Position, Module.PrimaryActor.Rotation, activation, actorID: caster.InstanceID)];
        _expiry = activation.AddSeconds(2);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.Cauterize)
        {
            ++NumCasts;
            _resolved = true;
            // Six hits span overlapping helpers; the first hit must not withdraw the charge.
            _expiry = WorldState.FutureTime(0.8d);
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == _source && !_resolved && (spell.RemainingTime > 0.5f
            || spell.Action.ID is (uint)AID.FlameBreath1 or (uint)AID.FlameBreath3 or (uint)AID.FlameBreath or (uint)AID.FallOfMan))
            _aoes = [];
    }

    public override void Update()
    {
        if (WorldState.CurrentTime > _expiry || _source?.IsDeadOrDestroyed == true || Module.PrimaryActor.IsDeadOrDestroyed)
            _aoes = [];
    }
}

class Touchdown(BossModule module) : Components.SimpleKnockbacks(module, (uint)AID.Touchdown, 10f, stopAtWall: true);
class DragonStomp(BossModule module) : Components.RaidwideCast(module, (uint)AID.DragonStomp);
class BlazingShriek(BossModule module) : Components.RaidwideCast(module, (uint)AID.BlazingShriek, "Raidwide + stun");

class BlazingFire(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<AOEInstance> _aoes = [];
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_aoes);

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.Twingaze)
        {
            _aoes.Clear();
            // Both recorded retreats use these fixed placements relative to Vishap's ward.
            WDir[] offsets = [new(12f, -98f), new(-8f, -92f), new(8f, -78f), new(-12f, -72f)];
            for (var i = 0; i < offsets.Length; ++i)
                _aoes.Add(new(new AOEShapeCircle(10f), caster.Position + offsets[i], activation: WorldState.FutureTime(5d + 1.6d * i)));
        }
        else if (spell.Action.ID == (uint)AID.BlazingFire)
            _aoes.RemoveAll(a => a.Origin.AlmostEqual(new WPos(spell.TargetPos.X, spell.TargetPos.Z), 1f));
    }

    public override void Update()
        => _aoes.RemoveAll(a => a.Activation.AddSeconds(2) < WorldState.CurrentTime || Module.PrimaryActor.IsDeadOrDestroyed || Module.PrimaryActor.IsTargetable);

    public override void DrawArenaBackground(int pcSlot, Actor pc)
    {
        for (var i = 0; i < Math.Min(2, _aoes.Count); ++i)
            _aoes[i].Shape.Draw(Arena, _aoes[i].Origin, default, i == 0 ? Colors.Danger : Colors.AOE);
    }
}

class ScorchingBreath(BossModule module) : Components.GenericAOEs(module)
{
    private AOEInstance[] _aoes = [];
    private Actor? _source;
    private bool _resolved;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _aoes;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is not ((uint)AID.ScorchingBreathVisual or (uint)AID.FallOfMan))
            return;
        _source = caster;
        _resolved = false;
        _aoes = [new(new AOEShapeRect(90f, 10f), caster.Position, spell.Rotation,
            Module.CastFinishAt(spell, spell.Action.ID == (uint)AID.ScorchingBreathVisual ? 4.7d : -0.3d), actorID: caster.InstanceID)];
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == _source && !_resolved && (spell.RemainingTime > 0.5f
            || spell.Action.ID is (uint)AID.FlameBreath1 or (uint)AID.FlameBreath3 or (uint)AID.FlameBreath or (uint)AID.FallOfMan))
            _aoes = [];
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.FallOfMan or (uint)AID.ScorchingBreath)
        {
            ++NumCasts;
            _resolved = true;
            // Fall of Man starts a shorter persistent channel; retain it after the warning cast ends.
            if (_aoes.Length != 0)
            {
                _aoes[0].Shape = new AOEShapeRect(40f, 10f);
                _aoes[0].Activation = WorldState.CurrentTime;
            }
        }
    }

    public override void Update()
    {
        if (_source?.IsDeadOrDestroyed == true || Module.PrimaryActor.IsDeadOrDestroyed)
            _aoes = [];
    }
}

class ScrollingBounds(BossModule module) : BossComponent(module)
{
    public const float HalfHeight = 40f;
    public const float HalfWidth = 22f;

    public static readonly ArenaBoundsRect Bounds = new(HalfWidth, 90f);

    private int Phase = 1;
    private (float Min, float Max) ZBounds = (120f, 300f);

    public override void OnMapEffect(byte index, uint state)
    {
        if (state == 0x00020001u)
        {
            if (index == 0x03)
            {
                ZBounds = (120f, 200f);
                Phase = 2;
            }
            else if (index == 0x04)
            {
                ZBounds = (-40f, 40f);
                Phase = 4;
            }
            else if (index == 0x06)
            {
                ZBounds = (-200f, -120f);
                Phase = 6;
            }
        }
        else if (state == 0x00800040u)
        {
            if (index == 0x00)
            {
                ZBounds = (-40f, 200f);
                Phase = 3;
            }
            else if (index == 0x01)
            {
                ZBounds = (-200f, 40f);
                Phase = 5;
            }
        }
        Arena.Center = new(0, (ZBounds.Min + ZBounds.Max) * 0.5f);
        Arena.Bounds = new ArenaBoundsRect(HalfWidth, (ZBounds.Max - ZBounds.Min) * 0.5f);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Publish the full authored corridor, so the retreat goal is inside the navigation map.
        float? destination = Phase == 1 && !actor.InCombat && actor.FindStatus(1268u) == null ? 207f
            : Phase == 3 && actor.PosRot.Z > 25f ? 25f : Phase == 5 && actor.PosRot.Z > -135f ? -135f : null;
        if (destination is float z)
            hints.AddForbiddenZone(new SDHalfPlane(new(0, z), new WDir(0, -1f)));
    }

}

class VishapStates : StateMachineBuilder
{
    public VishapStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<FlameBreath>()
            .ActivateOnEnter<Cauterize>()
            .ActivateOnEnter<Touchdown>()
            .ActivateOnEnter<FireballSpread>()
            .ActivateOnEnter<FireballAOE>()
            .ActivateOnEnter<Flamisphere>()
            .ActivateOnEnter<BodySlam>()
            .ActivateOnEnter<SidewiseSlice>()
            .ActivateOnEnter<ScrollingBounds>()
            .ActivateOnEnter<BlazingFire>()
            .ActivateOnEnter<DragonStomp>()
            .ActivateOnEnter<BlazingShriek>()
            .ActivateOnEnter<EarthShakerAOE>()
            .ActivateOnEnter<Earthshaker>()
            .ActivateOnEnter<EarthrisingAOE>()
            .ActivateOnEnter<Earthrising>()
            .ActivateOnEnter<RipperClaw>()
            .ActivateOnEnter<Levinshower>()
            .ActivateOnEnter<ShieldFlameBreath>()
            .ActivateOnEnter<ScorchingBreath>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.Quest, GroupID = 70127, NameID = 3330)]
public class Vishap(WorldState ws, Actor primary) : BossModule(ws, primary, new(default, 210f), ScrollingBounds.Bounds)
{
    // vishap doesn't start targetable
    private static readonly uint[] all = [(uint)OID.HordeWyvern1, (uint)OID.HordeWyvern2, (uint)OID.HordeWyvern3, (uint)OID.HordeWyvern4, (uint)OID.HordeWyvern5,
    (uint)OID.HordeWyvern6, (uint)OID.HordeWyvern7, (uint)OID.HordeWyvern8, (uint)OID.HordeWyvern9, (uint)OID.HordeWyvern10, (uint)OID.HordeDragonfly1,
    (uint)OID.HordeDragonfly2, (uint)OID.HordeDragonfly3, (uint)OID.HordeDragonfly4, (uint)OID.HordeDragonfly5, (uint)OID.HordeDragonfly6, (uint)OID.HordeDragonfly7,
    (uint)OID.HordeDragonfly8, (uint)OID.HordeAevis1, (uint)OID.HordeAevis2, (uint)OID.HordeAevis3, (uint)OID.HordeAevis4, (uint)OID.HordeAevis5, (uint)OID.HordeAevis6,
    (uint)OID.HordeAevis7, (uint)OID.HordeAevis8, (uint)OID.HordeAevis9, (uint)OID.HordeBiast1, (uint)OID.HordeBiast2, (uint)OID.HordeBiast3, (uint)OID.HordeBiast4,
    (uint)OID.HordeBiast5, (uint)OID.HordeBiast6, (uint)OID.HordeBiast7, (uint)OID.HordeBiast8, (uint)OID.HordeBiast9, (uint)OID.HordeArmoredDragon, (uint)OID.HordeShieldDragon,
    (uint)OID.HordeTranscendent, (uint)OID.HordeWyvern11, (uint)OID.HordeAevis10, (uint)OID.HordeAevis11, (uint)OID.HordeBiast10, (uint)OID.HordeBiast11];

    protected override bool CheckPull() => PrimaryActor.InCombat;

    protected override void CalculateModuleAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var enemy in hints.PotentialTargets)
            if (enemy.Priority >= 0 && enemy.Actor.IsTargetable && !enemy.Actor.IsAlly && !enemy.Actor.IsDeadOrDestroyed)
            {
                if (all.Contains(enemy.Actor.OID))
                    enemy.Priority = Math.Max(enemy.Priority, 2);
                else if (enemy.Actor == PrimaryActor)
                    enemy.Priority = Math.Max(enemy.Priority, 1);
            }
    }

    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actors(this, all);
        Arena.Actor(PrimaryActor, allowDeadAndUntargetable: true);
    }
}

