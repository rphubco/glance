namespace Glance.UI.Windows;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Glance.Core;
using Glance.Models;
using Glance.UI.Tabs;
using Glance.Utils;
using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

public sealed class ProfileTooltipWindow : Window
{
    ProfileData? _data;
    string? _name, _world, _profileId;
    long _version;
    bool _loading, _refreshing, _notFound, _showGlance, _fetchStarted, _updateMain;
    Guid _requestId;
    float _windowWidth = MinWidth;
    public string? CurrentTargetName => _name;
    public string? CurrentTargetWorld => _world;
    public string? UnverifiedTarget { get; private set; }

    const float MinWidth = 320f, MaxWidth = 420f, NamePadding = 120f;
    const float ImgSize = 90f, ImgHeight = 112f; // 4:5 
    const float CornerMargin = 30f, BottomOffset = 70f;

    const ImGuiWindowFlags TooltipFlags =
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
        ImGuiWindowFlags.NoScrollbar | 
        ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.AlwaysAutoResize |
        ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking;

    public ProfileTooltipWindow() : base("##RPHubTooltip", TooltipFlags) { IsOpen = false; RespectCloseHotkey = false; }

    public void UpdateData(CachedProfile? profile)
    {
        if (profile?.Data == null || profile.Version <= _version) return;
        _data = profile.Data;
        _profileId = profile.Id.ToString();
        _version = profile.Version;
        _loading = false;
        _notFound = false;
    }

    public void ShowGlance()
    {
        _showGlance = true;
        if (_name != null && _world != null && !_loading)
            Globals.GlanceWindow.Show(_name, _world, _data, _profileId);
    }

    public void OnBecameHardTarget()
    {
        if (_name == null || _world == null) return;
        var lp = Globals.Objects.LocalPlayer;
        if (lp != null && _name == lp.Name.TextValue && _world == lp.HomeWorld.Value.Name.ExtractText()) return;

        _updateMain = true;
        Globals.MainWindow.UpdateViewedTarget(_name, _world);
        if (_data != null)
            ProfileTab.SetTargetData(_name, _world, _data, _profileId);
        else if (_notFound)
            ProfileTab.SetTargetNotFound(_name, _world);
        else
            ProfileTab.SetTargetLoading(_name, _world);
    }

    public void EnsureLoaded()
    {
        if (_fetchStarted || !_loading || _name == null || _world == null) return;
        _fetchStarted = true;
        _ = Load(_name, _world, _requestId);
    }

    public void Show(string name, string world, bool showGlance = false, bool startFetch = true, bool updateMain = false)
    {
        _showGlance = showGlance;
        var cached = Globals.Cache.GetProfile(name, world);
        var stale = Globals.Cache.IsStaleNeighbor(name, world);
        var same = _name == name && _world == world;
        var hasData = _data != null || _notFound;
        var newer = cached != null && cached.Version > _version;
        if (same && _notFound && !newer)
        {
            if (cached?.Unverified == true) { UnverifiedTarget = name; return; }
            if (!Globals.Config.HideEmptyProfiles) { IsOpen = true; if (showGlance) Globals.GlanceWindow.Show(name, world, _data, _profileId); }
            if (updateMain) OnBecameHardTarget();
            return;
        }

        if (same && hasData && !stale && !newer)
        {
            IsOpen = true;
            if (showGlance) Globals.GlanceWindow.Show(name, world, _data, _profileId);
            if (updateMain) OnBecameHardTarget();
            return;
        }

        var thisRequest = Guid.NewGuid();
        _requestId = thisRequest;

        UnverifiedTarget = null;
        _updateMain = updateMain;

        if (cached?.Data != null)
        {
            (_name, _world, _data, _profileId, _version, _notFound, _loading) = (name, world, cached.Data, cached.Id.ToString(), cached.Version, false, false);
            IsOpen = true;
            if (showGlance) Globals.GlanceWindow.Show(name, world, _data, _profileId);
        }
        else
        {
            (_name, _world, _data, _profileId, _version, _notFound, _loading) = (name, world, null, null, 0, false, true);
            if (!Globals.Config.HideEmptyProfiles)
            {
                IsOpen = true;
                if (showGlance) Globals.GlanceWindow.Show(name, world, null, null);
            }
        }

        var lp = Globals.Objects.LocalPlayer;
        var isSelf = lp != null && name == lp.Name.TextValue && world == lp.HomeWorld.Value.Name.ExtractText();
        if (!isSelf && updateMain)
        {
            Globals.MainWindow.UpdateViewedTarget(name, world);
            ProfileTab.SetTargetLoading(name, world);
        }

        _windowWidth = Math.Clamp(ImGui.CalcTextSize(name).X + ImgSize + NamePadding, MinWidth, MaxWidth);

        _fetchStarted = startFetch || cached != null;
        if (_fetchStarted)
            _ = Load(name, world, thisRequest);
    }

