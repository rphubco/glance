namespace Glance.Services;

using Dalamud.Utility;
using Glance.Core;
using Glance.Models;
using Glance.Utils;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

public sealed class AuthService : IDisposable
{
    const string BaseUrl = "https://rphub.co/api/glance/auth";
    const int MaxPolls = 150;
    const int PollMs = 2000;

    readonly SemaphoreSlim _lock = new(1, 1);
    CancellationTokenSource? _cts;

    bool _hasAttempted;
    public bool IsFetching { get; private set; }
    public bool BeaconFetchFailed { get; private set; }
    public string? LastBeaconError { get; private set; }
    public bool IsAuthenticated { get; private set; }
    public string CurrentJwt
    {
        get
        {
            var hash = IdentityHash.Hash(Globals.PlayerState.ContentId);
            return !string.IsNullOrEmpty(hash) && Globals.Config.CharacterTokens?.TryGetValue(hash, out var jwt) == true ? jwt : string.Empty;
        }
    }
    public bool IsReady => IsAuthenticated && !string.IsNullOrEmpty(CurrentJwt);
    public bool IsValidating { get; private set; }
    public AuthUser? CurrentUser { get; private set; }
    public bool IsWaitingForBrowser { get; private set; }
    string? _lastAttemptedHash;

    public async Task RevalidateAsync()
    {
        if (string.IsNullOrEmpty(Globals.Config.ApiKey)) { IsAuthenticated = false; CurrentUser = null; return; }

        IsValidating = true;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/validate");
            req.Headers.Add("X-API-Key", Globals.Config.ApiKey);

            using var res = await Globals.Http.SendAsync(req);
            res.EnsureSuccessStatusCode();

            var data = await res.Content.ReadFromJsonAsync<AuthValidateResponse>();
            if (data?.Valid != true) { Logout(); return; }

            CurrentUser = data.User;
            IsAuthenticated = true;
            IsValidating = false;
            _ = Globals.Profiles.FetchProfilesAsync();
        }
        catch { IsAuthenticated = false; }
        finally { IsValidating = false; }
    }

    public async Task EnsureBeaconTokenAsync(bool force = false)
    {
        if (IsFetching) return;

        var cid = Globals.PlayerState.ContentId;
        if (cid == 0) return;

        var hash = IdentityHash.Hash(cid);

        await _lock.WaitAsync();
        try
        {
            if (IsFetching) return;

            if (hash != _lastAttemptedHash)
            {
                _hasAttempted = false;
                BeaconFetchFailed = false;
                LastBeaconError = null;
                _lastAttemptedHash = hash;
            }

            if (!string.IsNullOrEmpty(CurrentJwt)) return;
            if (!force) return;

            IsFetching = true;

            var success = await FetchBeaconToken(hash);
            if (success) _hasAttempted = true;
        }
        finally
        {
            IsFetching = false;
            _lock.Release();
        }
    }

    async Task<bool> FetchBeaconToken(string hash)
    {
        try
        {
            var (name, world) = await Globals.Framework.RunOnFrameworkThread(() =>
                (Globals.Objects.LocalPlayer?.Name.TextValue,
                 Globals.Objects.LocalPlayer?.HomeWorld.Value.Name.ExtractText()));

            if (name == null || world == null) return false;

            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"{BaseUrl}/beacon-token?name={Uri.EscapeDataString(name)}&world={Uri.EscapeDataString(world)}&cid={hash}");
            req.Headers.Add("X-API-Key", Globals.Config.ApiKey);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var res = await Globals.Http.SendAsync(req, cts.Token);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                if (res.StatusCode == System.Net.HttpStatusCode.Conflict) return true;

                Globals.Log.Error($"[Auth] Beacon fetch failed: {res.StatusCode} - {body}");

                LastBeaconError = res.StatusCode == (System.Net.HttpStatusCode)429 ? "RATE_LIMITED" :
                                 (body.Contains("error") ? Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(body)?.error : res.StatusCode.ToString());
                BeaconFetchFailed = true;
                return true;
            }

            var data = Newtonsoft.Json.JsonConvert.DeserializeObject<BeaconTokenResponse>(body);

            if (string.IsNullOrEmpty(data?.Jwt))
            {
                Globals.Log.Error($"[Auth] Beacon response had no JWT");
                return true;
            }

            Globals.Config.CharacterTokens[hash] = data.Jwt;
            Globals.Config.Save();

            BeaconFetchFailed = false;
            LastBeaconError = null;
            return true;
        }
        catch (OperationCanceledException)
        {
            LastBeaconError = "Please try again in a moment.";
            BeaconFetchFailed = true;
            return true;
        }
        catch (Exception ex)
        {
            Globals.Log.Error(ex, "[Auth] Fetch failed");
            LastBeaconError = "Connection Error";
            BeaconFetchFailed = true;
            return true;
        }
    }

    public Task RefreshBeaconTokenAsync() => EnsureBeaconTokenAsync(true);

    public void StartAuthProcess()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new();
        IsWaitingForBrowser = true;
        var rid = $"req_{Guid.NewGuid():N}"[..20];
        var q = HttpUtility.ParseQueryString("");
        q["q"] = rid;

        if (Globals.Objects.LocalPlayer is { } p)
        {
            q["name"] = p.Name.TextValue;
            q["world"] = p.HomeWorld.Value.Name.ExtractText();
            q["cid"] = IdentityHash.Hash(Globals.PlayerState.ContentId);
            q["race"] = Globals.PlayerState.Race.Value.Masculine.ExtractText();
            q["clan"] = Globals.PlayerState.Tribe.Value.Masculine.ExtractText();
        }

        Util.OpenLink($"https://rphub.co/activate?{q}");
        _ = Poll(rid, _cts.Token);
    }

    async Task Poll(string rid, CancellationToken ct)
    {
        for (var i = 0; i < MaxPolls && !IsAuthenticated; i++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/poll?q={rid}");
                using var res = await Globals.Http.SendAsync(req, ct);

                var body = await res.Content.ReadAsStringAsync(ct);

                if (!res.IsSuccessStatusCode)
                {
                    Globals.Log.Error($"[Auth] Poll Error {res.StatusCode}: {body}");
                    await Task.Delay(PollMs, ct);
                    continue;
                }

                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<AuthPollResponse>(body);

                if (data?.Status == "approved" && data.ApiKey != null)
                {
                    Globals.Log.Information("[Auth] Approval received!");
                    Globals.Config.ApiKey = data.ApiKey;
                    Globals.Config.Save();
                    IsAuthenticated = true;
                    IsWaitingForBrowser = false;
                    CurrentUser = data.User;
                    await Globals.Profiles.FetchProfilesAsync();
                    await EnsureBeaconTokenAsync(true);
                    return;
                }

                if (data?.Status == "expired")
                {
                    Globals.Log.Error("[Auth] Request expired on server.");
                    return;
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Globals.ChatGui.PrintError("[Glance] Connection timed out. Your Firewall or Antivirus may be blocking the plugin.");
                Globals.Log.Error("[Auth] Polling timed out. The network request failed to reach the server in time.");
                return;
            }
            catch (Exception ex)
            {
                Globals.Log.Error(ex, "[Auth] Exception during polling logic");
            }
            await Task.Delay(PollMs, ct);
        }
        IsWaitingForBrowser = false;
    }

    record BeaconTokenResponse(string Jwt);

    public void ResetSession() { _cts?.Cancel(); Globals.Profiles.Reset(); _hasAttempted = false; BeaconFetchFailed = false; LastBeaconError = null; _lastAttemptedHash = null; }

    public void Logout()
    {
        IsAuthenticated = false;
        _hasAttempted = false;
        CurrentUser = null;
        Globals.Config.ApiKey = "";
        Globals.Config.Save();
        ResetSession();
    }

    public void CancelAuth()
    {
        _cts?.Cancel();
        IsWaitingForBrowser = false;
    }

    public void Dispose() { _cts?.Cancel(); _cts?.Dispose(); _lock.Dispose(); }
}
