namespace PasteFence;

// 実行ファイルに埋め込んだアイコンを、タスクトレイと画面でも使うための入口。
//
// 実行ファイル自体のアイコンは .csproj の ApplicationIcon で付くが、それだけでは
// 常駐中に見えるトレイや、開いた画面の左上は Windows 標準の絵のままになる。
// 利用者がいちばん長く見るのはそちらなので、同じ絵を使う。
//
// 読み込みに失敗しても止めない。アイコンが出ないことと、保護が働かないことは別の話で、
// 絵のために常駐そのものを落とすのは割に合わない。
internal static class AppIcons
{
    private static readonly Icon? Embedded = Load();

    // 画面の左上とタスクバーに出る大きめの絵
    public static Icon Window => Embedded ?? SystemIcons.Shield;

    // タスクトレイに出る小さい絵。環境ごとの小アイコンの大きさに合わせる
    public static Icon Tray
    {
        get
        {
            if (Embedded is null)
                return SystemIcons.Shield;
            try
            {
                return new Icon(Embedded, SystemInformation.SmallIconSize);
            }
            catch (Exception)
            {
                return Embedded;
            }
        }
    }

    private static Icon? Load()
    {
        try
        {
            using var stream = typeof(AppIcons).Assembly
                .GetManifestResourceStream("PasteFence.pastefence.ico");
            return stream is null ? null : new Icon(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
