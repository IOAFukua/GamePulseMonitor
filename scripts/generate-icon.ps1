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

function Set-IconGraphicsQuality {
    param(
        [System.Drawing.Graphics]$Graphics
    )

    $Graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $Graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $Graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
}

function New-PolygonPath {
    param(
        [System.Drawing.PointF[]]$Points
    )

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddPolygon($Points)
    return $path
}

function New-SolidBrush {
    param(
        [int]$Alpha,
        [int]$Red,
        [int]$Green,
        [int]$Blue
    )

    return [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb($Alpha, $Red, $Green, $Blue))
}

function Stroke-Path {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Drawing2D.GraphicsPath]$Path,
        [int]$Alpha,
        [int]$Red,
        [int]$Green,
        [int]$Blue,
        [single]$Width
    )

    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb($Alpha, $Red, $Green, $Blue), $Width)
    try {
        $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $Graphics.DrawPath($pen, $Path)
    }
    finally {
        $pen.Dispose()
    }
}

function Draw-ThreeDimensionalCore {
    param(
        [System.Drawing.Graphics]$Graphics,
        [single]$X,
        [single]$Y,
        [single]$Size
    )

    $scale = $Size / 256.0
    Set-IconGraphicsQuality -Graphics $Graphics

    $shadow = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $shadow.AddEllipse($X + (42 * $scale), $Y + (202 * $scale), 172 * $scale, 26 * $scale)
    $shadowBrush = [System.Drawing.Drawing2D.PathGradientBrush]::new($shadow)
    try {
        $shadowBrush.CenterColor = [System.Drawing.Color]::FromArgb(58, 30, 80, 120)
        $shadowBrush.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 30, 80, 120))
        $Graphics.FillPath($shadowBrush, $shadow)
    }
    finally {
        $shadowBrush.Dispose()
        $shadow.Dispose()
    }

    $top = New-PolygonPath -Points @(
        [System.Drawing.PointF]::new($X + (80 * $scale), $Y + (36 * $scale)),
        [System.Drawing.PointF]::new($X + (178 * $scale), $Y + (54 * $scale)),
        [System.Drawing.PointF]::new($X + (222 * $scale), $Y + (118 * $scale)),
        [System.Drawing.PointF]::new($X + (134 * $scale), $Y + (170 * $scale)),
        [System.Drawing.PointF]::new($X + (39 * $scale), $Y + (132 * $scale))
    )
    $left = New-PolygonPath -Points @(
        [System.Drawing.PointF]::new($X + (39 * $scale), $Y + (132 * $scale)),
        [System.Drawing.PointF]::new($X + (134 * $scale), $Y + (170 * $scale)),
        [System.Drawing.PointF]::new($X + (134 * $scale), $Y + (213 * $scale)),
        [System.Drawing.PointF]::new($X + (52 * $scale), $Y + (176 * $scale))
    )
    $right = New-PolygonPath -Points @(
        [System.Drawing.PointF]::new($X + (134 * $scale), $Y + (170 * $scale)),
        [System.Drawing.PointF]::new($X + (222 * $scale), $Y + (118 * $scale)),
        [System.Drawing.PointF]::new($X + (204 * $scale), $Y + (160 * $scale)),
        [System.Drawing.PointF]::new($X + (134 * $scale), $Y + (213 * $scale))
    )
    $front = New-PolygonPath -Points @(
        [System.Drawing.PointF]::new($X + (52 * $scale), $Y + (176 * $scale)),
        [System.Drawing.PointF]::new($X + (134 * $scale), $Y + (213 * $scale)),
        [System.Drawing.PointF]::new($X + (204 * $scale), $Y + (160 * $scale)),
        [System.Drawing.PointF]::new($X + (222 * $scale), $Y + (118 * $scale)),
        [System.Drawing.PointF]::new($X + (134 * $scale), $Y + (170 * $scale)),
        [System.Drawing.PointF]::new($X + (39 * $scale), $Y + (132 * $scale))
    )

    $leftBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.RectangleF]::new($X + (38 * $scale), $Y + (130 * $scale), 100 * $scale, 86 * $scale),
        [System.Drawing.Color]::FromArgb(255, 31, 172, 222),
        [System.Drawing.Color]::FromArgb(255, 0, 103, 184),
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $rightBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.RectangleF]::new($X + (132 * $scale), $Y + (116 * $scale), 96 * $scale, 100 * $scale),
        [System.Drawing.Color]::FromArgb(255, 27, 205, 176),
        [System.Drawing.Color]::FromArgb(255, 0, 132, 193),
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $topBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.RectangleF]::new($X + (36 * $scale), $Y + (34 * $scale), 190 * $scale, 140 * $scale),
        [System.Drawing.Color]::FromArgb(255, 239, 254, 255),
        [System.Drawing.Color]::FromArgb(255, 110, 218, 248),
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)

    try {
        $Graphics.FillPath($leftBrush, $left)
        $Graphics.FillPath($rightBrush, $right)
        $Graphics.FillPath($topBrush, $top)

        Stroke-Path -Graphics $Graphics -Path $front -Alpha 235 -Red 255 -Green 255 -Blue 255 -Width ([single](12 * $scale))
        Stroke-Path -Graphics $Graphics -Path $front -Alpha 255 -Red 37 -Green 164 -Blue 218 -Width ([single](4 * $scale))
        Stroke-Path -Graphics $Graphics -Path $top -Alpha 170 -Red 255 -Green 255 -Blue 255 -Width ([single](3 * $scale))

        $core = New-PolygonPath -Points @(
            [System.Drawing.PointF]::new($X + (104 * $scale), $Y + (76 * $scale)),
            [System.Drawing.PointF]::new($X + (165 * $scale), $Y + (88 * $scale)),
            [System.Drawing.PointF]::new($X + (190 * $scale), $Y + (121 * $scale)),
            [System.Drawing.PointF]::new($X + (132 * $scale), $Y + (148 * $scale)),
            [System.Drawing.PointF]::new($X + (76 * $scale), $Y + (123 * $scale))
        )
        $coreBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
            [System.Drawing.RectangleF]::new($X + (72 * $scale), $Y + (72 * $scale), 122 * $scale, 80 * $scale),
            [System.Drawing.Color]::FromArgb(215, 255, 255, 255),
            [System.Drawing.Color]::FromArgb(120, 192, 246, 255),
            [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
        try {
            $Graphics.FillPath($coreBrush, $core)
            Stroke-Path -Graphics $Graphics -Path $core -Alpha 130 -Red 255 -Green 255 -Blue 255 -Width ([single](3 * $scale))
        }
        finally {
            $coreBrush.Dispose()
            $core.Dispose()
        }

        $accentTop = New-PolygonPath -Points @(
            [System.Drawing.PointF]::new($X + (161 * $scale), $Y + (55 * $scale)),
            [System.Drawing.PointF]::new($X + (211 * $scale), $Y + (72 * $scale)),
            [System.Drawing.PointF]::new($X + (194 * $scale), $Y + (100 * $scale)),
            [System.Drawing.PointF]::new($X + (151 * $scale), $Y + (83 * $scale))
        )
        $accentSide = New-PolygonPath -Points @(
            [System.Drawing.PointF]::new($X + (194 * $scale), $Y + (100 * $scale)),
            [System.Drawing.PointF]::new($X + (211 * $scale), $Y + (72 * $scale)),
            [System.Drawing.PointF]::new($X + (216 * $scale), $Y + (94 * $scale)),
            [System.Drawing.PointF]::new($X + (198 * $scale), $Y + (122 * $scale))
        )
        $accentTopBrush = New-SolidBrush -Alpha 255 -Red 45 -Green 216 -Blue 151
        $accentSideBrush = New-SolidBrush -Alpha 255 -Red 15 -Green 154 -Blue 126
        try {
            $Graphics.FillPath($accentSideBrush, $accentSide)
            $Graphics.FillPath($accentTopBrush, $accentTop)
            Stroke-Path -Graphics $Graphics -Path $accentTop -Alpha 230 -Red 255 -Green 255 -Blue 255 -Width ([single](5 * $scale))
            Stroke-Path -Graphics $Graphics -Path $accentSide -Alpha 160 -Red 255 -Green 255 -Blue 255 -Width ([single](3 * $scale))
        }
        finally {
            $accentTopBrush.Dispose()
            $accentSideBrush.Dispose()
            $accentTop.Dispose()
            $accentSide.Dispose()
        }

        $shinePen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(130, 255, 255, 255), [single](4 * $scale))
        try {
            $shinePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
            $shinePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
            $Graphics.DrawLine($shinePen, $X + (84 * $scale), $Y + (53 * $scale), $X + (137 * $scale), $Y + (63 * $scale))
            $Graphics.DrawLine($shinePen, $X + (62 * $scale), $Y + (128 * $scale), $X + (111 * $scale), $Y + (148 * $scale))
        }
        finally {
            $shinePen.Dispose()
        }
    }
    finally {
        $leftBrush.Dispose()
        $rightBrush.Dispose()
        $topBrush.Dispose()
        $front.Dispose()
        $right.Dispose()
        $left.Dispose()
        $top.Dispose()
    }
}

function New-IconBitmap {
    param(
        [int]$Size
    )

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        Set-IconGraphicsQuality -Graphics $graphics
        $graphics.Clear([System.Drawing.Color]::Transparent)
        Draw-ThreeDimensionalCore -Graphics $graphics -X 0 -Y 0 -Size $Size
    }
    finally {
        $graphics.Dispose()
    }

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
