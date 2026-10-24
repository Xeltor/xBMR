using BossMod;
using BossMod.CombatAssistance.Planning;
using System.Numerics;

public static partial class Checks
{
    private static void IngressRecovery()
    {
        var now = DateTime.UtcNow;
        var hints = new AIHints();
        Vector3? Flat(Vector3 p, Vector3 direction, float _) => direction.Y < -0.9f ? new(p.X, 0, p.Z) : null;
        bool Route(Func<Vector3, Vector3, float, Vector3?>? ray = null, Func<Vector3, bool>? loaded = null)
            => NativeIngressRecovery.Validate(default, new(6, 0), 0.5f, 6, hints, now, ray ?? Flat, loaded ?? (_ => true), out _);
        Check(Route(), "native entry accepts an observed supported corridor");
        Check(!Route(loaded: p => p.X < 4), "entry requires loaded collision along the entire route");
        Check(!Route((p, d, _) => d.Y < -0.9f && p.X is > 2 and < 4 ? null : Flat(p, d, 0)),
            "supported endpoints do not conceal a hole in the middle of entry");
        Check(!Route((p, d, _) => d.Y < -0.9f ? Flat(p, d, 0) : p.X > 3 ? p + d * 0.2f : null),
            "entry checks walls beyond the initial movement step");
        Check(!Route((_, _, _) => null), "missing current floor evidence withholds entry");
        hints.ImminentSpecialMode = (AIHints.SpecialMode.NoMovement, now.AddSeconds(0.5), default);
        Check(!Route(), "an upcoming authored movement prohibition applies before arena entry");
        hints.ImminentSpecialMode = (AIHints.SpecialMode.Pyretic, now, default);
        Check(!Route(), "active pyretic cannot be bypassed by being outside the arena");
        hints.ImminentSpecialMode = default;
        hints.ForbiddenZones.Add((new SDCircle(new(3, 0), 0.75f), now.AddSeconds(5), 1));
        Check(Route(), "entry may cross a warning when the complete route clears its deadline");
        hints.ForbiddenZones[0] = (new SDCircle(new(3, 0), 0.75f), now, 1);
        Check(!Route(), "active warning blocks the same corridor");
        hints.ForbiddenZones[0] = (new SDCircle(new(6, 0), 0.75f), now.AddSeconds(10), 1);
        Check(!Route(), "entry cannot stop in a future warning without onward escape");
        hints.ForbiddenZones.Clear();
        hints.TemporaryObstacles.Add(new SDCircle(new(3, 0), 0.75f));
        Check(!Route(), "authored temporary obstacles remain authoritative during entry");
        hints.TemporaryObstacles.Clear();
        hints.PathfindMapArenaProjectionLayer = 1;
        Check(!Route(), "unsupported projected entry never becomes a blind straight walk");
        hints.PathfindMapArenaProjectionLayer = null;
        hints.Teleporters.Add(new(new(3, 0), new(6, 0), 1, false));
        Check(!Route(), "entry does not mistake an authored teleporter for ordinary ground");
    }
}
