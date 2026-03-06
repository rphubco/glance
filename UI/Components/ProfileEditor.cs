namespace Glance.UI;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Glance.Utils;
using Glance.Core;
using Glance.Models;
using Glance.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

public static class ProfileEditor
{
    static bool _active, _createMode;
    static int _raceIdx, _clanIdx;
    static readonly FileDialogManager _fileDialogManager = new();
    static IDalamudTextureWrap? _pendingTex;
    static byte[]? _loadedImgBytes;
    static string _imageErr = "";
    static string _saveErr = "";
    static int _tab;
    static int _editingGlanceIdx = -1;
    public static bool IsActive => _active;

    static string _currentTab = "";
    static HashSet<uint> _iconExistsCache = new();
    static readonly Dictionary<string, List<uint>> _tabCache = new();
    static List<(int Start, int End)> _iconRanges = new();
    static bool _iconCacheBuilt = false;

    public static void OpenForEdit(ProfileData? data, string? profileId)
    {
        _createMode = false;
        _active = true;
        _tab = 0;
        Cleanup();
        Globals.ProfileEdit.Load(data, profileId);
        SetupRaceClan();
        Sound.PlayOpen();
    }

    public static void OpenForCreate()
    {
        _createMode = true;
        _active = true;
        _tab = 0;
        Cleanup();
        Globals.ProfileEdit.StartCreate();
        SetupRaceClan();
        Sound.PlayConfirm();
    }

    public static void Close()
    {
        _active = false;
        Cleanup();
    }

    static void Cleanup()
    {
        _pendingTex?.Dispose(); _pendingTex = null; _loadedImgBytes = null; _imageErr = ""; _saveErr = "";
        _raceIdx = _clanIdx = 0;
    }

    static void SetupRaceClan()
    {
        var d = Globals.ProfileEdit.Draft;
        if (d == null) return;
        _raceIdx = Array.IndexOf(RaceData.Races, d.Race);
        if (_raceIdx < 0 && !string.IsNullOrEmpty(d.Race))
            _raceIdx = Array.IndexOf(RaceData.Races, RaceData.CustomRace);

        if (_raceIdx < 0) _raceIdx = 0;
        if (RaceData.Clans.TryGetValue(RaceData.Races[_raceIdx], out var clans))
        {
            _clanIdx = Array.IndexOf(clans, d.Clan);
            if (_clanIdx < 0 && !string.IsNullOrEmpty(d.Clan))
                _clanIdx = Array.IndexOf(clans, RaceData.CustomRace);

            if (_clanIdx < 0) _clanIdx = 0;
        }
    }

    public static void Draw(string? name, string? world, Action? onSaved = null, Action? onCancelled = null)
    {
        if (!_active) return;
        var edit = Globals.ProfileEdit;
        if (!edit.IsLoaded) { _active = false; return; }

        _fileDialogManager.Draw();

        if (!_createMode) edit.AutoSave();

        DrawHeader(edit, name, world, onSaved, onCancelled);

        if (edit.Errors.Count > 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Error);
            foreach (var e in edit.Errors) ImGui.TextWrapped($"• {e}");
            ImGui.PopStyleColor();
            UI.Space();
        }

        DrawTabs();
        UI.Space();

