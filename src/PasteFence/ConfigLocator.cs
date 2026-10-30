namespace PasteFence;

// 保護ルールの置き場所を決め、無ければ実行ファイルに同梱した既定値から作る。
//
// なぜ要るか: v0.1.0 では、公開した実行ファイルを単体で落としても動かなかった。
// ルールの設定ファイルが配布物に入っておらず、落とした人の手元には存在しなかったため。
// 実行ファイル 1 つで動くことが、この道具の前提。
internal static class ConfigLocator
{
    public const string FileName = "pastefence-rules.json";

    // 探す順: ①実行ファイルの隣 ②利用者の設定フォルダー ③どちらも無ければ作る
    public static string Ensure()
    {
        var beside = Path.Combine(AppContext.BaseDirectory, FileName);
        if (File.Exists(beside))
            return beside;

        var roaming = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PasteFence",
            FileName);
        if (File.Exists(roaming))
            return roaming;

        var json = ReadEmbeddedDefault();

        // まず実行ファイルの隣へ。ヘルプもそこに在ると説明している
        try
        {
            File.WriteAllText(beside, json);
            return beside;
        }
        catch (Exception)
        {
            // Program Files など書き込めない場所に置かれた場合は下へ
        }

        var folder = Path.GetDirectoryName(roaming);
        if (folder is not null)
            Directory.CreateDirectory(folder);
        File.WriteAllText(roaming, json);
        return roaming;
    }

    // 同梱の既定ルール。これが欠けると実行ファイル単体では動けないので、
    // 読めなかったことを黙って流さず、はっきり止める。
    public static string ReadEmbeddedDefault()
    {
        using var stream = typeof(ConfigLocator).Assembly
            .GetManifestResourceStream("PasteFence." + FileName)
            ?? throw new InvalidDataException(
                "既定の保護ルールが実行ファイルに入っていません。ビルドの設定が壊れています。");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
