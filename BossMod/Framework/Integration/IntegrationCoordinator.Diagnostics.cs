using BossMod.Pathfinding;
using System.Diagnostics;
using System.Threading;

namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    internal sealed record DiagnosticRequest(int SchemaVersion, string InstanceGeneration);
    private int _diagnosticRunning;
    internal Task<string> CheckSafetyBackend(string json)
    {
        var request = IntegrationSession.Parse<DiagnosticRequest>(json);
        if (request == null || request.SchemaVersion != 1 || request.InstanceGeneration != Session.InstanceGeneration || !Session.ProviderAlive)
            return Task.FromResult(IntegrationSession.Serialize(new { available = false, diagnosticOnly = true, reason = "invalid-diagnostic-generation" }));
        if (Interlocked.CompareExchange(ref _diagnosticRunning, 1, 0) != 0)
            return Task.FromResult(IntegrationSession.Serialize(new { available = false, diagnosticOnly = true, reason = "diagnostic-busy" }));
        return Task.Run(() =>
        {
            try
            {
                var deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
                bool Live() => Session.ProviderAlive && Stopwatch.GetTimestamp() < deadline;
                var map = new NavigationMapSource(new ArenaBoundsSquare(8), default, null, default);
                var time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var projected = new SDKnockbackInAABBSquareAwayFromOrigin(default, default, 5, 7);
                var independent = new SDCircle(new(0, 3), 1);
                var paths = new List<string>();
                var landing = SeparatePreparation(new SDUnion([projected, independent]), new SDUnion([projected, independent]), paths,
                    new(clock: () => 0, frequency: 1000));
                var mixed = IntegrationSafetyRaster.Build(map, [new(new SDUnion([projected, independent]), time, 0)],
                    [new(landing!, time, 0)], [], time, Live);
                var danger = IntegrationSafetyRaster.Build(map, [new(new SDCircle(default, 100), time, 0)],
                    [new(new SDCircle(default, 100), time, 0)], [], time, Live);
                var invalid = IntegrationSafetyRaster.Build(map, [new(new SDCircle(default, float.NaN), time, 0)],
                    [new(new SDCircle(default, float.NaN), time, 0)], [], time, Live);
                var empty = IntegrationSafetyRaster.Build(map, [], [], [], time, Live);
                bool AllBlocked(NavigationRaster raster)
                {
                    for (var y = 0; y < raster.Height; ++y) for (var x = 0; x < raster.Width; ++x) if (raster.BlockAt(x, y) == 0) return false;
                    return true;
                }
                var route = empty == null ? null : IntegrationSearch.Run(empty.Preparation, default, new(3, 0), 0, 6);
                var witness = route is { WitnessComplete: true, Route.Length: > 0 };
                var tests = new
                {
                    preparationSeparated = mixed != null && paths.Count == 1 && mixed.Preparation.BlockAt(26, 16) != 0 && mixed.Landing.BlockAt(26, 16) == 0,
                    independentSiblingPreserved = mixed != null && mixed.Landing.BlockAt(16, 22) != 0,
                    allDangerRelaxationPrevented = danger != null && danger.PreparationRelaxed && AllBlocked(danger.Preparation),
                    invalidDistanceBlocked = invalid != null && AllBlocked(invalid.Landing), nativeRouteWitnessComplete = witness
                };
                var complete = Live() && tests.preparationSeparated && tests.independentSiblingPreserved && tests.allDangerRelaxationPrevented
                    && tests.invalidDistanceBlocked && tests.nativeRouteWitnessComplete;
                return IntegrationSession.Serialize(new { available = true, diagnosticOnly = true, instanceGeneration = Session.InstanceGeneration,
                    completed = complete, tests, liveSourceValidated = false, liveProofIdsIssued = false,
                    meaning = "isolated synthetic native raster/search backend inside the loaded provider; no live hints, actors, settings, inputs or gameplay permission" });
            }
            catch (Exception error)
            {
                return IntegrationSession.Serialize(new { available = false, diagnosticOnly = true, instanceGeneration = Session.InstanceGeneration,
                    completed = false, reason = "native-backend-diagnostic-failed", errorKind = error.GetType().Name });
            }
            finally { Interlocked.Exchange(ref _diagnosticRunning, 0); }
        });
    }
}
