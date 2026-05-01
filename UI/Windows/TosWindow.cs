namespace Glance.UI.Windows;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

public sealed class TosWindow : Window
{

    const string TOS_TEXT = @"TERMS OF SERVICE SUMMARY

By using Glance, you agree to the full Terms of Service at https://rphub.co/terms-of-service

KEY POINTS:

1. USE AT YOUR OWN RISK
Glance is a third-party tool.Use of third-party tools may violate the Final Fantasy XIV User Agreement. RPHub and its operator are not responsible for any actions taken against your Square Enix account, including suspensions or terminations. Glance is not affiliated with Square Enix, Dalamud, or XIVLauncher.

2. BETA SOFTWARE
Glance is currently in Beta.It is experimental and may contain bugs, cause game client instability, crashes, or data loss.The plugin is provided ""AS-IS"" without warranties of any kind.RPHub is not obligated to provide technical support.

3. DATA TRANSMISSION
By using Glance, you authorize the transmission of specific in-game data (such as character identifiers and profile metadata, excluding your Square Enix account identifier) between your game client and RPHub servers. Note: Your profile may be visible to other Glance users in-game even if your website profile is set to unlisted or private.

4. PROHIBITED USE
You may not use Glance, its source code, or the RPHub API to engage in automated data scraping, spamming, reverse engineering of user data, or any activity that disrupts the Service for other users.Violations may result in immediate account termination.

5. ZERO TOLERANCE CONTENT POLICY
RPHub has a zero tolerance policy for NSFW content (especially Lalafell). What you do in-game is your business, but any NSFW, pornographic, or sexually explicit content uploaded to our servers will result in immediate account termination. Content depicting minors in sexual situations is illegal and is subject to reporting to law enforcement. AI-generated imagery is also strictly prohibited. All uploads must be human-made or direct in-game screenshots.

6. SOURCE AVAILABILITY
Glance's source code is publicly available for review at github.com/rphubco/glance under an All Rights Reserved license. You may view and audit the code for security and transparency purposes. Redistribution, modification, or creation of derivative works without written permission is prohibited.

7. NON-COMMERCIAL PROJECT
RPHub is operated as a personal, non-commercial fan project.The Service does not charge fees, sell subscriptions, display advertising, accept donations, or generate revenue of any kind.

8. AGE REQUIREMENT
You must be at least 16 years old to use this Service.

ALL FINAL FANTASY XIV CONTENT IS PROPERTY OF SQUARE ENIX CO., LTD.GLANCE AND RPHUB ARE NOT AFFILIATED WITH SQUARE ENIX.

Full Terms of Service: https://rphub.co/terms-of-service
Privacy Policy: https://rphub.co/privacy-policy";

    float _scrollY;
    float _maxScroll;
    bool _hasScrolledToBottom;
    bool _accepted;

    public bool HasAccepted => _accepted;
    public event Action? OnAccepted;

    public TosWindow() : base("Glance - Terms of Service##TOS",
        ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove)
    {
        Size = new Vector2(500, 450);
        SizeCondition = ImGuiCond.Always;
        PositionCondition = ImGuiCond.Always;
    }

    IDisposable? _styleScope;
    IDisposable? _colorScope;

