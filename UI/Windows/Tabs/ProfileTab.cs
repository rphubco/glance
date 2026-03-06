namespace Glance.UI.Tabs;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Glance.Core;
using Glance.Models;
using Glance.UI.Windows;
using Glance.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;

public static class ProfileTab
{
    public static string? CurrentName { get; private set; }
    public static string? CurrentWorld { get; private set; }
    static ProfileData? _data;
    static string? _profileId;
    static bool _loading;
    static int _tab;
    static string _profileNote = "", _charNote = "";
    static bool _profileNoteDirty, _charNoteDirty;

    static string? _targetName, _targetWorld, _targetProfileId;
    static ProfileData? _targetData;
    static bool _targetLoading;
    static string? _lastViewedKey;
    public static void SetTab(int index) => _tab = index;
    public static void Show(string name, string world, ProfileData? data, string? profileId = null, int tab = 0)
    {
        (CurrentName, CurrentWorld, _data, _profileId, _loading, _tab) = (name, world, data, profileId, false, tab);
        _profileNote = Globals.Notes.Get(profileId ?? $"{name}@{world}");
        _charNote = Globals.Notes.Get($"{name}@{world}");
        _profileNoteDirty = _charNoteDirty = false;
        _lastViewedKey = profileId ?? $"{name}@{world}";
    }

    public static void EnsureTargetLoaded(string name, string world)
    {
        if (_targetName == name && _targetWorld == world)
            return;
        ShowTarget(name, world);
    }

    public static void SetTargetData(string name, string world, ProfileData data, string? profileId)
    {
        _targetName = name;
        _targetWorld = world;
        _targetData = data;
        _targetProfileId = profileId;
        _targetLoading = false;
    }

    public static void SetTargetLoading(string name, string world)
    {
        _targetName = name;
        _targetWorld = world;
        _targetData = null;
        _targetProfileId = null;
        _targetLoading = true;
    }

    public static void SetTargetNotFound(string name, string world)
    {
        if (_targetName == name && _targetWorld == world)
        {
            _targetData = null;
            _targetProfileId = null;
            _targetLoading = false;
        }
    }


    public static void Clear() => (CurrentName, CurrentWorld, _data, _profileId) = (null, null, null, null);

    public static void Draw() => Draw(false);
    public static void DrawTarget() => Draw(true);

    public static void ShowTarget(string name, string world)
    {
        _targetName = name;
        _targetWorld = world;
        _targetData = null;
        _targetProfileId = null;
        _targetLoading = true;

        var cached = Globals.Cache.GetProfile(name, world);
        if (cached?.Data != null)
        {
            _targetData = cached.Data;
            _targetProfileId = cached.Id.ToString();
            _targetLoading = false;
        }
        else
        {
            _ = Globals.Cache.FetchProfileAsync(name, world).ContinueWith(t =>
            {
                if (t.Result?.Data != null && _targetName == name && _targetWorld == world)
                {
                    _targetData = t.Result.Data;
                    _targetProfileId = t.Result.Id.ToString();
                }
                _targetLoading = false;
            });
        }
    }

