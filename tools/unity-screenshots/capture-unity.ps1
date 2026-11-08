param(
  [string]$Project = 'E:\Projects\fara\Temp\shots-unity\vrc-demo',
  [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.22f1\Editor\Unity.exe',
  [int]$W = 2200, [int]$H = 1300,
  [string[]]$Tags = @('overview', 'aao-trace', 'aao-merge', 'apl', 'apl-open')   # clothing: model-rig, merge-armature, blendshape-sync
)
$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System; using System.Drawing; using System.Drawing.Imaging; using System.Runtime.InteropServices;
public static class Win {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  public static string ShotScreen(IntPtr h, string path) {
    RECT r; GetWindowRect(h, out r); int w = r.R - r.L, hh = r.B - r.T;
    using (var bmp = new Bitmap(w, hh, PixelFormat.Format32bppArgb)) {
      using (var g = Graphics.FromImage(bmp)) { g.CopyFromScreen(r.L, r.T, 0, 0, new Size(w, hh)); }
      Color c = bmp.GetPixel(w / 2, hh / 2); Color d = bmp.GetPixel(w * 3 / 4, hh / 2);
      bmp.Save(path, ImageFormat.Png);
      if (c.R > 240 && c.G < 20 && c.B > 240 && d.R > 240 && d.G < 20 && d.B > 240) return "MAGENTA " + w + "x" + hh;
      return w + "x" + hh + " (screen)";
    }
  }
  public static string Shot(IntPtr h, string path) {
    RECT r; GetWindowRect(h, out r); int w = r.R - r.L, hh = r.B - r.T;
    using (var bmp = new Bitmap(w, hh, PixelFormat.Format32bppArgb)) {
      using (var g = Graphics.FromImage(bmp)) { IntPtr hdc = g.GetHdc(); bool ok = PrintWindow(h, hdc, 2); g.ReleaseHdc(hdc); if (!ok) return "PrintWindow failed"; }
      Color c = bmp.GetPixel(w / 2, hh / 2); Color d = bmp.GetPixel(w * 3 / 4, hh / 2);
      bmp.Save(path, ImageFormat.Png);
      if (c.R > 240 && c.G < 20 && c.B > 240 && d.R > 240 && d.G < 20 && d.B > 240) return "MAGENTA " + w + "x" + hh;
      return w + "x" + hh;
    }
  }
}
'@
[void][Win]::SetProcessDPIAware()
$out = Join-Path $Project 'ShotsOut'
Remove-Item -Force -ErrorAction SilentlyContinue "$Project\done.flag", "$Project\ready-*.flag", "$Project\ack-*.flag"
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $out
New-Item -ItemType Directory -Force -Path $out | Out-Null
New-Item -ItemType File -Force -Path "$Project\go.flag" | Out-Null

$proc = Start-Process -FilePath $Unity -ArgumentList @('-projectPath', "`"$Project`"", '-logFile', "`"$Project\unity.log`"") -PassThru
$proc.Id | Out-File "$Project\unity.pid" -Encoding ascii
"Unity PID $($proc.Id)"

$hwnd = [IntPtr]::Zero
$deadline = (Get-Date).AddMinutes(12)
$done = @{}
while ((Get-Date) -lt $deadline -and -not $proc.HasExited) {
  $proc.Refresh()
  if ($proc.MainWindowHandle -ne [IntPtr]::Zero) {
    $hwnd = $proc.MainWindowHandle
    # Before capturing, keep only a 12x12 px corner on screen (no monitor exists to the left or above).
    # HWND_TOPMOST (-1), SWP_NOSIZE | SWP_NOACTIVATE: size and focus unchanged.
    if (-not $shown) {
      $r = New-Object Win+RECT; [void][Win]::GetWindowRect($hwnd, [ref]$r)
      $ww = $r.R - $r.L; $hh2 = $r.B - $r.T
      [void][Win]::SetWindowPos($hwnd, [IntPtr](-1), (12 - $ww), (12 - $hh2), 0, 0, 0x0011)
    }
  }
  foreach ($t in $tags) {
    if (-not $done[$t] -and (Test-Path "$Project\ready-$t.flag") -and $hwnd -ne [IntPtr]::Zero) {
      # Show the whole window only while capturing (topmost, no activation). Unity does not paint when it is not visible.
      $shown = $true
      [void][Win]::SetWindowPos($hwnd, [IntPtr](-1), 0, 0, 0, 0, 0x0011)
      Start-Sleep -Milliseconds 3000
      $res = ''
      for ($try = 0; $try -lt 6; $try++) {
        $res = [Win]::Shot($hwnd, "$out\$t-window.png")
        if ($res -notmatch 'MAGENTA') { break }
        # PrintWindow gave an unpainted frame: read the window's own screen rectangle instead (window is topmost at 0,0).
        [void][Win]::SetWindowPos($hwnd, [IntPtr](-1), 0, 0, 0, 0, 0x0011)
        Start-Sleep -Milliseconds 1500
        $res = [Win]::ShotScreen($hwnd, "$out\$t-window.png")
        if ($res -notmatch 'MAGENTA') { break }
        Start-Sleep -Seconds 1
      }
      "$t -> $res"
      New-Item -ItemType File -Force -Path "$Project\ack-$t.flag" | Out-Null
      $done[$t] = $true
    }
  }
  if (Test-Path "$Project\done.flag") { break }
  Start-Sleep -Milliseconds 700
}
# If Unity is still running after the capture, stop only the PID we started.
Start-Sleep -Seconds 5
if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force; "stopped our Unity PID $($proc.Id)" } else { "Unity exited (code $($proc.ExitCode))" }
Get-ChildItem $out | Select-Object Name, Length | Format-Table -AutoSize | Out-String
