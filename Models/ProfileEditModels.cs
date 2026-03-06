namespace Glance.Models;

using System.Collections.Generic;
using System.Security.AccessControl;
using System.Text.Json.Serialization;
using System.Linq;
public class ProfileEditPayload
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("pageImage")] public string? PageImage { get; set; }
    [JsonPropertyName("details")] public string? Details { get; set; }
    [JsonPropertyName("race")] public string? Race { get; set; }
    [JsonPropertyName("clan")] public string? Clan { get; set; }
    [JsonPropertyName("freeCompany")] public string? FreeCompany { get; set; }
    [JsonPropertyName("playerNotes")] public string? PlayerNotes { get; set; }
    [JsonPropertyName("currentStatus")] public string? CurrentStatus { get; set; }
    [JsonPropertyName("location")] public string? Location { get; set; }
    [JsonPropertyName("commenting")] public bool Commenting { get; set; }
    [JsonPropertyName("privacy")] public string? Privacy { get; set; }
    [JsonPropertyName("about")] public List<AboutField> About { get; set; } = [];
    [JsonPropertyName("hooks")] public List<HookData> Hooks { get; set; } = [];
    [JsonPropertyName("glances")] public List<GlanceData>? Glances { get; set; }
}

public static class RaceData
{
    public const string CustomRace = "Custom";

    public static readonly string[] Races = [
        "Hyur", "Miqo'te", "Lalafell", "Roegadyn", "Au Ra", "Viera", "Hrothgar", "Elezen", CustomRace
    ];

    public static readonly Dictionary<string, string[]> Clans = new()
    {
        ["Hyur"] = ["Midlander", "Highlander"],
        ["Miqo'te"] = ["Seeker of the Sun", "Keeper of the Moon"],
        ["Lalafell"] = ["Plainsfolk", "Dunesfolk"],
        ["Roegadyn"] = ["Sea Wolf", "Hellsguard"],
        ["Au Ra"] = ["Raen", "Xaela"],
        ["Viera"] = ["Rava", "Veena"],
        ["Hrothgar"] = ["Helions", "The Lost"],
        ["Elezen"] = ["Wildwood", "Duskwight"],
        [CustomRace] = ["Custom"]
    };

    public static bool IsStandardRace(string? race) => race != null && Races.Contains(race) && race != CustomRace;
}
