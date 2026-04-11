namespace Glance.Services;

using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.Toast;
using FFXIVClientStructs.FFXIV.Client.Game;
using Glance.Core;
using Glance.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

public sealed class BeaconService : IDisposable
{
    const string Endpoint = "https://beacon.rphub.co/beacon";
    const int IntervalSec = 60;
    const uint AfkStatusId = 17;

    DateTime _lastPing;
    string _lastZone = "";
    bool _busy;
    uint _lastOnlineStatus;

    public unsafe void UpdateLocation()
    {
        if (!Globals.Config.BeaconEnabled) return;

        var player = Globals.ObjectTable?.LocalPlayer;
        if (player != null)
            _lastOnlineStatus = player.OnlineStatus.RowId;

        var territoryId = Globals.ClientState.TerritoryType;
        var worldId = Globals.PlayerState?.CurrentWorld.RowId ?? 0;

        var hm = HousingManager.Instance();
        if (hm != null)
        {
            var indoorId = hm->GetCurrentIndoorHouseId().Id;
            var houseId = hm->GetCurrentHouseId().Id;
            const ulong InvalidId = ulong.MaxValue;

            if (indoorId != 0 && indoorId != InvalidId)
            {
                _lastZone = $"{worldId}:H{indoorId}";
                return;
            }
            if (houseId != 0 && houseId != InvalidId)
            {
                _lastZone = $"{worldId}:E{houseId}";
                return;
            }
        }
        _lastZone = $"{worldId}:{territoryId}";
    }

    bool IsPlayerAfk() => _lastOnlineStatus == AfkStatusId;

    public async Task TickAsync()
    {
        if (!Globals.Config.BeaconEnabled) return;
        if (!Globals.ClientState.IsLoggedIn || !Globals.Auth.IsAuthenticated || Globals.PlayerState == null || _busy) return;
        if (string.IsNullOrEmpty(_lastZone) || _lastZone.StartsWith("0:")) return;
        if (IsPlayerAfk()) return;

        await Globals.Auth.EnsureBeaconTokenAsync();

        if (string.IsNullOrEmpty(Globals.Auth.CurrentJwt)) return;
        if ((DateTime.UtcNow - _lastPing).TotalSeconds < IntervalSec) return;

        var world = Globals.PlayerState.CurrentWorld.ValueNullable?.Name.ExtractText();
        if (string.IsNullOrEmpty(world)) return;
        await Heartbeat(world, _lastZone);
    }

    async Task Heartbeat(string world, string zone)
    {
        _busy = true;
        var sw = Stopwatch.StartNew();
        try
        {
            var name = Globals.PlayerState.CharacterName ?? "";

            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", Globals.Auth.CurrentJwt) },
                Content = JsonContent.Create(new
                {
                    name,
                    world,
                    location = zone,
                    profileVersion = Globals.Cache.MyLocalVersion,
                    hidden = !Globals.Config.BeaconLocationSharing,
                    nameplateIcon = Globals.Config.NameplateCustomIconId,
                })
            };

            var res = await Globals.Http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            _lastPing = DateTime.UtcNow;
            sw.Stop();

            if (res.StatusCode == HttpStatusCode.Forbidden)
            {
                Globals.Log.Warning($"[Beacon] Forbidden ({sw.ElapsedMilliseconds}ms): {body}");
                if (body != "Banned") await Globals.Auth.RefreshBeaconTokenAsync();
                return;
            }

            if (res.StatusCode == HttpStatusCode.TooManyRequests)
            {
                Globals.Log.Warning($"[Beacon] Rate limited ({sw.ElapsedMilliseconds}ms)");
                return;
            }

            if (res.IsSuccessStatusCode && JsonSerializer.Deserialize<List<NeighborResponse>>(body) is { } neighbors)
            {
                Globals.Log.Debug($"[Beacon] Success: {neighbors.Count} neighbors returned ({sw.ElapsedMilliseconds}ms)");

                Globals.Cache.UpdateFromBeacon(neighbors);
                _ = TryRefreshTooltip();
            }
            else
            {
                Globals.Log.Warning($"[Beacon] Failed {res.StatusCode} ({sw.ElapsedMilliseconds}ms)");
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            Globals.Log.Error($"[Beacon] Error ({sw.ElapsedMilliseconds}ms): {ex.Message}");
            _lastPing = DateTime.UtcNow;
        }
        finally { _busy = false; }
    }

    async Task TryRefreshTooltip()
    {
        var tt = Globals.Tooltip;
        if (tt?.IsOpen != true || string.IsNullOrEmpty(tt.CurrentTargetName)) return;

        if (Globals.Cache.IsStaleNeighbor(tt.CurrentTargetName, tt.CurrentTargetWorld))
            await tt.RefreshCurrentTargetAsync();
    }

    public void Dispose() { }
}
