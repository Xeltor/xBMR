// Camera input/collision behavior adapted from XCAI 4a0f7968; scene and lifecycle are native.
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using NativeCamera = FFXIVClientStructs.FFXIV.Client.Game.Camera;

namespace BossMod.CombatAssistance.Camera;

internal sealed record CombatCameraStatus(string State, long Updates, float? Yaw, float? TargetYaw)
{
    public float? Clearance { get; init; }
    public float? GroundClearance { get; init; }
    public float? CharacterVisibility { get; init; }
    public float? TargetPitch { get; init; }
    public float? Distance { get; init; }
    public float? TargetDistance { get; init; }
    public float? FightVisibility { get; init; }
    public float? ArenaRadius { get; init; }
    public float? FramingPenalty { get; init; }
    public int Subjects { get; init; }
    public float? SceneYaw { get; init; }
    public float? LogicalYaw { get; init; }
    public float? ScenePitch { get; init; }
    public float? RenderYaw { get; init; }
    public Vector3? ScenePosition { get; init; }
    public float? AdjustedInput { get; init; }
    public double EvaluationMilliseconds { get; init; }
    public int EvaluatedViews { get; init; }
    public int PrunedViews { get; init; }
    public double InputMilliseconds { get; init; }
    public int CollisionQueries { get; init; }
    public int ReusedCollisionQueries { get; init; }
}

internal sealed unsafe class CombatCameraController : IDisposable
{
    private delegate float CalculateYawDelegate(NativeCamera* camera);
    private static readonly InputId[] ManualControlKeys = [InputId.MOVE_FORE, InputId.MOVE_BACK,
        InputId.MOVE_LEFT, InputId.MOVE_RIGHT, InputId.MOVE_STRIFE_L, InputId.MOVE_STRIFE_R, InputId.MOVE_AND_STEER,
        InputId.CAMERA_UP, InputId.CAMERA_DOWN,
        InputId.CAMERA_LEFT, InputId.CAMERA_RIGHT, InputId.CAMERA_ZOOMIN, InputId.CAMERA_ZOOMOUT,
        InputId.CAMERA_RESET, InputId.CAMERA_LOOKING];
    private readonly Func<Config.AssistancePreferences> preferences;
    private Config.AssistancePreferences config => this.preferences();
    private readonly IObjectTable objects;
    private readonly IPluginLog log;
    private readonly IGameInteropProvider interop;
    private readonly Func<bool> automationAllowed;
    private readonly LegacyMovementOption movementOption;
    private CameraScene? scene;
    private float targetDistance;
    private Vector3 playerPosition;
    private Vector3 fightPosition;
    private float fightVisibility;
    private Hook<CalculateYawDelegate>? yawHook;
    private delegate void ReadCameraInputDelegate(NativeCamera* camera, int inputMode, float speedH, float speedV);
    // Same camera-input entry point used by vnavmesh's OverrideCamera.
    [Signature("48 8B C4 53 48 81 EC ?? ?? ?? ?? 44 0F 29 50 ??", DetourName = nameof(ReadCameraInput))]
    private Hook<ReadCameraInputDelegate>? inputHook = null;
    private float? pitch;
    private float targetPitch;
    private float framingPenalty;
    private readonly List<CameraSubject> subjects = [];
    private readonly List<CameraSubject> partySubjects = [];
    private readonly List<CameraSubject> raidSubjects = [];
    private nint hookedCamera;
    private float? yaw;
    private long lastTick;
    private long updates;
    private DateTime retryAfter;
    private bool eligible;
    private bool faulted;
    private int frameworkThread;
    private float targetYaw;
    private float clearance;
    private float groundClearance;
    private float characterVisibility;
    private DateTime nextViewEvaluation;
    private readonly CameraRaycast raycasts = new(ReadNativeRay);
    private double evaluationMilliseconds;
    private double inputMilliseconds;
    private int evaluatedViews;
    private int prunedViews;

    public CombatCameraStatus Status { get; private set; } = new("disabled", 0, null, null);