    static void Draw(bool targetMode)
    {
        if (targetMode)
        {
            if (_targetName == null || _targetWorld == null)
            {
                Empty("No Target", "Select a player to view");
                return;
            }

            if (_targetLoading)
            {
                Empty("Loading...", $"Fetching {_targetName}...");
                return;
            }

            if (_targetData == null)
            {
                Empty("No Profile", "No RPHub profile for this character");
                return;
            }

            DrawContent(_targetName, _targetWorld, _targetData, _targetProfileId, false);
        }
        else
        {
            if (Globals.Profiles.Data == null)
            {
                Empty("Loading...", "Fetching your profiles...");
                return;
            }

            if (CurrentName == null || _data == null)
            {
                var activeId = Globals.Profiles.ActiveProfileId;
                var active = activeId != null
                    ? Globals.Profiles.Data.Characters?.FirstOrDefault(c => c.Id == activeId)
                    : null;

                if (active == null)
                {
                    Empty("No Profile", "Select a profile in Characters");
                    return;
                }

                if (Globals.Objects.LocalPlayer is not { } lp)
                {
                    Empty("Not Logged In", "Log into a character to view profile");
                    return;
                }

                var charName = lp.Name.TextValue;
                var charWorld = lp.HomeWorld.Value.Name.ExtractText();

                var cached = Globals.Cache.GetProfile(charName, charWorld);
                if (cached?.Data != null)
                {
                    Show(charName, charWorld, cached.Data, cached.Id.ToString());
                }
                else
                {
                    if (!_loading)
                    {
                        _loading = true;
                        (CurrentName, CurrentWorld, _profileId) = (charName, charWorld, active.Id);
                        _ = Globals.Cache.FetchProfileAsync(charName, charWorld).ContinueWith(t =>
                        {
                            if (t.Result?.Data != null)
                                (_data, _profileId) = (t.Result.Data, t.Result.Id.ToString());
                            _loading = false;
                        });
                    }
                    Empty("Loading...", $"Fetching {charName}...");
                    return;
                }
            }

            DrawContent(CurrentName, CurrentWorld, _data, _profileId, Globals.Profiles.ActiveProfileId == _profileId);
        }
    }

        static void DrawContent(string? name, string? world, ProfileData? data, string? profileId, bool canEdit)
    {
        var key = profileId ?? $"{name}@{world}"; if (_lastViewedKey != key) { _lastViewedKey = key; _profileNote = Globals.Notes.Get(key); _charNote = Globals.Notes.Get($"{name}@{world}"); _profileNoteDirty = _charNoteDirty = false; }
        if (ProfileEditor.IsActive && canEdit) { ProfileEditor.Draw(name, world, () => { if (name != null && world != null) _ = Globals.Cache.RefreshProfileAsync(name, world).ContinueWith(t => { if (t.Result?.Data != null) _data = t.Result.Data; }); }); return; }
        DrawHeader(name, world, data, profileId, canEdit); UI.Space(); DrawTabs(); UI.Space();
        if (_tab == 0) GeneralTab(data); else if (_tab == 1) HooksTab(data); else NotesTab(name, world, profileId);
    }

