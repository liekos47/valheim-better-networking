# Draws the package icon in a hand-drawn colour-pen style and saves it as the 256x256 PNG Thunderstore requires.
param([string]$Out = (Join-Path (Split-Path $PSScriptRoot -Parent) "CW_Jesse.BetterNetworking\icon.png"))
Add-Type -AssemblyName System.Drawing
$S = 1024
$bmp = New-Object System.Drawing.Bitmap $S, $S
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.TextRenderingHint = 'AntiAlias'
$g.PixelOffsetMode = 'HighQuality'
$rnd = New-Object System.Random 47

function C([string]$hex, [int]$a = 255) {
    $c = [System.Drawing.ColorTranslator]::FromHtml($hex)
    [System.Drawing.Color]::FromArgb($a, $c.R, $c.G, $c.B)
}
function Pen($color, [float]$w) {
    $p = New-Object System.Drawing.Pen $color, $w
    $p.StartCap = 'Round'; $p.EndCap = 'Round'; $p.LineJoin = 'Round'
    $p
}
function J([float]$amount) { ($rnd.NextDouble() * 2 - 1) * $amount }

# an arc whose radius drifts the way a hand does; angles in degrees, clockwise from +x
function WobblyArc([float]$cx, [float]$cy, [float]$r, [float]$from, [float]$to, [float]$wobble) {
    $p1 = $rnd.NextDouble() * 6.28; $p2 = $rnd.NextDouble() * 6.28
    $pts = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    $steps = [Math]::Max(6, [int]([Math]::Abs($to - $from) / 9))
    for ($i = 0; $i -le $steps; $i++) {
        $deg = $from + ($to - $from) * $i / $steps
        $a = $deg * [Math]::PI / 180
        $rr = $r + $wobble * ([Math]::Sin($a * 2.3 + $p1) * 0.6 + [Math]::Sin($a * 5.1 + $p2) * 0.4)
        $pts.Add((New-Object System.Drawing.PointF ($cx + $rr * [Math]::Cos($a)), ($cy + $rr * [Math]::Sin($a))))
    }
    , $pts.ToArray()
}
# pen strokes never land twice in the same place: draw each line a few times, slightly off
function Stroke($color, [float]$w, [float]$cx, [float]$cy, [float]$r, [float]$from, [float]$to, [float]$wobble, [int]$passes = 2) {
    for ($k = 0; $k -lt $passes; $k++) {
        $g.DrawCurve((Pen $color ($w * (0.85 + $rnd.NextDouble() * 0.3))), (WobblyArc ($cx + (J 3)) ($cy + (J 3)) $r ($from + (J 2)) ($to + (J 2)) $wobble), 0.5)
    }
}
function Annulus([float]$cx, [float]$cy, [float]$r0, [float]$r1) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddEllipse($cx - $r1, $cy - $r1, 2 * $r1, 2 * $r1)
    if ($r0 -gt 0) { $path.AddEllipse($cx - $r0, $cy - $r0, 2 * $r0, 2 * $r0) }
    $path
}
# colour in a shape with back-and-forth pen lines at one angle
function Hatch($path, $color, [float]$w, [float]$angle, [float]$gap) {
    $state = $g.Save()
    $g.SetClip($path)
    $b = $path.GetBounds()
    $mx = $b.X + $b.Width / 2; $my = $b.Y + $b.Height / 2
    $half = [Math]::Max($b.Width, $b.Height) * 0.75
    $g.TranslateTransform($mx, $my)
    $g.RotateTransform($angle)
    for ($y = - $half; $y -le $half; $y += $gap) {
        [System.Drawing.PointF[]]$pts = @(
            (New-Object System.Drawing.PointF (- $half + (J 30)), ($y + (J 5))),
            (New-Object System.Drawing.PointF ((J 60)), ($y + (J 9))),
            (New-Object System.Drawing.PointF ($half + (J 30)), ($y + (J 5)))
        )
        $g.DrawCurve((Pen $color ($w * (0.8 + $rnd.NextDouble() * 0.4))), $pts, 0.5)
    }
    $g.Restore($state)
}

# paper
$g.Clear((C '#fbf6e9'))
for ($i = 0; $i -lt 2600; $i++) {
    $g.FillEllipse((New-Object System.Drawing.SolidBrush (C '#8a7a5c' ($rnd.Next(8, 26)))), $rnd.Next(0, $S), $rnd.Next(0, $S), $rnd.Next(2, 6), $rnd.Next(2, 6))
}

$cx = 440; $cy = 548; $rIn = 258; $rOut = 340

# portal surface: red at the rim, orange, then yellow in the middle, each coloured in at its own angle
Hatch (Annulus $cx $cy 0 $rIn) (C '#f6c21c' 215) 17 -38 19
Hatch (Annulus $cx $cy 95 $rIn) (C '#f08a18' 205) 16 24 19
Hatch (Annulus $cx $cy 172 $rIn) (C '#d8401f' 200) 16 -62 20
Hatch (Annulus $cx $cy 0 80) (C '#fff2a8' 190) 15 60 20