    public void Hide() { IsOpen = false; UnverifiedTarget = null; Globals.GlanceWindow.Hide(); }
    async Task Load(string name, string world, Guid requestId)
    {
        try
        {
            var cached = await Globals.Cache.FetchProfileAsync(name, world);
            if (requestId != _requestId) return;

            if (cached?.Data != null && _name == name && _world == world)
            {
                _data = cached.Data;
                _profileId = cached.Id.ToString();
                _version = cached.Version;
                _notFound = false;
                IsOpen = true;
                if (_showGlance) Globals.GlanceWindow.Show(name, world, _data, _profileId);
            }
            else
            {
                _data = null;
                _profileId = null;
                _version = 0;
                _notFound = true;
                if (cached?.Unverified == true)
                    UnverifiedTarget = name;
            }
        }
        catch { if (requestId == _requestId) _notFound = true; }
        finally
        {
            if (requestId == _requestId)
            {
                _loading = false;

                if (_notFound && Globals.Config.HideEmptyProfiles)
                {
                    IsOpen = false;
                    Globals.GlanceWindow.Hide();
                }

                var lp = await Globals.Framework.RunOnFrameworkThread(() => Globals.Objects.LocalPlayer);
                var isSelf = lp != null && name == lp.Name.TextValue && world == lp.HomeWorld.Value.Name.ExtractText();
                if (!isSelf && _updateMain)
                {
                    if (_data != null)
                        ProfileTab.SetTargetData(name, world, _data, _profileId);
                    else
                        ProfileTab.SetTargetNotFound(name, world);
                }
            }
        }
    }

    async Task Refresh()
    {
        if (_name == null || _world == null || _refreshing) return;
        _refreshing = true;
        var thisRequest = _requestId;

        try
        {
            var r = await Globals.Cache.RefreshProfileAsync(_name, _world);
            if (thisRequest != _requestId) return;
            if (r?.Data == null) return;
            _data = r.Data;
            _profileId = r.Id.ToString();
            _version = r.Version;
            _notFound = false;
            Globals.ProfileView.UpdateData(_name, _world, r);
        }
        catch { }
        finally { if (thisRequest == _requestId) _refreshing = false; }
    }

    public Task RefreshCurrentTargetAsync() => _name != null && _world != null && IsOpen ? Refresh() : Task.CompletedTask;

    public override void PreDraw()
    {
        Theme.PushStyle();
        ImGui.SetNextWindowSizeConstraints(new Vector2(_windowWidth, 0), new Vector2(_windowWidth, 600));

        var vp = ImGui.GetMainViewport();
        var estHeight = _data != null ? 320f : 160f;
        var pos = new Vector2(
            vp.WorkPos.X + vp.WorkSize.X - _windowWidth - CornerMargin,
            vp.WorkPos.Y + vp.WorkSize.Y - estHeight - CornerMargin - BottomOffset);

        ImGui.SetNextWindowPos(pos, ImGuiCond.FirstUseEver);
    }

    public override void PostDraw() => Theme.PopStyle();

    public override void Draw()
    {
        Theme.DrawFrame(Theme.CornerAccentSizeLarge);

        if (_loading) { DrawHeader(); DrawCentered("Loading...", Theme.LabelColor); DrawVersion(); return; }

        var cached = _name != null && _world != null ? Globals.Cache.GetProfile(_name, _world) : null;
        if (cached?.Unverified == true) { DrawUnverified(); return; }

        if (_notFound || _data == null) { DrawNoProfile(); return; }

        DrawProfile();
    }

