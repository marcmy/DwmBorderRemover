using System.Diagnostics;
using DwmBorderRemover.Core;
using DwmBorderRemover.Forms;
using DwmBorderRemover.Services;

namespace DwmBorderRemover;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly SettingsStore _settingsStore;
    private readonly BorderEngine _borderEngine;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _enabledMenuItem;
    private readonly SynchronizationContext _synchronizationContext;
    private readonly IpcServer _ipcServer;

    private AppSettings _settings;
    private OptionsForm? _optionsForm;
    private bool _restoredForExit;

    internal TrayApplicationContext(
        SettingsStore settingsStore,
        AppSettings settings,
        SynchronizationContext synchronizationContext,
        bool showOptionsOnStart)
    {
        _settingsStore = settingsStore;
        _settings = settings;
        _synchronizationContext = synchronizationContext;

        _borderEngine = new BorderEngine(_settings);
        _borderEngine.Start();

        try
        {
            AutoStartManager.SetEnabled(_settings.AutoStart);
        }
        catch (Exception exception)
        {
            AppLog.Write("Unable to update autostart: " + exception.Message);
        }

        ContextMenuStrip menu = new()
        {
            ShowImageMargin = false,
            BackColor = UiTheme.Panel,
            ForeColor = UiTheme.Foreground
        };

        _enabledMenuItem = new ToolStripMenuItem("Enabled")
        {
            Checked = _settings.Enabled,
            CheckOnClick = true
        };
        _enabledMenuItem.CheckedChanged += (_, _) => SetEnabled(_enabledMenuItem.Checked);

        ToolStripMenuItem optionsItem = new("Options…");
        optionsItem.Click += (_, _) => ShowOptions();

        ToolStripMenuItem pickWindowItem = new("Pick window and add to list…");
        pickWindowItem.Click += (_, _) => PickWindowAndAddRule();

        ToolStripMenuItem refreshItem = new("Reapply now");
        refreshItem.Click += (_, _) => _borderEngine.RefreshAllWindows();

        ToolStripMenuItem aboutItem = new("About / compatibility notes");
        aboutItem.Click += (_, _) => ShowAbout();

        ToolStripMenuItem exitItem = new("Exit and restore borders");
        exitItem.Click += (_, _) => ExitAndRestore();

        menu.Items.AddRange([
            _enabledMenuItem,
            new ToolStripSeparator(),
            optionsItem,
            pickWindowItem,
            refreshItem,
            new ToolStripSeparator(),
            aboutItem,
            exitItem
        ]);

        Icon icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? Application.ExecutablePath)
                    ?? SystemIcons.Application;

        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "DWM Border Remover",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowOptions();

        _ipcServer = new IpcServer(_synchronizationContext, HandleIpcCommand);

        if (showOptionsOnStart)
        {
            System.Windows.Forms.Timer startupTimer = new() { Interval = 120 };
            startupTimer.Tick += (_, _) =>
            {
                startupTimer.Stop();
                startupTimer.Dispose();
                ShowOptions();
            };
            startupTimer.Start();
        }
    }

    private void SetEnabled(bool enabled)
    {
        if (_settings.Enabled == enabled)
        {
            return;
        }

        _settings.Enabled = enabled;
        SaveAndApplySettings();

        _notifyIcon.Text = enabled
            ? "DWM Border Remover — Enabled"
            : "DWM Border Remover — Disabled";
    }

    private void ShowOptions()
    {
        if (_optionsForm is { IsDisposed: false })
        {
            _optionsForm.Show();
            _optionsForm.WindowState = FormWindowState.Normal;
            _optionsForm.Activate();
            return;
        }

        _optionsForm = new OptionsForm(_settings);
        _optionsForm.FormClosed += (_, _) =>
        {
            if (_optionsForm?.DialogResult == DialogResult.OK &&
                _optionsForm.ResultSettings is not null)
            {
                bool enabledChanged = _settings.Enabled != _optionsForm.ResultSettings.Enabled;
                _settings = _optionsForm.ResultSettings;
                SaveAndApplySettings();

                if (enabledChanged)
                {
                    _enabledMenuItem.Checked = _settings.Enabled;
                }
            }

            _optionsForm = null;
        };
        _optionsForm.Show();
        _optionsForm.Activate();
    }

    private void PickWindowAndAddRule()
    {
        using WindowPickerForm picker = new();
        if (picker.ShowDialog() != DialogResult.OK || picker.SelectedWindow is null)
        {
            return;
        }

        WindowInfo window = picker.SelectedWindow;
        ProgramRule? existing = _settings.Programs.FirstOrDefault(rule =>
            string.Equals(rule.ExecutableName, window.ExecutableName, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            _settings.Programs.Add(new ProgramRule
            {
                DisplayName = string.IsNullOrWhiteSpace(window.ProcessName)
                    ? window.ExecutableName
                    : window.ProcessName,
                ExecutableName = window.ExecutableName,
                ExecutablePath = window.ExecutablePath
            });
        }
        else
        {
            existing.ExecutablePath = window.ExecutablePath ?? existing.ExecutablePath;
        }

        SaveAndApplySettings();

        string action = _settings.Mode == RuleMode.Include ? "included" : "excluded";
        _notifyIcon.ShowBalloonTip(
            2500,
            "Program added",
            $"{window.ExecutableName} is now {action} by the active list mode.",
            ToolTipIcon.Info);
    }

    private void SaveAndApplySettings()
    {
        try
        {
            _settingsStore.Save(_settings);
            AutoStartManager.SetEnabled(_settings.AutoStart);
            _borderEngine.UpdateSettings(_settings);
        }
        catch (Exception exception)
        {
            AppLog.Write("Unable to save settings: " + exception);
            MessageBox.Show(
                "The settings could not be saved.\n\n" + exception.Message,
                "DWM Border Remover",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void HandleIpcCommand(string command)
    {
        switch (command.Trim().ToLowerInvariant())
        {
            case "show-options":
            case "--show-options":
                ShowOptions();
                break;
            case "enable":
            case "--enable":
                _enabledMenuItem.Checked = true;
                break;
            case "disable":
            case "--disable":
                _enabledMenuItem.Checked = false;
                break;
            case "reapply":
            case "--reapply":
                _borderEngine.RefreshAllWindows();
                break;
            case "restore-and-exit":
            case "--restore-and-exit":
                try
                {
                    AutoStartManager.SetEnabled(false);
                }
                catch (Exception exception)
                {
                    AppLog.Write("Unable to remove autostart during uninstall: " + exception.Message);
                }
                ExitAndRestore();
                break;
            case "exit":
            case "--exit":
                ExitAndRestore();
                break;
            default:
                ShowOptions();
                break;
        }
    }

    private void ShowAbout()
    {
        const string message =
            "DWM Border Remover removes Windows 11's one-pixel DWM window border.\n\n" +
            "Compatibility notes:\n" +
            "• Some apps draw their own frame, so a thin dark outline may remain. Steam is a known example.\n" +
            "• Discord may reset the DWM attribute. Automatic mode periodically reapplies it only to Discord-family apps.\n" +
            "• Forced rounded corners are best-effort. Apps with custom-shaped windows may ignore them.\n" +
            "• Use the program list to include or exclude troublesome software.";

        DialogResult result = MessageBox.Show(
            message + "\n\nOpen the project page?",
            "DWM Border Remover",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (result == DialogResult.Yes)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/marcmy/DwmBorderRemover",
                    UseShellExecute = true
                });
            }
            catch
            {
                // The dialog itself is still useful if opening a browser fails.
            }
        }
    }

    private void ExitAndRestore()
    {
        if (_restoredForExit)
        {
            return;
        }

        _restoredForExit = true;
        _borderEngine.RestoreAll();
        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        if (!_restoredForExit)
        {
            _borderEngine.RestoreAll();
        }

        _ipcServer.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _borderEngine.Dispose();
        _optionsForm?.Dispose();
        base.ExitThreadCore();
    }
}
