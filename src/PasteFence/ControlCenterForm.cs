namespace PasteFence;

public sealed class ControlCenterForm : Form
{
    private readonly Label _statusValue;
    private readonly Label _lastEventValue;

    public event EventHandler? SettingsRequested;
    public event EventHandler? HelpOpenRequested;
    public event EventHandler? LogsRequested;
    public event EventHandler? ExitRequested;

    public ControlCenterForm()
    {
        SuspendLayout();

        // 拡大表示（150%・200% など）への対応。倍率は 現在のDPI ÷ 96 で決まり、
        // 100% ではちょうど 1.0 になるので何も起きない。詳細は docs/high-dpi.md。
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);

        Text = "PasteFence - コントロールセンター";
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Yu Gothic UI", 9F);
        Icon = AppIcons.Window;
        KeyPreview = true;

        // 部品の位置をピクセルで決めず、中身の高さに合わせて縦に積む。
        // 以前は全部が絶対座標だったため、拡大表示で文字だけが伸びて
        // 下のボタンが窓の外へ出てしまっていた。
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(22, 16, 22, 16)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var i = 0; i < 5; i++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // 余った高さを吸う行
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // ボタン列は常に最下段

        var title = new Label
        {
            Text = "PasteFence",
            Font = new Font("Yu Gothic UI", 18F, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0)
        };
        var subtitle = new Label
        {
            Text = "コピー内容を端末内で判定し、安全版へ置き換えます。",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(2, 2, 0, 14)
        };

        _statusValue = new Label
        {
            Text = "監視中",
            AutoSize = true,
            Font = new Font("Yu Gothic UI", 12F, FontStyle.Bold),
            ForeColor = Color.DarkGreen,
            Margin = new Padding(0)
        };
        // 長いイベント文で窓が横に伸びないよう、幅は列に任せて末尾を省略する
        _lastEventValue = new Label
        {
            Text = "まだありません",
            AutoSize = false,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0)
        };

        var status = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 2,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 14)
        };
        status.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        status.Controls.Add(new Label { Text = "保護状態", AutoSize = true, Margin = new Padding(0, 6, 20, 6) }, 0, 0);
        status.Controls.Add(_statusValue, 1, 0);
        status.Controls.Add(new Label { Text = "最後のイベント", AutoSize = true, Margin = new Padding(0, 6, 20, 0) }, 0, 1);
        status.Controls.Add(_lastEventValue, 1, 1);

        var guide = BuildGuide();

        var privacy = new Label
        {
            Text = "ログにはクリップボード本文を保存しません。安全版にも履歴・クラウド同期の抑制情報を付けます。",
            AutoSize = true,
            ForeColor = Color.FromArgb(55, 75, 105),
            Margin = new Padding(0, 14, 0, 0)
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            // 窓が狭いときは切り捨てずに折り返す
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 16, 0, 0)
        };
        buttons.Controls.Add(MakeButton("使い方", (_, _) => HelpOpenRequested?.Invoke(this, EventArgs.Empty)));
        buttons.Controls.Add(MakeButton("保護ルール設定", (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty)));
        buttons.Controls.Add(MakeButton("ログフォルダー", (_, _) => LogsRequested?.Invoke(this, EventArgs.Empty)));
        buttons.Controls.Add(MakeButton("閉じて常駐", (_, _) => Hide()));
        buttons.Controls.Add(MakeButton("終了", (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty)));

        root.Controls.Add(title, 0, 0);
        root.Controls.Add(subtitle, 0, 1);
        root.Controls.Add(status, 0, 2);
        root.Controls.Add(guide, 0, 3);
        root.Controls.Add(privacy, 0, 4);
        root.Controls.Add(new Panel { Margin = new Padding(0), Height = 0 }, 0, 5);
        root.Controls.Add(buttons, 0, 6);

        FormClosing += (_, args) =>
        {
            if (args.CloseReason == CloseReason.UserClosing)
            {
                args.Cancel = true;
                Hide();
            }
        };

        KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.F1)
            {
                HelpOpenRequested?.Invoke(this, EventArgs.Empty);
                args.Handled = true;
            }
        };

        Controls.Add(root);

        // ここまでで部品が出揃ったので、拡大を走らせる。
        ResumeLayout(performLayout: false);

        // 中身がちょうど入る大きさを実測して、それを窓の下限にする。
        // 拡大率がいくつでもボタンが窓の外へ出ない。これより小さくはできない。
        var needed = root.GetPreferredSize(Size.Empty);
        // 「最後のイベント」の文が伸びたときのために 1 行ぶん余裕を足す
        var oneLine = TextRenderer.MeasureText("あ", Font).Height;
        ClientSize = new Size(needed.Width, needed.Height + oneLine);
        MinimumSize = Size;
    }

    private Button MakeButton(string text, EventHandler onClick)
    {
        // 大きさを決め打ちしない。文字に合わせて伸びるので拡大表示でも欠けない。
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12, 6, 12, 6),
            Margin = new Padding(0, 0, 10, 0)
        };
        button.Click += onClick;
        return button;
    }

    private GroupBox BuildGuide()
    {
        var inner = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0)
        };
        inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        foreach (var step in new[]
                 {
                     "1. 文章をコピーします（顧客名・電話番号・API キーなどを含むもの）",
                     "2. 隠す対象が見つかると、確認の画面が出ます",
                     "3. 貼り付けると、伏せ字にした安全版が貼られます"
                 })
        {
            inner.Controls.Add(new Label { Text = step, AutoSize = true, Margin = new Padding(0, 3, 0, 3) });
        }

        inner.Controls.Add(new Label
        {
            Text = "原文のまま貼りたいときは、確認の画面で「原文を◯秒だけ許可」を押します。",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 8, 0, 2)
        });

        var test = MakeButton("テスト文字をコピーして試す", (_, _) => CopyTestSample());
        test.Margin = new Padding(0, 10, 0, 2);
        inner.Controls.Add(test);

        var box = new GroupBox
        {
            Text = "はじめかた",
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 8, 14, 12),
            Margin = new Padding(0)
        };
        box.Controls.Add(inner);
        return box;
    }

    private static void CopyTestSample()
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

    public void UpdateState(string status, string lastEvent, bool isHealthy)
    {
        _statusValue.Text = status;
        _statusValue.ForeColor = isHealthy ? Color.DarkGreen : Color.DarkRed;
        _lastEventValue.Text = lastEvent;
    }
}
