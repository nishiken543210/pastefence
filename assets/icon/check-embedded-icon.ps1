# 実行ファイル（.exe）に PasteFence のアイコンが埋まっているかを調べる。
#
#   powershell -ExecutionPolicy Bypass -File check-embedded-icon.ps1 <exe> [<exe> ...]
#
# 全て通れば終了コード 0、1 つでも駄目なら 1。
#
# このファイルは必ず **UTF-8（BOM 付き）** で保存すること。
# Windows PowerShell 5.1 は BOM の無い .ps1 を ANSI として読むため、
# BOM が無いと日本語が化けて構文エラーになる。
param(
    [Parameter(Mandatory = $true, ValueFromRemainingArguments = $true)]
    [string[]]$Paths
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# pastefence.ico で使っている色。この 3 色が揃っていれば、
# 既定のアイコンではなく PasteFence のアイコンが入っていると言える。
#
# 色は厳密一致では見ない。Icon.ToBitmap() は GDI+ を通るため、
# 元の値から数階調ずれることがある（実際ずれていた）。
# 代わりに ±16 の幅を持たせ、さらに「全体の何 % を占めるか」で判定する。
$targets = @(
    @{ Name = '紺';   R = 0x1B; G = 0x3A; B = 0x5C; MinPercent = 10.0 },
    @{ Name = '白';   R = 0xFF; G = 0xFF; B = 0xFF; MinPercent = 5.0 },
    @{ Name = '琥珀'; R = 0xE9; G = 0xA1; B = 0x3B; MinPercent = 0.5 }
)
$tolerance = 16

function Test-Exe {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        Write-Host "見つかりません: $Path"
        return $false
    }
    $full = (Resolve-Path -LiteralPath $Path).Path
    Write-Host "=== $full ==="

    $icon = [System.Drawing.Icon]::ExtractAssociatedIcon($full)
    if ($null -eq $icon) {
        Write-Host "  アイコンを取り出せませんでした。"
        return $false
    }

    $bmp = $icon.ToBitmap()
    $total = $bmp.Width * $bmp.Height
    Write-Host ("  取り出した寸法: {0}x{1} ({2}px)" -f $bmp.Width, $bmp.Height, $total)

    $counts = @(0) * $targets.Count
    $hist = @{}
    foreach ($y in 0..($bmp.Height - 1)) {
        foreach ($x in 0..($bmp.Width - 1)) {
            $p = $bmp.GetPixel($x, $y)

            $key = ($p.R -shl 16) -bor ($p.G -shl 8) -bor $p.B
            if ($hist.ContainsKey($key)) { $hist[$key] = $hist[$key] + 1 } else { $hist[$key] = 1 }

            for ($i = 0; $i -lt $targets.Count; $i++) {
                $t = $targets[$i]
                if ([Math]::Abs([int]$p.R - [int]$t.R) -le $tolerance -and
                    [Math]::Abs([int]$p.G - [int]$t.G) -le $tolerance -and
                    [Math]::Abs([int]$p.B - [int]$t.B) -le $tolerance) {
                    $counts[$i] = $counts[$i] + 1
                    break
                }
            }
        }
    }

    $bmp.Dispose()
    $icon.Dispose()

    # 落ちたときに原因が分かるよう、多い色から並べて出しておく
    Write-Host "  多い色:"
    $hist.GetEnumerator() | Sort-Object -Property Value -Descending | Select-Object -First 8 | ForEach-Object {
        Write-Host ("    #{0:X6}  {1}px" -f $_.Key, $_.Value)
    }

    $ok = $true
    for ($i = 0; $i -lt $targets.Count; $i++) {
        $t = $targets[$i]
        $percent = 100.0 * $counts[$i] / $total
        $verdict = if ($percent -ge $t.MinPercent) { 'OK' } else { '不足' }
        $line = "  {0}(#{1:X2}{2:X2}{3:X2} ±{4}): {5}px = {6:N1}% / 必要 {7:N1}%  {8}"
        Write-Host ($line -f $t.Name, $t.R, $t.G, $t.B, $tolerance, $counts[$i], $percent, $t.MinPercent, $verdict)
        if ($percent -lt $t.MinPercent) { $ok = $false }
    }
    if (-not $ok) { Write-Host "  → PasteFence のアイコンが入っていない。" }
    return $ok
}

$failed = $false
foreach ($path in $Paths) {
    if (-not (Test-Exe -Path $path)) { $failed = $true }
}

if ($failed) {
    Write-Host "アイコンが埋まっていない実行ファイルがある。"
    exit 1
}

Write-Host "全ての実行ファイルに PasteFence のアイコンが埋まっている。"
exit 0
