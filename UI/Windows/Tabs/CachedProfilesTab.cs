namespace Glance.UI.Tabs;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Glance.Core;
using Glance.Models;
using Glance.Services;
using Glance.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

public static class CachedProfilesTab
{
    static List<CachedEntry>? _entries;
    static DateTime _lastRefresh;
    static string _search = "";

    const float CardHeight = 62f;
    const float ImageSize = 46f;
    const float CardSpacing = 4f;

    public static void Draw()
    {
        if ((DateTime.UtcNow - _lastRefresh).TotalSeconds > 3 || _entries == null)
            RefreshList();

        DrawHeader();
        UI.Space(UI.Sm);
        DrawSearchBar();
        UI.Space(UI.Sm);

        if (_entries == null || _entries.Count == 0)
        {
            DrawEmpty();
            return;
        }

        var filtered = string.IsNullOrWhiteSpace(_search)
            ? _entries
            : _entries.Where(e =>
                (e.DisplayName ?? e.Name).Contains(_search, StringComparison.OrdinalIgnoreCase) ||
                e.World.Contains(_search, StringComparison.OrdinalIgnoreCase)).ToList();

        if (filtered.Count == 0)
        {
            DrawEmpty("No Results", "No cached profiles match your search");
            return;
        }

        using var child = ImRaii.Child("##cachedlist", new Vector2(-1, -1));
        if (child)
        {
            unsafe
            {
                ImGuiListClipper clipper;
                ImGuiListClipperPtr clipperPtr = new ImGuiListClipperPtr(&clipper);

                clipperPtr.Begin(filtered.Count, CardHeight + CardSpacing);

                while (clipperPtr.Step())
                {
                    for (int i = clipperPtr.DisplayStart; i < clipperPtr.DisplayEnd; i++)
                    {
                        if (i < 0 || i >= filtered.Count) continue;

                        DrawCard(filtered[i]);
                        UI.Space(CardSpacing);
                    }
                }

                clipperPtr.End();
            }
        }
    }

    static void DrawHeader()
    {
        using (Globals.Fonts.Header.Push())
            ImGui.TextColored(Theme.Primary, "Cached Profiles");

        ImGui.SetWindowFontScale(Theme.SmallFont);
        ImGui.TextColored(Theme.TextMuted, $"{_entries?.Count ?? 0} profile{((_entries?.Count ?? 0) != 1 ? "s" : "")} cached locally");
        ImGui.SetWindowFontScale(1f);
    }

    static void DrawSearchBar()
    {
        var w = ImGui.GetContentRegionAvail().X;
        using var _bg = ImRaii.PushColor(ImGuiCol.FrameBg, Theme.ButtonBg with { W = 0.5f });
        ImGui.SetNextItemWidth(w);
        ImGui.InputTextWithHint("##cachesearch", "Search by name or world...", ref _search, 64);
    }

    static void DrawCard(CachedEntry e)
    {
        var dl = ImGui.GetWindowDrawList();
        var startPos = ImGui.GetCursorScreenPos();
        var w = ImGui.GetContentRegionAvail().X;
        var cardMax = startPos + new Vector2(w, CardHeight);

        var isHovered = ImGui.IsMouseHoveringRect(startPos, cardMax);

        var bgCol = isHovered
            ? Theme.ButtonHovered with { W = 0.5f }
            : Theme.ButtonBg with { W = 0.3f };
        dl.AddRectFilled(startPos, cardMax, Theme.Col(bgCol), 6);

        if (isHovered)
            dl.AddRectFilled(startPos, startPos + new Vector2(3, CardHeight), Theme.Col(Theme.GoldColor), 2, ImDrawFlags.RoundCornersLeft);

        var borderCol = isHovered ? Theme.GoldColor with { W = 0.6f } : Theme.FrameBorder with { W = 0.3f };
        dl.AddRect(startPos, cardMax, Theme.Col(borderCol), 6);

        var imgPos = startPos + new Vector2(8, (CardHeight - ImageSize) / 2);
        var imgMax = imgPos + new Vector2(ImageSize);

        var tex = Globals.Images.Get(e.Image);
        if (tex != null)
        {
            var (tw, th) = ((float)tex.Width, (float)tex.Height);
            var aspectRatio = tw / th;
            var (u0, u1) = aspectRatio > 1
                ? (new Vector2((tw - th) / 2 / tw, 0), new Vector2(1 - (tw - th) / 2 / tw, 1))
                : (new Vector2(0, (th - tw) / 2 / th), new Vector2(1, 1 - (th - tw) / 2 / th));
            dl.AddImageRounded(tex.Handle, imgPos, imgMax, u0, u1, 0xFFFFFFFF, 4);
        }
        else
        {
            dl.AddRectFilled(imgPos, imgMax, Theme.Col(Theme.FrameBorderInner with { W = 0.3f }), 4);
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                var icon = FontAwesomeIcon.User.ToIconString();
                var iconSz = ImGui.CalcTextSize(icon);
                dl.AddText(imgPos + (new Vector2(ImageSize) - iconSz) / 2, Theme.Col(Theme.LabelColorDim with { W = 0.5f }), icon);
            }
        }
        dl.AddRect(imgPos, imgMax, Theme.Col(Theme.FrameBorder with { W = 0.4f }), 4);

