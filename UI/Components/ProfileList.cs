namespace Glance.UI.Components;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Glance.Utils;
using Glance.Core;
using Glance.Models;
using System;
using System.Numerics;
using System.Threading.Tasks;

public static class ProfileList
{
    static string? _processing;
    static DateTime _lastSwitch;
    const int DebounceMs = 500;

    public static void Draw()
    {
        if (Globals.Auth.BeaconFetchFailed)
        {
            ErrorState(Globals.Auth.LastBeaconError ?? "Connection failed");
            return;
        }

        if (!Globals.Auth.IsReady)
        {
            Centered(FontAwesomeIcon.ShieldAlt, "Finalizing secure handshake...", Theme.LabelColorDim, 1.5f);
            return;
        }

        var data = Globals.Profiles.Data;
        if (data == null) { Centered(FontAwesomeIcon.Spinner, "Loading profiles...", Theme.LabelColor); return; }
        if (data.Characters.Length == 0) { Empty(); return; }

        foreach (var p in data.Characters)
            Card(p, p.Id == Globals.Profiles.ActiveProfileId);
    }

    static void Card(ProfileCharacter c, bool active)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var w = ImGui.GetContentRegionAvail().X;
        const float h = 56f, avatar = 40f, pad = 8f;
        var max = pos + new Vector2(w, h);

        ImGui.PushID(c.Id);

        dl.AddRectFilled(pos, max, Theme.Col((active ? Theme.ButtonActive : Theme.ButtonBg) with { W = 0.6f }), 6f);
        dl.AddRect(pos, max, Theme.Col((active ? Theme.GoldColor : Theme.FrameBorderInner) with { W = active ? 0.8f : 0.5f }), 6f);

        if (active)
            dl.AddRectFilled(new Vector2(pos.X, pos.Y + 8), new Vector2(pos.X + 3, max.Y - 8), Theme.Col(Theme.GoldColor), 2f);

        var ap = new Vector2(pos.X + pad + (active ? 6 : 0), pos.Y + (h - avatar) / 2);
        var am = ap + new Vector2(avatar, avatar);

