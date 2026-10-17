using Dalamud.Plugin.Services;
using System.Runtime.CompilerServices;

namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    private IntegrationLeases? _leases;
    private IntegrationSetting[] _settings = [];
    private AI.AIBehaviour? _observedFollow;
    private Autorotation.Preset? _observedPreset;
    private int _observedMaster = -1;
    private long _followRevision;
    private sealed record ControlObservation(string Scope, bool Allowed, long Timestamp);
    private ControlObservation? _controlObservation;

    private long ObserveFollow()
    {
        if (!ReferenceEquals(_observedFollow, ai.Beh) || !ReferenceEquals(_observedPreset, rotation.Preset) || _observedMaster != ai.MasterSlot)
        {
            _observedFollow = ai.Beh;
            _observedPreset = rotation.Preset;
            _observedMaster = ai.MasterSlot;
            ++_followRevision;
        }
        return _followRevision;
    }

    internal string GetFollow(string request) => WithContext(request, context => IntegrationSession.Serialize(new
    {
        available = true, context, revision = ObserveFollow(), active = ai.Beh != null,
        masterSlot = ai.MasterSlot, masterActorId = World.Party[ai.MasterSlot]?.InstanceID.ToString("X"),
        activePreset = rotation.Preset?.Name, savedAIPreset = ai.GetAIPreset,
        navigationTarget = P(ai.Controller.NaviTargetPos),
        meaning = "live BMR follow state; revision is scoped to this integration instance"
    }));

    internal sealed record FollowStop(IntegrationSession.Context Context, string OwnerToken, long ExpectedRevision, string Reason);

    internal string StopFollow(string json)
    {
        var request = IntegrationSession.Parse<FollowStop>(json);
        if (request == null || !Session.Matches(request.Context) || request.Context.Stage != "hints-ready"
            || !Session.IsOwnerActive(request.OwnerToken) || !ControlsAllowed()
            || request.Reason != "user-requested-compatibility-repair")
            return Unavailable("invalid-follow-stop-context");
        if (request.ExpectedRevision != ObserveFollow())
            return Unavailable("changed-follow-owner-or-preset");
        if (ai.Beh == null)
            return Unavailable("follow-already-idle");
        // This calls the native cancellation path; no saved follow preferences are edited.
        ai.SwitchToIdle();
        return IntegrationSession.Serialize(new { accepted = true, reason = "follow-stopped", revision = ObserveFollow() });
    }

    internal void AttachControls()
    {
        _settings = IntegrationSetting.Supported(Service.Config);
        _leases = new(Session.IsOwnerActive, ex => Service.LogVerbose($"[Integration] Lease cleanup: {ex.GetType().Name}: {ex.Message}"));
        Service.Framework.Update += UpdateControls;
    }

    private string ControlScope()
    {
        var player = World.Party.Player();
        var presetId = rotation.Preset == null ? 0 : RuntimeHelpers.GetHashCode(rotation.Preset);
        return $"{World.CurrentZone}:{World.CurrentCFCID}:{player?.InstanceID}:{player?.IsDead}:{presetId}:{rotation.Preset?.Name}";
    }

    private bool ControlsAllowed() => World.Party.Player() is { IsDead: false, MountId: 0 }
        && !World.Party.Members[PartyState.PlayerSlot].InCutscene && !movement.IsMoveRequested() && !hints.AutomaticMovementPaused;

    private void UpdateControls(IFramework framework)
    {
        var observation = new ControlObservation(ControlScope(), ControlsAllowed(), System.Diagnostics.Stopwatch.GetTimestamp());
        System.Threading.Volatile.Write(ref _controlObservation, observation);
        _leases?.Sweep(observation.Scope, observation.Allowed);
    }

    internal string GetSettings(string request) => WithContext(request, context => IntegrationSession.Serialize(new
    {
        available = true, context,
        settings = _settings.Select(setting =>
        {
            var state = setting.Read();
            return new { key = setting.Key, setting.Node, setting.Field, setting.AllowedValues, setting.MaxLeaseMs,
                setting.SynchronousOnly, state.Value, state.Revision, leased = state.LeaseId != null };
        }).ToArray()
    }));

    internal sealed record SettingRequest(IntegrationSession.Context Context, string OwnerToken, string Key,
        long ExpectedRevision, string ExpectedValue, string Value, int LeaseMs, string? LeaseId = null);
    internal sealed record ControlLease(int SchemaVersion, string InstanceGeneration, string OwnerToken, string LeaseId, int LeaseMs = 0);
    internal sealed record DirectionalRequest(IntegrationSession.Context Context, string OwnerToken, uint ActionId, long ExpectedRevision, string ExpectedValue);

    private bool ValidControlLease(ControlLease? request) => request != null && request.SchemaVersion == IntegrationSession.SchemaVersion
        && request.InstanceGeneration == Session.InstanceGeneration && !string.IsNullOrEmpty(request.OwnerToken) && !string.IsNullOrEmpty(request.LeaseId);

    internal string ApplySetting(string json)
    {
        var request = IntegrationSession.Parse<SettingRequest>(json);
        if (request == null || !Session.Matches(request.Context) || !Session.CanContribute || !ControlsAllowed() || _leases == null)
            return Unavailable("invalid-setting-context");
        var setting = _settings.FirstOrDefault(s => s.Key == request.Key && !s.SynchronousOnly);
        if (setting == null)
            return Unavailable("unsupported-or-synchronous-only-setting");
        _leases.Sweep(ControlScope(), true);
        return IntegrationSession.Serialize(_leases.Apply(request.OwnerToken, request.LeaseId, ControlScope(), setting,
            request.ExpectedRevision, request.ExpectedValue, request.Value, request.LeaseMs));
    }

    internal string RenewControl(string json)
    {
        var request = IntegrationSession.Parse<ControlLease>(json);
        var observation = System.Threading.Volatile.Read(ref _controlObservation);
        if (!ValidControlLease(request) || _leases == null || observation == null || !observation.Allowed
            || (System.Diagnostics.Stopwatch.GetTimestamp() - observation.Timestamp) / (double)System.Diagnostics.Stopwatch.Frequency >= 0.25)
            return Unavailable("invalid-control-lease");
        // Lifecycle callers need no game-object reads. The independent framework sweep remains
        // authoritative and revokes a renewal immediately when its native scope/manual state changes.
        return IntegrationSession.Serialize(_leases.Renew(request!.OwnerToken, request.LeaseId, observation.Scope, request.LeaseMs));
    }

    internal string ReleaseControl(string json)
    {
        var request = IntegrationSession.Parse<ControlLease>(json);
        return ValidControlLease(request) && _leases != null ? IntegrationSession.Serialize(_leases.Release(request!.OwnerToken, request.LeaseId))
            : Unavailable("invalid-control-lease");
    }

    private IntegrationSetting? DirectionalSetting(uint actionId)
    {
        var node = actionId switch
        {
            (uint)RPR.AID.HellsIngress or (uint)RPR.AID.HellsEgress => typeof(RPRConfig),
            (uint)DNC.AID.EnAvant => typeof(DNCConfig),
            (uint)WHM.AID.AetherialShift => typeof(WHMConfig),
            (uint)PCT.AID.Smudge => typeof(PCTConfig),
            (uint)DRG.AID.ElusiveJump => typeof(DRGConfig),
            (uint)Data.PhantomID.OccultFeatherfoot => typeof(ClassShared.SharedActionsConfig),
            _ => null
        };
        return node == null ? null : _settings.FirstOrDefault(s => s.Node == node.FullName && s.SynchronousOnly);
    }

    internal string WithDirectionalAngle(string json, Func<bool> execute)
    {
        var request = IntegrationSession.Parse<DirectionalRequest>(json);
        if (request == null || !Session.Matches(request.Context) || !Session.CanContribute || !ControlsAllowed() || execute == null || _leases == null)
            return Unavailable("invalid-directional-scope");
        var setting = DirectionalSetting(request.ActionId);
        if (setting == null)
            return Unavailable("unsupported-directional-action");
        _leases.Sweep(ControlScope(), true);
        var applied = _leases.Apply(request.OwnerToken, null, ControlScope(), setting, request.ExpectedRevision,
            request.ExpectedValue, setting.AllowedValues[0], setting.MaxLeaseMs);
        if (!applied.Accepted || applied.LeaseId == null)
            return IntegrationSession.Serialize(applied);
        var requested = false;
        var callbackSucceeded = true;
        IntegrationLeases.Result released;
        try { requested = execute(); }
        catch (Exception ex)
        {
            callbackSucceeded = false;
            Service.LogVerbose($"[Integration] Directional callback failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally { released = _leases.Release(request.OwnerToken, applied.LeaseId); }
        return IntegrationSession.Serialize(new
        {
            accepted = callbackSucceeded && released.Accepted,
            actionRequested = requested,
            releaseComplete = released.Accepted,
            reason = !callbackSucceeded ? "callback-failed" : released.Reason,
            meaning = "angle-setting scope only; not action legality, landing safety, cast permission or effect confirmation"
        });
    }

    private void DisposeControls()
    {
        Service.Framework.Update -= UpdateControls;
        System.Threading.Volatile.Write(ref _controlObservation, null);
        _leases?.Dispose();
    }
}
