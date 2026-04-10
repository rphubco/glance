namespace Glance.UI.Tabs;

using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface;
using Glance.Utils;
using Glance.Core;
using Glance.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

public static class NearbyTab
{
    static List<NearbyPlayer> _players = new();
    static DateTime _lastScan;
    static string? _hoveredKey;
    static bool _scanning;
    static float _spinAngle;

    const float CardHeight = 72f;
    const float ImageSize = 56f;
    const float CardSpacing = 6f;

    public static void Draw()
    {
        if (!Globals.Auth.IsAuthenticated)
        {
            DrawUnauthenticated();
            return;
        }

        if (!Globals.Config.BeaconEnabled)
        {
            DrawBeaconDisabled();
            return;
        }

        if ((DateTime.UtcNow - _lastScan).TotalSeconds > 5 && !_scanning)
            _ = ScanAsync();

        DrawHeader();
        UI.Space(UI.Sm);
        UI.Divider();
        UI.Space(UI.Sm);

        if (_players.Count == 0)
            DrawLoadingState();
        else
            DrawPlayerList();
    }

    public static void ClearSelection()
    {
        if (_hoveredKey != null)
        {
            _hoveredKey = null;
            Globals.Tooltip.Hide();
        }
    }
 

    static void DrawHeader()
    {
        var w = ImGui.GetContentRegionAvail().X;
        var startPos = ImGui.GetCursorScreenPos();

        ImGui.BeginGroup();
        using (Globals.Fonts.Header.Push())
        {
            ImGui.TextColored(Theme.Primary, "Nearby Profiles");
        }
        ImGui.EndGroup();

        ImGui.SameLine(0, 5f);
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.SetWindowFontScale(0.9f);
        ImGui.TextColored(Theme.LabelColorDim, FontAwesomeIcon.QuestionCircle.ToIconString());
        ImGui.SetWindowFontScale(1f);
        ImGui.PopFont();

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextColored(Theme.GoldColor, "Nearby Discovery");
            ImGui.Separator();

            ImGui.BulletText("Shows other Glance users currently in your immediate area.");

            var myWorld = Globals.PlayerState?.CurrentWorld.Value.Name.ExtractText() ?? "Unknown";
            ImGui.BulletText($"Currently tracking users on:");
            ImGui.SameLine();
            ImGui.TextColored(Theme.Primary, myWorld);

            ImGui.BulletText("Users from other worlds will display a globe icon.");

            ImGui.Spacing();
            ImGui.TextColored(Theme.GoldColor, "Privacy");
            ImGui.Separator();
            ImGui.BulletText("To hide yourself: Disable 'Share Location' in Settings.");

            ImGui.EndTooltip();
        }

        var zone = GetZone();
        ImGui.SetWindowFontScale(Theme.SmallFont);
        var zoneSize = ImGui.CalcTextSize(zone);
        ImGui.SetWindowFontScale(1f);

