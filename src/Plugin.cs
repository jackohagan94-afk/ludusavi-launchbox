using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace LudusaviLaunchBox;

public class LudusaviPlugin : ISystemEventsPlugin, IGameLaunchingPlugin, IGameMenuItemPlugin
{
    private PluginSettings? _settings;
    private IGame? _lastLaunchedGame;
    private bool _initialized;
    private static readonly object _lock = new();

    // ===== ISystemEventsPlugin =====
    public void OnEventRaised(string eventType)
    {
        try { if (eventType == SystemEventTypes.PluginInitialized) SafeInit(); } catch { }
    }

    // ===== IGameMenuItemPlugin =====
    public bool SupportsMultipleGames => true;
    public string Caption => "Ludusavi";
    public Image IconImage => new Bitmap(1, 1);
    public bool ShowInLaunchBox => true;
    public bool ShowInBigBox => false;

    public bool GetIsValidForGame(IGame selectedGame) => true;
    public bool GetIsValidForGames(IGame[] selectedGames) => true;

    public void OnSelected(IGame selectedGame) => ShowMenu(new[] { selectedGame });
    public void OnSelected(IGame[] selectedGames) => ShowMenu(selectedGames);

    private void ShowMenu(IGame[] games)
    {
        if (games.Length == 0) return;
        var game = games[0];
        var isPc = Etc.IsPc(game);

        var menu = new ContextMenuStrip();

        if (isPc)
        {
            menu.Items.Add("Back Up Saves", null, (_, _) => { foreach (var g in games) DoBackup(g); });
            menu.Items.Add("Restore Saves", null, (_, _) => { foreach (var g in games) DoRestore(g); });
        }
        else
        {
            menu.Items.Add("Back Up by Platform", null, (_, _) => { foreach (var g in games) DoPlatformBackup(g); });
            menu.Items.Add("Restore by Platform", null, (_, _) => { foreach (var g in games) DoPlatformRestore(g); });
        }

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open Ludusavi", null, (_, _) => GetGui());
        menu.Items.Add("Set Alternative Name...", null, (_, _) => SetAlternativeName(games[0]));

        // Tags
        var tagsMenu = new ToolStripMenuItem("Tags");
        tagsMenu.DropDownItems.Add("Mark as Backed Up", null, (_, _) => Interactor.AddTag(games[0], Tags.BackedUp));
        tagsMenu.DropDownItems.Add("Mark Skip Backup", null, (_, _) => Interactor.AddTag(games[0], Tags.Skip));
        tagsMenu.DropDownItems.Add("Clear All Ludusavi Tags", null, (_, _) => ClearTags(games[0]));
        menu.Items.Add(tagsMenu);

        menu.Show(Cursor.Position);
    }

    // ===== IGameLaunchingPlugin =====
    public void OnBeforeGameLaunching(IGame? game, IAdditionalApplication? app, IEmulator? emulator)
    {
        try { if (game != null) _lastLaunchedGame = game; } catch { }
    }

    public void OnAfterGameLaunched(IGame? game, IAdditionalApplication? app, IEmulator? emulator)
    {
        try { if (game != null) _lastLaunchedGame = game; } catch { }
    }

    public void OnGameExited()
    {
        try
        {
            var game = _lastLaunchedGame;
            if (game == null) return;

            var s = GetSettings();
            if (!s.BackupOnGameExited) return;

            var isPc = Etc.IsPc(game);
            var prefs = s.GetPlayPreferences(game, isPc);

            if (prefs.GameBackup.ShouldPerform() && (!s.OnlyBackupOnGameExitedIfPc || isPc) && isPc)
            {
                if (prefs.GameBackup.ShouldAsk())
                {
                    if (Interactor.AskChoice($"Back up save data for \"{game.Title}\"?") == Choice.Yes)
                        DoBackup(game);
                }
                else DoBackup(game);
            }

            if (prefs.PlatformBackup.ShouldPerform() && !isPc)
            {
                if (prefs.PlatformBackup.ShouldAsk())
                {
                    if (Interactor.AskChoice($"Back up save data for platform \"{game.Platform}\"?") == Choice.Yes)
                        DoPlatformBackup(game);
                }
                else if (!prefs.PlatformBackup.ShouldAsk()) DoPlatformBackup(game);
            }
        }
        catch { }
    }

