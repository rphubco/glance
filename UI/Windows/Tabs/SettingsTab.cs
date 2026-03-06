namespace Glance.UI.Tabs;

using Dalamud.Bindings.ImGui;
using Glance.Utils;
using Glance.Core;
using System.Threading.Tasks;
using System.Numerics;

public static class SettingsTab
{
    static bool _confirmCacheClear;
    static bool _confirmImageCacheClear;
    static long _profileCacheSize = -1;
    static long _imageCacheSize = -1;

    public static void Draw()
    {
        var config = Globals.Config;
        var changed = false;

        UI.Section("Beacon");
        ImGui.TextColored(Theme.LabelColor, "Control how your presence is shared with other RPHub users.");
        ImGui.Spacing();

        var beaconEnabled = config.BeaconEnabled;
        if (ImGui.Checkbox("Enable Beacon", ref beaconEnabled))
        {
            config.BeaconEnabled = beaconEnabled;
            changed = true;
            Sound.PlayClick();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Master toggle for the beacon service.\n\n" +
                "Disabling breaks: nearby list, nameplate icons,\n" +
                "and real-time profile updates.\n\n" +
                "Keep enabled for the best experience."
            );
        }

        if (beaconEnabled)
        {
            ImGui.Indent();
            var locationSharing = config.BeaconLocationSharing;
            if (ImGui.Checkbox("Share Location", ref locationSharing))
            {
                config.BeaconLocationSharing = locationSharing;
                changed = true;
                Sound.PlayClick();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(
                    "When enabled, your zone (not exact coordinates) is shared with RPHub users in the same zone.\n" +
                    "When disabled, you can still see other opted-in users nearby,\n" +
                    "but they won't see you and new users won't know you have a profile until they click on your character."
                );
            }
            ImGui.Unindent();
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Warning);
            ImGui.TextWrapped("Beacon is disabled. Nearby features and nameplate icons are unavailable.");
            ImGui.PopStyleColor();
        }

        var ghostMode = config.GhostMode;
        if (ImGui.Checkbox("Hide My Profile", ref ghostMode))
        {
            config.GhostMode = ghostMode;
            config.Save();
            Sound.PlayClick();

            Task.Run(async () =>
            {
                var success = await Globals.Profiles.SetGhostModeAsync(ghostMode);
                if (!success)
                {
                    config.GhostMode = !ghostMode;
                    config.Save();
                    Globals.ChatGui.Print("[Glance] Failed to update Hide Profile. Check your connection.");
                }
            });
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Hide your profile from other players.\n\n" +
                "When enabled, others cannot view your profile even if they click you.\n" +
                "You can still see other players' profiles.\n\n" +
                "Note: Users who have already cached your profile may still see it\n" +
                "locally until their cache expires (usually 24h)."
            );
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        UI.Section("Nameplates");
        ImGui.TextColored(Theme.LabelColor, "Customize how RPHub profiles appear on player nameplates.");

        var enableNameplates = config.EnableNameplates;
        if (ImGui.Checkbox("Enable Nameplate Modifications", ref enableNameplates))
        {
            config.EnableNameplates = enableNameplates;
            changed = true;
            Sound.PlayClick();
        }

        if (enableNameplates)
        {
            ImGui.Indent();
            var tintEnabled = config.NameplateTintEnabled;
            if (ImGui.Checkbox("Tint Nameplate Icons", ref tintEnabled)) { config.NameplateTintEnabled = tintEnabled; changed = true; Sound.PlayClick(); }

            if (tintEnabled)
            {
                ImGui.Indent();
                ImGui.Spacing();
                ImGui.TextColored(Theme.LabelColorDim, "Icon Colors");
                ImGui.Spacing();

                var verifiedColor = config.NameplateVerifiedColor;
                if (ImGui.ColorEdit4("Verified Profile##iconcolor", ref verifiedColor, ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.NoInputs))
                {
                    config.NameplateVerifiedColor = verifiedColor;
                    changed = true;
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Icon color for players with a verified RPHub profile.");

                var unverifiedColor = config.NameplateUnverifiedColor;
                if (ImGui.ColorEdit4("Unverified Profile##iconcolor", ref unverifiedColor, ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.NoInputs))
                {
                    config.NameplateUnverifiedColor = unverifiedColor;
                    changed = true;
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Icon color for players with an unverified RPHub profile.");

                ImGui.Spacing();
                ImGui.Unindent();
            }

            var onlyWhenRp = config.NameplateIconOnlyWhenRoleplaying;
            if (ImGui.Checkbox("Only show icon when /roleplaying", ref onlyWhenRp)) { config.NameplateIconOnlyWhenRoleplaying = onlyWhenRp; changed = true; Sound.PlayClick(); }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Hide the RPHub nameplate icon for players who don't have /roleplaying status active.");

            var showNames = config.NameplateShowCustomNames;
            if (ImGui.Checkbox("Show Custom Names", ref showNames)) { config.NameplateShowCustomNames = showNames; changed = true; Sound.PlayClick(); }

            var showTitles = config.NameplateShowCustomTitles;
            if (ImGui.Checkbox("Show Custom Titles", ref showTitles)) { config.NameplateShowCustomTitles = showTitles; changed = true; Sound.PlayClick(); }

            if (showTitles)
            {
                ImGui.Indent();
                var preferHonorifics = config.PreferHonorificsTitles;
                if (ImGui.Checkbox("Prefer Honorifics / In-Game Titles", ref preferHonorifics)) { config.PreferHonorificsTitles = preferHonorifics; changed = true; Sound.PlayClick(); }
                ImGui.Unindent();
            }
            ImGui.Unindent();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        UI.Section("User Interface");
        ImGui.TextColored(Theme.LabelColor, "Control tooltip, toolbox, and display behavior.");
        ImGui.Spacing();

        ImGui.TextColored(Theme.LabelColorDim, "Windows");
        ImGui.Indent();
        var showToolbox = config.ShowToolbox;
        if (ImGui.Checkbox("Show Roleplay Toolbox", ref showToolbox))
        {
            config.ShowToolbox = showToolbox; changed = true; Sound.PlayClick();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Show the floating roleplay toolbox window for quick access to your own profile.");
        ImGui.Unindent();
        ImGui.Spacing();

        ImGui.TextColored(Theme.LabelColorDim, "Tooltip Summary");
        ImGui.Indent();

        var hideEmpty = config.HideEmptyProfiles;
        if (ImGui.Checkbox("Hide for Empty Profiles", ref hideEmpty))
        {
            config.HideEmptyProfiles = hideEmpty; changed = true; Sound.PlayClick();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Don't show the summary popup if the player has no RPHub data.\nReduces visual noise in crowded cities.");

        var showHooks = config.ShowHooksInTooltip;
        if (ImGui.Checkbox("Show RP Hooks", ref showHooks))
        {
            config.ShowHooksInTooltip = showHooks; changed = true; Sound.PlayClick();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Display 6 interactive 'Pills' for character hooks at the bottom of the tooltip.\nHovering a pill shows the full description.");

        var onlyRp = config.OnlyShowRoleplaying;
        if (ImGui.Checkbox("Only show for /roleplaying", ref onlyRp))
        {
            config.OnlyShowRoleplaying = onlyRp; changed = true; Sound.PlayClick();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Only show tooltips for players who currently have the blue 'Roleplaying' icon active.");

        var onlyRendered = config.OnlyShowRenderedPlayers;
        if (ImGui.Checkbox("Only show rendered players in Nearby list", ref onlyRendered))
        {
            config.OnlyShowRenderedPlayers = onlyRendered; changed = true; Sound.PlayClick();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Filters the Nearby tab to only show players whose character models are currently loaded.\n\n" +
                "When enabled: Players too far away to be seen by your game client will be hidden.\n" +
                "When disabled: All users reported by the Beacon in this zone will be shown."
            );
        }

        var lockTarget = config.LockTargetProfile;
        if (ImGui.Checkbox("Lock profile to hard target", ref lockTarget))
        {
            config.LockTargetProfile = lockTarget; changed = true; Sound.PlayClick();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("When you have a hard target (clicked), hovering over other players won't override the displayed profile.");

        var clickOnly = config.TooltipOnClickOnly;
        if (ImGui.Checkbox("Only show on click (not hover)", ref clickOnly))
        {
            config.TooltipOnClickOnly = clickOnly; changed = true; Sound.PlayClick();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Tooltip will only appear when you click a player (hard target), not when hovering over them.");

        ImGui.Unindent();
        ImGui.Spacing();

        ImGui.TextColored(Theme.LabelColorDim, "Visibility Automation");
        ImGui.Indent();
        var combatMode = config.CombatMode;
        if (ImGui.Checkbox("Hide During Combat", ref combatMode))
        {
            config.CombatMode = combatMode; changed = true; Sound.PlayClick();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Automatically hide all Glance UI elements when you enter combat to keep your screen clear.");

        var dutyMode = config.DutyMode;
        if (ImGui.Checkbox("Hide Inside Duties", ref dutyMode))
        {
            config.DutyMode = dutyMode; changed = true; Sound.PlayClick();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Automatically hide Glance UI when inside instanced content like Dungeons or Raids.");
        ImGui.Unindent();
        ImGui.Spacing();

        ImGui.TextColored(Theme.LabelColorDim, "Chat");
        ImGui.Indent();
        var chatReplacement = config.ChatReplacementEnabled;
        if (ImGui.Checkbox("Replace Names in Chat", ref chatReplacement))
        {
            config.ChatReplacementEnabled = chatReplacement; changed = true; Sound.PlayClick();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Replace player names in chat with their RPHub character names.");
        ImGui.Unindent();
        ImGui.Spacing();

        ImGui.TextColored(Theme.LabelColorDim, "Interface Misc");
        ImGui.Indent();
        var enableSounds = config.EnableSounds;
        if (ImGui.Checkbox("Enable Interface Sounds", ref enableSounds))
        {
            config.EnableSounds = enableSounds; changed = true; Sound.PlayClick();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Toggle UI sound effects like clicks, success chimes, and menu opens.");
        ImGui.Unindent();

        UI.Section("Data Cache");
        if (_profileCacheSize < 0) _profileCacheSize = Globals.Cache.GetDiskCacheSize();
        if (_imageCacheSize < 0) _imageCacheSize = Globals.Images.GetDiskCacheSize();

        ImGui.Text("Profiles:"); ImGui.SameLine();
        ImGui.TextColored(Theme.TextMuted, FormatBytes(_profileCacheSize));
        ImGui.SameLine();
        if (ImGui.SmallButton("Clear##profiles")) { _confirmCacheClear = true; Sound.PlayOpen(); }

        ImGui.Text("Images:  "); ImGui.SameLine();
        ImGui.TextColored(Theme.TextMuted, FormatBytes(_imageCacheSize));
        ImGui.SameLine();
        if (ImGui.SmallButton("Clear##images")) { _confirmImageCacheClear = true; Sound.PlayOpen(); }

        if (_confirmCacheClear || _confirmImageCacheClear)
        {
            ImGui.Spacing();
            ImGui.TextColored(Theme.Warning, "Are you sure? This will force data to re-download.");
            if (ImGui.Button("Confirm Clear"))
            {
                if (_confirmCacheClear) Globals.Cache.ClearDiskCache();
                if (_confirmImageCacheClear) Globals.Images.ClearDiskCache();
                _confirmCacheClear = _confirmImageCacheClear = false;
                InvalidateSizeCache();
                Sound.PlaySuccess();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) { _confirmCacheClear = _confirmImageCacheClear = false; Sound.PlayCancel(); }
        }

        if (changed) config.Save();
    }

    static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    public static void InvalidateSizeCache()
    {
        _profileCacheSize = -1;
        _imageCacheSize = -1;
    }
}
