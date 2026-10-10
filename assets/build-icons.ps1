param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$sourcePath = Join-Path $PSScriptRoot 'requiem-logo.png'
$source = [System.Drawing.Image]::FromFile($sourcePath)
$stream = [System.IO.File]::Create((Join-Path $PSScriptRoot 'requiem.ico'))
$writer = [System.IO.BinaryWriter]::new($stream)
try {
    $sizes = @(16, 24, 32, 48, 64, 128, 256)
    $images = [System.Collections.Generic.List[byte[]]]::new()
    foreach ($size in $sizes) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $memory = [System.IO.MemoryStream]::new()
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($source, [System.Drawing.Rectangle]::new(0, 0, $size, $size), 0, 0, $source.Width, $source.Height, [System.Drawing.GraphicsUnit]::Pixel)
            $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
            $images.Add($memory.ToArray())
            if ($size -eq 32) { $bitmap.Save((Join-Path $PSScriptRoot 'requiem-widget.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
        } finally { $memory.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
    }
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Length)
    $offset = 6 + 16 * $sizes.Length
    for ($index = 0; $index -lt $sizes.Length; $index++) {
        $dimension = if ($sizes[$index] -eq 256) { 0 } else { $sizes[$index] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$index].Length); $writer.Write([uint32]$offset)
        $offset += $images[$index].Length
    }
    foreach ($bytes in $images) { $writer.Write($bytes) }
    [pscustomobject]@{ SourceWidth=$source.Width; SourceHeight=$source.Height; CornerAlpha=([System.Drawing.Bitmap]$source).GetPixel(0,0).A; IconSizes=($sizes -join ', ') }
} finally { $writer.Dispose(); $stream.Dispose(); $source.Dispose() }
