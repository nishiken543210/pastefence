# 高 DPI（拡大表示）への対応について

## 今の状態

- **`AutoScaleMode.Dpi` を 4 画面に入れてある。** 拡大表示でも部品の位置と大きさが
  文字と同じ比率で伸びる。
- **`PerMonitorV2` にはしていない。** 既定の `SystemAware` のまま。
  拡大率の違う画面が 2 枚ある環境では、ウィンドウを移したときにぼやける。

## なぜ必要だったか

最初はこの対応を見送っていた。「.NET 8 の WinForms の既定は `SystemAware` で、
1 枚の画面を 150% で使う限りぼやけない」という理由だった。

**ぼやけないのは事実だが、結論が間違っていた。** ぼやけない代わりに、
**レイアウトが崩れていた。** 150% の実機で撮った画面を測ると、こうなっていた。

| 症状 | 原因 |
| --- | --- |
| 「PasteFence」の見出しが下の説明文に重なる | 18pt の文字が 1.5 倍（約 50px）になり、`y=60` に置いた説明文を覆った |
| ログの説明が「履歴・クラウ」で切れる | 枠が `Size(650, 38)` の固定値で、1.5 倍の文字が入らない |
| 下のボタンが画面外に出て押せない | ボタンを `y=240` に直接置いているが、150% では表示領域がそこまで無い |

理屈はこうなる。4 つのフォームは部品の位置と大きさを**ピクセルで直接**書いている。
一方フォントは `new Font("Yu Gothic UI", 9F)` と**ポイント**で指定している。
ポイントは物理的な大きさなので、DPI が上がれば実ピクセルも増える。
つまり **中身だけが伸びて、入れ物は伸びない**。

## 直し方

4 つのフォームの作り始めに、これを足した。

```csharp
SuspendLayout();
AutoScaleMode = AutoScaleMode.Dpi;
AutoScaleDimensions = new SizeF(96F, 96F);
```

そして部品を全部足し終えたところで `ResumeLayout(performLayout: false);` を呼ぶ。

### `Font` ではなく `Dpi` を選んだ理由

`AutoScaleMode.Font` は Microsoft が勧める方式だが、
`AutoScaleDimensions` に「設計時のフォントの平均文字寸法」を数値で書く必要がある
（Segoe UI 9pt なら `7F, 15F` など）。これは実際に Windows 上で測らないと決まらない。
この値を間違えると、**100% の環境まで巻き添えで狂う。**

`Dpi` モードの倍率は `現在のDPI ÷ AutoScaleDimensions` で決まる。
`96, 96` と書けば倍率は `現在のDPI ÷ 96` になり、実測値が 1 つも要らない。
フォントがポイント指定で `DPI ÷ 96` に比例して伸びるのと、**比率がぴったり一致する。**

### `SuspendLayout` で囲む理由

拡大は「レイアウトが走るときに 1 回だけ」実行され、実行後に内部の要求フラグが消える
（`ContainerControl.PerformAutoScale`）。囲まないと、`AutoScaleDimensions` を
設定した時点で即座にレイアウトが走ってしまい、**まだ 1 つも部品が無い状態で**
拡大が終わってしまう。囲めば `ResumeLayout` まで繰り越される。

### 100% の環境への影響

無い。`PerformAutoScale` は倍率が `1.0` ちょうどのとき、拡大処理自体を飛ばす。
`96 ÷ 96 = 1.0` なので、100% の人には 1 行も実行されない。

### 文字が二重に拡大されないか

されない。フォントの拡大は `causedByFontChanged` が真の経路でしか起きず、
この経路は偽で呼ばれる。伸びるのは座標と寸法だけ。

## まだ残っていること

### 1. 拡大率の違う画面を 2 枚使うと、やはりぼやける

`SystemAware` のままなので、起動時の画面の拡大率に合わせて描いたものを、
別の拡大率の画面へ移すと Windows が引き伸ばす。
これを直すには `PerMonitorV2` にする必要があるが、移動のたびに
WinForms がレイアウトを測り直すので、下の 2 を先に片付けないと危ない。

### 2. 絶対座標そのもの

`AutoScaleMode.Dpi` は「ピクセル直書きのレイアウトを比率で引き伸ばす」だけで、
直書きをやめたわけではない。文字幅は厳密には DPI に比例しないので、
拡大率によっては数ピクセル足りない箇所が残りうる。
根本的には `TableLayoutPanel` と `Dock` / `Padding` による組み方へ
書き換えるのがよい。特に `ControlCenterForm` は `Anchor` がボタンにしか無い。

あわせて見直したい直書き:

- `SplitContainer.SplitterDistance`（`DetectionForm` 130 / `HelpForm` 260）
- `DataGridView` の列幅（`SettingsForm`）
- `ListView` の列幅（`DetectionForm`）
- 4 画面が個別に名指ししている `"Yu Gothic UI"` → `ApplicationDefaultFont` に一本化

## `app.manifest` を足さない理由

マニフェストに `dpiAware` / `dpiAwareness` を書くと、プロセス起動時点で確定し、
`ApplicationConfiguration.Initialize()` の設定が後から効かなくなる。
`.csproj` の `ApplicationHighDpiMode` で調整できる余地を残しておきたい。
`supportedOS` などマニフェストで足したい他の項目は、.NET SDK が `WinExe` に
対して既定のマニフェストを埋め込んでおり、今のところ困っていない。