    internal CombatCameraController(Func<Config.AssistancePreferences> preferences, IObjectTable objects,
        IPluginLog log, IGameInteropProvider interop, Func<bool> automationAllowed, LegacyMovementOption movementOption)
    {
        this.preferences = preferences;
        this.objects = objects;
        this.log = log;
        this.interop = interop;
        this.automationAllowed = automationAllowed;
        this.movementOption = movementOption;
    }

    // Called once after native hints and movement intent have been gathered. The
    // same lifecycle owns camera hooks and movement; no second framework subscriber.
    internal void Update(CameraScene? scene)
    {
        this.scene = scene;
        this.frameworkThread = Environment.CurrentManagedThreadId;
        this.eligible = false;
        if (!this.config.CombatCamera)
        {
            this.ReleaseHook();
            this.faulted = false;
            this.Hold("disabled");
            return;
        }

        if (this.faulted)
        {
            this.Hold("camera error; toggle off to reset");
            return;
        }

        try
        {
            if (!this.config.Enabled || !this.config.ManageMovement || !this.automationAllowed() || scene == null)
            {
                this.Hold("waiting for combat automation and target");
                return;
            }
            var fightCenter = scene.FightCenter;
            var manager = CameraManager.Instance();
            var camera = manager == null ? null : manager->GetActiveCamera();
            if (camera == null || camera != manager->Camera)
            {
                this.Hold("special camera");
                return;
            }

            if ((nint)camera != this.hookedCamera)
            {
                this.ReleaseHook();
                if (DateTime.UtcNow < this.retryAfter)
                    return;
                this.yawHook = this.interop.HookFromAddress<CalculateYawDelegate>(
                    (nint)camera->VirtualTable->CalculateSceneCameraYaw, this.CalculateYaw);
                this.hookedCamera = (nint)camera;
                this.interop.InitializeFromAttributes(this);
                this.yawHook.Enable();
                this.inputHook?.Enable();
            }

            if (DateTime.UtcNow >= this.nextViewEvaluation)
            {
                var started = Stopwatch.GetTimestamp();
                this.ChooseView(camera, scene.PlayerPosition, fightCenter);
                this.evaluationMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                this.nextViewEvaluation = DateTime.UtcNow.AddMilliseconds(250);
            }
            this.eligible = true;
            this.Status = new("ready", this.updates, this.yaw, this.targetYaw)
            {
                EvaluationMilliseconds = this.evaluationMilliseconds,
                EvaluatedViews = this.evaluatedViews,
                PrunedViews = this.prunedViews,
                InputMilliseconds = this.inputMilliseconds,
                CollisionQueries = this.raycasts.Queries,
                ReusedCollisionQueries = this.raycasts.Reused,
                GroundClearance = this.groundClearance,
                CharacterVisibility = this.characterVisibility,
                FightVisibility = this.fightVisibility,
            };
        }
        catch (Exception ex)
        {
            this.log.Verbose(ex, "Combat camera unavailable.");
            this.ReleaseHook();
            this.retryAfter = DateTime.UtcNow.AddSeconds(5);
            this.Hold("camera unavailable");
        }
    }

