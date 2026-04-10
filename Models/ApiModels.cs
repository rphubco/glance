namespace Glance.Models;

using System.Collections.Generic;
using System.Text.Json.Serialization;

public record AuthUser([property: JsonPropertyName("id")] string? Id, [property: JsonPropertyName("name")] string? Name);
public record AuthValidateResponse([property: JsonPropertyName("valid")] bool Valid, [property: JsonPropertyName("user")] AuthUser? User);
public record AuthPollResponse([property: JsonPropertyName("status")] string? Status, [property: JsonPropertyName("apiKey")] string? ApiKey, [property: JsonPropertyName("user")] AuthUser? User);
public record ProfileCharacter([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("world")] string? World, [property: JsonPropertyName("avatar")] string? Avatar);
public record ProfilesResponse([property: JsonPropertyName("characters")] ProfileCharacter[] Characters, [property: JsonPropertyName("count")] int Count, [property: JsonPropertyName("current_verified")] bool CurrentVerified, [property: JsonPropertyName("active_profile_id")] string? ActiveProfileId);
public record ProfileSummary([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("avatar")] string? Avatar, [property: JsonPropertyName("shortDescription")] string? ShortDescription, [property: JsonPropertyName("race")] string? Race, [property: JsonPropertyName("freeCompany")] string? FreeCompany);
public record SummaryResult([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("world")] string World, [property: JsonPropertyName("profile")] ProfileSummary? Profile);
public record SummaryBatchResponse([property: JsonPropertyName("summaries")] SummaryResult[] Summaries);
public record ManifestEntry([property: JsonPropertyName("profileId")] string ProfileId, [property: JsonPropertyName("updatedAt")] string? UpdatedAt, [property: JsonPropertyName("currentProfileId")] string? CurrentProfileId);
public record ManifestResponse([property: JsonPropertyName("manifest")] Dictionary<string, ManifestEntry> Manifest);
public record MetaResult([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("world")] string World, [property: JsonPropertyName("profileId")] string ProfileId, [property: JsonPropertyName("isStale")] bool IsStale, [property: JsonPropertyName("reason")] string? Reason, [property: JsonPropertyName("currentProfileId")] string? CurrentProfileId, [property: JsonPropertyName("updatedAt")] string? UpdatedAt);
public record MetaBatchResponse([property: JsonPropertyName("profiles")] MetaResult[] Profiles);
public record NeighborResponse([property: JsonPropertyName("Name")] string Name, [property: JsonPropertyName("ProfileId")] string ProfileId, [property: JsonPropertyName("Version")] long Version, [property: JsonPropertyName("NameplateIcon")] int NameplateIcon = 0);
public class ProfileLookupResponse { public int Id { get; set; } public long Version { get; set; } public ProfileData? Data { get; set; } public bool Unverified { get; set; } = false; }
public class BatchLookupResponse { public Dictionary<string, ProfileLookupResponse?> Results { get; set; } = new(); }