    static void DrawHeader(string? name, string? world, ProfileData? data, string? profileId, bool canEdit)
    {
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        var w = ImGui.GetContentRegionAvail().X;

        const float imgW = 140, imgH = 150, pad = UI.Lg;
        const float glanceBoxSize = 24f;
        const float glanceSpacing = 4f;
        const float glanceRowHeight = glanceBoxSize + 8;
        var h = imgH + pad * 2;

        dl.AddRectFilled(p, p + new Vector2(w, h), Theme.Col(Theme.FrameBorderInner with { W = 0.15f }), 8);
        var ip = p + new Vector2(pad);
        var ie = ip + new Vector2(imgW, imgH);
        var tex = Globals.Images.Get(data?.PageImage);
        if (tex != null)
        {
            var (tw, th) = ((float)tex.Width, (float)tex.Height);
            var r = imgW / imgH;
            var tr = tw / th;
            var (u0, u1) = tr > r
                ? (new Vector2((tw - th * r) / 2 / tw, 0), new Vector2(1 - (tw - th * r) / 2 / tw, 1))
                : (new Vector2(0, (th - tw / r) / 2 / th), new Vector2(1, 1 - (th - tw / r) / 2 / th));
            dl.AddImageRounded(tex.Handle, ip, ie, u0, u1, 0xFFFFFFFF, 6);
        }
        dl.AddRect(ip, ie, Theme.Col(Theme.FrameBorderInner with { W = 0.6f }), 6);
        DrawGlances(dl, p.X + pad, p.Y + pad + imgH - (glanceBoxSize / 2), imgW, glanceBoxSize, glanceSpacing, data);
        ImGui.SetCursorScreenPos(p + new Vector2(pad + imgW + UI.Lg, pad));
        ImGui.BeginGroup();
        using (Globals.Fonts.Title.Push()) ImGui.TextColored(Theme.NameColor, data?.Name ?? name ?? "Unknown");
        if (!string.IsNullOrEmpty(data?.Description))
        {
            ImGui.PushTextWrapPos(p.X + w - pad - 40);
            ImGui.TextColored(Theme.ValueColor, data.Description);
            ImGui.PopTextWrapPos();
        }
        else ImGui.TextColored(Theme.TextMuted, "No description provided");

        UI.Space(UI.Sm);
        var meta = string.Join("  •  ", new[] { data?.Race, data?.Clan }.Where(s => !string.IsNullOrEmpty(s)));
        if (meta.Length > 0) { IconText(FontAwesomeIcon.Dna, Theme.LabelColorDim, meta); UI.Space(UI.Xs); }
        IconText(FontAwesomeIcon.Globe, Theme.WorldColor, $"{name} @ {world}");
        if (!string.IsNullOrEmpty(data?.FreeCompany)) { UI.Space(UI.Xs); IconText(FontAwesomeIcon.Shield, Theme.LabelColor, data.FreeCompany); }
        ImGui.EndGroup();
        var bx = p.X + w - pad - 24;
        if (profileId != null && IconBtn(dl, new Vector2(bx, p.Y + pad), FontAwesomeIcon.ExternalLinkAlt, "View on RPHub"))
            Process.Start(new ProcessStartInfo { FileName = $"https://rphub.co/ch/{profileId}", UseShellExecute = true });
        bx -= 28;
        if (IconBtn(dl, new Vector2(bx, p.Y + pad), FontAwesomeIcon.Sync, "Refresh")) Refresh(name, world);
        if (canEdit) { bx -= 28; if (IconBtn(dl, new Vector2(bx, p.Y + pad), FontAwesomeIcon.Edit, "Edit Profile")) ProfileEditor.OpenForEdit(data, profileId); }
        if (!canEdit && profileId != null)
        {
            bx -= 28;
            if (IconBtn(dl, new Vector2(bx, p.Y + pad), FontAwesomeIcon.ExclamationTriangle, "Report Profile"))
            {
                ReportWindow.Open(name, world, profileId);
            }
        }
        ImGui.SetCursorScreenPos(p + new Vector2(0, h + UI.Sm));
    }

