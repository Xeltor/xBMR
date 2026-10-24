namespace BossMod.CombatAssistance.Planning;

// Framework-thread acknowledgement of one already selected native preparation.
// This owns no movement/action writer and never advances on request acceptance.
internal sealed class NativePreparationProgress
{
    internal ulong Actor { get; private set; }
    internal ulong Target { get; private set; }
    internal DateTime Started { get; private set; }
    internal ImmutableArray<uint> Remaining { get; private set; } = [];
    private uint _sequence;
    internal bool Active => !Remaining.IsEmpty;

    internal bool Approve(ulong actor, ulong target, ImmutableArray<uint> remaining, uint action, DateTime now)
    {
        if (actor == 0 || target == 0 || remaining.IsDefaultOrEmpty || remaining[0] != action) return false;
        if (Active) return Actor == actor && Target == target && Remaining.SequenceEqual(remaining);
        Actor = actor; Target = target; Started = now; Remaining = remaining; _sequence = 0;
        return true;
    }

    internal void Observe(ulong actor, uint action, ulong target, uint sequence)
    {
        if (!Active || actor != Actor || sequence != 0 && sequence == _sequence) return;
        _sequence = sequence;
        if (Remaining[0] != action || target != (action is 2259 or 18806 or 7389 ? Actor : Target)) { Reset(); return; }
        Remaining = Remaining.RemoveAt(0);
        if (Remaining.IsEmpty) Reset();
    }

    internal void Reset() { Actor = Target = 0; Started = default; Remaining = []; _sequence = 0; }
}
