using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LudusaviLaunchBox;

/// <summary>
/// Wraps calls to the Ludusavi CLI executable.
/// </summary>
public class LudusaviCli
{
    private readonly string _exePath;
    private Version? _ludusaviVersion;
    private bool _supportsApi;

    public Dictionary<string, string> Titles { get; } = new(); // game title -> canonical name
    public Dictionary<string, List<BackupInfo>> Backups { get; } = new(); // game name -> backups
    public Dictionary<string, string> BackupPaths { get; } = new(); // game name -> backup path
    public HashSet<string> KnownGames { get; } = new(StringComparer.OrdinalIgnoreCase);

    public LudusaviCli(string exePath)
    {
        _exePath = exePath;
    }

    // ===== Version & Capabilities =====

    public Version? GetVersion()
    {
        if (_ludusaviVersion != null) return _ludusaviVersion;

        var (code, output) = Etc.RunCommand(_exePath, "--version --api");
        if (code == 0)
        {
            try
            {
                var versionJson = JsonSerializer.Deserialize<VersionResponse>(output);
                if (versionJson?.Version != null && System.Version.TryParse(versionJson.Version, out var v))
                {
                    _ludusaviVersion = v;
                    _supportsApi = v >= new Version(0, 24, 0);
                }
            }
            catch { }
        }

        // Fallback: parse version from raw output
        if (_ludusaviVersion == null)
        {
            var (code2, output2) = Etc.RunCommand(_exePath, "--version");
            var match = System.Text.RegularExpressions.Regex.Match(output2, @"(\d+\.\d+\.\d+)");
            if (match.Success && System.Version.TryParse(match.Groups[1].Value, out var v2))
            {
                _ludusaviVersion = v2;
                _supportsApi = v2 >= new Version(0, 24, 0);
            }
        }

        return _ludusaviVersion;
    }

    public bool SupportsApi()
    {
        GetVersion();
        return _supportsApi;
    }

    // ===== Refresh =====

    public void RefreshBackups()
    {
        var (code, output) = Etc.RunCommand(_exePath, "--try-manifest-update backups --api");
        if (code != 0) return;

        try
        {
            var result = JsonSerializer.Deserialize<BackupsResponse>(output);
            if (result?.Games != null)
            {
                Backups.Clear();
                BackupPaths.Clear();
                foreach (var kvp in result.Games)
                {
                    var gameName = kvp.Key;
                    var gameData = kvp.Value;
                    Backups[gameName] = gameData.Backups ?? new List<BackupInfo>();
                    BackupPaths[gameName] = gameData.BackupPath ?? "";
                }
            }
        }
        catch { }
    }

    public void RefreshKnownGames()
    {
        var (code, output) = Etc.RunCommand(_exePath, "--try-manifest-update manifest show --api");
        if (code != 0) return;

        try
        {
            var manifest = JsonSerializer.Deserialize<ManifestResponse>(output);
            if (manifest?.Games != null)
            {
                KnownGames.Clear();
                foreach (var kvp in manifest.Games)
                    KnownGames.Add(kvp.Key);
            }
        }
        catch { }
    }

