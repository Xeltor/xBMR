using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.Shell;

namespace BossMod.CombatAssistance.UI;

// A fallback for unhandled compatibility commands, never a /xcai registration.
// Registering that name while XCAI is unloaded would make its next constructor's
// AddHandler fail before loaded-plugin detection can relinquish the alias.
internal sealed unsafe class MigrationCommandAlias : IDisposable
{
    private readonly Hook<ShellCommands.Delegates.TryInvokeDebugCommand> _hook;
    private readonly Action<string> _handle;
    internal bool Enabled { get; set; }

    internal MigrationCommandAlias(Action<string> handle)
    {
        _handle = handle;
        _hook = Service.Hook.HookFromAddress<ShellCommands.Delegates.TryInvokeDebugCommand>(
            (nint)ShellCommands.MemberFunctionPointers.TryInvokeDebugCommand, Invoke);
        _hook.Enable();
    }

    private int Invoke(ShellCommands* self, Utf8String* command, UIModule* module)
    {
        var result = _hook.Original(self, command, module);
        if (result != -1 || !Enabled || command == null || Service.CommandManager.Commands.ContainsKey("/xcai")) return result;
        if (!TryGetArguments(command->ToString(), out var arguments)) return result;
        try { _handle(arguments); }
        catch (Exception error) { Service.Logger.Verbose(error, "Unable to handle combat assistance compatibility command."); }
        return 0;
    }

    internal static bool TryGetArguments(string command, out string arguments)
    {
        var parts = command.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        arguments = string.Join(' ', parts.Skip(1));
        return parts.Length != 0 && parts[0].Equals("/xcai", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool Matches(string command) => TryGetArguments(command, out _);

    internal static bool CanHandle(int originalResult, bool enabled, bool registered, string command)
        => originalResult == -1 && enabled && !registered && Matches(command);

    public void Dispose()
    {
        Enabled = false;
        _hook.Dispose();
    }
}
