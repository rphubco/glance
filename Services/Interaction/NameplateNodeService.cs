namespace Glance.Services;

using System;
using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Glance.Core;

public sealed unsafe class NameplateNodeService : IDisposable
{
    AddonNamePlate* _addon;

    public NameplateNodeService()
    {
        Globals.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "NamePlate", OnDraw);
    }

    void OnDraw(AddonEvent type, AddonArgs args)
    {
        try
        {
            _addon = (AddonNamePlate*)args.Addon.Address;
            if (_addon != null) UpdateNodes();
        }
        catch (Exception e)
        {
            Globals.Log.Error($"NameplateNodeService draw error: {e}");
        }
    }

    void UpdateNodes()
    {
        var state = stackalloc byte[AddonNamePlate.NumNamePlateObjects];
        BuildVisibility(state);

        for (int i = 0; i < AddonNamePlate.NumNamePlateObjects; i++)
        {
            var iconNode = _addon->NamePlateObjectArray[i].NameIcon;
            if (iconNode == null) continue;

            if (state[i] == 2) ApplyColor(iconNode, Globals.Config.NameplateVerifiedColor);
            else if (state[i] == 3) ApplyColor(iconNode, Globals.Config.NameplateUnverifiedColor);
            else ResetColor(iconNode);
        }
    }

    void BuildVisibility(byte* state)
    {
        var config = Globals.Config;
        if (!config.EnableNameplates || !config.NameplateTintEnabled) return;

        var framework = Framework.Instance();
        if (framework == null) return;
        var uiModule = framework->GetUIModule();
        if (uiModule == null) return;
        var ui3d = uiModule->GetUI3DModule();
        if (ui3d == null) return;

        for (int j = 0; j < ui3d->NamePlateObjectInfoCount; j++)
        {
            var info = ui3d->NamePlateObjectInfoPointers[j].Value;
            if (info == null || info->GameObject == null) continue;

            int idx = info->NamePlateIndex;
            if ((uint)idx >= AddonNamePlate.NumNamePlateObjects) continue;

            var gameObj = info->GameObject;
            if ((ObjectKind)gameObj->ObjectKind != ObjectKind.Pc) continue;

            if (Globals.Objects.SearchById(gameObj->EntityId) is not IPlayerCharacter pc) continue;

            var name = pc.Name.TextValue;
            var world = pc.HomeWorld.Value.Name.ExtractText();

            if (config.NameplateIconOnlyWhenRoleplaying && pc.OnlineStatus.ValueNullable?.RowId != 22) continue;

            var profile = Globals.Cache.GetProfile(name, world);
            if (profile?.Data != null)
                state[idx] = profile.Unverified == true ? (byte)3 : (byte)2;
            else if (Globals.Cache.IsBeaconNeighbor(name, world))
                state[idx] = 1;
        }
    }

    public void Dispose()
    {
        Globals.AddonLifecycle.UnregisterListener(AddonEvent.PreDraw, "NamePlate", OnDraw);
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
}
