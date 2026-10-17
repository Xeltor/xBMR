using BossMod.Pathfinding;
using System.Diagnostics;
using System.Threading;

namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    internal Task<string> CheckControlBackend(string json)
    {
        var request = IntegrationSession.Parse<DiagnosticRequest>(json);
        if (request == null || request.SchemaVersion != 1 || request.InstanceGeneration != Session.InstanceGeneration || !Session.ProviderAlive)
            return Task.FromResult(Unavailable("invalid-control-diagnostic-generation"));
        if (Interlocked.CompareExchange(ref _diagnosticRunning, 1, 0) != 0) return Task.FromResult(Unavailable("diagnostic-busy"));
        return Task.Run(() =>
        {
            try
            {
                var deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
                bool Live() => Session.ProviderAlive && Stopwatch.GetTimestamp() < deadline;
                using var local = new IntegrationSession(() => 1, 1000);
                var owner = local.Register(new(1, local.InstanceGeneration, "isolated-control-diagnostic", 2000), _ => { }).OwnerToken!;
                local.BeginFrame("isolated", true); local.Publish("hints-ready", c => IntegrationSession.Serialize(c));
                var guard = new IntegrationSession.ContributionGuard(Live); local.PairConstraintGoals(owner, guard);
                local.SubmitNoLossBatch(new(local.CurrentContext, owner, "combat", [new(null), new(null)]), [p => 1 + p.X, p => 3 - p.X]);
                var leaves = local.CaptureIndividualGoalLeaves();
                var pending = leaves.Length == 2 && leaves.All(f => f(default) == 0);
                guard.Activate(); var individual = leaves[0](Vector2.UnitX) > leaves[0](default) && leaves.Sum(f => f(default)) == leaves.Sum(f => f(Vector2.UnitX));
                guard.Retire(); var retired = leaves.All(f => f(default) == 0) && local.IsOwnerActive(owner);
                var gate = new IntegrationPacing(() => 1); var input = new IntegrationInputObservation();
                input.Assign(local.InstanceGeneration, 1, Vector3.UnitX);
                var ticket = new IntegrationPacing.Ticket("isolated", owner, local.InstanceGeneration, 1, 1, Vector3.UnitX, 0, 100, Live, true);
                var armed = gate.Queue(ticket) && gate.Arm(ticket, input.CurrentAssignment!);
                var held = gate.Apply(input.CurrentAssignment, Vector3.UnitX, default, Vector2.UnitX, Vector2.UnitX, true, true, false, false, false);
                var manual = gate.Apply(input.CurrentAssignment, Vector3.UnitX, Vector2.UnitY, Vector2.UnitX, Vector2.UnitX, true, true, false, false, false);
                var time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var map = new Map(); map.InitGrid(0.5f, default, 16, 16);
                var zones = new[] { ((ShapeDistance)new SDCircle(default, 100), time.AddSeconds(3), 0UL) };
                _ = NavigationDecision.RasterizeForbiddenZones(map, zones, time, false, out _, allowRelaxation: false);
                var future = map.PixelMaxG.Take(256).All(v => v == 2);
                _ = NavigationDecision.RasterizeForbiddenZones(map, zones, time.AddSeconds(4), false, out _, allowRelaxation: false);
                var activated = map.PixelMaxG.Take(256).All(v => v == 0);
                var unknownFloor = IntegrationPhysicalProbe.Evaluate(default, Vector3.UnitX, 0.5f, (_, _, _) => null);
                var knownWall = IntegrationPhysicalProbe.Evaluate(default, Vector3.UnitX, 0.5f,
                    (p, d, _) => d.Y < -0.9f ? new(p.X, 0, p.Z) : p + d * 0.2f);
                var tests = new
                {
                    pendingMaskWithdrawsGoals = pending, individualLeavesPreserved = individual,
                    ordinaryRetirementPreservesObserver = retired, exactAutomaticHold = armed && held.Scale == 0 && held.Output == default,
                    manualPriority = manual.Scale == 1 && manual.Output == Vector2.UnitX && gate.Current == null,
                    futureWarningRetainsTraversal = future, activatedOwnedWarningNeverRelaxed = activated,
                    unknownFloorNeverDrop = unknownFloor.Status == "unknown", nativePhysicalClassifier = knownWall.Status == "blocked" && knownWall.MovementCollision
                };
                var completed = Live() && tests.pendingMaskWithdrawsGoals && tests.individualLeavesPreserved && tests.ordinaryRetirementPreservesObserver
                    && tests.exactAutomaticHold && tests.manualPriority && tests.futureWarningRetainsTraversal && tests.activatedOwnedWarningNeverRelaxed
                    && tests.unknownFloorNeverDrop && tests.nativePhysicalClassifier;
                return IntegrationSession.Serialize(new { available = true, diagnosticOnly = true, instanceGeneration = Session.InstanceGeneration, completed, tests,
                    liveSourceValidated = false, liveProofIdsIssued = false, inputsWritten = false,
                    meaning = "isolated native ownership, timing, automatic input-gate and physical classifier fixtures inside loaded provider; no game actor, hints, input or settings modified" });
            }
            catch (Exception error) { return IntegrationSession.Serialize(new { available = false, diagnosticOnly = true, completed = false, errorKind = error.GetType().Name }); }
            finally { Interlocked.Exchange(ref _diagnosticRunning, 0); }
        });
    }
}
