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
$navyRgb = 0x1B3A5C
$whiteRgb = 0xFFFFFF

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
    Write-Host ("  取り出した寸法: {0}x{1}" -f $bmp.Width, $bmp.Height)

    $navy = 0
    $white = 0
    $amber = 0
    for ($y = 0; $y -lt $bmp.Height; $y++) {
        for ($x = 0; $x -lt $bmp.Width; $x++) {
            $p = $bmp.GetPixel($x, $y)
            if ($p.A -lt 128) { continue }
            $rgb = ($p.R -shl 16) -bor ($p.G -shl 8) -bor $p.B
            if ($rgb -eq $navyRgb) {
                $navy++
            }
            elseif ($rgb -eq $whiteRgb) {
                $white++
            }
            # 琥珀色のクリップは面積が小さく縁がぼけるので、幅を持たせて数える
            elseif ($p.R -gt 200 -and $p.G -gt 120 -and $p.G -lt 190 -and $p.B -lt 110) {
                $amber++
            }
        }
    }

    $bmp.Dispose()
    $icon.Dispose()
    Write-Host ("  紺 {0}px / 白 {1}px / 琥珀 {2}px" -f $navy, $white, $amber)

    $ok = $true
    if ($navy -lt 1) { Write-Host "  紺 #1B3A5C が無い。"; $ok = $false }
    if ($white -lt 1) { Write-Host "  白 #FFFFFF が無い。"; $ok = $false }
    if ($amber -lt 1) { Write-Host "  琥珀色のクリップが無い。"; $ok = $false }
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