    void DrawUnverified()
    {
        ImGui.BeginGroup();
        DrawPlaceholder();
        ImGui.SameLine();
        ImGui.BeginGroup();
        using (Globals.Fonts.Header.Push()) ImGui.TextColored(Theme.NameColor, _name ?? "Unknown");
        ImGui.TextColored(Theme.WorldColor, $"@ {_world ?? "Unknown"}");
        var race = Globals.GetTargetRace();
        var clan = Globals.GetTargetClan();
        if (!string.IsNullOrEmpty(race)) ImGui.TextColored(Theme.LabelColor, $"{race} · {clan}");
        ImGui.EndGroup();
        ImGui.EndGroup();

        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();

        DrawCentered("Unverified RPHub User", Theme.GoldColor);
        UI.Space(4);
        DrawCentered("Profile hidden until verified", Theme.TextMuted);

        DrawVersion();
    }

    void DrawProfile()
    {
        var width = ImGui.GetContentRegionAvail().X;

        ImGui.BeginGroup();
        DrawImage();
        ImGui.SameLine();

        ImGui.BeginGroup();
        {
            var name = _data!.Name ?? _name ?? "Unknown";
            var world = $"@ {_world}";
            var available = width - ImgSize - Theme.Padding * 2;

            using (Globals.Fonts.Header.Push()) ImGui.TextColored(Theme.NameColor, name);
            ImGui.TextColored(Theme.WorldColor, world);

            if (!string.IsNullOrEmpty(_data.Description))
            {
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + available);
                ImGui.TextColored(Theme.ValueColor, _data.Description);
                ImGui.PopTextWrapPos();
            }

            var hasCustomRace = !string.IsNullOrEmpty(_data.Race);
            var race = hasCustomRace ? _data.Race : Globals.GetTargetRace();
            var clan = !string.IsNullOrEmpty(_data.Clan) ? _data.Clan : (hasCustomRace ? null : Globals.GetTargetClan());
            if (!string.IsNullOrEmpty(race))
                ImGui.TextColored(Theme.LabelColor, !string.IsNullOrEmpty(clan) ? $"{race} · {clan}" : race!);

            if (!string.IsNullOrEmpty(_data.FreeCompany))
                ImGui.TextColored(Theme.LabelColorDim, $"<{_data.FreeCompany}>");
        }
        ImGui.EndGroup();
        ImGui.EndGroup();

        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();

        ImGui.SetWindowFontScale(0.95f);

        // Pronouns
        //if (!string.IsNullOrEmpty(_data.Pronouns))
        //{
        //    ImGui.TextColored(Theme.LabelColor, $"Pronouns: {_data.Pronouns}");
        //    ImGui.Spacing();
        //}

        if (!string.IsNullOrEmpty(_data.CurrentStatus))
        {
            DrawInfoBox("Currently (IC)", _data.CurrentStatus, new Vector4(0.4f, 0.8f, 0.4f, 1f), new Vector4(0.15f, 0.22f, 0.15f, 0.9f));
            ImGui.Spacing();
        }

        if (!string.IsNullOrEmpty(_data.PlayerNotes))
        {
            DrawInfoBox("Player's Notes (OOC)", _data.PlayerNotes, new Vector4(0.7f, 0.7f, 0.9f, 1f), new Vector4(0.18f, 0.18f, 0.25f, 0.9f));
            ImGui.Spacing();
        }

        DrawFooterHooks(_data);