    public override void PreDraw()
    {
        var viewport = ImGui.GetMainViewport();
        var center = viewport.GetCenter();
        Position = new Vector2(center.X - 250, center.Y - 225);

        _styleScope = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(16, 16));
        _colorScope = ImRaii.PushColor(ImGuiCol.WindowBg, new Vector4(0.08f, 0.08f, 0.10f, 0.98f))
            .Push(ImGuiCol.Border, new Vector4(0.7f, 0.55f, 0.3f, 0.5f));
    }

    public override void PostDraw()
    {
        _colorScope?.Dispose();
        _colorScope = null;
        _styleScope?.Dispose();
        _styleScope = null;
    }

    public override void Draw()
    {
        var avail = ImGui.GetContentRegionAvail();

        using (ImRaii.PushFont(UiBuilder.DefaultFont))
        {
            ImGui.SetWindowFontScale(1.2f);
            CenteredText("Terms of Service", new Vector4(0.85f, 0.65f, 0.3f, 1f));
            ImGui.SetWindowFontScale(1f);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var instructionCol = _hasScrolledToBottom
            ? new Vector4(0.4f, 0.8f, 0.4f, 1f)
            : new Vector4(0.9f, 0.7f, 0.3f, 1f);
        var instructionText = _hasScrolledToBottom
            ? "✓ You may now accept the terms below"
            : "↓ Please scroll down to read the full terms";
        CenteredText(instructionText, instructionCol);

        ImGui.Spacing();

        var boxHeight = avail.Y - 110;
        using (ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.05f, 0.05f, 0.07f, 1f)))
        using (ImRaii.PushColor(ImGuiCol.Border, new Vector4(0.3f, 0.3f, 0.35f, 1f)))
        using (ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, 4f))
        using (ImRaii.PushStyle(ImGuiStyleVar.ChildBorderSize, 1f))
        {
            using var child = ImRaii.Child("##TosScroll", new Vector2(-1, boxHeight), true, ImGuiWindowFlags.AlwaysVerticalScrollbar);
            if (child)
            {
                using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.85f, 0.85f, 0.85f, 1f)))
                {
                    ImGui.SetWindowFontScale(0.95f);
                    ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X - 8);
                    ImGui.TextWrapped(TOS_TEXT);
                    ImGui.PopTextWrapPos();
                    ImGui.SetWindowFontScale(1f);
                }

                _scrollY = ImGui.GetScrollY();
                _maxScroll = ImGui.GetScrollMaxY();

                if (_maxScroll > 0 && _scrollY >= _maxScroll - 10f)
                    _hasScrolledToBottom = true;
            }
        }

        ImGui.Spacing();

        var buttonWidth = 140f;
        var buttonHeight = 32f;
        var spacing = 20f;
        var totalWidth = buttonWidth * 2 + spacing;
        var startX = (avail.X - totalWidth) / 2;

        ImGui.SetCursorPosX(startX);

        using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.3f, 0.15f, 0.15f, 0.8f)))
        using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.5f, 0.2f, 0.2f, 0.9f)))
        using (ImRaii.PushColor(ImGuiCol.ButtonActive, new Vector4(0.6f, 0.25f, 0.25f, 1f)))
        {
            if (ImGui.Button("Decline", new Vector2(buttonWidth, buttonHeight)))
                IsOpen = false;
        }

        ImGui.SameLine(0, spacing);

        var accepted = _hasScrolledToBottom;
        var muted = new Vector4(0.2f, 0.2f, 0.2f, 0.5f);
        using (ImRaii.PushColor(ImGuiCol.Button, accepted ? new Vector4(0.2f, 0.4f, 0.2f, 0.9f) : muted)
            .Push(ImGuiCol.ButtonHovered, accepted ? new Vector4(0.25f, 0.5f, 0.25f, 1f) : muted)
            .Push(ImGuiCol.ButtonActive, accepted ? new Vector4(0.3f, 0.6f, 0.3f, 1f) : muted)
            .Push(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 0.6f), !accepted))
        {
            if (ImGui.Button("I Accept", new Vector2(buttonWidth, buttonHeight)) && accepted)
            {
                _accepted = true;
                IsOpen = false;
                OnAccepted?.Invoke();
            }
        }
        if (!accepted && ImGui.IsItemHovered())
            ImGui.SetTooltip("Please scroll to the bottom to enable this button");
    }

    void CenteredText(string text, Vector4 color)
    {
        var textWidth = ImGui.CalcTextSize(text).X;
        ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - textWidth) / 2 + ImGui.GetCursorPosX());
        ImGui.TextColored(color, text);
    }

    public override bool DrawConditions() => !_accepted;
}
