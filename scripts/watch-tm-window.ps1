<#
.SYNOPSIS
  Watch the Trackmania window and keep only the frames where the screen changed.

.DESCRIPTION
  Built for catching HUD that appears for a few seconds mid-run: a knockout roll-call,
  a big message, an elimination warning. Capturing at a fixed rate produces hundreds of
  near-identical frames, so this compares each grab against the last one and saves only
  the ones that differ, then tiles them into a single contact sheet.

  It never touches window focus, so it is safe to run while you drive.

.EXAMPLE
  ./scripts/watch-tm-window.ps1 -Seconds 180 -OutDir shots
#>
param(
  [int]$Seconds = 180,
  [double]$Fps = 2,
  [string]$OutDir = "$PSScriptRoot\..\tm-shots",
  # Percent of sampled pixels that must change before a frame is worth keeping.
  [double]$Threshold = 3.5,
  [int]$MaxFrames = 40,
  [int]$Width = 1280
)

Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
[StructLayout(LayoutKind.Sequential)]
public struct RECT2 { public int Left; public int Top; public int Right; public int Bottom; }
public class WinWatch {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT2 r);
}
"@

[WinWatch]::SetProcessDPIAware() | Out-Null

$proc = Get-Process -ErrorAction SilentlyContinue |
        Where-Object { $_.ProcessName -like "*Trackmania*" -and $_.MainWindowHandle -ne 0 } |
        Select-Object -First 1
if (-not $proc) { "no Trackmania window found"; exit 1 }
$h = $proc.MainWindowHandle

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Get-ChildItem $OutDir -Filter "frame-*.png" -ErrorAction SilentlyContinue | Remove-Item -Force

# Grayscale sample of a tiny thumbnail. Cheap enough to run every frame.
function Get-Signature($bmp) {
  $t = New-Object System.Drawing.Bitmap 64, 36
  $g = [System.Drawing.Graphics]::FromImage($t)
  $g.DrawImage($bmp, 0, 0, 64, 36)
  $g.Dispose()
  $sig = New-Object 'int[]' (64 * 36)
  for ($y = 0; $y -lt 36; $y++) {
    for ($x = 0; $x -lt 64; $x++) {
      $c = $t.GetPixel($x, $y)
      $sig[$y * 64 + $x] = [int](($c.R + $c.G + $c.B) / 3)
    }
  }
  $t.Dispose()
  return $sig
}

$interval = [int](1000 / $Fps)
$deadline = (Get-Date).AddSeconds($Seconds)
$prev = $null
$kept = 0
$grabs = 0

while ((Get-Date) -lt $deadline -and $kept -lt $MaxFrames) {
  $r = New-Object RECT2
  [WinWatch]::GetWindowRect($h, [ref]$r) | Out-Null
  $w = $r.Right - $r.Left; $hgt = $r.Bottom - $r.Top
  if ($w -le 0 -or $hgt -le 0) { Start-Sleep -Milliseconds $interval; continue }

  $bmp = New-Object System.Drawing.Bitmap($w, $hgt)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
  $g.Dispose()
  $grabs++

  $sig = Get-Signature $bmp
  $changed = 0
  if ($prev -ne $null) {
    for ($i = 0; $i -lt $sig.Length; $i++) {
      if ([Math]::Abs($sig[$i] - $prev[$i]) -gt 18) { $changed++ }
    }
    $pct = 100.0 * $changed / $sig.Length
  } else {
    $pct = 100.0
  }
  $prev = $sig

  if ($pct -ge $Threshold) {
    $nh = [int]($hgt * $Width / $w)
    $small = New-Object System.Drawing.Bitmap($Width, $nh)
    $g2 = [System.Drawing.Graphics]::FromImage($small)
    $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g2.DrawImage($bmp, 0, 0, $Width, $nh)
    $g2.Dispose()
    $stamp = (Get-Date).ToString("HHmmss-fff")
    $small.Save((Join-Path $OutDir ("frame-{0:d3}-{1}.png" -f $kept, $stamp)), [System.Drawing.Imaging.ImageFormat]::Png)
    $small.Dispose()
    $kept++
  }
  $bmp.Dispose()
  Start-Sleep -Milliseconds $interval
}

# One image beats forty. Tile what survived into a grid, four across.
$frames = Get-ChildItem $OutDir -Filter "frame-*.png" | Sort-Object Name
if ($frames.Count -gt 0) {
  $cols = 4
  $rows = [Math]::Ceiling($frames.Count / $cols)
  $cw = 440; $ch = 248
  $sheet = New-Object System.Drawing.Bitmap ($cols * $cw), ($rows * $ch)
  $gs = [System.Drawing.Graphics]::FromImage($sheet)
  $gs.Clear([System.Drawing.Color]::Black)
  $font = New-Object System.Drawing.Font("Consolas", 14, [System.Drawing.FontStyle]::Bold)
  for ($i = 0; $i -lt $frames.Count; $i++) {
    $img = [System.Drawing.Image]::FromFile($frames[$i].FullName)
    $x = ($i % $cols) * $cw; $y = [Math]::Floor($i / $cols) * $ch
    $gs.DrawImage($img, $x, $y, $cw, ($ch - 18))
    $gs.DrawString($frames[$i].BaseName.Substring(6), $font, [System.Drawing.Brushes]::Lime, $x + 4, $y + $ch - 20)
    $img.Dispose()
  }
  $gs.Dispose()
  $sheetPath = Join-Path $OutDir "contact-sheet.png"
  $sheet.Save($sheetPath, [System.Drawing.Imaging.ImageFormat]::Png)
  $sheet.Dispose()
  "kept $kept of $grabs grabs -> $OutDir"
  "contact sheet: $sheetPath"
} else {
  "kept 0 of $grabs grabs, nothing changed above $Threshold%"
}
