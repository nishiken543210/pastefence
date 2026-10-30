namespace PasteFence.Tests;

// クリップボードの読み書き（内部で OLE を使う System.Windows.Forms.Clipboard）と
// WinForms のオブジェクト生成は、STA のスレッドでなければ例外になる。
// トップレベルステートメントが生成する Main には [STAThread] を付けられないため、
// STA のスレッドを起こしてその中で試験を動かす。
internal static class StaRunner
{
    public static void Run(Action action)
    {
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
            throw new Exception("STAスレッドの試験群が異常終了しました: " + error.Message, error);
    }
}
