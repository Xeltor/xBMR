// Camera scoring and smoothing retained from XCAI 4a0f7968. No movement authority.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BossMod.CombatAssistance.Camera;

internal readonly record struct CameraViewClearance(float Room, float Ground, float Character, float Fight);

internal readonly record struct CameraSubject(Vector3 Position, float Radius, float Height, float Weight);

// Compare priorities separately so even a full raid cannot outweigh the fight.
internal readonly record struct CameraViewScore(float Fight, float Party, float Raid) : IComparable<CameraViewScore>
{
    public float GroundClearance { get; init; } = 1f;
    public float CharacterVisibility { get; init; } = 1f;
    public float FightVisibility { get; init; } = 1f;

    internal bool Improves(CameraViewScore other)
        => this.GroundClearance > other.GroundClearance + 0.01f ||
            (this.GroundClearance >= other.GroundClearance &&
                (this.CharacterVisibility > other.CharacterVisibility + 0.01f ||
                    (this.CharacterVisibility >= other.CharacterVisibility &&
                        (this.FightVisibility > other.FightVisibility + 0.01f ||
                            (this.FightVisibility >= other.FightVisibility && ImprovesFraming(other))))));

    private bool ImprovesFraming(CameraViewScore other)
        => this.Fight > other.Fight + 0.15f ||
            (this.Fight == other.Fight && (this.Party > other.Party + 0.15f ||
                (this.Party == other.Party && this.Raid > other.Raid + 0.15f)));

    // Each component is an upper bound, not necessarily one attainable view.
    // Improves itself is unsuitable here: a slightly higher fight bound can hide
    // an actual equal-fight candidate whose party framing wins the tie.
    internal bool CouldImprove(CameraViewScore other)
        => this.GroundClearance > other.GroundClearance ||
            (this.GroundClearance >= other.GroundClearance &&
                (this.CharacterVisibility > other.CharacterVisibility ||
                    (this.CharacterVisibility >= other.CharacterVisibility &&
                        (this.FightVisibility > other.FightVisibility ||
                            (this.FightVisibility >= other.FightVisibility &&
                                (this.Fight > other.Fight + 0.15f ||
                                    (this.Fight >= other.Fight &&
                                        (this.Party > other.Party + 0.15f ||
                                            (this.Party >= other.Party && this.Raid > other.Raid + 0.15f)))))))));

    public int CompareTo(CameraViewScore other)
    {
        var ground = this.GroundClearance.CompareTo(other.GroundClearance);
        if (ground != 0)
            return ground;
        var character = this.CharacterVisibility.CompareTo(other.CharacterVisibility);
        if (character != 0)
            return character;
        var visibility = this.FightVisibility.CompareTo(other.FightVisibility);
        if (visibility != 0)
            return visibility;
        var fight = this.Fight.CompareTo(other.Fight);
        if (fight != 0)
            return fight;
        var party = this.Party.CompareTo(other.Party);
        return party != 0 ? party : this.Raid.CompareTo(other.Raid);
    }
}

internal static class CombatCameraPolicy
{
    internal static float GroundClearance(float cameraHeight, float groundHeight)
        => Math.Clamp((cameraHeight - groundHeight) / 0.6f, 0f, 1f);

    internal static float TargetYaw(Vector3 player, Vector3 target)
        => MathF.Atan2(player.X - target.X, player.Z - target.Z);

    internal static Vector3 CameraOffset(float yaw, float pitch, float distance)
        => new(MathF.Sin(yaw) * MathF.Cos(pitch) * distance,
            -MathF.Sin(pitch) * distance, MathF.Cos(yaw) * MathF.Cos(pitch) * distance);

    internal static Vector3 CameraRight(float yaw)
        => new(MathF.Cos(yaw), 0f, -MathF.Sin(yaw));

    internal static float AngleDistance(float a, float b)
        => MathF.Abs(MathF.Atan2(MathF.Sin(a - b), MathF.Cos(a - b)));

    internal static float ViewScore(float clearance, bool visible, float yaw, float current, float fight)
        => 8f * Math.Clamp(clearance, 0f, 1f) + (visible ? 2f : 0f)
            - 3.5f * MathF.Max(0f, AngleDistance(yaw, fight) - MathF.PI / 12f)
            - 0.7f * AngleDistance(yaw, current);

    // Project candidate views using the current native lens and proposed boom length.
    // Coordinates are normalized to the visible frame: +/-1 are the screen edges.
    internal static Vector2 Project(Vector3 subject, Vector3 pivot, float yaw, float pitch,
        float distance, Vector2 projectionScale)
    {
        var back = CameraOffset(yaw, pitch, 1f);
        var right = CameraRight(yaw);
        var up = Vector3.Cross(back, right);
        return Project(subject, pivot + back * distance, back, right, up, projectionScale);
    }

    private static Vector2 Project(Vector3 subject, Vector3 position, Vector3 back,
        Vector3 right, Vector3 up, Vector2 projectionScale)
    {
        var relative = subject - position;
        var depth = -Vector3.Dot(relative, back);
        if (depth <= 0.1f)
            return new Vector2(10f);
        return new Vector2(Vector3.Dot(relative, right) * projectionScale.X / depth,
            Vector3.Dot(relative, up) * projectionScale.Y / depth);
    }

