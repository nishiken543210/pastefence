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
        Text = "PasteFence - コントロールセンター";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(730, 330);
        // この画面だけ部品が絶対座標で置かれており、縮めるとボタンが隠れて押せなくなる
        // （下 17px・右 16px がはみ出す。他の 3 画面は Anchor で追従する）。
        // 設計どおりの大きさを下限にして、隠れない状態を保証する。
        MinimumSize = new Size(730, 330);
        Font = new Font("Yu Gothic UI", 9F);
        Icon = AppIcons.Window;
        KeyPreview = true;

        var title = new Label
        {
            Text = "PasteFence",
            Font = new Font("Yu Gothic UI", 18F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(25, 20)
        };
        var subtitle = new Label
        {
            Text = "コピー内容を端末内で判定し、安全版へ置き換えます。",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Location = new Point(28, 60)
        };

        var statusLabel = new Label { Text = "保護状態", AutoSize = true, Location = new Point(30, 110) };
        _statusValue = new Label
        {
            Text = "監視中",
            AutoSize = true,
            Font = new Font("Yu Gothic UI", 12F, FontStyle.Bold),
            ForeColor = Color.DarkGreen,
            Location = new Point(150, 105)
        };

        var lastEventLabel = new Label { Text = "最後のイベント", AutoSize = true, Location = new Point(30, 154) };
        _lastEventValue = new Label
        {
            Text = "まだありません",
            AutoEllipsis = true,
            Location = new Point(150, 154),
            Size = new Size(520, 36)
        };

        var privacy = new Label
        {
            Text = "ログにはクリップボード本文を保存しません。安全版にも履歴・クラウド同期の抑制情報を付けます。",
            AutoSize = false,
            Size = new Size(650, 38),
            Location = new Point(30, 196),
            ForeColor = Color.FromArgb(55, 75, 105)
        };

        var help = new Button { Text = "使い方", Size = new Size(110, 38), Location = new Point(30, 240) };
        help.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        help.Click += (_, _) => HelpOpenRequested?.Invoke(this, EventArgs.Empty);
        var settings = new Button { Text = "保護ルール設定", Size = new Size(145, 38), Location = new Point(150, 240) };
        settings.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        settings.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        var logs = new Button { Text = "ログフォルダー", Size = new Size(130, 38), Location = new Point(305, 240) };
        logs.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        logs.Click += (_, _) => LogsRequested?.Invoke(this, EventArgs.Empty);
        var hide = new Button { Text = "閉じて常駐", Size = new Size(115, 38), Location = new Point(445, 240) };
        hide.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        hide.Click += (_, _) => Hide();
        var exit = new Button { Text = "終了", Size = new Size(100, 38), Location = new Point(570, 240) };
        exit.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        exit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

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

        Controls.AddRange([title, subtitle, statusLabel, _statusValue, lastEventLabel, _lastEventValue, privacy, help, settings, logs, hide, exit]);
    }

    public void UpdateState(string status, string lastEvent, bool isHealthy)
    {
        _statusValue.Text = status;
        _statusValue.ForeColor = isHealthy ? Color.DarkGreen : Color.DarkRed;
        _lastEventValue.Text = lastEvent;
    }
}
