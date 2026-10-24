// Material layout and controls adapted from XelsCombatAI 4a0f7968; native settings ownership.
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using RebornMaterial;
using System;
using System.Numerics;

namespace BossMod.CombatAssistance.UI;

internal static class MaterialTheme
{
    // Aether teal supplies the Material role palette; structural shades stay deliberately quieter.
    internal static readonly Vector4 Primary = Rgb(0x4DB6AC);
    internal static readonly Vector4 Surface = Rgb(0x101C1B);
    internal static readonly Vector4 RaisedSurface = Rgb(0x182E2B);
    internal static readonly Vector4 Border = Rgb(0x315C56);

    internal static void Initialize(IDalamudPluginInterface pluginInterface)
    {
        M3.ElementBaseline = 1f;
        M3.Initialize(pluginInterface, new M3Settings { UiAccentColor = Primary });
    }

    internal static Scope Push()
    {
        var material = M3Style.Push(M3Density.Compact);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Surface);
        ImGui.PushStyleColor(ImGuiCol.Border, Border);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, RaisedSurface);
        return new Scope(material);
    }

    internal static CardScope BeginCard(string id, string? title = null, string? subtitle = null)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var previousHeight = M3CardHost.PreviousHeight(id);
        if (previousHeight > 0)
            ImGui.GetWindowDrawList().AddRectFilled(origin, origin + new Vector2(width, previousHeight),
                M3.U32(RaisedSurface), M3.ShapeMedium);
        var material = M3Card.Begin(id, title, accent: Primary, style: M3CardStyle.Outlined, subtitle: subtitle);
        return new CardScope(material, origin, width);
    }

    private static Vector4 Rgb(uint rgb) => new((rgb >> 16 & 255) / 255f, (rgb >> 8 & 255) / 255f, (rgb & 255) / 255f, 1f);

    internal readonly struct Scope(M3Style.Scope material) : IDisposable
    {
        public void Dispose()
        {
            ImGui.PopStyleColor(3);
            material.Dispose();
        }
    }

    internal readonly struct CardScope(M3Card.Scope material, Vector2 origin, float width) : IDisposable
    {
        public void Dispose()
        {
            var bottom = Math.Max(ImGui.GetCursorScreenPos().Y + M3Card.BottomPadding, origin.Y + 40f * M3.Scale);
            material.Dispose();
            ImGui.GetWindowDrawList().AddRect(origin, new Vector2(origin.X + width, bottom),
                M3.U32(Border), M3.ShapeMedium, ImDrawFlags.None, M3.Scale);
        }
    }
}
