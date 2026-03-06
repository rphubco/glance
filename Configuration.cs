namespace Glance.Core;

using Dalamud.Configuration;
using System.Collections.Generic;
using System.Numerics;

public class Configuration : IPluginConfiguration
{
    public int Version { get; set; }
    public string ApiKey { get; set; } = "";
    public Dictionary<ulong, string> CharacterTokens { get; set; } = [];
    public Dictionary<ulong, string> ActiveProfiles { get; set; } = [];
    public bool Enabled { get; set; } = true;
    public bool ShowToolbox { get; set; } = true;
    public bool BeaconEnabled { get; set; } = true;
    public bool BeaconLocationSharing { get; set; } = true;
    public bool EnableNameplates { get; set; } = true;
    public bool GhostMode { get; set; } = false;
    public bool NameplateTintEnabled { get; set; } = true;
    public bool NameplateShowCustomNames { get; set; } = true;
    public bool NameplateShowCustomTitles { get; set; } = true;
    public bool PreferHonorificsTitles { get; set; } = false;
    public bool EnableSounds { get; set; } = true;
    public bool ChatReplacementEnabled { get; set; } = true;
    public int NameplateCustomIconId { get; set; }
    public bool HideEmptyProfiles { get; set; } = true;
    public bool CombatMode { get; set; } = false;
    public bool DutyMode { get; set; } = false;
    public bool ShowHooksInTooltip { get; set; } = true;
    public bool OnlyShowRoleplaying { get; set; } = false;
    public bool OnlyShowRenderedPlayers { get; set; } = false;
    public bool LockTargetProfile { get; set; } = false;
    public bool TooltipOnClickOnly { get; set; } = false;
    public bool NameplateIconOnlyWhenRoleplaying { get; set; } = false;
    public Vector4 NameplateVerifiedColor   { get; set; } = new(0.255f, 0f, 0.843f, 1f);
    public Vector4 NameplateUnverifiedColor { get; set; } = new(0.8f, 0.8f, 0.8f, 1f);
    public void Save() => Globals.Interface.SavePluginConfig(this);
}
