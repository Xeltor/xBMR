using Dalamud.Game.Config;

namespace BossMod.CombatAssistance.Camera;

// Local, scoped ownership replaces the cross-plugin movement option lease.
// A user edit wins, including while camera assistance is borrowing the option.
internal sealed class LegacyMovementOption(Func<uint?> read, Action<uint> write, Action<Exception>? failure = null)
{
    private uint? _original;
    private DateTime _resumeAfter;
    internal bool Active => _original.HasValue;

    private DateTime _retryAfter;
    internal void Update(bool automated, bool manual, DateTime now)
    {
        if (now < _retryAfter)
            return;
        try { UpdateCore(automated, manual, now); }
        catch (Exception error)
        {
            _retryAfter = now.AddSeconds(10);
            failure?.Invoke(error);
        }
    }

    private void UpdateCore(bool automated, bool manual, DateTime now)
    {
        if (manual)
            _resumeAfter = now.AddSeconds(3);
        if (!automated || manual || now < _resumeAfter)
        {
            RestoreCore();
            return;
        }
        if (read() is not { } current)
            return;
        if (Active && current != 1)
        {
            _original = null;
            _resumeAfter = now.AddSeconds(3);
            return;
        }
        _original ??= current;
        if (current != 1)
            write(1);
    }

    internal void Restore()
    {
        try { RestoreCore(); }
        catch (Exception error) { failure?.Invoke(error); }
    }

    private void RestoreCore()
    {
        if (_original is not { } original || read() is not { } current)
            return;
        if (current == 1 && current != original)
            write(original);
        _original = null;
    }

    internal static LegacyMovementOption Native() => new(
        () => Service.GameConfig.TryGet(UiControlOption.MoveMode, out uint value) ? value : null,
        value => Service.GameConfig.Set(UiControlOption.MoveMode, value),
        error => Service.Logger.Verbose(error, "Camera movement option unavailable."));
}
