Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"

$outputPath = Join-Path (Split-Path $PSScriptRoot -Parent) "assets"
if (-not (Test-Path $outputPath)) {
    New-Item -ItemType Directory $outputPath | Out-Null
}

$sizes = @(16, 32, 48, 256)
$reviewPath = Join-Path (Split-Path $PSScriptRoot -Parent) "docs\reports\icon-review"
$runlyMasterBase64 = "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"

$icons = @(
    @{ Name = "js";      Label = "JS"; Bg = "#F7DF1E"; Fg = "#000000" },
    @{ Name = "ts";      Label = "TS"; Bg = "#3178C6"; Fg = "#FFFFFF" },
    @{ Name = "ps1";     Label = "PS"; Bg = "#0078D4"; Fg = "#FFFFFF" },
    @{ Name = "py";      Label = "PY"; Bg = "#3776AB"; Fg = "#FFFFFF" },
    @{ Name = "sh";      Label = "SH"; Bg = "#4EAA25"; Fg = "#FFFFFF" },
    @{ Name = "generic"; Label = "?";  Bg = "#666666"; Fg = "#FFFFFF" }
)

function New-RoundedSquarePath {
    param([int]$Size, [int]$Inset = 0)

    # Teknesyum UI §10 ile ayni dil: yaricap, dis kenarin tam %22'si.
    $radius = [single]($Size * 0.22)
    $diameter = $radius * 2
    $edge = [single]($Size - 1 - $Inset)
    $start = [single]$Inset
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($start, $start, $diameter, $diameter, 180, 90)
    $path.AddArc($edge - $diameter, $start, $diameter, $diameter, 270, 90)
    $path.AddArc($edge - $diameter, $edge - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($start, $edge - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-RunlySmallFrame {
    param([int]$Size)

    # 16px karesi ana cizimin kucultulmesiyle uretilemez: uc ince hiz izi tek pikselin altina
    # dusup mor bir lekeye donusuyor ve ok bir delik gibi okunuyor. Bu boyut elle cizilir --
    # izler atilir, geriye belge + dolu ok kalir. Standart: 16px once tasarlanir, sonra buyutulur.
    $scale = $Size / 16.0
    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

    $blue = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml("#00F3FF"))
    $pink = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml("#FF00EA"))
    $surface = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml("#08090A"))

    $outer = New-RoundedSquarePath -Size $Size
    $graphics.FillPath($surface, $outer)

    # Belge govdesi: ust sag kose katlanmis.
    $doc = New-Object System.Drawing.Drawing2D.GraphicsPath
    $doc.StartFigure()
    $doc.AddLine(3.0*$scale, 2.2*$scale, 10.2*$scale, 2.2*$scale)
    $doc.AddLine(10.2*$scale, 2.2*$scale, 13.0*$scale, 5.0*$scale)
    $doc.AddLine(13.0*$scale, 5.0*$scale, 13.0*$scale, 13.8*$scale)
    $doc.AddLine(13.0*$scale, 13.8*$scale, 3.0*$scale, 13.8*$scale)
    $doc.CloseFigure()
    $graphics.FillPath($blue, $doc)

    # Katlanan kose.
    $fold = New-Object System.Drawing.Drawing2D.GraphicsPath
    $fold.StartFigure()
    $fold.AddLine(10.2*$scale, 2.2*$scale, 13.0*$scale, 5.0*$scale)
    $fold.AddLine(13.0*$scale, 5.0*$scale, 10.2*$scale, 5.0*$scale)
    $fold.CloseFigure()
    $graphics.FillPath($pink, $fold)

    # Dolu ok: 16px'te okunan tek "calisiyor" isareti.
    $arrow = New-Object System.Drawing.Drawing2D.GraphicsPath
    $arrow.StartFigure()
    $arrow.AddLine(5.4*$scale, 5.6*$scale, 11.0*$scale, 8.4*$scale)
    $arrow.AddLine(11.0*$scale, 8.4*$scale, 5.4*$scale, 11.2*$scale)
    $arrow.CloseFigure()
    $graphics.FillPath($pink, $arrow)

    $arrow.Dispose(); $fold.Dispose(); $doc.Dispose(); $outer.Dispose()
    $surface.Dispose(); $pink.Dispose(); $blue.Dispose(); $graphics.Dispose()
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    return $stream.ToArray()
}

