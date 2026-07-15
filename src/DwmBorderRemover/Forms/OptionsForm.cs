using DwmBorderRemover.Core;

namespace DwmBorderRemover.Forms;

internal sealed class OptionsForm : Form
{
    private readonly AppSettings _workingSettings;
    private readonly CheckBox _autoStartCheckBox;
    private readonly ComboBox _modeComboBox;
    private readonly ComboBox _cornerComboBox;
    private readonly ComboBox _compatibilityComboBox;
    private readonly Label _compatibilityDescription;
    private readonly Label _listDescription;
    private readonly ListView _programList;

    internal AppSettings? ResultSettings { get; private set; }

    internal OptionsForm(AppSettings settings)
    {
        _workingSettings = settings.Clone();

        Text = "DWM Border Remover — Options";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 590);
        ClientSize = new Size(820, 650);
        AutoScaleMode = AutoScaleMode.Dpi;

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(22),
            BackColor = UiTheme.Background
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Label heading = new()
        {
            Text = "Make Windows lose the tiny picture frames.",
            Font = new Font("Segoe UI Semibold", 16F),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };

        Label subheading = new()
        {
            Text = "Choose where border removal applies and how aggressively compatibility cases are handled.",
            ForeColor = UiTheme.Muted,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 18)
        };

        TableLayoutPanel generalGrid = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(16),
            Margin = new Padding(0, 0, 0, 16),
            BackColor = UiTheme.Panel
        };
        generalGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        generalGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _autoStartCheckBox = new CheckBox
        {
            Text = "Start automatically with Windows",
            Checked = _workingSettings.AutoStart,
            AutoSize = true,
            Anchor = AnchorStyles.Left
        };

        _modeComboBox = CreateComboBox(
            new ComboEntry<RuleMode>("Exclude listed programs", RuleMode.Exclude),
            new ComboEntry<RuleMode>("Include only listed programs", RuleMode.Include));
        SelectValue(_modeComboBox, _workingSettings.Mode);
        _modeComboBox.SelectedIndexChanged += (_, _) => UpdateListDescription();

        _cornerComboBox = CreateComboBox(
            new ComboEntry<CornerStyle>("Rounded", CornerStyle.Rounded),
            new ComboEntry<CornerStyle>("Rounded (small radius)", CornerStyle.RoundedSmall),
            new ComboEntry<CornerStyle>("System default", CornerStyle.SystemDefault),
            new ComboEntry<CornerStyle>("Square", CornerStyle.Square));
        SelectValue(_cornerComboBox, _workingSettings.Corners);

        _compatibilityComboBox = CreateComboBox(
            new ComboEntry<CompatibilityMode>("Efficient — event driven", CompatibilityMode.Efficient),
            new ComboEntry<CompatibilityMode>("Automatic — poll known problem apps", CompatibilityMode.Automatic),
            new ComboEntry<CompatibilityMode>("Aggressive — poll every managed window", CompatibilityMode.Aggressive));
        SelectValue(_compatibilityComboBox, _workingSettings.Compatibility);
        _compatibilityComboBox.SelectedIndexChanged += (_, _) => UpdateCompatibilityDescription();

        generalGrid.Controls.Add(CreateSettingLabel("Autostart"), 0, 0);
        generalGrid.Controls.Add(_autoStartCheckBox, 1, 0);
        generalGrid.Controls.Add(CreateSettingLabel("Program-list mode"), 0, 1);
        generalGrid.Controls.Add(_modeComboBox, 1, 1);
        generalGrid.Controls.Add(CreateSettingLabel("Window corners"), 0, 2);
        generalGrid.Controls.Add(_cornerComboBox, 1, 2);
        generalGrid.Controls.Add(CreateSettingLabel("Compatibility"), 0, 3);
        generalGrid.Controls.Add(_compatibilityComboBox, 1, 3);

        Panel programPanel = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            BackColor = UiTheme.Panel,
            Margin = new Padding(0)
        };

        Label programHeading = new()
        {
            Text = "Program list",
            Font = new Font("Segoe UI Semibold", 11F),
            AutoSize = true,
            Location = new Point(16, 14)
        };

        _listDescription = new Label
        {
            AutoSize = true,
            ForeColor = UiTheme.Muted,
            Location = new Point(16, 42)
        };

        _programList = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = true,
            HideSelection = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Location = new Point(16, 70),
            Size = new Size(744, 225)
        };
        _programList.Columns.Add("Program", 220);
        _programList.Columns.Add("Executable", 150);
        _programList.Columns.Add("Path / match", 350);

        FlowLayoutPanel listButtons = new()
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            Location = new Point(16, 305)
        };

        Button pickButton = new() { Text = "Pick running window…", AutoSize = true };
        Button browseButton = new() { Text = "Add executable…", AutoSize = true };
        Button removeButton = new() { Text = "Remove", AutoSize = true };
        Button clearButton = new() { Text = "Clear", AutoSize = true };

        pickButton.Click += (_, _) => PickRunningWindow();
        browseButton.Click += (_, _) => AddExecutable();
        removeButton.Click += (_, _) => RemoveSelectedPrograms();
        clearButton.Click += (_, _) =>
        {
            _workingSettings.Programs.Clear();
            RefreshProgramList();
        };

        listButtons.Controls.AddRange([pickButton, browseButton, removeButton, clearButton]);

        programPanel.Controls.AddRange([programHeading, _listDescription, _programList, listButtons]);
        programPanel.Resize += (_, _) =>
        {
            _programList.Width = Math.Max(300, programPanel.ClientSize.Width - 32);
            _programList.Height = Math.Max(120, programPanel.ClientSize.Height - 130);
            listButtons.Top = _programList.Bottom + 10;
        };

        _compatibilityDescription = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Height = 52,
            ForeColor = UiTheme.Muted,
            Padding = new Padding(2, 10, 2, 6),
            Margin = new Padding(0, 8, 0, 8)
        };

        FlowLayoutPanel footer = new()
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };

        Button saveButton = new()
        {
            Text = "Save",
            DialogResult = DialogResult.None,
            Size = new Size(110, 34)
        };
        Button cancelButton = new()
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Size = new Size(110, 34)
        };

        saveButton.Click += (_, _) => SaveAndClose();
        footer.Controls.AddRange([saveButton, cancelButton]);

        Panel headerPanel = new() { Dock = DockStyle.Fill, AutoSize = true, BackColor = UiTheme.Background };
        headerPanel.Controls.Add(heading);
        subheading.Top = heading.Bottom + 4;
        headerPanel.Controls.Add(subheading);
        headerPanel.Height = subheading.Bottom + 4;

        root.Controls.Add(headerPanel, 0, 0);
        root.Controls.Add(generalGrid, 0, 1);
        root.Controls.Add(programPanel, 0, 2);
        root.Controls.Add(_compatibilityDescription, 0, 3);
        root.Controls.Add(footer, 0, 4);

        Controls.Add(root);
        AcceptButton = saveButton;
        CancelButton = cancelButton;

        UiTheme.Apply(this);
        UiTheme.StyleButton(saveButton, primary: true);
        UiTheme.StyleButton(cancelButton, primary: false);
        UiTheme.StyleButton(pickButton, primary: false);
        UiTheme.StyleButton(browseButton, primary: false);
        UiTheme.StyleButton(removeButton, primary: false);
        UiTheme.StyleButton(clearButton, primary: false);

        RefreshProgramList();
        UpdateListDescription();
        UpdateCompatibilityDescription();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UiTheme.EnableImmersiveDarkMode(this);
    }

    private static ComboBox CreateComboBox(params object[] items)
    {
        ComboBox comboBox = new()
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Width = 430,
            Margin = new Padding(4, 5, 4, 5)
        };
        comboBox.Items.AddRange(items);
        return comboBox;
    }

    private static Label CreateSettingLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = UiTheme.Muted,
            Margin = new Padding(0, 8, 12, 8)
        };
    }

    private static void SelectValue<T>(ComboBox comboBox, T value) where T : struct, Enum
    {
        for (int index = 0; index < comboBox.Items.Count; index++)
        {
            if (comboBox.Items[index] is ComboEntry<T> item && EqualityComparer<T>.Default.Equals(item.Value, value))
            {
                comboBox.SelectedIndex = index;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }

    private static T SelectedValue<T>(ComboBox comboBox) where T : struct, Enum
    {
        return comboBox.SelectedItem is ComboEntry<T> item ? item.Value : default;
    }

    private void UpdateListDescription()
    {
        RuleMode mode = SelectedValue<RuleMode>(_modeComboBox);
        _listDescription.Text = mode == RuleMode.Exclude
            ? "Border removal applies everywhere except these programs."
            : "Border removal applies only to these programs.";
    }

    private void UpdateCompatibilityDescription()
    {
        CompatibilityMode mode = SelectedValue<CompatibilityMode>(_compatibilityComboBox);
        _compatibilityDescription.Text = mode switch
        {
            CompatibilityMode.Efficient =>
                "Efficient mode reacts to window events and performs one recovery scan per minute. Lowest background activity.",
            CompatibilityMode.Automatic =>
                "Automatic mode remains event-driven, but periodically reapplies the setting only to known reset-prone apps such as Discord. Recommended.",
            _ =>
                "Aggressive mode reapplies the setting to every managed window every 750 ms. Use only when an app repeatedly restores its own border."
        };
    }

    private void PickRunningWindow()
    {
        using WindowPickerForm picker = new();
        Hide();
        DialogResult result = picker.ShowDialog();
        Show();
        Activate();

        if (result == DialogResult.OK && picker.SelectedWindow is not null)
        {
            AddWindowRule(picker.SelectedWindow);
        }
    }

    private void AddExecutable()
    {
        using OpenFileDialog dialog = new()
        {
            Title = "Choose a program executable",
            Filter = "Applications (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        string executableName = Path.GetFileName(dialog.FileName);
        AddOrReplaceRule(new ProgramRule
        {
            DisplayName = Path.GetFileNameWithoutExtension(dialog.FileName),
            ExecutableName = executableName,
            ExecutablePath = dialog.FileName
        });
    }

    private void AddWindowRule(WindowInfo window)
    {
        AddOrReplaceRule(new ProgramRule
        {
            DisplayName = string.IsNullOrWhiteSpace(window.ProcessName)
                ? window.ExecutableName
                : window.ProcessName,
            ExecutableName = window.ExecutableName,
            ExecutablePath = window.ExecutablePath
        });
    }

    private void AddOrReplaceRule(ProgramRule rule)
    {
        ProgramRule? existing = _workingSettings.Programs.FirstOrDefault(candidate =>
            string.Equals(candidate.ExecutableName, rule.ExecutableName, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.DisplayName = rule.DisplayName;
            existing.ExecutablePath = rule.ExecutablePath;
        }
        else
        {
            _workingSettings.Programs.Add(rule);
        }

        RefreshProgramList();
    }

    private void RemoveSelectedPrograms()
    {
        foreach (ListViewItem selected in _programList.SelectedItems)
        {
            if (selected.Tag is ProgramRule rule)
            {
                _workingSettings.Programs.Remove(rule);
            }
        }

        RefreshProgramList();
    }

    private void RefreshProgramList()
    {
        _programList.BeginUpdate();
        _programList.Items.Clear();

        foreach (ProgramRule rule in _workingSettings.Programs
                     .OrderBy(rule => rule.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            ListViewItem item = new(string.IsNullOrWhiteSpace(rule.DisplayName)
                ? rule.ExecutableName
                : rule.DisplayName);
            item.SubItems.Add(rule.ExecutableName);
            item.SubItems.Add(rule.MatchText);
            item.Tag = rule;
            _programList.Items.Add(item);
        }

        _programList.EndUpdate();
    }

    private void SaveAndClose()
    {
        _workingSettings.AutoStart = _autoStartCheckBox.Checked;
        _workingSettings.Mode = SelectedValue<RuleMode>(_modeComboBox);
        _workingSettings.Corners = SelectedValue<CornerStyle>(_cornerComboBox);
        _workingSettings.Compatibility = SelectedValue<CompatibilityMode>(_compatibilityComboBox);

        ResultSettings = _workingSettings.Clone();
        DialogResult = DialogResult.OK;
        Close();
    }

    private sealed record ComboEntry<T>(string Text, T Value) where T : struct, Enum
    {
        public override string ToString() => Text;
    }
}
