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
    private static bool IsPad(Actor actor) => actor.OID is 2004753 or 2004754 or 2004755 or 2004756;
    public override void OnActorEState(Actor actor, ushort state)
    {
        if (IsPad(actor))
        {
            if (state == 4)
                _active.Add(actor);
            else
                _active.Remove(actor);
        }
    }
    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (IsPad(actor) && state == 0x00010010)
            _active.Remove(actor);
    }
    public override void OnActorDestroyed(Actor actor) => _active.Remove(actor);
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (actor.FindStatus(SID.Doom) != null)
            hints.Add(_active.Count == 0 ? "Doom: find a lit cleansing circle; current activation unknown!" : "Doom: enter a lit cleansing circle!");
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (actor.FindStatus(SID.Doom) is not { } doom)
            return;
        // Layout cylinders are 2.5y and both observed successful cleanses are inside them.
        // Cleanse immediately: the next light change is not reliably forecast. Arrive within 2y; moving lights are re-read on each callback rather than a fitted cycle.
        var safe = _active.Where(p => !p.IsDeadOrDestroyed).Select(p => (ShapeDistance)new SDInvertedCircle(p.Position, 2)).ToArray();
        if (safe.Length != 0)
            hints.AddForbiddenZone(new SDIntersection(safe), WorldState.CurrentTime);
    }
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (pc.FindStatus(SID.Doom) != null)
            foreach (var pad in _active)
                Arena.AddCircleUnfilled(pad.Position, 2.5f, Colors.Safe);
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
