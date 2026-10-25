using TerraFX.Interop.Windows;
using NativeWindows = TerraFX.Interop.Windows.Windows;

namespace BossMod;

// Compare wall-clock hitches with CPU work on the calling framework thread.
// One read at entry; the second read happens only when reporting a slow frame.
internal static unsafe class FrameworkThreadClock
{
    internal static long? ReadCpuTicks()
    {
        if (!OperatingSystem.IsWindows()) return null;
        FILETIME creation, exit, kernel, user;
        if (NativeWindows.GetThreadTimes(NativeWindows.GetCurrentThread(), &creation, &exit, &kernel, &user) == BOOL.FALSE)
            return null;
        return Ticks(kernel) + Ticks(user);
    }

    internal static double? CpuMillisecondsSince(long? started)
        => started is { } before && ReadCpuTicks() is { } after && after >= before ? (after - before) / 10000d : null;

    private static long Ticks(FILETIME time) => ((long)time.dwHighDateTime << 32) | time.dwLowDateTime;
}
