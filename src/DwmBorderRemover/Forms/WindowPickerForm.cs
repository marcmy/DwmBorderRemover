using DwmBorderRemover.Core;
using DwmBorderRemover.Interop;

namespace DwmBorderRemover.Forms;

internal sealed class WindowPickerForm : Form
{
    private const int HotKeyId = 0xDB01;

    private readonly Label _targetLabel;
    private readonly Label _hintLabel;
    private readonly System.Windows.Forms.Timer _previewTimer;
    private WindowInfo? _currentTarget;

    internal WindowInfo? SelectedWindow { get; private set; }

    internal WindowPickerForm()
    {
        Text = "Pick a window";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;
        ClientSize = new Size(520, 190);
        AutoScaleMode = AutoScaleMode.Dpi;

        Label title = new()
        {
            Text = "Pick a program window",
            Font = new Font("Segoe UI Semibold", 15F),
            AutoSize = true,
            Location = new Point(22, 18)
        };

        _hintLabel = new Label
        {
            Text = "Move the pointer over the window you want, then press F8.",
            AutoSize = true,
            ForeColor = UiTheme.Muted,
            Location = new Point(24, 56)
        };

        _targetLabel = new Label
        {
            Text = "Waiting for a window…",
            AutoEllipsis = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = UiTheme.Surface,
            Padding = new Padding(10),
            Location = new Point(24, 88),
            Size = new Size(472, 48)
        };

        Button cancelButton = new()
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(391, 149),
            Size = new Size(105, 30)
        };

        Controls.AddRange([title, _hintLabel, _targetLabel, cancelButton]);
        CancelButton = cancelButton;

        _previewTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _previewTimer.Tick += (_, _) => UpdateTargetPreview();

        UiTheme.Apply(this);
        UiTheme.StyleButton(cancelButton, primary: false);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UiTheme.EnableImmersiveDarkMode(this);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (!NativeMethods.RegisterHotKey(Handle, HotKeyId, NativeMethods.ModNoRepeat, NativeMethods.VkF8))
        {
            _hintLabel.Text = "F8 is already in use by another program. Close it and try again.";
            _hintLabel.ForeColor = Color.OrangeRed;
            return;
        }

        _previewTimer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _previewTimer.Stop();
        _ = NativeMethods.UnregisterHotKey(Handle, HotKeyId);
        _previewTimer.Dispose();
        base.OnFormClosed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WmHotkey && m.WParam.ToInt32() == HotKeyId)
        {
            CaptureCurrentTarget();
            return;
        }

        base.WndProc(ref m);
    }

    private void UpdateTargetPreview()
    {
        WindowInfo? target = WindowCatalog.GetWindowUnderCursor();
        _currentTarget = target;

        if (target is null)
        {
            _targetLabel.Text = "No compatible top-level window under the pointer.";
            return;
        }

        string title = string.IsNullOrWhiteSpace(target.Title) ? "(untitled window)" : target.Title;
        _targetLabel.Text = $"{target.ExecutableName}  —  {title}";
    }

    private void CaptureCurrentTarget()
    {
        UpdateTargetPreview();
        if (_currentTarget is null)
        {
            System.Media.SystemSounds.Beep.Play();
            return;
        }

        SelectedWindow = _currentTarget;
        DialogResult = DialogResult.OK;
        Close();
    }
}
