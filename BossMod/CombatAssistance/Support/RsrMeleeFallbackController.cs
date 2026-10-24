using BossMod.CombatAssistance.Config;
using BossMod.CombatAssistance.Integrations;
using System;
using System.Reflection;

namespace BossMod.CombatAssistance.Support;

internal sealed class RsrMeleeFallbackController : IDisposable
{
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly Func<AssistancePreferences> preferences;
    private AssistancePreferences config => this.preferences();
    private readonly NativeSupportContext services;
    private readonly RsrRuntimeAccess reflection;
    private readonly Func<bool> allowed;
    private RsrSupportActionLease? lease;
    private object? rotation;
    private DateTime nextProbe;
    private bool faulted;
    private bool disposed;

    public RsrMeleeFallbackController(Func<AssistancePreferences> preferences, NativeSupportContext services,
        RsrRuntimeAccess reflection, Func<bool> allowed)
    {
        this.preferences = preferences;
        this.services = services;
        this.reflection = reflection;
        this.allowed = allowed;
    }

    private bool wasMounted;

    public string Status { get; private set; } = "disabled";

    internal static string? ActionProperty(uint job) => job switch
    {
        22 => "PiercingTalonPvE",
        30 => "ThrowingDaggerPvE",
        34 => "EnpiPvE",
        39 => "HarpePvE",
        _ => null,
    };

    internal void Update()
    {
        if (!this.allowed())
        {
            this.Reset();
            this.Status = "inactive under native activation or manual-control gates";
            return;
        }
        var mounted = this.services.Mounted;
        if (mounted)
        {
            this.Reset();
            this.wasMounted = true;
            this.Status = "inactive while mounted";
            return;
        }
        if (this.wasMounted)
        {
            this.wasMounted = false;
            this.nextProbe = DateTime.MinValue;
        }
        var job = this.services.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;
        var property = ActionProperty(job);
        if (!this.config.Enabled || !this.config.FixRsrMeleeRangedAfterMovement || property == null)
        {
            this.Reset();
            this.Status = property == null ? "not needed for this job" : "disabled";
            return;
        }
        if (DateTime.UtcNow < this.nextProbe) return;
        this.nextProbe = DateTime.UtcNow.AddSeconds(1);
        try
        {
            if (!this.reflection.TryGetSupportContext(out var plugin, out var basic, out var current))
            {
                this.Reset();
                this.Status = "waiting for RSR";
                return;
            }
            if (ReferenceEquals(current, this.rotation) && (this.faulted || this.lease?.Owned == true)) return;
            this.Reset();
            this.rotation = current;
            if (RsrRuntimeAccess.Job(current!) != job)
            {
                this.Status = "waiting for RSR job change";
                return;
            }

            var dataCenter = basic!.GetType("RotationSolver.Basic.DataCenter", true)!;
            var actionInterface = basic.GetType("RotationSolver.Basic.Actions.IBaseAction", true)!;
            var helper = basic.GetType("RotationSolver.Basic.Helpers.IActionHelper", true)!;
            var moving = helper.GetProperty("MovingActions", StaticFlags)!.GetValue(null) as Array
                ?? throw new InvalidOperationException("RSR movement history unavailable");
            var last = dataCenter.GetProperty("LastAction", StaticFlags)!;
            var age = dataCenter.GetProperty("TimeSinceLastAction", StaticFlags)!;
            var pvp = dataCenter.GetProperty("IsPvP", StaticFlags)!;
            var currentRotation = dataCenter.GetProperty("CurrentRotation", StaticFlags)!;
            var force = actionInterface.GetProperty("ForceEnable", StaticFlags)!;
            var preview = actionInterface.GetProperty("ActionPreview", StaticFlags)!;
            if (moving.GetType().GetElementType() != last.PropertyType || !last.PropertyType.IsEnum ||
                age.PropertyType != typeof(TimeSpan) ||
                actionInterface.GetMethod("CanUse")!.GetParameters().Length != 12)
                throw new InvalidOperationException("RSR ranged fallback contract changed");

            var original = current!.GetType().GetProperty(property, InstanceFlags)!.GetValue(current)!;
            var setting = original.GetType().GetProperty("Setting", InstanceFlags)!.GetValue(original)!;
            if (setting.GetType().GetProperty("SpecialType", InstanceFlags)!.GetValue(setting)?.ToString() != "MeleeRangedAttack")
                throw new InvalidOperationException("RSR action is not a melee ranged fallback");

            bool Allowed() => !this.disposed && !this.faulted && this.config.Enabled && this.config.FixRsrMeleeRangedAfterMovement &&
                Service.Framework.IsInFrameworkUpdateThread && this.allowed() &&
                this.services.ObjectTable.LocalPlayer is { IsDead: false } player && player.IsValid() && player.Address != 0 && player.ClassJob.RowId == job &&
                player.StatusFlags.HasFlag(Dalamud.Game.ClientState.Objects.Enums.StatusFlags.InCombat) &&
                !player.StatusList.Any(s => s.StatusId is 148 or 1140) && pvp.GetValue(null) is false &&
                ReferenceEquals(currentRotation.GetValue(null), current) && this.lease?.Owned == true;

            var binding = new RsrMeleeFallbackBinding(original, moving,
                () => Convert.ToUInt32(last.GetValue(null)), () => (TimeSpan)age.GetValue(null)!, Allowed,
                () => force.GetValue(null) is true, () => preview.GetValue(null) is true,
                accepted => this.Status = accepted ? "ranged fallback allowed after movement; native checks passed" :
                    "movement history hold expired; native RSR checks still reject fallback");
            this.rotation = current;
            this.lease = RsrSupportActionLease.Acquire(current, property, (action, method, arguments) =>
            {
                if (this.faulted) return RsrSupportActionProxy.Forward(action, method, arguments);
                try { return binding.Invoke(method, arguments); }
                catch (Exception ex)
                {
                    this.faulted = true;
                    this.Status = "ranged fallback correction unavailable; RSR keeps control";
                    this.services.Log.Verbose(ex, "RSR melee fallback correction yielded");
                    this.lease?.Suspend();
                    // Do not replay a call with an unknown result.
                    if (method.ReturnType == typeof(bool)) return false;
                    throw;
                }
            });
            this.Status = "ready; movement history hold limited to 0.8 seconds";
        }
        catch (Exception ex)
        {
            this.lease?.Dispose();
            this.lease = null;
            this.faulted = true;
            this.Status = "ranged fallback correction unavailable; RSR keeps control";
            this.services.Log.Verbose(ex, "Could not bind RSR melee fallback correction");
        }
    }

    private void Reset()
    {
        this.lease?.Dispose();
        this.lease = null;
        this.rotation = null;
        this.faulted = false;
    }

    public void Dispose()
    {
        this.disposed = true;
        this.Reset();
    }
}
