namespace PasteFence;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var mutex = new Mutex(true, @"Local\PasteFenceCSharpPrototype", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "PasteFence C# prototype はすでに起動しています。タスクトレイを確認してください。",
                "PasteFence",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            Application.Run(new PasteFenceApplicationContext());
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"PasteFenceを開始できませんでした。\n\n{ex.Message}",
                "PasteFence",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
