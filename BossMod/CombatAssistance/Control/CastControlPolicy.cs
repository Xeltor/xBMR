using System;

namespace BossMod.CombatAssistance.Control;

internal enum CastControlChoice { Wait, Interrupt, Stun, Dodge, Submitted }

internal readonly record struct CastControlInput(
    bool Interruptible, bool InterruptRequired, bool StunRequired, bool OptionalControlUseful,
    bool MovementStarted, float Elapsed, float Remaining, bool InterruptReady, bool StunReady,
    bool ActionWindow);

// A dodge commitment belongs to one observed cast, not to the actor or action ID
// forever. Required encounter control can still protect the party after a dodge.
internal sealed class CastControlPolicy
{
    private uint action;
    private float elapsed;
    private DateTime observed;
    private bool dodging;
    private bool submitted;
    internal long Generation { get; private set; }
    internal DateTime SubmittedAt { get; private set; }
    internal uint SubmittedAction { get; private set; }

    internal void Observe(uint actionId, float castElapsed, DateTime now)
    {
        if (this.action != actionId || castElapsed + 0.1f < this.elapsed ||
            now < this.observed || now - this.observed > TimeSpan.FromSeconds(1))
        {
            this.Generation++;
            this.dodging = false;
            this.submitted = false;
            this.SubmittedAt = default;
            this.SubmittedAction = 0;
        }
        this.action = actionId;
        this.elapsed = castElapsed;
        this.observed = now;
    }

    internal CastControlChoice Choose(CastControlInput input)
    {
        if (this.submitted) return CastControlChoice.Submitted;
        if (!float.IsFinite(input.Elapsed) || !float.IsFinite(input.Remaining) || input.Elapsed < 0f || input.Remaining <= 0.4f)
            return CastControlChoice.Dodge;

        // Required or party-protecting control remains useful after our own dodge.
        if (input.InterruptRequired && input.Interruptible && input.InterruptReady && input.ActionWindow)
            return CastControlChoice.Interrupt;
        if (input.StunRequired && input.StunReady && input.ActionWindow)
            return CastControlChoice.Stun;
        if (input.InterruptRequired || input.StunRequired)
            return CastControlChoice.Wait;

        this.dodging |= input.MovementStarted || input.Elapsed > 0.75f;
        if (this.dodging || !input.OptionalControlUseful) return CastControlChoice.Dodge;
        if (input.Interruptible && input.InterruptReady && input.ActionWindow) return CastControlChoice.Interrupt;
        return input.StunReady && input.ActionWindow ? CastControlChoice.Stun : CastControlChoice.Wait;
    }

    internal void Submitted(DateTime now, uint actionId)
    {
        this.submitted = true;
        this.SubmittedAt = now;
        this.SubmittedAction = actionId;
    }
}
