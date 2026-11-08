# 記事用のスクリーンショットを Blender で自動撮影する（Windows 用）。
#
#   .\tools\screenshots\capture.ps1                 # 全部撮り直す
#   .\tools\screenshots\capture.ps1 -Only mirror-half,loopcut   # 名前を指定して撮り直す
#   .\tools\screenshots\capture.ps1 -Blender "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe"
#
# 撮影中は Blender のウィンドウが開いて自動で操作されます（1 〜 2 分）。触らずに待ってください。
# 設定は一時フォルダ（%TEMP%\fara-avatar-modding-guide-shots）に作るので、普段の Blender の設定は変わりません。
# 撮影後は Python（Pillow が必要: py -3 -m pip install pillow）で切り抜きと番号付けを行います。
param(
    [string[]]$Only = @(),
    [string]$Blender = "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe"
)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Resolve-Path (Join-Path $here "..\..")
$out = Join-Path $repo "Content\learn"
$work = Join-Path $env:TEMP "fara-avatar-modding-guide-shots"
$config = Join-Path $work "config"
New-Item -ItemType Directory -Force -Path $config | Out-Null

$env:BLENDER_USER_CONFIG = $config
if (-not (Test-Path (Join-Path $config "userpref.blend"))) {
    & $Blender -b --factory-startup --python (Join-Path $here "mkprefs.py") | Out-Null
}

$args = @("--window-geometry", "0", "0", "1600", "900", "--python", (Join-Path $here "capture.py"), "--", "--out", $out, "--work", $work)
if ($Only.Count -gt 0) { $args += @("--only") + $Only }
& $Blender @args | Out-Null

if (Test-Path (Join-Path $work "capture.log")) { Get-Content (Join-Path $work "capture.log") }
py -3 (Join-Path $here "postprocess.py") (Join-Path $work "post.json")