    public async Task RefreshTitlesAsync(List<Unbroken.LaunchBox.Plugins.Data.IGame> games, PluginSettings settings)
    {
        if (!SupportsApi())
        {
            // Without API support, resolve titles one by one via find command
            foreach (var game in games)
            {
                var title = ResolveTitle(game, settings);
                if (title != null)
                    Titles[game.Title ?? ""] = title;
            }
            return;
        }

        // Batch resolve via api command
        var requests = new List<ApiRequest>();
        foreach (var game in games)
        {
            var name = Etc.GetGameName(game, settings);
            var steamId = Etc.SteamId(game);

            requests.Add(new ApiRequest
            {
                FindTitle = new FindTitleRequest
                {
                    Names = new List<string> { game.Title ?? "", name }.Distinct().ToList(),
                    SteamId = steamId?.ToString(),
                    Normalized = settings.RetryUnrecognizedWithNormalization
                }
            });
        }

        var input = new ApiInput { Requests = requests };
        var json = JsonSerializer.Serialize(input);
        var (code, output) = Etc.RunCommandWithStdin(_exePath, "api", json);
        if (code != 0) return;

        try
        {
            var result = JsonSerializer.Deserialize<ApiOutput>(output);
            if (result?.Responses != null)
            {
                for (int i = 0; i < result.Responses.Count && i < games.Count; i++)
                {
                    var title = result.Responses[i].FoundTitle;
                    if (!string.IsNullOrEmpty(title))
                        Titles[games[i].Title ?? ""] = title;
                }
            }
        }
        catch { }
    }

    // ===== Find Title =====

    public string? ResolveTitle(Unbroken.LaunchBox.Plugins.Data.IGame game, PluginSettings settings)
    {
        // Check cache
        if (Titles.TryGetValue(game.Title ?? "", out var cached))
            return cached;

        var name = Etc.GetGameName(game, settings);
        var steamId = Etc.SteamId(game);

        // Try Steam ID first
        if (steamId.HasValue)
        {
            var (code, output) = Etc.RunCommand(_exePath, $"--try-manifest-update find --api --steam-id {steamId}");
            if (code == 0)
            {
                var title = ParseFindResponse(output);
                if (!string.IsNullOrEmpty(title))
                {
                    Titles[game.Title ?? ""] = title;
                    return title;
                }
            }
        }

        // Try by name
        {
            var (code, output) = Etc.RunCommand(_exePath, $"--try-manifest-update find --api \"{EscapeArg(name)}\"");
            if (code == 0)
            {
                var title = ParseFindResponse(output);
                if (!string.IsNullOrEmpty(title))
                {
                    Titles[game.Title ?? ""] = title;
                    return title;
                }
            }
        }

        // Try normalized
        if (settings.RetryUnrecognizedWithNormalization)
        {
            var (code, output) = Etc.RunCommand(_exePath, $"--try-manifest-update find --api --normalized \"{EscapeArg(name)}\"");
            if (code == 0)
            {
                var title = ParseFindResponse(output);
                if (!string.IsNullOrEmpty(title))
                {
                    Titles[game.Title ?? ""] = title;
                    return title;
                }
            }
        }

        return null;
    }

    // ===== Backup =====

    public (bool Success, string? Message) Backup(string gameName, string? backupPath = null)
    {
        var pathArg = !string.IsNullOrEmpty(backupPath) ? $"--path \"{EscapeArg(backupPath)}\"" : "";
        var (code, output) = Etc.RunCommand(_exePath, $"--try-manifest-update backup --force --api {pathArg} \"{EscapeArg(gameName)}\"");
        return ParseResult(code, output);
    }

    public (bool Success, string? Message) BackupAll(string? backupPath = null)
    {
        var pathArg = !string.IsNullOrEmpty(backupPath) ? $"--path \"{EscapeArg(backupPath)}\"" : "";
        var (code, output) = Etc.RunCommand(_exePath, $"--try-manifest-update backup --force --api {pathArg}");
        return ParseResult(code, output);
    }

    // ===== Restore =====

    public (bool Success, string? Message) Restore(string gameName, string? backupName = null, string? backupPath = null)
    {
        var pathArg = !string.IsNullOrEmpty(backupPath) ? $"--path \"{EscapeArg(backupPath)}\"" : "";
        var backupArg = !string.IsNullOrEmpty(backupName) ? $"--backup \"{EscapeArg(backupName)}\"" : "";
        var (code, output) = Etc.RunCommand(_exePath, $"--try-manifest-update restore --force --api {pathArg} {backupArg} \"{EscapeArg(gameName)}\"");
        return ParseResult(code, output);
    }