        var btnSize = new Vector2(28, 28);
        ImGui.SameLine();
        ImGui.SetCursorPosX(w - zoneSize.X - btnSize.X - 12);
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.ButtonBg with { W = 0.4f });
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.ButtonHovered);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.LabelColor);
        ImGui.PushFont(UiBuilder.IconFont);
        if (ImGui.Button($"{FontAwesomeIcon.ExternalLinkAlt.ToIconString()}##detach_nearby", btnSize))
        {
            Globals.NearbyWindow.IsOpen = true;
            Sound.PlayOpen();
        }
        ImGui.PopFont();
        ImGui.PopStyleColor(3);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Detach to separate window");

        ImGui.SameLine();
        ImGui.SetCursorPosX(w - zoneSize.X);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);
        ImGui.SetWindowFontScale(Theme.SmallFont);
        ImGui.TextColored(Theme.TextMuted, zone);
        ImGui.SetWindowFontScale(1f);

        ImGui.SetCursorScreenPos(new Vector2(startPos.X, startPos.Y + 24));
        ImGui.SetWindowFontScale(Theme.SmallFont);
        if (_players.Count > 0)
            ImGui.TextColored(Theme.Success, $"{_players.Count} roleplayer{(_players.Count != 1 ? "s" : "")} found");
        else
            ImGui.TextColored(Theme.TextMuted, "Scanning for users...");
        ImGui.SetWindowFontScale(1f);

        ImGui.SetCursorScreenPos(new Vector2(startPos.X, startPos.Y + 42));
    }

    static void DrawPlayerList()
    {
        var myWorld = Globals.PlayerState?.HomeWorld.Value.Name.ExtractText();

        if (ImGui.BeginChild("##playerlist_tab", new Vector2(-1, -1), false, ImGuiWindowFlags.None))
        {
            foreach (var player in _players)
            {
                DrawPlayerCard(player, myWorld);
                UI.Space(CardSpacing);
            }
        }
        ImGui.EndChild();
    }

    static void DrawPlayerCard(NearbyPlayer p, string? myWorld)
    {
        var dl = ImGui.GetWindowDrawList();
        var startPos = ImGui.GetCursorScreenPos();
        var w = ImGui.GetContentRegionAvail().X;
        var cardMax = startPos + new Vector2(w, CardHeight);

        var isHovered = ImGui.IsMouseHoveringRect(startPos, cardMax);
        var isSelected = _hoveredKey == p.Key;

        var bgCol = isHovered || isSelected
            ? Theme.ButtonHovered with { W = 0.5f }
            : Theme.ButtonBg with { W = 0.3f };
        dl.AddRectFilled(startPos, cardMax, Theme.Col(bgCol), 6);

        if (isHovered || isSelected)
            dl.AddRectFilled(startPos, startPos + new Vector2(3, CardHeight), Theme.Col(Theme.GoldColor), 2, ImDrawFlags.RoundCornersLeft);

        var borderCol = isHovered ? Theme.GoldColor with { W = 0.6f } : Theme.FrameBorder with { W = 0.3f };
        dl.AddRect(startPos, cardMax, Theme.Col(borderCol), 6);

        var imgPos = startPos + new Vector2(10, (CardHeight - ImageSize) / 2);
        var imgMax = imgPos + new Vector2(ImageSize);

        var tex = Globals.Images.Get(p.ProfileImage);
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
            ImGui.PushFont(UiBuilder.IconFont);
            var placeholderIcon = FontAwesomeIcon.User.ToIconString();
            var iconSz = ImGui.CalcTextSize(placeholderIcon);
            dl.AddText(imgPos + (new Vector2(ImageSize) - iconSz) / 2, Theme.Col(Theme.LabelColorDim with { W = 0.5f }), placeholderIcon);
            ImGui.PopFont();
        }
        dl.AddRect(imgPos, imgMax, Theme.Col(Theme.FrameBorder with { W = 0.4f }), 4);

        var textX = imgMax.X + 12;
        var textStartY = startPos.Y + 10;

        ImGui.SetCursorScreenPos(new Vector2(textX, textStartY));
        using (Globals.Fonts.Header.Push())
        {
            var displayName = p.DisplayName ?? p.Name;
            var maxTextW = w - (textX - startPos.X) - 80;
            if (ImGui.CalcTextSize(displayName).X > maxTextW)
                displayName = Truncate(displayName, 16);
            ImGui.TextColored(Theme.NameColor, displayName);
        }

        if (!string.IsNullOrEmpty(p.Title))
        {
            ImGui.SetCursorScreenPos(new Vector2(textX, textStartY + 20));
            ImGui.SetWindowFontScale(Theme.SmallFont);
            var title = p.Title;
            var maxTitleW = w - (textX - startPos.X) - 80;
            if (ImGui.CalcTextSize(title).X > maxTitleW)
                title = Truncate(title, 28);
            ImGui.TextColored(Theme.TextMuted, title);
            ImGui.SetWindowFontScale(1f);
        }

        var bottomY = startPos.Y + CardHeight - 22;
        ImGui.SetCursorScreenPos(new Vector2(textX, bottomY));
        ImGui.SetWindowFontScale(Theme.SmallFont);

        if (myWorld != null && myWorld != p.World)
        {
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.TextColored(Theme.WorldColor with { W = 0.7f }, FontAwesomeIcon.Globe.ToIconString());
            ImGui.PopFont();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{p.Name} is visiting from {p.World}"); 

            ImGui.SameLine(0, 4);
            ImGui.TextColored(Theme.WorldColor, p.World);
            ImGui.SameLine(0, 12);
        }

        var (proximityText, proximityCol) = GetProximityInfo(p.Proximity);
        ImGui.TextColored(proximityCol, proximityText);
        ImGui.SetWindowFontScale(1f);

        var btnSize = new Vector2(28, 28);
        var btnPos = new Vector2(startPos.X + w - btnSize.X - 10, startPos.Y + (CardHeight - btnSize.Y) / 2);
        ImGui.SetCursorScreenPos(btnPos);

        ImGui.PushStyleColor(ImGuiCol.Button, Theme.ButtonBg with { W = 0.6f });
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.GoldColor with { W = 0.4f });
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.GoldColor with { W = 0.6f });
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.LabelColor);
        ImGui.PushFont(UiBuilder.IconFont);
        if (ImGui.Button($"{FontAwesomeIcon.Crosshairs.ToIconString()}##target_{p.Key}", btnSize))
        {
            TargetPlayer(p.Name, p.World);
        }
        ImGui.PopFont();
        ImGui.PopStyleColor(4);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            ImGui.SetTooltip("Target this player");
        }

        ImGui.SetCursorScreenPos(startPos);
        if (ImGui.InvisibleButton($"##player_{p.Key}", new Vector2(w - btnSize.X - 20, CardHeight)))
        {
            if (_hoveredKey == p.Key)
            {
                _hoveredKey = null;
                Globals.Tooltip.Hide();
                Sound.PlayCancel();
            }
            else
            {
                _hoveredKey = p.Key;
                Globals.Tooltip.Show(p.Name, p.World);
                Globals.MainWindow.ShowTarget(p.Name, p.World);
                Sound.PlayOpen();
            }
        }

        if (isHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        ImGui.SetCursorScreenPos(new Vector2(startPos.X, cardMax.Y));
    }

    static void TargetPlayer(string name, string world)
    {
        var player = Globals.Objects
            .OfType<IPlayerCharacter>()
            .FirstOrDefault(pc => pc.Name.TextValue == name && pc.HomeWorld.Value.Name.ExtractText() == world);

        if (player != null)
        {
            Globals.TargetManager.Target = player;
            Sound.PlayClick();
        }
    }

    static (string text, Vector4 color) GetProximityInfo(Proximity proximity) => proximity switch
    {
        Proximity.Close => ("Close", Theme.Success),
        Proximity.Nearby => ("Nearby", Theme.Warning),
        Proximity.InArea => ("In Area", Theme.LabelColorDim),
        _ => ("In Area", Theme.LabelColorDim)
    };

    static Proximity CalculateProximity(float distance) => distance switch
    {
        < 15f => Proximity.Close,
        < 40f => Proximity.Nearby,
        _ => Proximity.InArea
    };

    static void DrawBeaconDisabled()
    {
        var avail = ImGui.GetContentRegionAvail();
        var dl = ImGui.GetWindowDrawList();

        UI.Space(avail.Y * 0.25f);

        var centerX = ImGui.GetCursorScreenPos().X + avail.X / 2;
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.SetWindowFontScale(2f);
        var icon = FontAwesomeIcon.BroadcastTower.ToIconString();
        var iconSize = ImGui.CalcTextSize(icon);
        dl.AddText(new Vector2(centerX - iconSize.X / 2, ImGui.GetCursorScreenPos().Y), Theme.Col(Theme.LabelColorDim with { W = 0.5f }), icon);
        ImGui.SetWindowFontScale(1f);
        ImGui.PopFont();

        UI.Space(50);
        Theme.Centered("Beacon Not Enabled", Theme.LabelColor);
        UI.Space(UI.Xs);

        if (!Globals.Config.BeaconEnabled)
        {
            Theme.Centered("Enable Beacon in Settings to discover", Theme.TextMuted);
            Theme.Centered("nearby roleplayers.", Theme.TextMuted);
        }
        else
        {
            Theme.Centered("Enable Location Sharing in Settings", Theme.TextMuted);
            Theme.Centered("to discover nearby roleplayers.", Theme.TextMuted);
        }

        UI.Space(16);

        var buttonW = 140f;
        ImGui.SetCursorPosX((avail.X - buttonW) / 2);
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.PrimaryButtonBg);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.PrimaryButtonHover);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.PrimaryButtonText);
        if (ImGui.Button("Open Settings", new Vector2(buttonW, 30)))
        {
            Globals.MainWindow.SetTab(3);
            Sound.PlayOpen();
        }
        ImGui.PopStyleColor(3);
    }

    static void DrawUnauthenticated()
    {
        var avail = ImGui.GetContentRegionAvail();
        var dl = ImGui.GetWindowDrawList();

        UI.Space(avail.Y * 0.3f);

        var centerX = ImGui.GetCursorScreenPos().X + avail.X / 2;
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.SetWindowFontScale(2f);
        var icon = FontAwesomeIcon.Lock.ToIconString();
        var iconSize = ImGui.CalcTextSize(icon);
        dl.AddText(new Vector2(centerX - iconSize.X / 2, ImGui.GetCursorScreenPos().Y), Theme.Col(Theme.Error with { W = 0.6f }), icon);
        ImGui.SetWindowFontScale(1f);
        ImGui.PopFont();

        UI.Space(40);
        Theme.Centered("Not Logged In", Theme.LabelColor);
        UI.Space(UI.Xs);
        Theme.Centered("Connect your RPHub account to see", Theme.TextMuted);
        Theme.Centered("nearby roleplayers", Theme.TextMuted);
    }

    static void DrawLoadingState()
    {
        var avail = ImGui.GetContentRegionAvail();
        var dl = ImGui.GetWindowDrawList();

        UI.Space(avail.Y * 0.25f);

        _spinAngle += ImGui.GetIO().DeltaTime * 3f;
        var centerX = ImGui.GetCursorScreenPos().X + avail.X / 2;
        var spinPos = new Vector2(centerX, ImGui.GetCursorScreenPos().Y);

        for (var i = 0; i < 8; i++)
        {
            var a = _spinAngle + i * (MathF.PI / 4);
            var alpha = (i + 1) / 8f;
            var p1 = spinPos + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 8;
            var p2 = spinPos + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 16;
            dl.AddLine(p1, p2, Theme.Col(Theme.GoldColor with { W = alpha }), 2.5f);
        }

        UI.Space(32);
        Theme.Centered("Scanning nearby players...", Theme.LabelColor);
        UI.Space(UI.Xs);
        Theme.Centered("Looking for RPHub profiles", Theme.TextMuted);
    }

    static async Task ScanAsync()
    {
        if (_scanning) return;
        _scanning = true;
        _lastScan = DateTime.UtcNow;

        try
        {
            var lp = Globals.Objects.LocalPlayer;
            if (lp == null) { _players.Clear(); return; }

            var found = new List<NearbyPlayer>();
            var neighbors = Globals.Cache.GetBeaconNeighbors().ToList();

            var uncached = new List<(string Name, string World)>();
            foreach (var (name, world) in neighbors)
            {
                if (name.Equals(lp.Name.TextValue, StringComparison.OrdinalIgnoreCase)) continue;
                if (Globals.Cache.GetProfile(name, world) == null)
                    uncached.Add((name, world));
            }
            if (uncached.Count > 0)
                await Globals.Cache.FetchBatchAsync(uncached);

            foreach (var (name, world) in neighbors)
            {
                if (name.Equals(lp.Name.TextValue, StringComparison.OrdinalIgnoreCase)) continue;

                var pc = Globals.Objects.OfType<IPlayerCharacter>().FirstOrDefault(x =>
                    x.Name.TextValue.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                    x.HomeWorld.Value.Name.ExtractText().Equals(world, StringComparison.OrdinalIgnoreCase));

                if (Globals.Config.OnlyShowRenderedPlayers && pc == null)
                    continue;

                float dist = pc != null ? Vector3.Distance(lp.Position, pc.Position) : 100f;

                if (Globals.Config.OnlyShowRoleplaying && pc != null)
                {
                    if (pc.OnlineStatus.ValueNullable?.RowId != 22) continue;
                }

                var cached = Globals.Cache.GetProfile(name, world);
                if (cached != null && Globals.Mutes.IsMuted(cached.Id.ToString()))
                    continue;

                string displayName = cached?.Data?.Name ?? name;
                string description = cached?.Data?.Description ?? (cached != null ? "Unverified Profile" : "Loading profile...");

                found.Add(new NearbyPlayer(
                    name,
                    world,
                    displayName,
                    description,
                    cached?.Data?.PageImage,
                    cached?.Data?.Glances ?? [],
                    CalculateProximity(dist)
                ));
            }

            _players = found.OrderBy(p => p.Proximity).ToList();
        }
        catch { }
        finally { _scanning = false; }
    }

    static string GetZone()
    {
        try
        {
            var id = Globals.ClientState.TerritoryType;
            return Globals.Data.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()?.GetRow(id).PlaceName.Value.Name.ExtractText() ?? $"Zone {id}";
        }
        catch { return $"Zone {Globals.ClientState.TerritoryType}"; }
    }

    static string Truncate(string s, int max) => s.Length > max ? s[..(max - 2)] + "…" : s;

    enum Proximity { Close, Nearby, InArea }

    record NearbyPlayer(
        string Name,
        string World,
        string? DisplayName,
        string Title,
        string? ProfileImage,
        List<GlanceData>? Glances,
        Proximity Proximity
    )
    {
        public string Key => $"{Name}@{World}";
    }
}
