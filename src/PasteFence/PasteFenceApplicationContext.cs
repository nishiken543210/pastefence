using System.Diagnostics;

namespace PasteFence;

public sealed class PasteFenceApplicationContext : ApplicationContext
{
    private readonly string _configPath = ConfigLocator.Ensure();
    private readonly string _logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
    private readonly string _helpSeenPath = Path.Combine(AppContext.BaseDirectory, "pastefence-help-seen.txt");
    private PasteFenceConfig _config;
    private readonly AuditLogger _logger;
    private readonly ClipboardMonitorWindow _monitor;
    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly ControlCenterForm _controlCenter;
    private DetectionForm? _detectionForm;
    private HelpForm? _helpForm;
    private SettingsForm? _settingsForm;
    private string? _quarantinedOriginal;
    private string? _quarantinedSafe;
    private ProtectionResult? _lastResult;
    private DateTimeOffset? _permitUntil;
    private string? _lastWrittenText;
    private bool _handlingClipboard;
    private bool _exiting;

    public PasteFenceApplicationContext()
    {
        _config = PasteFenceConfig.Load(_configPath);
        RuleEngine.Validate(_config);
        _logger = new AuditLogger(_logDirectory, _config.LogRetentionDays);

        _controlCenter = new ControlCenterForm();
        _controlCenter.SettingsRequested += (_, _) => ShowSettings();
        _controlCenter.HelpOpenRequested += (_, _) => ShowHelp();
        _controlCenter.LogsRequested += (_, _) => OpenLogDirectory();
        _controlCenter.ExitRequested += (_, _) => ExitApplication();

        var menu = new ContextMenuStrip();
        menu.Items.Add("コントロールセンター", null, (_, _) => ShowControlCenter());
        menu.Items.Add("使い方", null, (_, _) => ShowHelp());
        menu.Items.Add("保護内容を表示", null, (_, _) => ShowLastDetection());
        menu.Items.Add($"原文を{_config.OriginalAccessSeconds}秒だけ許可", null, (_, _) => PermitOriginal());
        menu.Items.Add("隔離した原文を破棄", null, (_, _) => DiscardQuarantine(clearClipboard: true));
        menu.Items.Add("保護ルール設定", null, (_, _) => ShowSettings());
        menu.Items.Add("ログフォルダー", null, (_, _) => OpenLogDirectory());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("終了", null, (_, _) => ExitApplication());

        _trayIcon = new NotifyIcon
        {
            Icon = AppIcons.Tray,
            Text = "PasteFence - 監視中",
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ShowControlCenter();

        _monitor = new ClipboardMonitorWindow();
        _monitor.ClipboardChanged += OnClipboardChanged;

        _timer = new System.Windows.Forms.Timer { Interval = 250 };
        _timer.Tick += (_, _) => OnTimerTick();
        _timer.Start();

        _logger.Write("monitor", "started");
        UpdateStatus("監視中", "起動しました", true);
        _controlCenter.Show();
        var shownManualVersion = string.Empty;
        try { shownManualVersion = File.Exists(_helpSeenPath) ? File.ReadAllText(_helpSeenPath).Trim() : string.Empty; } catch { }
        if (!shownManualVersion.Equals(HelpContent.ManualVersion, StringComparison.Ordinal))
        {
            _controlCenter.BeginInvoke(() => ShowHelp());
            try { File.WriteAllText(_helpSeenPath, HelpContent.ManualVersion); } catch { }
        }
    }

    private void OnClipboardChanged(object? sender, EventArgs args)
    {
        if (_handlingClipboard || _exiting)
            return;

        _handlingClipboard = true;
        try
        {
            var current = ProtectedClipboard.TryGetText();
            if (current is null)
                return;

            if (_lastWrittenText is not null && current == _lastWrittenText)
            {
                _lastWrittenText = null;
                return;
            }

            if (_permitUntil.HasValue)
                ClearQuarantineState();
            else if (_quarantinedOriginal is not null)
                ClearQuarantineState();

            if (string.IsNullOrWhiteSpace(current))
                return;

            ProtectionResult result;
            try
            {
                result = RuleEngine.Evaluate(current, _config);
            }
            catch (Exception ex)
            {
                FailClose("rule-evaluation", ex.Message);
                return;
            }

            if (!result.Matched)
            {
                UpdateStatus("監視中", $"{DateTime.Now:HH:mm:ss} 判定対象なし", true);
                return;
            }

            _quarantinedOriginal = current;
            _quarantinedSafe = result.SafeText;
            _lastResult = result;
            _permitUntil = null;

            try
            {
                WriteProtectedClipboard(result.SafeText);
            }
            catch (Exception ex)
            {
                _logger.Write("protection", "failed", result.MatchedRules, result.MatchedLabels);
                FailClose("clipboard-write", ex.Message);
                return;
            }

            _logger.Write("protection", "safe-version-written", result.MatchedRules, result.MatchedLabels);
            UpdateStatus("原文をメモリ隔離中", $"{DateTime.Now:HH:mm:ss} {string.Join("、", result.MatchedRules)}を保護", true);
            ShowDetection(result);
            _trayIcon.ShowBalloonTip(2500, "保護対象を検出しました",
                $"{string.Join("、", result.MatchedLabels)}を安全版へ置き換えました。", ToolTipIcon.Warning);
        }
        finally
        {
            _handlingClipboard = false;
        }
    }

    private void OnTimerTick()
    {
        if (!_permitUntil.HasValue)
            return;

        var remaining = _permitUntil.Value - DateTimeOffset.Now;
        if (remaining > TimeSpan.Zero)
        {
            _trayIcon.Text = $"PasteFence - 原文許可 残り{Math.Ceiling(remaining.TotalSeconds)}秒";
            return;
        }

        try
        {
            var current = ProtectedClipboard.TryGetText();
            if (current == _quarantinedOriginal && _quarantinedSafe is not null)
                WriteProtectedClipboard(_quarantinedSafe);
            _logger.Write("original-permission", "expired", _lastResult?.MatchedRules, _lastResult?.MatchedLabels);
            ClearQuarantineState();
            UpdateStatus("監視中", $"{DateTime.Now:HH:mm:ss} 原文の一時許可を終了", true);
            _trayIcon.ShowBalloonTip(2000, "原文の許可を終了しました", "原文を利用できない状態へ戻しました。", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            FailClose("permission-expiry", ex.Message);
        }
    }

    private void ShowDetection(ProtectionResult result)
    {
        if (_detectionForm is { IsDisposed: false })
            _detectionForm.Close();

        var form = new DetectionForm(result, _config.OriginalAccessSeconds);
        form.KeepSafeRequested += (_, _) => form.Close();
        form.PermitOriginalRequested += (_, _) => PermitOriginal();
        form.DiscardRequested += (_, _) =>
        {
            DiscardQuarantine(clearClipboard: true);
            form.Close();
        };
        form.HelpOpenRequested += (_, _) => ShowHelp();
        form.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(_detectionForm, form))
                _detectionForm = null;
        };
        _detectionForm = form;
        form.Show();
        form.Activate();
    }

