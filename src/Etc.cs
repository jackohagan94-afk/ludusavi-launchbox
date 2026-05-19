using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace LudusaviLaunchBox;

public static class Etc
{
    private static readonly Regex HomeDir = new("^~");
    private static readonly Regex SteamUrlRegex = new(@"steam://rungameid/(\d+)", RegexOptions.IgnoreCase);
    private static readonly HashSet<string> PcPlatforms = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "PC", "PC (Windows)", "Linux", "PC (Linux)",
        "Macintosh", "PC (DOS)", "DOS", "PC (Macintosh)"
    };

    // ===== Process Execution =====

    public static (int ExitCode, string Output) RunCommand(string command, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            AddArguments(psi, args);

            using var p = Process.Start(psi);
            if (p == null) return (-1, "");

            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(30000);
            return (p.ExitCode, output);
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }

    public static (int ExitCode, string Output) RunCommandWithStdin(string command, string[] args, string stdin)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            AddArguments(psi, args);

            using var p = Process.Start(psi);
            if (p == null) return (-1, "");

            // Write stdin without BOM
            using var writer = new StreamWriter(p.StandardInput.BaseStream, new UTF8Encoding(false));
            writer.Write(stdin);
            writer.Close();

            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(30000);
            return (p.ExitCode, output);
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }

    public static void RunCommandGui(string command, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                UseShellExecute = false,
                CreateNoWindow = false
            };
            AddArguments(psi, args);
            Process.Start(psi);
        }
        catch { }
    }

    private static void AddArguments(ProcessStartInfo psi, IEnumerable<string> args)
    {
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);
    }

    // ===== Game Detection =====

    public static bool IsOnSteam(Unbroken.LaunchBox.Plugins.Data.IGame game)
    {
        return string.Equals(game.Source, "Steam", StringComparison.OrdinalIgnoreCase);
    }

    public static int? SteamId(Unbroken.LaunchBox.Plugins.Data.IGame game)
    {
        if (!IsOnSteam(game)) return null;

        // Try parsing ApplicationPath for steam://rungameid/XXXXX
        var appPath = game.ApplicationPath ?? "";
        var match = SteamUrlRegex.Match(appPath);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var id))
            return id;

        // Try CommandLine
        var cmdLine = game.CommandLine ?? "";
        match = SteamUrlRegex.Match(cmdLine);
        if (match.Success && int.TryParse(match.Groups[1].Value, out id))
            return id;

        return null;
    }

    public static bool IsPc(Unbroken.LaunchBox.Plugins.Data.IGame game)
    {
        var platform = game.Platform ?? "";
        return string.IsNullOrEmpty(platform) || PcPlatforms.Contains(platform);
    }

    public static string GetGameName(Unbroken.LaunchBox.Plugins.Data.IGame game, PluginSettings settings)
    {
        var name = game.Title ?? "";

        // Check alternative titles first
        if (settings.AlternativeTitles.TryGetValue(name, out var alt) && !string.IsNullOrEmpty(alt))
            return alt;

        if (!IsPc(game) && settings.AddSuffixForNonPcGameNames)
        {
            var suffix = settings.SuffixForNonPcGameNames.Replace("<platform>", game.Platform ?? "");
            if (!name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                name += suffix;
        }

        return name;
    }

    public static string GetPlatformName(Unbroken.LaunchBox.Plugins.Data.IGame game)
    {
        return game.Platform ?? "Unknown";
    }

    // ===== Path Normalization =====

    public static string NormalizePath(string path)
    {
        return HomeDir.Replace(path, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
                      .Replace("/", "\\");
    }

    // ===== Custom Field Helpers =====

    public static bool HasCustomField(Unbroken.LaunchBox.Plugins.Data.IGame game, string name)
    {
        return game.GetAllCustomFields().Any(f => f.Name == name);
    }

    public static void SetCustomField(Unbroken.LaunchBox.Plugins.Data.IGame game, string name, string value)
    {
        // Remove existing first
        var existing = game.GetAllCustomFields().FirstOrDefault(f => f.Name == name);
        if (existing != null)
            game.TryRemoveCustomField(existing);

        var field = game.AddNewCustomField();
        field.Name = name;
        field.Value = value;
    }

    public static void RemoveCustomField(Unbroken.LaunchBox.Plugins.Data.IGame game, string name)
    {
        var existing = game.GetAllCustomFields().FirstOrDefault(f => f.Name == name);
        if (existing != null)
            game.TryRemoveCustomField(existing);
    }
}

public static class Tags
{
    private const string Prefix = "[Ludusavi] ";

    public const string Skip = Prefix + "Skip";
    public const string GameBackup = Prefix + "Game: backup";
    public const string GameNoBackup = Prefix + "Game: no backup";
    public const string GameBackupAndRestore = Prefix + "Game: backup and restore";
    public const string GameNoRestore = Prefix + "Game: no restore";
    public const string PlatformBackup = Prefix + "Platform: backup";
    public const string PlatformNoBackup = Prefix + "Platform: no backup";
    public const string PlatformBackupAndRestore = Prefix + "Platform: backup and restore";
    public const string PlatformNoRestore = Prefix + "Platform: no restore";
    public const string BackedUp = Prefix + "Backed up";
    public const string UnknownSaveData = Prefix + "Unknown save data";

    public static readonly Dictionary<string, string[]> Conflicts = new()
    {
        { Skip, Array.Empty<string>() },
        { GameBackup, new[] { Skip, GameNoBackup } },
        { GameNoBackup, new[] { GameBackup, GameBackupAndRestore } },
        { GameBackupAndRestore, new[] { Skip, GameBackup, GameNoBackup, GameNoRestore } },
        { GameNoRestore, new[] { GameBackupAndRestore } },
        { PlatformBackup, new[] { Skip, PlatformNoBackup } },
        { PlatformNoBackup, new[] { PlatformBackup, PlatformBackupAndRestore } },
        { PlatformBackupAndRestore, new[] { Skip, PlatformBackup, PlatformNoBackup, PlatformNoRestore } },
        { PlatformNoRestore, new[] { PlatformBackupAndRestore } },
    };
}
