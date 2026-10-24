namespace BossMod.CombatAssistance.Runtime;

internal interface ILegacyXcaiRetirementOperations
{
    object[] FindInstallations();
    void ObserveInstallations(bool installed);
    Task Unload(object installation);
    void RemoveUnloaded(object installation);
    void ReportFailure(Exception error);
}

// Framework-thread owner. An unload may finish asynchronously, but removal and
// inventory checks run on the framework thread. Never wait for disposal in a frame.
internal sealed class LegacyXcaiRetirement(ILegacyXcaiRetirementOperations operations) : IAsyncDisposable
{
    private Task? _unload;
    private object? _target;
    private bool _unloaded;
    private DateTime _nextCheck;
    private bool _disposed;
    internal bool BlocksAssistance { get; private set; }

    internal void Update(bool loggedIn, bool safeToUnload, DateTime now)
    {
        if (_disposed || now < _nextCheck)
            return;
        _nextCheck = now.AddSeconds(1);
        try
        {
            var installed = operations.FindInstallations();
            BlocksAssistance = installed.Length != 0 || _unload != null;
            operations.ObserveInstallations(BlocksAssistance);
            if (_unload is { IsCompleted: false })
                return;
            if (_unload is { } completion)
            {
                _unload = null;
                completion.GetAwaiter().GetResult(); // already completed; observes unload failure
                _unloaded = true;
            }
            if (!loggedIn)
                return;
            if (_target != null && !installed.Any(item => ReferenceEquals(item, _target)))
            {
                _target = null; // The installer changed; never act on an old identity.
                _unloaded = false;
            }
            _target ??= installed.FirstOrDefault();
            if (_target == null)
            {
                BlocksAssistance = false;
                return;
            }
            if (!safeToUnload)
                return;
            // After a completed unload, a second identity/state check in the adapter
            // is required before the standard installer removal is allowed.
            if (!_unloaded)
            {
                _unload = operations.Unload(_target);
                if (!_unload.IsCompleted)
                    return;
                var completed = _unload;
                _unload = null;
                completed.GetAwaiter().GetResult();
                _unloaded = true;
            }
            operations.RemoveUnloaded(_target);
            _target = null;
            _unloaded = false;
            BlocksAssistance = operations.FindInstallations().Length != 0;
            operations.ObserveInstallations(BlocksAssistance);
        }
        catch (Exception error)
        {
            // No retry storm and no assistance/import while an unload is uncertain.
            BlocksAssistance = true;
            _unloaded = false;
            _nextCheck = now.AddSeconds(30);
            operations.ReportFailure(error);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        if (_unload != null)
        {
            try { await _unload.ConfigureAwait(false); }
            catch (Exception error) { operations.ReportFailure(error); }
        }
        // Do not remove a plugin after this owner has stopped.
    }
}
