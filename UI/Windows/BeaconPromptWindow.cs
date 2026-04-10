namespace Glance.UI.Windows;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Glance.Core;
using System;
using System.Numerics;

public sealed class BeaconPromptWindow : Window
{
    bool _decided;

    public BeaconPromptWindow() : base("Glance - Nearby Players##BeaconPrompt",
        ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove)
    {
        Size = new Vector2(520, 420);
        SizeCondition = ImGuiCond.Always;
        PositionCondition = ImGuiCond.Always;
    }

    public override void PreDraw()
    {
        var viewport = ImGui.GetMainViewport();
        var center = viewport.GetCenter();
        Position = new Vector2(center.X - 260, center.Y - 210);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(16, 16));
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.08f, 0.08f, 0.10f, 0.98f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.45f, 0.35f, 0.75f, 0.6f));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar();
    }

    public override void Draw()
    {
        var avail = ImGui.GetContentRegionAvail();

        ImGui.PushFont(UiBuilder.DefaultFont);
        ImGui.SetWindowFontScale(1.2f);
        CenteredText("Enable Nearby Players?", new Vector4(0.72f, 0.62f, 0.92f, 1f));
        ImGui.SetWindowFontScale(1f);
        ImGui.PopFont();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var boxHeight = avail.Y - 80;
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.05f, 0.05f, 0.07f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.3f, 0.3f, 0.35f, 1f));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1f);

        if (ImGui.BeginChild("##BeaconPromptBody", new Vector2(-1, boxHeight), true))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.88f, 0.88f, 0.88f, 1f));
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X - 8);

            ImGui.TextWrapped(
                "Nearby Players lets other Glance users discover you in-game. " +
                "This is how the plugin tints nearby profiles on nameplates and populates the Nearby tab.");

            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.7f, 0.85f, 0.7f, 1f), "When enabled, the plugin sends:");
            ImGui.Spacing();
            ImGui.TextWrapped("\u2022 Your character name and home world");
            ImGui.TextWrapped("\u2022 Your current zone (or housing plot id)");
            ImGui.TextWrapped("\u2022 One update per minute while logged in");

            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.85f, 0.7f, 0.7f, 1f), "What is never sent:");
            ImGui.Spacing();
            ImGui.TextWrapped("\u2022 No coordinates, position, or movement data");
            ImGui.TextWrapped("\u2022 No information about other players you see");
            ImGui.TextWrapped("\u2022 Data is automatically deleted 2-3 minutes after you log out or disable the feature");

            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.65f, 0.65f, 0.72f, 1f),
                "You can change this at any time from the plugin's settings. " +
                "See https://rphub.co/privacy-policy for full details.");

            ImGui.PopTextWrapPos();
            ImGui.PopStyleColor();
        }
        ImGui.EndChild();

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(2);

        ImGui.Spacing();

        var buttonWidth = 200f;
        var buttonHeight = 32f;
        var spacing = 20f;
        var totalWidth = buttonWidth * 2 + spacing;
        var startX = (avail.X - totalWidth) / 2;

        ImGui.SetCursorPosX(startX);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.28f, 0.28f, 0.3f, 0.8f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.38f, 0.38f, 0.4f, 0.9f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.48f, 0.48f, 0.5f, 1f));
        if (ImGui.Button("Keep Disabled", new Vector2(buttonWidth, buttonHeight)))
        {
            Globals.Config.BeaconEnabled = false;
            Globals.Config.BeaconLocationSharing = false;
            Globals.Config.HasSeenBeaconPrompt = true;
            Globals.Config.Save();
            _decided = true;
            IsOpen = false;
            Globals.MainWindow.IsOpen = true;
            Globals.MainWindow.BringToFront();
        }
        ImGui.PopStyleColor(3);

        ImGui.SameLine(0, spacing);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.22f, 0.6f, 0.9f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.4f, 0.3f, 0.72f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.48f, 0.36f, 0.82f, 1f));
        if (ImGui.Button("Enable Nearby Players", new Vector2(buttonWidth, buttonHeight)))
        {
            Globals.Config.BeaconEnabled = true;
            Globals.Config.BeaconLocationSharing = true;
            Globals.Config.HasSeenBeaconPrompt = true;
            Globals.Config.Save();
            _decided = true;
            IsOpen = false;
            Globals.MainWindow.IsOpen = true;
            Globals.MainWindow.BringToFront();
        }
        ImGui.PopStyleColor(3);
    }

    void CenteredText(string text, Vector4 color)
    {
        var textWidth = ImGui.CalcTextSize(text).X;
        ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - textWidth) / 2 + ImGui.GetCursorPosX());
        ImGui.TextColored(color, text);
    }

    public override bool DrawConditions() => !_decided;
}
