namespace Glance.UI.Windows;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Glance.Utils;
using Glance.Core;
using Glance.Models;
using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

public sealed class ToolboxWindow : Window
{
    const float IconSize = 28f;
    const float Spacing = 4f;
    const float PickerIconSize = 32f;
    const float PickerSpacing = 4f;

    string _icEdit = "", _oocEdit = "";
    bool _saving;
    bool _initialized;
    string? _activeProfileId;
    ProfileData? _activeProfile;

    const ImGuiWindowFlags Flags =
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking |
        ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize;

    public ToolboxWindow() : base("##RPHubToolbox", Flags)
    {
        IsOpen = false;
        RespectCloseHotkey = false;
    }

    public void Toggle() => IsOpen = !IsOpen;

    public override bool DrawConditions()
    {
        RefreshActiveProfile();
        return Globals.Config.ShowToolbox && Globals.Auth.IsAuthenticated && _activeProfileId != null;
    }

    IDisposable? _themeScope;

    public override void PreDraw()
    {
        _themeScope = Theme.PushStyle();
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(new Vector2(vp.WorkPos.X + 16, vp.WorkPos.Y + vp.WorkSize.Y - 80), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowBgAlpha(0.9f);
    }

    public override void PostDraw() { _themeScope?.Dispose(); _themeScope = null; }

    public override void Draw()
    {
        var dl = ImGui.GetWindowDrawList();

        ImGui.SetWindowFontScale(0.85f);
        ImGui.TextColored(Theme.LabelColorDim, "Toolbox");
        ImGui.SetWindowFontScale(1f);

        if (Globals.PlayerState == null)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Theme.Error))
                ImGui.TextWrapped("Not logged in");
            return;
        }

        var p = ImGui.GetCursorScreenPos();

        var totalW = 7 * IconSize + 6 * Spacing;
        ImGui.Dummy(new Vector2(totalW, IconSize));

        var x = p.X;

        DrawProfileIcon(dl, new Vector2(x, p.Y));
        x += IconSize + Spacing;

        var hasIc = !string.IsNullOrEmpty(_activeProfile?.CurrentStatus);
        DrawIconButton(dl, new Vector2(x, p.Y), FontAwesomeIcon.Comment, "IC Status", hasIc, () => OpenPopup("ic"));
        x += IconSize + Spacing;

        var hasOoc = !string.IsNullOrEmpty(_activeProfile?.PlayerNotes);
        DrawIconButton(dl, new Vector2(x, p.Y), FontAwesomeIcon.CommentDots, "OOC Notes", hasOoc, () => OpenPopup("ooc"));
        x += IconSize + Spacing;

        var hasCustomIcon = Globals.Config.NameplateCustomIconId > 0;
        DrawIconButton(dl, new Vector2(x, p.Y), FontAwesomeIcon.IdBadge, "Nameplate Icon", hasCustomIcon,
            () => OpenPopup("icon"));
        x += IconSize + Spacing;

        var locOn = Globals.Config.BeaconLocationSharing && Globals.Config.BeaconEnabled;
        DrawIconButton(dl, new Vector2(x, p.Y), FontAwesomeIcon.MapMarkerAlt, locOn ? "Location: ON" : "Location: OFF", locOn,
            () => { if (Globals.Config.BeaconEnabled) { Globals.Config.BeaconLocationSharing = !Globals.Config.BeaconLocationSharing; Globals.Config.Save(); Sound.PlayOpen(); } },
            !Globals.Config.BeaconEnabled);
        x += IconSize + Spacing;

        DrawIconButton(dl, new Vector2(x, p.Y), FontAwesomeIcon.Users, "Nearby Players", false, OpenNearby);
        x += IconSize + Spacing;

        DrawIconButton(dl, new Vector2(x, p.Y), FontAwesomeIcon.Cog, "Settings", false, OpenSettings);

        DrawICPopup();
        DrawOOCPopup();
        DrawIconPickerPopup();
    }

    void RefreshActiveProfile()
    {
        var localPlayer = Globals.PlayerState;
        if (localPlayer == null || string.IsNullOrEmpty(localPlayer.CharacterName.ToString()))
        {
            _activeProfileId = null;
            _activeProfile = null;
            _initialized = false;
            return;
        }

        var currentName = localPlayer.CharacterName.ToString();
        var currentWorld = localPlayer.HomeWorld.Value.Name.ToString();

        var chars = Globals.Profiles.Data?.Characters;
        if (chars == null || chars.Length == 0)
        {
            return;
        }

        var activeId = Globals.Profiles.ActiveProfileId;
        var active = activeId != null ? chars.FirstOrDefault(c => c.Id == activeId) : null;

        if (active == null)
        {
            _activeProfileId = null;
            return;
        }

        var fetchWorld = string.IsNullOrEmpty(active.World) ? currentWorld : active.World;

        if (active.Id != _activeProfileId)
        {
            _activeProfileId = active.Id;
            _activeProfile = null;
            _initialized = false;
        }

        var cached = Globals.Cache.GetProfile(currentName, fetchWorld);

        if (cached?.Data != null)
        {
            _activeProfile = cached.Data;
            _initialized = true;
        }
        else if (!_initialized)
        {
            _initialized = true;

            _ = Globals.Cache.FetchProfileAsync(currentName, fetchWorld).ContinueWith(t =>
            {
                if (t.Result?.Data != null)
                {
                    _activeProfile = t.Result.Data;
                }
            });
        }
    }

    void DrawProfileIcon(ImDrawListPtr dl, Vector2 pos)
    {
        var max = pos + new Vector2(IconSize);

        dl.AddRectFilled(pos, max, Theme.Col(Theme.ButtonBg), 4);
        dl.AddRect(pos, max, Theme.Col(Theme.FrameBorder with { W = 0.4f }), 4);

        if (_activeProfile != null && !string.IsNullOrEmpty(_activeProfile.PageImage))
        {
            var tex = Globals.Images.Get(_activeProfile.PageImage);
            if (tex != null)
            {
                var (tw, th) = ((float)tex.Width, (float)tex.Height);
                var aspect = tw / th;
                var (u0, u1) = aspect > 1
                    ? (new Vector2((1f - 1f / aspect) / 2f, 0), new Vector2(1f - (1f - 1f / aspect) / 2f, 1))
                    : (new Vector2(0, (1f - aspect) / 2f), new Vector2(1, 1f - (1f - aspect) / 2f));
                dl.AddImageRounded(tex.Handle, pos + new Vector2(2), max - new Vector2(2), u0, u1, 0xFFFFFFFF, 3);
            }
        }
        else
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                var icon = FontAwesomeIcon.User.ToIconString();
                var sz = ImGui.CalcTextSize(icon);
                dl.AddText(pos + (new Vector2(IconSize) - sz) / 2, Theme.Col(Theme.LabelColorDim), icon);
            }
        }

        ImGui.SetCursorScreenPos(pos);
        if (ImGui.InvisibleButton("##profilepic", new Vector2(IconSize)))
            OpenProfile();

        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            dl.AddRect(pos, max, Theme.Col(Theme.GoldColor with { W = 0.7f }), 4, ImDrawFlags.None, 2);

            using var tip = ImRaii.Tooltip();
            var name = _activeProfile?.Name ?? Globals.Profiles.Data?.Characters?.FirstOrDefault(c => c.Id == _activeProfileId)?.Name ?? "No Profile";
            ImGui.TextColored(Theme.LabelColor, name);
        }
    }

    void DrawIconButton(ImDrawListPtr dl, Vector2 pos, FontAwesomeIcon icon, string tooltip,
        bool active, Action onClick, bool disabled = false)
    {
        var max = pos + new Vector2(IconSize);

        dl.AddRectFilled(pos, max, Theme.Col(Theme.ButtonBg), 4);
        dl.AddRect(pos, max, Theme.Col(Theme.FrameBorder with { W = active ? 0.5f : 0.3f }), 4);

        if (active)
            dl.AddRectFilled(pos + new Vector2(1), max - new Vector2(1), Theme.Col(Theme.LabelColor with { W = 0.08f }), 3);

        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var iconStr = icon.ToIconString();
            var sz = ImGui.CalcTextSize(iconStr);
            var col = disabled ? Theme.LabelColorDim with { W = 0.4f } : active ? Theme.LabelColor : Theme.LabelColorDim;
            dl.AddText(pos + (new Vector2(IconSize) - sz) / 2, Theme.Col(col), iconStr);
        }

        ImGui.SetCursorScreenPos(pos);
        if (disabled)
            ImGui.InvisibleButton($"##{tooltip}", new Vector2(IconSize));
        else if (ImGui.InvisibleButton($"##{tooltip}", new Vector2(IconSize)))
            onClick();

        if (ImGui.IsItemHovered() && !disabled)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            dl.AddRect(pos, max, Theme.Col(Theme.GoldColor with { W = 0.7f }), 4, ImDrawFlags.None, 2);
            ImGui.SetTooltip(tooltip);
        }
    }

    void OpenPopup(string id)
    {
        Sound.PlayOpen();
        _icEdit = _activeProfile?.CurrentStatus ?? "";
        _oocEdit = _activeProfile?.PlayerNotes ?? "";
        ImGui.OpenPopup($"##toolbox_{id}");
    }

    public void ResetState()
    {
        Globals.Log.Info("[Toolbox] ResetState called - clearing for re-sync");
        _activeProfileId = null;
        _activeProfile = null;
        _initialized = false;
    }

    void DrawICPopup()
    {
        ImGui.SetNextWindowSize(new Vector2(260, 0), ImGuiCond.Always);
        using var popup = ImRaii.Popup("##toolbox_ic");
        if (!popup.Success) return;

        ImGui.TextColored(Theme.LabelColor, "IC Status");
        ImGui.Separator();

        using (ImRaii.PushColor(ImGuiCol.FrameBg, Theme.ButtonBg))
            ImGui.InputTextMultiline("##icedit", ref _icEdit, 150, new Vector2(244, 70));

        ImGui.Spacing();

        var changed = _icEdit != (_activeProfile?.CurrentStatus ?? "");
        using (ImRaii.Disabled(!changed))
        using (ImRaii.PushColor(ImGuiCol.Button, Theme.PrimaryButtonBg)
            .Push(ImGuiCol.Text, Theme.PrimaryButtonText))
        {
            if (ImGui.Button(_saving ? "..." : "Save", new Vector2(50, 20)))
            {
                SaveField("ic");
                Sound.PlaySuccess();
                ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, Theme.ButtonBg)
            .Push(ImGuiCol.Text, Theme.LabelColor))
        {
            if (ImGui.Button("Cancel", new Vector2(50, 20))) { ImGui.CloseCurrentPopup(); Sound.PlayCancel(); }
        }
    }

    void DrawOOCPopup()
    {
        ImGui.SetNextWindowSize(new Vector2(260, 0), ImGuiCond.Always);
        using var popup = ImRaii.Popup("##toolbox_ooc");
        if (!popup.Success) return;

        ImGui.TextColored(Theme.LabelColor, "OOC Notes");
        ImGui.Separator();

        using (ImRaii.PushColor(ImGuiCol.FrameBg, Theme.ButtonBg))
            ImGui.InputTextMultiline("##oocedit", ref _oocEdit, 250, new Vector2(244, 70));

        ImGui.Spacing();

        var changed = _oocEdit != (_activeProfile?.PlayerNotes ?? "");
        using (ImRaii.Disabled(!changed))
        using (ImRaii.PushColor(ImGuiCol.Button, Theme.PrimaryButtonBg)
            .Push(ImGuiCol.Text, Theme.PrimaryButtonText))
        {
            if (ImGui.Button(_saving ? "..." : "Save", new Vector2(50, 20)))
            {
                SaveField("ooc");
                Sound.PlaySuccess();
                ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, Theme.ButtonBg)
            .Push(ImGuiCol.Text, Theme.LabelColor))
        {
            if (ImGui.Button("Cancel", new Vector2(50, 20))) { ImGui.CloseCurrentPopup(); Sound.PlayCancel(); }
        }
    }

    void DrawIconPickerPopup()
    {
        ImGui.SetNextWindowSize(new Vector2(320, 0), ImGuiCond.Always);
        using var popup = ImRaii.Popup("##toolbox_icon");
        if (!popup.Success) return;

        ImGui.TextColored(Theme.LabelColor, "Nameplate Icon");
        ImGui.Separator();
        ImGui.Spacing();

        var current = Globals.Config.NameplateCustomIconId;

        if (current > 0)
        {
            using (ImRaii.PushColor(ImGuiCol.Button, Theme.ButtonBg)
                .Push(ImGuiCol.Text, Theme.LabelColor))
            {
                if (ImGui.Button("Reset to Default", new Vector2(-1, 22)))
                {
                    Globals.Config.NameplateCustomIconId = 0;
                    Globals.Config.Save();
                    Sound.PlayClick();
                    ImGui.CloseCurrentPopup();
                }
            }
            ImGui.Spacing();
        }

        ImGui.TextColored(Theme.LabelColorDim, "Community Activities");
        ImGui.Spacing();
        DrawIconGrid(RpIcons.CommunityIcons, current, "community");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(Theme.LabelColorDim, "Housing Appeal");
        ImGui.Spacing();
        DrawIconGrid(RpIcons.HousingIcons, current, "housing");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        using (ImRaii.PushColor(ImGuiCol.Text, Theme.TextMuted))
        {
            ImGui.TextWrapped(
                "This icon replaces the default RPHub nameplate icon for players " +
                "who have set their status to Role-playing (/roleplaying) or have " +
                "the RP tag active. Changes may take a moment to appear as " +
                "nameplates refresh periodically.");
        }

        ImGui.Spacing();
    }

    void DrawIconGrid(RpIcons.IconEntry[] icons, int currentId, string prefix)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        var cols = Math.Max(1, (int)((avail + PickerSpacing) / (PickerIconSize + PickerSpacing)));

        for (var i = 0; i < icons.Length; i++)
        {
            var entry = icons[i];
            var isSelected = entry.Id == currentId;

            if (i % cols != 0)
                ImGui.SameLine(0, PickerSpacing);

            var cursor = ImGui.GetCursorScreenPos();
            var tex = Globals.TextureProvider.GetFromGameIcon(new Dalamud.Interface.Textures.GameIconLookup((uint)entry.Id))
                .GetWrapOrDefault();

            if (isSelected)
            {
                ImGui.GetWindowDrawList().AddRectFilled(
                    cursor - new Vector2(2),
                    cursor + new Vector2(PickerIconSize + 2),
                    Theme.Col(Theme.GoldColor with { W = 0.3f }), 4);
                ImGui.GetWindowDrawList().AddRect(
                    cursor - new Vector2(2),
                    cursor + new Vector2(PickerIconSize + 2),
                    Theme.Col(Theme.GoldColor with { W = 0.8f }), 4, ImDrawFlags.None, 2);
            }

            if (tex != null)
            {
                ImGui.Image(tex.Handle, new Vector2(PickerIconSize));
            }
            else
            {
                ImGui.Dummy(new Vector2(PickerIconSize));
            }

            ImGui.SetCursorScreenPos(cursor);
            if (ImGui.InvisibleButton($"##icon_{prefix}_{entry.Id}", new Vector2(PickerIconSize)))
            {
                Globals.Config.NameplateCustomIconId = isSelected ? 0 : entry.Id;
                Globals.Config.Save();
                Sound.PlayClick();
                ImGui.CloseCurrentPopup();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (!isSelected)
                {
                    ImGui.GetWindowDrawList().AddRect(
                        cursor - new Vector2(1),
                        cursor + new Vector2(PickerIconSize + 1),
                        Theme.Col(Theme.LabelColor with { W = 0.5f }), 4);
                }
                ImGui.SetTooltip(entry.Label);
            }
        }
    }

    void SaveField(string field)
    {
        if (_activeProfileId == null || _activeProfile == null || _saving) return;
        _saving = true;

        var active = Globals.Profiles.Data?.Characters?.FirstOrDefault(c => c.Id == _activeProfileId);
        if (active == null) { _saving = false; return; }

        _ = Task.Run(async () =>
        {
            try
            {
                Globals.ProfileEdit.Load(_activeProfile, _activeProfileId);

                if (Globals.ProfileEdit.Draft != null)
                {
                    if (field == "ic") Globals.ProfileEdit.Draft.CurrentStatus = _icEdit;
                    else if (field == "ooc") Globals.ProfileEdit.Draft.PlayerNotes = _oocEdit;

                    var (ok, err) = await Globals.ProfileEdit.SaveAsync();
                    Globals.Log.Info($"[SaveField] SaveAsync result: ok={ok}, err={err}");

                    if (ok && active.Name != null && active.World != null)
                    {
                        if (field == "ic") _activeProfile.CurrentStatus = _icEdit;
                        else if (field == "ooc") _activeProfile.PlayerNotes = _oocEdit;

                        await Globals.Cache.RefreshProfileAsync(active.Name, active.World);
                        var refreshed = Globals.Cache.GetProfile(active.Name, active.World);
                        if (refreshed?.Data != null) _activeProfile = refreshed.Data;
                    }
                }
            }
            catch (Exception ex)
            {
                Globals.Log.Error($"[SaveField] Error: {ex.Message}");
            }
            finally { _saving = false; }
        });
    }

    void OpenProfile() => Globals.MainWindow.ShowProfile();

    void OpenNearby()
    {
        Globals.NearbyWindow.IsOpen = true;
        Globals.NearbyWindow.BringToFront();
        Sound.PlayOpen();
    }

    void OpenSettings()
    {
        Globals.MainWindow.SetTab(3);
        Globals.MainWindow.IsOpen = true;
        Globals.MainWindow.BringToFront();
        Sound.PlayOpen();
    }
}
