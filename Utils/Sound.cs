using FFXIVClientStructs.FFXIV.Client.UI;
using Glance.Core;

namespace Glance.Utils;

public static class Sound
{
    public const uint Click = 1u;
    public const uint Confirm = 2u;
    public const uint Cancel = 3u;
    public const uint Error = 17u;
    public const uint Success = 25u;
    public const uint Warning = 29u;
    public const uint Open = 23u;
    public const uint Close = 24u;
    public const uint Select = 14u;
    public const uint Hover = 52u;
    public const uint Tab = 31u;

    public static unsafe void Play(uint id) {if (!Globals.Config.EnableSounds) return; UIGlobals.PlaySoundEffect(id);}

    public static void PlayClick() => Play(Click);
    public static void PlayConfirm() => Play(Confirm);
    public static void PlayCancel() => Play(Cancel);
    public static void PlayError() => Play(Error);
    public static void PlaySuccess() => Play(Success);
    public static void PlayWarning() => Play(Warning);
    public static void PlayOpen() => Play(Open);
    public static void PlayClose() => Play(Close);
    public static void PlaySelect() => Play(Select);
    public static void PlayTab() => Play(Tab);
}
