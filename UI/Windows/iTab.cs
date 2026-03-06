using System;

namespace RPHubPlugin.Windows.Tabs;

public interface ITab : IDisposable
{
    string Name { get; }
    void Draw();
}
