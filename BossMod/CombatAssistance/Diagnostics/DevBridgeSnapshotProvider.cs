using BossMod.AI;
using BossMod.CombatAssistance.Config;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace BossMod.CombatAssistance.Diagnostics;

internal sealed class DevBridgeSnapshotProvider : IDisposable
{
    internal const string ChannelName = "BossModReborn.DevBridge.GetSnapshot";
    private readonly NativeDiagnosticSnapshot _snapshot = new();
    private readonly ICallGateProvider<string, string> _provider;
    private bool _disposed;

    internal DevBridgeSnapshotProvider(IDalamudPluginInterface plugin, AIManager ai, AssistanceSettingsSession settings)
    {
        _provider = plugin.GetIpcProvider<string, string>(ChannelName);
        _provider.RegisterFunc(request => _snapshot.Read(request, ai.Autorot.MovementPlanning.CurrentPlan, settings.MayAssist,
            ai.Beh != null, settings.Current.State.ToString(), ai.Autorot.MovementPlanning.LastFailure, DateTime.UtcNow,
            Service.Framework.IsInFrameworkUpdateThread, ai.Autorot.MovementPlanning.Execution,
            ai.Autorot.MovementPlanning.Assistance?.CameraStatus, ai.Autorot.MovementPlanning.Assistance?.ActionStatus,
            ai.Autorot.MovementPlanning.Assistance?.SupportSnapshot, ai.Autorot.MovementPlanning.Assistance?.SupportStatus,
            ai.Autorot.MovementPlanning.Assistance?.CastControlDecision,
            ai.Autorot.MovementPlanning.Assistance?.TankStanceDecision, ai.Autorot.MovementPlanning.Assistance?.TankStanceStatus,
            ai.Autorot.MovementPlanning.Assistance?.TankRecoveryDecision, ai.Autorot.MovementPlanning.Assistance?.TankRecoveryStatus,
            Service.Framework.IsInFrameworkUpdateThread ? ai.Autorot.MovementPlanning.Assistance?.RotationMode : null, ai.Autorot.MovementPlanning.LastInputChange,
            () => NativeActionTimingObservation.Capture(ai.Autorot),
            ai.Autorot.MovementPlanning.Assistance?.TargetDecision, ai.Autorot.MovementPlanning.Assistance?.TargetStatus,
            ai.Autorot.MovementPlanning.Assistance?.KnockbackObservationStatus,
            Service.Framework.IsInFrameworkUpdateThread ? ai.Autorot.MovementPlanning.Assistance?.DisplacementStatus : null,
            ai.Autorot.MovementPlanning.Assistance?.KnockbackSelectionStatus,
            ai.Autorot.MovementPlanning.Assistance?.HealingStatus));
    }

    internal void Observe(WorldState world)
    {
        var player = world.Party.Player();
        _snapshot.Observe(player?.InCombat == true, player?.InstanceID ?? 0, world.CurrentZone, DateTime.UtcNow);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _provider.UnregisterFunc();
    }
}
