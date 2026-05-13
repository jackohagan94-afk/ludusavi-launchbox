namespace LudusaviLaunchBox;

public enum Operation { Backup, Restore }
public enum BackupCriteria { Game, Platform }
public enum Choice { Yes, Always, No, Never }
public enum RefreshContext { Startup, EditedConfig, ConfiguredTitle, CreatedBackup }

public class PlayPreferences
{
    public OperationPreference GameBackup { get; set; } = new();
    public OperationPreference GameRestore { get; set; } = new();
    public OperationPreference PlatformBackup { get; set; } = new();
    public OperationPreference PlatformRestore { get; set; } = new();
}

public class OperationPreference
{
    public bool Do { get; set; }
    public bool Ask { get; set; }

    public bool ShouldAsk() => Do && Ask;
    public bool ShouldPerform() => Do;
}