    static void DrawGlances(ImDrawListPtr dl, float x, float y, float containerW, float boxSize, float spacing, ProfileData? data)
    {
        var glances = data?.Glances ?? new List<GlanceData>();

        var totalW = 5 * boxSize + 4 * spacing;
        var startX = x + (containerW - totalW) / 2;
 

        for (var i = 0; i < 5; i++)
        {
            var hasData = i < glances.Count && glances[i].IconId > 0 && !string.IsNullOrEmpty(glances[i].Label);
            var pos = new Vector2(startX + i * (boxSize + spacing), y);
            var max = pos + new Vector2(boxSize);

            dl.AddRectFilled(pos, max, Theme.Col(Theme.ButtonBg), 4);
            dl.AddRect(pos, max, Theme.Col(Theme.FrameBorder with { W = hasData ? 0.5f : 0.2f }), 4);

            if (hasData)
            {
                var g = glances[i];

                var tex = Globals.TextureProvider.GetFromGameIcon(new GameIconLookup(g.IconId));
                if (tex.TryGetWrap(out var wrap, out _))
                {
                    var iconSz = new Vector2(boxSize - 4);
                    dl.AddImage(wrap.Handle, pos + new Vector2(2), pos + new Vector2(2) + iconSz);
                }

                ImGui.SetCursorScreenPos(pos);
                ImGui.InvisibleButton($"##glance{i}", new Vector2(boxSize));

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    dl.AddRect(pos, max, Theme.Col(Theme.GoldColor), 4, ImDrawFlags.None, 2);

                    ImGui.BeginTooltip();
                    ImGui.TextColored(Theme.GoldColor, g.Label ?? "");
                    if (!string.IsNullOrEmpty(g.Value))
                    {
                        ImGui.Spacing();
                        ImGui.PushTextWrapPos(250);
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
                dl.AddText(pos + (new Vector2(boxSize) - tsz) / 2, Theme.Col(Theme.LabelColorDim with { W = 0.3f }), txt);
                ImGui.PopFont();
                ImGui.SetCursorScreenPos(pos);
                ImGui.InvisibleButton($"##glance{i}", new Vector2(boxSize));
            }
        }
    }

    static void DrawTabs()
    {
        var w = ImGui.GetContentRegionAvail().X; string[] tabs = ["General", "Hooks", "Notes"]; var tw = (w - UI.Xs * 2) / 3;
        for (var i = 0; i < tabs.Length; i++)
        {
            if (i > 0) ImGui.SameLine(0, UI.Xs); var sel = _tab == i;
            ImGui.PushStyleColor(ImGuiCol.Button, sel ? Theme.ButtonActive : Theme.ButtonBg);
            ImGui.PushStyleColor(ImGuiCol.Text, sel ? Theme.GoldColor : Theme.LabelColor);
            if (ImGui.Button(tabs[i], new Vector2(tw, 30)))
            {
                if (_tab != i)
                {
                    _tab = i;
                    Sound.PlayClick();
                }
            }
            ImGui.PopStyleColor(2);
        }
    }

    static void DrawInfoBox(string label, string content, Vector4 labelColor, Vector4 bgColor)
    {
        var dl = ImGui.GetWindowDrawList(); var start = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X;
        var text = content.Replace("\\n", "\n").Replace("/n", "\n"); const float pad = 8f;
        var textSize = ImGui.CalcTextSize(text, false, w - pad * 2); var boxHeight = ImGui.GetTextLineHeight() + textSize.Y + 16f; var max = start + new Vector2(w, boxHeight);
        dl.AddRectFilled(start, max, Theme.Col(bgColor), 4f); dl.AddRect(start, max, Theme.Col(labelColor with { W = 0.4f }), 4f);
        ImGui.BeginGroup(); ImGui.SetCursorPosX(ImGui.GetCursorPosX() + pad); ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4); ImGui.TextColored(labelColor, label);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + pad); ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + w - pad * 2); ImGui.TextColored(Theme.ValueColor, text); ImGui.PopTextWrapPos(); ImGui.EndGroup();
        ImGui.SetCursorScreenPos(new Vector2(start.X, max.Y + 4));
    }

    static void GeneralTab(ProfileData? data)
    {
        if (data == null) return;

        if (!string.IsNullOrEmpty(data.CurrentStatus)) { DrawInfoBox("Currently (IC)", data.CurrentStatus, new Vector4(0.4f, 0.8f, 0.4f, 1f), new Vector4(0.15f, 0.22f, 0.15f, 0.9f)); UI.Space(); }
        if (!string.IsNullOrEmpty(data.PlayerNotes)) { DrawInfoBox("Player's Notes (OOC)", data.PlayerNotes, new Vector4(0.7f, 0.7f, 0.9f, 1f), new Vector4(0.18f, 0.18f, 0.25f, 0.9f)); UI.Space(); }
        if (!string.IsNullOrEmpty(data.Details)) { Section("Details"); Wrapped(data.Details); UI.Space(); }
        if (data.About is { Count: > 0 })
        {
            Section("About");
            if (ImGui.BeginTable("##about", 2, ImGuiTableFlags.SizingStretchSame))
            {
                foreach (var f in data.About.Where(f => !string.IsNullOrEmpty(f.Input)))
                {
                    ImGui.TableNextColumn();
                    ImGui.TextColored(Theme.LabelColorDim, f.Label ?? "Info");
                    ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X);
                    ImGui.TextColored(Theme.ValueColor, f.Input);
                    ImGui.PopTextWrapPos();
                    UI.Space(UI.Xs);
                }
                ImGui.EndTable();
            }
        }
    }

    static void HooksTab(ProfileData? data)
    {
        if (data?.Hooks is not { Count: > 0 }) { Empty("No Hooks", "No RP hooks defined"); return; }
        foreach (var h in data.Hooks)
        {
            var dl = ImGui.GetWindowDrawList(); var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X;
            ImGui.BeginGroup(); ImGui.Dummy(new Vector2(w, UI.Sm)); ImGui.Indent(UI.Md);
            using (Globals.Fonts.Header.Push()) ImGui.TextColored(Theme.GoldColor, h.Title ?? "Hook");
            if (!string.IsNullOrEmpty(h.Description)) { UI.Space(UI.Xs); ImGui.SetWindowFontScale(Theme.MediumFont); Wrapped(h.Description, Theme.TextMuted); ImGui.SetWindowFontScale(1f); }
            ImGui.Unindent(UI.Md); ImGui.Dummy(new Vector2(0, UI.Sm)); ImGui.EndGroup();
            var e = ImGui.GetItemRectMax(); var hov = ImGui.IsItemHovered(); if (hov) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            dl.AddRectFilled(p, new Vector2(p.X + w, e.Y), Theme.Col(Theme.ButtonBg with { W = hov ? 0.35f : 0.25f }), 4);
            dl.AddRectFilled(p, new Vector2(p.X + 3, e.Y), Theme.Col(Theme.GoldColor with { W = hov ? 0.9f : 0.6f }), 4, ImDrawFlags.RoundCornersLeft);
            UI.Space(UI.Sm);
        }
    }

    static void NotesTab(string? name, string? world, string? profileId)
    {
        if (name == null || world == null) return;
        var w = ImGui.GetContentRegionAvail().X; var halfH = (ImGui.GetContentRegionAvail().Y - 120) / 2;
        DrawNoteField("Profile Notes", "Tied to this RPHub profile", profileId ?? $"{name}@{world}", ref _profileNote, ref _profileNoteDirty, w, halfH);
        UI.Space(UI.Sm);
        DrawNoteField("Character Notes", "Tied to character name & world", $"{name}@{world}", ref _charNote, ref _charNoteDirty, w, halfH);
        UI.Space(UI.Sm);
        var canSave = _profileNoteDirty || _charNoteDirty;
        if (!canSave) ImGui.BeginDisabled();
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.PrimaryButtonBg); ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.PrimaryButtonHover); ImGui.PushStyleColor(ImGuiCol.Text, Theme.PrimaryButtonText);
        if (ImGui.Button("Save All", new Vector2(90, 26))) { if (_profileNoteDirty) { Globals.Notes.Set(profileId ?? $"{name}@{world}", _profileNote); _profileNoteDirty = false; } if (_charNoteDirty) { Globals.Notes.Set($"{name}@{world}", _charNote); _charNoteDirty = false; } Sound.PlaySuccess(); }
        ImGui.PopStyleColor(3); if (!canSave) ImGui.EndDisabled();
        ImGui.SameLine(); if (!canSave) ImGui.BeginDisabled();
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.ButtonBg); ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.Error with { W = 0.4f }); ImGui.PushStyleColor(ImGuiCol.Text, Theme.LabelColor);
        if (ImGui.Button("Discard", new Vector2(80, 26))) { _profileNote = Globals.Notes.Get(profileId ?? $"{name}@{world}"); _charNote = Globals.Notes.Get($"{name}@{world}"); _profileNoteDirty = _charNoteDirty = false;  Sound.PlayCancel();}
        ImGui.PopStyleColor(3); if (!canSave) ImGui.EndDisabled();
    }

    static void DrawNoteField(string label, string hint, string key, ref string note, ref bool dirty, float w, float h)
    {
        var startY = ImGui.GetCursorPosY();
        ImGui.PushFont(UiBuilder.IconFont); ImGui.TextColored(Theme.LabelColorDim, FontAwesomeIcon.StickyNote.ToIconString()); ImGui.PopFont();
        ImGui.SameLine(0, UI.Sm); ImGui.TextColored(Theme.LabelColor, label); ImGui.SameLine(); ImGui.TextColored(Theme.TextMuted, $"- {hint}");
        if (dirty) { var statusText = "(unsaved)"; ImGui.SameLine(w - ImGui.CalcTextSize(statusText).X); ImGui.TextColored(Theme.Warning, statusText); }
        ImGui.SetCursorPosY(startY + ImGui.GetTextLineHeightWithSpacing() + UI.Xs);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.ButtonBg with { W = 0.5f }); ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(UI.Sm));
        if (ImGui.InputTextMultiline($"##{key}", ref note, 4096, new Vector2(w, h), ImGuiInputTextFlags.AllowTabInput)) dirty = true;
        ImGui.PopStyleVar(); ImGui.PopStyleColor();
    }

    static void Section(string t) { ImGui.TextColored(Theme.Primary, t.ToUpperInvariant()); ImGui.Separator(); UI.Space(UI.Xs); }
    static void Wrapped(string t, Vector4? col = null) { ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X); ImGui.TextColored(col ?? Theme.ValueColor, t); ImGui.PopTextWrapPos(); }
    static void IconText(FontAwesomeIcon i, Vector4 col, string t) { ImGui.PushFont(UiBuilder.IconFont); ImGui.TextColored(Theme.LabelColorDim, i.ToIconString()); ImGui.PopFont(); ImGui.SameLine(0, UI.Sm); ImGui.TextColored(col, t); }

    static void Empty(string t, string s)
    {
        UI.Space(ImGui.GetContentRegionAvail().Y * 0.3f);
        Icon(ImGui.GetWindowDrawList(), ImGui.GetCursorScreenPos() + new Vector2(ImGui.GetContentRegionAvail().X / 2, 0), FontAwesomeIcon.UserSlash, Theme.LabelColorDim, 2.5f);
        UI.Space(40); Theme.Centered(t, Theme.LabelColor); Theme.Centered(s, Theme.TextMuted);
    }

    static void Icon(ImDrawListPtr dl, Vector2 c, FontAwesomeIcon i, Vector4 col, float scale = 1f)
    {
        ImGui.PushFont(UiBuilder.IconFont); var sz = ImGui.GetFontSize() * scale; var txt = i.ToIconString();
        ImGui.SetWindowFontScale(scale); var s = ImGui.CalcTextSize(txt); ImGui.SetWindowFontScale(1);
        dl.AddText(ImGui.GetFont(), sz, c - s / 2, Theme.Col(col), txt); ImGui.PopFont();
    }

    static bool IconBtn(ImDrawListPtr dl, Vector2 p, FontAwesomeIcon i, string tip)
    {
        ImGui.SetCursorScreenPos(p); var c = ImGui.InvisibleButton($"##{tip}", new Vector2(24)); var h = ImGui.IsItemHovered();
        if (h) { dl.AddRectFilled(p, p + new Vector2(24), Theme.Col(Theme.ButtonHovered), 4); ImGui.SetMouseCursor(ImGuiMouseCursor.Hand); ImGui.SetTooltip(tip); }
        Icon(dl, p + new Vector2(12), i, h ? Theme.GoldColor : Theme.LabelColor); return c;
    }

    static void Refresh(string? name, string? world)
    {
        if (name == null || world == null) return;
        if (name == _targetName && world == _targetWorld) { _targetLoading = true; _ = Globals.Cache.RefreshProfileAsync(name, world).ContinueWith(t => { _targetLoading = false; if (t.Result?.Data != null) _targetData = t.Result.Data; }); }
        else { _loading = true; _ = Globals.Cache.RefreshProfileAsync(name, world).ContinueWith(t => { _loading = false; if (t.Result?.Data != null) _data = t.Result.Data; }); }
    }

     
}

