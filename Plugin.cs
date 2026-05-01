namespace Glance.Core;

using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Glance.UI.Tabs;
using Glance.UI.Windows;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;

public sealed class Plugin : IDalamudPlugin
{
    private readonly Commands.PluginCommands _commands;
    readonly WindowSystem _ws = new("RPHub");
    readonly TosWindow _tosWindow;
    bool _ticking;
    string? _lastTarget;
    bool _tosAccepted;
    bool _isUIHidden;
    bool _lastHadHardTarget;
    bool _lastActiveWasHard;
    DateTime? _hideAt;
    string? _pendingHoverId;
    DateTime _pendingHoverSince;
    string? _lastHardId;
    string? _hoverAtHardChange;
    const float HoverDelaySecs = 0.5f;

    const string TOS_ACCEPTED_FILE = "tos_accepted.txt";
    const string TOS_VERSION = "1.3";
    private DateTime _lastTokenAttempt = DateTime.MinValue;
    public Plugin(IDalamudPluginInterface pi)
    {
        Globals.Initialize(pi);

        _commands = new Commands.PluginCommands();
        _tosWindow = new TosWindow();
        _tosWindow.OnAccepted += OnTosAccepted;
        _ws.AddWindow(_tosWindow);

        _ws.AddWindow(Globals.MainWindow);
        _ws.AddWindow(Globals.Tooltip);
        _ws.AddWindow(Globals.NearbyWindow);
        _ws.AddWindow(Globals.ProfileView);
        _ws.AddWindow(Globals.GlanceWindow);
        _ws.AddWindow(Globals.Toolbox);
        _ws.AddWindow(Globals.ReportWindow);
        _ws.AddWindow(Globals.BeaconPrompt);

        Globals.Interface.UiBuilder.Draw += _ws.Draw;
        Globals.Interface.UiBuilder.OpenMainUi += OnOpenMainUi;

        Globals.Interface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
        _tosAccepted = CheckTosAccepted();

        if (_tosAccepted)
        {
            InitializePlugin();
        }
        else
        {
            _tosWindow.IsOpen = true;
        }
    }

    void OnTosAccepted()
    {
        SaveTosAccepted();
        _tosAccepted = true;
        InitializePlugin();
    }

    void InitializePlugin()
    {
        MaybeShowBeaconPrompt();

        Globals.ClientState.Login += OnLogin;
        Globals.ClientState.Logout += OnLogout;
        Globals.Framework.Update += OnTick;
        Globals.Toolbox.IsOpen = true;
        _ = Globals.Auth.RevalidateAsync();
        _ = Globals.Mutes.FetchAsync();
    }

    // dont reprompt users that seen it
    void MaybeShowBeaconPrompt()
    {
        if (Globals.Config.HasSeenBeaconPrompt) return;

        var isExistingUser = Globals.Config.BeaconEnabled
            || !string.IsNullOrEmpty(Globals.Config.ApiKey);
        if (isExistingUser)
        {
            Globals.Config.HasSeenBeaconPrompt = true;
            Globals.Config.Save();
            return;
        }

        Globals.BeaconPrompt.IsOpen = true;
    }

    private void OnOpenConfigUi() => OnOpenMainUi();
    void OnOpenMainUi()
    {
        if (!_tosAccepted)
        {
            _tosWindow.IsOpen = true;
        }
        else
        {
            Globals.MainWindow.IsOpen = true;
            Globals.MainWindow.BringToFront();
        }
    }

    bool CheckTosAccepted()
    {
        try
        {
            var path = Path.Combine(Globals.Interface.GetPluginConfigDirectory(), TOS_ACCEPTED_FILE);
            if (File.Exists(path))
            {
                var version = File.ReadAllText(path).Trim();
                return version == TOS_VERSION;
            }
        }
        catch { /* eee */ }
        return false;
    }

