using BossMod.CombatAssistance.Config;
using BossMod.CombatAssistance.Runtime;

namespace BossMod.CombatAssistance.UI;

internal sealed class PatchNoteController : IDisposable
{
    private readonly PatchNoteEntry[] _history = PatchNoteHistory.Load();
    private readonly AssistanceSettingsSession _settings;
    private readonly PatchNotesWindow _window;
    private AssistancePreferences? _observed;
    private PatchNoteEntry[] _pending = [];

    internal PatchNoteController(AssistanceSettingsSession settings)
    {
        _settings = settings;
        _window = new(shown =>
        {
            if (!_settings.MayEdit) return;
            var config = _settings.Preferences;
            config.AcknowledgedPatchNotes = PatchNoteHistory.Acknowledge(_history, config.AcknowledgedPatchNotes, shown);
            _pending = PatchNoteHistory.Pending(_history, config.AcknowledgedPatchNotes, config.PatchNotesBaselineVersion);
            _settings.Save();
        });
    }

    internal void Show() { if (_settings.MayEdit) _window.Show(_history); }

    internal void Update(bool mayShow)
    {
        if (!_settings.MayEdit) { _window.Suspend(); return; }
        _window.ObserveDismissal();
        var config = _settings.Preferences;
        if (!ReferenceEquals(config, _observed))
        {
            _observed = config;
            _pending = PatchNoteHistory.Pending(_history, config.AcknowledgedPatchNotes, config.PatchNotesBaselineVersion);
        }
        if (mayShow && _pending.Length != 0) _window.Show(_pending);
    }

    public void Dispose() { _window.Suspend(); _window.Dispose(); }
}