    private void ReadCameraInput(NativeCamera* camera, int inputMode, float speedH, float speedV)
    {
        var frameworkInput = Environment.CurrentManagedThreadId == this.frameworkThread;
        // Return the borrowed mode before the game consumes the player's input.
        // Deltas still contain the previous automatic step here; only inspect raw
        // input until the original reader has replaced them with native values.
        if (frameworkInput && (nint)camera == this.hookedCamera &&
            (inputMode != 0 || HasManualInput(camera, includeDeltas: false)))
            this.movementOption.Update(false, true, DateTime.UtcNow);
        this.inputHook!.Original(camera, inputMode, speedH, speedV);
        if (!frameworkInput)
            return;
        if (!this.eligible || !this.automationAllowed())
        {
            this.movementOption.Restore();
            return;
        }
        var started = Stopwatch.GetTimestamp();
        try
        {
            var manager = CameraManager.Instance();
            if (!this.config.CombatCamera || manager == null || manager->GetActiveCamera() != camera ||
                (nint)camera != this.hookedCamera || camera->ZoomMode != CameraZoomMode.ThirdPerson ||
                camera->ControlMode is not (CameraControlMode.ThirdPersonLegacy or CameraControlMode.ThirdPersonFixed) ||
                camera->ShouldResetAngles || camera->IsEventCameraAutoControl)
            {
                this.Hold("camera released");
                return;
            }
            if (inputMode != 0 || HasManualInput(camera))
            {
                this.movementOption.Update(false, true, DateTime.UtcNow);
                this.Hold("manual camera input");
                return;
            }
            if (this.movementOption.Paused(DateTime.UtcNow))
            {
                this.Hold("manual camera pause");
                return;
            }
            if (!float.IsFinite(camera->DirH) || !float.IsFinite(camera->DirV))
                return;
            var tick = Stopwatch.GetTimestamp();
            var dt = this.lastTick == 0 ? 0f : (float)Stopwatch.GetElapsedTime(this.lastTick, tick).TotalSeconds;
            this.lastTick = tick;
            if (this.objects.LocalPlayer is not { } player || !player.IsValid() || player.Address == 0
                || this.scene == null || player.GameObjectId != this.scene.PlayerId)
                return;
            this.playerPosition = player.Position;
            var nextYaw = CombatCameraPolicy.Step(camera->DirH, this.targetYaw, dt, this.config.CameraPanningSpeed);
            var nextPitch = CombatCameraPolicy.Step(camera->DirV, this.targetPitch, dt);
            var nextDistance = this.config.CameraAutoZoom
                ? CombatCameraPolicy.ZoomStep(camera->Distance, this.targetDistance, camera->MaxDistance, dt)
                : camera->Distance;
            if (nextDistance - camera->Distance < 0.002f)
                nextDistance = camera->Distance;
            // Recheck the actual next step at execution time. Yaw, pitch and zoom
            // ease independently, so the planning arc alone cannot authorize it.
            var changingView = MathF.Abs(nextYaw - camera->DirH) > 0.00001f ||
                MathF.Abs(nextPitch - camera->DirV) > 0.00001f || nextDistance > camera->Distance;
            if (changingView && !this.TransitionClear(camera->DirH, camera->DirV, camera->Distance,
                    nextYaw, nextPitch, nextDistance, (Vector3)camera->SceneCamera.Position))
            {
                this.nextViewEvaluation = default;
                this.Hold("view obstructed; replanning");
                return;
            }
            this.yaw = nextYaw;
            this.pitch = nextPitch;
            // Feed the normal input stage so collision and rendering use the same
            // angles, rather than changing logical yaw after rendering has happened.
            camera->InputDeltaH = CombatCameraPolicy.ActiveInputDelta(camera->DirH, this.yaw.Value);
            camera->InputDeltaV = this.pitch.Value - camera->DirV;
            // Change the requested native boom length only. Native interpolation,
            // collision, FoV, zoom limits and saved camera settings remain owned by the game.
            if (nextDistance > camera->Distance)
                camera->Distance = nextDistance;
            this.updates++;
            this.Status = new("framing fight", this.updates, this.yaw, this.targetYaw)
            {
                Clearance = this.clearance,
                GroundClearance = this.groundClearance,
                CharacterVisibility = this.characterVisibility,
                TargetPitch = this.targetPitch,
                Distance = camera->Distance,
                TargetDistance = this.targetDistance,
                FightVisibility = this.fightVisibility,
                ArenaRadius = this.FreshArena()?.BoundingRadius,
                FramingPenalty = this.framingPenalty,
                Subjects = this.subjects.Count,
                EvaluationMilliseconds = this.evaluationMilliseconds,
                EvaluatedViews = this.evaluatedViews,
                PrunedViews = this.prunedViews,
                InputMilliseconds = this.inputMilliseconds,
                CollisionQueries = this.raycasts.Queries,
                ReusedCollisionQueries = this.raycasts.Reused,
            };
        }
        catch (Exception ex)
        {
            this.eligible = false;
            this.faulted = true;
            this.Hold("camera error; released");
            this.log.Verbose(ex, "Combat camera input released after failure.");
        }
        finally
        {
            this.inputMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }
    }

