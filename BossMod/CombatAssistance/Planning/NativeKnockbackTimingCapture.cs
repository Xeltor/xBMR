using BossMod.CombatAssistance.Knockback;

namespace BossMod.CombatAssistance.Planning;

// Geometry always comes from BMR. The session observer supplies only native
// effect timing and cast identity; no learned geometry or legacy store is read.
internal static class NativeKnockbackTimingCapture
{
    internal static bool Bind(NativeKnockbackObserver observer, WorldState world, Actor player, NativeKnockbackPhases captured,
        out NativeKnockbackPhases bound, out string? reason, NativeKnockbackPhase? held = null)
    {
        bound = captured;
        reason = null;
        var result = ImmutableArray.CreateBuilder<NativeKnockbackPhase>();
        foreach (var phase in captured.Phases)
        {
            if (!observer.TryBind(phase.Source, out var timing, out var generation, out var revision, out reason)) return false;
            if (held is { Timing: { } original } && held.Source.SameGeometry(phase.Source)
                && held.CastGeneration == generation && held.TimingEvidenceRevision == revision)
                timing = original;
            result.Add(phase with { Timing = timing, CastGeneration = generation, TimingEvidenceRevision = revision, LandingRadius = player.HitboxRadius });
        }
        bound = captured with { Phases = result.ToImmutable(), Detail = "native authored geometry with session native-effect timing" };
        return true;
    }
}