function New-RunlyFrame {
    param([int]$Size)

    if ($Size -le 16) {
        return New-RunlySmallFrame -Size $Size
    }

    # 32px ve uzeri: kullanici tarafindan onaylanan 1024px ana cizimin olceklenmesi.
    $masterPath = Join-Path $PSScriptRoot "runly-master.png"
    if (-not (Test-Path -LiteralPath $masterPath)) {
        throw "Runly ana ikon cizimi bulunamadi: $masterPath"
    }
    $master = [System.Drawing.Image]::FromFile($masterPath)
    $scaled = New-Object System.Drawing.Bitmap($Size, $Size)
    $scaledGraphics = [System.Drawing.Graphics]::FromImage($scaled)
    $scaledGraphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $scaledGraphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $scaledGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $scaledGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $scaledGraphics.DrawImage($master, 0, 0, $Size, $Size)
    $scaledGraphics.Dispose(); $master.Dispose()
    $scaledStream = New-Object System.IO.MemoryStream
    $scaled.Save($scaledStream, [System.Drawing.Imaging.ImageFormat]::Png)
    $scaled.Dispose()
    return $scaledStream.ToArray()
}

function New-IconFrame {
    param([int]$Size, [string]$Label, [string]$Bg, [string]$Fg)

    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias

    $bgColor = [System.Drawing.ColorTranslator]::FromHtml($Bg)
    $brush = New-Object System.Drawing.SolidBrush($bgColor)
    $radius = [Math]::Max(2, [int]($Size * 0.18))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $radius * 2, $radius * 2, 180, 90)
    $path.AddArc($Size - $radius * 2 - 1, 0, $radius * 2, $radius * 2, 270, 90)
    $path.AddArc($Size - $radius * 2 - 1, $Size - $radius * 2 - 1, $radius * 2, $radius * 2, 0, 90)
    $path.AddArc(0, $Size - $radius * 2 - 1, $radius * 2, $radius * 2, 90, 90)
    $path.CloseFigure()
    $graphics.FillPath($brush, $path)

    $fontSize = if ($Label.Length -ge 2) { $Size * 0.38 } else { $Size * 0.55 }
    $font = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $fgColor = [System.Drawing.ColorTranslator]::FromHtml($Fg)
    $textBrush = New-Object System.Drawing.SolidBrush($fgColor)
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $rect = New-Object System.Drawing.RectangleF(0, 0, $Size, $Size)
    $graphics.DrawString($Label, $font, $textBrush, $rect, $format)

    $format.Dispose(); $textBrush.Dispose(); $font.Dispose()
    $path.Dispose(); $brush.Dispose(); $graphics.Dispose()

    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    return $stream.ToArray()
}

function Save-Ico {
    param([string]$Path, [byte[][]]$Frames, [int[]]$FrameSizes)

    $stream = [System.IO.File]::Create($Path)
    $writer = New-Object System.IO.BinaryWriter($stream)

    # ICONDIR: reserved, type=1 (icon), image count
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$Frames.Count)

    # Frame data starts after the directory: 6 header bytes + 16 bytes per entry
    $offset = 6 + (16 * $Frames.Count)

    for ($i = 0; $i -lt $Frames.Count; $i++) {
        $size = $FrameSizes[$i]
        # 256 is stored as 0 in the single-byte width/height fields
        $dim = if ($size -ge 256) { 0 } else { $size }
        $writer.Write([byte]$dim)          # width
        $writer.Write([byte]$dim)          # height
        $writer.Write([byte]0)             # palette entries (0 = truecolor)
        $writer.Write([byte]0)             # reserved
        $writer.Write([uint16]1)           # color planes
        $writer.Write([uint16]32)          # bits per pixel
        $writer.Write([uint32]$Frames[$i].Length)
        $writer.Write([uint32]$offset)
        $offset += $Frames[$i].Length
    }

    foreach ($frame in $Frames) {
        $writer.Write($frame)
    }

    $writer.Flush(); $writer.Dispose(); $stream.Dispose()
}

