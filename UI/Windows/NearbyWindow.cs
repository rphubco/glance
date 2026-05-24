namespace Glance.UI.Windows;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Glance.Utils;
using Glance.Core;
using Glance.UI.Tabs;
using System;
using System.Numerics;

public sealed class NearbyWindow : Window
{
    const ImGuiWindowFlags Flags =
        ImGuiWindowFlags.NoCollapse |
        ImGuiWindowFlags.NoDocking;

    public NearbyWindow() : base("Nearby Roleplayers##RPHubDetached", Flags)
    {
        Size = new Vector2(340, 480) * ImGuiHelpers.GlobalScale;
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 300) * ImGuiHelpers.GlobalScale,
            MaximumSize = new Vector2(500, 800) * ImGuiHelpers.GlobalScale
        };
    }

    IDisposable? _themeScope;
    public override void PreDraw() => _themeScope = Theme.PushStyle();
    public override void PostDraw() { _themeScope?.Dispose(); _themeScope = null; }

    public override void Draw()
    {
        Theme.DrawFrame();
        NearbyTab.Draw();
    }

    public override void OnClose()
    {
        NearbyTab.ClearSelection();
    }
}
