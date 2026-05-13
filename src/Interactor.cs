using System;
using System.Drawing;
using System.Windows.Forms;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace LudusaviLaunchBox;

/// <summary>
/// Handles user interaction: dialogs, notifications, custom field (tag) management.
/// </summary>
public class Interactor
{
    /// <summary>
    /// Simple Yes/No dialog.
    /// </summary>
    public static bool UserConsents(string message, string title = "Ludusavi")
    {
        return MessageBox.Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    }

    /// <summary>
    /// Show an informational message.
    /// </summary>
    public static void NotifyInfo(string message, string title = "Ludusavi")
    {
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>
    /// Show an error message.
    /// </summary>
    public static void NotifyError(string message, string title = "Ludusavi Error")
    {
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    /// <summary>
    /// Ask user with Always/Never options. Returns the choice.
    /// Returns null if skipped (unimplemented for now, defaults to Yes/No).
    /// </summary>
    public static Choice? AskChoice(string message, string title = "Ludusavi")
    {
        var result = MessageBox.Show(
            message + "\n\nYes = Just this time\nNo = Skip this time",
            title,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes) return Choice.Yes;
        return Choice.No;
    }

    /// <summary>
    /// Show a text input dialog.
    /// </summary>
    public static string? InputText(string prompt, string title = "Ludusavi", string defaultText = "")
    {
        // Simple text input using a dedicated form
        using var form = new Form
        {
            Text = title,
            Width = 400,
            Height = 150,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterScreen
        };

        var label = new Label { Text = prompt, Left = 10, Top = 10, Width = 360 };
        var textBox = new TextBox { Text = defaultText, Left = 10, Top = 35, Width = 360 };
        var buttonOk = new Button { Text = "OK", Left = 200, Top = 70, Width = 80, DialogResult = DialogResult.OK };
        var buttonCancel = new Button { Text = "Cancel", Left = 290, Top = 70, Width = 80, DialogResult = DialogResult.Cancel };

        form.Controls.Add(label);
        form.Controls.Add(textBox);
        form.Controls.Add(buttonOk);
        form.Controls.Add(buttonCancel);
        form.AcceptButton = buttonOk;
        form.CancelButton = buttonCancel;

        return form.ShowDialog() == DialogResult.OK ? textBox.Text : null;
    }

    // ===== Custom Field (Tag) Management =====

    public static void AddTag(IGame game, string tagName)
    {
        if (Etc.HasCustomField(game, tagName)) return;

        // Remove conflicting tags
        if (Tags.Conflicts.TryGetValue(tagName, out var conflicts))
        {
            foreach (var conflict in conflicts)
                Etc.RemoveCustomField(game, conflict);
        }

        Etc.SetCustomField(game, tagName, "true");
    }

    public static void RemoveTag(IGame game, string tagName)
    {
        Etc.RemoveCustomField(game, tagName);
    }

    public static bool HasTag(IGame game, string tagName)
    {
        return Etc.HasCustomField(game, tagName);
    }

    /// <summary>
    /// Opens a folder in Windows Explorer.
    /// </summary>
    public static void OpenFolder(string path)
    {
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", Etc.NormalizePath(path));
        }
        catch { }
    }
}
