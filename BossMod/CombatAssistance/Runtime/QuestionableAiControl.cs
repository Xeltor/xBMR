namespace BossMod.CombatAssistance.Runtime;

// Questionable's existing BossMod protocol is a start/stop handshake in this mode.
// Its preset payloads and setup writes never become native assistance policy.
internal sealed class QuestionableAiControl(Func<bool> active, Action<bool> setEnabled)
{
    private bool? _running;
    private bool _ready;
    private readonly HashSet<string> _presets = [];
    internal bool Enabled { get; private set; }
    internal bool OwnsActivation { get; private set; }
    internal string? ActivePreset { get; private set; }
    internal bool CanControl => Enabled && _ready && _running == true;

    // IPC has no sender parameter. Attribute only these infrequent compatibility
    // calls to the actual Questionable assembly; unrelated clients keep native IPC.
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool IsQuestionableCaller()
        => new System.Diagnostics.StackTrace().GetFrames().Any(frame =>
            frame.GetMethod()?.DeclaringType?.Assembly.GetName().Name == "Questionable");

    internal bool IsCaller => IsQuestionableCaller();

    internal static bool IsPreset(string? name)
        => name is "Questionable" or "Questionable - Quest Battles" or "Questionable - Normal Movement";

    internal void Observe(bool enabled, bool ready, bool? running)
    {
        var started = _running == false && running == true;
        Enabled = enabled;
        _ready = ready;
        _running = running;
        if (!CanControl)
            Stop();
        else if (started)
            Start();
    }

    internal bool Start(string? preset = null)
    {
        if (!CanControl || preset != null && !IsPreset(preset)) return false;
        if (!active())
        {
            setEnabled(true);
            OwnsActivation = active();
        }
        if (!active()) return false;
        if (preset != null) ActivePreset = preset;
        return true;
    }

    internal bool Stop()
    {
        var owned = OwnsActivation;
        ReleaseOwnership();
        if (owned && active()) setEnabled(false);
        return owned;
    }

    // An explicit native command or UI change takes over; polling the same run
    // cannot undo that change. A later explicit Questionable start is a new request.
    internal void ReleaseOwnership()
    {
        OwnsActivation = false;
        ActivePreset = null;
    }

    internal bool CreatePreset(string name, bool overwrite)
    {
        if (!IsPreset(name) || !overwrite && _presets.Contains(name)) return false;
        _presets.Add(name);
        return true;
    }
    internal bool DeletePreset(string name) => _presets.Remove(name);
    internal string? GetPreset(string name) => !_presets.Contains(name) ? null
        : System.Text.Json.JsonSerializer.Serialize(new { Name = name, Modules = new Dictionary<string, object>() });

    internal bool BlocksConfiguration(IReadOnlyList<string> args)
    {
        var start = args.Count > 0 && args[0].Equals("cfg", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        if (args.Count - start < 3) return false; // Reads keep their ordinary IPC contract.
        return true; // Questionable may read settings; it never supplies their values.
    }

    internal bool BlocksCompatibilityCommand(string command, IReadOnlyList<string> args)
        => command.Equals("/vbm", StringComparison.OrdinalIgnoreCase);
}