    internal static float FramingPenalty(IReadOnlyList<CameraSubject> subjects, Vector3 pivot,
        float yaw, float pitch, float distance, Vector2 projectionScale)
    {
        if (subjects.Count == 0)
            return 0f;
        var total = 0f;
        var weight = 0f;
        var worst = 0f;
        // The lens basis is identical for every sample of this candidate view.
        var back = CameraOffset(yaw, pitch, 1f);
        var right = CameraRight(yaw);
        var up = Vector3.Cross(back, right);
        var position = pivot + back * distance;
        foreach (var subject in subjects)
        {
            var center = Project(subject.Position + Vector3.UnitY * subject.Height * 0.5f,
                position, back, right, up, projectionScale);
            var penalty = 2f * MathF.Max(0f, MathF.Abs(center.X) - 0.25f);
            // Include feet, upper body and lateral extent: a visible center alone
            // can still mean a boss or party member is clipped by a screen edge.
            for (var sample = 0; sample < 4; sample++)
            {
                var point = subject.Position + (sample switch
                {
                    0 => Vector3.Zero,
                    1 => Vector3.UnitY * subject.Height,
                    2 => Vector3.UnitY * subject.Height * 0.5f - right * subject.Radius,
                    _ => Vector3.UnitY * subject.Height * 0.5f + right * subject.Radius,
                });
                var screen = Project(point, position, back, right, up, projectionScale);
                var edge = MathF.Max(MathF.Abs(screen.X), MathF.Abs(screen.Y));
                penalty += 3f * MathF.Max(0f, edge - 0.65f) + 12f * MathF.Max(0f, edge - 0.85f);
            }
            total += penalty * subject.Weight;
            weight += subject.Weight;
            worst = MathF.Max(worst, penalty);
        }
        // Keep a single excluded party member significant even in a full party.
        return total / MathF.Max(weight, 1f) + 0.5f * worst;
    }

    internal static bool AllowsTransition(CameraViewClearance current, CameraViewClearance next, bool zoomingOut)
        => float.IsFinite(current.Room) && float.IsFinite(current.Ground) &&
            float.IsFinite(current.Character) && float.IsFinite(current.Fight) &&
            float.IsFinite(next.Room) && float.IsFinite(next.Ground) &&
            float.IsFinite(next.Character) && float.IsFinite(next.Fight) &&
            next.Room + 0.01f >= current.Room && (!zoomingOut || next.Room >= 0.99f) &&
            next.Ground + 0.01f >= current.Ground && next.Character + 0.01f >= current.Character &&
            next.Fight + 0.01f >= current.Fight;

    // Zoom only outward, inside native limits. Small gains do not justify moving
    // the lens; easing and a speed cap avoid snaps and repeated zoom breathing.
    internal static float ZoomStep(float current, float target, float maximum, float elapsedSeconds)
    {
        if (!float.IsFinite(current) || !float.IsFinite(target) || !float.IsFinite(maximum) ||
            !float.IsFinite(elapsedSeconds) || current <= 0f || maximum <= current)
            return current;
        target = Math.Clamp(target, current, maximum);
        var dt = Math.Clamp(elapsedSeconds, 0f, 0.1f);
        var change = (target - current) * (1f - MathF.Exp(-dt / 0.8f));
        return current + MathF.Min(change, 4f * dt);
    }

    internal static float ZoomCoveragePenalty(float fightPenalty, float surroundingsPenalty)
        => fightPenalty + 0.8f * MathF.Sqrt(MathF.Max(0f, surroundingsPenalty));

    internal static bool ZoomImproves(float currentPenalty, float candidatePenalty, float extraDistance)
        => float.IsFinite(currentPenalty) && float.IsFinite(candidatePenalty) &&
            float.IsFinite(extraDistance) && extraDistance > 0f &&
            currentPenalty - candidatePenalty > 0.35f + 0.08f * extraDistance;

    internal static float ActiveInputDelta(float current, float desired)
    {
        var delta = MathF.Atan2(MathF.Sin(desired - current), MathF.Cos(desired - current));
        // Zero relinquishes native camera-input ownership to walking pivot.
        // Alternate around the fixed desired angle at subpixel precision, without drift.
        return MathF.Abs(delta) < 0.00001f ? MathF.CopySign(0.00001f, delta) : delta;
    }

    internal static float Step(float current, float target, float elapsedSeconds, float speed = 1f)
    {
        var delta = MathF.Atan2(MathF.Sin(target - current), MathF.Cos(target - current));
        const float deadzone = 3f * MathF.PI / 180f;
        if (MathF.Abs(delta) <= deadzone)
            return current;

        var dt = Math.Clamp(elapsedSeconds, 0f, 0.1f);
        speed = float.IsFinite(speed) ? Math.Clamp(speed, 0.25f, 3f) : 1f;
        // Ease toward the edge of the framing tolerance, so crossing it does
        // not suddenly switch from zero motion to a visible correction.
        var correction = MathF.CopySign(MathF.Abs(delta) - deadzone, delta);
        var change = correction * (1f - MathF.Exp(-dt * speed / 0.7f));
        var limit = 40f * MathF.PI / 180f * dt * speed;
        return MathF.Atan2(MathF.Sin(current + Math.Clamp(change, -limit, limit)),
            MathF.Cos(current + Math.Clamp(change, -limit, limit)));
    }
}