    private float CalculateYaw(NativeCamera* camera)
    {
        var sceneYaw = this.yawHook!.Original(camera);
        if (Environment.CurrentManagedThreadId == this.frameworkThread && this.eligible)
        {
            this.Status = this.Status with
            {
                SceneYaw = sceneYaw,
                LogicalYaw = camera->DirH,
                AdjustedInput = camera->InputDeltaHAdjusted,
                ScenePosition = (Vector3)camera->SceneCamera.Position,
                ScenePitch = camera->DirV,
                RenderYaw = camera->SceneCamera.RenderCamera == null ? null :
                    MathF.Atan2(camera->SceneCamera.RenderCamera->ViewMatrix.M13, camera->SceneCamera.RenderCamera->ViewMatrix.M33),
            };
        }
        return sceneYaw;
    }

    private void ChooseView(NativeCamera* camera, Vector3 player, Vector3 fight)
    {
        using var collisionObservation = this.raycasts.Begin();
        this.evaluatedViews = 0;
        this.prunedViews = 0;
        var nativeFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
        if (nativeFramework == null || nativeFramework->BGCollisionModule == null ||
            !float.IsFinite(camera->Distance) || !float.IsFinite(camera->DirV))
            throw new InvalidOperationException("Camera collision data unavailable.");
        var renderCamera = camera->SceneCamera.RenderCamera;
        if (renderCamera == null)
            throw new InvalidOperationException("Camera projection unavailable.");
        var projectionScale = new Vector2(MathF.Abs(renderCamera->ProjectionMatrix.M11),
            MathF.Abs(renderCamera->ProjectionMatrix.M22));
        if (!float.IsFinite(projectionScale.X) || !float.IsFinite(projectionScale.Y) ||
            projectionScale.X <= 0f || projectionScale.Y <= 0f)
            throw new InvalidOperationException("Camera lens unavailable.");

        var current = camera->DirH;
        var currentPitch = camera->DirV;
        var fightYaw = Vector3.DistanceSquared(player, fight) < 4f
            ? current : CombatCameraPolicy.TargetYaw(player, fight);
        // Approximate the normal character pivot; native collision retains the
        // final camera position. Zoom candidates change only the requested boom.
        var pivot = player + new Vector3(0f, 1.5f, 0f);
        var distance = Math.Clamp(camera->Distance, 2f, 50f);
        this.playerPosition = player;
        this.fightPosition = fight;
        this.targetDistance = camera->Distance;
        this.subjects.Clear();
        this.partySubjects.Clear();
        this.raidSubjects.Clear();
        this.subjects.Add(new(player, 0.5f, 2f, 1f));
        this.subjects.Add(new(fight, 1f, 2f, 2f));
        if (this.scene is { } captured)
        {
            this.subjects.AddRange(captured.Enemies);
            this.partySubjects.AddRange(captured.Party);
            this.raidSubjects.AddRange(captured.Alliance);
        }

        var currentClearance = ViewClearance(pivot, current, currentPitch, distance);
        this.clearance = currentClearance;
        var best = current;
        var bestPitch = currentPitch;
        // Check the native position as well as the estimate: collision can shorten
        // the boom and put the real camera much lower than a full-distance model.
        this.groundClearance = GroundClearance((Vector3)camera->SceneCamera.Position, current);
        this.characterVisibility = CharacterVisibility((Vector3)camera->SceneCamera.Position, player, current);
        this.fightVisibility = FightVisibility((Vector3)camera->SceneCamera.Position, fight, current);
        var currentVisibility = new CameraViewClearance(currentClearance,
            this.groundClearance, this.characterVisibility, this.fightVisibility);
        var bestScore = Score(current, currentPitch, currentClearance, distance, physical: currentVisibility)!.Value;
        // Rank inexpensive screen-framing candidates first, then limit collision
        // work to twelve promising views per 250 ms evaluation.
        var candidates = new List<(float Yaw, float Pitch, CameraViewScore Score)>();
        for (var step = -12; step <= 12; step++)
        {
            var candidate = step == 12 ? fightYaw : current + step * MathF.PI / 12f;
            for (var height = -1; height < 5; height++)
            {
                var candidatePitch = height == -1 ? currentPitch :
                    Math.Clamp(-0.18f - height * 0.15f, camera->DirVMin, camera->DirVMax);
                candidates.Add((candidate, candidatePitch, FramingScore(candidate, candidatePitch, distance)));
            }
        }
        candidates.Sort((left, right) => right.Score.CompareTo(left.Score));
        candidates.RemoveRange(Math.Min(12, candidates.Count), Math.Max(0, candidates.Count - 12));
        // Always test elevated views at the current yaw. Otherwise framing's
        // shortlist can omit every way out of a low or ground-clipped view.
        for (var height = 0; height < 5; height++)
        {
            var recoveryPitch = Math.Clamp(-0.18f - height * 0.15f, camera->DirVMin, camera->DirVMax);
            candidates.Add((current, recoveryPitch, FramingScore(current, recoveryPitch, distance)));
        }
        // If native collision has already left scenery between the lens and the
        // character, search the full circle instead of trusting the framing shortlist.
        if (this.characterVisibility < 1f || bestScore.FightVisibility < 1f)
        {
            for (var step = -12; step <= 12; step++)
            {
                var recoveryYaw = current + step * MathF.PI / 12f;
                candidates.Add((recoveryYaw, currentPitch, FramingScore(recoveryYaw, currentPitch, distance)));
            }
        }
        for (var candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            var candidate = candidates[candidateIndex];
            // Framing and collision only subtract from this bound. Do not use
            // nominal-distance framing as a bound: a shortened boom can improve it.
            var ceiling = new CameraViewScore(CombatCameraPolicy.ViewScore(1f, true, candidate.Yaw, current, fightYaw)
                - 0.5f * MathF.Abs(candidate.Pitch - currentPitch)
                - 1.2f * MathF.Abs(candidate.Pitch + 0.33f), 0f, 0f);
            if (!ceiling.CouldImprove(bestScore))
            {
                this.prunedViews++;
                continue;
            }
            var room = ViewClearance(pivot, candidate.Yaw, candidate.Pitch, distance);
            var framing = FramingScore(candidate.Yaw, candidate.Pitch, distance * room);
            if (!(framing with { Fight = framing.Fight - 8f * (1f - room) }).CouldImprove(bestScore))
            {
                this.prunedViews++;
                continue;
            }
            if (Score(candidate.Yaw, candidate.Pitch, room, distance, framing, bestScore) is not { } score)
            {
                this.prunedViews++;
                continue;
            }
            this.evaluatedViews++;
            if (!score.Improves(bestScore))
                continue;
            if (!this.TransitionClear(current, currentPitch, distance, candidate.Yaw, candidate.Pitch, distance,
                    (Vector3)camera->SceneCamera.Position))
                continue;
            best = candidate.Yaw;
            bestPitch = candidate.Pitch;
            bestScore = score;
        }
        this.targetYaw = best;
        this.targetPitch = bestPitch;
        this.fightVisibility = bestScore.FightVisibility;
        this.framingPenalty = CombatCameraPolicy.FramingPenalty(this.subjects, pivot, best,
            bestPitch, distance, projectionScale);
        // Widen only for a material fight/surroundings gain, never merely to fit
        // scattered allies. Keep the wider setting to avoid zoom breathing.
        if (this.config.CameraAutoZoom && float.IsFinite(camera->MaxDistance) && camera->MaxDistance > distance && camera->MaxDistance <= 50f)
        {
            var baseline = ZoomPenalty(distance);
            var bestPenalty = baseline;
            for (var step = 1; step <= 4; step++)
            {
                var candidateDistance = distance + (camera->MaxDistance - distance) * step / 4f;
                var penalty = ZoomPenalty(candidateDistance);
                if (!CombatCameraPolicy.ZoomImproves(bestPenalty, penalty, candidateDistance - this.targetDistance) ||
                    !CombatCameraPolicy.ZoomImproves(baseline, penalty, candidateDistance - distance))
                    continue;
                var room = ViewClearance(pivot, best, bestPitch, candidateDistance);
                var score = Score(best, bestPitch, room, candidateDistance)!.Value;
                if (CombatCameraPolicy.FramingPenalty(this.subjects, pivot, best, bestPitch, candidateDistance, projectionScale) >
                        this.framingPenalty + 0.01f ||
                    room < 0.99f || score.GroundClearance < 1f || score.CharacterVisibility < 1f ||
                    score.FightVisibility < 1f || !this.TransitionClear(current, currentPitch, distance,
                        best, bestPitch, candidateDistance, (Vector3)camera->SceneCamera.Position))
                    continue;
                this.targetDistance = candidateDistance;
                bestPenalty = penalty;
            }
        }

        float ZoomPenalty(float viewDistance)
        {
            var penalty = CombatCameraPolicy.FramingPenalty(this.subjects, pivot, best, bestPitch, viewDistance, projectionScale);
            if (this.FreshArena() is { } observed && observed.BoundingRadius >= 18f &&
                observed.Contains(new(player.X, player.Z)))
            {
                CameraSubject[] surroundings = [new(new(observed.Center.X, player.Y, observed.Center.Y),
                    observed.BoundingRadius, 0f, 1f)];
                penalty = CombatCameraPolicy.ZoomCoveragePenalty(penalty,
                    CombatCameraPolicy.FramingPenalty(surroundings, pivot, best, bestPitch, viewDistance, projectionScale));
            }
            return penalty;
        }

        CameraViewScore FramingScore(float angle, float viewPitch, float viewDistance)
            => new(CombatCameraPolicy.ViewScore(1f, true, angle, current, fightYaw)
                - CombatCameraPolicy.FramingPenalty(this.subjects, pivot, angle, viewPitch, viewDistance, projectionScale)
                - 0.5f * MathF.Abs(viewPitch - currentPitch)
                - 1.2f * MathF.Abs(viewPitch + 0.33f),
                -CombatCameraPolicy.FramingPenalty(this.partySubjects, pivot, angle, viewPitch, viewDistance, projectionScale),
                -CombatCameraPolicy.FramingPenalty(this.raidSubjects, pivot, angle, viewPitch, viewDistance, projectionScale));

        CameraViewScore? Score(float angle, float viewPitch, float room, float viewDistance,
            CameraViewScore? capturedFraming = null, CameraViewScore? incumbent = null, CameraViewClearance? physical = null)
        {
            var position = pivot + CombatCameraPolicy.CameraOffset(angle, viewPitch, viewDistance * room);
            var framing = capturedFraming ?? FramingScore(angle, viewPitch, viewDistance * room);
            var bound = framing with
            {
                GroundClearance = physical?.Ground ?? GroundClearance(position, angle),
                Fight = framing.Fight - 8f * (1f - room),
            };
            // Resolve priorities in order. Unknown visibility is optimistically 1;
            // reject only when even that bound cannot improve the incumbent.
            if (incumbent is { } bestGround && !bound.CouldImprove(bestGround))
                return null;
            bound = bound with { CharacterVisibility = physical?.Character ?? CharacterVisibility(position, player, angle) };
            if (incumbent is { } bestCharacter && !bound.CouldImprove(bestCharacter))
                return null;
            var towardFight = fight + new Vector3(0f, 1f, 0f) - position;
            var length = towardFight.Length();
            var visible = length < 0.1f || !this.Raycast(
                position, towardFight / length, out _, length);
            var fightVisibility = physical?.Fight ?? (visible ? FightVisibility(position, fight, angle) : 0f);
            var fightScore = bound.Fight - (visible ? 0f : 2f);
            bound = bound with { FightVisibility = fightVisibility, Fight = fightScore };
            if (incumbent is { } bestFight && !bound.CouldImprove(bestFight))
                return null;
            // Keep useful space around both the player and fight visible, rather
            // than accepting a corner that leaves only the subjects unobstructed.
            var side = CombatCameraPolicy.CameraRight(angle);
            var halfWidth = Math.Clamp(viewDistance * 0.2f, 1.5f, 5f);
            var clearSamples = 0;
            var testedSamples = 0;
            const int sampleCount = 10;
            for (var row = 0; row < 2; row++)
            {
                var center = row == 0 ? pivot : fight + new Vector3(0f, 1.5f, 0f);
                for (var sample = -2; sample <= 2; sample++)
                {
                    var subject = center + side * (sample * halfWidth / 2f);
                    var towardSubject = subject - position;
                    var subjectDistance = towardSubject.Length();
                    if (subjectDistance < 0.1f || !this.Raycast(
                            position, towardSubject / subjectDistance, out _, subjectDistance))
                        clearSamples++;
                    testedSamples++;
                    var upperVisibility = (float)(clearSamples + sampleCount - testedSamples) / sampleCount;
                    bound = bound with
                    {
                        FightVisibility = physical?.Fight ?? MathF.Min(upperVisibility, fightVisibility),
                        Fight = fightScore - 8f * (1f - upperVisibility),
                    };
                    if (incumbent is { } bestForeground && !bound.CouldImprove(bestForeground))
                        return null;
                }
            }
            return bound;
        }
    }

