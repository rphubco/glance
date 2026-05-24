namespace Glance.Utils;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Glance.Core;
using System;
using System.Collections.Generic;
using System.Numerics;

public static class UI
{
    public static float XXs => 2 * ImGuiHelpers.GlobalScale;
    public static float Xs => 4 * ImGuiHelpers.GlobalScale;
    public static float Sm => 8 * ImGuiHelpers.GlobalScale;
    public static float Md => 12 * ImGuiHelpers.GlobalScale;
    public static float Lg => 16 * ImGuiHelpers.GlobalScale;
    public static float Xl => 24 * ImGuiHelpers.GlobalScale;

    static readonly Dictionary<string, float> _a = [], _f = [];

    public static float Animate(string id, float target, float spd = 8f)
    {
        _a.TryGetValue(id, out var c);
        return _a[id] = c + (target - c) * Math.Min(spd * ImGui.GetIO().DeltaTime, 1f);
    }
    public static void Section(string title)
    {
        ImGui.TextColored(Theme.GoldColor, title);
        var p = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddLine(p, p + new Vector2(ImGui.GetContentRegionAvail().X, 0), Theme.Col(Theme.SeparatorColor));
        Space(Sm);
    }

    public static void Space() => Space(Sm);
    public static void Space(float h) => ImGui.Dummy(new Vector2(0, h));

    public static void Divider()
    {
        Space(Xs);
        var p = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddLine(p, p + new Vector2(ImGui.GetContentRegionAvail().X, 0), Theme.Col(Theme.SeparatorColor with { W = 0.4f }));
        Space(Xs);
    }

     public static bool QuickAction(string icon, string label, float w, string? tip = null)
    {
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        var h = 50f * ImGuiHelpers.GlobalScale;
        var clicked = ImGui.InvisibleButton($"##qa_{label}", new Vector2(w, h));
        var hov = ImGui.IsItemHovered();
        dl.AddRectFilled(p, p + new Vector2(w, h), Theme.Col(hov ? Theme.ButtonHovered : Theme.ButtonBg), 6f);
        dl.AddRect(p, p + new Vector2(w, h), Theme.Col(Theme.FrameBorderInner), 6f);
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var isz = ImGui.CalcTextSize(icon);
            dl.AddText(p + new Vector2((w - isz.X) / 2, 10 * ImGuiHelpers.GlobalScale), Theme.Col(hov ? Theme.GoldColor : Theme.LabelColor), icon);
        }
        Vector2 lsz;
        using (Globals.Fonts.Small.Push())
        {
            lsz = ImGui.CalcTextSize(label);
            dl.AddText(p + new Vector2((w - lsz.X) / 2, h - lsz.Y - 6 * ImGuiHelpers.GlobalScale), Theme.Col(Theme.ValueColor), label);
        }
        if (hov) { ImGui.SetMouseCursor(ImGuiMouseCursor.Hand); if (tip != null) ImGui.SetTooltip(tip); }
        return clicked;
    }
    public static void QuickActions(params (string Icon, string Label, string? Tip, Action Act)[] acts)
    {
        if (acts.Length == 0) return;
        var w = (ImGui.GetContentRegionAvail().X - 4 * ImGuiHelpers.GlobalScale * (acts.Length - 1)) / acts.Length;
        for (var i = 0; i < acts.Length; i++)
        {
            if (i > 0) { ImGui.SameLine(); ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 4 * ImGuiHelpers.GlobalScale); }
            if (QuickAction(acts[i].Icon, acts[i].Label, w, acts[i].Tip)) acts[i].Act();
        }
        Space(Sm);
    }
}
