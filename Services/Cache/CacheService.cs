namespace Glance.Services;

using Glance.Core;
using Glance.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

public sealed class CacheService
{
    const int VersionCheckMins = 2;
    const string BaseUrl = "https://beacon.rphub.co/profile";

    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    readonly ConcurrentDictionary<string, CachedProfile> _profiles = new();
    readonly ConcurrentDictionary<string, bool> _fetching = new();
    readonly ConcurrentDictionary<string, NeighborInfo> _neighbors = new();
    readonly ConcurrentDictionary<string, DateTime> _versionChecks = new();
    readonly string _cacheDir;

    public long MyLocalVersion { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public IEnumerable<(string Key, CachedProfile Profile)> GetAllCached()
    => _profiles.Select(kv => (kv.Key, kv.Value));
    public CacheService()
    {
        _cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XIVLauncher", "pluginConfigs", "Glance", "ProfileCache");
        Directory.CreateDirectory(_cacheDir);
        LoadDiskCache();
    }

    void LoadDiskCache()
    {
        if (!Directory.Exists(_cacheDir)) return;
        foreach (var file in Directory.GetFiles(_cacheDir, "*.json"))
        {
            try
            {
                var profile = JsonSerializer.Deserialize<CachedProfile>(File.ReadAllText(file), Json);
                if (profile != null)
                    _profiles[Path.GetFileNameWithoutExtension(file).Replace("_", "@").ToLowerInvariant()] = profile;
            }
            catch { }
        }
    }

    void SaveToDisk(string key, CachedProfile profile)
    {
        try { File.WriteAllText(Path.Combine(_cacheDir, $"{key.Replace("@", "_").ToLowerInvariant()}.json"), JsonSerializer.Serialize(profile, Json)); }
        catch { }
    }

    public void ClearDiskCache()
    {
        try
        {
            if (Directory.Exists(_cacheDir)) Directory.Delete(_cacheDir, true);
            Directory.CreateDirectory(_cacheDir);
            _profiles.Clear();
        }
        catch { }
    }

    public long GetDiskCacheSize()
    {
        try
        {
            if (!Directory.Exists(_cacheDir)) return 0;
            long size = 0;
            foreach (var file in Directory.GetFiles(_cacheDir, "*.json"))
                size += new FileInfo(file).Length;
            return size;
        }
        catch { return 0; }
    }

    public IEnumerable<string> GetCachedKeysForWorld(string world)
    {
        var suffix = $"@{world}".ToLowerInvariant();
        return _profiles.Keys.Where(k => k.EndsWith(suffix));
    }

    public string? FindFullNameMatching(string shortenedName, string world)
    {
        var suffix = $"@{world}".ToLowerInvariant();

        foreach (var key in _profiles.Keys)
        {
            if (!key.EndsWith(suffix)) continue;
            var fullName = key[..^suffix.Length];
            if (NameMatches(shortenedName, fullName)) return fullName;
        }
        return null;
    }

    static bool NameMatches(string shortened, string full)
    {
        if (shortened.Equals(full, StringComparison.OrdinalIgnoreCase))
            return true;

        var sParts = shortened.Split(' ');
        var fParts = full.Split(' ');
        if (sParts.Length != 2 || fParts.Length != 2) return false;

        return PartMatches(sParts[0], fParts[0]) && PartMatches(sParts[1], fParts[1]);
    }

    static bool PartMatches(string part, string fullPart)
    {
        if (part.Equals(fullPart, StringComparison.OrdinalIgnoreCase)) return true;
        return part.Length == 2 && part[1] == '.' &&
               char.ToUpperInvariant(part[0]) == char.ToUpperInvariant(fullPart[0]);
    }

    public void UpdateFromBeacon(List<NeighborResponse> list)
    {
        _neighbors.Clear();
        foreach (var n in list)
        {
            if (string.IsNullOrWhiteSpace(n.Name)) continue;

            var parts = n.Name.Split('@');
            if (parts.Length < 2) continue;

            var cleanName = parts[0].Trim();
            var cleanWorld = parts[1].Trim();
            var key = $"{cleanName}@{cleanWorld}";

            _neighbors[key] = new NeighborInfo(n.ProfileId, n.Version, n.NameplateIcon);
        }
    }

    public bool IsBeaconNeighbor(string name, string world)
    {
        var searchKey = $"{name}@{world}";
        return _neighbors.ContainsKey(searchKey) ||
               _neighbors.Keys.Any(k => k.Equals(searchKey, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsStaleNeighbor(string name, string world)
    {
        var key = $"{name}@{world}".ToLowerInvariant();
        return _neighbors.TryGetValue(key, out var n) && _profiles.TryGetValue(key, out var c) && c.Version < n.Version;
    }

    public int GetNeighborNameplateIcon(string name, string world)
    {
        var key = $"{name}@{world}".ToLowerInvariant();
        return _neighbors.TryGetValue(key, out var n) ? n.NameplateIcon : 0;
    }

    public CachedProfile? GetProfile(string name, string world) => _profiles.GetValueOrDefault($"{name}@{world}".ToLowerInvariant());

    public async Task FetchBatchAsync(IEnumerable<(string Name, string World)> profiles)
    {
        var token = Globals.Auth.CurrentJwt;
        if (string.IsNullOrEmpty(token)) return;

        var toFetch = new List<(string Name, string World)>();
        foreach (var (name, world) in profiles)
        {
            var k = $"{name}@{world}".ToLowerInvariant();
            if (_fetching.ContainsKey(k)) continue;
            if (_profiles.ContainsKey(k)) continue;
            _fetching[k] = true;
            toFetch.Add((name, world));
        }

        if (toFetch.Count == 0) return;

        try
        {
            var payload = new { profiles = toFetch.Select(p => new { name = p.Name, world = p.World }).ToArray() };
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/batch")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
                Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
            };

            var res = await Globals.Http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return;

            var batch = JsonSerializer.Deserialize<BatchLookupResponse>(await res.Content.ReadAsStringAsync(), Json);
            if (batch?.Results == null) return;

            foreach (var (pk, result) in batch.Results)
            {
                var k = pk.ToLowerInvariant();

                if (result == null || result.Data == null)
                {
                    _profiles[k] = new CachedProfile(null, 0, null, DateTime.UtcNow, result?.Unverified == true);
                    continue;
                }

                var profile = new CachedProfile(result.Id, result.Version, result.Data, DateTime.UtcNow, false);
                _profiles[k] = profile;
                _versionChecks[k] = DateTime.UtcNow;
                SaveToDisk(k, profile);
            }
        }
        catch (Exception e) { Globals.Log.Error($"[ProfileFetch] Batch error: {e.Message}"); }
        finally
        {
            foreach (var (name, world) in toFetch)
                _fetching.TryRemove($"{name}@{world}".ToLowerInvariant(), out _);
        }
    }

    public async Task<CachedProfile?> FetchProfileAsync(string name, string world)
    {
        var key = $"{name}@{world}".ToLowerInvariant();
        if (_fetching.ContainsKey(key)) return _profiles.GetValueOrDefault(key);

        if (_profiles.TryGetValue(key, out var cached))
        {
            if (_neighbors.TryGetValue(key, out var neighbor))
            {
                if (cached.Version >= neighbor.Version) return cached;
            }
            else
            {
                if (_versionChecks.TryGetValue(key, out var last) && DateTime.UtcNow - last < TimeSpan.FromMinutes(VersionCheckMins))
                    return cached;

                var serverVer = await GetServerVersionAsync(name, world);
                _versionChecks[key] = DateTime.UtcNow;
                if (serverVer == null || cached.Version >= serverVer) return cached;
            }
        }

        return await DoFetch(key, name, world, false);
    }

    public async Task<CachedProfile?> RefreshProfileAsync(string name, string world)
    {
        var key = $"{name}@{world}".ToLowerInvariant();
        _versionChecks.TryRemove(key, out _);
        return await DoFetch(key, name, world, true);
    }

    async Task<CachedProfile?> DoFetch(string key, string name, string world, bool force)
    {
        if (!force) _fetching[key] = true;
        try
        {
            var token = Globals.Auth.CurrentJwt;
            var url = $"{BaseUrl}/lookup?name={Uri.EscapeDataString(name)}&world={Uri.EscapeDataString(world)}";
            Globals.Log.Debug($"[ProfileFetch] Requesting: {url}");
            using var req = new HttpRequestMessage(HttpMethod.Get,
                string.IsNullOrEmpty(token) ? $"{BaseUrl}/public?name={Uri.EscapeDataString(name)}&world={Uri.EscapeDataString(world)}" : url);

            if (!string.IsNullOrEmpty(token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var res = await Globals.Http.SendAsync(req);
            if (res.StatusCode == System.Net.HttpStatusCode.NotFound || res.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                var emptyProfile = new CachedProfile(null, 0, null, DateTime.UtcNow, true);
                _profiles[key] = emptyProfile;

                var filePath = Path.Combine(_cacheDir, $"{key.Replace("@", "_").ToLowerInvariant()}.json");
                if (File.Exists(filePath)) File.Delete(filePath);

                return emptyProfile;
            }
            if (!res.IsSuccessStatusCode) return _profiles.GetValueOrDefault(key);

            var result = JsonSerializer.Deserialize<ProfileLookupResponse>(await res.Content.ReadAsStringAsync(), Json);
            if (result == null) return _profiles.GetValueOrDefault(key);

            if (result.Unverified)
            {
                var unverifiedProfile = new CachedProfile(null, 0, null, DateTime.UtcNow, true);
                _profiles[key] = unverifiedProfile;
                return unverifiedProfile;
            }

            var existingProfile = _profiles.GetValueOrDefault(key);
            var existingVersion = existingProfile?.Version ?? 0;
            var wasUnverified = existingProfile?.Unverified ?? false;

            if (!force && !wasUnverified && result.Version <= existingVersion)
                return existingProfile;

            var profile = new CachedProfile(result.Id, result.Version, result.Data, DateTime.UtcNow, false);
            _profiles[key] = profile;
            _versionChecks[key] = DateTime.UtcNow;
            SaveToDisk(key, profile);
            return profile;
        }
        catch { return _profiles.GetValueOrDefault(key); }
        finally { if (!force) _fetching.TryRemove(key, out _); }
    }

    async Task<long?> GetServerVersionAsync(string name, string world)
    {
        try
        {
            var token = Globals.Auth.CurrentJwt;
            if (string.IsNullOrEmpty(token)) return null;

            using var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/version?name={Uri.EscapeDataString(name)}&world={Uri.EscapeDataString(world)}")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) }
            };

            var res = await Globals.Http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return null;
            return JsonSerializer.Deserialize<VersionResponse>(await res.Content.ReadAsStringAsync(), Json)?.Version;
        }
        catch { return null; }
    }

    public IEnumerable<(string Name, string World)> GetBeaconNeighbors()
    {
        foreach (var key in _neighbors.Keys)
        {
            int separator = key.LastIndexOf('@');
            if (separator > 0)
            {
                var name = key.Substring(0, separator);
                var world = key.Substring(separator + 1);
                yield return (name, world);
            }
        }
    }

    public void Reset()
    {
        _profiles.Clear();
        _neighbors.Clear();
        _versionChecks.Clear();
    }

    record VersionResponse(int Id, long Version);
}