        var textX = imgMax.X + 10;
        var maxTextW = w - (textX - startPos.X) - 12;

        ImGui.SetCursorScreenPos(new Vector2(textX, startPos.Y + 8));
        using (Globals.Fonts.Header.Push())
        {
            var displayName = e.DisplayName ?? e.Name;
            if (ImGui.CalcTextSize(displayName).X > maxTextW)
                displayName = displayName.Length > 18 ? displayName[..16] + "…" : displayName;
            ImGui.TextColored(Theme.NameColor, displayName);
        }

        ImGui.SetCursorScreenPos(new Vector2(textX, startPos.Y + 28));
        ImGui.SetWindowFontScale(Theme.SmallFont);

        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.TextColored(Theme.WorldColor with { W = 0.7f }, FontAwesomeIcon.Globe.ToIconString());
        ImGui.SameLine(0, 4);
        ImGui.TextColored(Theme.WorldColor, $"{e.Name} @ {e.World}");

        ImGui.SetWindowFontScale(1f);

        if (!string.IsNullOrEmpty(e.Description))
        {
            ImGui.SetCursorScreenPos(new Vector2(textX, startPos.Y + CardHeight - 18));
            ImGui.SetWindowFontScale(Theme.SmallFont);
            var desc = e.Description.Length > 40 ? e.Description[..38] + "…" : e.Description;
            ImGui.TextColored(Theme.TextMuted, desc);
            ImGui.SetWindowFontScale(1f);
        }

        ImGui.SetCursorScreenPos(startPos);
        if (ImGui.InvisibleButton($"##cached_{e.Key}", new Vector2(w, CardHeight)))
        {
            Globals.MainWindow.ShowTarget(e.Name, e.World);
            Sound.PlayOpen();
        }

        if (isHovered)
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        ImGui.SetCursorScreenPos(new Vector2(startPos.X, cardMax.Y));
    }

    static void DrawEmpty(string? title = null, string? sub = null)
    {
        var avail = ImGui.GetContentRegionAvail();
        var dl = ImGui.GetWindowDrawList();

        UI.Space(avail.Y * 0.25f);

        var centerX = ImGui.GetCursorScreenPos().X + avail.X / 2;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            ImGui.SetWindowFontScale(2f);
            var icon = FontAwesomeIcon.Database.ToIconString();
            var iconSize = ImGui.CalcTextSize(icon);
            dl.AddText(new Vector2(centerX - iconSize.X / 2, ImGui.GetCursorScreenPos().Y), Theme.Col(Theme.LabelColorDim with { W = 0.4f }), icon);
            ImGui.SetWindowFontScale(1f);
        }

        UI.Space(40);
        Theme.Centered(title ?? "No Cached Profiles", Theme.LabelColor);
        UI.Space(UI.Xs);
        Theme.Centered(sub ?? "Profiles you view will be saved here", Theme.TextMuted);
    }

    static void RefreshList()
    {
        _lastRefresh = DateTime.UtcNow;
        var list = new List<CachedEntry>();

        string? myName = null, myWorld = null;
        if (Globals.Objects.LocalPlayer is { } lp)
        {
            myName = lp.Name.TextValue;
            myWorld = lp.HomeWorld.Value.Name.ToString();
        }

        foreach (var (key, cached) in Globals.Cache.GetAllCached())
        {
            if (cached.Data == null || cached.Unverified) continue;

            var parts = key.Split('@', 2);
            if (parts.Length != 2) continue;

            var name = parts[0];
            var world = parts[1];

            if (myName != null && name.Equals(myName, StringComparison.OrdinalIgnoreCase) &&
                world.Equals(myWorld!, StringComparison.OrdinalIgnoreCase))
                continue;

            list.Add(new CachedEntry(
                name, world,
                cached.Data.Name,
                cached.Data.Description,
                cached.Data.PageImage,
                cached.FetchedAt));
        }

        _entries = list.OrderByDescending(e => e.FetchedAt).ToList();
    }

    record CachedEntry(
        string Name,
        string World,
        string? DisplayName,
        string? Description,
        string? Image,
        DateTime FetchedAt)
    {
        public string Key => $"{Name}@{World}";
    }
}