        ImGui.SetWindowFontScale(1f);
        DrawVersion();
    }


    void DrawNoProfile()
    {
        ImGui.BeginGroup();
        DrawPlaceholder();
        ImGui.SameLine();
        ImGui.BeginGroup();
        using (Globals.Fonts.Header.Push()) ImGui.TextColored(Theme.NameColor, _name ?? "Unknown");
        ImGui.TextColored(Theme.WorldColor, $"@ {_world ?? "Unknown"}");
        var race = Globals.GetTargetRace();
        var clan = Globals.GetTargetClan();
        if (!string.IsNullOrEmpty(race)) ImGui.TextColored(Theme.LabelColor, $"{race} · {clan}");
        ImGui.EndGroup();
        ImGui.EndGroup();

        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawCentered("No RPHub profile", Theme.LabelColorDim);
        DrawVersion();
    }

    void DrawHeader()
    {
        ImGui.BeginGroup();
        DrawPlaceholder();
        ImGui.SameLine();
        ImGui.BeginGroup();
        using (Globals.Fonts.Header.Push()) ImGui.TextColored(Theme.NameColor, _name ?? "Unknown");
        ImGui.TextColored(Theme.WorldColor, $"@ {_world ?? "Unknown"}");
        ImGui.EndGroup();
        ImGui.EndGroup();

        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
    }

    void DrawImage()
    {
        var pos = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();

        if (!string.IsNullOrEmpty(_data?.PageImage) && Globals.Images.Get(_data.PageImage) is { } tex)
        {
            var (tw, th) = ((float)tex.Width, (float)tex.Height);
            var targetAspect = ImgSize / ImgHeight;
            var srcAspect = tw / th;

            var (u0, u1) = srcAspect > targetAspect
                ? (new Vector2((1f - targetAspect / srcAspect) / 2f, 0), new Vector2(1f - (1f - targetAspect / srcAspect) / 2f, 1))
                : (new Vector2(0, (1f - srcAspect / targetAspect) / 2f), new Vector2(1, 1f - (1f - srcAspect / targetAspect) / 2f));

            dl.AddImageRounded(tex.Handle, pos, pos + new Vector2(ImgSize, ImgHeight), u0, u1, 0xFFFFFFFF, 4f);
            dl.AddRect(pos, pos + new Vector2(ImgSize, ImgHeight), Theme.Col(Theme.FrameBorderInner), 4f);
            ImGui.Dummy(new Vector2(ImgSize, ImgHeight));
        }
        else DrawPlaceholderAt(pos);
    }

    void DrawPlaceholder() => DrawPlaceholderAt(ImGui.GetCursorScreenPos());

    void DrawPlaceholderAt(Vector2 pos)
    {
        var dl = ImGui.GetWindowDrawList();
        var max = pos + new Vector2(ImgSize, ImgHeight);

        dl.AddRectFilled(pos, max, Theme.Col(Theme.PlaceholderBg), 4f);
        dl.AddRect(pos, max, Theme.Col(Theme.FrameBorderInner), 4f);

        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.SetWindowFontScale(1.5f);
        var icon = FontAwesomeIcon.User.ToIconString();
        var size = ImGui.CalcTextSize(icon);
        dl.AddText(pos + (new Vector2(ImgSize, ImgHeight) - size) / 2, Theme.Col(Theme.LabelColorDim), icon);
        ImGui.SetWindowFontScale(1f);
        ImGui.PopFont();

        ImGui.SetCursorScreenPos(pos);
        ImGui.Dummy(new Vector2(ImgSize, ImgHeight));
    }

    static void DrawInfoBox(string label, string content, Vector4 labelColor, Vector4 bgColor)
    {
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var w = ImGui.GetContentRegionAvail().X;
        const float pad = 8f;

        var text = content.Replace("\\n", "\n").Replace("/n", "\n");
        var textSize = ImGui.CalcTextSize(text, false, w - pad * 2);
        var boxHeight = ImGui.GetTextLineHeight() + textSize.Y + 16f;
        var max = start + new Vector2(w, boxHeight);

        dl.AddRectFilled(start, max, Theme.Col(bgColor), 4f);
        dl.AddRect(start, max, Theme.Col(labelColor with { W = 0.4f }), 4f);

        ImGui.BeginGroup();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + pad);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);
        ImGui.TextColored(labelColor, label);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + pad);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + w - pad * 2);
        ImGui.TextColored(Theme.ValueColor, text);
        ImGui.PopTextWrapPos();
        ImGui.EndGroup();

        ImGui.SetCursorScreenPos(new Vector2(start.X, max.Y + 4));
    }

    void DrawVersion()
    {
        ImGui.Spacing();
        ImGui.SetWindowFontScale(Theme.SmallFont);

        var width = ImGui.GetContentRegionAvail().X;

        if (_data != null && !_refreshing)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.ButtonBg with { W = 0.5f });
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.ButtonHovered);
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.LabelColor);
            if (ImGui.SmallButton("View Profile"))
            {
                if (_name != null && _world != null)
                {
                    var lp = Globals.Objects.LocalPlayer;
                    var isSelf = lp != null && _name == lp.Name.TextValue && _world == lp.HomeWorld.Value.Name.ExtractText();
                    if (isSelf)
                        Globals.MainWindow.ShowProfile();
                    else
                        Globals.MainWindow.ShowTarget(_name, _world);
                }
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Open full profile view");
            ImGui.PopStyleColor(3);
        }
        else if (_refreshing) ImGui.TextColored(Theme.LabelColorDim, "Refreshing...");

        var text = $"Glance beta v{Globals.Version}";
        var size = ImGui.CalcTextSize(text);
        ImGui.SameLine(width - size.X);
        ImGui.TextColored(Theme.VersionColor, text);

        ImGui.SetWindowFontScale(1f);
    }

    static void DrawCentered(string text, Vector4 color)
    {
        var width = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX((width - ImGui.CalcTextSize(text).X) / 2);
        ImGui.TextColored(color, text);
    }
    void DrawFooterHooks(ProfileData data)
    {
        if (!Globals.Config.ShowHooksInTooltip || data.Hooks == null || data.Hooks.Count == 0)
            return;

        ImGui.Separator();
        UI.Space(4);

        float windowVisibleX2 = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        var style = ImGui.GetStyle();
        var drawList = ImGui.GetWindowDrawList();

        int maxDisplay = 6;
        float currentLineX = ImGui.GetCursorScreenPos().X;

        for (int i = 0; i < data.Hooks.Count; i++)
        {
            if (i >= maxDisplay)
            {
                string moreText = $" +{data.Hooks.Count - maxDisplay} more";
                ImGui.SetWindowFontScale(0.85f);
                var moreSize = ImGui.CalcTextSize(moreText);
                if (currentLineX + moreSize.X > windowVisibleX2)
                    UI.Space(2); 

                ImGui.TextColored(Theme.TextMuted, moreText);
                ImGui.SetWindowFontScale(1.0f);
                break;
            }

            var hook = data.Hooks[i];
            var title = hook.Title ?? "Hook";

            ImGui.SetWindowFontScale(0.85f);
            var textSize = ImGui.CalcTextSize(title);
            var padding = new Vector2(10f, 3f);
            var boxSize = textSize + (padding * 2);

            if (currentLineX + boxSize.X > windowVisibleX2 && i > 0)
            {
                UI.Space(2);
                currentLineX = ImGui.GetCursorScreenPos().X;
            }
            else if (i > 0)
            {
                ImGui.SameLine(0, 5f);
                currentLineX = ImGui.GetCursorScreenPos().X;
            }

            var p = ImGui.GetCursorScreenPos();
            drawList.AddRectFilled(p, p + boxSize, Theme.Col(Theme.GoldColor with { W = 0.08f }), 12f);
            drawList.AddRect(p, p + boxSize, Theme.Col(Theme.GoldColor with { W = 0.2f }), 12f);
            drawList.AddText(p + padding, Theme.Col(Theme.GoldColor), title);

            ImGui.InvisibleButton($"##hook_{i}", boxSize);

            currentLineX = ImGui.GetItemRectMax().X + 5f;

            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                drawList.AddRect(p, p + boxSize, Theme.Col(Theme.GoldColor with { W = 0.6f }), 12f);

                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(260f);
                ImGui.TextColored(Theme.GoldColor, title);
                if (!string.IsNullOrEmpty(hook.Description))
                {
                    ImGui.Separator();
                    ImGui.TextColored(Theme.ValueColor, hook.Description);
                }
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
            ImGui.SetWindowFontScale(1.0f);
        }

        UI.Space(2);
    }
}
