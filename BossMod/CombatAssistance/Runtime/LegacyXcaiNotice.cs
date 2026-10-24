using Newtonsoft.Json;
using System.IO;

namespace BossMod.CombatAssistance.Runtime;

internal enum LegacyXcaiNoticeState { None, RemovalPending, Removed, Acknowledged }

// A durable receipt, independent of the assistance settings being imported.
// Only completed removal can display the modal; acknowledgement never gates removal.
internal sealed class LegacyXcaiNotice : IAsyncDisposable
{
    private sealed record Receipt(int FormatVersion, LegacyXcaiNoticeState State);
    private readonly Task<LegacyXcaiNoticeState> _load;
    private readonly string _path;
    private readonly Action<Exception> _report;
    private Task _io;
    private bool _loaded;
    private bool _disposed;
    private bool? _installed;
    private bool _detected;
    internal LegacyXcaiNoticeState State { get; private set; }
    internal bool ShouldShow => !_disposed && _loaded && _installed == false && State == LegacyXcaiNoticeState.Removed;

    internal LegacyXcaiNotice(string directory, Action<Exception> report)
    {
        _path = Path.Combine(directory, "xcai-retirement-notice.json");
        _report = report;
        _load = Task.Run(() =>
        {
            try
            {
                if (!File.Exists(_path)) return LegacyXcaiNoticeState.None;
                var receipt = JsonConvert.DeserializeObject<Receipt>(File.ReadAllText(_path));
                if (receipt is not { FormatVersion: 1 } || !Enum.IsDefined(receipt.State))
                    throw new InvalidDataException("Invalid XCAI retirement notice receipt.");
                return receipt.State;
            }
            catch (Exception error)
            {
                _report(error);
                return LegacyXcaiNoticeState.None;
            }
        });
        _io = _load;
    }

    internal void ObserveInstallations(bool installed)
    {
        if (_disposed) return;
        _installed = installed;
        _detected |= installed;
        Poll();
    }

    internal void Poll()
    {
        if (_disposed || !_load.IsCompleted)
            return;
        ApplyObservation();
    }

    private void ApplyObservation()
    {
        if (!_loaded)
        {
            State = _load.GetAwaiter().GetResult(); // completed IO only
            _loaded = true;
        }
        if (_detected && State == LegacyXcaiNoticeState.None)
            Save(LegacyXcaiNoticeState.RemovalPending);
        if (_installed == false && State == LegacyXcaiNoticeState.RemovalPending)
            Save(LegacyXcaiNoticeState.Removed);
    }

    internal void Acknowledge()
    {
        if (ShouldShow)
            Save(LegacyXcaiNoticeState.Acknowledged);
    }

    private void Save(LegacyXcaiNoticeState state)
    {
        State = state;
        var json = JsonConvert.SerializeObject(new Receipt(1, state), Formatting.Indented);
        _io = _io.ContinueWith(_ =>
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                var temporary = _path + ".tmp";
                File.WriteAllText(temporary, json);
                File.Move(temporary, _path, overwrite: true);
            }
            catch (Exception error) { _report(error); }
        }, TaskScheduler.Default);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        // Preserve detection/completion even if shutdown wins the initial read race.
        // Windows and frame callbacks are already stopped by the native owner.
        await _load.ConfigureAwait(false);
        ApplyObservation();
        await _io.ConfigureAwait(false);
    }
}
