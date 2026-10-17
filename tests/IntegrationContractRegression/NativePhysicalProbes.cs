using BossMod.Integration;

static partial class Checks
{
    private static void NativePhysicalProbes()
    {
        Vector3? Flat(Vector3 p, Vector3 d, float length) => d.Y < -0.9f ? new(p.X, 0, p.Z) : null;
        var from = Vector3.Zero; var step = new Vector3(2, 0, 0);
        var clear = IntegrationPhysicalProbe.Evaluate(from, step, 0.5f, Flat);
        Check(clear.Status == "clear" && clear.CurrentSupportVerified && clear.AheadSupportVerified
            && clear.ProbePosition![0] == 1.25f, "native physical probe preserves supported floor and clamps lookahead to 1.25y");
        var unknown = IntegrationPhysicalProbe.Evaluate(from, step, 0.5f, (_, _, _) => null);
        Check(unknown.Status == "unknown" && !unknown.CurrentSupportVerified, "missing current floor is unknown, never a proven drop");
        var ledge = IntegrationPhysicalProbe.Evaluate(from, step, 0.5f,
            (p, d, n) => d.Y < -0.9f && p.X <= 0.7f ? new(p.X, 0, p.Z) : null);
        Check(ledge.Status == "blocked" && ledge.CurrentSupportVerified && ledge.Reason == "observed-no-ground-support-ahead",
            "actual supported origin followed by a complete negative floor observation identifies a proposed unsupported step");
        var drop = IntegrationPhysicalProbe.Evaluate(from, step, 0.5f,
            (p, d, n) => d.Y < -0.9f ? new(p.X, p.X > 0.7f ? -3 : 0, p.Z) : null);
        Check(drop.Status == "blocked" && drop.Drop == 3, "native observed drop over two yalms blocks the proposed step");
        var wall = IntegrationPhysicalProbe.Evaluate(from, step, 0.5f,
            (p, d, n) => d.Y < -0.9f ? new(p.X, 0, p.Z) : p + d * 0.2f);
        Check(wall.Status == "blocked" && wall.MovementCollision && wall.CurrentSupportVerified,
            "native foot/torso collision remains distinct from missing floor support");
        Check(IntegrationPhysicalProbe.Evaluate(from, step, 0.5f, (_, _, _) => throw new InvalidOperationException()).Status == "unknown",
            "native query failure cannot authorize a physical brake");
        Check(IntegrationPhysicalProbe.Evaluate(from, step, 0.5f, (_, _, _) => new(float.NaN, 0, 0)).Status == "unknown",
            "nonfinite native floor observations never become physical evidence");
    }
}
