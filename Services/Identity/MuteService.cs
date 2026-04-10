namespace Glance.Services;

using Glance.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public class MuteService
{
    const string BaseUrl = "https://rphub.co/api/glance/mutes";

    Dictionary<int, string> _muted = new();
    bool _fetching;

    public bool IsMuted(int characterId) => _muted.ContainsKey(characterId);
    public bool IsMuted(string? profileId)
    {
        if (string.IsNullOrEmpty(profileId)) return false;
        return int.TryParse(profileId, out var id) && _muted.ContainsKey(id);
    }

    public int Count => _muted.Count;
    public IReadOnlyDictionary<int, string> All => _muted;

    public async Task FetchAsync()
    {
        if (_fetching) return;
        _fetching = true;

        try
        {
            var apiKey = Globals.Config.ApiKey;
            if (string.IsNullOrEmpty(apiKey)) return;

            using var req = new HttpRequestMessage(HttpMethod.Get, BaseUrl);
            req.Headers.Add("X-API-Key", apiKey);

            using var res = await Globals.Http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return;

            var data = System.Text.Json.JsonSerializer.Deserialize<MuteListResponse>(await res.Content.ReadAsStringAsync());
            if (data?.Characters != null)
            {
                var dict = new Dictionary<int, string>();
                foreach (var c in data.Characters)
                    dict[c.Id] = c.Name;
                _muted = dict;
            }
        }
        catch { }
        finally { _fetching = false; }
    }

    public async Task<bool> MuteAsync(int characterId, string? name = null)
    {
        try
        {
            var apiKey = Globals.Config.ApiKey;
            if (string.IsNullOrEmpty(apiKey)) return false;

            using var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
            req.Headers.Add("X-API-Key", apiKey);
            req.Content = JsonContent.Create(new { character_id = characterId });

            using var res = await Globals.Http.SendAsync(req);
            if (res.IsSuccessStatusCode)
            {
                _muted[characterId] = name ?? $"Character #{characterId}";
                return true;
            }
        }
        catch { }
        return false;
    }

    public async Task<bool> UnmuteAsync(int characterId)
    {
        try
        {
            var apiKey = Globals.Config.ApiKey;
            if (string.IsNullOrEmpty(apiKey)) return false;

            using var req = new HttpRequestMessage(HttpMethod.Delete, BaseUrl);
            req.Headers.Add("X-API-Key", apiKey);
            req.Content = JsonContent.Create(new { character_id = characterId });

            using var res = await Globals.Http.SendAsync(req);
            if (res.IsSuccessStatusCode)
            {
                _muted.Remove(characterId);
                return true;
            }
        }
        catch { }
        return false;
    }
}

class MuteListResponse
{
    [JsonPropertyName("characters")]
    public MuteEntry[] Characters { get; set; } = [];
}

class MuteEntry
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}
