namespace Glance.UI.Windows;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using Glance.Utils;
using Glance.Core;
using Glance.UI.Components;
using Glance.UI.Tabs;
using System;
using System.Numerics;

public sealed class MainWindow() : Window("Glance##Dashboard", ImGuiWindowFlags.NoCollapse)
{
    int _tab;
    int _charSub;
    float _alpha;
    bool _init;
    float _spinAngle;
    int _pendingProfileTab = 0;
    string? _viewedTargetName;
    string? _viewedTargetWorld;
    string? _lastShownTargetName;
    string? _lastShownTargetWorld;

    public void ShowProfile()
    {
        _tab = 0;
        _charSub = 1;
        IsOpen = true;
        BringToFront();
    }

    public void UpdateViewedTarget(string name, string world)
    {
        _viewedTargetName = name;
        _viewedTargetWorld = world;
    }

    public void ClearTarget()
    {
        if (_charSub != 2) return;
        var activeId = Globals.Profiles.ActiveProfileId;
        var activeProfile = activeId != null
            ? Array.Find(Globals.Profiles.Data?.Characters ?? [], c => c.Id == activeId)
            : null;
        _charSub = activeProfile != null ? 1 : 0;
        _viewedTargetName = null;
        _viewedTargetWorld = null;
        _lastShownTargetName = null;
        _lastShownTargetWorld = null;
    }

    public void ShowTarget(string name, string world, int profileTab = 0)
    {
        var activeId = Globals.Profiles.ActiveProfileId;
        var activeProfile = activeId != null
            ? Array.Find(Globals.Profiles.Data?.Characters ?? [], c => c.Id == activeId)
            : null;

        if (activeProfile != null && activeProfile.Name == name && activeProfile.World == world)
        {
            _tab = 0;
            _charSub = 1;
            IsOpen = true;
            BringToFront();
            return;
        }

        _viewedTargetName = name;
        _viewedTargetWorld = world;
        _lastShownTargetName = null;  
        _lastShownTargetWorld = null;

        _tab = 0;
        _charSub = 2;
        _pendingProfileTab = profileTab;
        IsOpen = true;
        BringToFront();
        ProfileTab.ShowTarget(name, world);
    }

    public void SetTab(int tab) { _tab = tab; _charSub = 0; }

    public override void PreDraw()
    {
        Theme.PushStyle();
        ImGui.SetNextWindowSize(new Vector2(700, 520), ImGuiCond.FirstUseEver);
    }

    public override void PostDraw() => Theme.PopStyle();

    public override void Draw()
    {
        Theme.DrawFrame(Theme.CornerAccentSizeLarge);
        if (!_init) { _init = true; _alpha = 0f; }
        _alpha = UI.Animate("main_fade", 1f, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, _alpha);

        if (Globals.Auth.IsValidating) DrawValidating();
        else if (!Globals.Auth.IsAuthenticated)
        {
            if (_charSub == 2 && _tab == 0)
                DrawTargetOnly();
            else
                DrawLogin();
        }
        else DrawLayout();

        ImGui.PopStyleVar();
    }

