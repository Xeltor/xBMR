using BossMod.ReplayAnalysis.Evidence;
using System;
using System.Collections.Generic;

namespace BossMod.CombatAssistance.Sharing;

// The public intake accepts 600 attempts per rolling minute, across clients.
// Burst while capacity is available; keep only this worker's bounded request
// timestamps. Remote Retry-After remains authoritative for shared contention.
internal sealed class DropoffRequestBudget
{
    internal const int Limit = 600;
    private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(60250);
    private readonly Queue<TimeSpan> attempts = new(Limit);

    public TimeSpan Reserve(TimeSpan elapsed)
    {
        while (this.attempts.TryPeek(out var first) && elapsed - first >= Window)
        {
            this.attempts.Dequeue();
        }
        if (this.attempts.Count == Limit)
        {
            return Window - (elapsed - this.attempts.Peek());
        }
        this.attempts.Enqueue(elapsed);
        return TimeSpan.Zero;
    }
}