# swirl
foreach ($a0 in 10, 130, 250) {
    $pts = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    for ($r = 58; $r -le 228; $r += 10) {
        $a = ($a0 + $r * 0.95) * [Math]::PI / 180
        $pts.Add((New-Object System.Drawing.PointF ($cx + $r * [Math]::Cos($a) + (J 3)), ($cy + $r * [Math]::Sin($a) + (J 3))))
    }
    $g.DrawCurve((Pen (C '#a8300f' 215) 13), $pts.ToArray(), 0.5)
    $g.DrawCurve((Pen (C '#a8300f' 120) 9), $pts.ToArray(), 0.4)
}

# stone ring
Hatch (Annulus $cx $cy $rIn $rOut) (C '#8d949c' 190) 14 48 17
Hatch (Annulus $cx $cy $rIn $rOut) (C '#6c737c' 110) 12 -30 30
Stroke (C '#2f343b' 235) 15 $cx $cy $rOut 0 360 7 3
Stroke (C '#2f343b' 235) 14 $cx $cy $rIn 0 360 6 3
for ($i = 0; $i -lt 11; $i++) {
    $a = ($i * 32.7 + 12 + (J 4)) * [Math]::PI / 180
    $r1 = $rIn + 6; $r2 = $rOut - 6
    $g.DrawLine((Pen (C '#2f343b' 225) 11), $cx + $r1 * [Math]::Cos($a) + (J 3), $cy + $r1 * [Math]::Sin($a) + (J 3), $cx + $r2 * [Math]::Cos($a) + (J 3), $cy + $r2 * [Math]::Sin($a) + (J 3))
}

# a few rays of glow, as a child would draw the sun
foreach ($deg in 100, 128, 156, 184, 212, 240) {
    $a = ($deg + (J 4)) * [Math]::PI / 180
    $r1 = $rOut + 34; $r2 = $rOut + 78 + (J 12)
    $g.DrawLine((Pen (C '#f08a18' 220) 15), $cx + $r1 * [Math]::Cos($a), $cy + $r1 * [Math]::Sin($a), $cx + $r2 * [Math]::Cos($a), $cy + $r2 * [Math]::Sin($a))
}

# signal arcs leaving the portal, in blue pen
foreach ($r in 132, 252, 372, 492) {
    Stroke (C '#fbf6e9' 235) 58 $cx $cy $r -76 -14 5 1     # leave paper showing round the line so it reads over the orange
    Stroke (C '#1668c7' 240) 30 $cx $cy $r -76 -14 6 3
    Stroke (C '#39a9ea' 170) 13 $cx $cy ($r - 5) -72 -18 5 1
}
$dot = Annulus $cx $cy 0 46
$g.FillPath((New-Object System.Drawing.SolidBrush (C '#fbf6e9' 235)), (Annulus $cx $cy 0 60))
Hatch $dot (C '#1668c7' 240) 16 35 13
Stroke (C '#1668c7' 240) 12 $cx $cy 44 0 360 3 2

# PC tag
$tx = 650; $ty = 782; $tw = 344; $th = 212
$tag = New-Object System.Drawing.Drawing2D.GraphicsPath
$tag.AddRectangle((New-Object System.Drawing.RectangleF $tx, $ty, $tw, $th))
$g.FillPath((New-Object System.Drawing.SolidBrush (C '#fbf6e9')), $tag)
Hatch $tag (C '#ffe27a' 200) 18 -20 21
for ($k = 0; $k -lt 3; $k++) {
    [System.Drawing.PointF[]]$box = @(
        (New-Object System.Drawing.PointF ($tx + (J 7)), ($ty + (J 7))),
        (New-Object System.Drawing.PointF ($tx + $tw + (J 7)), ($ty + (J 7))),
        (New-Object System.Drawing.PointF ($tx + $tw + (J 7)), ($ty + $th + (J 7))),
        (New-Object System.Drawing.PointF ($tx + (J 7)), ($ty + $th + (J 7)))
    )
    $g.DrawClosedCurve((Pen (C '#1668c7' 235) 15), $box, [single]0.08, [System.Drawing.Drawing2D.FillMode]::Alternate)
}
# "PC" in the angular, straight-stroke letterforms of Valheim's Norse lettering, drawn stroke by stroke
$letters = @(
    @(@(714, 806), @(712, 968)),                                                   # P: stem
    @(@(712, 814), @(804, 850), @(714, 898)),                                      # P: pointed bowl
    @(@(934, 826), @(884, 810), @(850, 852), @(852, 924), @(884, 966), @(934, 950)) # C: faceted, no curve
)
foreach ($pass in @(@(24, 240, '#d81414'), @(20, 210, '#d81414'), @(10, 150, '#ff5a3c'))) {
    foreach ($letter in $letters) {
        [System.Drawing.PointF[]]$pts = $letter | ForEach-Object { New-Object System.Drawing.PointF ($_[0] + (J 4)), ($_[1] + (J 4)) }
        $g.DrawLines((Pen (C $pass[2] $pass[1]) $pass[0]), $pts)
    }
}

$small = New-Object System.Drawing.Bitmap 256, 256
$g2 = [System.Drawing.Graphics]::FromImage($small)
$g2.InterpolationMode = 'HighQualityBicubic'
$g2.PixelOffsetMode = 'HighQuality'
$g2.DrawImage($bmp, 0, 0, 256, 256)
$small.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $g2.Dispose(); $bmp.Dispose(); $small.Dispose()
"saved $Out"