    public (bool Success, string? Message) RestoreAll(string? backupPath = null)
    {
        var pathArg = !string.IsNullOrEmpty(backupPath) ? $"--path \"{EscapeArg(backupPath)}\"" : "";
            var (code, output) = Etc.RunCommand(_exePath, "--try-manifest-update restore --force --api {pathArg}");
        return ParseResult(code, output);
    }

    // ===== GUI =====

    public void OpenGui()
    {
        Etc.RunCommandGui(_exePath, "gui --api");
    }

    public void OpenGuiForGame(string gameName)
    {
        Etc.RunCommandGui(_exePath, $"gui --api --custom-game \"{EscapeArg(gameName)}\"");
    }

    // ===== Helpers =====

    private static string? ParseFindResponse(string output)
    {
        try
        {
            var json = JsonSerializer.Deserialize<FindResponse>(output);
            return json?.Title;
        }
        catch { }
        return null;
    }

    private static (bool, string?) ParseResult(int code, string output)
    {
        if (code != 0) return (false, $"Ludusavi exited with code {code}");

        try
        {
            var json = JsonSerializer.Deserialize<OperationResponse>(output);
            if (json?.Errors != null && json.Errors.Count > 0)
                return (false, string.Join("; ", json.Errors));
            return (true, json?.Overall?.GamesProcessed > 0 ? $"Processed {json.Overall.GamesProcessed} games" : "No changes");
        }
        catch
        {
            return (code == 0, output);
        }
    }

    private static string EscapeArg(string arg)
    {
        return arg.Replace("\"", "\\\"");
    }

    // ===== JSON Models =====

    private class VersionResponse
    {
        [JsonPropertyName("version")]
        public string? Version { get; set; }
    }

    private class FindResponse
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }
    }

    private class OperationResponse
    {
        [JsonPropertyName("errors")]
        public List<string>? Errors { get; set; }
        [JsonPropertyName("overall")]
        public OverallInfo? Overall { get; set; }
    }

    public class OverallInfo
    {
        [JsonPropertyName("gamesProcessed")]
        public int GamesProcessed { get; set; }
    }

    private class BackupsResponse
    {
        [JsonPropertyName("games")]
        public Dictionary<string, GameBackupData>? Games { get; set; }
    }

    public class GameBackupData
    {
        [JsonPropertyName("backupPath")]
        public string? BackupPath { get; set; }
        [JsonPropertyName("backups")]
        public List<BackupInfo>? Backups { get; set; }
    }

    public class BackupInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("when")]
        public DateTime When { get; set; }
        [JsonPropertyName("os")]
        public string? Os { get; set; }
        [JsonPropertyName("locked")]
        public bool Locked { get; set; }
        [JsonPropertyName("comment")]
        public string? Comment { get; set; }
    }

    private class ApiInput
    {
        [JsonPropertyName("requests")]
        public List<ApiRequest>? Requests { get; set; }
    }

    private class ApiRequest
    {
        [JsonPropertyName("findTitle")]
        public FindTitleRequest? FindTitle { get; set; }
    }

    private class FindTitleRequest
    {
        [JsonPropertyName("names")]
        public List<string>? Names { get; set; }
        [JsonPropertyName("steamId")]
        public string? SteamId { get; set; }
        [JsonPropertyName("normalized")]
        public bool Normalized { get; set; }
    }

    private class ApiOutput
    {
        [JsonPropertyName("responses")]
        public List<ApiResponse>? Responses { get; set; }
    }

    private class ApiResponse
    {
        [JsonPropertyName("foundTitle")]
        public string? FoundTitle { get; set; }
    }

    private class ManifestResponse
    {
        [JsonPropertyName("games")]
        public Dictionary<string, ManifestEntry>? Games { get; set; }
    }

    private class ManifestEntry
    {
        [JsonPropertyName("files")]
        public List<string>? Files { get; set; }
    }
}
