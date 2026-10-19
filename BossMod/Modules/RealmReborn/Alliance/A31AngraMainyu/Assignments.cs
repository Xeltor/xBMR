namespace BossMod.RealmReborn.Alliance.A31AngraMainyu;

class DoubleVision(BossModule module) : Components.GenericAOEs(module, warningText: "Change floor color!")
{
    private DateTime _activation;
    private WPos _origin;
    private Angle _rotation;
    private bool _visualResolved;
    private int _resolvedColors;
    private readonly HashSet<ulong> _resolvedPlayers = [];
    private static readonly AOEShapeCone HalfArena = new(60, 90.Degrees());
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.DoubleVision)
        {
            _origin = spell.LocXZ;
            _rotation = spell.Rotation;
            _activation = Module.CastFinishAt(spell, -0.5);
            _visualResolved = false;
            _resolvedColors = 0;
            _resolvedPlayers.Clear();
        }
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.DoubleVision)
            _visualResolved = true;
        if (spell.Action.ID is (uint)AID.SullenGaze or (uint)AID.IrefulGaze)
        {
            foreach (var target in spell.Targets)
                _resolvedPlayers.Add(target.ID);
            _resolvedColors |= spell.Action.ID == (uint)AID.SullenGaze ? 1 : 2;
            if (_resolvedColors == 3)
                _activation = default;
        }
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.DoubleVision && !_visualResolved)
            _activation = default;
    }
    public override void Update()
    {
        if (_activation != default && WorldState.CurrentTime > _activation.AddSeconds(3))
            _activation = default;
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        if (_activation == default || _resolvedPlayers.Contains(actor.InstanceID))
            return [];
        // Both recorded waves put Sullen in front of the cast's locked facing, Ireful behind.
        // Only the player's previous color is dangerous; an unbranded player can choose either.
        var sullen = actor.FindStatus(SID.BrandOfTheSullen) != null;
        var ireful = actor.FindStatus(SID.BrandOfTheIreful) != null;
        return sullen || ireful ? new AOEInstance[] { new(HalfArena, _origin, sullen ? _rotation : _rotation + 180.Degrees(), _activation) } : [];
    }
}

class LevelMechanics(BossModule module) : Components.GenericAOEs(module)
{
    private Actor? _target;
    private DateTime _activation;
    private bool _visualResolved;
    private Actor? Target => _target is { IsDeadOrDestroyed: false } target && WorldState.Actors.Find(target.InstanceID) == target ? target : null;
    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID is (uint)IconID.Icon44 or (uint)IconID.Icon45)
        {
            _target = actor;
            _activation = WorldState.FutureTime(4);
            _visualResolved = false;
        }
    }
    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (tether.ID is (uint)TetherID.Tether1 or (uint)TetherID.Tether5
            && WorldState.Actors.Find(tether.Target) is { } target && target.FindStatus(SID.Bind) != null)
            _target = target;
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID is (uint)AID.Level100Flare1 or (uint)AID.Level150Death1)
        {
            _activation = Module.CastFinishAt(spell, -0.5);
            _visualResolved = false;
        }
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID is (uint)AID.Level100Flare1 or (uint)AID.Level150Death1)
            _visualResolved = true;
        else if (spell.Action.ID is (uint)AID.Level100Flare2 or (uint)AID.Level150Death2)
            _target = null;
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID is (uint)AID.Level100Flare1 or (uint)AID.Level150Death1 && !_visualResolved)
            _target = null;
    }
    public override void Update()
    {
        if (_activation != default && WorldState.CurrentTime > _activation.AddSeconds(3))
            _target = null;
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Target is { } target)
            hints.Add(actor == target ? "Level marker: stay still; everyone else leave the circle!" : "Leave the marked player's level circle!",
                actor != target && actor.Position.InCircle(target.Position, 12));
    }
    // Keep the bound recipient still; all other players leave the 12y circle.
    // Publish the same timed shape to radar, AI and mechanic forecast consumers.
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
        => Target is { } target && actor != target
            ? new AOEInstance[] { new(new AOEShapeCircle(12), target.Position, default, _activation) } : [];
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (Target is { } target)
            Arena.AddCircleUnfilled(target.Position, 12, pc == target ? Colors.Safe : Colors.Danger);
    }
}

