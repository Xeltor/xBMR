// Drawing primitives and color/style values retained from XCAI 4a0f7968.
// Decision collection is native; no standalone controller or arrival override is used.
using BossMod.CombatAssistance.Config;
using Dalamud.Bindings.ImGui;

namespace BossMod.CombatAssistance.UI;

internal sealed partial class DecisionOverlayController
{
    private readonly List<Vector2> filledShapePoints = new(50);
    private enum OverlayVisualKind
    {
        ActiveDestination,
        PreviewDestination,
        MovementPath,
        ZoneRing,
        ActionFootprint,
        PositionalArc,
        BlockedMarker,
        ContextMarker,
        CompactCallout
    }

    private enum OverlayVisualProminence
    {
        Passive,
        Preview,
        Active,
        Blocked
    }

    private sealed record OverlayVisual(
        OverlayVisualKind Kind,
        OverlayVisualProminence Prominence,
        DecisionOverlaySource Source,
        DecisionOverlayState State,
        Vector3 Anchor,
        DecisionOverlayShape? Shape,
        DecisionOverlayLine? Line,
        DecisionOverlayMarker? Marker,
        string? Label,
        int Rank,
        float Alpha);

    private sealed record OverlayBadge(DecisionOverlayState State, DecisionOverlaySource Source, string Text);

    private void DrawVisual(ImDrawListPtr drawList, OverlayVisual visual)
    {
        if (visual.Alpha <= 0.02f)
        {
            return;
        }

        switch (visual.Kind)
        {
            case OverlayVisualKind.ActiveDestination:
            case OverlayVisualKind.PreviewDestination:
                this.DrawDestinationPad(drawList, visual);
                break;
            case OverlayVisualKind.MovementPath:
                if (visual.Line != null)
                {
                    var dashed = visual.Prominence == OverlayVisualProminence.Preview;
                    this.DrawPathLine(
                        drawList,
                        visual.Line.From,
                        visual.Line.To,
                        ColorFor(visual.State, visual.Source, visual.Alpha),
                        LineThickness(visual.Source, visual.State),
                        dashed);
                }

                break;
            case OverlayVisualKind.ActionFootprint:
            case OverlayVisualKind.ZoneRing:
            case OverlayVisualKind.PositionalArc:
                if (visual.Shape != null)
                {
                    this.DrawShape(drawList, visual.Shape, visual.Source, ColorFor(visual.State, visual.Source, visual.Alpha), visual.Alpha);
                }

                break;
            case OverlayVisualKind.BlockedMarker:
                this.DrawBlockedMarker(drawList, visual.Anchor, visual.Source, visual.Alpha);
                break;
            case OverlayVisualKind.ContextMarker:
                if (visual.Marker != null)
                {
                    this.DrawContextMarker(drawList, visual);
                }

                break;
            case OverlayVisualKind.CompactCallout:
                break;
        }
    }

    private void DrawDestinationPad(ImDrawListPtr drawList, OverlayVisual visual)
    {
        var color = ColorFor(visual.State, visual.Source, visual.Alpha);
        var fill = FillColorFor(visual.State, visual.Source, visual.Alpha * 1.8f);
        var radius = visual.Kind == OverlayVisualKind.PreviewDestination ? 0.46f : 0.56f;
        this.DrawCircleFilled(drawList, visual.Anchor, radius * 1.45f, fill);
        if (visual.Kind == OverlayVisualKind.PreviewDestination)
        {
            this.DrawDashedCircle(drawList, visual.Anchor, radius, color, 2.4f);
        }
        else
        {
            this.DrawCircle(drawList, visual.Anchor, radius, color, 3.3f);
            this.DrawCircle(drawList, visual.Anchor, radius * 0.45f, color, 2.2f);
        }

        if (config.DecisionOverlayDensity != OverlayDensity.Minimal && !string.IsNullOrWhiteSpace(visual.Label))
        {
            this.DrawLabel(drawList, visual.Anchor, visual.Label, color);
        }
    }

    private void DrawContextMarker(ImDrawListPtr drawList, OverlayVisual visual)
    {
        var marker = visual.Marker!;
        var markerRadius = MarkerRadius(visual.Source, marker);
        var markerColor = ColorFor(marker.State, visual.Source, visual.Alpha);
        if (ShouldFillMarker(marker.State, visual.Source))
        {
            this.DrawCircleFilled(drawList, marker.Position, markerRadius, MarkerFillColorFor(marker.State, visual.Source, visual.Alpha));
        }

        this.DrawCircle(drawList, marker.Position, markerRadius, markerColor, thickness: MarkerThickness(visual.Source, marker.State));
        if (marker.Label != null && config.DecisionOverlayDensity == OverlayDensity.Detailed)
        {
            this.DrawLabel(drawList, marker.Position, marker.Label, markerColor);
        }
    }

