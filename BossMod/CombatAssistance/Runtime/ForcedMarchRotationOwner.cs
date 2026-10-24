using BossMod.CombatAssistance.Integrations;

using System;

namespace BossMod.CombatAssistance.Runtime;

// Owns only the temporary RSR Off mode. Provider reloads and external mode
// changes yield for the remainder of this mechanic instead of fighting them.
internal sealed class ForcedMarchRotationOwner(
    Func<(RsrMode? Mode, object? Provider)> read,
    Func<RsrMode, bool> write)
{
    private object? provider;
    private RsrMode restore;
    private long mechanic;
    private bool yielded;
    public bool Owned { get; private set; }
    public bool Paused { get; private set; }
    public string Status { get; private set; } = "inactive";

    public void Tick(bool protect, long identity)
    {
        if (!protect)
        {
            Release();
            if (!Owned)
            {
                mechanic = 0;
                yielded = false;
            }
            return;
        }
        if (identity != mechanic && !Owned)
        {
            mechanic = identity;
            yielded = false;
        }
        var current = read();
        if (Owned)
        {
            if (current.Provider == null || current.Mode == null)
            {
                Paused = false;
                Status = "RSR read unavailable; restore retained";
                return;
            }
            if (!ReferenceEquals(current.Provider, provider) || current.Mode != RsrMode.Off)
            {
                Paused = false;
                Owned = false;
                provider = null;
                yielded = true;
                Status = "yielded to external RSR mode or provider change";
            }
            else
            {
                Paused = true;
                Status = "attacks paused for forced march";
            }
            return;
        }
        if (yielded) return;
        if (current.Provider != null && current.Mode == RsrMode.Off)
        {
            Paused = true;
            Status = "RSR already off; no restoration owned";
            return;
        }
        Paused = false;
        if (current.Provider == null || current.Mode is not (RsrMode.Auto or RsrMode.Manual or RsrMode.Henched or RsrMode.AutoDuty or RsrMode.TargetOnly))
        {
            Status = "RSR mode preserved";
            return;
        }
        restore = current.Mode.Value;
        provider = current.Provider;
        // An uncertain IPC result can still have applied Off. Retain the exact
        // restore obligation until readback proves ownership ended.
        Owned = true;
        write(RsrMode.Off);
        var confirmed = read();
        Paused = ReferenceEquals(confirmed.Provider, provider) && confirmed.Mode == RsrMode.Off;
        Status = Paused ? "attacks paused for forced march" : "RSR pause unconfirmed; restore retained";
    }

    public void Abandon()
    {
        Owned = false;
        Paused = false;
        provider = null;
        mechanic = 0;
        yielded = false;
        Status = "explicit RSR mode intent replaces temporary pause";
    }

    public void Release()
    {
        Paused = false;
        if (!Owned) return;
        var current = read();
        if (current.Mode == null || current.Provider == null)
        {
            Status = "RSR restore pending";
            return;
        }
        if (ReferenceEquals(current.Provider, provider) && current.Mode == RsrMode.Off)
        {
            if (!write(restore))
            {
                Status = "RSR restore pending";
                return;
            }
            var confirmed = read();
            if (confirmed.Mode == null || confirmed.Provider == null ||
                ReferenceEquals(provider, confirmed.Provider) && confirmed.Mode == RsrMode.Off)
            {
                Status = "RSR restore pending";
                return;
            }
        }
        Owned = false;
        provider = null;
        Status = "released forced march pause";
    }
}

internal readonly record struct ForcedMarchTiming(double ActivationIn, double ActiveRemaining, long Identity)
{
    // Leave the long warning debuff available for attacks. Include a small
    // status transition margin without pausing through distant chained moves.
    public bool ProtectFacing => ActivationIn is >= -0.35 and <= 1.1 || double.IsFinite(ActiveRemaining) && ActiveRemaining > 0;
}
