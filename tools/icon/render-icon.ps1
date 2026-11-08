# Renders TSCC_WASD.svg's design into TSCC_WASD.ico (+ tools/icon/preview.png) using only WPF from Windows.
# Run: powershell -STA -NoProfile -ExecutionPolicy Bypass -File tools\icon\render-icon.ps1
Add-Type -AssemblyName PresentationCore, WindowsBase
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$icoPath = Join-Path $root 'TSCC_WASD.App\Assets\TSCC_WASD.ico'
$pngPath = Join-Path $root 'TSCC_WASD.App\Assets\TSCC_WASD_256.png'   # in-app header image (sharp at any displayed size via HighQuality scaling)
$previewPath = Join-Path $PSScriptRoot 'preview.png'
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$smallMax = 24   # sizes <= this use the simplified variant

$pad = [Windows.Media.Brushes]::Transparent
function Brush($hex) { $b = [Windows.Media.SolidColorBrush]([Windows.Media.ColorConverter]::ConvertFromString($hex)); $b.Freeze(); $b }
$bg = Brush '#1E2433'; $blue = Brush '#7C9CFF'; $white = Brush '#FFFFFF'
$body = [Windows.Media.Geometry]::Parse('M76 88h104c22 0 36 16 42 40l10 48c4 20-14 32-28 20l-28-26h-96l-28 26c-14 12-32 0-28-20l10-48c6-24 20-40 42-40z')
$dpad = [Windows.Media.Geometry]::Parse('M84 116V152M66 134H102')
$body.Freeze(); $dpad.Freeze()

function Draw-Icon($dc, [bool]$small) {
  $dc.DrawRoundedRectangle($bg, $null, [Windows.Rect]::new(8, 8, 240, 240), 56, 56)
  # back controller: hollow outline (thicker when small)
  $sw = if ($small) { 22 } else { 14 }
  $pen = [Windows.Media.Pen]::new($blue, $sw); $pen.LineJoin = 'Round'
  $m = [Windows.Media.Matrix]::Identity
  $m.Translate(-128, -142); $m.Scale(0.7, 0.7); $m.Translate(112, 110)
  $dc.PushTransform([Windows.Media.MatrixTransform]::new($m)); $dc.DrawGeometry($null, $pen, $body); $dc.Pop()
  # front controller: solid white with buttons (no buttons when small)
  $m = [Windows.Media.Matrix]::Identity
  $m.Translate(-128, -142); $m.Scale(0.7, 0.7); $m.Translate(144, 150)
  $dc.PushTransform([Windows.Media.MatrixTransform]::new($m))
  $dc.DrawGeometry($white, $null, $body)
  if (-not $small) {
    foreach ($p in @(@(172,116), @(154,134), @(190,134), @(172,152))) {
      $dc.DrawEllipse($bg, $null, [Windows.Point]::new($p[0], $p[1]), 9, 9)
    }
    $pen2 = [Windows.Media.Pen]::new($bg, 12); $pen2.StartLineCap = 'Round'; $pen2.EndLineCap = 'Round'
    $dc.DrawGeometry($null, $pen2, $dpad)
  }
  $dc.Pop()
}

function Render([int]$px) {
  $small = $px -le $smallMax
  $dv = [Windows.Media.DrawingVisual]::new()
  $dc = $dv.RenderOpen()
  $dc.PushTransform([Windows.Media.ScaleTransform]::new($px / 256.0, $px / 256.0))
  Draw-Icon $dc $small
  $dc.Pop(); $dc.Close()
  [Windows.Media.RenderOptions]::SetEdgeMode($dv, 'Unspecified')
  $rtb = [Windows.Media.Imaging.RenderTargetBitmap]::new($px, $px, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
  $rtb.Render($dv); $rtb.Freeze(); $rtb
}
function ToPng($bmp) {
  $enc = [Windows.Media.Imaging.PngBitmapEncoder]::new()
  $enc.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bmp))
  $ms = [IO.MemoryStream]::new(); $enc.Save($ms); ,$ms.ToArray()
}

# ---- .ico ----
$pngs = foreach ($s in $sizes) { ,(ToPng (Render $s)) }
$ms = [IO.MemoryStream]::new(); $w = [IO.BinaryWriter]::new($ms)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $s = $sizes[$i]; $b = if ($s -ge 256) { 0 } else { $s }
  $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0)
  $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
  $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Flush(); [IO.File]::WriteAllBytes($icoPath, $ms.ToArray())

[IO.File]::WriteAllBytes($pngPath, (ToPng (Render 256)))

# ---- preview sheet: each size 1:1 on light and dark, plus 256 enlarged ----
$sheetW = 1100; $sheetH = 620
$dv = [Windows.Media.DrawingVisual]::new(); $dc = $dv.RenderOpen()
$dc.DrawRectangle((Brush '#F3F3F3'), $null, [Windows.Rect]::new(0, 0, $sheetW / 2, $sheetH))
$dc.DrawRectangle((Brush '#101010'), $null, [Windows.Rect]::new($sheetW / 2, 0, $sheetW / 2, $sheetH))
$ft = { param($t, $c) [Windows.Media.FormattedText]::new($t, [Globalization.CultureInfo]::InvariantCulture, 'LeftToRight', [Windows.Media.Typeface]::new('Segoe UI'), 13, (Brush $c), 1.0) }
$x = 20
foreach ($s in ($sizes | Where-Object { $_ -lt 256 })) {
  $bmp = Render $s
  $dc.DrawImage($bmp, [Windows.Rect]::new($x, 40, $s, $s))
  $dc.DrawImage($bmp, [Windows.Rect]::new($sheetW / 2 + $x, 40, $s, $s))
  $dc.DrawText((& $ft "$s" '#555555'), [Windows.Point]::new($x, 15))
  $dc.DrawText((& $ft "$s" '#AAAAAA'), [Windows.Point]::new($sheetW / 2 + $x, 15))
  $x += $s + 12
}
$big = Render 256
$dc.DrawImage($big, [Windows.Rect]::new(20, 260, 340, 340))
$dc.DrawImage($big, [Windows.Rect]::new($sheetW / 2 + 20, 260, 340, 340))
$dc.Close()
$sheet = [Windows.Media.Imaging.RenderTargetBitmap]::new($sheetW, $sheetH, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
$sheet.Render($dv)
[IO.File]::WriteAllBytes($previewPath, (ToPng $sheet))
Write-Host "Wrote $icoPath, $pngPath and $previewPath"