    private void DrawBlockedMarker(ImDrawListPtr drawList, Vector3 anchor, DecisionOverlaySource source, float alpha)
    {
        var color = ColorFor(DecisionOverlayState.Rejected, source, alpha);
        this.DrawDashedCircle(drawList, anchor, 0.52f, color, 3f);
        this.DrawCross(drawList, anchor, 0.34f, color, 2.6f);
    }

    private void DrawShape(ImDrawListPtr drawList, DecisionOverlayShape shape, DecisionOverlaySource source, uint color, float alpha = 1f)
    {
        var fillColor = FillColorFor(shape.State, source, alpha);
        switch (shape.Kind)
        {
            case DecisionOverlayShapeKind.Circle:
                if (ShouldFillShape(shape.State, source) || source is DecisionOverlaySource.AoE or DecisionOverlaySource.NextAction)
                {
                    this.DrawCircleFilled(drawList, shape.Origin, shape.Radius, fillColor);
                }
                if (shape.State == DecisionOverlayState.Future)
                {
                    this.DrawDashedCircle(drawList, shape.Origin, shape.Radius, color, ShapeThickness(source, shape.State));
                }
                else
                {
                    this.DrawCircle(drawList, shape.Origin, shape.Radius, color, ShapeThickness(source, shape.State));
                }
                break;
            case DecisionOverlayShapeKind.Cone:
                if (ShouldFillShape(shape.State, source) || source is DecisionOverlaySource.AoE or DecisionOverlaySource.NextAction or DecisionOverlaySource.PassageOfArms or DecisionOverlaySource.TankBehavior)
                {
                    this.DrawConeFilled(drawList, shape.Origin, shape.Radius, shape.RotationRadians, shape.HalfWidth, fillColor);
                }
                if (source == DecisionOverlaySource.Positionals)
                {
                    this.DrawPositionalArc(drawList, shape, color);
                    break;
                }

                this.DrawCone(drawList, shape.Origin, shape.Radius, shape.RotationRadians, shape.HalfWidth, color, ShapeThickness(source, shape.State), shape.State == DecisionOverlayState.Future);
                break;
            case DecisionOverlayShapeKind.Rectangle:
                if (ShouldFillShape(shape.State, source) || source is DecisionOverlaySource.AoE or DecisionOverlaySource.NextAction)
                {
                    this.DrawRectangleFilled(drawList, shape.Origin, shape.RotationRadians, shape.Length, shape.HalfWidth, fillColor);
                }
                this.DrawRectangle(drawList, shape.Origin, shape.RotationRadians, shape.Length, shape.HalfWidth, color, ShapeThickness(source, shape.State), shape.State == DecisionOverlayState.Future);
                break;
        }
    }

    private void DrawCircle(ImDrawListPtr drawList, Vector3 center, float radius, uint color, float thickness)
    {
        Vector2? previous = null;
        foreach (var unit in OverlayCircleSamples.Outline)
        {
            var point = center + new Vector3(unit.X * radius, 0f, unit.Y * radius);
            if (!this.Project(point, out var screen))
            {
                previous = null;
                continue;
            }

            if (previous.HasValue)
            {
                drawList.AddLine(previous.Value, screen, ShadowColor(), thickness + 2.5f);
                drawList.AddLine(previous.Value, screen, color, thickness);
            }

            previous = screen;
        }
    }

    private void DrawCircleFilled(ImDrawListPtr drawList, Vector3 center, float radius, uint color)
    {
        var points = this.filledShapePoints;
        points.Clear();
        foreach (var unit in OverlayCircleSamples.Fill)
        {
            var point = center + new Vector3(unit.X * radius, 0f, unit.Y * radius);
            if (this.Project(point, out var screen))
            {
                points.Add(screen);
            }
        }

        if (points.Count >= 3)
        {
            DrawFilledPolygon(drawList, points, color);
        }
    }

