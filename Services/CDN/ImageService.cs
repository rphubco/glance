using Dalamud.Interface.Textures.TextureWraps;
using Glance.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Image = SixLabors.ImageSharp.Image;

namespace Glance.Services;

public sealed class ImageService : IDisposable
{
    const string CdnBase = "https://cdn.rphub.co/";
    const int MaxCached = 100;

    private readonly SemaphoreSlim _loadLock = new(2, 2);
    readonly ConcurrentDictionary<string, CachedTex> _cache = new();
    readonly ConcurrentQueue<string> _lruQueue = new();
    readonly string _diskCache;
    private HttpClient _http => Globals.ImageHttp;

    public ImageService()
    {
        _diskCache = Path.Combine(Globals.Interface.ConfigDirectory.FullName, "ImageCache");
        try { Directory.CreateDirectory(_diskCache); } catch { }
    }

    public IDalamudTextureWrap? Get(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var url = path.StartsWith("http") ? path : $"{CdnBase}{path.TrimStart('/')}";

        if (_cache.TryGetValue(url, out var c))
        {
            if (c.Loading) return null;
            if (!c.Failed && c.Tex != null) return c.Tex;
            if (c.FailedAt.HasValue && DateTime.UtcNow - c.FailedAt.Value < TimeSpan.FromMinutes(5))
                return null;
        }

        var loadingState = new CachedTex(null, true, false, null);
        if (_cache.TryAdd(url, loadingState)) { _ = Load(url);}
        else { }

        return null;
    }

    async Task Load(string url)
    {
        try
        {
            var fileName = GetCacheFileName(url);
            var diskPath = Path.Combine(_diskCache, fileName);
            byte[] bytes;

            if (File.Exists(diskPath))
            {
                bytes = await File.ReadAllBytesAsync(diskPath);
            }
            else
            {
                bytes = await _http.GetByteArrayAsync(url);

                await _loadLock.WaitAsync();
                try
                {
                    bytes = await Task.Run(() => ProcessImage(bytes));
                }
                finally
                {
                    _loadLock.Release();
                }

                await File.WriteAllBytesAsync(diskPath, bytes);
            }

            var tex = await Globals.TextureProvider.CreateFromImageAsync(bytes);
            _cache[url] = new(tex, false, tex == null, tex == null ? DateTime.UtcNow : null);
            if (tex != null) TrackAndEvict(url);
        }
        catch (Exception ex)
        {
            Globals.Log.Error(ex, $"[ImageService] Failed: {url}");
            _cache[url] = new(null, false, true, DateTime.UtcNow);
        }
    }

    static byte[] ProcessImage(byte[] input)
    {
        try
        {
            using var image = Image.Load(input);

            int maxDim = 1024;
            if (image.Width > maxDim || image.Height > maxDim)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(maxDim, maxDim),
                    Mode = ResizeMode.Max
                }));
            }

            using var ms = new MemoryStream();
            image.SaveAsJpeg(ms, new JpegEncoder { Quality = 75 });
            return ms.ToArray();
        }
        catch
        {
            return input;
        }
    }

    string GetCacheFileName(string url)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(url);
        var hashBytes = md5.ComputeHash(inputBytes);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLower() + ".jpg";
    }

    void TrackAndEvict(string url)
    {
        _lruQueue.Enqueue(url);
        while (_lruQueue.Count > MaxCached && _lruQueue.TryDequeue(out var old))
        {
            if (old != url && _cache.TryRemove(old, out var evicted))
                evicted.Tex?.Dispose();
        }
    }

    public void ClearDiskCache()
    {
        foreach (var c in _cache.Values) c.Tex?.Dispose();
        _cache.Clear();
        while (_lruQueue.TryDequeue(out _)) { }
        try { if (Directory.Exists(_diskCache)) Directory.Delete(_diskCache, true); Directory.CreateDirectory(_diskCache); } catch { }
    }

    public long GetDiskCacheSize()
    {
        try
        {
            if (!Directory.Exists(_diskCache)) return 0;
            long size = 0;
            foreach (var file in Directory.GetFiles(_diskCache))
                size += new FileInfo(file).Length;
            return size;
        }
        catch { return 0; }
    }

    public void Dispose()
    {
        foreach (var c in _cache.Values) c.Tex?.Dispose();
        _cache.Clear();
        _loadLock.Dispose();
    }

    record CachedTex(IDalamudTextureWrap? Tex, bool Loading, bool Failed, DateTime? FailedAt);
}