    private CameraArena? FreshArena() => this.scene?.Arena;

    private bool TransitionClear(float yaw, float pitch, float distance,
        float nextYaw, float nextPitch, float nextDistance, Vector3? actualCamera = null)
    {
        var pivot = this.playerPosition + Vector3.UnitY * 1.5f;
        var arc = MathF.Atan2(MathF.Sin(nextYaw - yaw), MathF.Cos(nextYaw - yaw));
        var steps = Math.Max(1, (int)MathF.Ceiling(MathF.Max(MathF.Abs(arc), MathF.Abs(nextPitch - pitch)) / (MathF.PI / 36f)));
        steps = Math.Max(steps, (int)MathF.Ceiling(MathF.Abs(nextDistance - distance)));
        var room = ViewClearance(pivot, yaw, pitch, distance);
        var origin = actualCamera ?? pivot + CombatCameraPolicy.CameraOffset(yaw, pitch, distance * room);
        var ground = GroundClearance(origin, yaw);
        var character = CharacterVisibility(origin, this.playerPosition, yaw);
        var fight = FightVisibility(origin, this.fightPosition, yaw);
        for (var step = 1; step <= steps; step++)
        {
            var fraction = (float)step / steps;
            var angle = yaw + arc * fraction;
            var tilt = pitch + (nextPitch - pitch) * fraction;
            var length = distance + (nextDistance - distance) * fraction;
            var clearance = ViewClearance(pivot, angle, tilt, length);
            var position = pivot + CombatCameraPolicy.CameraOffset(angle, tilt, length * clearance);
            if (!CombatCameraPolicy.AllowsTransition(new(room, ground, character, fight),
                    new(clearance, GroundClearance(position, angle),
                        CharacterVisibility(position, this.playerPosition, angle),
                        FightVisibility(position, this.fightPosition, angle)), nextDistance > distance))
                return false;
            var travel = position - origin;
            var travelDistance = travel.Length();
            if (travelDistance > 0.1f && this.Raycast(origin,
                    travel / travelDistance, out _, travelDistance))
                return false;
            origin = position;
        }
        return true;
    }

