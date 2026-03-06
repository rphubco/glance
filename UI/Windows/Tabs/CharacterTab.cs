namespace Glance.UI.Tabs;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Glance.Utils;
using Glance.Core;
using Glance.UI;
using Glance.UI.Components;
using System;
using System.Numerics;

public static class CharacterTab
{
    public static void Draw()
    {
        if (ProfileEditor.IsActive)
        {
            string? name = null, world = null;
            if (Globals.Objects.LocalPlayer is { } lp)
            {
                name = lp.Name.TextValue;
                world = lp.HomeWorld.Value.Name.ExtractText();
            }
            ProfileEditor.Draw(name, world, onSaved: () => _ = Globals.Profiles.FetchProfilesAsync());
            return;
        }

        UserHeader.Draw();
        UI.Space(4);

        var cnt = Globals.Profiles.Data?.Characters?.Length ?? 0;
        var canCreate = cnt < 8;

        if (Globals.Objects.LocalPlayer is { } lp2)
        {
            var isVerified = Globals.Profiles.Data?.CurrentVerified == true;
            var playerName = lp2.Name.TextValue;
            var playerWorld = lp2.HomeWorld.Value.Name.ExtractText();

            if (!isVerified)
            {
                DrawUnverifiedBanner(playerName, playerWorld);
                UI.Space(12);
            }

            ImGui.TextColored(Theme.LabelColor, "Character:");
            ImGui.SameLine();
            ImGui.TextColored(Theme.NameColor, playerName);

            if (isVerified)
            {
                ImGui.SameLine();
                ImGui.TextColored(Theme.Success, "(Verified)");
            }

            UI.Space(8);
            ImGui.TextColored(Theme.GoldColor, "Your Profiles");

            ImGui.SameLine(0, 5f);
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.SetWindowFontScale(0.9f); 
            ImGui.TextColored(Theme.LabelColorDim, FontAwesomeIcon.QuestionCircle.ToIconString());
            ImGui.SetWindowFontScale(1f);
            ImGui.PopFont();

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextColored(Theme.GoldColor, "Privacy & Profiles");
                ImGui.Separator();
                ImGui.BulletText("To hide your profile from others: Check 'Hide Profile' in Settings.");
                ImGui.BulletText("To unlink completely: Use the interface on the RPHub website.");
                ImGui.BulletText("Changes may take a few moments to sync across all users.");
                ImGui.EndTooltip();
            }

            ImGui.Separator();
            UI.Space(10);

            if (Globals.Objects.LocalPlayer is { } pc)
            {
                ProfileList.Draw();
            }

            UI.QuickActions((FontAwesomeIcon.Plus.ToIconString(), "Create New Profile", canCreate ? null : "Limit (8/8)", () => { if (canCreate) ProfileEditor.OpenForCreate(); }));
        }
        else
        {
            ImGui.TextColored(Theme.Warning, "Log in to a character.");
        }
    }

    static void DrawUnverifiedBanner(string name, string world)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        const float padding = 12f;

        var contentHeight = ImGui.GetTextLineHeight() * 8 + padding * 2 + 65;

        var max = pos + new Vector2(width, contentHeight);

        dl.AddRectFilled(pos, max, Theme.Col(new Vector4(0.6f, 0.4f, 0.1f, 0.3f)), 6f);
        dl.AddRect(pos, max, Theme.Col(new Vector4(1f, 0.7f, 0.2f, 0.6f)), 6f, ImDrawFlags.None, 2f);

        ImGui.SetCursorScreenPos(pos + new Vector2(padding, padding));
        ImGui.BeginGroup();

        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(new Vector4(1f, 0.7f, 0.2f, 1f), FontAwesomeIcon.ExclamationTriangle.ToIconString());
        ImGui.PopFont();
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), "Character Not Verified");

        UI.Space(6);

        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width - padding * 2);
        ImGui.TextColored(Theme.ValueColor, "Verification proves you own this character, preventing impersonation. Once verified:");
        UI.Space(4);
        ImGui.TextColored(Theme.LabelColor, "• Your profile is visible to other plugin users");
        ImGui.TextColored(Theme.LabelColor, "• Your profile will remain visible even if you disable plugin or switch to game console.");
        UI.Space(4);
        ImGui.TextColored(Theme.TextMuted, "You can still create and edit profiles while unverified.");
        ImGui.PopTextWrapPos();

        UI.Space(8);

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.5f, 0.1f, 0.8f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.6f, 0.2f, 0.9f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.7f, 0.4f, 0.1f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));

        if (ImGui.Button("Verify on RPHub.co", new Vector2(160, 28)))
        {
            var url = $"https://rphub.co/verify?name={Uri.EscapeDataString(name)}&world={Uri.EscapeDataString(world)}";
            Dalamud.Utility.Util.OpenLink(url);
        }

        ImGui.PopStyleColor(4);

        ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.3f, 0.3f, 0.8f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.4f, 0.4f, 0.4f, 0.9f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.25f, 0.25f, 0.25f, 1f));

        ImGui.PushFont(UiBuilder.IconFont);
        if (ImGui.Button(FontAwesomeIcon.Sync.ToIconString(), new Vector2(28, 28)))
            _ = Globals.Profiles.FetchProfilesAsync();
        ImGui.PopFont();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Refresh verification status");

        ImGui.PopStyleColor(3);

        ImGui.SameLine();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 6);
        ImGui.TextColored(Theme.TextMuted, "Takes ~1 minute");

        ImGui.EndGroup();

        ImGui.SetCursorScreenPos(new Vector2(pos.X, max.Y + 4));
    }
}
