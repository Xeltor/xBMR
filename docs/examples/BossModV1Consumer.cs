using System;
using System.Numerics;
using System.Text.Json;
using System.Threading;

namespace IntegrationExample;

// A compile-checked transport example, not the production XCAI migration.
// Inject Dalamud subscriber InvokeFunc delegates; no xBMR assembly reference is needed.
internal sealed class BossModV1Consumer(
    Func<string> capabilities,
    Func<string, Action<string>, string> register,
    Func<string, string> renew,
    Func<string, Func<Vector2, float>, string> submit,
    Func<string, string> unregister) : IDisposable
{
    private string? _generation;
    private string? _ownerToken;
    private Func<Vector2, float>? _score;
    private long _lastFrame;
    private int _lastStage = -1;
    internal event Action<string>? MovementProposed;
    internal event Action<string>? MovementApplied;
    internal string Status { get; private set; } = "disconnected";

    internal bool Connect()
    {
        if (_ownerToken != null)
            return true;
        using var document = JsonDocument.Parse(capabilities());
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
            return false;
        var families = root.GetProperty("families");
        foreach (var family in new[] { "frame-stages", "goal-scoring", "movement-observation" })
        {
            if (!families.TryGetProperty(family, out var version) || version.GetInt32() != 1)
                return false;
        }
        _generation = root.GetProperty("instanceGeneration").GetString();
        var request = JsonSerializer.Serialize(new { schemaVersion = 1, instanceGeneration = _generation, name = "XCAI.V1.Sample", leaseMs = 1000 });
        using var response = JsonDocument.Parse(register(request, OnFrame));
        if (!response.RootElement.GetProperty("accepted").GetBoolean())
            return false;
        _ownerToken = response.RootElement.GetProperty("ownerToken").GetString();
        _lastFrame = 0;
        _lastStage = -1;
        Status = "connected";
        return _ownerToken != null;
    }

    // Publish an immutable prepared scoring closure. Never close over live game/controller state.
    internal void SetScore(Func<Vector2, float>? score) => Volatile.Write(ref _score, score);

    private string Lease(string token) => JsonSerializer.Serialize(new
    {
        schemaVersion = 1, instanceGeneration = _generation, ownerToken = token, leaseMs = 1000
    });

    private void OnFrame(string json)
    {
        var token = _ownerToken;
        if (token == null)
            return;
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.GetProperty("available").GetBoolean())
            return;
        var context = root.GetProperty("context");
        if (context.GetProperty("schemaVersion").GetInt32() != 1 || context.GetProperty("instanceGeneration").GetString() != _generation)
            return;
        var frame = context.GetProperty("frameId").GetInt64();
        var stage = context.GetProperty("stage").GetString() switch
        {
            "hints-ready" => 0, "movement-proposed" => 1, "movement-applied" => 2, _ => -1
        };
        if (stage < 0 || frame < _lastFrame || frame == _lastFrame && stage <= _lastStage)
            return;
        _lastFrame = frame;
        _lastStage = stage;
        if (stage == 0)
        {
            using var lease = JsonDocument.Parse(renew(Lease(token)));
            if (!lease.RootElement.GetProperty("accepted").GetBoolean())
            {
                _ownerToken = null;
                Status = "lease-lost";
                return;
            }
            if (Volatile.Read(ref _score) is { } score)
            {
                var request = JsonSerializer.Serialize(new { context, ownerToken = token, category = "combat" });
                using var response = JsonDocument.Parse(submit(request, score));
                Status = response.RootElement.GetProperty("accepted").GetBoolean() ? "scoring" : "submission-rejected";
            }
        }
        else if (stage == 1)
        {
            MovementProposed?.Invoke(json);
        }
        else
        {
            MovementApplied?.Invoke(json);
        }
    }

    public void Dispose()
    {
        var token = _ownerToken;
        _ownerToken = null;
        Volatile.Write(ref _score, null);
        if (token != null)
            unregister(Lease(token));
        Status = "disconnected";
    }
}
