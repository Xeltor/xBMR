namespace BossMod;

// Accept both the original BMR arguments and VBM's optional console-command prefix.
internal static class IPCConfiguration
{
    public static List<string> Execute(ConfigRoot config, IReadOnlyList<string> args, bool save)
    {
        var values = args.ToArray();
        var skip = values.Length > 0 && values[0].Equals("cfg", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        return config.ConsoleCommand(values.AsSpan(skip), save);
    }
}
