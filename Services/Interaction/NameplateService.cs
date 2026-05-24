namespace Glance.Services;

using Dalamud.Game.Gui.NamePlate;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Glance.Core;
using System;
using System.Collections.Generic;
using System.Numerics;

public sealed unsafe class NameplateService : IDisposable
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
            var np = (AddonNamePlate.NamePlateObject*)handler.NamePlateObjectAddress;
            var iconNode = np != null ? np->NameIcon : null;

            if (handler.PlayerCharacter is not { } pc)
            {
                if (iconNode != null) ResetColor(iconNode);
                continue;
            }

            var iconId = handler.NameIconId;
            var isRoleplaying = iconId == RpStatusIcon || iconId == RpStatusIcon1;
            var name = pc.Name.TextValue;
            var world = pc.HomeWorld.ValueNullable?.Name.ToString();
            if (string.IsNullOrEmpty(world))
            {
                if (iconNode != null) ResetColor(iconNode);
                continue;
            }
            var profile = Globals.Cache.GetProfile(name, world);

            if (isRoleplaying)
            {
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

            if (iconNode == null) continue;

            var shouldTint = config.NameplateTintEnabled
                && profile?.Data != null
                && (!config.NameplateIconOnlyWhenRoleplaying || isRoleplaying);

            if (shouldTint)
                ApplyColor(iconNode, profile!.Unverified ? config.NameplateUnverifiedColor : config.NameplateVerifiedColor);
            else
                ResetColor(iconNode);
        }
    }

    static void ApplyColor(AtkImageNode* node, Vector4 c)
    {
        node->AtkResNode.MultiplyRed = 25;
        node->AtkResNode.MultiplyGreen = 25;
        node->AtkResNode.MultiplyBlue = 25;
        node->AtkResNode.AddRed = (short)(c.X * 200);
        node->AtkResNode.AddGreen = (short)(c.Y * 200);
        node->AtkResNode.AddBlue = (short)(c.Z * 200);
    }

    static void ResetColor(AtkImageNode* node)
    {
        node->AtkResNode.MultiplyRed = 100;
        node->AtkResNode.MultiplyGreen = 100;
        node->AtkResNode.MultiplyBlue = 100;
        node->AtkResNode.AddRed = 0;
        node->AtkResNode.AddGreen = 0;
        node->AtkResNode.AddBlue = 0;
    }

    public void Dispose() => Globals.NamePlateGui.OnNamePlateUpdate -= OnNamePlateUpdate;
}
