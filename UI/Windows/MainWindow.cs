namespace Glance.UI.Windows;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
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

    IDisposable? _themeScope;

    public override void PreDraw()
    {
        _themeScope = Theme.PushStyle();
        ImGui.SetNextWindowSize(new Vector2(700, 520) * ImGuiHelpers.GlobalScale, ImGuiCond.FirstUseEver);
    }

    public override void PostDraw() { _themeScope?.Dispose(); _themeScope = null; }

    public override void Draw()
    {
        Theme.DrawFrame(Theme.CornerAccentSizeLarge);
        if (!_init) { _init = true; _alpha = 0f; }
        _alpha = UI.Animate("main_fade", 1f, 4f);
        using var alpha = ImRaii.PushStyle(ImGuiStyleVar.Alpha, _alpha);

        if (Globals.Auth.IsValidating) DrawValidating();
        else if (!Globals.Auth.IsAuthenticated)
        {
            if (_charSub == 2 && _tab == 0)
                DrawTargetOnly();
            else
                DrawLogin();
        }
        else DrawLayout();
    }

    void DrawTargetOnly()
    {
        using (ImRaii.PushColor(ImGuiCol.Button, Theme.ButtonBg))
        using (ImRaii.PushColor(ImGuiCol.ButtonHovered, Theme.ButtonHovered))
        using (ImRaii.PushColor(ImGuiCol.Text, Theme.LabelColor))
        {
            if (ImGui.Button("< Back to Login", new Vector2(120, 24) * ImGuiHelpers.GlobalScale)) { _charSub = 0; Sound.PlayCancel(); }
        }

        UI.Space(4 * ImGuiHelpers.GlobalScale);
        UI.Divider();
        UI.Space(4 * ImGuiHelpers.GlobalScale);

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
        DrawSpinner(dl, spinPos, 10 * ImGuiHelpers.GlobalScale, 18 * ImGuiHelpers.GlobalScale, 2.5f);

        UI.Space(36 * ImGuiHelpers.GlobalScale);
        using (Globals.Fonts.Header.Push())
            CenteredText("Verifying account...", Theme.LabelColor);
    }

    void DrawLayout()
    {
        using (var sidebar = ImRaii.Child("Sidebar", new Vector2(180 * ImGuiHelpers.GlobalScale, -1), true))
        {
            if (sidebar)
            {
                Header();
                UI.Space(8 * ImGuiHelpers.GlobalScale); UI.Divider(); UI.Space(8 * ImGuiHelpers.GlobalScale);
                Tabs();
                var y = ImGui.GetContentRegionAvail().Y;
                if (y > 90 * ImGuiHelpers.GlobalScale) { ImGui.SetCursorPosY(ImGui.GetCursorPosY() + y - 27f * ImGuiHelpers.GlobalScale); Logout(); }
            }
        }
        ImGui.SameLine();
        using var content = ImRaii.Child("Content", new Vector2(-1, -1), true);
        if (content)
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
    }

    void DrawLoginRequiredPrompt()
    {
        var avail = ImGui.GetContentRegionAvail();
        ImGui.SetCursorPosY(avail.Y * 0.3f);

        using (ImRaii.PushFont(UiBuilder.IconFont))
            CenteredText(FontAwesomeIcon.SignOutAlt.ToIconString(), Theme.TextMuted);

        UI.Space(10 * ImGuiHelpers.GlobalScale);
        using (Globals.Fonts.Header.Push())
            CenteredText("No Character Detected", Theme.ValueColor);

        UI.Space(10 * ImGuiHelpers.GlobalScale);
        CenteredText("Please log in to a character to manage", Theme.TextMuted);
        CenteredText("your profiles and use Glance features.", Theme.TextMuted);
    }

    void DrawLinkPrompt()
    {
        var avail = ImGui.GetContentRegionAvail();
        var centerX = avail.X / 2;

        ImGui.SetCursorPosY(avail.Y * 0.25f);

        using (ImRaii.PushFont(UiBuilder.IconFont))
            CenteredText(FontAwesomeIcon.Link.ToIconString(), Theme.GoldColor);

        UI.Space(10 * ImGuiHelpers.GlobalScale);
        using (Globals.Fonts.Header.Push())
            CenteredText("New Character Detected", Theme.ValueColor);

        UI.Space(10 * ImGuiHelpers.GlobalScale);
        CenteredText("Link this character to your RPHub account to", Theme.TextMuted);
        CenteredText("enable profile management and Glance features.", Theme.TextMuted);

        UI.Space(6 * ImGuiHelpers.GlobalScale);
        CenteredText("Clicking below securely registers this character", Theme.GoldColor with { W = 0.8f });
        CenteredText("to your cloud profile.", Theme.GoldColor with { W = 0.8f });

        UI.Space(20 * ImGuiHelpers.GlobalScale);

        if (Globals.Auth.IsFetching)
        {
            _spinAngle += ImGui.GetIO().DeltaTime * 3f;
            var dl = ImGui.GetWindowDrawList();
            var spinPos = ImGui.GetCursorScreenPos() + new Vector2(centerX, 15 * ImGuiHelpers.GlobalScale);
            DrawSpinner(dl, spinPos, 8 * ImGuiHelpers.GlobalScale, 14 * ImGuiHelpers.GlobalScale, 2f);
            UI.Space(40 * ImGuiHelpers.GlobalScale);
            CenteredText("Linking character...", Theme.GoldColor);
        }
        else
        {
            ImGui.SetCursorPosX(centerX - 100 * ImGuiHelpers.GlobalScale);
            if (ImGui.Button("Link Current Character", new Vector2(200, 40) * ImGuiHelpers.GlobalScale))
            {
                _ = Globals.Auth.RefreshBeaconTokenAsync();
                Sound.PlayConfirm();
            }

            if (Globals.Auth.BeaconFetchFailed)
            {
                UI.Space(10 * ImGuiHelpers.GlobalScale);
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
                ImGui.Image(wrap.Handle, new Vector2(36, 36) * ImGuiHelpers.GlobalScale);
                ImGui.SameLine();
            }
        }
        catch { }

        ImGui.SetCursorScreenPos(new Vector2(ImGui.GetCursorScreenPos().X, startY));
        using (ImRaii.Group())
        {
            using (Globals.Fonts.Header.Push())
            {
                ImGui.TextColored(Theme.Primary, "Glance beta");
            }
            using (Globals.Fonts.Small.Push())
                ImGui.TextColored(Theme.TextMuted, "by RPHub.co");
        }
        ImGui.SetCursorScreenPos(new Vector2(startPos.X, startPos.Y + 30 * ImGuiHelpers.GlobalScale));
    }

    void Tabs()
    {
        Tab("Character", FontAwesomeIcon.User, 0);

        if (_tab == 0)
        {
            ImGui.Indent(16 * ImGuiHelpers.GlobalScale);
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
                gameTargetWorld = pc.HomeWorld.Value.Name.ToString();
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

            ImGui.Unindent(16 * ImGuiHelpers.GlobalScale);
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
        var h = 28f * ImGuiHelpers.GlobalScale;

        var hov = ImGui.IsMouseHoveringRect(p, p + new Vector2(w, h));
        if (sel || hov) dl.AddRectFilled(p, p + new Vector2(w, h), Theme.Col((sel ? Theme.ButtonBg : Theme.ButtonHovered) with { W = 0.4f }), 3f);

        if (ImGui.InvisibleButton($"##sub_{name}", new Vector2(w, h))) { _tab = 0; _charSub = idx; Sound.PlayTab(); }

        ImGui.SetCursorScreenPos(p + new Vector2(8 * ImGuiHelpers.GlobalScale, (h - ImGui.GetTextLineHeight()) / 2));
        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.TextColored((sel ? Theme.GoldColor : Theme.LabelColor) with { W = 0.7f }, icon.ToIconString());
        ImGui.SameLine();
        using (Globals.Fonts.Small.Push())
            ImGui.TextColored(sel ? Theme.GoldColor : Theme.LabelColor, name);

        if (hov) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        UI.Space(2 * ImGuiHelpers.GlobalScale);
    }

    static string Truncate(string s, int max) => s.Length > max ? s[..(max - 2)] + ".." : s;

    void Tab(string name, FontAwesomeIcon icon, int idx)
    {
        var sel = _tab == idx;
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        var w = ImGui.GetContentRegionAvail().X;
        var h = 38f * ImGuiHelpers.GlobalScale;

        var hov = ImGui.IsMouseHoveringRect(p, p + new Vector2(w, h));
        if (sel || hov) dl.AddRectFilled(p, p + new Vector2(w, h), Theme.Col((sel ? Theme.ButtonActive : Theme.ButtonHovered) with { W = 0.5f }), 4f);
        if (sel) dl.AddRectFilled(p, p + new Vector2(3 * ImGuiHelpers.GlobalScale, h), Theme.Col(Theme.GoldColor), 2f);

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

        ImGui.SetCursorScreenPos(p + new Vector2(12 * ImGuiHelpers.GlobalScale, (h - ImGui.GetTextLineHeight()) / 2));
        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.TextColored(sel ? Theme.GoldColor : Theme.LabelColor, icon.ToIconString());
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 6 * ImGuiHelpers.GlobalScale);
        ImGui.TextColored(sel ? Theme.GoldColor : Theme.ValueColor, name);

        if (hov) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        UI.Space(4 * ImGuiHelpers.GlobalScale);
    }

    void Logout()
    {
        using (ImRaii.PushColor(ImGuiCol.Button, Theme.ButtonBg with { W = 0.4f })
            .Push(ImGuiCol.ButtonHovered, Theme.Error with { W = 0.3f })
            .Push(ImGuiCol.ButtonActive, Theme.Error with { W = 0.5f })
            .Push(ImGuiCol.Text, Theme.LabelColor))
        {
            if (ImGui.Button("Log Out", new Vector2(ImGui.GetContentRegionAvail().X, 24 * ImGuiHelpers.GlobalScale))) Globals.Auth.Logout();
        }
    }

    void DrawLogin()
    {
        var avail = ImGui.GetContentRegionAvail();
        var centerX = avail.X / 2;
        var dl = ImGui.GetWindowDrawList();

        ImGui.SetCursorPosY(avail.Y * 0.12f);
        DrawLogo(centerX);
        UI.Space(20 * ImGuiHelpers.GlobalScale);

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
                ImGui.SetCursorPosX(centerX - 38 * ImGuiHelpers.GlobalScale);
                ImGui.Image(wrap.Handle, new Vector2(56, 56) * ImGuiHelpers.GlobalScale);
            }
        }
        catch { }

        UI.Space(16 * ImGuiHelpers.GlobalScale);
        using (Globals.Fonts.Large.Push())
            CenteredText("Glance", Theme.GoldColor);

        UI.Space(2 * ImGuiHelpers.GlobalScale);
        using (Globals.Fonts.Small.Push())
            CenteredText("by RPHub.co", Theme.TextMuted);
    }

    void DrawAuthProgress(float centerX, ImDrawListPtr dl)
    {
        var stepsX = centerX - 110 * ImGuiHelpers.GlobalScale;
        var authStep = GetAuthStep();

        DrawAuthStep(stepsX, "Requesting link...", 1, authStep);
        DrawAuthStep(stepsX, "Waiting for approval...", 2, authStep);
        DrawAuthStep(stepsX, "Syncing profiles...", 3, authStep);

        UI.Space(12 * ImGuiHelpers.GlobalScale);

        _spinAngle += ImGui.GetIO().DeltaTime * 2.5f;
        var spinPos = ImGui.GetCursorScreenPos() + new Vector2(centerX, 14 * ImGuiHelpers.GlobalScale);
        DrawSpinner(dl, spinPos, 6 * ImGuiHelpers.GlobalScale, 12 * ImGuiHelpers.GlobalScale, 2f);

        UI.Space(36 * ImGuiHelpers.GlobalScale);

        using (Globals.Fonts.Small.Push())
        {
            CenteredText("A browser window should have opened.", Theme.TextMuted);
            CenteredText("Complete sign-in there to continue.", Theme.TextMuted);
        }

        UI.Space(16 * ImGuiHelpers.GlobalScale);

        ImGui.SetCursorPosX(centerX - 70 * ImGuiHelpers.GlobalScale);
        using (ImRaii.PushColor(ImGuiCol.Button, Theme.ButtonBg with { W = 0.3f })
            .Push(ImGuiCol.ButtonHovered, Theme.Error with { W = 0.35f })
            .Push(ImGuiCol.ButtonActive, Theme.Error with { W = 0.5f })
            .Push(ImGuiCol.Text, Theme.TextMuted))
        {
            if (ImGui.Button("Cancel", new Vector2(140, 30) * ImGuiHelpers.GlobalScale)) Globals.Auth.CancelAuth();
        }
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

        UI.Space(8 * ImGuiHelpers.GlobalScale);
        ImGui.SetCursorPosX(centerX - 100 * ImGuiHelpers.GlobalScale);

        using (ImRaii.PushColor(ImGuiCol.Button, isLoggedIn ? Theme.PrimaryButtonBg : Theme.ButtonBg with { W = 0.2f })
            .Push(ImGuiCol.ButtonHovered, Theme.PrimaryButtonHover, isLoggedIn)
            .Push(ImGuiCol.ButtonActive, Theme.PrimaryButtonBg with { W = 0.9f }, isLoggedIn)
            .Push(ImGuiCol.Text, isLoggedIn ? Theme.PrimaryButtonText : Theme.TextMuted with { W = 0.5f }))
        using (ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 4f))
        {
            if (ImGui.Button("Connect RPHub Account", new Vector2(200, 36) * ImGuiHelpers.GlobalScale) && isLoggedIn) { Globals.Auth.StartAuthProcess(); Sound.PlayConfirm(); }
        }

        if (!isLoggedIn)
        {
            UI.Space(4 * ImGuiHelpers.GlobalScale);
            CenteredText("Please login on a character to sign in", Theme.Error with { W = 0.8f });
        }
    }

    void DrawFooter(Vector2 avail, ImDrawListPtr dl, float centerX)
    {
        var footerH = 50f * ImGuiHelpers.GlobalScale;
        ImGui.SetCursorPosY(avail.Y - footerH);

        var divStart = ImGui.GetCursorScreenPos();
        dl.AddLine(divStart + new Vector2(40 * ImGuiHelpers.GlobalScale, 0), divStart + new Vector2(avail.X - 40 * ImGuiHelpers.GlobalScale, 0), Theme.Col(Theme.FrameBorder with { W = 0.3f }));

        UI.Space(14 * ImGuiHelpers.GlobalScale);

        var iconGap = 24f * ImGuiHelpers.GlobalScale;
        var iconsW = 24f * ImGuiHelpers.GlobalScale * 3 + iconGap * 2;
        var iconsX = centerX - iconsW / 2;

        ImGui.SetCursorPosX(iconsX);
        using (ImRaii.Group())
        {
            SocialIcon(FontAwesomeIcon.Heart, "https://discord.gg/rbSWGK9gvT", "Discord");
            ImGui.SameLine(0, iconGap);
            SocialIcon(FontAwesomeIcon.Globe, "https://rphub.co", "Website");
            ImGui.SameLine(0, iconGap);
            SocialIcon(FontAwesomeIcon.QuestionCircle, "https://rphub.co/plugin", "Help");
        }
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

        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var iconStr = done ? FontAwesomeIcon.Check.ToIconString() : FontAwesomeIcon.Circle.ToIconString();
            ImGui.TextColored(col, iconStr);
        }

        ImGui.SameLine();
        using (Globals.Fonts.Small.Push())
            ImGui.TextColored(active ? Theme.ValueColor : (done ? Theme.Success : Theme.TextMuted with { W = 0.5f }), text);

        UI.Space(4 * ImGuiHelpers.GlobalScale);
    }

    void SocialIcon(FontAwesomeIcon icon, string url, string tooltip)
    {
        var p = ImGui.GetCursorScreenPos();
        var size = new Vector2(24, 24) * ImGuiHelpers.GlobalScale;
        var hov = ImGui.IsMouseHoveringRect(p, p + size);

        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.TextColored(hov ? Theme.GoldColor : Theme.TextMuted, icon.ToIconString());

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
