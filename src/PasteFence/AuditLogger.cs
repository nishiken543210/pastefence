using System.Text;

namespace PasteFence;

public sealed class AuditLogger
{
    private readonly string _directory;
    private int _retentionDays;

    public AuditLogger(string directory, int retentionDays)
    {
        _directory = directory;
        _retentionDays = retentionDays;
        Directory.CreateDirectory(_directory);
        DeleteExpiredFiles();
    }

    public void SetRetentionDays(int days)
    {
        _retentionDays = Math.Clamp(days, 1, 365);
        DeleteExpiredFiles();
    }

    public void Write(string eventType, string outcome, IEnumerable<string>? rules = null, IEnumerable<string>? labels = null)
    {
        try
        {
            var path = Path.Combine(_directory, $"pastefence-audit-{DateTime.Now:yyyy-MM-dd}.csv");
            var isNew = !File.Exists(path);
            using var writer = new StreamWriter(path, append: true, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            if (isNew)
                writer.WriteLine("timestamp,event,outcome,rules,categories");

            writer.WriteLine(string.Join(',',
                Csv(DateTimeOffset.Now.ToString("O")),
                Csv(eventType),
                Csv(outcome),
                Csv(string.Join('|', rules ?? [])),
                Csv(string.Join('|', labels ?? []))));
        }
        catch
        {
            // 保護処理をログ書込失敗で止めない。GUI側の状態で失敗を通知する。
        }
    }

    private void DeleteExpiredFiles()
    {
        var threshold = DateTime.Now.Date.AddDays(-_retentionDays);
        foreach (var path in Directory.EnumerateFiles(_directory, "pastefence-audit-*.csv"))
        {
            if (File.GetLastWriteTime(path) < threshold)
                File.Delete(path);
        }
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
