namespace Glance.Core.Commands;

using Dalamud.Game.Command;
using Glance.Utils;
using System;
using System.Numerics;

public class PluginCommands : IDisposable
{
    const string MainCommand = "/glance";

    public PluginCommands()
    {
        Globals.Commands.AddHandler(MainCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle Glance interface. Use 'on', 'off', or 'toolbox' for specific controls.",
            ShowInHelp = true
        });
    }

    void OnCommand(string command, string args)
    {
        var argument = args.Trim().ToLower();
        switch (argument)
        {
            case "off":
                DisablePlugin();
                PrintStatus("Disabled", "Plugin paused. Beacon stopped and UI hidden.", Theme.Error);
                break;
            case "on":
                Globals.Config.Enabled = true;
                Globals.Config.Save();
                Globals.Toolbox.IsOpen = true;
                Globals.Config.ShowToolbox = true;
                PrintStatus("Enabled", "Glance services are now active.", Theme.Success);
                break;
            case "toolbox":
                Globals.Config.ShowToolbox = !Globals.Config.ShowToolbox;
                Globals.Config.Save();
                if (Globals.Config.ShowToolbox)
                {
                    Globals.Toolbox.IsOpen = true;
                }
                PrintStatus("Toolbox", Globals.Config.ShowToolbox ? "Toolbox visible." : "Toolbox hidden.", Theme.Primary);
                break;
            case "help":
                PrintHelp();
                break;
            case "":
                Globals.MainWindow.Toggle();
                break;
            default:
                PrintStatus("Error", $"Unknown command '{argument}'. Type '/glance help' for options.", Theme.Primary);
                break;
        }
    }

    void PrintStatus(string status, string message, Vector4 color)
    {
        Globals.ChatGui.Print(new Dalamud.Game.Text.SeStringHandling.SeStringBuilder()
            .AddUiForeground(34)
            .AddText("[Glance] ")
            .AddUiForegroundOff()
            .AddText("» ")
            .AddText($"{status}: ")
            .AddItalics(message)
            .BuiltString);
    }

    void PrintHelp()
    {
        PrintStatus("Help", "Command List:", Theme.Primary);
        var builder = new Dalamud.Game.Text.SeStringHandling.SeStringBuilder();
        ushort blue = 34;
        builder.AddText("  /glance ")
               .AddUiForeground(blue).AddText("on").AddUiForegroundOff().AddText("/")
               .AddUiForeground(blue).AddText("off").AddUiForegroundOff()
               .AddText("      — Toggle plugin services\n");
        builder.AddText("/glance ")
               .AddUiForeground(blue).AddText("toolbox").AddUiForegroundOff()
               .AddText("   — Toggle the toolbox visibility\n");
        Globals.ChatGui.Print(builder.BuiltString);
    }

    void DisablePlugin()
    {
        Globals.Config.Enabled = false;
        Globals.MainWindow.IsOpen = false;
        Globals.NearbyWindow.IsOpen = false;
        Globals.GlanceWindow.IsOpen = false;
        Globals.Toolbox.IsOpen = false;
        Globals.Tooltip.Hide();
    }

    public void Dispose() => Globals.Commands.RemoveHandler(MainCommand);
}
