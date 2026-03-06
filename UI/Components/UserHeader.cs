namespace Glance.UI.Components;

using Dalamud.Bindings.ImGui;
using Glance.Core;

public static class UserHeader
{
    public static void Draw()
    {
        if (!Globals.Auth.IsAuthenticated || Globals.Auth.CurrentUser is not { } user) return;

        ImGui.TextUnformatted(user.Name);
        ImGui.Separator();
    }
}
