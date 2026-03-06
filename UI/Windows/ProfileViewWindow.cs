namespace Glance.UI.Windows;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Glance.Utils;
using Glance.Core;
using Glance.Models;
using Glance.UI.Tabs;
using System.Numerics;

public sealed class ProfileViewWindow : Window
{
    float _alpha;
    bool _init;

    public ProfileViewWindow() : base("Profile##GlanceView", ImGuiWindowFlags.NoCollapse)
    {
        Size = new Vector2(450, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(300, 200), MaximumSize = new Vector2(600, 800) };
    }

    public void Show(string? name, string? world, ProfileData? data, string? profileId = null)
    {
        if (name == null || world == null) return;
        ProfileTab.Show(name, world, data, profileId);
        Globals.MainWindow.ShowProfile();
    }

    public void UpdateData(string name, string world, CachedProfile? profile)
    {
        if (!IsOpen || ProfileTab.CurrentName != name || ProfileTab.CurrentWorld != world) return;
        if (profile?.Data != null)
            ProfileTab.Show(name, world, profile.Data, profile.Id.ToString());
    }

    public override void PreDraw() => Theme.PushStyle();
    public override void PostDraw() => Theme.PopStyle();

    public override void Draw()
    {
        Theme.DrawFrame();
        if (!_init) { _init = true; _alpha = 0f; }
        _alpha = UI.Animate("profile_fade", 1f, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, _alpha);
        ProfileTab.Draw();
        ImGui.PopStyleVar();
    }
}
