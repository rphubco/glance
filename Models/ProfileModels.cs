namespace Glance.Models;

using System.Collections.Generic;
using System.Text.Json.Serialization;

public class ProfileData
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("pageImage")] public string? PageImage { get; set; }
    [JsonPropertyName("about")] public List<AboutField>? About { get; set; }
    [JsonPropertyName("details")] public string? Details { get; set; }
    [JsonPropertyName("race")] public string? Race { get; set; }
    [JsonPropertyName("clan")] public string? Clan { get; set; }
    [JsonPropertyName("hooks")] public List<HookData>? Hooks { get; set; }
    [JsonPropertyName("commenting")] public bool Commenting { get; set; }
    [JsonPropertyName("privacy")] public string? Privacy { get; set; }
    [JsonPropertyName("playerNotes")] public string? PlayerNotes { get; set; } // ooc
    [JsonPropertyName("currentStatus")] public string? CurrentStatus { get; set; } // ic current >:3c
    [JsonPropertyName("location")] public string? Location { get; set; }
    [JsonPropertyName("freeCompany")] public string? FreeCompany { get; set; }
    [JsonPropertyName("glances")] public List<GlanceData>? Glances { get; set; }
}

public class HookData
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
}

public class AboutField
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("label")] public string? Label { get; set; }
    [JsonPropertyName("input")] public string? Input { get; set; }
}

public class GlanceData
{
    [JsonPropertyName("iconId")] public uint IconId { get; set; }
    [JsonPropertyName("label")] public string? Label { get; set; }
    [JsonPropertyName("value")] public string? Value { get; set; }
}
