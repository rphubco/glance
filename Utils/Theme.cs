namespace Glance.Utils;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Numerics;

public static class Theme
{
    public static readonly Vector4
        FrameBg = new(0.11f, 0.11f, 0.11f, 0.95f),
        FrameBorder = new(0.4f, 0.35f, 0.25f, 1),
        FrameBorderInner = new(0.25f, 0.22f, 0.18f, 1),
        NameColor = new(1, 0.9f, 0.7f, 1),
        GoldColor = new(1, 0.85f, 0.5f, 1),
        ValueColor = new(0.85f, 0.82f, 0.75f, 1),
        LabelColor = new(0.68f, 0.65f, 0.6f, 1),
        LabelColorDim = new(0.45f, 0.42f, 0.38f, 1),
        WorldColor = new(0.6f, 0.6f, 0.55f, 1),
        SeparatorColor = new(0.35f, 0.32f, 0.25f, 0.6f),
        ButtonBg = new(0.18f, 0.16f, 0.14f, 0.9f),
        ButtonHovered = new(0.28f, 0.24f, 0.18f, 1),
        ButtonActive = new(0.35f, 0.3f, 0.22f, 1),
        PrimaryButtonBg = new(0.35f, 0.28f, 0.18f, 0.95f),
        PrimaryButtonHover = new(0.45f, 0.36f, 0.22f, 1),
        PrimaryButtonText = new(1, 0.95f, 0.85f, 1),
        Success = new(0.4f, 0.9f, 0.4f, 1),
        Warning = new(0.9f, 0.8f, 0.2f, 1),
        Error = new(0.9f, 0.4f, 0.4f, 1),
        Info = new(0.4f, 0.7f, 1, 1),
        PlaceholderBg = new(0.08f, 0.08f, 0.08f, 1),
        TooltipBg = new(0.1f, 0.1f, 0.1f, 0.98f),
        TextMuted = new(0.65f, 0.63f, 0.58f, 1),  
        Primary = new(0.55f, 0.65f, 1, 1),
        VersionColor = new(0.35f, 0.32f, 0.28f, 1);

    public static float Padding => 10f * ImGuiHelpers.GlobalScale;
    public static float CornerAccentSize => 6f * ImGuiHelpers.GlobalScale;
    public static float CornerAccentSizeLarge => 8f * ImGuiHelpers.GlobalScale;

    public static IDisposable PushStyle()
    {
        var vars = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, 4f)
            .Push(ImGuiStyleVar.WindowPadding, new Vector2(Padding))
            .Push(ImGuiStyleVar.WindowBorderSize, 2f)
            .Push(ImGuiStyleVar.FrameRounding, 2f)
            .Push(ImGuiStyleVar.ItemSpacing, new Vector2(6, 4) * ImGuiHelpers.GlobalScale)
            .Push(ImGuiStyleVar.ChildRounding, 2f);

        var colors = ImRaii.PushColor(ImGuiCol.WindowBg, FrameBg)
            .Push(ImGuiCol.Border, FrameBorder)
            .Push(ImGuiCol.Separator, SeparatorColor)
            .Push(ImGuiCol.FrameBg, ButtonBg)
            .Push(ImGuiCol.FrameBgHovered, ButtonHovered)
            .Push(ImGuiCol.FrameBgActive, ButtonActive)
            .Push(ImGuiCol.Button, ButtonBg)
            .Push(ImGuiCol.ButtonHovered, ButtonHovered)
            .Push(ImGuiCol.ButtonActive, ButtonActive)
            .Push(ImGuiCol.Header, ButtonBg)
            .Push(ImGuiCol.HeaderHovered, ButtonHovered)
            .Push(ImGuiCol.HeaderActive, ButtonActive)
            .Push(ImGuiCol.Text, ValueColor)
            .Push(ImGuiCol.TitleBg, FrameBg)
            .Push(ImGuiCol.TitleBgActive, new Vector4(0.15f, 0.14f, 0.12f, 1))
            .Push(ImGuiCol.ChildBg, new Vector4(0.09f, 0.09f, 0.09f, 0.5f))
            .Push(ImGuiCol.ScrollbarBg, new Vector4(0.08f, 0.08f, 0.08f, 0.5f))
            .Push(ImGuiCol.ScrollbarGrab, FrameBorderInner)
            .Push(ImGuiCol.ScrollbarGrabHovered, ButtonHovered)
            .Push(ImGuiCol.ScrollbarGrabActive, ButtonActive)
            .Push(ImGuiCol.CheckMark, GoldColor)
            .Push(ImGuiCol.SliderGrab, GoldColor)
            .Push(ImGuiCol.SliderGrabActive, NameColor);

        return new Scope(vars, colors);
    }

    sealed class Scope(IDisposable vars, IDisposable colors) : IDisposable
    {
        public void Dispose() { colors.Dispose(); vars.Dispose(); }
    }

    public static void DrawFrame() => DrawFrame(CornerAccentSize);
    public static void DrawFrame(float sz)
    {
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetWindowPos();
        var s = ImGui.GetWindowSize();
        dl.AddRect(p + new Vector2(3 * ImGuiHelpers.GlobalScale), p + s - new Vector2(3 * ImGuiHelpers.GlobalScale), Col(FrameBorderInner), 2f);
        var c = Col(FrameBorder);
        Corner(dl, p, c, sz, 1, 1);
        Corner(dl, new(p.X + s.X, p.Y), c, sz, -1, 1);
        Corner(dl, new(p.X, p.Y + s.Y), c, sz, 1, -1);
        Corner(dl, p + s, c, sz, -1, -1);
    }

    static void Corner(ImDrawListPtr dl, Vector2 p, uint c, float l, int dx, int dy)
    {
        dl.AddLine(p, new(p.X + l * dx, p.Y), c, 2f);
        dl.AddLine(p, new(p.X, p.Y + l * dy), c, 2f);
    }

    public static void Centered(string t, Vector4 col)
    {
        ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(t).X) / 2);
        ImGui.TextColored(col, t);
    }

    public static uint Col(Vector4 v) => ImGui.ColorConvertFloat4ToU32(v);
}
