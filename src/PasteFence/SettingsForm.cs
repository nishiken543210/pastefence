using System.ComponentModel;

namespace PasteFence;

public sealed class SettingsSavedEventArgs(PasteFenceConfig config) : EventArgs
{
    public PasteFenceConfig Config { get; } = config;
}

public sealed class SettingsForm : Form
{
    public event EventHandler? HelpOpenRequested;
    public event EventHandler<SettingsSavedEventArgs>? SettingsSaved;
    private readonly BindingList<ProtectionRule> _rules;
    private readonly BindingSource _ruleSource = new();
    private readonly BindingSource _conditionSource = new();
    private readonly DataGridView _ruleGrid;
    private readonly DataGridView _conditionGrid;
    private readonly Label _ruleCountLabel;
    private readonly NumericUpDown _permitSeconds;
    private readonly NumericUpDown _retentionDays;

    public PasteFenceConfig EditedConfig { get; }

    public SettingsForm(PasteFenceConfig source)
    {
        EditedConfig = Clone(source);
        _rules = new BindingList<ProtectionRule>(EditedConfig.Rules);
        _ruleSource.DataSource = _rules;
        _rules.ListChanged += (_, _) => UpdateRuleCount();

        Text = "PasteFence - 保護ルール設定";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1040, 700);
        MinimumSize = new Size(850, 570);
        Font = new Font("Yu Gothic UI", 9F);
        Icon = AppIcons.Window;
        KeyPreview = true;

        var title = new Label
        {
            Text = "保護ルール",
            Font = new Font("Yu Gothic UI", 16F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 16)
        };
        var hint = new Label
        {
            Text = "上段がルール、下段が選択中ルールの複数条件です。AND／ORはルール単位で指定します。",
            AutoSize = true,
            Location = new Point(22, 54),
            ForeColor = Color.DimGray
        };
        var help = new Button { Text = "設定ヘルプ", Size = new Size(100, 32), Location = new Point(902, 18), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        help.Click += (_, _) => HelpOpenRequested?.Invoke(this, EventArgs.Empty);

        _ruleCountLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Yu Gothic UI", 10F, FontStyle.Bold),
            Location = new Point(22, 88)
        };

        var addRule = new Button
        {
            Text = "＋ 新しいルール",
            Size = new Size(170, 38),
            Location = new Point(832, 76),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.FromArgb(75, 125, 220),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            AccessibleDescription = "保護ルールを新しく追加します"
        };
        addRule.FlatAppearance.BorderSize = 0;
        addRule.Click += (_, _) => AddNewRule();

        _ruleGrid = BuildRuleGrid();
        _ruleGrid.Location = new Point(22, 122);
        _ruleGrid.Size = new Size(980, 175);
        _ruleGrid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _ruleGrid.DataSource = _ruleSource;
        _ruleGrid.SelectionChanged += (_, _) => BindSelectedConditions();

