param(
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $root 'src\GamePulseMonitor\Assets'
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

Add-Type -AssemblyName System.Drawing

function New-IconBitmap {
    param(
        [int]$Size
    )

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

    $scale = $Size / 256.0
    $corner = [Math]::Round(44 * $scale)
    $bounds = [System.Drawing.RectangleF]::new([single](8 * $scale), [single](8 * $scale), [single](240 * $scale), [single](240 * $scale))

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = 2 * $corner
    $path.AddArc($bounds.X, $bounds.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($bounds.Right - $diameter, $bounds.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($bounds.Right - $diameter, $bounds.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($bounds.X, $bounds.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()

    $background = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        $bounds,
        [System.Drawing.Color]::FromArgb(255, 9, 18, 32),
        [System.Drawing.Color]::FromArgb(255, 16, 33, 50),
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
    $graphics.FillPath($background, $path)
    $borderPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 69, 95, 128), [single](4 * $scale))
    $graphics.DrawPath($borderPen, $path)

    $monitorRect = [System.Drawing.RectangleF]::new([single](48 * $scale), [single](50 * $scale), [single](160 * $scale), [single](110 * $scale))
    $monitorPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $monitorRadius = 18 * $scale
    $monitorDiameter = 2 * $monitorRadius
    $monitorPath.AddArc($monitorRect.X, $monitorRect.Y, $monitorDiameter, $monitorDiameter, 180, 90)
    $monitorPath.AddArc($monitorRect.Right - $monitorDiameter, $monitorRect.Y, $monitorDiameter, $monitorDiameter, 270, 90)
    $monitorPath.AddArc($monitorRect.Right - $monitorDiameter, $monitorRect.Bottom - $monitorDiameter, $monitorDiameter, $monitorDiameter, 0, 90)
    $monitorPath.AddArc($monitorRect.X, $monitorRect.Bottom - $monitorDiameter, $monitorDiameter, $monitorDiameter, 90, 90)
    $monitorPath.CloseFigure()

    $screenBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 12, 25, 39))
    $accentPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 47, 226, 166), [single](10 * $scale))
    $accentPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $accentPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $accentPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    $graphics.FillPath($screenBrush, $monitorPath)
    $monitorPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 95, 205, 255), [single](7 * $scale))
    $graphics.DrawPath($monitorPen, $monitorPath)

    $pulsePoints = @(
        [System.Drawing.PointF]::new([single](64 * $scale), [single](118 * $scale)),
        [System.Drawing.PointF]::new([single](92 * $scale), [single](118 * $scale)),
        [System.Drawing.PointF]::new([single](108 * $scale), [single](88 * $scale)),
        [System.Drawing.PointF]::new([single](126 * $scale), [single](139 * $scale)),
        [System.Drawing.PointF]::new([single](146 * $scale), [single](105 * $scale)),
        [System.Drawing.PointF]::new([single](190 * $scale), [single](105 * $scale))
    )
    $graphics.DrawLines($accentPen, $pulsePoints)

    $standPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 95, 205, 255), [single](7 * $scale))
    $standPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $standPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($standPen, 128 * $scale, 160 * $scale, 128 * $scale, 181 * $scale)
    $graphics.DrawLine($standPen, 92 * $scale, 185 * $scale, 164 * $scale, 185 * $scale)

    if ($Size -ge 48) {
        $fontSize = [Math]::Max(15, [Math]::Round(38 * $scale))
        $font = [System.Drawing.Font]::new('Segoe UI', [single]$fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
        $textBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 246, 249, 255))
        $format = [System.Drawing.StringFormat]::new()
        $format.Alignment = [System.Drawing.StringAlignment]::Center
        $format.LineAlignment = [System.Drawing.StringAlignment]::Center
        $textRect = [System.Drawing.RectangleF]::new([single](48 * $scale), [single](189 * $scale), [single](160 * $scale), [single](44 * $scale))
        $graphics.DrawString('FPS', $font, $textBrush, $textRect, $format)
        $font.Dispose()
        $textBrush.Dispose()
        $format.Dispose()
    }

    $standPen.Dispose()
    $accentPen.Dispose()
    $monitorPen.Dispose()
    $screenBrush.Dispose()
    $borderPen.Dispose()
    $background.Dispose()
    $path.Dispose()
    $monitorPath.Dispose()
    $graphics.Dispose()

    return $bitmap
}

function Save-Png {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [string]$Path
    )

    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function Write-Ico {
    param(
        [string]$Path,
        [byte[][]]$PngImages,
        [int[]]$Sizes
    )

    $stream = [System.IO.File]::Create($Path)
    $writer = New-Object System.IO.BinaryWriter $stream
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$PngImages.Length)

        $offset = 6 + (16 * $PngImages.Length)
        for ($i = 0; $i -lt $PngImages.Length; $i++) {
            $size = $Sizes[$i]
            $writer.Write([byte]($(if ($size -eq 256) { 0 } else { $size })))
            $writer.Write([byte]($(if ($size -eq 256) { 0 } else { $size })))
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$PngImages[$i].Length)
            $writer.Write([uint32]$offset)
            $offset += $PngImages[$i].Length
        }

        foreach ($image in $PngImages) {
            $writer.Write($image)
        }
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngImages = New-Object 'System.Collections.Generic.List[byte[]]'

foreach ($size in $sizes) {
    $bitmap = New-IconBitmap -Size $size
    try {
        if ($size -eq 256) {
            Save-Png -Bitmap $bitmap -Path (Join-Path $OutputDirectory 'GamePulseMonitor.png')
        }

        $memory = New-Object System.IO.MemoryStream
        try {
            $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
            $pngImages.Add($memory.ToArray())
        }
        finally {
            $memory.Dispose()
        }
    }
    finally {
        $bitmap.Dispose()
    }
}

Write-Ico -Path (Join-Path $OutputDirectory 'GamePulseMonitor.ico') -PngImages $pngImages.ToArray() -Sizes $sizes
Write-Host "Generated icon assets in $OutputDirectory"