    void SaveTosAccepted()
    {
        try
        {
            var path = Path.Combine(Globals.Interface.GetPluginConfigDirectory(), TOS_ACCEPTED_FILE);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, TOS_VERSION);
        }
        catch { /* www */ }
    }
    void OnLogin()
    {
        Globals.Log.Information("[AuthEvent] OnLogin fired.");
        Globals.Toolbox.ResetState();

        Task.Run(async () => {
            await Globals.Auth.RevalidateAsync();
            _ = Globals.Mutes.FetchAsync();
            if (Globals.Profiles.Data?.Characters == null || Globals.Profiles.Data.Characters.Length == 0)
            {
                await Task.Delay(3000);
                await Globals.Profiles.FetchProfilesAsync();
            }
        });
    }

    void OnLogout(int _, int __) => Globals.Auth.ResetSession();

    void OnTick(IFramework fw)
    {
        if (!Globals.Config.Enabled || !_tosAccepted) return;
        if (Globals.Auth.IsAuthenticated && Globals.PlayerState.ContentId != 0)
        {
            if (string.IsNullOrEmpty(Globals.Auth.CurrentJwt) && !Globals.Auth.BeaconFetchFailed)
            {
                if ((DateTime.Now - _lastTokenAttempt).TotalSeconds > 2)
                {
                    _lastTokenAttempt = DateTime.Now;
                    _ = Globals.Auth.EnsureBeaconTokenAsync(true);
                }
            }
        }

        // remove plugin in combat
        var inCombat = Globals.Objects.LocalPlayer?.StatusFlags.HasFlag(
            Dalamud.Game.ClientState.Objects.Enums.StatusFlags.InCombat) == true;

        var inCutscene = Globals.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedInCutSceneEvent] ||
                 Globals.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.WatchingCutscene78];

        var inDuty = Globals.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty];
        var shouldHide = (Globals.Config.CombatMode && inCombat) || (Globals.Config.DutyMode && inDuty) || inCutscene;

        if (shouldHide && !_isUIHidden)
        {
            _isUIHidden = true;
            Globals.Tooltip.Hide();
            Globals.GlanceWindow.Hide();
            Globals.Toolbox.IsOpen = false;
        }
        else if (!shouldHide && _isUIHidden)
        {
            _isUIHidden = false;
            Globals.Toolbox.IsOpen = true;
        }

        if (_isUIHidden) return;

        if (Globals.KeyState[VirtualKey.ESCAPE])
        {
            NearbyTab.ClearSelection();
        }

        CheckTarget();

        if (!Globals.Auth.IsAuthenticated || Globals.Objects.LocalPlayer == null || _ticking) return;
        Globals.Beacon.UpdateLocation();
        _ticking = true;
        _ = Task.Run(async () => { try { await Globals.Beacon.TickAsync(); } finally { _ticking = false; } });
    }

    void CheckTarget()
    {
        var hover = ImGui.GetIO().WantCaptureMouse ? null : Globals.TargetManager.MouseOverTarget as IPlayerCharacter;
        var hard = Globals.TargetManager.Target as IPlayerCharacter;
        var lp = Globals.Objects.LocalPlayer;
        if (hard == null && lp != null && Globals.TargetManager.Target != null
            && Globals.TargetManager.Target.Name.TextValue == lp.Name.TextValue)
            hard = lp;

        var hardId = hard != null ? $"{hard.Name.TextValue}@{hard.HomeWorld.Value.Name.ToString()}" : null;
        if (hardId != _lastHardId)
        {
            _lastHardId = hardId;
            _hoverAtHardChange = _pendingHoverId;
        }

        if (hover != null)
        {
            var hoverId = $"{hover.Name.TextValue}@{hover.HomeWorld.Value.Name.ToString()}";
            if (_pendingHoverId != hoverId) { _pendingHoverId = hoverId; _pendingHoverSince = DateTime.UtcNow; }
        }
        else _pendingHoverId = null;

        var hoverReady = hover != null && _pendingHoverId != null
            && (DateTime.UtcNow - _pendingHoverSince).TotalSeconds >= HoverDelaySecs;

        var clickOnly = Globals.Config.TooltipOnClickOnly;
        var fetchNow = hard != null || (!clickOnly && hoverReady);
        var hoverHasProfile = hover != null && Globals.Cache.GetProfile(hover.Name.TextValue, hover.HomeWorld.Value.Name.ToString())?.Data != null;
        // dont stela focus back to hard target if hover already had profile loaded 
        var freshHover = hover != null && _pendingHoverId != _hoverAtHardChange && hoverHasProfile;
        var active = clickOnly ? hard
            : hard != null ? ((!Globals.Config.LockTargetProfile && freshHover) ? hover : hard)
            : hover;
        var isSelf = lp != null && active != null && active.Name.TextValue == lp.Name.TextValue;

        if (active != null)
        {
            if (!isSelf && Globals.Config.OnlyShowRoleplaying && active.OnlineStatus.ValueNullable?.RowId != 22)
            {
                if (_lastTarget != null) { _lastTarget = null; Globals.Tooltip.Hide(); Globals.MainWindow.ClearTarget(); }
                return;
            }

            var name = active.Name.TextValue;
            var world = active.HomeWorld.Value.Name.ToString();
            var id = $"{name}@{world}";
            if (_lastTarget == id)
            {
                var hasHard = hard != null;
                var isActiveHard = active == hard && hasHard;
                if (hasHard && !_lastHadHardTarget)
                {
                    if (Globals.Config.DisableTooltip)
                    {
                        var cached = Globals.Cache.GetProfile(name, world);
                        Globals.GlanceWindow.Show(name, world, cached?.Data, cached?.Id.ToString());
                    }
                    else Globals.Tooltip.ShowGlance();
                }
                else if (!hasHard && _lastHadHardTarget) Globals.GlanceWindow.Hide();
                if (isActiveHard && !_lastActiveWasHard && !Globals.Config.DisableTooltip) Globals.Tooltip.OnBecameHardTarget();
                _lastHadHardTarget = hasHard;
                _lastActiveWasHard = isActiveHard;
                if (fetchNow) Globals.Tooltip.EnsureLoaded();
                return;
            }
            _lastTarget = id;
            _hideAt = null;
            _lastHadHardTarget = hard != null;
            _lastActiveWasHard = active == hard && hard != null;
            if (Globals.Config.DisableTooltip)
            {
                var cached = Globals.Cache.GetProfile(name, world);
                if (hard != null) Globals.GlanceWindow.Show(name, world, cached?.Data, cached?.Id.ToString());
                if (fetchNow) Globals.Tooltip.EnsureLoaded();
            }
            else
                Globals.Tooltip.Show(name, world, hard != null, fetchNow, updateMain: active == hard && hard != null);
        }
        else
        {
            if (_lastTarget != null) { _lastTarget = null; _hideAt = DateTime.UtcNow.AddSeconds(0.3); }

            if (_hideAt.HasValue && DateTime.UtcNow >= _hideAt.Value)
            {
                _hideAt = null;
                _lastHadHardTarget = false;
                _lastActiveWasHard = false;
                Globals.Tooltip.Hide();
                Globals.MainWindow.ClearTarget();
            }
        }
    }

    public void Dispose()
    {
        if (_tosAccepted)
        {
            Globals.ClientState.Login -= OnLogin;
            Globals.ClientState.Logout -= OnLogout;
            Globals.Framework.Update -= OnTick;
        }
        _tosWindow.OnAccepted -= OnTosAccepted;
        Globals.Interface.UiBuilder.Draw -= _ws.Draw;
        _commands?.Dispose();
        _ws.RemoveAllWindows();
        Globals.Dispose();
    }
}
