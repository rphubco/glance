namespace Glance.Core;

using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Glance.Utils;
using Glance.Services;
using Glance.UI;
using Glance.UI.Windows;
using System;
using System.Net.Http;

public sealed class Globals
{

    [PluginService] public static IDalamudPluginInterface Interface { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;
    [PluginService] public static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;
    [PluginService] public static IObjectTable Objects { get; private set; } = null!;
    [PluginService] public static ICommandManager Commands { get; private set; } = null!;
    [PluginService] public static IDataManager Data { get; private set; } = null!;
    [PluginService] public static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] public static INamePlateGui NamePlateGui { get; private set; } = null!;
    [PluginService] public static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] public static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;
    [PluginService] public static IObjectTable? ObjectTable { get; set; }
    [PluginService] public static IKeyState KeyState { get; private set; }

    public static string Version => typeof(Globals).Assembly.GetName().Version?.ToString(3) ?? "unknown";
    public static HttpClient Http { get; private set; } = null!;
    public static HttpClient ImageHttp { get; private set; } = null!;
    public static Configuration Config { get; set; } = null!;
    public static AuthService Auth { get; private set; } = null!;
    public static ProfileService Profiles { get; private set; } = null!;
    public static CacheService Cache { get; private set; } = null!;
    public static ImageService Images { get; private set; } = null!;
    public static NameplateService Nameplates { get; private set; } = null!;
    public static BeaconService Beacon { get; private set; } = null!;
    public static ProfileTooltipWindow Tooltip { get; private set; } = null!;
    public static NearbyWindow NearbyWindow { get; private set; } = null!;
    public static GlanceWindow GlanceWindow { get; private set; } = null!;
    public static ToolboxWindow Toolbox { get; private set; } = null!;
    public static ProfileEditService ProfileEdit { get; private set; } = null!;
    public static ProfileViewWindow ProfileView { get; private set; } = null!;
    public static MainWindow MainWindow { get; private set; } = null!;
    public static FontManager Fonts { get; private set; } = null!;
    public static NotesService Notes { get; private set; } = null!;
    public static MuteService Mutes { get; private set; } = null!;
    public static ChatReplaceService ChatReplace { get; private set; } = null!;
    public static ReportWindow ReportWindow { get; private set; } = null!;
    public static NameplateNodeService NameplateNodes { get; private set; } = null!;
    public static BeaconPromptWindow BeaconPrompt { get; private set; } = null!;
    public static void Initialize(IDalamudPluginInterface pi)
    {
        pi.Create<Globals>();

        var apiHandler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 100,
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };
        Http = new HttpClient(new RateLimitHandler(apiHandler))
        {
            Timeout = TimeSpan.FromSeconds(60),
            DefaultRequestVersion = System.Net.HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };

        Http.DefaultRequestHeaders.UserAgent.Clear();
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

        var imageHandler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 20,
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };
        ImageHttp = new HttpClient(imageHandler)
        {
            Timeout = TimeSpan.FromSeconds(60),
            DefaultRequestVersion = System.Net.HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };

        ImageHttp.DefaultRequestHeaders.UserAgent.Clear();
        ImageHttp.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

        Config = Interface.GetPluginConfig() as Configuration ?? new();
        Fonts = new FontManager();
        Fonts.Initialize(pi);

        Auth = new();
        Profiles = new();
        Cache = new();
        Images = new();
        ReportWindow = new();
        Nameplates = new();
        Beacon = new();
        Tooltip = new();
        NearbyWindow = new();
        GlanceWindow = new();
        Toolbox = new();
        ProfileEdit = new();
        ProfileView = new();
        ChatReplace = new();
        MainWindow = new();
        Notes = new();
        Mutes = new();
        NameplateNodes = new();
        BeaconPrompt = new();
    }

    public static void Dispose()
    {
        Images?.Dispose();
        Nameplates?.Dispose();
        NameplateNodes?.Dispose();
        Auth?.Dispose();
        Beacon?.Dispose();
        Http?.Dispose();
        ImageHttp?.Dispose();
        Fonts?.Dispose();
        ChatReplace?.Dispose();
    }

    public static string? GetTargetRace()
    {
        if (TargetManager.Target is not IPlayerCharacter pc) return null;
        return pc.Customize[(int)CustomizeIndex.Race] switch
        {
            1 => "Hyur",
            2 => "Elezen",
            3 => "Lalafell",
            4 => "Miqo'te",
            5 => "Roegadyn",
            6 => "Au Ra",
            7 => "Hrothgar",
            8 => "Viera",
            _ => null
        };
    }

    public static string? GetTargetClan()
    {
        if (TargetManager.Target is not IPlayerCharacter pc) return null;
        return pc.Customize[(int)CustomizeIndex.Tribe] switch
        {
            1 => "Midlander",
            2 => "Highlander",
            3 => "Wildwood",
            4 => "Duskwight",
            5 => "Plainsfolk",
            6 => "Dunesfolk",
            7 => "Seeker of the Sun",
            8 => "Keeper of the Moon",
            9 => "Sea Wolf",
            10 => "Hellsguard",
            11 => "Raen",
            12 => "Xaela",
            13 => "Helions",
            14 => "The Lost",
            15 => "Rava",
            16 => "Veena",
            _ => null
        };
    }
}
