<#
.SYNOPSIS
  Save a PNG of the Trackmania window, so an agent can see what the game is drawing.

.DESCRIPTION
  Finds the Trackmania process, raises its window, and copies that screen rectangle.
  Written for iterating on HUD layers: change a mode script, reload it, capture, look.

  Limits worth knowing. This copies a region of the screen rather than asking the
  window to redraw, so the game must be visible and unoccluded, and the capture steals
  focus for a moment. Exclusive fullscreen can come back black; borderless windowed
  works. SetProcessDPIAware is required, or the rect arrives in logical pixels and the
  grab is offset by the display scaling factor.

.EXAMPLE
  ./scripts/grab-tm-window.ps1 -Out shot.png
#>
param(
  [string]$Out = "$PSScriptRoot\tm-window.png",
  [int]$MaxWidth = 1280
)

Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
[StructLayout(LayoutKind.Sequential)]
public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
public class WinGrab {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
}
"@

# Without this the rect comes back in logical pixels and the grab is offset.
[WinGrab]::SetProcessDPIAware() | Out-Null

$proc = Get-Process -ErrorAction SilentlyContinue |
        Where-Object { $_.ProcessName -like "*Trackmania*" -and $_.MainWindowHandle -ne 0 } |
        Select-Object -First 1
if (-not $proc) { "no Trackmania window found"; exit 1 }

$h = $proc.MainWindowHandle
if ([WinGrab]::IsIconic($h)) { [WinGrab]::ShowWindow($h, 9) | Out-Null }
[WinGrab]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 400

$r = New-Object RECT
[WinGrab]::GetWindowRect($h, [ref]$r) | Out-Null
$w = $r.Right - $r.Left
$hgt = $r.Bottom - $r.Top
if ($w -le 0 -or $hgt -le 0) { "bad window rect ${w}x${hgt}"; exit 1 }

$bmp = New-Object System.Drawing.Bitmap($w, $hgt)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
$g.Dispose()

if ($w -gt $MaxWidth) {
  $nh = [int]($hgt * $MaxWidth / $w)
  $small = New-Object System.Drawing.Bitmap($MaxWidth, $nh)
  $g2 = [System.Drawing.Graphics]::FromImage($small)
  $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g2.DrawImage($bmp, 0, 0, $MaxWidth, $nh)
  $g2.Dispose(); $bmp.Dispose(); $bmp = $small
}

$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
"captured $($proc.ProcessName) ${w}x${hgt} -> $Out"
