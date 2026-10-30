namespace PasteFence;

public sealed class DetectionForm : Form
{
    public event EventHandler? KeepSafeRequested;
    public event EventHandler? PermitOriginalRequested;
    public event EventHandler? DiscardRequested;
    public event EventHandler? HelpOpenRequested;

    public DetectionForm(ProtectionResult result, int permitSeconds)
    {
        Text = "PasteFence - 保護対象を検出";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(680, 520);
        MinimumSize = new Size(600, 440);
        TopMost = true;
        Font = new Font("Yu Gothic UI", 9F);
        Icon = SystemIcons.Warning;
        KeyPreview = true;

        var title = new Label
        {
            Text = "保護対象を検出し、安全版へ置き換えました",
            Font = new Font("Yu Gothic UI", 15F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(24, 20)
        };

        var rules = new Label
        {
            Text = "一致ルール: " + string.Join("、", result.MatchedRules),
            AutoSize = true,
            Location = new Point(27, 62),
            ForeColor = Color.FromArgb(55, 75, 105)
        };

        var split = new SplitContainer
        {
            Orientation = Orientation.Horizontal,
            Location = new Point(27, 92),
            Size = new Size(610, 300),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            SplitterDistance = 130
        };

        var reasonList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true
        };
        reasonList.Columns.Add("判定", 60);
        reasonList.Columns.Add("条件", 160);
        reasonList.Columns.Add("ルール", 180);
        reasonList.Columns.Add("理由", 180);
        // 一致したものを先に出す。ルールが増えると不一致の行が増え、
        // そのままの順では「何に当たったのか」が下へ埋もれて読めなくなる。
        foreach (var detail in result.Details.OrderByDescending(d => d.Matched))
        {
            var item = new ListViewItem(detail.Matched ? "一致" : "不一致");
            item.SubItems.Add(detail.Label);
            item.SubItems.Add(detail.RuleName);
            item.SubItems.Add(detail.Reason);
            item.ForeColor = detail.Matched ? Color.DarkGreen : Color.DimGray;
            reasonList.Items.Add(item);
        }

        var preview = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Text = result.SafeText,
            BackColor = Color.FromArgb(245, 247, 250)
        };
        split.Panel1.Controls.Add(reasonList);
        split.Panel2.Controls.Add(preview);

        var keepButton = new Button
        {
            Text = "安全版のまま",
            Size = new Size(130, 38),
            Location = new Point(27, 410),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        keepButton.Click += (_, _) => KeepSafeRequested?.Invoke(this, EventArgs.Empty);

        var permitButton = new Button
        {
            Text = $"原文を{permitSeconds}秒だけ許可",
            Size = new Size(180, 38),
            Location = new Point(168, 410),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        permitButton.Click += (_, _) => PermitOriginalRequested?.Invoke(this, EventArgs.Empty);

        var discardButton = new Button
        {
            Text = "クリップボードから破棄",
            Size = new Size(190, 38),
            Location = new Point(447, 410),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        discardButton.Click += (_, _) => DiscardRequested?.Invoke(this, EventArgs.Empty);

        var helpButton = new Button
        {
            Text = "使い方",
            Size = new Size(90, 30),
            Location = new Point(547, 20),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        helpButton.Click += (_, _) => HelpOpenRequested?.Invoke(this, EventArgs.Empty);

        KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.F1)
            {
                HelpOpenRequested?.Invoke(this, EventArgs.Empty);
                args.Handled = true;
            }
        };

        Controls.AddRange([title, rules, split, keepButton, permitButton, discardButton, helpButton]);
    }
}
