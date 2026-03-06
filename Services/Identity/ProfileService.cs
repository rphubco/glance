namespace Glance.Services;

using Dalamud.Plugin.Services;
using Glance.Core;
using Glance.Models;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Web;

public sealed class ProfileService
{
    const string BaseUrl = "https://rphub.co/api/glance/characters";
    const string BeaconUrl = "https://beacon.rphub.co";

    public ProfilesResponse? Data { get; private set; }
    public string? PendingActiveId { get; private set; }
    public string? ActiveProfileId => PendingActiveId ?? Data?.ActiveProfileId;
    ulong _lastContentId;

    public async Task FetchProfilesAsync()
    {
        if (!Globals.Auth.IsAuthenticated) { Globals.Log.Info("[Profiles] FetchProfilesAsync: not authenticated, skipping"); return; }
        string? cid = null, name = null, world = null;
        ulong contentId = 0;
        await Globals.Framework.RunOnFrameworkThread(() =>
        {
            if (Globals.Objects.LocalPlayer is not { } p) return;
            contentId = Globals.PlayerState.ContentId;
            cid = contentId.ToString();
            name = p.Name.TextValue;
            world = p.HomeWorld.Value.Name.ExtractText();
        });

        Globals.Log.Info($"[Profiles] FetchProfilesAsync: cid={cid}, name={name}, world={world}");

        try
        {
            var q = HttpUtility.ParseQueryString("");
            if (cid != null) { q["contentId"] = cid; q["name"] = name; q["world"] = world; }
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/mine?{q}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Globals.Config.ApiKey);
            var res = await Globals.Http.SendAsync(req);

            Globals.Log.Info($"[Profiles] FetchProfilesAsync response: {res.StatusCode}");

            if (!res.IsSuccessStatusCode) return;
            var data = await res.Content.ReadFromJsonAsync<ProfilesResponse>();

            Globals.Log.Info($"[Profiles] FetchProfilesAsync result: Characters={data?.Characters?.Length ?? -1}, ActiveId={data?.ActiveProfileId}");

            if (data?.ActiveProfileId == PendingActiveId) PendingActiveId = null;
            Data = data;

            if (contentId != 0 && contentId != _lastContentId)
            {
                _lastContentId = contentId;
                if (Globals.Config.ActiveProfiles.TryGetValue(contentId, out var saved) &&
                    saved != Data?.ActiveProfileId &&
                    Data?.Characters?.Any(p => p.Id == saved) == true)
                {
                    _ = SetActiveProfileAsync(saved);
                }
                else if (Data?.ActiveProfileId != null)
                {
                    Globals.Config.ActiveProfiles[contentId] = Data.ActiveProfileId;
                    Globals.Config.Save();
                }
            }
        }
        catch { }
    }

    public async Task<bool> SetGhostModeAsync(bool enabled)
    {
        if (!Globals.Auth.IsReady) return false;

        try
        {
            await Globals.Auth.EnsureBeaconTokenAsync();

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BeaconUrl}/profile/ghostmode")
            {
                Content = JsonContent.Create(new { enabled }),
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", Globals.Auth.CurrentJwt) }
            };

            var res = await Globals.Http.SendAsync(req);
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync();
                Globals.Log.Error($"[GhostMode] {res.StatusCode} - {body}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Globals.Log.Error(ex, "[GhostMode] Failed to toggle");
            return false;
        }
    }

    public async Task SetActiveProfileAsync(string profileId)
    {
        if (!Globals.Auth.IsReady) return;

        ulong contentId = 0;
        string? cid = null;

        await Globals.Framework.RunOnFrameworkThread(() =>
        {
            contentId = Globals.PlayerState.ContentId;
            if (contentId != 0) cid = contentId.ToString();
        });

        if (cid == null) return;

        PendingActiveId = profileId;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BeaconUrl}/profile/activate")
            {
                Content = JsonContent.Create(new { contentId = cid, characterId = profileId }),
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", Globals.Auth.CurrentJwt) }
            };

            var res = await Globals.Http.SendAsync(req);

            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync();
                Globals.Log.Error($"[SetProfile] {res.StatusCode} - {body}");
                PendingActiveId = null;
                return;
            }

            Globals.Config.ActiveProfiles[contentId] = profileId;
            Globals.Config.Save();
            await FetchProfilesAsync();
        }
        catch (Exception ex)
        {
            Globals.Log.Error(ex, "[SetProfile] Failed");
            PendingActiveId = null;
        }
    }

    public void Reset()
    {
        Data = null;
        PendingActiveId = null;
        _lastContentId = 0;
    }
}