    // ===== Init =====
    private void SafeInit()
    {
        if (_initialized) return;
        lock (_lock) { if (_initialized) return; _initialized = true; }

        try
        {
            var s = GetSettings();
            try { s.Save(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".", "ludusavi_settings.json")); } catch { }

            var cli = GetCli(s);

            _ = Task.Run(() =>
            {
                try { cli.RefreshBackups(); cli.RefreshKnownGames(); } catch { }
            });
        }
        catch { }
    }

    // ===== Helpers =====
    private PluginSettings GetSettings()
    {
        if (_settings != null) return _settings;

        try
        {
            var dllPath = Assembly.GetExecutingAssembly().Location;
            var dir = Path.GetDirectoryName(dllPath) ?? ".";
            var path = Path.Combine(dir, "ludusavi_settings.json");
            _settings = PluginSettings.Load(path);

            // Auto-detect ludusavi if path not set
            if (string.IsNullOrEmpty(_settings.ExePath) || !File.Exists(_settings.ExePath))
            {
                _settings.ExePath = "ludusavi"; // fallback to PATH
                try { _settings.Save(path); } catch { }
            }
        }
        catch { _settings = new PluginSettings { ExePath = "ludusavi" }; }
        return _settings;
    }

    private LudusaviCli GetCli(PluginSettings s) => new(s.EffectiveExePath);

    private void DoBackup(IGame game)
    {
        try
        {
            var s = GetSettings(); var cli = GetCli(s);
            var name = cli.ResolveTitle(game, s) ?? Etc.GetGameName(game, s);
            var (success, message) = string.IsNullOrEmpty(s.EffectiveBackupPath)
                ? cli.Backup(name) : cli.Backup(name, s.EffectiveBackupPath);
            if (success && s.TagGamesWithBackups) Interactor.AddTag(game, Tags.BackedUp);
            if (!success && !string.IsNullOrEmpty(message)) Interactor.NotifyError(message);
        }
        catch { }
    }

    private void DoRestore(IGame game)
    {
        var isPc = Etc.IsPc(game);
        var name = isPc 
            ? (GetCli(GetSettings()).ResolveTitle(game, GetSettings()) ?? Etc.GetGameName(game, GetSettings()))
            : Etc.GetPlatformName(game);
        
        var label = isPc ? game.Title : $"platform {Etc.GetPlatformName(game)}";
        if (!Interactor.UserConsents($"Restore save data for {label}?")) return;

        try
        {
            var s = GetSettings(); var cli = GetCli(s);
            var (success, message) = string.IsNullOrEmpty(s.EffectiveBackupPath)
                ? cli.Restore(name) : cli.Restore(name, backupPath: s.EffectiveBackupPath);
            Interactor.NotifyInfo(success ? $"Restored: {label}" : $"Failed: {message}");
        }
        catch { }
    }

    private void DoPlatformBackup(IGame game)
    {
        try { var s = GetSettings(); GetCli(s).Backup(Etc.GetPlatformName(game), s.EffectiveBackupPath); } catch { }
    }

    private void DoPlatformRestore(IGame game)
    {
        var platformName = Etc.GetPlatformName(game);
        if (!Interactor.UserConsents($"Restore save data for platform \"{platformName}\"?")) return;
        try
        {
            var s = GetSettings(); var cli = GetCli(s);
            var (success, message) = string.IsNullOrEmpty(s.EffectiveBackupPath)
                ? cli.Restore(platformName) : cli.Restore(platformName, backupPath: s.EffectiveBackupPath);
            Interactor.NotifyInfo(success ? $"Restored: {platformName}" : $"Failed: {message}");
        }
        catch { }
    }

    private void GetGui() { try { GetCli(GetSettings()).OpenGui(); } catch { } }

    private void SetAlternativeName(IGame game)
    {
        var s = GetSettings();
        s.AlternativeTitles.TryGetValue(game.Title ?? "", out var current);
        var input = Interactor.InputText($"Alternative name for \"{game.Title}\":", "Ludusavi", current ?? "");
        if (input != null)
        {
            if (string.IsNullOrWhiteSpace(input)) s.AlternativeTitles.Remove(game.Title ?? "");
            else s.AlternativeTitles[game.Title ?? ""] = input.Trim();
            s.Save(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".", "ludusavi_settings.json"));
        }
    }

    private static void ClearTags(IGame game)
    {
        foreach (var tag in new[] { Tags.Skip, Tags.GameBackup, Tags.GameNoBackup, Tags.GameBackupAndRestore,
            Tags.GameNoRestore, Tags.PlatformBackup, Tags.PlatformNoBackup, Tags.PlatformBackupAndRestore,
            Tags.PlatformNoRestore, Tags.BackedUp, Tags.UnknownSaveData })
            Interactor.RemoveTag(game, tag);
    }
}
