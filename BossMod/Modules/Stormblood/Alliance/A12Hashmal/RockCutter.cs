namespace BossMod.Stormblood.Alliance.A12Hashmal;

// Action range 4 plus Hashmal's 6-yalm hitbox. The sector is conservative; the exact angle is unknown.
class RockCutter(BossModule module) : Components.Cleave(module, (uint)AID.RockCutter, new AOEShapeCone(10f, 75f.Degrees()), [(uint)OID.Boss])
{
    private Actor? _caster;
    private ulong _targetID;

    public override List<(Actor origin, Actor target, Angle angle)> OriginsAndTargets()
        => _caster is { IsDeadOrDestroyed: false } caster && WorldState.Actors.Find(_targetID) is { IsDeadOrDestroyed: false } target
            ? [(caster, target, caster.AngleTo(target))] : [];

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_caster is { IsDeadOrDestroyed: false })
            hints.Add("Rock Cutter cleaves toward the tank. Mitigate the tankbuster.");
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        base.AddHints(slot, actor, hints);
        if (actor.InstanceID == _targetID && _caster is { IsDeadOrDestroyed: false } caster
            && Raid.WithoutSlot().Any(ally => ally != actor && ally.Type != ActorType.Pet
                && Shape.Check(ally.Position, caster.Position, caster.AngleTo(actor))))
            hints.Add("Aim Rock Cutter away from nearby allies!");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_caster is not { IsDeadOrDestroyed: false } caster || WorldState.Actors.Find(_targetID) is not { IsDeadOrDestroyed: false } target)
            return;

        if (actor != target || !PreferLeastCollateral(caster, target, hints))
            base.AddAIHints(slot, actor, assignment, hints);
        if (Raid.FindSlot(_targetID) is >= 0 and var targetSlot)
            hints.AddPredictedDamage(new BitMask().WithBit(targetSlot), NextExpected, AIHints.PredictedDamageType.Tankbuster);
    }

    private bool PreferLeastCollateral(Actor caster, Actor target, AIHints hints)
    {
        var cone = (AOEShapeCone)Shape;
        var origin = caster.Position;
        var allies = Raid.WithoutSlot().Where(ally => ally != target && ally.Type != ActorType.Pet)
            .Select(ally => ally.Position)
            .Where(position => position != origin && position.InCircle(origin, cone.Radius))
            .ToArray();
        var aim = new ArcList(origin, cone.Radius);
        foreach (var position in allies)
            aim.ForbidArcByLength(Angle.FromDirection(position - origin), cone.HalfAngle);
        if (aim.Allowed(default).Any())
            return false;

        // Other players can still dodge. Do not cover the entire arena with the tank's aim constraints.
        hints.GoalZones.Add(AimScore);
        return true;

        float AimScore(WPos position)
        {
            if (position == origin || !position.InCircle(origin, cone.Radius))
                return 0f;

            var direction = Angle.FromDirection(position - origin);
            var spared = 0;
            foreach (var ally in allies)
                if (!cone.Check(ally, origin, direction))
                    ++spared;
            return 10f * spared;
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster.OID == (uint)OID.Boss && spell.Action.ID == WatchedAction)
        {
            _caster = caster;
            _targetID = spell.TargetID == caster.InstanceID ? caster.TargetID : spell.TargetID;
            NextExpected = Module.CastFinishAt(spell);
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == _caster && spell.Action.ID == WatchedAction && !spell.EventHappened && WorldState.CurrentTime < NextExpected.AddSeconds(-0.25d))
            _caster = null;
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (caster == _caster && spell.Action.ID == WatchedAction)
            _caster = null;
    }

    public override void Update()
    {
        if (_caster != null && WorldState.CurrentTime > NextExpected.AddSeconds(0.75d))
            _caster = null;
    }
}
