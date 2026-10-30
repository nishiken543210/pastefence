namespace PasteFence.Tests;

// 自作の試験ハーネス。外部パッケージ（NuGet）を足さない方針なので、
// 合否の集計と出力の体裁だけを受け持つ。
// 全件通ると最後に "PASS: N tests" を出し、終了コード 0 を返す。
internal sealed class TestHarness
{
    private readonly List<string> _failures = [];
    private readonly List<string> _skips = [];
    private int _passed;

    public void Run(string name, Action test)
    {
        try
        {
            test();
            _passed++;
            Console.WriteLine("PASS: " + name);
        }
        catch (Exception ex)
        {
            _failures.Add($"{name}: {ex.Message}");
        }
    }

    // 環境の都合で確かめられない試験は、黙って通さずに理由を出して飛ばす。
    // 「動かせなかった」ことが CI のログに残らないと、緑が嘘になる。
    public void Skip(string name, string reason)
    {
        _skips.Add($"{name}: {reason}");
        Console.WriteLine($"SKIP: {name} （{reason}）");
    }

    // クリップボードは他のアプリ（Windows のクリップボード履歴・クリップボード管理ツール）
    // と取り合いになる。書いた直後に読むと、まだ届いていないことがある。
    // 即座に判定すると試験がたまに赤くなり、赤を無視する癖がつくので、少し待って確かめ直す。
    public static bool ClipboardSettlesTo(string? expected)
    {
        for (var i = 0; i < 20; i++)
        {
            if (ProtectedClipboard.TryGetText() == expected)
                return true;
            Thread.Sleep(25);
        }
        return false;
    }

    public static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    public int Report()
    {
        if (_skips.Count > 0)
        {
            Console.WriteLine($"SKIP: {_skips.Count} tests（この環境では確かめられません）");
            foreach (var skip in _skips)
                Console.WriteLine("- " + skip);
        }

        if (_failures.Count > 0)
        {
            Console.Error.WriteLine($"FAIL: {_failures.Count} test(s)");
            foreach (var failure in _failures)
                Console.Error.WriteLine("- " + failure);
            return 1;
        }

        // 件数は数え間違いを避けるため実際に通った数を出す。
        // 飛ばした試験は上の SKIP 行に出るので、合計が環境で変わることが分かる。
        Console.WriteLine($"PASS: {_passed} tests");
        return 0;
    }
}
