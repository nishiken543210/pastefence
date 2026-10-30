using System.Reflection;
using System.Runtime.CompilerServices;

namespace PasteFence.Tests;

// PasteFenceApplicationContext の状態の移り変わり
// （隔離 → 原文をN秒だけ許可 → 期限切れで安全版へ復帰 → 破棄）の試験。
//
// このクラスは private が多く、コンストラクターがトレイアイコン・クリップボード監視ウィンドウ・
// 使い方ウィンドウまで作ってしまうため、そのままでは試験から呼べない。
// src/ を変更しない前提なので、コンストラクターを通さずに実体だけを作り、
// 状態の確認に必要な依存（設定・監査ログ・トレイアイコン・コントロールセンター）を入れて呼ぶ。
// 試験できなかった部分は PR 本文に「どこを internal にすれば試験できるか」として挙げている。
internal static class ApplicationContextTests
{
    private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private const string SkipReason =
        "この環境ではクリップボードを開けない（OpenClipboard が失敗する）ため確かめられない";

    // 既定ルールに一致する架空のコピー内容
    private const string Original = "顧客名A\r\n電話番号：080-1234-5678\r\n明日の打ち合わせは10時です。";

    // 既定ルールに一致しないコピー内容
    private const string Harmless = "来週の会議の議題をまとめます。";

