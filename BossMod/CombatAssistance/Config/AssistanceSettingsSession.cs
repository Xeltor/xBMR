using Newtonsoft.Json;

namespace BossMod.CombatAssistance.Config;

// Framework-thread owner of settings. The ordered IO queue receives serialized values;
// background work never reaches the mutable preferences displayed by the UI.
internal sealed class AssistanceSettingsSession : IAsyncDisposable
{
    private sealed record PendingLoad(long Generation, Task<AssistanceSettingsLoad> Task);
    private sealed record PendingSave(long Generation, Task<string?> Task, AssistanceSettingsDocument Document, bool Recovery);
    private readonly AssistanceSettingsStore _store;
    private readonly AssistancePreferences _unavailable = new() { Enabled = false };
    private Task _io = Task.CompletedTask;
    private PendingLoad? _pendingLoad;
    private PendingSave? _pendingSave;
    private bool? _standalone;
    private long _generation;
    private bool _disposed;
    internal AssistanceSettingsLoad Current { get; private set; } = new(AssistanceSettingsState.Unavailable, null, "Loading combat assistance settings.");
    internal AssistancePreferences Preferences => Current.Document?.Preferences ?? _unavailable;
    internal bool Active { get; set; }
    internal bool MayAssist => Active && !_disposed && _standalone == false && Current.MayAssist;
    internal bool MayEdit => !_disposed && _standalone == false && Current.State == AssistanceSettingsState.Ready;

    internal AssistanceSettingsSession(AssistanceSettingsStore store) => _store = store;

    internal void ObserveStandalone(bool loaded)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_standalone == loaded)
            return;
        _standalone = loaded;
        var generation = ++_generation;
        Current = new(loaded ? AssistanceSettingsState.StandaloneLoaded : AssistanceSettingsState.Unavailable,
            Current.Document, loaded ? "XCAI removal is pending; integrated assistance and settings import are inactive until it finishes." : "Loading combat assistance settings.");
        _pendingSave = null;
        var load = _io.ContinueWith(_ => _store.Load(loaded), TaskScheduler.Default);
        _io = load;
        _pendingLoad = new(generation, load);
    }

    // Only inspect completed tasks; no disk IO or waits occur in a frame.
    internal void Poll()
    {
        if (_disposed)
            return;
        if (_pendingLoad is { Task.IsCompleted: true } load)
        {
            _pendingLoad = null;
            if (load.Generation == _generation)
                Current = load.Task.IsCompletedSuccessfully ? load.Task.Result
                    : new(AssistanceSettingsState.Unavailable, null, load.Task.Exception?.GetBaseException().Message ?? "Settings load failed.");
            _ = load.Task.Exception;
        }
        if (_pendingSave is { Task.IsCompleted: true } save)
        {
            _pendingSave = null;
            if (save.Generation == _generation)
            {
                var failure = save.Task.IsCompletedSuccessfully ? save.Task.Result : save.Task.Exception?.GetBaseException().Message ?? "Settings save failed.";
                if (failure != null)
                    Current = new(AssistanceSettingsState.Unavailable, Current.Document, failure);
                else if (save.Recovery)
                    Current = new(AssistanceSettingsState.Ready, save.Document, "Legacy settings imported; current encounter settings preserved.");
            }
            _ = save.Task.Exception;
        }
    }

    internal void Save()
    {
        if (!MayEdit || Current.Document == null)
            return;
        Preferences.Clamp();
        QueueSave(Current.Document, false);
    }

    // The old plugin has unloaded and its provider generation cannot own native state.
    // Preserve the current saved BMR choice rather than guessing that a historical false
    // value was ours to undo. Keep an explicit audit and the original legacy file.
    internal void ReconcileRecovery(IReadOnlyDictionary<uint, bool> nativeSavedChoices)
    {
        if (_disposed || _standalone != false || Current.State != AssistanceSettingsState.RecoveryPending
            || Current.Document is not { } document || _pendingSave != null)
            return;
        var preferences = document.Preferences;
        if (preferences.BmrHallwayModulesToRestore.Any(oid => !nativeSavedChoices.ContainsKey(oid)))
            return;
        var reconciled = JsonConvert.DeserializeObject<AssistanceSettingsDocument>(JsonConvert.SerializeObject(document))!;
        foreach (var oid in preferences.BmrHallwayModulesToRestore)
            reconciled.RecoveryReconciliation.Add(new(oid, nativeSavedChoices[oid], preferences.BmrHallwayRestoreGeneration,
                preferences.BmrHallwayRestoreUserRevisions.TryGetValue(oid, out var revision) ? revision : null, DateTime.UtcNow));
        reconciled.Preferences.BmrHallwayModulesToRestore.Clear();
        reconciled.Preferences.BmrHallwayRestoreUserRevisions.Clear();
        reconciled.Preferences.BmrHallwayRestoreGeneration = null;
        QueueSave(reconciled, true);
    }

    private void QueueSave(AssistanceSettingsDocument document, bool recovery)
    {
        var snapshot = JsonConvert.SerializeObject(document);
        var save = _io.ContinueWith(_ =>
        {
            try
            {
                _store.Save(JsonConvert.DeserializeObject<AssistanceSettingsDocument>(snapshot)
                    ?? throw new JsonSerializationException("Missing settings snapshot."));
                return (string?)null;
            }
            catch (Exception error) { return error.Message; }
        }, TaskScheduler.Default);
        _io = save;
        _pendingSave = new(_generation, save, document, recovery);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        // Unload awaits persistence asynchronously, after the windows stop editing.
        try { await _io.ConfigureAwait(false); }
        catch (Exception) { } // observed and exposed by Poll while active; shutdown cannot retry safely
    }
}