    private float FightVisibility(Vector3 camera, Vector3 fight, float yaw)
    {
        var side = CombatCameraPolicy.CameraRight(yaw) * 2f;
        var clear = 0;
        // A small horizontal fan catches a wall or prop filling the fight view
        // even when one center ray happens to pass a doorway or a corner.
        for (var sample = -1; sample <= 1; sample++)
        {
            var delta = fight + Vector3.UnitY * 1.5f + side * sample - camera;
            var distance = delta.Length();
            if (distance < 0.1f || !this.Raycast(camera, delta / distance, out _, distance))
                clear++;
        }
        return clear / 3f;
    }

    private float CharacterVisibility(Vector3 camera, Vector3 player, float yaw)
    {
        if (!float.IsFinite(camera.X) || !float.IsFinite(camera.Y) || !float.IsFinite(camera.Z))
            return 0f;
        var right = CombatCameraPolicy.CameraRight(yaw) * 0.35f;
        var clear = 0;
        const int samples = 5;
        for (var sample = 0; sample < samples; sample++)
        {
            var subject = player + (sample switch
            {
                0 => Vector3.UnitY * 0.2f,
                1 => Vector3.UnitY,
                2 => Vector3.UnitY * 1.8f,
                3 => Vector3.UnitY + right,
                _ => Vector3.UnitY - right,
            });
            var direction = subject - camera;
            var distance = direction.Length();
            // Stop short of the character so the floor or nearby terrain at the
            // endpoint does not count as a foreground obstruction.
            var probeDistance = distance - 0.35f;
            if (distance < 0.1f || probeDistance <= 0f ||
                !this.Raycast(camera, direction / distance, out _, probeDistance))
                clear++;
        }
        return (float)clear / samples;
    }

