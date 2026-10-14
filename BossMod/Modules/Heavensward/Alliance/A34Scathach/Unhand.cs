namespace BossMod.Heavensward.Alliance.A34Scathach;

// The two recorded self-casts damage seven players and apply Vulnerability Up.
// Eligibility is uncalibrated: warn from the cast without an all-player damage
// mask or a hard radius. A dying limb still delivers its already committed hit.
class Unhand(BossModule module) : BossComponent(module)
{
    private sealed record Pending(Actor Caster, ActorCastInfo Cast, DateTime Activation);
    private readonly List<Pending> _pending = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        Update();
        if (caster.OID == (uint)OID.ShadowLimb && spell.Action.ID == (uint)AID.Unhand
            && !caster.IsDestroyed && !Module.PrimaryActor.IsDead && !Module.PrimaryActor.IsDestroyed
            && !_pending.Exists(p => p.Caster == caster))
            _pending.Add(new(caster, spell, Module.CastFinishAt(spell)));
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        Update();
        if (_pending.Count > 0)
        {
            var remaining = Math.Max(0, (_pending.Min(p => p.Activation) - WorldState.CurrentTime).TotalSeconds);
            hints.Add($"Unhand: incoming damage in {remaining:f1}s (target eligibility unverified)");
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.Unhand)
            _pending.RemoveAll(p => p.Caster == caster);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.Unhand)
            _pending.RemoveAll(p => p.Caster == caster && p.Cast == spell);
    }

    public override void Update()
    {
        if (Module.PrimaryActor.IsDead || Module.PrimaryActor.IsDestroyed)
            _pending.Clear();
        else
            _pending.RemoveAll(p => p.Caster.IsDestroyed || WorldState.CurrentTime > p.Activation.AddSeconds(2d));
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            _pending.Clear();
        else
            _pending.RemoveAll(p => p.Caster == actor);
    }

    public override void OnActorUntargetable(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            _pending.Clear();
    }
}
