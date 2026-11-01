using BossMod.AI;

namespace BossMod.CombatAssistance.UI;

// Keep the native DrawUI preference in sync with the shared settings host without
// treating page visibility or unrelated native setting changes as AI activation.
internal sealed class AutomovementSettingsVisibility : IDisposable
{
    private readonly AIConfig _config;
    private readonly Action _open;
    private readonly Action _close;
    private readonly EventSubscription _subscription;
    private bool? _observed;

    internal AutomovementSettingsVisibility(AIConfig config, Action open, Action close)
    {
        _config = config;
        _open = open;
        _close = close;
        _subscription = config.Modified.ExecuteAndSubscribe(Observe);
    }

    private void Observe()
    {
        if (_observed == _config.DrawUI)
            return;
        _observed = _config.DrawUI;
        if (_config.DrawUI) _open();
        else _close();
    }

    internal void Publish(bool visible)
    {
        _observed = visible;
        if (_config.DrawUI == visible)
            return;
        _config.DrawUI = visible;
        _config.Modified.Fire();
    }

    public void Dispose() => _subscription.Dispose();
}
