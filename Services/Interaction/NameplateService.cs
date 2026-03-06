namespace Glance.Services;

using Dalamud.Game.Gui.NamePlate;
using Dalamud.Utility;
using Glance.Core;
using System;
using System.Collections.Generic;

public sealed class NameplateService : IDisposable
{
    const int RpStatusIcon = 61595;
    const int RpStatusIcon1 = 61545;

    public NameplateService() => Globals.NamePlateGui.OnNamePlateUpdate += OnNamePlateUpdate;

    void OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        var config = Globals.Config;
        if (!config.EnableNameplates) return;

        var lp = Globals.Objects.LocalPlayer;

        foreach (var handler in handlers)
        {
            if (handler.PlayerCharacter is not { } pc) continue;

            var iconId = handler.NameIconId;
            var isRoleplaying = iconId == RpStatusIcon || iconId == RpStatusIcon1;

            // text + icon swap only, tinting handled by NameplateNodeService
            if (isRoleplaying)
            {
                var name = pc.Name.TextValue;
                var world = pc.HomeWorld.Value.Name.ExtractText();
                var profile = Globals.Cache.GetProfile(name, world);
                var isSelf = lp != null && name == lp.Name.TextValue;
                var customIcon = isSelf
                    ? config.NameplateCustomIconId
                    : Globals.Cache.GetNeighborNameplateIcon(name, world);

                if (customIcon > 0)
                    handler.NameIconId = customIcon;

                if (profile?.Data != null)
                {
                    if (config.NameplateShowCustomNames && !string.IsNullOrEmpty(profile.Data.Name))
                        handler.Name = profile.Data.Name;

                    if (config.NameplateShowCustomTitles && !string.IsNullOrEmpty(profile.Data.Description))
                    {
                        var existingTitle = handler.Title;
                        var hasExistingTitle = !string.IsNullOrWhiteSpace(existingTitle?.TextValue);

                        if (!config.PreferHonorificsTitles || !hasExistingTitle)
                            handler.Title = $"<{profile.Data.Description}>";
                    }
                }
            }
        }
    }

    public void Dispose() => Globals.NamePlateGui.OnNamePlateUpdate -= OnNamePlateUpdate;
}
