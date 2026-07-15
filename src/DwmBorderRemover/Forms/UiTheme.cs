using DwmBorderRemover.Interop;

namespace DwmBorderRemover.Forms;

internal static class UiTheme
{
    internal static readonly Color Background = Color.FromArgb(30, 31, 36);
    internal static readonly Color Panel = Color.FromArgb(39, 41, 48);
    internal static readonly Color Surface = Color.FromArgb(48, 50, 58);
    internal static readonly Color Foreground = Color.FromArgb(235, 237, 242);
    internal static readonly Color Muted = Color.FromArgb(168, 173, 184);
    internal static readonly Color Accent = Color.FromArgb(123, 97, 255);

    internal static void Apply(Form form)
    {
        form.BackColor = Background;
        form.ForeColor = Foreground;
        form.Font = new Font("Segoe UI", 9F);
        ApplyControl(form);
    }

    internal static void EnableImmersiveDarkMode(Form form)
    {
        if (!form.IsHandleCreated)
        {
            return;
        }

        int enabled = 1;
        _ = NativeMethods.DwmSetWindowAttribute(
            form.Handle,
            NativeMethods.DwmwaUseImmersiveDarkMode,
            ref enabled,
            sizeof(int));
    }

    private static void ApplyControl(Control control)
    {
        foreach (Control child in control.Controls)
        {
            child.ForeColor = Foreground;

            switch (child)
            {
                case System.Windows.Forms.Panel or TableLayoutPanel or FlowLayoutPanel or GroupBox:
                    child.BackColor = Panel;
                    break;
                case TextBox textBox:
                    textBox.BackColor = Surface;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case ComboBox comboBox:
                    comboBox.BackColor = Surface;
                    comboBox.FlatStyle = FlatStyle.Flat;
                    break;
                case ListView listView:
                    listView.BackColor = Surface;
                    listView.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case Button button:
                    StyleButton(button, primary: false);
                    break;
                default:
                    child.BackColor = control.BackColor;
                    break;
            }

            ApplyControl(child);
        }
    }

    internal static void StyleButton(Button button, bool primary)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(78, 81, 92);
        button.BackColor = primary ? Accent : Surface;
        button.ForeColor = Foreground;
        button.Padding = new Padding(10, 2, 10, 2);
        button.Cursor = Cursors.Hand;
    }
}