    void DrawTargetOnly()
    {
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.ButtonBg);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.ButtonHovered);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.LabelColor);
        if (ImGui.Button("< Back to Login", new Vector2(120, 24))) { _charSub = 0; Sound.PlayCancel(); }
        ImGui.PopStyleColor(3);

        UI.Space(4);
        UI.Divider();
        UI.Space(4);

        ProfileTab.DrawTarget();
    }

    void DrawValidating()
    {
        var avail = ImGui.GetContentRegionAvail();
        var centerX = avail.X / 2;

        ImGui.SetCursorPosY(avail.Y * 0.4f);

        _spinAngle += ImGui.GetIO().DeltaTime * 3f;
        var dl = ImGui.GetWindowDrawList();
        var spinPos = ImGui.GetCursorScreenPos() + new Vector2(centerX, 0);
        DrawSpinner(dl, spinPos, 10, 18, 2.5f);

        UI.Space(36);
        ImGui.SetWindowFontScale(1.1f);
        CenteredText("Verifying account...", Theme.LabelColor);
        ImGui.SetWindowFontScale(1f);
    }

    void DrawLayout()
    {
        if (ImGui.BeginChild("Sidebar", new Vector2(180, -1), true))
        {
            Header();
            UI.Space(8); UI.Divider(); UI.Space(8);
            Tabs();
            var y = ImGui.GetContentRegionAvail().Y;
            if (y > 90) { ImGui.SetCursorPosY(ImGui.GetCursorPosY() + y - 27f); Logout(); }
        }
        ImGui.EndChild();
        ImGui.SameLine();
        if (ImGui.BeginChild("Content", new Vector2(-1, -1), true))
        {
            if (Globals.Objects.LocalPlayer == null)
            {
                DrawLoginRequiredPrompt();
            }
            else if (!Globals.Auth.IsReady && _tab == 0)
            {
                DrawLinkPrompt();
            }
            else
            {
                switch (_tab)
                {
                    case 0:
                        if (_charSub == 2)
                        {
                            if (_pendingProfileTab != 0)
                            {
                                ProfileTab.SetTab(_pendingProfileTab);
                                _pendingProfileTab = 0;
                            }
                            ProfileTab.DrawTarget();
                        }
                        else if (_charSub == 1) ProfileTab.Draw();
                        else CharacterTab.Draw();
                        break;
                    case 1: NearbyTab.Draw(); break;
                    case 2: CachedProfilesTab.Draw(); break;  
                    case 3: SettingsTab.Draw(); break;
                    case 4: AboutTab.Draw(); break;
                }
            }
        }
        ImGui.EndChild();
    }

    void DrawLoginRequiredPrompt()
    {
        var avail = ImGui.GetContentRegionAvail();
        ImGui.SetCursorPosY(avail.Y * 0.3f);

        ImGui.PushFont(UiBuilder.IconFont);
        CenteredText(FontAwesomeIcon.SignOutAlt.ToIconString(), Theme.TextMuted);
        ImGui.PopFont();

        UI.Space(10);
        ImGui.SetWindowFontScale(1.2f);
        CenteredText("No Character Detected", Theme.ValueColor);
        ImGui.SetWindowFontScale(1.0f);

        UI.Space(10);
        CenteredText("Please log in to a character to manage", Theme.TextMuted);
        CenteredText("your profiles and use Glance features.", Theme.TextMuted);
    }

    void DrawLinkPrompt()
    {
        var avail = ImGui.GetContentRegionAvail();
        var centerX = avail.X / 2;

        ImGui.SetCursorPosY(avail.Y * 0.25f);

        ImGui.PushFont(UiBuilder.IconFont);
        CenteredText(FontAwesomeIcon.Link.ToIconString(), Theme.GoldColor);
        ImGui.PopFont();

        UI.Space(10);
        ImGui.SetWindowFontScale(1.2f);
        CenteredText("New Character Detected", Theme.ValueColor);
        ImGui.SetWindowFontScale(1.0f);

        UI.Space(10);
        CenteredText("Link this character to your RPHub account to", Theme.TextMuted);
        CenteredText("enable profile management and Glance features.", Theme.TextMuted);

        UI.Space(6);
        CenteredText("Clicking below securely registers this character", Theme.GoldColor with { W = 0.8f });
        CenteredText("to your cloud profile.", Theme.GoldColor with { W = 0.8f });

        UI.Space(20);

        if (Globals.Auth.IsFetching)
        {
            _spinAngle += ImGui.GetIO().DeltaTime * 3f;
            var dl = ImGui.GetWindowDrawList();
            var spinPos = ImGui.GetCursorScreenPos() + new Vector2(centerX, 15);
            DrawSpinner(dl, spinPos, 8, 14, 2f);
            UI.Space(40);
            CenteredText("Linking character...", Theme.GoldColor);
        }
        else
        {
            ImGui.SetCursorPosX(centerX - 100);
            if (ImGui.Button("Link Current Character", new Vector2(200, 40)))
            {
                _ = Globals.Auth.RefreshBeaconTokenAsync();
                Sound.PlayConfirm();
            }

            if (Globals.Auth.BeaconFetchFailed)
            {
                UI.Space(10);
                CenteredText(Globals.Auth.LastBeaconError ?? "Failed to link character.", Theme.Error);
            }
        }
    }

    void Header()
    {
        var startPos = ImGui.GetCursorScreenPos();
        var startY = ImGui.GetCursorScreenPos().Y;
        try
        {
            var icon = Globals.TextureProvider.GetFromGameIcon(new GameIconLookup(65120));
            if (icon.TryGetWrap(out var wrap, out _))
            {
                ImGui.Image(wrap.Handle, new Vector2(36, 36));
                ImGui.SameLine();
            }
        }
        catch { }

        ImGui.SetCursorScreenPos(new Vector2(ImGui.GetCursorScreenPos().X, startY));
        ImGui.BeginGroup();
        ImGui.SetWindowFontScale(1.1f);
        using (Globals.Fonts.Header.Push())
        {
            ImGui.TextColored(Theme.Primary, "Glance beta");
        }
        ImGui.SetWindowFontScale(Theme.SmallFont);
        ImGui.TextColored(Theme.TextMuted, "by RPHub.co");
        ImGui.SetWindowFontScale(1f);
        ImGui.EndGroup();
        ImGui.SetCursorScreenPos(new Vector2(startPos.X, startPos.Y + 30));
    }

    void Tabs()
    {
        Tab("Character", FontAwesomeIcon.User, 0);

        if (_tab == 0)
        {
            ImGui.Indent(16);
            SubTab("Profiles", FontAwesomeIcon.Users, 0);

            var activeId = Globals.Profiles.ActiveProfileId;
            var activeProfile = activeId != null
                ? Array.Find(Globals.Profiles.Data?.Characters ?? [], c => c.Id == activeId)
                : null;

            if (activeProfile != null)
                SubTab("My Profile", FontAwesomeIcon.IdCard, 1);

            var target = Globals.Objects.LocalPlayer?.TargetObject;
            string? gameTargetName = null, gameTargetWorld = null;
            if (target is Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter pc)
            {
                gameTargetName = pc.Name.TextValue;
                gameTargetWorld = pc.HomeWorld.Value.Name.ExtractText();
            }

            var displayName = _charSub == 2 && _viewedTargetName != null
                ? _viewedTargetName
                : gameTargetName;
            var displayWorld = _charSub == 2 && _viewedTargetWorld != null
                ? _viewedTargetWorld
                : gameTargetWorld;

            var isTargetingSelf = activeProfile != null
                && displayName == activeProfile.Name
                && displayWorld == activeProfile.World;

            if (displayName != null && !isTargetingSelf)
            {
                SubTab(Truncate(displayName, 12), FontAwesomeIcon.Crosshairs, 2);
                if (_charSub == 2 && displayName != null && displayWorld != null
                    && (displayName != _lastShownTargetName || displayWorld != _lastShownTargetWorld))
                {
                    _lastShownTargetName = displayName;
                    _lastShownTargetWorld = displayWorld;
                    ProfileTab.ShowTarget(displayName, displayWorld);
                }
            }
            else if (_charSub == 2)
            {
                _charSub = activeProfile != null ? 1 : 0;
                _viewedTargetName = null;
                _viewedTargetWorld = null;
            }

            ImGui.Unindent(16);
        }

        Tab("Nearby", FontAwesomeIcon.Users, 1);
        Tab("Cached", FontAwesomeIcon.Database, 2);     
        Tab("Settings", FontAwesomeIcon.Cog, 3);    
        Tab("About", FontAwesomeIcon.InfoCircle, 4);   
    }

    void SubTab(string name, FontAwesomeIcon icon, int idx)
    {
        var sel = _tab == 0 && _charSub == idx;
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        var w = ImGui.GetContentRegionAvail().X;
        const float h = 28f;

        var hov = ImGui.IsMouseHoveringRect(p, p + new Vector2(w, h));
        if (sel || hov) dl.AddRectFilled(p, p + new Vector2(w, h), Theme.Col((sel ? Theme.ButtonBg : Theme.ButtonHovered) with { W = 0.4f }), 3f);

        if (ImGui.InvisibleButton($"##sub_{name}", new Vector2(w, h))) { _tab = 0; _charSub = idx; Sound.PlayTab(); }

        ImGui.SetCursorScreenPos(p + new Vector2(8, (h - ImGui.GetTextLineHeight()) / 2));
        ImGui.SetWindowFontScale(Theme.SmallFont);
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored((sel ? Theme.GoldColor : Theme.LabelColor) with { W = 0.7f }, icon.ToIconString());
        ImGui.PopFont();
        ImGui.SameLine();
        ImGui.TextColored(sel ? Theme.GoldColor : Theme.LabelColor, name);
        ImGui.SetWindowFontScale(1f);

        if (hov) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        UI.Space(2);
    }

    static string Truncate(string s, int max) => s.Length > max ? s[..(max - 2)] + ".." : s;

    void Tab(string name, FontAwesomeIcon icon, int idx)
    {
        var sel = _tab == idx;
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        var w = ImGui.GetContentRegionAvail().X;
        const float h = 38f;

        var hov = ImGui.IsMouseHoveringRect(p, p + new Vector2(w, h));
        if (sel || hov) dl.AddRectFilled(p, p + new Vector2(w, h), Theme.Col((sel ? Theme.ButtonActive : Theme.ButtonHovered) with { W = 0.5f }), 4f);
        if (sel) dl.AddRectFilled(p, p + new Vector2(3, h), Theme.Col(Theme.GoldColor), 2f);

        if (ImGui.InvisibleButton($"##t_{name}", new Vector2(w, h)))
        {
            _tab = idx;
            if (idx != 0)
            {
                _viewedTargetName = null;
                _viewedTargetWorld = null;
            }
            Sound.PlayTab();
        }

        ImGui.SetCursorScreenPos(p + new Vector2(12, (h - ImGui.GetTextLineHeight()) / 2));
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(sel ? Theme.GoldColor : Theme.LabelColor, icon.ToIconString());
        ImGui.PopFont();
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 6);
        ImGui.TextColored(sel ? Theme.GoldColor : Theme.ValueColor, name);

        if (hov) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        UI.Space(4);
    }

    void Logout()
    {
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.ButtonBg with { W = 0.4f });
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.Error with { W = 0.3f });
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.Error with { W = 0.5f });
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.LabelColor);
        if (ImGui.Button("Log Out", new Vector2(ImGui.GetContentRegionAvail().X, 24))) Globals.Auth.Logout();
        ImGui.PopStyleColor(4);
    }

    void DrawLogin()
    {
        var avail = ImGui.GetContentRegionAvail();
        var centerX = avail.X / 2;
        var dl = ImGui.GetWindowDrawList();

        ImGui.SetCursorPosY(avail.Y * 0.12f);
        DrawLogo(centerX);
        UI.Space(20);

        if (Globals.Auth.IsWaitingForBrowser)
            DrawAuthProgress(centerX, dl);
        else
            DrawConnectButton(centerX);

        DrawFooter(avail, dl, centerX);
    }

    void DrawLogo(float centerX)
    {
        try
        {
            var icon = Globals.TextureProvider.GetFromGameIcon(new GameIconLookup(65120));
            if (icon.TryGetWrap(out var wrap, out _))
            {
                ImGui.SetCursorPosX(centerX - 38);
                ImGui.Image(wrap.Handle, new Vector2(56, 56));
            }
        }
        catch { }

        UI.Space(16);
        ImGui.SetWindowFontScale(1.4f);
        using (Globals.Fonts.Header.Push())
        {
            CenteredText("Glance", Theme.GoldColor);
        }
        ImGui.SetWindowFontScale(1f);

        UI.Space(2);
        ImGui.SetWindowFontScale(0.9f);
        CenteredText("by RPHub.co", Theme.TextMuted);
        ImGui.SetWindowFontScale(1f);
    }

    void DrawAuthProgress(float centerX, ImDrawListPtr dl)
    {
        var stepsX = centerX - 110;
        var authStep = GetAuthStep();

        DrawAuthStep(stepsX, "Requesting link...", 1, authStep);
        DrawAuthStep(stepsX, "Waiting for approval...", 2, authStep);
        DrawAuthStep(stepsX, "Syncing profiles...", 3, authStep);

        UI.Space(12);

        _spinAngle += ImGui.GetIO().DeltaTime * 2.5f;
        var spinPos = ImGui.GetCursorScreenPos() + new Vector2(centerX, 14);
        DrawSpinner(dl, spinPos, 6, 12, 2f);

        UI.Space(36);

        ImGui.SetWindowFontScale(0.85f);
        CenteredText("A browser window should have opened.", Theme.TextMuted);
        CenteredText("Complete sign-in there to continue.", Theme.TextMuted);
        ImGui.SetWindowFontScale(1f);

        UI.Space(16);

        ImGui.SetCursorPosX(centerX - 70);
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.ButtonBg with { W = 0.3f });
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.Error with { W = 0.35f });
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.Error with { W = 0.5f });
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextMuted);
        if (ImGui.Button("Cancel", new Vector2(140, 30))) Globals.Auth.CancelAuth();
        ImGui.PopStyleColor(4);
    }

    int _authStep;
    float _stepTimer;

    int GetAuthStep()
    {
        if (Globals.Auth.IsWaitingForBrowser)
        {
            _stepTimer += ImGui.GetIO().DeltaTime;
            if (_authStep == 0) { _authStep = 1; _stepTimer = 0; }
            else if (_authStep == 1 && _stepTimer > 0.8f) { _authStep = 2; _stepTimer = 0; }
        }
        else { _authStep = 0; _stepTimer = 0; }
        return _authStep;
    }

    void DrawConnectButton(float centerX)
    {
        var isLoggedIn = Globals.Objects.LocalPlayer != null;

        UI.Space(8);
        ImGui.SetCursorPosX(centerX - 100);

        if (isLoggedIn)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.PrimaryButtonBg);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.PrimaryButtonHover);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.PrimaryButtonBg with { W = 0.9f });
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.PrimaryButtonText);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);

            if (ImGui.Button("Connect RPHub Account", new Vector2(200, 36))) { Globals.Auth.StartAuthProcess(); Sound.PlayConfirm(); }

            ImGui.PopStyleVar();
            ImGui.PopStyleColor(4);
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.ButtonBg with { W = 0.2f });
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextMuted with { W = 0.5f });
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);

            ImGui.Button("Connect RPHub Account", new Vector2(200, 36));

            ImGui.PopStyleVar();
            ImGui.PopStyleColor(2);

            UI.Space(4);
            CenteredText("Please login on a character to sign in", Theme.Error with { W = 0.8f });
        }
    }

    void DrawFooter(Vector2 avail, ImDrawListPtr dl, float centerX)
    {
        const float footerH = 50f;
        ImGui.SetCursorPosY(avail.Y - footerH);

        var divStart = ImGui.GetCursorScreenPos();
        dl.AddLine(divStart + new Vector2(40, 0), divStart + new Vector2(avail.X - 40, 0), Theme.Col(Theme.FrameBorder with { W = 0.3f }));

        UI.Space(14);

        const float iconGap = 24f;
        var iconsW = 24f * 3 + iconGap * 2;
        var iconsX = centerX - iconsW / 2;

        ImGui.SetCursorPosX(iconsX);
        ImGui.BeginGroup();
        SocialIcon(FontAwesomeIcon.Heart, "https://discord.gg/rbSWGK9gvT", "Discord");
        ImGui.SameLine(0, iconGap);
        SocialIcon(FontAwesomeIcon.Globe, "https://rphub.co", "Website");
        ImGui.SameLine(0, iconGap);
        SocialIcon(FontAwesomeIcon.QuestionCircle, "https://rphub.co/plugin", "Help");
        ImGui.EndGroup();
    }

    void DrawSpinner(ImDrawListPtr dl, Vector2 pos, float innerRadius, float outerRadius, float thickness)
    {
        for (var i = 0; i < 8; i++)
        {
            var a = _spinAngle + i * (MathF.PI / 4);
            var alpha = (i + 1) / 8f;
            var p1 = pos + new Vector2(MathF.Cos(a), MathF.Sin(a)) * innerRadius;
            var p2 = pos + new Vector2(MathF.Cos(a), MathF.Sin(a)) * outerRadius;
            dl.AddLine(p1, p2, Theme.Col(Theme.GoldColor with { W = alpha }), thickness);
        }
    }

    void DrawAuthStep(float x, string text, int step, int current)
    {
        ImGui.SetCursorPosX(x);

        var done = current > step;
        var active = current == step;
        var col = done ? Theme.Success : (active ? Theme.GoldColor : Theme.TextMuted with { W = 0.4f });

        ImGui.PushFont(UiBuilder.IconFont);
        var iconStr = done ? FontAwesomeIcon.Check.ToIconString() : FontAwesomeIcon.Circle.ToIconString();
        ImGui.TextColored(col, iconStr);
        ImGui.PopFont();

        ImGui.SameLine();
        ImGui.SetWindowFontScale(0.9f);
        ImGui.TextColored(active ? Theme.ValueColor : (done ? Theme.Success : Theme.TextMuted with { W = 0.5f }), text);
        ImGui.SetWindowFontScale(1f);

        UI.Space(4);
    }

    void SocialIcon(FontAwesomeIcon icon, string url, string tooltip)
    {
        var p = ImGui.GetCursorScreenPos();
        var size = new Vector2(24, 24);
        var hov = ImGui.IsMouseHoveringRect(p, p + size);

        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(hov ? Theme.GoldColor : Theme.TextMuted, icon.ToIconString());
        ImGui.PopFont();

        if (hov)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            ImGui.SetTooltip(tooltip);
        }

        ImGui.SetCursorScreenPos(p);
        if (ImGui.InvisibleButton($"##social_{icon}", size)) { Dalamud.Utility.Util.OpenLink(url); Sound.PlayOpen(); }
    }

    void CenteredText(string t, Vector4 c)
    {
        var w = ImGui.GetWindowContentRegionMax().X;
        var tw = ImGui.CalcTextSize(t).X;
        var posX = ((w - tw) / 2) + (-10f);
        ImGui.SetCursorPosX(posX);
        ImGui.TextColored(c, t);
    }
}