        var deleteRule = new Button { Text = "選択ルールを削除", Size = new Size(150, 32), Location = new Point(22, 307) };
        deleteRule.Click += (_, _) =>
        {
            if (_ruleSource.Current is ProtectionRule rule &&
                MessageBox.Show($"『{rule.Name}』を削除しますか？", "PasteFence", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                _rules.Remove(rule);
        };

        var conditionTitle = new Label
        {
            Text = "選択中ルールの判定条件",
            Font = new Font("Yu Gothic UI", 11F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(22, 352)
        };
        var conditionHint = new Label
        {
            Text = "条件は下の表へ追加します。ルール名とAND／ORは上の選択行で変更できます。",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Location = new Point(250, 356)
        };
        _conditionGrid = BuildConditionGrid();
        _conditionGrid.Location = new Point(22, 382);
        _conditionGrid.Size = new Size(980, 168);
        _conditionGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _conditionGrid.DataSource = _conditionSource;

        var addCondition = new Button { Text = "条件追加", Size = new Size(105, 32), Location = new Point(22, 562), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        addCondition.Click += (_, _) =>
        {
            if (_conditionSource.DataSource is BindingList<ProtectionCondition> conditions)
                conditions.Add(new ProtectionCondition());
        };
        var deleteCondition = new Button { Text = "選択条件削除", Size = new Size(125, 32), Location = new Point(137, 562), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        deleteCondition.Click += (_, _) =>
        {
            if (_conditionSource.Current is ProtectionCondition condition &&
                _conditionSource.DataSource is BindingList<ProtectionCondition> conditions)
                conditions.Remove(condition);
        };

        var permitLabel = new Label { Text = "原文許可", AutoSize = true, Location = new Point(292, 570), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        _permitSeconds = new NumericUpDown { Minimum = 1, Maximum = 300, Value = EditedConfig.OriginalAccessSeconds, Size = new Size(70, 28), Location = new Point(360, 565), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        var secondsLabel = new Label { Text = "秒", AutoSize = true, Location = new Point(434, 570), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        var retentionLabel = new Label { Text = "ログ保持", AutoSize = true, Location = new Point(476, 570), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        _retentionDays = new NumericUpDown { Minimum = 1, Maximum = 365, Value = EditedConfig.LogRetentionDays, Size = new Size(70, 28), Location = new Point(544, 565), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        var daysLabel = new Label { Text = "日", AutoSize = true, Location = new Point(618, 570), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };

        var cancel = new Button { Text = "キャンセル", Size = new Size(110, 38), Location = new Point(754, 610), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        cancel.Click += (_, _) => Close();
        var save = new Button { Text = "保存して反映", Size = new Size(130, 38), Location = new Point(872, 610), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        save.Click += (_, _) => SaveAndClose();

        KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.F1)
            {
                HelpOpenRequested?.Invoke(this, EventArgs.Empty);
                args.Handled = true;
            }
        };

        Controls.AddRange([title, hint, help, _ruleCountLabel, addRule, _ruleGrid, deleteRule, conditionTitle, conditionHint, _conditionGrid, addCondition, deleteCondition,
            permitLabel, _permitSeconds, secondsLabel, retentionLabel, _retentionDays, daysLabel, cancel, save]);
        AcceptButton = save;
        CancelButton = cancel;

        Shown += (_, _) =>
        {
            if (_ruleGrid.Rows.Count > 0)
            {
                _ruleGrid.Rows[0].Selected = true;
                _ruleGrid.CurrentCell = _ruleGrid.Rows[0].Cells[1];
            }
            BindSelectedConditions();
        };
        UpdateRuleCount();
    }

    private DataGridView BuildRuleGrid()
    {
        var grid = BaseGrid();
        grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "有効", DataPropertyName = nameof(ProtectionRule.Enabled), Width = 60 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ルール名", DataPropertyName = nameof(ProtectionRule.Name), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        var mode = new DataGridViewComboBoxColumn { HeaderText = "条件の接続", DataPropertyName = nameof(ProtectionRule.MatchModeDisplay), Width = 180 };
        mode.Items.AddRange("AND（すべて一致）", "OR（どれか一致）");
        grid.Columns.Add(mode);
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "条件数", ReadOnly = true, Width = 90 });
        grid.CellFormatting += (_, args) =>
        {
            if (args.ColumnIndex == 3 && args.RowIndex >= 0 && grid.Rows[args.RowIndex].DataBoundItem is ProtectionRule rule)
                args.Value = rule.Conditions.Count;
        };
        return grid;
    }

    private static DataGridView BuildConditionGrid()
    {
        var grid = BaseGrid();
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "条件名", DataPropertyName = nameof(ProtectionCondition.Label), Width = 160 });
        var type = new DataGridViewComboBoxColumn { HeaderText = "判定方法", DataPropertyName = nameof(ProtectionCondition.MatchTypeDisplay), Width = 130 };
        type.Items.AddRange("文字を含む", "正規表現");
        grid.Columns.Add(type);
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "検出文字／パターン", DataPropertyName = nameof(ProtectionCondition.Pattern), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "安全版での表示", DataPropertyName = nameof(ProtectionCondition.ReplaceWith), Width = 220 });
        return grid;
    }

    private static DataGridView BaseGrid() => new()
    {
        AutoGenerateColumns = false,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        RowHeadersVisible = false,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.Fixed3D
    };

    private void BindSelectedConditions()
    {
        _ruleGrid.EndEdit();
        if (_ruleSource.Current is ProtectionRule rule)
        {
            var conditions = new BindingList<ProtectionCondition>(rule.Conditions);
            conditions.ListChanged += (_, _) => _ruleGrid.InvalidateColumn(3);
            _conditionSource.DataSource = conditions;
        }
        else
            _conditionSource.DataSource = null;
    }

    private void AddNewRule()
    {
        _ruleGrid.EndEdit();
        var rule = new ProtectionRule
        {
            Name = $"新しいルール {_rules.Count + 1}",
            MatchMode = "all",
            Conditions = [new ProtectionCondition
            {
                Label = "新しい条件",
                MatchType = "contains",
                Pattern = "",
                ReplaceWith = "[保護]"
            }]
        };

        _rules.Add(rule);
        _ruleGrid.ClearSelection();
        var row = _ruleGrid.Rows[^1];
        row.Selected = true;
        _ruleGrid.CurrentCell = row.Cells[1];
        _ruleGrid.FirstDisplayedScrollingRowIndex = row.Index;
        _ruleGrid.BeginEdit(selectAll: true);
    }

    private void UpdateRuleCount()
    {
        if (_ruleCountLabel is null)
            return;
        _ruleCountLabel.Text = $"登録ルール：{_rules.Count}件　（必要な数だけ追加できます）";
    }

    private void SaveAndClose()
    {
        try
        {
            Validate();
            _ruleGrid.EndEdit();
            _conditionGrid.EndEdit();
            _ruleSource.EndEdit();
            _conditionSource.EndEdit();
            EditedConfig.Rules = _rules.ToList();
            EditedConfig.OriginalAccessSeconds = (int)_permitSeconds.Value;
            EditedConfig.LogRetentionDays = (int)_retentionDays.Value;
            RuleEngine.Validate(EditedConfig);
            SettingsSaved?.Invoke(this, new SettingsSavedEventArgs(EditedConfig));
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "設定を保存できません", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static PasteFenceConfig Clone(PasteFenceConfig source) => new()
    {
        OriginalAccessSeconds = source.OriginalAccessSeconds,
        LogRetentionDays = source.LogRetentionDays,
        Rules = source.Rules.Select(rule => new ProtectionRule
        {
            Name = rule.Name,
            Enabled = rule.Enabled,
            MatchMode = rule.MatchMode,
            Conditions = rule.Conditions.Select(condition => new ProtectionCondition
            {
                Label = condition.Label,
                MatchType = condition.MatchType,
                Pattern = condition.Pattern,
                ReplaceWith = condition.ReplaceWith
            }).ToList()
        }).ToList()
    };
}
