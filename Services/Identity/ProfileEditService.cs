namespace Glance.Services;

using Glance.Core;
using Glance.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

public sealed class ProfileEditService
{
    const string UpdateEndpoint = "https://beacon.rphub.co/profile/update";
    const string CreateEndpoint = "https://beacon.rphub.co/profile/create";

    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    readonly string _draftFile;
    ProfileEditPayload? _original, _draft;
    string? _profileId;
    DateTime _lastAuto;
    bool _saving;

    byte[]? _pendingImg;
    string? _pendingImgName;
    bool _createMode;

    public ProfileEditPayload? Draft => _draft;
    public bool IsDirty => _draft != null && (_createMode || GetChanges().Count > 0 || _pendingImg != null);
    public bool IsSaving => _saving;
    public bool IsLoaded => _draft != null;
    public bool HasPendingImage => _pendingImg != null;
    public byte[]? PendingImageData => _pendingImg;
    public bool IsCreateMode => _createMode;
    public List<string> Errors { get; } = [];

    public ProfileEditService()
    {
        _draftFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XIVLauncher", "pluginConfigs", "Glance", "draft.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_draftFile)!);
    }

    public void Load(ProfileData? data, string? id)
    {
        _createMode = false;
        if (data == null) { _draft = _original = null; _profileId = null; ClearPendingImage(); return; }
        _profileId = id;

        if (TryLoadDraft(id, out var d)) { _draft = d; _original = FromData(data); return; }
        _draft = FromData(data);
        _original = Clone(_draft);
        ClearPendingImage();
    }

    public void StartCreate()
    {
        _createMode = true;
        _profileId = null;
        _original = null;
        ClearPendingImage();

        _draft = new ProfileEditPayload
        {
            Name = "", Description = "", PageImage = null, Details = "",
            Race = "", Clan = "", FreeCompany = "", PlayerNotes = "",
            CurrentStatus = "", Location = "", Commenting = true, Privacy = "Public",
            About = [], Hooks = [], Glances = []
        };
    }

    static ProfileEditPayload FromData(ProfileData d) => new()
    {
        Name = d.Name, Description = d.Description, PageImage = d.PageImage, Details = d.Details,
        Race = d.Race, Clan = d.Clan, FreeCompany = d.FreeCompany, PlayerNotes = d.PlayerNotes,
        CurrentStatus = d.CurrentStatus, Location = d.Location, Commenting = d.Commenting, Privacy = d.Privacy,
        About = d.About != null ? [.. d.About] : [], Hooks = d.Hooks != null ? [.. d.Hooks] : [],
        Glances = d.Glances != null ? d.Glances.ConvertAll(g => new GlanceData { IconId = g.IconId, Label = g.Label, Value = g.Value }) : []
    };

    Dictionary<string, object?> GetChanges()
    {
        var c = new Dictionary<string, object?>();
        if (_draft == null || _original == null) return c;

        if (_draft.Name != _original.Name) c["name"] = _draft.Name;
        if (_draft.Description != _original.Description) c["description"] = _draft.Description;
        if (_draft.PageImage != _original.PageImage) c["pageImage"] = _draft.PageImage;
        if (_draft.Details != _original.Details) c["details"] = _draft.Details;
        if (_draft.Race != _original.Race) c["race"] = _draft.Race;
        if (_draft.Clan != _original.Clan) c["clan"] = _draft.Clan;
        if (_draft.FreeCompany != _original.FreeCompany) c["freeCompany"] = _draft.FreeCompany;
        if (_draft.PlayerNotes != _original.PlayerNotes) c["playerNotes"] = _draft.PlayerNotes;
        if (_draft.CurrentStatus != _original.CurrentStatus) c["currentStatus"] = _draft.CurrentStatus;
        if (_draft.Location != _original.Location) c["location"] = _draft.Location;
        if (_draft.Commenting != _original.Commenting) c["commenting"] = _draft.Commenting;
        if (_draft.Privacy != _original.Privacy) c["privacy"] = _draft.Privacy;
        if (!ListsMatch(_draft.About, _original.About, (a, b) => a.Label == b.Label && a.Input == b.Input)) c["about"] = _draft.About;
        if (!ListsMatch(_draft.Hooks, _original.Hooks, (a, b) => a.Title == b.Title && a.Description == b.Description)) c["hooks"] = _draft.Hooks;
        if (!ListsMatch(_draft.Glances ?? [], _original.Glances ?? [], (a, b) => a.IconId == b.IconId && a.Label == b.Label && a.Value == b.Value))
            c["glances"] = _draft.Glances;

        return c;
    }

    static bool ListsMatch<T>(List<T> a, List<T> b, Func<T, T, bool> eq)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++) if (!eq(a[i], b[i])) return false;
        return true;
    }

    public bool Validate()
    {
        Errors.Clear();
        if (_draft == null) return false;

        if (string.IsNullOrWhiteSpace(_draft.Name)) Errors.Add("Name required");
        else if (_draft.Name.Length > 64) Errors.Add("Name too long");

        if (_createMode)
        {
            if (string.IsNullOrWhiteSpace(_draft.Description)) Errors.Add("Description required");
            if (string.IsNullOrWhiteSpace(_draft.Race) || _draft.Race == RaceData.CustomRace)
                Errors.Add("Race required (Please enter a custom race name)");
            if (_pendingImg == null && string.IsNullOrEmpty(_draft.PageImage)) Errors.Add("Image required");
        }

        if (_draft.Race?.Length > 32) Errors.Add("Race name too long");
        if (_draft.Clan?.Length > 32) Errors.Add("Clan name too long");
        if (_draft.Description?.Length > 128) Errors.Add("Description too long");
        if (_draft.Details?.Length > 4096) Errors.Add("Details too long");
        if (_draft.CurrentStatus?.Length > 256) Errors.Add("Status too long");
        if (_draft.PlayerNotes?.Length > 1024) Errors.Add("OOC too long");
        if (_draft.About.Count > 20) Errors.Add("Too many about fields");
        if (_draft.Hooks.Count > 10) Errors.Add("Too many hooks");

        for (var i = 0; i < _draft.About.Count; i++)
            if (string.IsNullOrWhiteSpace(_draft.About[i].Label) && !string.IsNullOrWhiteSpace(_draft.About[i].Input))
                Errors.Add($"About #{i + 1} needs label");

        for (var i = 0; i < _draft.Hooks.Count; i++)
            if (string.IsNullOrWhiteSpace(_draft.Hooks[i].Title))
                Errors.Add($"Hook #{i + 1} needs title");

        return Errors.Count == 0;
    }

    bool TryLoadDraft(string? id, out ProfileEditPayload? draft)
    {
        draft = null;
        if (id == null || !File.Exists(_draftFile)) return false;
        try
        {
            var saved = JsonSerializer.Deserialize<DraftFile>(File.ReadAllText(_draftFile), Json);
            if (saved?.Id != id || DateTime.UtcNow - saved.At > TimeSpan.FromHours(24)) { DeleteDraft(); return false; }
            draft = saved.Data;
            return true;
        }
        catch { DeleteDraft(); return false; }
    }

    public void AutoSave()
    {
        if (_draft == null || _profileId == null || !IsDirty || _createMode) return;
        if ((DateTime.UtcNow - _lastAuto).TotalSeconds < 30) return;
        try
        {
            File.WriteAllText(_draftFile, JsonSerializer.Serialize(new DraftFile { Id = _profileId, At = DateTime.UtcNow, Data = _draft }, Json));
            _lastAuto = DateTime.UtcNow;
        }
        catch { }
    }

    public void DeleteDraft() { try { if (File.Exists(_draftFile)) File.Delete(_draftFile); } catch { } }
    public bool HasDraft => File.Exists(_draftFile);

    public void AddHook() => _draft?.Hooks.Add(new HookData { Id = 0, Title = "", Description = "" });
    public void RemoveHook(int i) { if (_draft != null && i >= 0 && i < _draft.Hooks.Count) _draft.Hooks.RemoveAt(i); }
    public void AddAbout() => _draft?.About.Add(new AboutField { Id = null, Label = "", Input = "" });
    public void RemoveAbout(int i) { if (_draft != null && i >= 0 && i < _draft.About.Count) _draft.About.RemoveAt(i); }

    public void Discard()
    {
        if (_createMode) { _draft = null; _createMode = false; }
        else if (_original != null) _draft = Clone(_original);
        ClearPendingImage();
        DeleteDraft();
    }

    public (bool Ok, string? Err) StageImage(byte[] data, string fileName)
    {
        if (data.Length > 5 * 1024 * 1024) return (false, "File too large (5MB max)");
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".gif")) return (false, "Invalid file type");
        _pendingImg = data;
        _pendingImgName = fileName;
        return (true, null);
    }

    public void ClearPendingImage() { _pendingImg = null; _pendingImgName = null; }

    public async Task<(bool Ok, string? Err)> SaveAsync()
    {
        if (_draft == null || _saving) return (false, "nope");
        if (!Validate()) return (false, $"{Errors.Count} error(s)");

        _saving = true;
        try { return _createMode ? await CreateAsync() : await UpdateAsync(); }
        finally { _saving = false; }
    }

    async Task<(bool Ok, string? Err)> CreateAsync()
    {
        if (_draft == null || _pendingImg == null) return (false, "Missing required data");

        var data = new Dictionary<string, object?>
        {
            ["name"] = _draft.Name, ["description"] = _draft.Description, ["details"] = _draft.Details,
            ["race"] = _draft.Race, ["clan"] = _draft.Clan, ["freeCompany"] = _draft.FreeCompany,
            ["playerNotes"] = _draft.PlayerNotes, ["currentStatus"] = _draft.CurrentStatus,
            ["location"] = _draft.Location, ["commenting"] = _draft.Commenting, ["privacy"] = _draft.Privacy,
            ["about"] = _draft.About, ["hooks"] = _draft.Hooks, ["glances"] = _draft.Glances,
        };

        using var content = BuildMultipart(null, data, _pendingImg, _pendingImgName!);
        using var req = new HttpRequestMessage(HttpMethod.Post, CreateEndpoint)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", Globals.Auth.CurrentJwt) },
            Content = content
        };

        var res = await Globals.Http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
        {
            try
            {
                var err = JsonSerializer.Deserialize<ErrorResponse>(body, Json);
                if (err?.Details?.Count > 0) return (false, string.Join(", ", err.Details));
                return (false, err?.Error ?? body);
            }
            catch { return (false, body); }
        }

        var response = JsonSerializer.Deserialize<CreateResponse>(body, Json);
        _createMode = false;
        _profileId = response?.CharacterId;
        if (response?.PageImage != null) _draft.PageImage = response.PageImage;
        _original = Clone(_draft);
        ClearPendingImage();

        await Globals.Profiles.FetchProfilesAsync();
        await RefreshMyCache();
        return (true, null);
    }

    async Task<(bool Ok, string? Err)> UpdateAsync()
    {
        var charId = Globals.Profiles.ActiveProfileId;
        if (string.IsNullOrEmpty(charId)) return (false, "no profile");

        var changes = GetChanges();
        if (changes.Count == 0 && _pendingImg == null) return (true, null);

        HttpResponseMessage res;
        if (_pendingImg != null)
        {
            using var content = BuildMultipart(charId, changes.Count > 0 ? changes : null, _pendingImg, _pendingImgName!);
            using var req = new HttpRequestMessage(HttpMethod.Patch, UpdateEndpoint)
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", Globals.Auth.CurrentJwt) },
                Content = content
            };
            res = await Globals.Http.SendAsync(req);
        }
        else
        {
            changes["characterId"] = charId;
            using var req = new HttpRequestMessage(HttpMethod.Patch, UpdateEndpoint)
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", Globals.Auth.CurrentJwt) },
                Content = JsonContent.Create(changes)
            };
            res = await Globals.Http.SendAsync(req);
        }

        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync();
            return (false, string.IsNullOrEmpty(body) ? $"{res.StatusCode}" : body);
        }

        var responseBody = await res.Content.ReadAsStringAsync();
        if (!string.IsNullOrEmpty(responseBody))
        {
            try
            {
                var resp = JsonSerializer.Deserialize<SaveResponse>(responseBody, Json);
                if (resp?.PageImage != null && _draft != null) _draft.PageImage = resp.PageImage;
            }
            catch { }
        }

        _original = Clone(_draft);
        ClearPendingImage();
        DeleteDraft();
        await RefreshMyCache();
        return (true, null);
    }

    MultipartFormDataContent BuildMultipart(string? charId, Dictionary<string, object?>? data, byte[] img, string imgName)
    {
        var boundary = "----" + Guid.NewGuid().ToString("N");
        var content = new MultipartFormDataContent(boundary);
        content.Headers.Remove("Content-Type");
        content.Headers.TryAddWithoutValidation("Content-Type", $"multipart/form-data; boundary={boundary}");

        if (charId != null)
        {
            var c = new StringContent(charId);
            c.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "\"characterId\"" };
            content.Add(c);
        }

        if (data != null)
        {
            var c = new StringContent(JsonSerializer.Serialize(data), System.Text.Encoding.UTF8, "application/json");
            c.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "\"data\"" };
            content.Add(c);
        }

        var ext = Path.GetExtension(imgName).ToLowerInvariant();
        var mime = ext switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", ".gif" => "image/gif", _ => "application/octet-stream" };
        var imgContent = new ByteArrayContent(img);
        imgContent.Headers.ContentType = new MediaTypeHeaderValue(mime);
        imgContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "\"image\"", FileName = $"\"{imgName}\"" };
        content.Add(imgContent);

        return content;
    }

    async Task RefreshMyCache()
    {
        string? name = null, world = null;
        await Globals.Framework.RunOnFrameworkThread(() =>
        {
            if (Globals.Objects.LocalPlayer is { } p) { name = p.Name.TextValue; world = p.HomeWorld.Value.Name.ToString(); }
        });
        if (name != null && world != null)
        {
            Globals.Cache.MyLocalVersion = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await Globals.Cache.RefreshProfileAsync(name, world);
        }
    }

    static ProfileEditPayload Clone(ProfileEditPayload? p) => p == null ? new() : new()
    {
        Name = p.Name, Description = p.Description, PageImage = p.PageImage, Details = p.Details,
        Race = p.Race, Clan = p.Clan, FreeCompany = p.FreeCompany, PlayerNotes = p.PlayerNotes,
        CurrentStatus = p.CurrentStatus, Location = p.Location, Commenting = p.Commenting, Privacy = p.Privacy,
        About = p.About.ConvertAll(a => new AboutField { Id = a.Id, Label = a.Label, Input = a.Input }),
        Hooks = p.Hooks.ConvertAll(h => new HookData { Id = h.Id, Title = h.Title, Description = h.Description }),
        Glances = p.Glances?.ConvertAll(g => new GlanceData { IconId = g.IconId, Label = g.Label, Value = g.Value }) ?? []
    };

    record DraftFile { public string? Id { get; init; } public DateTime At { get; init; } public ProfileEditPayload? Data { get; init; } }
    record SaveResponse { public bool Success { get; init; } public long Version { get; init; } public string? PageImage { get; init; } }
    record CreateResponse { public bool Success { get; init; } public string? CharacterId { get; init; } public string? PageImage { get; init; } }
    record ErrorResponse { public string? Error { get; init; } public List<string>? Details { get; init; } }
}
