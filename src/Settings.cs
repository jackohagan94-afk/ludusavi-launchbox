using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LudusaviLaunchBox;

public class PluginSettings
{
    public string ExePath { get; set; } = "";

    // Backup
    public bool OverrideBackupPath { get; set; }
    public string BackupPath { get; set; } = "";
    public bool BackupOnGameExited { get; set; } = true;
    public bool AskBackupOnGameExited { get; set; } = true;

    // Restore
    public bool RestoreOnGameStarting { get; set; }

    // Filtering
    public bool OnlyBackupOnGameExitedIfPc { get; set; }
    public bool BackupByPlatformForNonPc { get; set; } = true;

    // Title resolution
    public bool RetryUnrecognizedWithNormalization { get; set; } = true;
    public bool AddSuffixForNonPcGameNames { get; set; } = true;
    public string SuffixForNonPcGameNames { get; set; } = " (<platform>)";

    // Tagging
    public bool TagGamesWithBackups { get; set; }
    public bool TagGamesWithUnknownSaveData { get; set; }

    // Alternative titles: Playnite title -> Ludusavi lookup name
    public Dictionary<string, string> AlternativeTitles { get; set; } = new();

    [JsonIgnore]
    public string EffectiveBackupPath => OverrideBackupPath && !string.IsNullOrEmpty(BackupPath)
        ? BackupPath
        : "";

    [JsonIgnore]
    public string EffectiveExePath => !string.IsNullOrEmpty(ExePath) ? ExePath : "ludusavi";

    public static PluginSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<PluginSettings>(json) ?? new PluginSettings();
            }
        }
        catch { }
        return new PluginSettings();
    }

    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public PlayPreferences GetPlayPreferences(Unbroken.LaunchBox.Plugins.Data.IGame game, bool isPc)
    {
        var prefs = new PlayPreferences();

        // Check custom fields for [Ludusavi] tags
        var customFields = game.GetAllCustomFields().ToList();
        bool skip = HasCustomField(customFields, "[Ludusavi] Skip");
        bool gameBackup = HasCustomField(customFields, "[Ludusavi] Game: backup");
        bool gameNoBackup = HasCustomField(customFields, "[Ludusavi] Game: no backup");
        bool gameBackupRestore = HasCustomField(customFields, "[Ludusavi] Game: backup and restore");
        bool gameNoRestore = HasCustomField(customFields, "[Ludusavi] Game: no restore");
        bool platformBackup = HasCustomField(customFields, "[Ludusavi] Platform: backup");
        bool platformNoBackup = HasCustomField(customFields, "[Ludusavi] Platform: no backup");
        bool platformBackupRestore = HasCustomField(customFields, "[Ludusavi] Platform: backup and restore");
        bool platformNoRestore = HasCustomField(customFields, "[Ludusavi] Platform: no restore");

        if (skip)
        {
            prefs.GameBackup.Do = false;
            prefs.GameRestore.Do = false;
            prefs.PlatformBackup.Do = false;
            prefs.PlatformRestore.Do = false;
            return prefs;
        }

        // Game backup
        if (gameNoBackup) { prefs.GameBackup.Do = false; }
        else if (gameBackupRestore || gameBackup) { prefs.GameBackup.Do = true; prefs.GameBackup.Ask = false; }
        else { prefs.GameBackup.Do = BackupOnGameExited; prefs.GameBackup.Ask = AskBackupOnGameExited; }

        // Game restore
        if (gameNoRestore) { prefs.GameRestore.Do = false; }
        else if (gameBackupRestore) { prefs.GameRestore.Do = true; prefs.GameRestore.Ask = false; }
        else { prefs.GameRestore.Do = RestoreOnGameStarting; prefs.GameRestore.Ask = false; }

        // Platform backup
        if (platformNoBackup) { prefs.PlatformBackup.Do = false; }
        else if (platformBackupRestore || platformBackup) { prefs.PlatformBackup.Do = true; prefs.PlatformBackup.Ask = false; }
        else { prefs.PlatformBackup.Do = BackupOnGameExited && BackupByPlatformForNonPc && !isPc; prefs.PlatformBackup.Ask = AskBackupOnGameExited; }

        // Platform restore
        if (platformNoRestore) { prefs.PlatformRestore.Do = false; }
        else if (platformBackupRestore) { prefs.PlatformRestore.Do = true; prefs.PlatformRestore.Ask = false; }
        else { prefs.PlatformRestore.Do = RestoreOnGameStarting && !isPc; prefs.PlatformRestore.Ask = false; }

        return prefs;
    }

    private static bool HasCustomField(IEnumerable<Unbroken.LaunchBox.Plugins.Data.ICustomField> fields, string name)
    {
        return fields.Any(f => f.Name == name && (f.Value == "true" || string.IsNullOrEmpty(f.Value)));
    }
}