    private void DrawCone(ImDrawListPtr drawList, Vector3 center, float radius, float rotation, float halfWidth, uint color, float thickness, bool dashed = false)
    {
        var left = center + Direction(rotation - halfWidth) * radius;
        var right = center + Direction(rotation + halfWidth) * radius;
        this.DrawLine(drawList, center, left, color, thickness, dashed: dashed);
        this.DrawLine(drawList, center, right, color, thickness, dashed: dashed);

        const int Segments = 24;
        Vector2? previous = null;
        for (var i = 0; i <= Segments; ++i)
        {
            var angle = rotation - halfWidth + 2f * halfWidth * i / Segments;
            var point = center + Direction(angle) * radius;
            if (!this.Project(point, out var screen))
            {
                previous = null;
                continue;
            }

            if (previous.HasValue && (!dashed || i % 3 != 1))
            {
                drawList.AddLine(previous.Value, screen, ShadowColor(), thickness + 2.5f);
                drawList.AddLine(previous.Value, screen, color, thickness);
            }

            previous = screen;
        }
    }

    private void DrawPositionalArc(ImDrawListPtr drawList, DecisionOverlayShape shape, uint color)
    {
        var thickness = shape.State switch
        {
            DecisionOverlayState.Active => 3.2f,
            DecisionOverlayState.Candidate => 3.6f,
            DecisionOverlayState.Future => 2.8f,
            _ => 2.2f
        };
        var start = shape.RotationRadians - shape.HalfWidth;
        var end = shape.RotationRadians + shape.HalfWidth;
        var dashed = shape.State == DecisionOverlayState.Future;
        this.DrawArc(drawList, shape.Origin, shape.Radius, start, end, color, thickness, dashed);
        this.DrawRadialTick(drawList, shape.Origin, shape.Radius, start, color, thickness);
        this.DrawRadialTick(drawList, shape.Origin, shape.Radius, end, color, thickness);
        if (shape.State is DecisionOverlayState.Candidate or DecisionOverlayState.Active)
        {
            this.DrawRadialTick(drawList, shape.Origin, shape.Radius, shape.RotationRadians, color, thickness);
        }
    }

    private void DrawArc(ImDrawListPtr drawList, Vector3 center, float radius, float start, float end, uint color, float thickness, bool dashed)
    {
        const int MinimumSegments = 8;
        var segments = Math.Max(MinimumSegments, (int)(MathF.Abs(end - start) / MathF.Tau * 96f));
        Vector2? previous = null;
        for (var i = 0; i <= segments; ++i)
        {
            var angle = start + (end - start) * i / segments;
            var point = center + Direction(angle) * radius;
            if (!this.Project(point, out var screen))
            {
                previous = null;
                continue;
            }

            if (previous.HasValue && (!dashed || i % 3 != 1))
            {
                drawList.AddLine(previous.Value, screen, ShadowColor(), thickness + 2.8f);
                drawList.AddLine(previous.Value, screen, color, thickness);
            }

            previous = screen;
        }
    }

    private void DrawRadialTick(ImDrawListPtr drawList, Vector3 center, float radius, float angle, uint color, float thickness)
    {
        var direction = Direction(angle);
        var outer = center + direction * radius;
        var inner = center + direction * MathF.Max(0.1f, radius - 0.75f);
        this.DrawLine(drawList, inner, outer, color, thickness);
    }

    private void DrawConeFilled(ImDrawListPtr drawList, Vector3 center, float radius, float rotation, float halfWidth, uint color)
    {
        const int Segments = 24;
        var points = this.filledShapePoints;
        points.Clear();
        if (!this.Project(center, out var centerScreen))
        {
            return;
        }

        points.Add(centerScreen);
        for (var i = 0; i <= Segments; ++i)
        {
            var angle = rotation - halfWidth + 2f * halfWidth * i / Segments;
            var point = center + Direction(angle) * radius;
            if (this.Project(point, out var screen))
            {
                points.Add(screen);
            }
        }

        if (points.Count >= 3)
        {
            DrawFilledPolygon(drawList, points, color);
        }
    }

    private void DrawRectangle(ImDrawListPtr drawList, Vector3 origin, float rotation, float length, float halfWidth, uint color, float thickness, bool dashed = false)
    {
        var forward = Direction(rotation);
        var side = new Vector3(forward.Z, 0f, -forward.X);
        var p1 = origin + side * halfWidth;
        var p2 = origin - side * halfWidth;
        var p3 = origin + forward * length - side * halfWidth;
        var p4 = origin + forward * length + side * halfWidth;
        this.DrawLine(drawList, p1, p2, color, thickness, dashed: dashed);
        this.DrawLine(drawList, p2, p3, color, thickness, dashed: dashed);
        this.DrawLine(drawList, p3, p4, color, thickness, dashed: dashed);
        this.DrawLine(drawList, p4, p1, color, thickness, dashed: dashed);
    }