    public static void Register(TestHarness harness, PasteFenceConfig config, bool clipboardUsable)
    {
        // クリップボードに触らずに確かめられるもの
        harness.Run("処理中は二重にクリップボードを見ない", () =>
        {
            using var probe = new ContextProbe(config);
            probe.SetField("_handlingClipboard", true);

            probe.InvokeOnClipboardChanged();

            // 本体に入っていれば finally で false に戻るので、true のままなら入っていない
            TestHarness.Assert(
                probe.GetField("_handlingClipboard") is true,
                "処理中フラグが立っているのにクリップボード処理へ入りました。");
        });

        harness.Run("許可中はトレイに残り秒数を出す", () =>
        {
            using var probe = new ContextProbe(config);
            probe.SetField("_permitUntil", DateTimeOffset.Now.AddSeconds(3));

            probe.Invoke("OnTimerTick");

            var trayText = probe.TrayText();
            TestHarness.Assert(trayText is not null && trayText.Contains("残り"), $"トレイに残り秒数が出ていません（実際: {trayText}）。");
            TestHarness.Assert(probe.GetField("_permitUntil") is not null, "期限内なのに許可が終了しました。");
        });

        harness.Run("許可期限が無いときのタイマーは何もしない", () =>
        {
            using var probe = new ContextProbe(config);
            probe.SetTrayText("変化していないことの目印");

            probe.Invoke("OnTimerTick");

            TestHarness.Assert(probe.TrayText() == "変化していないことの目印", "許可していないのにトレイ表示が書き換わりました。");
        });

        harness.Run("隔離を解くとトレイ表示が監視中へ戻る", () =>
        {
            using var probe = new ContextProbe(config);
            Quarantine(probe, config);
            probe.SetField("_permitUntil", DateTimeOffset.Now.AddSeconds(5));

            probe.Invoke("ClearQuarantineState");

            AssertQuarantineCleared(probe);
            TestHarness.Assert(probe.TrayText() == "PasteFence - 監視中", $"トレイ表示が監視中へ戻っていません（実際: {probe.TrayText()}）。");
        });

        // クリップボードを実際に読み書きして確かめるもの
        var clipboardTests = new (string Name, Action Body)[]
        {
            ("自分が書き戻した内容を利用者のコピーとして扱わない", OwnWriteIsNotTreatedAsUserCopy),
            ("終了処理中はクリップボードを処理しない", ExitingStopsHandling),
            ("原文をN秒だけ許可すると原文が戻り期限が入る", PermitOriginalRestoresOriginal),
            ("期限切れで安全版へ戻し隔離を解く", ExpiryRestoresSafeText),
            ("隔離を破棄するとクリップボードも空にする", DiscardClearsClipboard),
            ("許可中に別の内容をコピーすると隔離が解ける", NextCopyClearsQuarantine)
        };

        foreach (var test in clipboardTests)
        {
            if (clipboardUsable)
                harness.Run(test.Name, () => test.Body());
            else
                harness.Skip(test.Name, SkipReason);
        }

        if (!clipboardUsable)
            return;

        try
        {
            ProtectedClipboard.Clear();
        }
        catch (Exception)
        {
            // 後片付けの失敗は試験の合否に含めない
        }

        void OwnWriteIsNotTreatedAsUserCopy()
        {
            using var probe = new ContextProbe(config);

            // 安全版の書き戻しと同じ経路。_lastWrittenText に覚えさせる
            probe.Invoke("WriteProtectedClipboard", Original);
            TestHarness.Assert(probe.GetField("_lastWrittenText") as string == Original, "書き戻した内容を覚えていません。");
            TestHarness.Assert(ProtectedClipboard.TryGetText() == Original, "書き戻しがクリップボードへ届いていません。");

            probe.InvokeOnClipboardChanged();

            TestHarness.Assert(probe.GetField("_quarantinedOriginal") is null, "自分の書き戻しを利用者のコピーとして隔離しました。");
            TestHarness.Assert(probe.GetField("_lastWrittenText") is null, "一度照合した書き戻しの記録が残っています（次の同じ内容のコピーを見落とします）。");
        }

        void ExitingStopsHandling()
        {
            using var probe = new ContextProbe(config);
            ProtectedClipboard.SetProtectedText(Original);
            probe.SetField("_exiting", true);

            probe.InvokeOnClipboardChanged();

            TestHarness.Assert(probe.GetField("_quarantinedOriginal") is null, "終了処理中なのに隔離を始めました。");
        }

        void PermitOriginalRestoresOriginal()
        {
            using var probe = new ContextProbe(config);
            var result = Quarantine(probe, config);
            ProtectedClipboard.SetProtectedText(result.SafeText);

            probe.Invoke("PermitOriginal");

            TestHarness.Assert(ProtectedClipboard.TryGetText() == Original, "原文がクリップボードへ戻っていません。");
            TestHarness.Assert(probe.GetField("_lastWrittenText") as string == Original, "原文の書き戻しを自分の変更として覚えていません。");

            var permitUntil = (DateTimeOffset?)probe.GetField("_permitUntil");
            TestHarness.Assert(permitUntil.HasValue, "許可期限が入っていません。");
            TestHarness.Assert(permitUntil!.Value > DateTimeOffset.Now, "許可期限が過去になっています。");
            TestHarness.Assert(
                permitUntil.Value <= DateTimeOffset.Now.AddSeconds(config.OriginalAccessSeconds + 1),
                $"許可期限が設定値（{config.OriginalAccessSeconds}秒）より長くなっています。");

            var log = probe.ReadAuditLog();
            TestHarness.Assert(log.Contains("original-permission") && log.Contains("started"), "原文許可の開始が監査ログに残っていません。");
            TestHarness.Assert(!log.Contains("080-1234-5678"), "監査ログに原文が含まれています。");
        }

        void ExpiryRestoresSafeText()
        {
            using var probe = new ContextProbe(config);
            var result = Quarantine(probe, config);

            // 原文を許可した直後の状態（クリップボードに原文、期限が切れている）
            ProtectedClipboard.SetProtectedText(Original);
            probe.SetField("_permitUntil", DateTimeOffset.Now.AddSeconds(-1));

            probe.Invoke("OnTimerTick");

            TestHarness.Assert(ProtectedClipboard.TryGetText() == result.SafeText, "期限切れで安全版へ戻っていません。");
            AssertQuarantineCleared(probe);
            TestHarness.Assert(probe.TrayText() == "PasteFence - 監視中", $"トレイ表示が監視中へ戻っていません（実際: {probe.TrayText()}）。");

            var log = probe.ReadAuditLog();
            TestHarness.Assert(log.Contains("original-permission") && log.Contains("expired"), "原文許可の終了が監査ログに残っていません。");
        }

        void DiscardClearsClipboard()
        {
            using var probe = new ContextProbe(config);
            var result = Quarantine(probe, config);
            ProtectedClipboard.SetProtectedText(result.SafeText);

            probe.Invoke("DiscardQuarantine", true);

            TestHarness.Assert(ProtectedClipboard.TryGetText() is null, "破棄したのにクリップボードに内容が残っています。");
            AssertQuarantineCleared(probe);

            var log = probe.ReadAuditLog();
            TestHarness.Assert(log.Contains("quarantine") && log.Contains("discarded"), "破棄が監査ログに残っていません。");
        }

        void NextCopyClearsQuarantine()
        {
            using var probe = new ContextProbe(config);
            Quarantine(probe, config);
            probe.SetField("_permitUntil", DateTimeOffset.Now.AddSeconds(30));

            // 利用者が別の（保護対象ではない）内容をコピーした状態
            ProtectedClipboard.SetProtectedText(Harmless);

            probe.InvokeOnClipboardChanged();

            AssertQuarantineCleared(probe);
            TestHarness.Assert(ProtectedClipboard.TryGetText() == Harmless, "保護対象ではないコピーが書き換えられました。");
        }
    }

