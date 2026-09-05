<#
.SYNOPSIS
  Save a frame whenever a knockout popup is on screen, and nothing else.

.DESCRIPTION
  Generic frame differencing is useless while someone is driving: the camera changes
  most of the picture every frame. Both Nadeo's roll-call and our revival screen draw
  the same popup art, whose green is unmistakable (R is 0, G 85-150, B 50-100), so
  sampling a grid over the middle of the screen detects the screen itself.

  Never touches window focus, so it is safe while a match is running.
#>
param(
  [int]$Seconds = 600,
  [string]$OutDir = "$PSScriptRoot\..\tm-popups",
  [int]$MaxFrames = 8,
  [int]$Width = 1280
)

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
[StructLayout(LayoutKind.Sequential)]
public struct RECT3 { public int Left; public int Top; public int Right; public int Bottom; }
public class PopWatch {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT3 r);
}
"@
[PopWatch]::SetProcessDPIAware() | Out-Null

$proc = Get-Process -ErrorAction SilentlyContinue |
        Where-Object { $_.ProcessName -like "*Trackmania*" -and $_.MainWindowHandle -ne 0 } |
        Select-Object -First 1
if (-not $proc) { "no Trackmania window"; exit 1 }
$h = $proc.MainWindowHandle
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$deadline = (Get-Date).AddSeconds($Seconds)
$kept = 0
$wasUp = $false

while ((Get-Date) -lt $deadline -and $kept -lt $MaxFrames) {
  $r = New-Object RECT3
  [PopWatch]::GetWindowRect($h, [ref]$r) | Out-Null
  $w = $r.Right - $r.Left; $hh = $r.Bottom - $r.Top
  if ($w -le 0 -or $hh -le 0) { Start-Sleep -Milliseconds 1500; continue }

  $bmp = New-Object System.Drawing.Bitmap($w, $hh)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
  $g.Dispose()

  $hits = 0; $total = 0
  foreach ($fx in 0.34,0.42,0.50,0.58,0.66) {
    foreach ($fy in 0.40,0.50,0.60) {
      $c = $bmp.GetPixel([int]($w * $fx), [int]($hh * $fy))
      $total++
      if ($c.R -lt 40 -and $c.G -gt 70 -and $c.G -lt 165 -and $c.B -gt 40 -and $c.B -lt 110 -and $c.G -gt $c.B) { $hits++ }
    }
  }
  $isUp = $hits -ge [int]($total * 0.6)

  if ($isUp) {
    $nh = [int]($hh * $Width / $w)
    $small = New-Object System.Drawing.Bitmap($Width, $nh)
    $g2 = [System.Drawing.Graphics]::FromImage($small)
    $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g2.DrawImage($bmp, 0, 0, $Width, $nh)
    $g2.Dispose()
    $stamp = (Get-Date).ToString("HHmmss")
    $small.Save((Join-Path $OutDir "popup-$stamp.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $small.Dispose()
    $kept++
    "popup at $stamp ($hits/$total)"

    # A second frame a few seconds in. The first fires on the popup's first green
    # pixel, which is before Nadeo animates the player rows in, so an early frame
    # shows the header and count over an empty panel and looks like a bug.
    Start-Sleep -Milliseconds 2600
    $r2 = New-Object RECT3
    [PopWatch]::GetWindowRect($h, [ref]$r2) | Out-Null
    $w2 = $r2.Right - $r2.Left; $h2 = $r2.Bottom - $r2.Top
    if ($w2 -gt 0 -and $h2 -gt 0) {
      $late = New-Object System.Drawing.Bitmap($w2, $h2)
      $lg = [System.Drawing.Graphics]::FromImage($late)
      $lg.CopyFromScreen($r2.Left, $r2.Top, 0, 0, $late.Size)
      $lg.Dispose()
      $lnh = [int]($h2 * $Width / $w2)
      $ls = New-Object System.Drawing.Bitmap($Width, $lnh)
      $lg2 = [System.Drawing.Graphics]::FromImage($ls)
      $lg2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
      $lg2.DrawImage($late, 0, 0, $Width, $lnh)
      $lg2.Dispose()
      $ls.Save((Join-Path $OutDir "popup-$stamp-late.png"), [System.Drawing.Imaging.ImageFormat]::Png)
      $ls.Dispose(); $late.Dispose()
      "  late frame for $stamp"
    }

    # Short, because a revival round shows the stock roll-call and then our screen a
    # few seconds later. Sleeping through that gap is how the first pass missed ours.
    Start-Sleep -Milliseconds 1200
  }
  $wasUp = $isUp
  $bmp.Dispose()
  Start-Sleep -Milliseconds 1200
}
"done: $kept frames in $OutDir"