    private void DrawRectangleFilled(ImDrawListPtr drawList, Vector3 origin, float rotation, float length, float halfWidth, uint color)
    {
        var forward = Direction(rotation);
        var side = new Vector3(forward.Z, 0f, -forward.X);
        var p1 = origin + side * halfWidth;
        var p2 = origin - side * halfWidth;
        var p3 = origin + forward * length - side * halfWidth;
        var p4 = origin + forward * length + side * halfWidth;
        if (this.Project(p1, out var s1) &&
            this.Project(p2, out var s2) &&
            this.Project(p3, out var s3) &&
            this.Project(p4, out var s4))
        {
            DrawFilledPolygon(drawList, [s1, s2, s3, s4], color);
        }
    }

    private static void DrawFilledPolygon(ImDrawListPtr drawList, IReadOnlyList<Vector2> points, uint color)
    {
        for (var i = 1; i + 1 < points.Count; ++i)
        {
            drawList.AddTriangleFilled(points[0], points[i], points[i + 1], color);
        }
    }

    private void DrawLine(ImDrawListPtr drawList, Vector3 from, Vector3 to, uint color, float thickness, bool arrow = false, bool dashed = false)
    {
        if (this.Project(from, out var fromScreen) && this.Project(to, out var toScreen))
        {
            if (arrow)
            {
                DrawArrowLine(drawList, fromScreen, toScreen, color, thickness);
                return;
            }

            if (dashed)
            {
                DrawDashedScreenLine(drawList, fromScreen, toScreen, color, thickness);
                return;
            }

            DrawScreenLine(drawList, fromScreen, toScreen, color, thickness);
        }
    }

    private void DrawPathLine(ImDrawListPtr drawList, Vector3 from, Vector3 to, uint color, float thickness, bool dashed)
    {
        if (!this.Project(from, out var fromScreen) || !this.Project(to, out var toScreen))
        {
            return;
        }

        var direction = toScreen - fromScreen;
        var length = direction.Length();
        if (length <= 3f)
        {
            return;
        }

        direction /= length;
        var start = fromScreen + direction * MathF.Min(16f, length * 0.18f);
        var end = toScreen - direction * MathF.Min(18f, length * 0.22f);
        if (dashed)
        {
            DrawDashedScreenLine(drawList, start, end, color, thickness);
        }
        else
        {
            DrawScreenLine(drawList, start, end, color, thickness);
        }

        DrawPathChevron(drawList, start, end, color, thickness);
    }

    private void DrawDashedCircle(ImDrawListPtr drawList, Vector3 center, float radius, uint color, float thickness)
    {
        const int Segments = 48;
        Vector2? previous = null;
        for (var i = 0; i <= Segments; ++i)
        {
            var angle = i * MathF.Tau / Segments;
            var point = center + new Vector3(MathF.Sin(angle) * radius, 0f, MathF.Cos(angle) * radius);
            if (!this.Project(point, out var screen))
            {
                previous = null;
                continue;
            }

            if (previous.HasValue && i % 4 < 2)
            {
                DrawScreenLine(drawList, previous.Value, screen, color, thickness);
            }

            previous = screen;
        }
    }

    private void DrawCross(ImDrawListPtr drawList, Vector3 center, float radius, uint color, float thickness)
    {
        this.DrawLine(drawList, center + new Vector3(-radius, 0f, -radius), center + new Vector3(radius, 0f, radius), color, thickness);
        this.DrawLine(drawList, center + new Vector3(-radius, 0f, radius), center + new Vector3(radius, 0f, -radius), color, thickness);
    }

