namespace Glance.Utils;

using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;
using System;

public class FontManager : IDisposable
{
    public IFontHandle Title { get; private set; } = null!;
    public IFontHandle Header { get; private set; } = null!;

    public void Initialize(IDalamudPluginInterface pi)
    {
        Title = pi.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Axis, 28f));
        Header = pi.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Axis, 20f));
    }

    public void Dispose()
    {
        Title?.Dispose();
        Header?.Dispose();
    }
}
