using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;

namespace IntegrationExample;

// Discovery must independently require cast-control-observation:1. Call on the native
// callback thread. Handle IPC disappearance/malformed transport at the caller boundary.
internal sealed class BossModV1CastControl(Func<string, string> query)
{
    internal sealed record Threat(string ActorId, string CastId, uint ActionId, bool InterruptRequested,
        bool StunRequested, string NativeDeadlineTicks, string WorldTimeTicks, string WorldTimeKind);
    internal sealed record Result(bool Available, bool ThreatCoverageComplete, Threat[] PublishedThreats);
    private static Result Unknown() => new(false, false, []);

    internal Result Read(JsonElement context, Vector2 point)
    {
        if (context.GetProperty("schemaVersion").GetInt32() != 1 || !float.IsFinite(point.X) || !float.IsFinite(point.Y))
            return Unknown();
        using var document = JsonDocument.Parse(query(JsonSerializer.Serialize(new { context, points = new[] { new[] { point.X, point.Y } } })));
        var root = document.RootElement;
        if (!root.GetProperty("available").GetBoolean()) return Unknown();
        var echoed = root.GetProperty("context");
        foreach (var field in new[] { "schemaVersion", "instanceGeneration", "frameId", "stage" })
            if (echoed.GetProperty(field).GetRawText() != context.GetProperty(field).GetRawText()) return Unknown();
        if (!root.GetProperty("complete").GetBoolean() || !root.GetProperty("pointQueriesComplete").GetBoolean()) return Unknown();
        var threats = new List<Threat>();
        foreach (var target in root.GetProperty("targets").EnumerateArray())
        {
            var cast = target.GetProperty("cast");
            if (target.GetProperty("priority").GetInt32() < 0 || target.GetProperty("pendingDead").GetBoolean()
                || !target.GetProperty("isTargetable").GetBoolean() || cast.ValueKind == JsonValueKind.Null
                || cast.GetProperty("eventHappened").GetBoolean()
                || cast.GetProperty("remainingSeconds").ValueKind != JsonValueKind.Number
                || cast.GetProperty("remainingSeconds").GetSingle() <= 0
                || cast.GetProperty("deadlineTicks").ValueKind != JsonValueKind.String
                || cast.GetProperty("deadlineKind").GetString() != root.GetProperty("worldTime").GetProperty("kind").GetString()) continue;
            var interrupt = target.GetProperty("interruptRequested").GetBoolean() && cast.GetProperty("interruptible").GetBoolean();
            var stun = target.GetProperty("stunRequested").GetBoolean();
            if (!interrupt && !stun) continue;
            var actorId = target.GetProperty("actorId").GetString()!;
            var castId = cast.GetProperty("castId").GetString()!;
            var actionId = cast.GetProperty("actionId").GetUInt32();
            foreach (var zone in root.GetProperty("zones").EnumerateArray())
            {
                var distance = zone.GetProperty("distances")[0];
                if (zone.GetProperty("correlation").GetString() != "producer-recorded-cast"
                    || zone.GetProperty("sourceActorId").GetString() != actorId || zone.GetProperty("castId").GetString() != castId
                    || zone.GetProperty("actionId").GetUInt32() != actionId || distance.ValueKind != JsonValueKind.Number
                    || !float.IsFinite(distance.GetSingle()) || distance.GetSingle() > 0) continue;
                threats.Add(new(actorId, castId, actionId, interrupt, stun, cast.GetProperty("deadlineTicks").GetString()!,
                    root.GetProperty("worldTime").GetProperty("ticks").GetString()!, root.GetProperty("worldTime").GetProperty("kind").GetString()!));
                break;
            }
        }
        // Even an empty result is not proof of no threat. Consumers still own action legality,
        // range, immunity and scheduling; the native estimate is not a server interrupt cutoff.
        return new(true, root.GetProperty("castThreatCoverageComplete").GetBoolean(), threats.ToArray());
    }
}
