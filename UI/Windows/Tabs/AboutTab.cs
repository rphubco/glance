namespace Glance.UI.Tabs;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Glance.Utils;
using Glance.Core;
using System.Numerics;

public static class AboutTab
{
    public static void Draw()
    {
        var dl = ImGui.GetWindowDrawList();
        var w = ImGui.GetContentRegionAvail().X;

        DrawLogo(dl, w);
        UI.Space(UI.Lg);
        DrawFeatures(dl, w);
        UI.Space(UI.Lg);
        DrawLinks(dl, w);
        UI.Space(UI.Lg);
        DrawFooter(dl, w);
    }

    static void DrawLogo(ImDrawListPtr dl, float w)
    {
        UI.Space(UI.Md);

        try
        {
            var icon = Globals.TextureProvider.GetFromGameIcon(new GameIconLookup(65120));
            if (icon.TryGetWrap(out var wrap, out _))
            {
                var imgSize = new Vector2(48, 48);
                ImGui.SetCursorPosX((w - imgSize.X) / 2);
                ImGui.Image(wrap.Handle, imgSize);
            }
        }
        catch { }

        UI.Space(UI.Sm);
        using (Globals.Fonts.Title.Push()) Theme.Centered("Glance", Theme.GoldColor);
        ImGui.SetWindowFontScale(Theme.SmallFont);
        Theme.Centered($"v{Globals.Version} beta  •  by RPHub.co", Theme.TextMuted);
        ImGui.SetWindowFontScale(1f);

        UI.Space(UI.Sm);

        var tagline = "Character profiles & roleplay coordination for FFXIV";
        ImGui.SetWindowFontScale(0.95f);
        Theme.Centered(tagline, Theme.ValueColor);
        ImGui.SetWindowFontScale(1f);
    }

    static void DrawFeatures(ImDrawListPtr dl, float w)
    {
        var p = ImGui.GetCursorScreenPos();
        var boxH = 4 * 52f + UI.Sm * 2;
        dl.AddRectFilled(p, p + new Vector2(w, boxH), Theme.Col(Theme.ButtonBg with { W = 0.25f }), 6);
        dl.AddRect(p, p + new Vector2(w, boxH), Theme.Col(Theme.FrameBorder with { W = 0.3f }), 6);

        ImGui.SetCursorScreenPos(p + new Vector2(0, UI.Sm));

        Feature(dl, w, FontAwesomeIcon.IdCard, "Character Profiles",
            "Create rich profiles with portraits, backstory, and at-a-glance info");
        Feature(dl, w, FontAwesomeIcon.Eye, "Glance Tooltips",
            "See other players' profiles by targeting them in-game");
        Feature(dl, w, FontAwesomeIcon.Users, "Nearby Discovery",
            "Find roleplayers in your area with the beacon system");
        Feature(dl, w, FontAwesomeIcon.Comment, "Live Status",
            "Share IC status and OOC notes that update in real-time");

        ImGui.SetCursorScreenPos(p + new Vector2(0, boxH + UI.Xs));
    }

    static void Feature(ImDrawListPtr dl, float w, FontAwesomeIcon icon, string title, string desc)
    {
        var p = ImGui.GetCursorScreenPos();
        const float iconCol = 44f;
        const float h = 48f;

        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var iconStr = icon.ToIconString();
            var iconSz = ImGui.CalcTextSize(iconStr);
            dl.AddText(p + new Vector2(UI.Lg + (iconCol - iconSz.X) / 2, (h - iconSz.Y) / 2), Theme.Col(Theme.GoldColor), iconStr);
        }

        ImGui.SetCursorScreenPos(p + new Vector2(UI.Lg + iconCol, 6));
        ImGui.TextColored(Theme.LabelColor, title);

        ImGui.SetCursorScreenPos(p + new Vector2(UI.Lg + iconCol, 24));
        ImGui.SetWindowFontScale(Theme.SmallFont);
        ImGui.PushTextWrapPos(p.X + w - UI.Lg);
        ImGui.TextColored(Theme.TextMuted, desc);
        ImGui.PopTextWrapPos();
        ImGui.SetWindowFontScale(1f);

        ImGui.SetCursorScreenPos(p + new Vector2(0, h + UI.Xs));
    }

    static void DrawLinks(ImDrawListPtr dl, float w)
    {
        ImGui.SetWindowFontScale(Theme.SmallFont);
        Theme.Centered("LINKS", Theme.LabelColorDim);
        ImGui.SetWindowFontScale(1f);
        UI.Space(UI.Sm);

        var btnW = (w - UI.Sm * 2) / 3;
        LinkBtn(dl, FontAwesomeIcon.Globe, "Website", "https://rphub.co", btnW);
        ImGui.SameLine(0, UI.Sm);
        LinkBtn(dl, FontAwesomeIcon.Heart, "Discord", "https://discord.com/invite/rbSWGK9gvT", btnW);
        ImGui.SameLine(0, UI.Sm);
        LinkBtn(dl, FontAwesomeIcon.Code, "GitHub", "https://github.com/rphubco/glance", btnW);
    }

    static void LinkBtn(ImDrawListPtr dl, FontAwesomeIcon icon, string label, string url, float w)
    {
        var p = ImGui.GetCursorScreenPos();
        const float h = 36f;

        var clicked = ImGui.InvisibleButton($"##link_{label}", new Vector2(w, h));
        var hov = ImGui.IsItemHovered();

        dl.AddRectFilled(p, p + new Vector2(w, h), Theme.Col(hov ? Theme.ButtonHovered : Theme.ButtonBg), 4);
        dl.AddRect(p, p + new Vector2(w, h), Theme.Col(Theme.FrameBorder with { W = hov ? 0.5f : 0.3f }), 4);

        Vector2 iconSz;
        var iconStr = icon.ToIconString();
        using (ImRaii.PushFont(UiBuilder.IconFont))
            iconSz = ImGui.CalcTextSize(iconStr);

        var labelSz = ImGui.CalcTextSize(label);
        var totalW = iconSz.X + 6 + labelSz.X;
        var startX = p.X + (w - totalW) / 2;

        using (ImRaii.PushFont(UiBuilder.IconFont))
            dl.AddText(new Vector2(startX, p.Y + (h - iconSz.Y) / 2), Theme.Col(hov ? Theme.GoldColor : Theme.LabelColorDim), iconStr);
        dl.AddText(new Vector2(startX + iconSz.X + 6, p.Y + (h - labelSz.Y) / 2), Theme.Col(hov ? Theme.GoldColor : Theme.LabelColor), label);

        if (hov) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (clicked) Dalamud.Utility.Util.OpenLink(url);
    }

    static void DrawFooter(ImDrawListPtr dl, float w)
    {
        var p = ImGui.GetCursorScreenPos();
        dl.AddLine(p + new Vector2(40, 0), p + new Vector2(w - 40, 0), Theme.Col(Theme.SeparatorColor with { W = 0.3f }));
        UI.Space(UI.Md);

        ImGui.SetWindowFontScale(Theme.SmallFont);
        Theme.Centered("Made with ♥ for the FFXIV RP community", Theme.TextMuted);
        UI.Space(UI.Xs);
        Theme.Centered("All FFXIV content © Square Enix Co., Ltd.", Theme.LabelColorDim with { W = 0.5f });
        Theme.Centered("Glance is not affiliated with Square Enix, Dalamud, or XIVLauncher.", Theme.LabelColorDim with { W = 0.4f });
        ImGui.SetWindowFontScale(1f);
    }
}
