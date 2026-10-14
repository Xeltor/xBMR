namespace BossMod.Heavensward.Alliance.A34Scathach;

// One recorded emergence: helper damage follows the corrected visual finish by
// 4.0709s. This early 4.0s estimate is a planning cue, not a server snapshot.
class BlindingShadow(BossModule module) : BossComponent(module)
{
    private ActorCastInfo? _visualCast;
    private bool _visualResolved;
    private DateTime _activation;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        Update();
        if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.BlindingShadow1
            && !caster.IsDead && !caster.IsDestroyed && spell != _visualCast)
        {
            _visualCast = spell;
            _visualResolved = false;
            _activation = Module.CastFinishAt(spell, 4d);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        Update();
        if (spell.Action.ID == (uint)AID.BlindingShadow2)
            Clear();
        else if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.BlindingShadow1 && _visualCast != null)
            _visualResolved = true;
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell == _visualCast && !_visualResolved)
            Clear();
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        Update();
        if (_activation != default)
            hints.Add("Raidwide: Blinding Shadow (timing estimated)");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        Update();
        if (_activation == default)
            return;
        BitMask affected = default;
        foreach (var (s, _) in Raid.WithSlot())
            affected.Set(s);
        if (affected != default)
            hints.AddPredictedDamage(affected, _activation);
    }

    public override void Update()
    {
        if (Module.PrimaryActor.IsDead || Module.PrimaryActor.IsDestroyed
            || _activation != default && WorldState.CurrentTime > _activation.AddSeconds(2d))
            Clear();
    }

    public override void OnActorUntargetable(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            Clear();
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            Clear();
    }

    private void Clear()
    {
        _visualCast = null;
        _visualResolved = false;
        _activation = default;
    }
}