    private void ShowLastDetection()
    {
        if (_lastResult is null || _quarantinedOriginal is null)
        {
            MessageBox.Show("現在、隔離中の原文はありません。", "PasteFence", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        ShowDetection(_lastResult);
    }

    private void PermitOriginal()
    {
        if (_quarantinedOriginal is null)
        {
            MessageBox.Show("一時許可できる原文はありません。", "PasteFence", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            WriteProtectedClipboard(_quarantinedOriginal);
            _permitUntil = DateTimeOffset.Now.AddSeconds(_config.OriginalAccessSeconds);
            _logger.Write("original-permission", "started", _lastResult?.MatchedRules, _lastResult?.MatchedLabels);
            _detectionForm?.Close();
            UpdateStatus("原文を一時許可中", $"{DateTime.Now:HH:mm:ss} {_config.OriginalAccessSeconds}秒の許可を開始", true);
            _trayIcon.ShowBalloonTip(3000, "原文を一時許可しました",
                $"{_config.OriginalAccessSeconds}秒後に安全版へ戻します。", ToolTipIcon.Warning);
        }
        catch (Exception ex)
        {
            ReportFailure("original-permission", ex.Message);
        }
    }

    private void DiscardQuarantine(bool clearClipboard)
    {
        try
        {
            if (clearClipboard)
            {
                var current = ProtectedClipboard.TryGetText();
                if (current == _quarantinedOriginal || current == _quarantinedSafe)
                    ProtectedClipboard.Clear();
            }
            _logger.Write("quarantine", "discarded", _lastResult?.MatchedRules, _lastResult?.MatchedLabels);
            ClearQuarantineState();
            UpdateStatus("監視中", $"{DateTime.Now:HH:mm:ss} 隔離内容を破棄", true);
        }
        catch (Exception ex)
        {
            ReportFailure("quarantine-discard", ex.Message);
        }
    }

    private void ClearQuarantineState()
    {
        _quarantinedOriginal = null;
        _quarantinedSafe = null;
        _lastResult = null;
        _permitUntil = null;
        _trayIcon.Text = "PasteFence - 監視中";
    }

    private void WriteProtectedClipboard(string text)
    {
        _lastWrittenText = text;
        try
        {
            ProtectedClipboard.SetProtectedText(text);
        }
        catch
        {
            _lastWrittenText = null;
            throw;
        }
    }

    private void ShowSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _helpForm?.SelectTopic("rule-concepts");
            ArrangeHelpAndSettings();
            _settingsForm.Activate();
            return;
        }

        var form = new SettingsForm(_config);
        form.HelpOpenRequested += (_, _) => ShowHelp("rule-concepts");
        form.SettingsSaved += (_, args) =>
        {
            args.Config.Save(_configPath);
            _config = args.Config;
            _logger.SetRetentionDays(_config.LogRetentionDays);
            _logger.Write("settings", "saved");
            UpdateStatus("監視中", $"{DateTime.Now:HH:mm:ss} ルール設定を反映", true);
        };
        form.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(_settingsForm, form))
                _settingsForm = null;
        };
        _settingsForm = form;
        form.Show();
        _helpForm?.SelectTopic("rule-concepts");
        ArrangeHelpAndSettings();
        form.Activate();
    }

    private void ShowHelp(string? topicId = null)
    {
        if (_helpForm is { IsDisposed: false })
        {
            if (topicId is not null)
                _helpForm.SelectTopic(topicId);
            ArrangeHelpAndSettings();
            _helpForm.Activate();
            return;
        }

        var form = new HelpForm();
        form.SettingsOpenRequested += (_, _) => ShowSettings();
        form.LogsOpenRequested += (_, _) => OpenLogDirectory();
        form.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(_helpForm, form))
                _helpForm = null;
        };
        _helpForm = form;
        form.Show();
        if (topicId is not null)
            form.SelectTopic(topicId);
        ArrangeHelpAndSettings();
        form.Activate();
    }

    private void ArrangeHelpAndSettings()
    {
        if (_helpForm is not { IsDisposed: false, Visible: true } ||
            _settingsForm is not { IsDisposed: false, Visible: true })
            return;

        var area = Screen.FromControl(_settingsForm).WorkingArea;
        const int gap = 8;
        const int minimumHelpWidth = 720;
        const int minimumSettingsWidth = 850;
        if (area.Width < minimumHelpWidth + minimumSettingsWidth + gap)
            return;

        var settingsWidth = Math.Min(1040, Math.Max(minimumSettingsWidth, area.Width / 2));
        var helpWidth = Math.Min(1000, area.Width - settingsWidth - gap);
        if (helpWidth < minimumHelpWidth)
        {
            helpWidth = minimumHelpWidth;
            settingsWidth = area.Width - helpWidth - gap;
        }

        var totalWidth = helpWidth + gap + settingsWidth;
        var left = area.Left + Math.Max(0, (area.Width - totalWidth) / 2);
        var height = Math.Min(720, area.Height - 40);
        var top = area.Top + Math.Max(0, (area.Height - height) / 2);
        _helpForm.Bounds = new Rectangle(left, top, helpWidth, height);
        _settingsForm.Bounds = new Rectangle(left + helpWidth + gap, top, settingsWidth, height);
    }

    private void OpenLogDirectory()
    {
        Directory.CreateDirectory(_logDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", _logDirectory) { UseShellExecute = true });
    }

    private void ShowControlCenter()
    {
        if (!_controlCenter.Visible)
            _controlCenter.Show();
        if (_controlCenter.WindowState == FormWindowState.Minimized)
            _controlCenter.WindowState = FormWindowState.Normal;
        _controlCenter.Activate();
    }

    // 判定・書き戻し・復帰のいずれかに失敗したときは、原文をクリップボードに残さない。
    // 残すと利用者は「保護された」と思ったまま原文を貼れてしまい、このツールの目的が崩れる
    // （フェイルクローズ。失敗を知らせるだけの ReportFailure とは別にしてある）。
    private void FailClose(string eventType, string message)
    {
        var rules = _lastResult?.MatchedRules;
        var labels = _lastResult?.MatchedLabels;
        var cleared = true;

        try
        {
            ProtectedClipboard.Clear();
        }
        catch (Exception ex)
        {
            cleared = false;
            message = $"{message} / クリップボードの消去にも失敗: {ex.Message}";
        }

        _logger.Write(eventType, cleared ? "fail-close" : "fail-close-incomplete", rules, labels);
        ClearQuarantineState();
        UpdateStatus("要確認", $"{DateTime.Now:HH:mm:ss} {message}", false);
        _trayIcon.ShowBalloonTip(5000, "PasteFenceで保護エラー",
            cleared
                ? "判定できなかったため、クリップボードを空にしました。コピーし直してください。"
                : "判定に失敗し、クリップボードも空にできませんでした。貼り付けないでください。",
            ToolTipIcon.Error);
    }

    private void ReportFailure(string eventType, string message)
    {
        _logger.Write(eventType, "failed", _lastResult?.MatchedRules, _lastResult?.MatchedLabels);
        UpdateStatus("要確認", $"{DateTime.Now:HH:mm:ss} {message}", false);
        _trayIcon.ShowBalloonTip(5000, "PasteFenceで保護エラー", "貼り付けを中止し、コントロールセンターを確認してください。", ToolTipIcon.Error);
    }

    private void UpdateStatus(string status, string lastEvent, bool healthy)
    {
        _controlCenter.UpdateState(status, lastEvent, healthy);
        if (!_permitUntil.HasValue)
            _trayIcon.Text = healthy ? $"PasteFence - {status}" : "PasteFence - 要確認";
    }

    private void ExitApplication()
    {
        _exiting = true;
        _logger.Write("monitor", "stopped");
        _timer.Stop();
        _monitor.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _detectionForm?.Close();
        _helpForm?.Close();
        _settingsForm?.Close();
        _controlCenter.Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_exiting)
            ExitApplication();
        base.Dispose(disposing);
    }
}
