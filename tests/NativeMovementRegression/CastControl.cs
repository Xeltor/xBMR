using BossMod;
using BossMod.CombatAssistance.Control;
using BossMod.CombatAssistance.Integrations;

public static partial class Checks
{
    private static void CastControl()
    {
        var now = DateTime.UtcNow;
        var early = new CastControlInput(false, false, false, true, false, 0.2f, 3f, true, true, true);
        var policy = new CastControlPolicy();
        policy.Observe(100, 0.2f, now);
        Check(policy.Choose(early) == CastControlChoice.Stun, "early legal stun can prevent a dodge");
        Check(policy.Choose(early with { MovementStarted = true }) == CastControlChoice.Dodge, "movement commits this optional cast to dodging");
        Check(policy.Choose(early) == CastControlChoice.Dodge, "stopping after a dodge never unlocks a late stun");
        Check(policy.Choose(early with { Interruptible = true }) == CastControlChoice.Dodge, "optional interrupt also respects the dodge commitment");
        Check(policy.Choose(early with { Interruptible = true, InterruptRequired = true }) == CastControlChoice.Interrupt,
            "party-protecting interrupt remains useful after personal movement");
        Check(policy.Choose(early with { StunRequired = true }) == CastControlChoice.Stun, "explicit encounter stun overrides an optional dodge commitment");
        policy.Observe(100, 0.01f, now.AddMilliseconds(200));
        Check(policy.Choose(early) == CastControlChoice.Stun, "a new cast of the same action gets a fresh choice");
        policy.Submitted(now, 7540);
        Check(policy.Choose(early) == CastControlChoice.Submitted, "submission cannot consume two actions for the same cast");
        policy.Observe(101, 0.1f, now.AddMilliseconds(300));
        Check(policy.Choose(early with { Interruptible = true }) == CastControlChoice.Interrupt, "prefer a real interrupt over consuming stun resistance");
        Check(policy.Choose(early with { ActionWindow = false }) == CastControlChoice.Wait, "optional control never clips the rotation");
        Check(policy.Choose(early with { Elapsed = 0.8f }) == CastControlChoice.Dodge, "missed early opportunity commits to the dodge");
        Check(policy.Choose(early) == CastControlChoice.Dodge, "cooldown recovery cannot reverse a dodge commitment");
        policy.Observe(102, 0.1f, now.AddMilliseconds(400));
        Check(policy.Choose(early with { Remaining = 0.2f }) == CastControlChoice.Dodge, "do not submit after the response margin expires");
        Check(policy.Choose(early with { Remaining = float.NaN }) == CastControlChoice.Dodge, "invalid cast timing cannot authorize an action");
        Check(policy.Choose(early with { OptionalControlUseful = false }) == CastControlChoice.Dodge, "unrelated casts do not consume optional stuns");
        Check(policy.Choose(early with { StunReady = false }) == CastControlChoice.Wait, "unavailable action leaves BMR in control");

        var immunity = new CastControlImmunity();
        Check(!immunity.ConfirmedSusceptible(1), "unknown susceptibility has no positive evidence yet");
        immunity.ObserveEffect(1, 139, 14, 2);
        Check(immunity.ConfirmedSusceptible(1) && !immunity.Blocks(1), "a real party stun provides positive target evidence");
        immunity.ObserveEffect(1, 7540, 14, 2);
        Check(!immunity.ConfirmedSusceptible(1) && immunity.Blocks(1), "stop conservatively before another diminishing stun");
        immunity.ObserveEffect(2, 7540, 55, 2);
        Check(immunity.Blocks(2), "server full-resist effect blocks repeat stun expenditure");
        immunity.ObserveEffect(3, 7863, 8, 0);
        Check(immunity.Blocks(3), "explicit no-effect response blocks repeat stun expenditure");
        immunity.ObserveEffect(4, 7538, 8, 0);
        Check(!immunity.Blocks(4), "a raced interrupt does not falsely classify future stuns as immune");
        Check(CastControlImmunity.BlocksStun(39) && CastControlImmunity.BlocksStun(1349) &&
            CastControlImmunity.BlocksStun(426) && CastControlImmunity.BlocksStun(470) && CastControlImmunity.BlocksStun(2),
            "live-sheet resistance buffs and existing stun override encounter hints");
        Check(!CastControlImmunity.BlocksStun(1250), "unrelated buffs do not become immunity evidence");
        immunity.Reset();
        Check(!immunity.Blocks(1) && !immunity.Blocks(2) && !immunity.ConfirmedSusceptible(1), "resistance evidence ends with the encounter");

        for (ulong id = 100; id < 400; ++id) immunity.ObserveEffect(id, 7540, 55, 2);
        Check(immunity.Blocks(399), "encounter resistance has no old transport target cap");
        Check(RsrCastControlSelection.Actions(19).Any(a => a.Id == 16)
            && RsrCastControlSelection.Actions(23).Single().Id == 7551
            && RsrCastControlSelection.Actions(41).Single().Id == 7863
            && RsrCastControlSelection.Actions(24).Length == 0, "actual role controls and explicit PLD GCD fallback only");
        var hints = new AIHints();
        var actor = (Actor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        actor.InstanceID = 42;
        var cast = new ActorCastInfo { Action = new(ActionType.Spell, 123), TotalTime = 3, TargetID = 1 };
        actor.CastInfo = cast;
        var shape = new AOEShapeCircle(5).Distance(default, default);
        hints.AddForbiddenZoneFromCast(shape, now.AddSeconds(3), 42, cast);
        Check(NativeCastControl.Threatens(hints, actor, default), "producer-recorded cast warns at the native current point");
        Check(!NativeCastControl.Threatens(hints, actor, new(10, 0)), "warning provenance is not enough without an affected position");
        actor.CastInfo = cast.Clone();
        Check(!NativeCastControl.Threatens(hints, actor, default), "same action on a replacement cast cannot reuse stale warning identity");
        actor.CastInfo = cast;
        cast.EventHappened = true;
        Check(!NativeCastControl.Threatens(hints, actor, default), "completed cast cannot authorize an optional stun");
        cast.EventHappened = false;
        cast.TargetID = 2;
        Check(!NativeCastControl.Threatens(hints, actor, default), "mutated cast identity loses warning correlation");
        hints.Clear();
        for (var i = 0; i < 200; ++i)
            hints.AddForbiddenZoneFromCast(shape, now.AddSeconds(3), 42, cast);
        Check(NativeCastControl.Threatens(hints, actor, default), "native cast observations have no obsolete 128-zone transport cap");
    }
}
