namespace Glance.UI.Windows;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Glance.Utils;
using Glance.Core;
using Glance.UI.Tabs;
using System.Numerics;

public sealed class NearbyWindow : Window
{
    const ImGuiWindowFlags Flags =
        ImGuiWindowFlags.NoCollapse |
        ImGuiWindowFlags.NoDocking;

    public NearbyWindow() : base("Nearby Roleplayers##RPHubDetached", Flags)
    {
        Size = new Vector2(340, 480);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 300),
            MaximumSize = new Vector2(500, 800)
        };
    }

    public override void PreDraw() => Theme.PushStyle();
    public override void PostDraw() => Theme.PopStyle();

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