    private float GroundClearance(Vector3 position, float yaw)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
            return 0f;
        var clearance = 1f;
        var right = CombatCameraPolicy.CameraRight(yaw) * 0.4f;
        var back = CombatCameraPolicy.CameraOffset(yaw, 0f, 0.4f);
        // Start above the lens so shallow penetration is detected even when a
        // ray originating inside the floor would miss its front face.
        for (var sample = 0; sample < 5; sample++)
        {
            var offset = sample switch
            {
                1 => right,
                2 => -right,
                3 => back,
                4 => -back,
                _ => Vector3.Zero,
            };
            var origin = position + offset + Vector3.UnitY * 3f;
            if (this.Raycast(origin, -Vector3.UnitY, out var hit, 3.6f))
                clearance = MathF.Min(clearance, CombatCameraPolicy.GroundClearance(position.Y, hit.Y));
        }
        return clearance;
    }

    private float ViewClearance(Vector3 pivot, float yaw, float pitch, float distance)
    {
        var offset = CombatCameraPolicy.CameraOffset(yaw, pitch, distance);
        var side = CombatCameraPolicy.CameraRight(yaw) * 0.35f;
        var room = 1f;
        for (var i = -1; i <= 3; i++)
        {
            var origin = pivot + (i <= 1 ? side * i : Vector3.UnitY * (i == 2 ? -0.4f : 0.4f));
            if (this.Raycast(origin, offset / distance, out var hit, distance))
                room = MathF.Min(room, Math.Clamp((Vector3.Distance(origin, hit) - 0.4f) / distance, 0f, 1f));
        }
        return room;
    }

    private bool Raycast(Vector3 origin, Vector3 direction, out Vector3 point, float distance)
    {
        var sample = this.raycasts.Read(new(origin, direction, distance));
        point = sample.Point;
        return sample.Hit;
    }

    private static CameraRayHit ReadNativeRay(CameraRay ray)
    {
        var hit = BGCollisionModule.RaycastMaterialFilter(ray.Origin, ray.Direction, out var result, ray.Distance);
        return new(hit, hit ? (Vector3)result.Point : default);
    }

    private static bool HasManualInput(NativeCamera* camera, bool includeDeltas = true)
    {
        var input = UIInputData.Instance();
        // Window focus is not camera input: automated combat can run in the background.
        if (input == null ||
            includeDeltas && (camera->InputDeltaH != 0f || camera->InputDeltaV != 0f) ||
            input->CurrentMouseDragButtons != 0 ||
            input->CursorInputs.MouseButtonHeldFlags != 0 || input->CursorInputs.MouseWheel != 0 ||
            Math.Abs(input->GamepadInputs.LeftStickX) > 0.1f || Math.Abs(input->GamepadInputs.LeftStickY) > 0.1f ||
            Math.Abs(input->GamepadInputs2.LeftStickX) > 0.1f || Math.Abs(input->GamepadInputs2.LeftStickY) > 0.1f ||
            Math.Abs(input->GamepadInputs.RightStickX) > 0.1f || Math.Abs(input->GamepadInputs.RightStickY) > 0.1f ||
            Math.Abs(input->GamepadInputs2.RightStickX) > 0.1f || Math.Abs(input->GamepadInputs2.RightStickY) > 0.1f)
            return true;
        foreach (var key in ManualControlKeys)
        {
            if (input->IsInputIdDown(key))
                return true;
        }
        return false;
    }

    private void Hold(string reason)
    {
        this.yaw = null;
        this.lastTick = 0;
        this.pitch = null;
        this.Status = new(reason, this.updates, null, null);
    }

    private void ReleaseHook()
    {
        this.inputHook?.Dispose();
        this.inputHook = null;
        this.pitch = null;
        this.yawHook?.Dispose();
        this.yawHook = null;
        this.hookedCamera = 0;
        this.yaw = null;
        this.lastTick = 0;
    }

    public void Dispose()
    {
        this.scene = null;
        this.eligible = false;
        this.ReleaseHook();
    }
}