    private void DrawLabel(ImDrawListPtr drawList, Vector3 world, string label, uint color, float pixelYOffset = 0f)
    {
        if (!this.Project(world, out var screen))
        {
            return;
        }

        var textSize = ImGui.CalcTextSize(label);
        // Center horizontally, stack upward from floor point; pixelYOffset shifts additional labels further up.
        var pos = screen + new Vector2(-textSize.X * 0.5f, -textSize.Y - 4f - pixelYOffset);
        var padding = new Vector2(4f, 2f);
        drawList.AddRectFilled(
            pos - padding,
            pos + textSize + padding,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.58f)),
            4f);
        drawList.AddText(pos, color, label);
    }

    private void DrawBadgeGroup(ImDrawListPtr drawList, Vector3 anchor, IReadOnlyList<OverlayBadge> badges)
    {
        if (badges.Count == 0 || !this.Project(anchor, out var anchorScreen))
        {
            return;
        }

        var visibleBadges = badges
            .Where(badge => badge.State != DecisionOverlayState.Suppressed)
            .OrderBy(badge => LabelSortPriority(badge.State))
            .ThenBy(badge => badge.Source)
            .Take(4)
            .ToArray();
        if (visibleBadges.Length == 0)
        {
            return;
        }

        var padding = new Vector2(5f, 3f);
        var badgeHeight = ImGui.GetTextLineHeight() + padding.Y * 2f;
        var gap = 4f;
        var widths = new float[visibleBadges.Length];
        var totalWidth = 0f;
        for (var i = 0; i < visibleBadges.Length; ++i)
        {
            widths[i] = ImGui.CalcTextSize(visibleBadges[i].Text).X + padding.X * 2f;
            totalWidth += widths[i];
            if (i > 0)
            {
                totalWidth += gap;
            }
        }

        var size = new Vector2(totalWidth, badgeHeight);
        var displaySize = ImGui.GetIO().DisplaySize;
        var pos = anchorScreen + new Vector2(-size.X * 0.5f, -size.Y - 18f);
        if (pos.X + size.X > displaySize.X - 8f)
        {
            pos.X = displaySize.X - size.X - 8f;
        }

        if (pos.Y < 8f)
        {
            pos.Y = anchorScreen.Y + 14f;
        }

        pos.X = Math.Clamp(pos.X, 8f, MathF.Max(8f, displaySize.X - size.X - 8f));
        pos.Y = Math.Clamp(pos.Y, 8f, MathF.Max(8f, displaySize.Y - size.Y - 8f));

        var bg = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.50f));
        var border = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.18f));
        drawList.AddLine(anchorScreen, new Vector2(pos.X, pos.Y + size.Y * 0.5f), border, 1f);
        var cursor = pos;
        for (var i = 0; i < visibleBadges.Length; ++i)
        {
            var badge = visibleBadges[i];
            var badgeSize = new Vector2(widths[i], badgeHeight);
            var fill = BadgeFillColorFor(badge.State, badge.Source);
            var textColor = BadgeTextColorFor(badge.State, badge.Source);
            drawList.AddRectFilled(cursor, cursor + badgeSize, bg, 4f);
            drawList.AddRectFilled(cursor, cursor + new Vector2(4f, badgeSize.Y), fill, 4f);
            drawList.AddRect(cursor, cursor + badgeSize, border, 4f);
            drawList.AddText(cursor + padding, textColor, badge.Text);
            cursor.X += badgeSize.X + gap;
        }
    }

    private bool Project(Vector3 world, out Vector2 screen)
    {
        return Service.GameGui.WorldToScreen(world, out screen);
    }

    private static Vector3 Direction(float rotation)
    {
        var (sin, cos) = MathF.SinCos(rotation);
        return new Vector3(sin, 0f, cos);
    }

    private static Vector3 Midpoint(Vector3 from, Vector3 to)
    {
        return new Vector3((from.X + to.X) * 0.5f, (from.Y + to.Y) * 0.5f, (from.Z + to.Z) * 0.5f);
    }

    private static Vector3 ShapeLabelAnchor(DecisionOverlayShape shape)
    {
        return shape.Kind switch
        {
            DecisionOverlayShapeKind.Cone => shape.Origin + Direction(shape.RotationRadians) * MathF.Max(0.5f, shape.Radius * 0.65f),
            DecisionOverlayShapeKind.Rectangle => shape.Origin + Direction(shape.RotationRadians) * MathF.Max(0.5f, shape.Length * 0.5f),
            _ => shape.Origin + new Vector3(0f, 0f, MathF.Max(0.5f, shape.Radius * 0.65f))
        };
    }

    private static void DrawArrowLine(ImDrawListPtr drawList, Vector2 from, Vector2 to, uint color, float thickness)
    {
        var direction = to - from;
        var length = direction.Length();
        if (length <= 1f)
        {
            return;
        }

        direction /= length;
        var tipInset = Math.Clamp(thickness * 1.2f, 2f, 5f);
        var tip = to - direction * tipInset;
        drawList.AddLine(from, tip, ShadowColor(), thickness + 2.5f);
        drawList.AddLine(from, tip, color, thickness);

        var headLength = Math.Clamp(thickness * 4.8f, 11f, 18f);
        var headWidth = Math.Clamp(thickness * 2.2f, 5f, 9f);
        DrawArrowChevron(drawList, tip, direction, headLength, headWidth, ShadowColor(), thickness + 2.5f);
        DrawArrowChevron(drawList, tip, direction, headLength, headWidth, color, thickness);

        if (length > 110f)
        {
            var mid = from + direction * (length * 0.58f);
            DrawArrowChevron(drawList, mid, direction, headLength * 0.62f, headWidth * 0.62f, ShadowColor(), MathF.Max(1.5f, thickness + 1.6f));
            DrawArrowChevron(drawList, mid, direction, headLength * 0.62f, headWidth * 0.62f, color, MathF.Max(1.2f, thickness * 0.72f));
        }
    }

    private static void DrawPathChevron(ImDrawListPtr drawList, Vector2 from, Vector2 to, uint color, float thickness)
    {
        var direction = to - from;
        var length = direction.Length();
        if (length <= 14f)
        {
            return;
        }

        direction /= length;
        var headLength = Math.Clamp(thickness * 4f, 9f, 15f);
        var headWidth = Math.Clamp(thickness * 1.8f, 4f, 8f);
        var tip = from + direction * (length * 0.62f);
        DrawArrowChevron(drawList, tip, direction, headLength, headWidth, ShadowColor(), thickness + 2.2f);
        DrawArrowChevron(drawList, tip, direction, headLength, headWidth, color, thickness);
    }

    private static void DrawScreenLine(ImDrawListPtr drawList, Vector2 from, Vector2 to, uint color, float thickness)
    {
        drawList.AddLine(from, to, ShadowColor(), thickness + 2.5f);
        drawList.AddLine(from, to, color, thickness);
    }

    private static void DrawDashedScreenLine(ImDrawListPtr drawList, Vector2 from, Vector2 to, uint color, float thickness)
    {
        var direction = to - from;
        var length = direction.Length();
        if (length <= 1f)
        {
            return;
        }

        direction /= length;
        const float Dash = 10f;
        const float Gap = 7f;
        for (var offset = 0f; offset < length; offset += Dash + Gap)
        {
            var start = from + direction * offset;
            var end = from + direction * MathF.Min(length, offset + Dash);
            DrawScreenLine(drawList, start, end, color, thickness);
        }
    }

    private static void DrawArrowChevron(ImDrawListPtr drawList, Vector2 tip, Vector2 direction, float length, float width, uint color, float thickness)
    {
        var side = new Vector2(-direction.Y, direction.X);
        var basePoint = tip - direction * length;
        drawList.AddLine(basePoint + side * width, tip, color, thickness);
        drawList.AddLine(basePoint - side * width, tip, color, thickness);
    }

    private static int LabelSortPriority(DecisionOverlayState state)
    {
        return state switch
        {
            DecisionOverlayState.Active => 0,
            DecisionOverlayState.Candidate => 1,
            DecisionOverlayState.Future => 2,
            DecisionOverlayState.Rejected => 3,
            DecisionOverlayState.Suppressed => 4,
            _ => 5
        };
    }

    private static bool ShouldFillShape(DecisionOverlayState state, DecisionOverlaySource source)
    {
        _ = state;
        _ = source;
        return false;
    }

    private static bool ShouldDrawShapeLabel(DecisionOverlaySource source, DecisionOverlayShape shape)
    {
        if (string.IsNullOrWhiteSpace(shape.Label) || shape.State == DecisionOverlayState.Suppressed)
        {
            return false;
        }

        return source switch
        {
            DecisionOverlaySource.Positionals => false,
            DecisionOverlaySource.TargetUptime => false,
            DecisionOverlaySource.BossCenterAvoidance => false,
            DecisionOverlaySource.GapCloser => false,
            DecisionOverlaySource.NextAction => false,
            DecisionOverlaySource.AoE => false,
            DecisionOverlaySource.HealerCoverage => false,
            DecisionOverlaySource.PartyHealerRange => false,
            DecisionOverlaySource.LeyLines => false,
            DecisionOverlaySource.TankBehavior => false,
            _ => true
        };
    }

    private static bool ShouldDrawLineLabel(DecisionOverlaySource source, DecisionOverlayLine line)
    {
        if (string.IsNullOrWhiteSpace(line.Label) || line.State == DecisionOverlayState.Suppressed)
        {
            return false;
        }

        return false;
    }

    private static float LineThickness(DecisionOverlaySource source, DecisionOverlayState state)
    {
        if (source == DecisionOverlaySource.FinalMovement)
        {
            return 4.5f;
        }

        return state switch
        {
            DecisionOverlayState.Active => 3.4f,
            DecisionOverlayState.Candidate => 3.6f,
            DecisionOverlayState.Future => 2.1f,
            DecisionOverlayState.Rejected => 2.2f,
            _ => 1.5f
        };
    }

    private static float ShapeThickness(DecisionOverlaySource source, DecisionOverlayState state)
    {
        if (source == DecisionOverlaySource.FinalMovement)
        {
            return 4f;
        }

        return state switch
        {
            DecisionOverlayState.Active => 3.2f,
            DecisionOverlayState.Candidate => 3.4f,
            DecisionOverlayState.Future => 1.9f,
            DecisionOverlayState.Rejected => 2.4f,
            DecisionOverlayState.Suppressed => 1.4f,
            _ => 2f
        };
    }

    private static float MarkerRadius(DecisionOverlaySource source, DecisionOverlayMarker marker)
    {
        var radius = Math.Max(marker.Radius, 0.25f);
        return source switch
        {
            DecisionOverlaySource.FinalMovement => MathF.Max(radius, 0.55f),
            DecisionOverlaySource.EscapeLanding => MathF.Max(radius, 0.5f),
            DecisionOverlaySource.GapCloser when marker.State == DecisionOverlayState.Future => MathF.Max(radius, 0.45f),
            DecisionOverlaySource.Positionals => MathF.Max(radius, 0.45f),
            DecisionOverlaySource.PassageOfArms => MathF.Max(radius, 0.4f),
            DecisionOverlaySource.SurvivabilityZone => MathF.Max(radius, 0.4f),
            DecisionOverlaySource.StarryMuse => MathF.Max(radius, 0.4f),
            DecisionOverlaySource.PartyHealerRange => MathF.Max(radius, 0.4f),
            DecisionOverlaySource.TankBehavior => MathF.Max(radius, 0.45f),
            DecisionOverlaySource.LeyLines => MathF.Max(radius, 0.4f),
            _ => radius
        };
    }

    private static bool ShouldFillMarker(DecisionOverlayState state, DecisionOverlaySource source)
    {
        return source is (DecisionOverlaySource.FinalMovement
                   or DecisionOverlaySource.EscapeLanding
                   or DecisionOverlaySource.GapCloser
                   or DecisionOverlaySource.PassageOfArms
                   or DecisionOverlaySource.Positionals
                   or DecisionOverlaySource.SurvivabilityZone
                   or DecisionOverlaySource.StarryMuse
                   or DecisionOverlaySource.TankBehavior
                   or DecisionOverlaySource.PartyHealerRange
                   or DecisionOverlaySource.LeyLines) &&
               state is DecisionOverlayState.Active or DecisionOverlayState.Candidate or DecisionOverlayState.Future;
    }

    private static float MarkerThickness(DecisionOverlaySource source, DecisionOverlayState state)
    {
        if (source is DecisionOverlaySource.FinalMovement or DecisionOverlaySource.EscapeLanding)
        {
            return 3f;
        }

        return state == DecisionOverlayState.Suppressed ? 1.5f : 2.2f;
    }

    private static uint MarkerFillColorFor(DecisionOverlayState state, DecisionOverlaySource source)
    {
        return MarkerFillColorFor(state, source, 1f);
    }

    private static uint MarkerFillColorFor(DecisionOverlayState state, DecisionOverlaySource source, float alphaMultiplier)
    {
        var color = ColorVectorFor(state, source);
        color.W = Math.Clamp(0.18f * alphaMultiplier, 0f, 0.28f);
        return ImGui.GetColorU32(color);
    }

    private static uint BadgeFillColorFor(DecisionOverlayState state, DecisionOverlaySource source)
    {
        return ImGui.GetColorU32(ColorVectorFor(state, source));
    }

    private static uint BadgeTextColorFor(DecisionOverlayState state, DecisionOverlaySource source)
    {
        _ = state;
        _ = source;
        return ImGui.GetColorU32(new Vector4(0.96f, 0.96f, 0.96f, 1f));
    }

    private static uint ShadowColor()
    {
        return ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.68f));
    }

    private static uint ColorFor(DecisionOverlayState state, DecisionOverlaySource source)
    {
        return ImGui.GetColorU32(ColorVectorFor(state, source));
    }

    private static uint ColorFor(DecisionOverlayState state, DecisionOverlaySource source, float alphaMultiplier)
    {
        var color = ColorVectorFor(state, source);
        color.W = Math.Clamp(color.W * alphaMultiplier, 0f, 1f);
        return ImGui.GetColorU32(color);
    }

    private static Vector4 ColorVectorFor(DecisionOverlayState state, DecisionOverlaySource source)
    {
        if (source == DecisionOverlaySource.Positionals)
        {
            return state switch
            {
                DecisionOverlayState.Active => new Vector4(0.20f, 0.95f, 0.35f, 1f),
                DecisionOverlayState.Candidate => new Vector4(1f, 0.18f, 0.12f, 1f),
                DecisionOverlayState.Future => new Vector4(1f, 0.56f, 0.12f, 1f),
                _ => new Vector4(0.58f, 0.58f, 0.58f, 0.82f)
            };
        }

        if (source == DecisionOverlaySource.FinalMovement && state == DecisionOverlayState.Active)
        {
            return new Vector4(0.95f, 1f, 1f, 1f);
        }

        return state switch
        {
            DecisionOverlayState.Active => SourceAccent(source, 1f),
            DecisionOverlayState.Candidate => SourceAccent(source, 1f),
            DecisionOverlayState.Future => new Vector4(1f, 0.86f, 0.25f, 1f),
            DecisionOverlayState.Rejected => new Vector4(1f, 0.2f, 0.2f, 1f),
            DecisionOverlayState.Suppressed => new Vector4(0.58f, 0.58f, 0.58f, 0.82f),
            _ => new Vector4(0.55f, 0.55f, 0.55f, 1f)
        };
    }

    private static uint FillColorFor(DecisionOverlayState state, DecisionOverlaySource source)
    {
        return FillColorFor(state, source, 1f);
    }

    private static uint FillColorFor(DecisionOverlayState state, DecisionOverlaySource source, float alphaMultiplier)
    {
        var alpha = state switch
        {
            DecisionOverlayState.Active => 0.11f,
            DecisionOverlayState.Candidate => 0.09f,
            DecisionOverlayState.Future => 0.055f,
            DecisionOverlayState.Rejected => 0.08f,
            DecisionOverlayState.Suppressed => 0.0f,
            _ => 0.08f
        };
        if (source == DecisionOverlaySource.Positionals)
        {
            alpha = state switch
            {
                DecisionOverlayState.Active => 0.10f,
                DecisionOverlayState.Candidate => 0.12f,
                DecisionOverlayState.Future => 0.07f,
                _ => alpha
            };
        }

        var color = ColorVectorFor(state, source);
        color.W = Math.Clamp(alpha * alphaMultiplier, 0f, 0.22f);
        return ImGui.GetColorU32(color);
    }

    private static Vector4 SourceAccent(DecisionOverlaySource source, float alpha)
    {
        return source switch
        {
            DecisionOverlaySource.AoE => new Vector4(1f, 0.55f, 0.18f, alpha),
            DecisionOverlaySource.HealerCoverage => new Vector4(0.35f, 0.95f, 0.48f, alpha),
            DecisionOverlaySource.PartyHealerRange => new Vector4(0.30f, 0.82f, 0.95f, alpha),
            DecisionOverlaySource.SurvivabilityZone => new Vector4(0.35f, 0.95f, 0.48f, alpha),
            DecisionOverlaySource.PassageOfArms => new Vector4(1f, 0.88f, 0.25f, alpha),
            DecisionOverlaySource.Positionals => new Vector4(0.88f, 0.45f, 1f, alpha),
            DecisionOverlaySource.GapCloser => new Vector4(0.25f, 0.95f, 0.95f, alpha),
            DecisionOverlaySource.EscapeLanding => new Vector4(0.25f, 0.95f, 0.95f, alpha),
            DecisionOverlaySource.TargetUptime => new Vector4(0.35f, 0.68f, 1f, alpha),
            DecisionOverlaySource.LeyLines => new Vector4(0.72f, 0.52f, 1f, alpha),
            DecisionOverlaySource.BossCenterAvoidance => new Vector4(1f, 0.30f, 0.18f, alpha),
            DecisionOverlaySource.NextAction => new Vector4(1f, 0.86f, 0.25f, alpha),
            DecisionOverlaySource.RedMageMeleeCombo => new Vector4(1f, 0.35f, 0.35f, alpha),
            DecisionOverlaySource.TankBehavior => new Vector4(1f, 0.58f, 0.16f, alpha),
            DecisionOverlaySource.FinalMovement => new Vector4(0.95f, 1f, 1f, alpha),
            _ => new Vector4(0.35f, 0.68f, 1f, alpha)
        };
    }
}
