using System.Collections.Generic;

namespace BossMod.CombatAssistance.Control;

// Bounded current-encounter resistance; cleared out of combat or on territory changes.
internal sealed class CastControlImmunity
{
    private readonly object sync = new();
    private readonly HashSet<ulong> resisted = [];
    private readonly Dictionary<ulong, int> stuns = [];

    internal static bool IsStunAction(uint action) => action is 7540 or 7863 or 16;

    // IDs verified against the live Status sheet. Include nonstandard Stun and
    // explicit stun-nullification buffs, not similarly named positional effects.
    internal static bool BlocksStun(uint status) => status is
        2 or 142 or 149 or 201 or 1343 or 1513 or 1521 or 1522 or 2656 or 2953 or 3408 or 3465 or 4163 or 4374 or 4378 or 4433 or
        39 or 426 or 470 or 1303 or 1349 or 1983 or 1984 or 2663 or 3054 or 3673 or 4096 or 4477;

    internal void ObserveEffect(ulong target, uint action, byte type, uint value)
    {
        if (target == 0) return;
        lock (this.sync)
        {
            // Verified BMR effect contract: 55/2 means fully resisted Stun.
            // Any source can establish resistance; Holy and other party stuns
            // also consume the target's diminishing-return budget.
            if ((type is 55 or 20 && value == 2) || IsStunAction(action) && type is 1 or 2 or 7 or 8)
            {
                this.resisted.Add(target);
            }
            if (type == 14 && value == 2)
            {
                this.stuns[target] = this.stuns.GetValueOrDefault(target) + 1;
            }
        }
    }

    internal void MarkUnconfirmed(ulong target)
    {
        lock (this.sync)
            this.resisted.Add(target);
    }

    internal bool Blocks(ulong target)
    {
        lock (this.sync)
            // Conservatively stop before the third observed application rather
            // than spending control at the edge of diminishing resistance.
            return this.resisted.Contains(target) || this.stuns.GetValueOrDefault(target) >= 2;
    }

    internal bool ConfirmedSusceptible(ulong target)
    {
        lock (this.sync)
            return this.stuns.GetValueOrDefault(target) == 1 && !this.resisted.Contains(target);
    }

    internal void Reset()
    {
        lock (this.sync)
        {
            this.resisted.Clear();
            this.stuns.Clear();
        }
    }
}
