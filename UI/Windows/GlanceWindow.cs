namespace Glance.UI.Windows;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using Glance.Utils;
using Glance.Core;
using Glance.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

public sealed class GlanceWindow : Window
{
    const float Width = 180f;
    const float GlanceBoxSize = 28f;
    const float GlanceSpacing = 4f;

    string? _name, _world, _profileId;
    ProfileData? _profile;
    bool _loading, _hiding;
    float _alpha;

    const ImGuiWindowFlags Flags =
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoFocusOnAppearing |
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoResize |
        ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking |
        ImGuiWindowFlags.AlwaysAutoResize;

    public GlanceWindow() : base("##Glance", Flags)
    {
        IsOpen = false;
        RespectCloseHotkey = false;
    }

    public void Show(string name, string world, ProfileData? profile, string? profileId)
    {
        _name = name;
        _world = world;
        _profile = profile;
        _profileId = profileId;
        _loading = false;
        _hiding = false;
        IsOpen = true;
    }

    public void Hide() => _hiding = true;

    public override void PreDraw()
    {
        Theme.PushStyle();

        var dt = ImGui.GetIO().DeltaTime;
        _alpha = _hiding
            ? Math.Max(0f, _alpha - dt * 7f)
            : Math.Min(1f, _alpha + dt * 9f);
        if (_hiding && _alpha <= 0f) { IsOpen = false; _hiding = false; }
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, _alpha);

        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(new Vector2(vp.WorkPos.X + vp.WorkSize.X - Width - 24, vp.WorkPos.Y + 80), ImGuiCond.FirstUseEver);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar();
        Theme.PopStyle();
    }

    public override void Draw()
    {
        if (_name == null) { IsOpen = false; return; }

        Theme.DrawFrame();

        DrawHeader();
        ImGui.Spacing();
        DrawGlances();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawToolbar();
    }

    void DrawHeader()
    {
        var w = ImGui.GetContentRegionAvail().X;
        var displayName = _profile?.Name ?? _name ?? "Unknown";

        if (ImGui.CalcTextSize(displayName).X > w)
        {
            while (displayName.Length > 3 && ImGui.CalcTextSize(displayName + "…").X > w)
                displayName = displayName[..^1];
            displayName += "…";
        }

        ImGui.SetCursorPosX((w - ImGui.CalcTextSize(displayName).X) / 2 + Theme.Padding);
        ImGui.TextColored(Theme.NameColor, displayName);

        if (!string.IsNullOrEmpty(_profile?.Description))
        {
            var desc = _profile.Description;
            ImGui.SetWindowFontScale(Theme.SmallFont);
            if (ImGui.CalcTextSize(desc).X > w)
            {
                while (desc.Length > 3 && ImGui.CalcTextSize(desc + "…").X > w)
                    desc = desc[..^1];
                desc += "…";
            }
            ImGui.SetCursorPosX((w - ImGui.CalcTextSize(desc).X) / 2 + Theme.Padding);
            ImGui.TextColored(Theme.TextMuted, desc);
            ImGui.SetWindowFontScale(1f);
        }
    }

    void DrawGlances()
    {
        var glances = _profile?.Glances ?? new List<GlanceData>();
        var w = ImGui.GetContentRegionAvail().X;
        var dl = ImGui.GetWindowDrawList();

        var totalW = 5 * GlanceBoxSize + 4 * GlanceSpacing;
        var startX = ImGui.GetCursorScreenPos().X + (w - totalW) / 2;
        var startY = ImGui.GetCursorScreenPos().Y;

        ImGui.Dummy(new Vector2(w, GlanceBoxSize));

        for (var i = 0; i < 5; i++)
        {
            var hasData = i < glances.Count && glances[i].IconId > 0 && !string.IsNullOrEmpty(glances[i].Label);
            var pos = new Vector2(startX + i * (GlanceBoxSize + GlanceSpacing), startY);
            var max = pos + new Vector2(GlanceBoxSize);

            dl.AddRectFilled(pos, max, Theme.Col(Theme.ButtonBg), 4);
            dl.AddRect(pos, max, Theme.Col(Theme.FrameBorder with { W = hasData ? 0.5f : 0.2f }), 4);

            if (hasData)
            {
                var g = glances[i];
                try
                {
                    var tex = Globals.TextureProvider.GetFromGameIcon(new GameIconLookup(g.IconId));
                    if (tex.TryGetWrap(out var wrap, out _))
                    {
                        var pad = 2f;
                        dl.AddImage(wrap.Handle, pos + new Vector2(pad), max - new Vector2(pad));
                    }
                }
                catch { }

                ImGui.SetCursorScreenPos(pos);
                ImGui.InvisibleButton($"##g{i}", new Vector2(GlanceBoxSize));

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    dl.AddRect(pos, max, Theme.Col(Theme.GoldColor), 4, ImDrawFlags.None, 2);

                    ImGui.BeginTooltip();
                    ImGui.TextColored(Theme.GoldColor, g.Label ?? "");
                    if (!string.IsNullOrEmpty(g.Value))
                    {
                        ImGui.Spacing();
                        ImGui.PushTextWrapPos(220);
                        ImGui.TextColored(Theme.ValueColor, g.Value);
                        ImGui.PopTextWrapPos();
                    }
                    ImGui.EndTooltip();
                }
            }
            else
            {
                ImGui.PushFont(UiBuilder.IconFont);
                var txt = FontAwesomeIcon.Question.ToIconString();
                var tsz = ImGui.CalcTextSize(txt);
                dl.AddText(pos + (new Vector2(GlanceBoxSize) - tsz) / 2, Theme.Col(Theme.LabelColorDim with { W = 0.2f }), txt);
                ImGui.PopFont();

                ImGui.SetCursorScreenPos(pos);
                ImGui.InvisibleButton($"##g{i}", new Vector2(GlanceBoxSize));
            }
        }
    }

    void DrawToolbar()
    {
        var w = ImGui.GetContentRegionAvail().X;
        var btnSize = new Vector2(22);
        var btnCount = 5;
        var spacing = 6f;
        var totalW = btnCount * btnSize.X + (btnCount - 1) * spacing;
        var startX = (w - totalW) / 2;

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + startX);

        if (IconBtn(FontAwesomeIcon.User, "Open Profile", btnSize))
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

        ImGui.SameLine(0, spacing);

        var hasNotes = (_profileId != null && Globals.Notes.Get(_profileId).Length > 0) || (_name != null && _world != null && Globals.Notes.Get($"{_name}@{_world}").Length > 0);
        if (IconBtn(FontAwesomeIcon.StickyNote, hasNotes ? "Notes (has notes)" : "Notes", btnSize, false, hasNotes))
        {
            if (_name != null && _world != null)
            {
                Globals.MainWindow.ShowTarget(_name, _world, 2);
                Tabs.ProfileTab.SetTab(2);
            }
        }

        ImGui.SameLine(0, spacing);

        if (IconBtn(FontAwesomeIcon.ExternalLinkAlt, "View on RPHub", btnSize, _profileId == null))
            if (_profileId != null)
                Process.Start(new ProcessStartInfo { FileName = $"https://rphub.co/ch/{_profileId}", UseShellExecute = true });

        ImGui.SameLine(0, spacing);

        if (IconBtn(_loading ? FontAwesomeIcon.Spinner : FontAwesomeIcon.Sync, _loading ? "Loading..." : "Refresh", btnSize, _loading))
            Refresh();

        ImGui.SameLine(0, spacing);

        if (IconBtn(FontAwesomeIcon.ExclamationTriangle, "Report Profile", btnSize, _profileId == null))
        {
            ReportWindow.Open(_name, _world, _profileId);
            Sound.PlayOpen();
        }
    }

    void Refresh()
    {
        if (_name == null || _world == null) return;
        _loading = true;
        _ = Globals.Cache.RefreshProfileAsync(_name, _world).ContinueWith(t =>
        {
            if (t.Result?.Data != null)
            {
                _profile = t.Result.Data;
                _profileId = t.Result.Id.ToString();
            }
            _loading = false;
        });
    }

    static bool IconBtn(FontAwesomeIcon icon, string tip, Vector2 sz, bool disabled = false, bool highlight = false)
    {
        var pos = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();

        var clicked = false;
        if (disabled)
            ImGui.InvisibleButton($"##{tip}", sz);
        else
            clicked = ImGui.InvisibleButton($"##{tip}", sz);

        var hov = ImGui.IsItemHovered() && !disabled;

        if (highlight && !hov)
            dl.AddRectFilled(pos, pos + sz, Theme.Col(Theme.GoldColor with { W = 0.15f }), 3);
        if (hov)
            dl.AddRectFilled(pos, pos + sz, Theme.Col(Theme.ButtonHovered), 3);

        ImGui.PushFont(UiBuilder.IconFont);
        var txt = icon.ToIconString();
        var tsz = ImGui.CalcTextSize(txt);
        var col = disabled ? Theme.LabelColorDim : hov || highlight ? Theme.GoldColor : Theme.LabelColor;
        dl.AddText(pos + (sz - tsz) / 2, Theme.Col(col), txt);
        ImGui.PopFont();

        if (hov)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            ImGui.SetTooltip(tip);
        }

        return clicked;
    }
}
