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
        M3.ElementBaseline = 0.75f;
        M3.Initialize(pluginInterface, Service.Config.Get<RebornUI.InterfaceConfig>());
    }

    internal static Scope Push()
    {
        var material = M3Style.Push(M3Density.Compact);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Surface);
        ImGui.PushStyleColor(ImGuiCol.Border, Border);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, RaisedSurface);
        return new Scope(material);
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

}
