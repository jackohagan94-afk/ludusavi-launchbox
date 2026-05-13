using System;
using System.Drawing;
using Unbroken.LaunchBox.Plugins.Data;

namespace LudusaviLaunchBox;

/// <summary>
/// Shows a badge on games that have known save data or existing backups.
/// </summary>
public class BackupBadge : IGameBadge
{
    private readonly Func<IGame, bool>? _appliesFunc;
    private Image? _icon;

    public string UniqueId => "LudusaviBackupBadge";
    public string Name => "Ludusavi Backup";
    public int Index { get; set; } = 100;
    public Image DefaultIcon => _icon ??= CreateBadgeIcon();

    public BackupBadge()
    {
        _appliesFunc = null;
    }

    public BackupBadge(Func<IGame, bool> appliesFunc)
    {
        _appliesFunc = appliesFunc;
    }

    public bool GetAppliesToGame(IGame game)
    {
        return _appliesFunc?.Invoke(game) ?? false;
    }

    private static Image CreateBadgeIcon()
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(Color.FromArgb(64, 180, 80));
        g.FillEllipse(brush, 2, 2, 12, 12);
        return bmp;
    }
}
