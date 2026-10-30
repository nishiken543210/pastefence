namespace PasteFence;

public sealed class HelpForm : Form
{
    private readonly TextBox _searchBox;
    private readonly TreeView _topicTree;
    private readonly Label _topicTitle;
    private readonly Label _topicLead;
    private readonly RichTextBox _content;

    public event EventHandler? SettingsOpenRequested;
    public event EventHandler? LogsOpenRequested;

    public HelpForm()
    {
        Text = "PasteFence - ヘルプ";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1040, 760);
        MinimumSize = new Size(850, 620);
        Font = new Font("Yu Gothic UI", 9F);
        Icon = AppIcons.Window;
        KeyPreview = true;

        var title = new Label
        {
            Text = "PasteFence ヘルプ",
            Font = new Font("Yu Gothic UI", 18F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(22, 14)
        };
        var subtitle = new Label
        {
            Text = "目的、基本操作、具体例、ルール設定、ログ、困った時の確認方法を説明します。",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Location = new Point(25, 53)
        };

        var split = new SplitContainer
        {
            Location = new Point(22, 82),
            Size = new Size(980, 570),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            SplitterDistance = 260,
            FixedPanel = FixedPanel.Panel1
        };

        var searchLabel = new Label { Text = "ヘルプを検索", AutoSize = true, Location = new Point(4, 3) };
        _searchBox = new TextBox
        {
            Location = new Point(4, 27),
            Size = new Size(246, 28),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            PlaceholderText = "例：AND、ログ、検出しない"
        };
        _searchBox.TextChanged += (_, _) => PopulateTree(_searchBox.Text);

        _topicTree = new TreeView
        {
            Location = new Point(4, 64),
            Size = new Size(246, 496),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            HideSelection = false,
            FullRowSelect = true
        };
        _topicTree.AfterSelect += (_, args) =>
        {
            if (args.Node?.Tag is HelpTopic topic)
                RenderTopic(topic);
        };
        split.Panel1.Controls.AddRange([searchLabel, _searchBox, _topicTree]);

        _topicTitle = new Label
        {
            Font = new Font("Yu Gothic UI", 16F, FontStyle.Bold),
            AutoSize = false,
            Location = new Point(18, 8),
            Size = new Size(675, 38),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _topicLead = new Label
        {
            AutoSize = false,
            Location = new Point(20, 50),
            Size = new Size(670, 54),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = Color.FromArgb(55, 75, 105)
        };
        _content = new RichTextBox
        {
            Location = new Point(18, 108),
            Size = new Size(675, 452),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            DetectUrls = false,
            ScrollBars = RichTextBoxScrollBars.Vertical
        };
        split.Panel2.Controls.AddRange([_topicTitle, _topicLead, _content]);

        var test = new Button
        {
            Text = "テスト文字をコピー",
            Size = new Size(150, 38),
            Location = new Point(22, 668),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        test.Click += (_, _) => CopyTestSample();
        var settings = new Button
        {
            Text = "保護ルール設定",
            Size = new Size(145, 38),
            Location = new Point(182, 668),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        settings.Click += (_, _) => SettingsOpenRequested?.Invoke(this, EventArgs.Empty);
        var logs = new Button
        {
            Text = "ログフォルダー",
            Size = new Size(135, 38),
            Location = new Point(337, 668),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        logs.Click += (_, _) => LogsOpenRequested?.Invoke(this, EventArgs.Empty);
        var note = new Label
        {
            Text = "F1: ヘルプ　Esc: 閉じる",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Location = new Point(668, 679),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        var close = new Button
        {
            Text = "閉じる",
            DialogResult = DialogResult.OK,
            Size = new Size(100, 38),
            Location = new Point(902, 668),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };

        Controls.AddRange([title, subtitle, split, test, settings, logs, note, close]);
        AcceptButton = close;
        CancelButton = close;
        KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.Escape)
                Close();
            if (args.KeyCode == Keys.F && args.Control)
            {
                _searchBox.Focus();
                _searchBox.SelectAll();
                args.Handled = true;
            }
        };

        PopulateTree(string.Empty);
    }

    private void PopulateTree(string query)
    {
        var normalized = query.Trim();
        var selectedId = (_topicTree.SelectedNode?.Tag as HelpTopic)?.Id;
        _topicTree.BeginUpdate();
        _topicTree.Nodes.Clear();

        var topics = string.IsNullOrWhiteSpace(normalized)
            ? HelpContent.Topics
            : HelpContent.Topics.Where(topic => topic.SearchText.Contains(normalized, StringComparison.CurrentCultureIgnoreCase)).ToList();

        foreach (var group in topics.GroupBy(topic => topic.Category))
        {
            var categoryNode = new TreeNode(group.Key);
            foreach (var topic in group)
                categoryNode.Nodes.Add(new TreeNode(topic.Title) { Tag = topic });
            _topicTree.Nodes.Add(categoryNode);
        }

        _topicTree.ExpandAll();
        _topicTree.EndUpdate();

        TreeNode? target = null;
        foreach (TreeNode category in _topicTree.Nodes)
        {
            target = category.Nodes.Cast<TreeNode>().FirstOrDefault(node => (node.Tag as HelpTopic)?.Id == selectedId) ?? target;
        }
        target ??= _topicTree.Nodes.Cast<TreeNode>().SelectMany(node => node.Nodes.Cast<TreeNode>()).FirstOrDefault();

        if (target is not null)
            _topicTree.SelectedNode = target;
        else
            RenderNoResults(normalized);
    }

    public void SelectTopic(string topicId)
    {
        if (!string.IsNullOrEmpty(_searchBox.Text))
            _searchBox.Text = string.Empty;

        foreach (TreeNode category in _topicTree.Nodes)
        {
            var target = category.Nodes.Cast<TreeNode>()
                .FirstOrDefault(node => (node.Tag as HelpTopic)?.Id == topicId);
            if (target is null)
                continue;

            _topicTree.SelectedNode = target;
            target.EnsureVisible();
            return;
        }
    }

    private void RenderTopic(HelpTopic topic)
    {
        _topicTitle.Text = topic.Title;
        _topicLead.Text = topic.Lead;
        _content.Clear();

        foreach (var section in topic.Sections)
        {
            _content.SelectionFont = new Font("Yu Gothic UI", 11F, FontStyle.Bold);
            _content.SelectionColor = Color.FromArgb(25, 55, 95);
            _content.AppendText(section.Title + Environment.NewLine);
            _content.SelectionFont = new Font("Yu Gothic UI", 9.5F, FontStyle.Regular);
            _content.SelectionColor = Color.FromArgb(30, 35, 45);
            _content.AppendText(section.Body + Environment.NewLine + Environment.NewLine);
        }

        _content.SelectionStart = 0;
        _content.ScrollToCaret();
    }

    private void RenderNoResults(string query)
    {
        _topicTitle.Text = "該当する項目がありません";
        _topicLead.Text = $"『{query}』に一致する説明は見つかりませんでした。別の言葉で検索してください。";
        _content.Clear();
    }

    private void CopyTestSample()
    {
        try
        {
            Clipboard.SetText(HelpContent.TestSample, TextDataFormat.UnicodeText);
        }
        catch (Exception ex)
        {
            MessageBox.Show("テスト文字をコピーできませんでした。\r\n\r\n" + ex.Message,
                "PasteFence", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
