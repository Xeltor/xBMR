// Adapted from XCAI 4a0f7968; external RSR selection remains scoped to its original update.
using System;
using System.Collections.Generic;
using System.Linq;

namespace BossMod.CombatAssistance.Support;

// A raise opens one recovery episode. Reaching full HP consumes it, including
// when somebody else's heal completes the recovery. Later damage cannot reopen it.
internal sealed class RaisedHealingPolicy
{
    private readonly Dictionary<ulong, (DateTime Expires, bool Revived)> pending = [];

    public void ObserveRaise(ulong actor, DateTime now)
    {
        if (actor == 0 || this.pending.ContainsKey(actor)) return;
        if (this.pending.Count >= 16) return;
        this.pending[actor] = (now.AddSeconds(90), false);
    }

    public bool Observe(ulong actor, bool visible, bool dead, bool party, uint hp, uint maxHp, DateTime now)
    {
        if (!this.pending.TryGetValue(actor, out var episode)) return false;
        if (now >= episode.Expires || party || episode.Revived && (!visible || dead) ||
            visible && !dead && maxHp > 0 && hp >= maxHp)
        {
            this.pending.Remove(actor);
            return false;
        }
        if (!visible || dead || hp == 0 || maxHp == 0) return false;
        if (!episode.Revived) this.pending[actor] = (now.AddSeconds(30), true);
        return true;
    }

    public ulong[] Actors => this.pending.Keys.ToArray();
    public void Clear() => this.pending.Clear();
}
