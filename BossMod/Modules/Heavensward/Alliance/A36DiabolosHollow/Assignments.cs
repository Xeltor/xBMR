namespace BossMod.Heavensward.Alliance.A36DiabolosHollow;

// Markers are instant actions: cast-target helpers never receive their start.
class HollowNight(BossModule module) : Components.StackWithIcon(module, (uint)IconID.Icon91, (uint)AID.HollowNight, 8, 6, 8)
{
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == StackAction)
            CastCounter = 0;
        base.OnEventCast(caster, spell);
        if (spell.Action.ID == StackAction)
            CastCounter = 0;
    }

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID == StackIcon)
        {
            if (Stacks.Any(s => s.Target == actor))
                return; // duplicate cues must not postpone the response deadline
            Stacks.RemoveAll(s => s.Target.InstanceID == actor.InstanceID);
        }
        base.OnEventIcon(actor, iconID, targetID);
    }

    public override void Update()
    {
        base.Update();
        Stacks.RemoveAll(s => s.Target.IsDestroyed || s.Activation.AddSeconds(2) < WorldState.CurrentTime);
    }
}

class HollowNightGaze(BossModule module) : Components.GenericGaze(module, (uint)AID.HollowNight)
{
    public override ReadOnlySpan<Eye> ActiveEyes(int slot, Actor actor)
    {
        if (Module.FindComponent<HollowNight>() is not { } stack)
            return [];
        List<Eye> eyes = [];
        foreach (var s in stack.Stacks)
            if (s.Target != actor && !s.Target.IsDestroyed && !s.Target.IsDead)
                eyes.Add(new(s.Target.Position, s.Activation, range: 8, actorID: s.Target.InstanceID));
        return eyes.ToArray();
    }
}

// The inherited cone dimensions still need independent physical validation.
class EarthShaker2(BossModule module) : Components.BaitAwayIcon(module, new AOEShapeCone(60, 15.Degrees()), (uint)IconID.Icon40, (uint)AID.EarthShaker2, 4.7)
{
    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID == IID)
        {
            if (CurrentBaits.Any(b => b.Target == actor))
                return;
            CurrentBaits.RemoveAll(b => b.Target.InstanceID == actor.InstanceID);
        }
        base.OnEventIcon(actor, iconID, targetID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != WatchedAction)
            return;
        ++NumCasts;
        // Helper order differs from icon order. Retire only an unambiguous
        // marked recipient, rather than guessing from the nearest current facing.
        var matches = CurrentBaits.Where(b => spell.Targets.Any(t => t.ID == b.Target.InstanceID)).ToArray();
        if (matches.Length == 1)
            CurrentBaits.RemoveAll(b => b.Target.InstanceID == matches[0].Target.InstanceID);
    }

    public override void Update()
    {
        base.Update();
        CurrentBaits.RemoveAll(b => b.Target.IsDestroyed || b.Activation.AddSeconds(2) < WorldState.CurrentTime);
    }
}

// Cone angle is not established by the available sheet/recording. Keep an
// actionable marker warning and healing deadline without inventing a safe cone.
class Blindside(BossModule module) : BossComponent(module)
{
    private Actor? _target;
    private DateTime _activation;

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID == (uint)IconID.Icon93 && (_target != actor || _activation.AddSeconds(2) <= WorldState.CurrentTime))
        {
            _target = actor;
            _activation = WorldState.FutureTime(5);
        }
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_target != null)
            hints.Add(TargetAvailable()
                ? "Blindside: share the marked frontal attack (cone bounds unverified)"
                : "Blindside still incoming: marked target unavailable; watch boss facing and share the frontal attack (cone bounds unverified)");
    }

    private bool TargetAvailable() => _target is { IsDeadOrDestroyed: false } target && WorldState.Actors.Find(target.InstanceID) == target;

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (TargetAvailable() && Raid.FindSlot(_target!.InstanceID) is var targetSlot && targetSlot >= 0)
            hints.AddPredictedDamage(BitMask.Build(targetSlot), _activation, AIHints.PredictedDamageType.Tankbuster);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.Blindside)
        {
            _target = null;
            _activation = default;
        }
    }

    public override void Update()
    {
        // The original's third marked tank dies2.43s before the cleave still resolves.
        // Death/destruction withdraws only target-specific healing, never the bounded incoming warning.
        if (_target != null && _activation.AddSeconds(2) <= WorldState.CurrentTime)
        {
            _target = null;
            _activation = default;
        }
    }
}

class PavorInanis(BossModule module) : Components.RaidwideCast(module, (uint)AID.PavorInanis);
// No Hollow Terror cast occurred in the source; do not guess its inner radius.
class HollowTerror(BossModule module) : Components.CastHint(module, (uint)AID.HollowTerror, "Hollow Terror: get close or far away");
