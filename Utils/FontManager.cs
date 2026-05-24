namespace Glance.Utils;

using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;
using System;

public class FontManager : IDisposable
{
    public IFontHandle Small { get; private set; } = null!;
    public IFontHandle Header { get; private set; } = null!;
    public IFontHandle Large { get; private set; } = null!;
    public IFontHandle Title { get; private set; } = null!;

    public void Initialize(IDalamudPluginInterface pi)
    {
        var atlas = pi.UiBuilder.FontAtlas;
        Small = atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(-0.85f)));
        Header = atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(-1.25f)));
        Large = atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(-1.50f)));
        Title = atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(-1.75f)));
    }

    public void Dispose()
    {
        Small?.Dispose();
        Header?.Dispose();
        Large?.Dispose();
        Title?.Dispose();
    }
}
