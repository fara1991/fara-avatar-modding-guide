param(
  [string]$In = 'E:\Projects\fara\Temp\shots-unity\vrc-demo\ShotsOut',
  [string]$Repo = 'E:\Projects\fara\fara-avatar-modding-guide'
)
# ASCII only (Windows PowerShell 5.1 reads this file in the ANSI code page).
Add-Type -AssemblyName System.Drawing

function Open-Bmp([string]$name) { New-Object System.Drawing.Bitmap (Join-Path $In $name) }

function Save-Crop([string]$srcName, [int]$x, [int]$y, [int]$w, [int]$h, [string]$dest) {
  $src = Open-Bmp $srcName
  $dst = New-Object System.Drawing.Bitmap $w, $h
  $g = [System.Drawing.Graphics]::FromImage($dst)
  $g.DrawImage($src, (New-Object System.Drawing.Rectangle 0, 0, $w, $h), (New-Object System.Drawing.Rectangle $x, $y, $w, $h), [System.Drawing.GraphicsUnit]::Pixel)
  $g.Dispose()
  New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null
  $dst.Save($dest, [System.Drawing.Imaging.ImageFormat]::Png)
  $dst.Dispose(); $src.Dispose()
  "saved $dest ($w x $h)"
}

# Inspector panes (x 1431..1912). Heights chosen to show the whole component.
Save-Crop 'apl-open-window.png'  1431 81 481 684 (Join-Path $Repo 'Content\learn\vrchat-features\images\avatar-pose-library-inspector.png')
Save-Crop 'aao-trace-window.png' 1431 81 481 684 (Join-Path $Repo 'Content\learn\optimize-aao\images\avatar-optimizer-trace-and-optimize.png')
Save-Crop 'aao-merge-window.png' 1431 81 481 380 (Join-Path $Repo 'Content\learn\optimize-aao\images\avatar-optimizer-merge-skinned-mesh.png')

# Unity editor overview: mask the account button, drop the status bar, draw numbered frames.
$bmp = Open-Bmp 'overview-window.png'
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'
# toolbar color sampled next to the account button
$c = $bmp.GetPixel(300, 66)
$g.FillRectangle((New-Object System.Drawing.SolidBrush $c), 12, 54, 64, 26)
$panes = @(
  @{ n = '1'; name = 'Scene';     r = @(8, 81, 686, 733);    col = '#2563EB'; chip = @(26, 700) },
  @{ n = '2'; name = 'Hierarchy'; r = @(694, 81, 406, 923);  col = '#16A34A'; chip = @(712, 860) },
  @{ n = '3'; name = 'Project';   r = @(1100, 81, 331, 923); col = '#EA580C'; chip = @(1118, 860) },
  @{ n = '4'; name = 'Inspector'; r = @(1431, 81, 481, 923); col = '#7C3AED'; chip = @(1449, 860) },
  @{ n = '5'; name = 'Console';   r = @(8, 814, 686, 190);   col = '#DC2626'; chip = @(26, 905) }
)
$font = New-Object System.Drawing.Font 'Segoe UI', 46, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
foreach ($p in $panes) {
  $col = [System.Drawing.ColorTranslator]::FromHtml($p.col)
  $pen = New-Object System.Drawing.Pen $col, 6
  $r = $p.r
  $g.DrawRectangle($pen, ($r[0] + 3), ($r[1] - 4), ($r[2] - 6), ($r[3] + 1))
  $label = $p.n + '  ' + $p.name
  $size = $g.MeasureString($label, $font)
  $w = [int]$size.Width + 30; $h = 76
  $brush = New-Object System.Drawing.SolidBrush $col
  $g.FillRectangle($brush, $p.chip[0], $p.chip[1], $w, $h)
  $g.DrawString($label, $font, [System.Drawing.Brushes]::White, ($p.chip[0] + 15), ($p.chip[1] + 9))
}
$g.Dispose()
$dest = Join-Path $Repo 'Content\learn\unity\images\editor-overview-window.png'
New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null
$crop = New-Object System.Drawing.Bitmap 1920, 1004
$g2 = [System.Drawing.Graphics]::FromImage($crop)
$g2.DrawImage($bmp, (New-Object System.Drawing.Rectangle 0, 0, 1920, 1004), (New-Object System.Drawing.Rectangle 0, 0, 1920, 1004), [System.Drawing.GraphicsUnit]::Pixel)
$g2.Dispose()
$crop.Save($dest, [System.Drawing.Imaging.ImageFormat]::Png)
$crop.Dispose(); $bmp.Dispose()
"saved $dest (1920 x 1004)"