        if (ImGui.BeginChild("##editscroll", new Vector2(-1, -1)))
        {
            if (_tab == 0) DrawGeneral(edit);
            else if (_tab == 1) DrawAbout(edit);
            else DrawHooks(edit);
        }
        ImGui.EndChild();
    }

    static void DrawHeader(ProfileEditService edit, string? name, string? world, Action? onSaved, Action? onCancelled)
    {
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        var w = ImGui.GetContentRegionAvail().X;
        const float h = 50;

        dl.AddRectFilled(p, p + new Vector2(w, h), Theme.Col(Theme.FrameBorderInner with { W = 0.15f }), 6);

        ImGui.SetCursorScreenPos(p + new Vector2(12, 12));
        using (Globals.Fonts.Header.Push()) ImGui.TextColored(Theme.GoldColor, _createMode ? "Create Profile" : "Editing Profile");

        var btnX = p.X + w - 12;

        ImGui.SetCursorScreenPos(new Vector2(btnX - 70, p.Y + 10));
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.ButtonBg);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.Error with { W = 0.4f });
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.LabelColor);
        if (ImGui.Button("Cancel", new Vector2(70, 30)))
        {
            edit.Discard();
            Cleanup();
            _active = false;
            onCancelled?.Invoke();
        }
        ImGui.PopStyleColor(3);

        ImGui.SetCursorScreenPos(new Vector2(btnX - 150, p.Y + 10));
        var dirty = edit.IsDirty || _createMode;
        if (!dirty) ImGui.BeginDisabled();
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.PrimaryButtonBg);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.PrimaryButtonHover);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.PrimaryButtonText);
        if (ImGui.Button(edit.IsSaving ? "Saving..." : (_createMode ? "Publish" : "Save"), new Vector2(70, 30)) && !edit.IsSaving)
        {
            _saveErr = "";
            _ = Task.Run(async () =>
            {
                var (ok, err) = await edit.SaveAsync();
                if (ok)
                {
                    if (name != null && world != null)
                        await Globals.Cache.RefreshProfileAsync(name, world);
                    _active = false;
                    Cleanup();
                    onSaved?.Invoke();
                    Sound.PlaySuccess();
                }
                else
                {
                    _saveErr = err ?? "Save failed";
                    Sound.PlayError();
                }
            });
        }
        ImGui.PopStyleColor(3);
        if (!dirty) ImGui.EndDisabled();

        if (!string.IsNullOrEmpty(_saveErr))
        {
            ImGui.SetCursorScreenPos(new Vector2(p.X + 12, p.Y + 35));
            ImGui.TextColored(Theme.Error, _saveErr);
        }

        ImGui.SetCursorScreenPos(p + new Vector2(0, h + UI.Sm));
    }

    static void DrawTabs()
    {
        var w = ImGui.GetContentRegionAvail().X;
        string[] tabs = ["General", "About", "Hooks"];
        var tw = (w - UI.Xs * 2) / 3;

        for (var i = 0; i < tabs.Length; i++)
        {
            if (i > 0) ImGui.SameLine(0, UI.Xs);
            var sel = _tab == i;
            ImGui.PushStyleColor(ImGuiCol.Button, sel ? Theme.ButtonActive : Theme.ButtonBg);
            ImGui.PushStyleColor(ImGuiCol.Text, sel ? Theme.GoldColor : Theme.LabelColor);
            if (ImGui.Button(tabs[i], new Vector2(tw, 28)))
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

    static void DrawGeneral(ProfileEditService edit)
    {
        var draft = edit.Draft;
        if (draft == null) return;

        var w = ImGui.GetContentRegionAvail().X;

        ImGui.BeginGroup();
        ImGui.TextColored(Theme.LabelColor, "Profile Image");
        UI.Space(UI.Xs);

        var imgP = ImGui.GetCursorScreenPos();
        const float imgSz = 120;
        var dl = ImGui.GetWindowDrawList();

        if (edit.HasPendingImage && edit.PendingImageData != null && _loadedImgBytes != edit.PendingImageData)
        {
            _pendingTex?.Dispose();
            try { _pendingTex = Globals.TextureProvider.CreateFromImageAsync(edit.PendingImageData).Result; _loadedImgBytes = edit.PendingImageData; }
            catch { _pendingTex = null; }
        }

        if (edit.HasPendingImage && _pendingTex != null)
        {
            var (tw, th) = ((float)_pendingTex.Width, (float)_pendingTex.Height);
            var r = 1f; var tr = tw / th;
            var (u0, u1) = tr > r
                ? (new Vector2((tw - th * r) / 2 / tw, 0), new Vector2(1 - (tw - th * r) / 2 / tw, 1))
                : (new Vector2(0, (th - tw / r) / 2 / th), new Vector2(1, 1 - (th - tw / r) / 2 / th));
            dl.AddImageRounded(_pendingTex.Handle, imgP, imgP + new Vector2(imgSz), u0, u1, 0xFFFFFFFF, 6);
            dl.AddRect(imgP, imgP + new Vector2(imgSz), Theme.Col(Theme.Success with { W = 0.8f }), 6);
        }
        else
        {
            var tex = Globals.Images.Get(draft.PageImage);
            if (tex != null)
            {
                var (tw, th) = ((float)tex.Width, (float)tex.Height);
                var r = 1f; var tr = tw / th;
                var (u0, u1) = tr > r
                    ? (new Vector2((tw - th * r) / 2 / tw, 0), new Vector2(1 - (tw - th * r) / 2 / tw, 1))
                    : (new Vector2(0, (th - tw / r) / 2 / th), new Vector2(1, 1 - (th - tw / r) / 2 / th));
                dl.AddImageRounded(tex.Handle, imgP, imgP + new Vector2(imgSz), u0, u1, 0xFFFFFFFF, 6);
            }
            else dl.AddRectFilled(imgP, imgP + new Vector2(imgSz), Theme.Col(Theme.ButtonBg), 6);
            dl.AddRect(imgP, imgP + new Vector2(imgSz), Theme.Col(Theme.FrameBorderInner with { W = 0.6f }), 6);
        }

        ImGui.SetCursorScreenPos(imgP + new Vector2(0, imgSz + UI.Sm));

        ImGui.PushStyleColor(ImGuiCol.Button, Theme.ButtonBg);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.LabelColor);
        if (ImGui.Button("Change Image", new Vector2(imgSz, 24)))
        {
            _fileDialogManager.OpenFileDialog(
                "Select Portrait",
                ".png,.jpg,.jpeg,.webp,.gif",
                (bool ok, string path) =>
                {
                    if (!ok || string.IsNullOrEmpty(path)) return;
                    try
                    {
                        if (File.Exists(path))
                        {
                            var (success, err) = edit.StageImage(File.ReadAllBytes(path), Path.GetFileName(path));
                            _imageErr = success ? "" : (err ?? "Failed");
                        }
                    }
                    catch (Exception ex) { _imageErr = ex.Message; }
                }
            );
        }
        ImGui.PopStyleColor(2);

        if (edit.HasPendingImage)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.Error with { W = 0.3f });
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Error);
            if (ImGui.Button("Clear##img", new Vector2(imgSz, 20))) { edit.ClearPendingImage(); _pendingTex?.Dispose(); _pendingTex = null; _loadedImgBytes = null; }
            ImGui.PopStyleColor(2);
        }

        if (!string.IsNullOrEmpty(_imageErr))
        {
            ImGui.PushTextWrapPos(imgSz);
            ImGui.TextColored(Theme.Error, _imageErr);
            ImGui.PopTextWrapPos();
        }

        ImGui.EndGroup();
        ImGui.SameLine(0, UI.Lg);

        ImGui.BeginGroup();
        var fieldW = w - imgSz - UI.Lg - 12;

        Field("Name", draft, d => d.Name, (d, v) => d.Name = v, 30, fieldW);
        Field("Description", draft, d => d.Description, (d, v) => d.Description = v, 30, fieldW);

        var startY = ImGui.GetCursorPosY();
        ImGui.BeginGroup();
        ImGui.TextColored(Theme.LabelColor, "Race");
        ImGui.SetNextItemWidth(fieldW * 0.5f);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.ButtonBg with { W = 0.5f });
        if (ImGui.Combo("##race", ref _raceIdx, RaceData.Races, RaceData.Races.Length))
        {
            draft.Race = RaceData.Races[_raceIdx];
            _clanIdx = 0;
            if (RaceData.Clans.TryGetValue(draft.Race, out var c) && c.Length > 0)
                draft.Clan = c[0];
        }
        ImGui.PopStyleColor();

        if (!RaceData.IsStandardRace(draft.Race))
        {
            UI.Space(UI.Xs);
            ImGui.SetNextItemWidth(fieldW * 0.5f);
            var customRace = (draft.Race == RaceData.CustomRace) ? "" : draft.Race;
            if (ImGui.InputTextWithHint("##customRace", "Enter Custom Race...", ref customRace, 32))
                draft.Race = customRace;
        }
        ImGui.EndGroup();

        ImGui.SameLine(0, UI.Lg);
        ImGui.SetCursorPosY(startY);

        ImGui.BeginGroup();
        ImGui.TextColored(Theme.LabelColor, "Clan");
        ImGui.SetNextItemWidth(fieldW * 0.35f);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.ButtonBg with { W = 0.5f });

        if (_raceIdx >= 0 && _raceIdx < RaceData.Races.Length && RaceData.Clans.TryGetValue(RaceData.Races[_raceIdx], out var clans))
        {
            if (ImGui.Combo("##clan", ref _clanIdx, clans, clans.Length))
                draft.Clan = clans[_clanIdx];

            ImGui.PopStyleColor();

            if (draft.Clan == RaceData.CustomRace || (!clans.Contains(draft.Clan) && !string.IsNullOrEmpty(draft.Clan)))
            {
                UI.Space(UI.Xs);
                ImGui.SetNextItemWidth(fieldW * 0.35f);
                var customClan = (draft.Clan == RaceData.CustomRace) ? "" : draft.Clan;
                if (ImGui.InputTextWithHint("##customClan", "Enter Custom Clan...", ref customClan, 32))
                    draft.Clan = customClan;
            }
        }
        else
        {
            ImGui.PopStyleColor();
        }
        ImGui.EndGroup();

        Field("Free Company", draft, d => d.FreeCompany, (d, v) => d.FreeCompany = v, 30, fieldW);
        ImGui.EndGroup();

        UI.Space();
        ImGui.Separator();
        UI.Space();

        ImGui.TextColored(Theme.LabelColor, "At A Glance");
        ImGui.TextColored(Theme.TextMuted, "Quick info icons shown on your profile");
        UI.Space(UI.Sm);

        DrawGlanceEditor(draft, w);

        UI.Space();
        ImGui.Separator();
        UI.Space();

        Field("Location", draft, d => d.Location, (d, v) => d.Location = v, 50, w);

        UI.Space();

        ImGui.TextColored(Theme.LabelColor, "Details (Auto-wrapping is not supported in Dalamud, use new lines.)");
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.ButtonBg with { W = 0.5f });
        var details = draft.Details ?? "";
        if (ImGui.InputTextMultiline("##details", ref details, 500, new Vector2(w, 100), ImGuiInputTextFlags.None)) draft.Details = details;
        ImGui.PopStyleColor();

        UI.Space();

        ImGui.TextColored(Theme.LabelColor, "Current Status (IC)");
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.ButtonBg with { W = 0.5f });
        var status = draft.CurrentStatus ?? "";
        if (ImGui.InputTextMultiline("##status", ref status, 150, new Vector2(w, 60))) draft.CurrentStatus = status;
        ImGui.PopStyleColor();

        UI.Space();

        ImGui.TextColored(Theme.LabelColor, "Player Notes (OOC)");
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.ButtonBg with { W = 0.5f });
        var pnotes = draft.PlayerNotes ?? "";
        if (ImGui.InputTextMultiline("##ooc", ref pnotes, 250, new Vector2(w, 60))) draft.PlayerNotes = pnotes;
        ImGui.PopStyleColor();

        UI.Space();
    }

    static void DrawGlanceEditor(ProfileEditPayload d, float availWidth)
    {
        d.Glances ??= [];
        while (d.Glances.Count < 5) d.Glances.Add(new GlanceData());

        var dl = ImGui.GetWindowDrawList();
        const float boxSize = 48f;
        const float spacing = 12f;
        var totalW = 5 * boxSize + 4 * spacing;
        var startX = (availWidth - totalW) / 2;

        var basePos = ImGui.GetCursorScreenPos() + new Vector2(startX, 0);
        ImGui.Dummy(new Vector2(availWidth, boxSize + UI.Sm));

        for (var i = 0; i < 5; i++)
        {
            var glance = d.Glances[i];
            var hasData = glance.IconId > 0 && !string.IsNullOrEmpty(glance.Label);
            var pos = basePos + new Vector2(i * (boxSize + spacing), 0);
            var max = pos + new Vector2(boxSize);

            dl.AddRectFilled(pos, max, Theme.Col(Theme.ButtonBg), 6);
            dl.AddRect(pos, max, Theme.Col(Theme.FrameBorder), 6);

            if (glance.IconId > 0)
            {
                var tex = Globals.TextureProvider.GetFromGameIcon(new Dalamud.Interface.Textures.GameIconLookup(glance.IconId));
                if (tex.TryGetWrap(out var wrap, out _))
                {
                    var iconSz = new Vector2(boxSize - 8);
                    dl.AddImage(wrap.Handle, pos + new Vector2(4), pos + new Vector2(4) + iconSz);
                }
            }
            else
            {
                ImGui.PushFont(UiBuilder.IconFont);
                var txt = FontAwesomeIcon.Plus.ToIconString();
                var tsz = ImGui.CalcTextSize(txt);
                dl.AddText(pos + (new Vector2(boxSize) - tsz) / 2, Theme.Col(Theme.LabelColorDim), txt);
                ImGui.PopFont();
            }

            var overlayPos = new Vector2(max.X - 16, max.Y - 16);
            dl.AddRectFilled(overlayPos, overlayPos + new Vector2(14), Theme.Col(Theme.Primary), 3);
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.SetWindowFontScale(0.6f);
            var editTxt = FontAwesomeIcon.Pen.ToIconString();
            var editSz = ImGui.CalcTextSize(editTxt);
            dl.AddText(overlayPos + (new Vector2(14) - editSz) / 2, 0xFFFFFFFF, editTxt);
            ImGui.SetWindowFontScale(1f);
            ImGui.PopFont();

            ImGui.SetCursorScreenPos(pos);
            if (ImGui.InvisibleButton($"##glanceedit{i}", new Vector2(boxSize)))
            {
                _editingGlanceIdx = i;
                ImGui.OpenPopup("GlanceEditPopup");
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                dl.AddRect(pos, max, Theme.Col(Theme.GoldColor), 6, ImDrawFlags.None, 2);

                if (hasData)
                {
                    ImGui.BeginTooltip();
                    ImGui.PushTextWrapPos(250);
                    ImGui.TextColored(Theme.GoldColor, glance.Label);
                    if (!string.IsNullOrEmpty(glance.Value))
                    {
                        ImGui.Spacing();
                        ImGui.TextColored(Theme.ValueColor, glance.Value);
                    }
                    ImGui.PopTextWrapPos();
                    ImGui.EndTooltip();
                }
            }
        }

        DrawGlancePopup(d);
    }

    static void DrawGlancePopup(ProfileEditPayload d)
    {
        ImGui.SetNextWindowSize(new Vector2(400, 500), ImGuiCond.Always);

        var isOpen = ImGui.BeginPopup("GlanceEditPopup");

        if (!isOpen)
        {
            if (_editingGlanceIdx >= 0 && d.Glances != null && _editingGlanceIdx < d.Glances.Count)
            {
                var g = d.Glances[_editingGlanceIdx];
                if (g.IconId > 0 && string.IsNullOrEmpty(g.Label))
                    g.Label = "Glance";
            }
            _editingGlanceIdx = -1;
            return;
        }

        if (_editingGlanceIdx < 0 || d.Glances == null || _editingGlanceIdx >= d.Glances.Count)
        {
            ImGui.EndPopup();
            return;
        }

        var glance = d.Glances[_editingGlanceIdx];
        var dl = ImGui.GetWindowDrawList();
        var contentWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;

        ImGui.TextColored(Theme.GoldColor, $"Edit Glance {_editingGlanceIdx + 1}");
        ImGui.Separator();
        UI.Space(UI.Sm);

        var previewPos = ImGui.GetCursorScreenPos();
        const float previewSize = 48f;
        dl.AddRectFilled(previewPos, previewPos + new Vector2(previewSize), Theme.Col(Theme.ButtonBg), 6);
        dl.AddRect(previewPos, previewPos + new Vector2(previewSize), Theme.Col(Theme.FrameBorder), 6);

        if (glance.IconId > 0)
        {
            var tex = Globals.TextureProvider.GetFromGameIcon(new Dalamud.Interface.Textures.GameIconLookup(glance.IconId));
            if (tex.TryGetWrap(out var wrap, out _))
                dl.AddImage(wrap.Handle, previewPos + new Vector2(4), previewPos + new Vector2(previewSize - 4));
        }

        ImGui.SetCursorScreenPos(previewPos + new Vector2(previewSize + UI.Md, 0));
        ImGui.BeginGroup();
        ImGui.Text("Title");
        ImGui.SetNextItemWidth(contentWidth - previewSize - UI.Md - 8);
        var label = glance.Label ?? "";
        if (ImGui.InputText("##glancelabel", ref label, 32)) glance.Label = label;
        ImGui.EndGroup();

        ImGui.SetCursorScreenPos(previewPos + new Vector2(0, previewSize + UI.Sm));

        ImGui.Text("Description");
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        var value = glance.Value ?? "";
        if (ImGui.InputTextMultiline("##glancevalue", ref value, 256, new Vector2(contentWidth, 50)))
            glance.Value = value;

        UI.Space(UI.Sm);
        ImGui.Separator();
        UI.Space(UI.Sm);

        DrawIconTabs(glance, 36f, contentWidth);

        UI.Space(UI.Sm);

        if (glance.IconId > 0 || !string.IsNullOrEmpty(glance.Label))
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.Error with { W = 0.6f });
            if (ImGui.Button("Clear", new Vector2(80, 26)))
            {
                glance.IconId = 0;
                glance.Label = null;
                glance.Value = null;
            }
            ImGui.PopStyleColor();
            ImGui.SameLine();
        }

        ImGui.SetCursorPosX(contentWidth - 60 + ImGui.GetStyle().WindowPadding.X);
        if (ImGui.Button("Done", new Vector2(60, 26))) ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }

    static bool IconExists(uint iconId)
    {
        if (_iconExistsCache.Contains(iconId))
            return true;

        try
        {
            var tex = Globals.TextureProvider.GetFromGameIcon(new Dalamud.Interface.Textures.GameIconLookup(iconId));
            if (tex.TryGetWrap(out var wrap, out _) && wrap != null && wrap.Handle != nint.Zero)
            {
                _iconExistsCache.Add(iconId);
                return true;
            }
        }
        catch { }
        return false;
    }

    static void DrawIconTabs(GlanceData glance, float iconSize, float contentWidth)
    {
        if (ImGui.BeginTabBar("IconTabs", ImGuiTabBarFlags.NoTooltip))
        {
            if (ImGui.BeginTabItem(" ★ "))
            {
                _currentTab = "Main";
                _iconRanges.Clear();
                _iconRanges.Add((0, 100));
                _iconRanges.Add((62000, 62600));
                _iconRanges.Add((62800, 62900));
                _iconRanges.Add((66000, 66400));
                _iconRanges.Add((90000, 100000));
                DrawIconGrid(glance, iconSize, contentWidth);
                ImGui.EndTabItem();
                _tabCache.Remove(_currentTab);
            }

            if (ImGui.BeginTabItem("Misc"))
            {
                _currentTab = "Misc";
                _iconRanges.Clear();
                _iconRanges.Add((60000, 61000));
                _iconRanges.Add((61200, 61250));
                _iconRanges.Add((61290, 62000));
                _iconRanges.Add((63900, 64000));
                _iconRanges.Add((65000, 65900));
                DrawIconGrid(glance, iconSize, contentWidth);
                ImGui.EndTabItem();
                _tabCache.Remove(_currentTab);
            }

            if (ImGui.BeginTabItem("Actions"))
            {
                _currentTab = "Actions";
                _iconRanges.Clear();
                _iconRanges.Add((100, 4000));
                _iconRanges.Add((5100, 8000));
                _iconRanges.Add((8000, 10000));
                _iconRanges.Add((19800, 20000));
                DrawIconGrid(glance, iconSize, contentWidth);
                _tabCache.Remove(_currentTab);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Mounts"))
            {
                _currentTab = "Mounts";
                _iconRanges.Clear();
                _iconRanges.Add((4000, 4400));
                _iconRanges.Add((4400, 5100));
                _iconRanges.Add((59000, 60000));
                _iconRanges.Add((68000, 69000));
                DrawIconGrid(glance, iconSize, contentWidth);
                _tabCache.Remove(_currentTab);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Items"))
            {
                _currentTab = "Items";
                _iconRanges.Clear();
                _iconRanges.Add((20000, 30000));
                _iconRanges.Add((30000, 40000));
                _iconRanges.Add((50000, 54000));
                DrawIconGrid(glance, iconSize, contentWidth);
                _tabCache.Remove(_currentTab);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Status"))
            {
                _currentTab = "Status";
                _iconRanges.Clear();
                _iconRanges.Add((210000, 220000));
                DrawIconGrid(glance, iconSize, contentWidth);
                _tabCache.Remove(_currentTab);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    static void DrawIconGrid(GlanceData glance, float iconSize, float contentWidth)
    {
        if (!_tabCache.TryGetValue(_currentTab, out var cache))
        {
            cache = new List<uint>();
            foreach (var (start, end) in _iconRanges)
            {
                for (var i = start; i < end && cache.Count < 2000; i++)
                {
                    if (IconExists((uint)i))
                        cache.Add((uint)i);
                }
            }
            _tabCache[_currentTab] = cache;
        }

        if (cache.Count == 0)
        {
            ImGui.TextColored(Theme.TextMuted, "No icons found in this category");
            return;
        }

        var childHeight = 180f;
        if (ImGui.BeginChild($"{_currentTab}##IconScroll", new Vector2(contentWidth, childHeight), true))
        {
            var spacing = ImGui.GetStyle().ItemSpacing.X;
            var columns = Math.Max(1, (int)((contentWidth - 16) / (iconSize + spacing)));

            ImGuiListClipperPtr clipper;
            unsafe { clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper()); }

            var rows = (cache.Count + columns - 1) / columns;
            clipper.Begin(rows, iconSize + ImGui.GetStyle().ItemSpacing.Y);

            while (clipper.Step())
            {
                for (var row = clipper.DisplayStart; row < clipper.DisplayEnd; row++)
                {
                    for (var col = 0; col < columns; col++)
                    {
                        var idx = row * columns + col;
                        if (idx >= cache.Count) break;

                        if (col > 0) ImGui.SameLine();
                        DrawSingleIcon(cache[idx], glance, iconSize);
                    }
                }
            }

            clipper.Destroy();
        }
        ImGui.EndChild();
    }

    static void DrawSingleIcon(uint iconId, GlanceData glance, float size)
    {
        var pos = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        var selected = glance.IconId == iconId;

        if (ImGui.InvisibleButton($"##icon_{iconId}", new Vector2(size)))
            glance.IconId = iconId;

        if (selected)
            dl.AddRectFilled(pos, pos + new Vector2(size), Theme.Col(Theme.GoldColor with { W = 0.3f }), 4);

        try
        {
            var tex = Globals.TextureProvider.GetFromGameIcon(new Dalamud.Interface.Textures.GameIconLookup(iconId));
            if (tex.TryGetWrap(out var wrap, out _) && wrap != null)
                dl.AddImage(wrap.Handle, pos + new Vector2(2), pos + new Vector2(size - 2));
        }
        catch
        {
            dl.AddRectFilled(pos, pos + new Vector2(size), Theme.Col(Theme.ButtonBg), 4);
        }

        if (selected)
            dl.AddRect(pos, pos + new Vector2(size), Theme.Col(Theme.GoldColor), 4, ImDrawFlags.None, 2);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"{iconId}");
    }

    static void DrawAbout(ProfileEditService edit)
    {
        var draft = edit.Draft;
        if (draft == null) return;

        var w = ImGui.GetContentRegionAvail().X;

        ImGui.TextColored(Theme.TextMuted, "Add custom fields to show info about your character");
        UI.Space();

        for (var i = 0; i < draft.About.Count; i++)
        {
            var a = draft.About[i];
            var dl = ImGui.GetWindowDrawList();
            var p = ImGui.GetCursorScreenPos();

            dl.AddRectFilled(p, p + new Vector2(w, 60), Theme.Col(Theme.ButtonBg with { W = 0.3f }), 4);

            ImGui.BeginGroup();
            ImGui.SetCursorScreenPos(p + new Vector2(8, 8));

            ImGui.SetNextItemWidth(120);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.ButtonBg);
            var lbl = a.Label ?? "";
            if (ImGui.InputText($"##lbl{i}", ref lbl, 50)) a.Label = lbl;
            ImGui.PopStyleColor();

            ImGui.SameLine();
            ImGui.SetNextItemWidth(w - 200);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.ButtonBg);
            var inp = a.Input ?? "";
            if (ImGui.InputText($"##inp{i}", ref inp, 100)) a.Input = inp;
            ImGui.PopStyleColor();

            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.Error with { W = 0.3f });
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Error);
            if (ImGui.Button($"X##{i}", new Vector2(24, 24))) { edit.RemoveAbout(i); ImGui.EndGroup(); ImGui.SetCursorScreenPos(p + new Vector2(0, 64)); continue; }
            ImGui.PopStyleColor(2);

            ImGui.EndGroup();
            ImGui.SetCursorScreenPos(p + new Vector2(0, 64));
        }

        UI.Space();
        if (draft.About.Count < 20)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.ButtonBg);
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Success);
            if (ImGui.Button("+ Add Field", new Vector2(100, 26))) edit.AddAbout();
            ImGui.PopStyleColor(2);
        }
    }

    static void DrawHooks(ProfileEditService edit)
    {
        var draft = edit.Draft;
        if (draft == null) return;

        var w = ImGui.GetContentRegionAvail().X;

        ImGui.TextColored(Theme.TextMuted, "RP hooks are conversation starters others can use");
        UI.Space();

        for (var i = 0; i < draft.Hooks.Count; i++)
        {
            var h = draft.Hooks[i];
            var dl = ImGui.GetWindowDrawList();
            var p = ImGui.GetCursorScreenPos();

            dl.AddRectFilled(p, p + new Vector2(w, 100), Theme.Col(Theme.ButtonBg with { W = 0.3f }), 4);
            dl.AddRectFilled(p, p + new Vector2(3, 100), Theme.Col(Theme.GoldColor with { W = 0.6f }), 4, ImDrawFlags.RoundCornersLeft);

            ImGui.BeginGroup();
            ImGui.SetCursorScreenPos(p + new Vector2(12, 8));

            ImGui.TextColored(Theme.LabelColorDim, "Title");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(w - 100);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.ButtonBg);
            var title = h.Title ?? "";
            if (ImGui.InputText($"##htitle{i}", ref title, 50)) h.Title = title;
            ImGui.PopStyleColor();

            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.Error with { W = 0.3f });
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Error);
            if (ImGui.Button($"X##h{i}", new Vector2(24, 24))) { edit.RemoveHook(i); ImGui.EndGroup(); ImGui.SetCursorScreenPos(p + new Vector2(0, 108)); continue; }
            ImGui.PopStyleColor(2);

            ImGui.SetCursorScreenPos(p + new Vector2(12, 36));
            ImGui.TextColored(Theme.LabelColorDim, "Description");
            ImGui.SetCursorScreenPos(p + new Vector2(12, 52));
            ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.ButtonBg);
            var desc = h.Description ?? "";
            if (ImGui.InputTextMultiline($"##hdesc{i}", ref desc, 300, new Vector2(w - 24, 40))) h.Description = desc;
            ImGui.PopStyleColor();

            ImGui.EndGroup();
            ImGui.SetCursorScreenPos(p + new Vector2(0, 108));
        }

        UI.Space();
        if (draft.Hooks.Count < 10)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.ButtonBg);
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Success);
            if (ImGui.Button("+ Add Hook", new Vector2(100, 26))) edit.AddHook();
            ImGui.PopStyleColor(2);
        }
    }

    static void Field<T>(string label, T obj, Func<T, string?> getter, Action<T, string> setter, int max, float w)
    {
        ImGui.TextColored(Theme.LabelColor, label);
        ImGui.SetNextItemWidth(w);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.ButtonBg with { W = 0.5f });
        var v = getter(obj) ?? "";
        if (ImGui.InputText($"##{label}", ref v, (int)max)) setter(obj, v);
        ImGui.PopStyleColor();
    }
}