        if (Globals.Images.Get(c.Avatar) is { } tex)
        {
            var tw = (float)tex.Width;
            var th = (float)tex.Height;
            Vector2 uv0, uv1;
            if (tw > th) { var crop = (tw - th) / 2 / tw; uv0 = new Vector2(crop, 0); uv1 = new Vector2(1 - crop, 1); }
            else if (th > tw) { var crop = (th - tw) / 2 / th; uv0 = new Vector2(0, crop); uv1 = new Vector2(1, 1 - crop); }
            else { uv0 = Vector2.Zero; uv1 = Vector2.One; }
            dl.AddImageRounded(tex.Handle, ap, am, uv0, uv1, 0xFFFFFFFF, 4f);
        }
        else
        {
            dl.AddRectFilled(ap, am, Theme.Col(Theme.PlaceholderBg), 4f);
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                var icon = FontAwesomeIcon.User.ToIconString();
                var isz = ImGui.CalcTextSize(icon);
                dl.AddText(ImGui.GetFont(), ImGui.GetFontSize(), ap + (new Vector2(avatar, avatar) - isz) / 2, Theme.Col(Theme.LabelColorDim), icon);
            }
        }
        dl.AddRect(ap, am, Theme.Col(Theme.FrameBorderInner), 4f);

        var tx = am.X + pad;
        dl.AddText(new Vector2(tx, pos.Y + h / 2 - ImGui.GetTextLineHeight() - 2), Theme.Col(active ? Theme.GoldColor : Theme.ValueColor), c.Name);

        ImGui.SetWindowFontScale(Theme.SmallFont);
        var status = active ? "Active Profile" : (_processing == c.Id ? "Switching..." : "Click to activate");
        dl.AddText(new Vector2(tx, pos.Y + h / 2 + 2), Theme.Col(active ? Theme.Primary : Theme.TextMuted), status);
        ImGui.SetWindowFontScale(1f);

        if (active)
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                var chk = FontAwesomeIcon.CheckCircle.ToIconString();
                var csz = ImGui.CalcTextSize(chk);
                dl.AddText(ImGui.GetFont(), ImGui.GetFontSize(), new Vector2(max.X - pad - csz.X, pos.Y + (h - csz.Y) / 2), Theme.Col(Theme.Primary), chk);
            }
        }

        ImGui.SetCursorScreenPos(pos);
        if (ImGui.InvisibleButton("##card", new Vector2(w, h)))
        {
            if (active)
            {
                _ = ViewOwnProfileAsync(c.Id);
            }
            else if (_processing == null && (DateTime.UtcNow - _lastSwitch).TotalMilliseconds >= DebounceMs)
            {
                _lastSwitch = DateTime.UtcNow;
                _processing = c.Id;
                _ = SelectAsync(c.Id);
                Sound.PlaySelect();
            }
        }

        if (ImGui.IsItemHovered())
        {
            dl.AddRectFilled(pos, max, Theme.Col(Theme.ButtonHovered with { W = 0.3f }), 6f);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

            if (active)
                ImGui.SetTooltip("Click to view and manage profile");
        }

        ImGui.SetCursorScreenPos(new Vector2(pos.X, max.Y));
        ImGui.PopID();
        UI.Space(4);
    }

    static void Empty()
    {
        UI.Space(20);
        Centered(FontAwesomeIcon.UserPlus, null, Theme.LabelColorDim, 2f);
        UI.Space(12);
        Theme.Centered("No profiles yet", Theme.LabelColor);
        UI.Space(4);
        Theme.Centered("Create your first character profile", Theme.TextMuted);
        UI.Space(16);

        var w = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX((w - 140) / 2);
        using (ImRaii.PushColor(ImGuiCol.Button, Theme.PrimaryButtonBg)
            .Push(ImGuiCol.ButtonHovered, Theme.PrimaryButtonHover)
            .Push(ImGuiCol.Text, Theme.PrimaryButtonText))
        {
            if (ImGui.Button("Create Profile", new Vector2(140, 32))) { ProfileEditor.OpenForCreate(); }
        }
    }

    static void Centered(FontAwesomeIcon icon, string? text, Vector4 col, float scale = 1f)
    {
        var w = ImGui.GetContentRegionAvail().X;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            ImGui.SetWindowFontScale(scale);
            var ic = icon.ToIconString();
            ImGui.SetCursorPosX((w - ImGui.CalcTextSize(ic).X) / 2);
            ImGui.TextColored(col, ic);
            ImGui.SetWindowFontScale(1f);
        }
        if (text != null) { UI.Space(8); Theme.Centered(text, col); }
    }

    static async Task SelectAsync(string id)
    {
        try
        {
            await Globals.Profiles.SetActiveProfileAsync(id);
            Globals.Toolbox.ResetState();
            await Task.Delay(100);

            string? name = null, world = null;
            await Globals.Framework.RunOnFrameworkThread(() =>
            {
                if (Globals.Objects.LocalPlayer is not { } lp) return;
                name = lp.Name.TextValue;
                world = lp.HomeWorld.Value.Name.ToString();
            });

            if (name == null || world == null) return;

            Globals.Cache.MyLocalVersion = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var refreshed = await Globals.Cache.RefreshProfileAsync(name, world);

            if (refreshed?.Data != null)
                Tabs.ProfileTab.Show(name, world, refreshed.Data, id);

            var tt = Globals.Tooltip;
            if (tt?.IsOpen == true &&
                string.Equals(tt.CurrentTargetName, name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(tt.CurrentTargetWorld, world, StringComparison.OrdinalIgnoreCase))
            {
                tt.UpdateData(refreshed);
            }

            Globals.ProfileView.UpdateData(name, world, refreshed);
            Sound.PlaySuccess();
        }
        finally { _processing = null; }
    }

    static async Task ViewOwnProfileAsync(string profileId)
    {
        string? name = null, world = null;
        await Globals.Framework.RunOnFrameworkThread(() =>
        {
            if (Globals.Objects.LocalPlayer is not { } lp) return;
            name = lp.Name.TextValue;
            world = lp.HomeWorld.Value.Name.ToString();
        });

        if (name == null || world == null) return;

        var cached = await Globals.Cache.FetchProfileAsync(name, world);

        Tabs.ProfileTab.Show(name, world, cached?.Data, profileId);
        Globals.MainWindow.ShowProfile();
    }

    public static async Task ViewOwnProfileAsync2(string profileId)
    {
        string? name = null, world = null;
        await Globals.Framework.RunOnFrameworkThread(() =>
        {
            if (Globals.Objects.LocalPlayer is not { } lp) return;
            name = lp.Name.TextValue;
            world = lp.HomeWorld.Value.Name.ToString();
        });
        if (name == null || world == null) return;
        var cached = await Globals.Cache.FetchProfileAsync(name, world);
        if (cached?.Data != null)
        {
            Tabs.ProfileTab.Show(name, world, cached.Data, profileId);
        }
    }

    static void ErrorState(string message)
    {
        UI.Space(24);
        var title = "Sync Failed";
        var subMessage = message;
        var isNotFound = message.Contains("NOT_FOUND");

        if (isNotFound)
        {
            title = "Character Not Found";
            subMessage = "We couldn't find your character on Lodestone. Please ensure you've finished the Level 1 tutorial and the Lodestone has updated.";
        }
        else if (message.Contains("PRIVATE"))
        {
            title = "Profile Private";
            subMessage = "Your Lodestone profile is set to Private. We cannot verify ownership until you set it to Public.";
        }
        else if (message.Contains("MAINTENANCE"))
        {
            title = "Lodestone Maintenance";
            subMessage = "Lodestone is currently down for maintenance. Try again later.";
        }
        else if (message.Contains("RATE_LIMITED"))
        {
            title = "Slow Down!";
            subMessage = "You're sending requests too fast. Please wait a minute before trying to verify again.";
        }

        Centered(FontAwesomeIcon.ExclamationTriangle, title, Theme.LabelColor, 2f);
        UI.Space(12);

        ImGui.PushTextWrapPos(ImGui.GetCursorPos().X + ImGui.GetContentRegionAvail().X - 20f);
        Theme.Centered(subMessage, Theme.TextMuted);
        ImGui.PopTextWrapPos();

        UI.Space(24);
        var w = ImGui.GetContentRegionAvail().X;
        var btnW = 160f;
        ImGui.SetCursorPosX((w - btnW) / 2);

        var fetching = Globals.Auth.IsFetching;
        using (ImRaii.Disabled(fetching))
        using (ImRaii.PushColor(ImGuiCol.Button, Theme.PrimaryButtonBg)
            .Push(ImGuiCol.ButtonHovered, Theme.PrimaryButtonHover))
        {
            var btnText = fetching ? "Checking..." : "Check Again";

            if (ImGui.Button(btnText, new Vector2(btnW, 36)))
            {
                _ = Globals.Auth.RefreshBeaconTokenAsync();
                Sound.PlayConfirm();
            }
        }
    }
}
