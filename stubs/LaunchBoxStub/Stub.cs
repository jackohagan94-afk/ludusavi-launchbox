using System;
using System.Collections.Generic;

// ============================================================================
// Compile-time stub for Unbroken.LaunchBox.Plugins.dll
// Declares only the types used by LudusaviLaunchBox.
// At runtime, LaunchBox provides the real assembly from its Core folder.
// ============================================================================

namespace Unbroken.LaunchBox.Plugins
{
    public interface IGameLaunchingPlugin
    {
        void OnBeforeGameLaunching(Data.IGame game, Data.IAdditionalApplication app, Data.IEmulator emulator);
        void OnAfterGameLaunched(Data.IGame game, Data.IAdditionalApplication app, Data.IEmulator emulator);
        void OnGameExited();
    }

    public interface IGameMenuItemPlugin
    {
        Data.IGameMenuItem GetMenuItem(Data.IGame[] selectedGames);
    }

    public interface IGameMultiMenuItemPlugin
    {
        IEnumerable<Data.IGameMenuItem> GetMenuItems(params Data.IGame[] selectedGames);
    }

    public interface ISystemMenuItemPlugin
    {
        string Caption { get; }
        System.Drawing.Image IconImage { get; }
        bool ShowInLaunchBox { get; }
        bool ShowInBigBox { get; }
        bool AllowInBigBoxWhenLocked { get; }
        void OnSelected();
    }

    public interface ISystemEventsPlugin
    {
        void OnEventRaised(string eventType);
    }

    public static class PluginHelper
    {
        public static Data.IDataManager DataManager { get; set; }
        public static Data.IStateManager StateManager { get; set; }
    }
}

namespace Unbroken.LaunchBox.Plugins.Data
{
    public interface IGame
    {
        string Id { get; }
        string Title { get; }
        string SortTitle { get; set; }
        string SortTitleOrTitle { get; }
        string Platform { get; set; }
        string Source { get; set; }
        string ApplicationPath { get; set; }
        string CommandLine { get; set; }
        string LaunchBoxDbId { get; }
        string Notes { get; set; }
        bool Favorite { get; set; }
        bool Hide { get; set; }
        bool Broken { get; set; }
        bool Completed { get; set; }
        string Status { get; set; }
        string Developer { get; set; }
        string Publisher { get; set; }
        string Region { get; set; }
        string Version { get; set; }
        string EmulatorId { get; set; }
        string ManualPath { get; set; }
        string MusicPath { get; set; }
        string VideoPath { get; set; }
        string VideoUrl { get; set; }
        string WikipediaUrl { get; set; }
        string ConfigurationPath { get; set; }
        string ConfigurationCommandLine { get; set; }
        string RootFolder { get; set; }
        string DateAdded { get; set; }
        string DateModified { get; set; }
        string LastPlayedDate { get; set; }
        int PlayCount { get; set; }
        string PlayTime { get; set; }
        bool UseDosBox { get; set; }
        bool UseScummVm { get; set; }
        bool Portable { get; set; }

        IAdditionalApplication AddNewAdditionalApplication();
        IAlternateName AddNewAlternateName();
        ICustomField AddNewCustomField();
        IEnumerable<IAdditionalApplication> GetAllAdditionalApplications();
        IEnumerable<IAlternateName> GetAllAlternateNames();
        IEnumerable<ICustomField> GetAllCustomFields();
        bool TryRemoveAdditionalApplication(IAdditionalApplication app);
        bool TryRemoveAlternateNames(IAlternateName name);
        bool TryRemoveCustomField(ICustomField field);
        void Configure();
        void Play();
        void OpenFolder();
        void OpenManual();
    }

    public interface IGameMenuItem
    {
        string Caption { get; }
        IEnumerable<IGameMenuItem> Children { get; }
        bool Enabled { get; }
        System.Drawing.Image Icon { get; set; }
        void OnSelect(params IGame[] selectedGames);
    }

    public interface IGameBadge
    {
        string UniqueId { get; }
        string Name { get; }
        int Index { get; set; }
        System.Drawing.Image DefaultIcon { get; }
        bool GetAppliesToGame(IGame game);
    }

    public interface ICustomField
    {
        string GameId { get; set; }
        string Name { get; set; }
        string Value { get; set; }
    }

    public interface IAdditionalApplication
    {
        string Name { get; set; }
        string ApplicationPath { get; set; }
        string CommandLine { get; set; }
    }

    public interface IEmulator
    {
        string Id { get; }
        string Name { get; }
        string ApplicationPath { get; set; }
        string CommandLine { get; set; }
    }

    public interface IAlternateName
    {
        string Name { get; set; }
    }

    public interface IStateManager
    {
        bool IsBigBox { get; }
        bool IsBigBoxLocked { get; }
        bool IsBigBoxInAttractMode { get; }
        bool IsPremium { get; }
        string BigBoxCurrentTheme { get; }
        IGame[] GetAllSelectedGames();
        IPlatform GetSelectedPlatform();
    }

    public interface IDataManager
    {
        IGame[] GetAllGames();
        IPlatform[] GetAllPlatforms();
        IEmulator[] GetAllEmulators();
        IGame GetGameById(string id);
        IEmulator GetEmulatorById(string id);
        IPlatform GetPlatformByName(string name);
        IGame AddNewGame();
        IPlatform AddNewPlatform();
        void Save(bool forceReload);
        void ForceReload();
        bool TryRemoveGame(IGame game);
    }

    public interface IPlatform
    {
        string Name { get; set; }
    }

    public static class SystemEventTypes
    {
        public const string PluginInitialized = "PluginInitialized";
        public const string LaunchBoxStartupCompleted = "LaunchBoxStartupCompleted";
        public const string BigBoxStartupCompleted = "BigBoxStartupCompleted";
        public const string LaunchBoxShutdownBeginning = "LaunchBoxShutdownBeginning";
        public const string BigBoxShutdownBeginning = "BigBoxShutdownBeginning";
        public const string GameStarting = "GameStarting";
        public const string GameExited = "GameExited";
        public const string SelectionChanged = "SelectionChanged";
        public const string BigBoxLocked = "BigBoxLocked";
        public const string BigBoxUnlocked = "BigBoxUnlocked";
        public const string BigBoxThemeChanged = "BigBoxThemeChanged";
    }
}