function Export-IcoPng {
    param([string]$IcoPath, [int]$Size, [string]$PngPath)

    $bytes = [System.IO.File]::ReadAllBytes($IcoPath)
    $count = [BitConverter]::ToUInt16($bytes, 4)
    for ($i = 0; $i -lt $count; $i++) {
        $entry = 6 + (16 * $i)
        $width = if ($bytes[$entry] -eq 0) { 256 } else { [int]$bytes[$entry] }
        if ($width -eq $Size) {
            $length = [BitConverter]::ToUInt32($bytes, $entry + 8)
            $offset = [BitConverter]::ToUInt32($bytes, $entry + 12)
            $png = New-Object byte[] $length
            [Array]::Copy($bytes, $offset, $png, 0, $length)
            [System.IO.File]::WriteAllBytes($PngPath, $png)
            return
        }
    }
    throw "$IcoPath icinde $Size px kare bulunamadi."
}

Write-Host "=== Runly ikonlari uretiliyor ===" -ForegroundColor Green

if (-not (Test-Path $reviewPath)) {
    New-Item -ItemType Directory $reviewPath -Force | Out-Null
}

# Kullanici tarafindan onaylanan ilk D konsepti.
$runlyFrames = @()
foreach ($size in $sizes) {
    $runlyFrames += , (New-RunlyFrame -Size $size)
}
$runlyPath = Join-Path $outputPath "runly.ico"
Save-Ico -Path $runlyPath -Frames $runlyFrames -FrameSizes $sizes
$runlyBytes = [System.IO.File]::ReadAllBytes($runlyPath)
if ($runlyBytes[0] -ne 0 -or $runlyBytes[1] -ne 0 -or $runlyBytes[2] -ne 1 -or $runlyBytes[3] -ne 0) {
    throw "$runlyPath gecerli bir ICO degil (ICONDIR basligi hatali)."
}
Write-Host ("  {0,-14} {1,6} bayt  {2} kare" -f "runly", $runlyBytes.Length, ([BitConverter]::ToUInt16($runlyBytes, 4))) -ForegroundColor Cyan

foreach ($icon in $icons) {
    $icoPath = Join-Path $outputPath "$($icon.Name).ico"

    $frames = @()
    foreach ($size in $sizes) {
        $frames += , (New-IconFrame -Size $size -Label $icon.Label -Bg $icon.Bg -Fg $icon.Fg)
    }

    Save-Ico -Path $icoPath -Frames $frames -FrameSizes $sizes

    $bytes = [System.IO.File]::ReadAllBytes($icoPath)
    if ($bytes[0] -ne 0 -or $bytes[1] -ne 0 -or $bytes[2] -ne 1 -or $bytes[3] -ne 0) {
        throw "$icoPath gecerli bir ICO degil (ICONDIR basligi hatali)."
    }
    $count = [BitConverter]::ToUInt16($bytes, 4)
    Write-Host ("  {0,-14} {1,6} bayt  {2} kare" -f $icon.Name, $bytes.Length, $count) -ForegroundColor Cyan
}

# Onizlemeleri dogrudan uretilmis ICO'nun karelerinden ac.
foreach ($size in @(16, 32, 256)) {
    Export-IcoPng -IcoPath $runlyPath -Size $size -PngPath (Join-Path $reviewPath "runly-from-ico-$size.png")
}

Get-ChildItem $outputPath -Filter "*.png" | Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host "Tamamlandi." -ForegroundColor Green
