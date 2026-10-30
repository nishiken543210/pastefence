# 高 DPI（拡大表示）への対応について

結論から書くと、**今回は DPI まわりを変えていない**。
`.csproj` の `ApplicationHighDpiMode` に、今と同じ値（`SystemAware`）を
明示的に書いただけで、動きは 1 つも変わらない。

以下はその理由。

## 今どうなっているか

`.csproj` に `ApplicationHighDpiMode` の指定は無く、`app.manifest` も無い。
この状態で `Program.cs` の `ApplicationConfiguration.Initialize()` が
何を呼ぶかは、.NET 8 の WinForms が持っている既定値で決まる。

その既定値は `SystemAware` である。
（.NET 8 の WinForms アナライザーの `ApplicationConfig.PropertyDefaultValue` に
`public const HighDpiMode DpiMode = HighDpiMode.SystemAware;` と書かれている。
`Microsoft.WindowsDesktop.App.Ref` 8.0.33 の元になったコミットで確認した。）

つまり **PasteFence は既に DPI 対応済みのアプリとして起動している**。
「WinForms の既定は DPI 非対応なので 150% の画面でぼやける」というのは、
.NET Framework 時代の話で、今の SDK には当てはまらない。

`SystemAware` で実際にぼやけるのは、次の場合だけ:

- 拡大率の違うディスプレイが 2 枚あり、ウィンドウを片方からもう片方へ移したとき
- アプリを起動したまま Windows の拡大率を変えたとき

1 枚のディスプレイを 150% で使っている限り、文字も枠もぼやけない。

## では `PerMonitorV2` に上げればよいのでは

上の 2 つを直せるのは `PerMonitorV2` だけだが、このコードでは危ないと判断した。

画面を作っている 4 つのフォームを読むと、部品の位置と大きさが
**ほぼ全てピクセル直書き**になっている。

| ファイル | 直書きの様子 |
| --- | --- |
| `ControlCenterForm.cs` | `Anchor` も `Dock` も 1 つも無い。12 個の部品が全て `Location = new Point(x, y)` の絶対座標。ボタンは `Size(110, 38)` などの固定寸法 |
| `DetectionForm.cs` | `Anchor` はあるが、`SplitContainer` の `SplitterDistance = 130` とボタンの `Location` / `Size` は直書き。`ListView` の列幅も `60 / 160 / 180 / 180` の固定値 |
| `SettingsForm.cs` | `DataGridView` を `Size(980, 175)` などで置き、列幅も `60 / 180 / 90 / 160 / 130 / 220` の固定値。`NumericUpDown` は `Size(70, 28)` |
| `HelpForm.cs` | `SplitContainer` の `SplitterDistance = 260`、`TreeView` が `Size(246, 496)`、見出しラベルが `AutoSize = false` ＋ `Size(675, 38)` |

さらに 4 つとも `AutoScaleMode` を設定していない。
フォントだけはポイント指定（`new Font("Yu Gothic UI", 9F)`）なので
DPI に応じて実寸が伸びるのに対し、入れ物の方はピクセル固定のまま、という形になっている。

`PerMonitorV2` にすると、ディスプレイ間の移動のたびに WinForms が
フォームと子部品を拡大率の比で測り直す。このとき
`SplitterDistance`・`DataGridView` の列幅・`AutoSize = false` のラベル・
`MinimumSize` は素直に付いてこないことが多く、
**文字が切れる / 枠からはみ出す**という形で壊れるのが典型的な失敗。

そして今回の作業環境は Linux で、`net8.0-windows` の WinForms は
ビルドも実行もできない。**見た目を 1 度も確認できないまま**
この変更を入れることになる。

直してよくなる場面（ディスプレイ 2 枚で拡大率が違う人）より、
壊れる場面（全員）の方が広い。だから今回は触らない。

## やるとしたら何を直す必要があるか

`PerMonitorV2` へ上げるときは、この順で進めるのがよい。

1. **先に確認する手段を用意する。** Windows の実機か、Actions で
   フォームを起動してスクリーンショットを撮る仕組み。
   100% / 150% / 200% の 3 通りで 4 フォームを撮れること。
   これが無いまま 2. 以降をやるべきではない。
2. **絶対座標をやめる。** `ControlCenterForm` は全面的に、
   他の 3 つも部分的に、`TableLayoutPanel` / `FlowLayoutPanel` と
   `Dock` / `Padding` による組み方へ書き換える。
   ボタンは `AutoSize = true` ＋ `AutoSizeMode.GrowAndShrink` にして
   `Size(110, 38)` のような固定寸法を消す。
3. **各フォームに `AutoScaleMode = AutoScaleMode.Font` と
   `AutoScaleDimensions` を設定する。** 今はどちらも未設定で、
   フォームが自分の基準 DPI を知らない状態になっている。
4. **直書きの寸法を DPI 比で計算し直す。**
   `SplitContainer.SplitterDistance`、`DataGridView` の列幅、
   `ListView` の列幅、`MinimumSize`。
   `DataGridView` の列は `AutoSizeColumnMode.Fill` / `DisplayedCells` へ寄せると
   手計算が減る。
5. **`Font` の指定を見直す。** 4 フォームが個別に `"Yu Gothic UI"` を
   名指ししている。`ApplicationDefaultFont` に一本化できる。
6. そのうえで `.csproj` の `ApplicationHighDpiMode` を `PerMonitorV2` にし、
   1. で用意した方法で 3 つの拡大率を目で確認する。

## `app.manifest` を足さなかった理由

マニフェストに `dpiAware` / `dpiAwareness` を書く方法もあるが、採らなかった。

- マニフェストで DPI 対応を宣言すると、プロセス起動時点で確定してしまい、
  `ApplicationConfiguration.Initialize()` の設定が後から効かなくなる。
  `.csproj` 側で調整する余地を残しておきたい。
- DPI 以外にマニフェストで足したい項目（`supportedOS`、`asInvoker` など）は、
  .NET SDK が `WinExe` に対して既定のマニフェストを埋め込んでおり、
  今のところ困っていない。

つまり、上の 1〜6 をやる段になっても、まず `.csproj` の
`ApplicationHighDpiMode` を書き換えれば足りる。