    // 隔離直後と同じ状態を作る。
    // 隔離の入口（OnClipboardChanged の検出側）は DetectionForm を開くため試験から呼べないので、
    // 入口が作る状態だけを再現してから、その後の移り変わりを確かめる。
    private static ProtectionResult Quarantine(ContextProbe probe, PasteFenceConfig config)
    {
        var result = RuleEngine.Evaluate(Original, config);
        TestHarness.Assert(result.Matched, "試験データが既定ルールに一致しません。");

        probe.SetField("_quarantinedOriginal", Original);
        probe.SetField("_quarantinedSafe", result.SafeText);
        probe.SetField("_lastResult", result);
        probe.SetField("_permitUntil", null);
        return result;
    }

    private static void AssertQuarantineCleared(ContextProbe probe)
    {
        TestHarness.Assert(probe.GetField("_quarantinedOriginal") is null, "隔離した原文が残っています。");
        TestHarness.Assert(probe.GetField("_quarantinedSafe") is null, "隔離した安全版が残っています。");
        TestHarness.Assert(probe.GetField("_lastResult") is null, "直前の判定結果が残っています。");
        TestHarness.Assert(probe.GetField("_permitUntil") is null, "原文の許可期限が残っています。");
    }

    private sealed class ContextProbe : IDisposable
    {
        private static readonly Type ContextType = typeof(PasteFenceApplicationContext);
        private readonly List<IDisposable> _injected = [];
        private readonly object _context;

        public string LogDirectory { get; }

        public ContextProbe(PasteFenceConfig config)
        {
            _context = RuntimeHelpers.GetUninitializedObject(ContextType);

            // 初期化していない実体のまま終了処理（ファイナライザー）が走らないようにする
            GC.SuppressFinalize(_context);

            LogDirectory = Path.Combine(Path.GetTempPath(), "PasteFenceTests", Guid.NewGuid().ToString("N"));
            SetField("_config", config);
            SetField("_logger", new AuditLogger(LogDirectory, 30));

            // NotifyIcon や ControlCenterForm を試験側で名指しすると、
            // 試験プロジェクトにも WinForms の参照設定が必要になる。フィールドの型から作って避ける。
            CreateAndSetField("_trayIcon");
            CreateAndSetField("_controlCenter");
        }

        public void SetField(string name, object? value)
        {
            var field = Field(name);
            field.SetValue(_context, value);

            // readonly のフィールドにも入ったことを確かめる。入っていなければ以降の確認が無意味になる
            if (!Equals(field.GetValue(_context), value))
                throw new Exception($"フィールド {name} に値を入れられませんでした。");
        }

        public object? GetField(string name) => Field(name).GetValue(_context);

        public void Invoke(string name, params object?[] arguments)
        {
            var method = ContextType.GetMethod(name, MemberFlags)
                ?? throw new Exception($"メソッド {name} がありません（src/ 側で名前が変わった可能性）。");

            try
            {
                method.Invoke(_context, arguments);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException!;
            }
        }

        public void InvokeOnClipboardChanged() => Invoke("OnClipboardChanged", new object?[] { null, EventArgs.Empty });

        public string? TrayText() => (string?)TrayTextProperty().GetValue(GetField("_trayIcon"));

        public void SetTrayText(string value) => TrayTextProperty().SetValue(GetField("_trayIcon"), value);

        public string ReadAuditLog()
        {
            if (!Directory.Exists(LogDirectory))
                return string.Empty;

            return string.Join("\n", Directory.GetFiles(LogDirectory, "*.csv").Select(File.ReadAllText));
        }

        public void Dispose()
        {
            // 失敗したときに検出ウィンドウが開いたままになることがあるので閉じておく
            if (GetField("_detectionForm") is IDisposable detectionForm)
                detectionForm.Dispose();

            foreach (var disposable in _injected)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception)
                {
                    // 後片付けの失敗は試験の合否に含めない
                }
            }

            try
            {
                if (Directory.Exists(LogDirectory))
                    Directory.Delete(LogDirectory, recursive: true);
            }
            catch (Exception)
            {
                // 同上
            }
        }

        private void CreateAndSetField(string name)
        {
            var instance = Activator.CreateInstance(Field(name).FieldType)
                ?? throw new Exception($"{name} の実体を作れませんでした。");

            if (instance is IDisposable disposable)
                _injected.Add(disposable);

            SetField(name, instance);
        }

        private PropertyInfo TrayTextProperty()
        {
            var trayIcon = GetField("_trayIcon") ?? throw new Exception("_trayIcon が入っていません。");
            return trayIcon.GetType().GetProperty("Text")
                ?? throw new Exception("トレイアイコンに Text がありません。");
        }

        private static FieldInfo Field(string name) =>
            ContextType.GetField(name, MemberFlags)
                ?? throw new Exception($"フィールド {name} がありません（src/ 側で名前が変わった可能性）。");
    }
}