class DoomCleanse(BossModule module) : BossComponent(module)
{
    private readonly HashSet<Actor> _active = [];
    private Actor? _lastLit;
    private DateTime _litSince;
    private bool _cycleConfirmed;
    private static bool IsPad(Actor actor) => actor.OID is >= 2004753 and <= 2004756;
    private static uint NextOID(uint oid) => oid == 2004756 ? 2004753 : oid + 1;
    public override void OnActorEState(Actor actor, ushort state)
    {
        if (!IsPad(actor))
            return;
        if (state == 4)
        {
            var interval = (WorldState.CurrentTime - _litSince).TotalSeconds;
            _cycleConfirmed = _lastLit != null && actor.OID == NextOID(_lastLit.OID) && interval is >= 4.8 and <= 5.2;
            _lastLit = actor;
            _litSince = WorldState.CurrentTime;
            _active.Clear();
            _active.Add(actor);
        }
        else
        {
            _active.Remove(actor);
            if (actor == _lastLit)
            {
                _lastLit = null;
                _cycleConfirmed = false;
            }
        }
    }
    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (IsPad(actor) && state == 0x00010010)
            _active.Remove(actor);
    }
    public override void OnActorDestroyed(Actor actor)
    {
        _active.Remove(actor);
        if (_lastLit == actor)
        {
            _lastLit = null;
            _cycleConfirmed = false;
        }
    }
    public override void Update()
    {
        // A missing light-change event must not leave an old destination lit indefinitely.
        if (_lastLit != null && WorldState.CurrentTime > _litSince.AddSeconds(5.3))
        {
            _active.Clear();
            _lastLit = null;
            _cycleConfirmed = false;
        }
    }
    private Actor? Destination(Actor actor)
    {
        var current = _active.Where(p => !p.IsDeadOrDestroyed).MinBy(p => (p.Position - actor.Position).LengthSq());
        if (current == null || !_cycleConfirmed || current != _lastLit || actor.Position.InCircle(current.Position, 2))
            return current;
        // Seventy observed transitions in two runs advance clockwise every 4.956-5.059s.
        // Compare conservative straight-line travel against the remaining light window.
        // This selects a destination; it does not certify the actual route or server snapshot.
        var speed = Math.Clamp(WorldState.Client.MoveSpeed, 0.1f, 6);
        var arrival = WorldState.CurrentTime.AddSeconds((actor.Position - current.Position).Length() / speed + 1);
        if (arrival < _litSince.AddSeconds(4.5))
            return current;
        var next = WorldState.Actors.FirstOrDefault(p => p.OID == NextOID(current.OID) && !p.IsDeadOrDestroyed);
        if (next == null || actor.FindStatus(SID.Doom) is not { } doom)
            return current;
        var nextArrival = WorldState.CurrentTime.AddSeconds((actor.Position - next.Position).Length() / speed + 1);
        var deadline = doom.ExpireAt < _litSince.AddSeconds(9.5) ? doom.ExpireAt.AddSeconds(-1) : _litSince.AddSeconds(9.5);
        return _litSince.AddSeconds(5.3) < deadline && nextArrival < deadline ? next : current;
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (actor.FindStatus(SID.Doom) != null)
        {
            var target = Destination(actor);
            hints.Add(target == null ? "Doom: find a lit cleansing circle!" : _active.Contains(target)
                ? "Doom: enter the lit cleansing circle!" : "Doom: go to the next cleansing circle and wait for its light!");
        }
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Unlit pads are safe to stand on. Arrive immediately and wait if the current
        // light will expire before arrival; every observed activation corrects the plan.
        if (actor.FindStatus(SID.Doom) != null && Destination(actor) is { } target)
            hints.AddForbiddenZone(new SDInvertedCircle(target.Position, 2), WorldState.CurrentTime);
    }
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (pc.FindStatus(SID.Doom) != null)
        {
            foreach (var pad in _active)
                Arena.AddCircleUnfilled(pad.Position, 2.5f, Colors.Safe);
            if (Destination(pc) is { } target && !_active.Contains(target))
                Arena.AddCircleUnfilled(target.Position, 2, Colors.Safe);
        }
    }
}

class Adds(BossModule module) : BossComponent(module)
{
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Module.Enemies((uint)OID.FinalHourglass).Any(a => !a.IsDeadOrDestroyed))
            hints.Add("Roulette: kill the hourglasses, then leave the final lit quadrant");
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var target in hints.PotentialTargets)
            if (target.Actor.InCombat && target.Priority >= 0)
            {
                target.Priority = target.Actor.OID switch { (uint)OID.FinalHourglass => 4, (uint)OID.AngraMainyusDaewa => 3, (uint)OID.Boss => 1, _ => target.Priority };
                if (target.Actor.OID == (uint)OID.AngraMainyusDaewa && target.Actor.CastInfo is { Interruptible: true } cast && cast.Action.ID is (uint)AID.Paralyze or (uint)AID.EyesOnMe)
                    target.ShouldBeInterrupted = true;
            }
    }
}
