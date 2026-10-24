namespace BossMod.CombatAssistance.Support;

internal enum TankRecoveryKind { AuthoredSwap, TankDeath, LooseEnemy }
internal sealed record TankRecoveryEnemy(ulong Id, ulong Victim, int Priority, bool ShouldTank, bool PreferProvoke,
    bool Casting, bool KnownEncounter, float Distance);
internal sealed record TankRecoveryMember(ulong Id, bool Tank, bool Dead, uint HP, uint MaxHP,
    bool InCombat = false, bool Casting = false, bool Stance = false, ulong Target = 0, WPos Position = default, float Radius = 0);
internal sealed record TankRecoveryCandidate(ulong Enemy, ulong Victim, TankRecoveryKind Kind, int Priority, int Urgency, float Distance);

// Native actor identities and authored hints replace the old consumer snapshot.
// Timing below filters target flicker and elects a backup after observed death;
// it is gameplay policy, not a serialized-frame expiry or external lease.
internal sealed class TankRecoveryPolicy
{
    private readonly Dictionary<ulong, (ulong Tank, DateTime LastAlive)> _owners = [];
    private readonly Dictionary<ulong, (ulong Victim, DateTime Since)> _loose = [];
    private DateTime _last;

    internal ImmutableArray<TankRecoveryCandidate> Observe(DateTime now, ulong player,
        IReadOnlyList<TankRecoveryEnemy> enemies, IReadOnlyList<TankRecoveryMember> party,
        bool swaps, bool deaths, bool trash, Func<TankRecoveryMember, TankRecoveryEnemy, bool> inProvokeRange)
    {
        if (now < _last) Reset();
        _last = now;
        var live = enemies.Select(e => e.Id).ToHashSet();
        foreach (var id in _owners.Keys.Where(id => !live.Contains(id)).ToArray()) _owners.Remove(id);
        foreach (var id in _loose.Keys.Where(id => !live.Contains(id)).ToArray()) _loose.Remove(id);
        var candidates = ImmutableArray.CreateBuilder<TankRecoveryCandidate>();
        foreach (var enemy in enemies)
        {
            var victim = party.FirstOrDefault(p => p.Id == enemy.Victim);
            var livingTank = victim is { Tank: true, Dead: false, HP: > 0 };
            if (livingTank)
            {
                _owners[enemy.Id] = (victim!.Id, now);
                _loose.Remove(enemy.Id);
            }
            if (enemy.Priority < 0 || !enemy.ShouldTank || enemy.Victim == player)
            {
                _loose.Remove(enemy.Id);
                continue;
            }
            var urgency = Urgency(victim);
            TankRecoveryKind? kind = swaps && enemy.PreferProvoke ? TankRecoveryKind.AuthoredSwap : null;
            if (kind == null && deaths && !livingTank && !enemy.Casting
                && _owners.TryGetValue(enemy.Id, out var owner) && (now - owner.LastAlive).TotalSeconds is >= 0 and <= 8
                && party.Any(p => p.Id == owner.Tank && (p.Dead || p.HP == 0)))
            {
                var ranked = party.Where(p => p.Tank && !p.Dead && p.HP > 0 && p.InCombat
                    && (p.Target == enemy.Id || p.Id == player) && inProvokeRange(p, enemy))
                    .OrderBy(p => p.Casting ? 2 : p.Stance ? 0 : 1).ThenBy(p => p.Id).Select(p => p.Id).ToArray();
                var rank = Array.IndexOf(ranked, player);
                if (rank >= 0 && (urgency == 2 || (now - owner.LastAlive).TotalSeconds >= Math.Min(rank, 3) * 0.75))
                    kind = TankRecoveryKind.TankDeath;
            }
            var loose = !livingTank && victim is { Tank: false, Dead: false, HP: > 0 } && !enemy.Casting;
            if (!loose) _loose.Remove(enemy.Id);
            else
            {
                if (!_loose.TryGetValue(enemy.Id, out var prior) || prior.Victim != enemy.Victim)
                    _loose[enemy.Id] = prior = (enemy.Victim, now);
                if (kind == null && trash && (!enemy.KnownEncounter || enemy.PreferProvoke)
                    && (now - prior.Since).TotalSeconds >= (urgency == 2 ? 0.5 : 1.5))
                    kind = TankRecoveryKind.LooseEnemy;
            }
            if (kind is { } reason)
                candidates.Add(new(enemy.Id, enemy.Victim, reason, enemy.Priority, urgency, enemy.Distance));
        }
        var attackers = candidates.GroupBy(c => c.Victim).ToDictionary(g => g.Key, g => g.Count());
        return candidates.OrderBy(c => c.Kind == TankRecoveryKind.AuthoredSwap ? 0 : 1)
            .ThenByDescending(c => c.Priority).ThenByDescending(c => c.Urgency)
            .ThenByDescending(c => attackers[c.Victim])
            .ThenBy(c => c.Distance).ThenBy(c => c.Enemy).ToImmutableArray();
    }

    internal static int Urgency(TankRecoveryMember? member) => member is not { HP: > 0, MaxHP: > 0 } ? 0
        : member.HP <= member.MaxHP * 0.25f ? 2 : member.HP <= member.MaxHP * 0.5f ? 1 : 0;

    internal static bool CanRetarget(float distance, int held, int urgency) => distance <= (held >= 2 && urgency < 2 ? 3 : 8);
    internal void Reset() { _owners.Clear(); _loose.Clear(); _last = default; }
}
