# 원소 마법사 로그라이트 - 프로토타입 아트 생성 스크립트
# GDI+로 벡터풍 스프라이트와 아이콘을 그려 Assets/_Project/Art 아래에 PNG로 저장한다.
# 실행: powershell -ExecutionPolicy Bypass -File Tools/ArtGen/generate_art.ps1
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$spr = Join-Path $root 'Assets\_Project\Art\Sprites'
$ico = Join-Path $root 'Assets\_Project\Art\Icons'
New-Item -ItemType Directory -Force $spr | Out-Null
New-Item -ItemType Directory -Force $ico | Out-Null

# ---------------------------------------------------------------- 공용 함수
function New-Canvas([int]$w, [int]$h) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    return @{ B = $bmp; G = $g }
}
function Save-Canvas($c, [string]$dir, [string]$name) {
    $c.G.Dispose()
    $c.B.Save((Join-Path $dir "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $c.B.Dispose()
}
function Col([int]$a, [int]$r, [int]$g, [int]$b) { return [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }
function Pts([double[]]$v) {
    $list = New-Object 'System.Collections.Generic.List[System.Drawing.PointF]'
    for ($i = 0; $i -lt $v.Length; $i += 2) { $list.Add((New-Object System.Drawing.PointF([single]$v[$i], [single]$v[$i + 1]))) }
    return , $list.ToArray()
}
function New-Pen($col, [double]$w) {
    $p = New-Object System.Drawing.Pen($col, [single]$w)
    $p.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $p.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $p.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    return $p
}
function Fill-Circle($g, $col, [double]$cx, [double]$cy, [double]$r) {
    $b = New-Object System.Drawing.SolidBrush($col)
    $g.FillEllipse($b, [single]($cx - $r), [single]($cy - $r), [single](2 * $r), [single](2 * $r)); $b.Dispose()
}
function Fill-Ellipse($g, $col, [double]$cx, [double]$cy, [double]$rx, [double]$ry) {
    $b = New-Object System.Drawing.SolidBrush($col)
    $g.FillEllipse($b, [single]($cx - $rx), [single]($cy - $ry), [single](2 * $rx), [single](2 * $ry)); $b.Dispose()
}
function Stroke-Circle($g, $col, [double]$w, [double]$cx, [double]$cy, [double]$r) {
    $p = New-Pen $col $w
    $g.DrawEllipse($p, [single]($cx - $r), [single]($cy - $r), [single](2 * $r), [single](2 * $r)); $p.Dispose()
}
function Fill-Poly($g, $col, [double[]]$v) {
    $b = New-Object System.Drawing.SolidBrush($col); $g.FillPolygon($b, (Pts $v)); $b.Dispose()
}
function Stroke-Poly($g, $col, [double]$w, [double[]]$v) {
    $p = New-Pen $col $w; $g.DrawPolygon($p, (Pts $v)); $p.Dispose()
}
function Fill-Curve($g, $col, [double[]]$v, [double]$tension = 0.5) {
    $b = New-Object System.Drawing.SolidBrush($col)
    $g.FillClosedCurve($b, (Pts $v), [System.Drawing.Drawing2D.FillMode]::Winding, [single]$tension); $b.Dispose()
}
function Stroke-Curve($g, $col, [double]$w, [double[]]$v, [double]$tension = 0.5) {
    $p = New-Pen $col $w; $g.DrawClosedCurve($p, (Pts $v), [single]$tension, [System.Drawing.Drawing2D.FillMode]::Winding); $p.Dispose()
}
function Draw-Line($g, $col, [double]$w, [double]$x1, [double]$y1, [double]$x2, [double]$y2) {
    $p = New-Pen $col $w; $g.DrawLine($p, [single]$x1, [single]$y1, [single]$x2, [single]$y2); $p.Dispose()
}
function Draw-OpenCurve($g, $col, [double]$w, [double[]]$v, [double]$tension = 0.5) {
    $p = New-Pen $col $w; $g.DrawCurve($p, (Pts $v), [single]$tension); $p.Dispose()
}
# 중심이 밝고 가장자리가 투명한 원형 빛
function Fill-Glow($g, [double]$cx, [double]$cy, [double]$r, $center, $edge) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddEllipse([single]($cx - $r), [single]($cy - $r), [single](2 * $r), [single](2 * $r))
    $b = New-Object System.Drawing.Drawing2D.PathGradientBrush($path)
    $b.CenterColor = $center
    $b.SurroundColors = [System.Drawing.Color[]]@($edge)
    $g.FillPath($b, $path); $b.Dispose(); $path.Dispose()
}
function RoundRect-Path([double]$x, [double]$y, [double]$w, [double]$h, [double]$r) {
    $d = [single](2 * $r)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc([single]$x, [single]$y, $d, $d, 180, 90)
    $path.AddArc([single]($x + $w - 2 * $r), [single]$y, $d, $d, 270, 90)
    $path.AddArc([single]($x + $w - 2 * $r), [single]($y + $h - 2 * $r), $d, $d, 0, 90)
    $path.AddArc([single]$x, [single]($y + $h - 2 * $r), $d, $d, 90, 90)
    $path.CloseFigure()
    return , $path
}
function Fill-RoundRect($g, $col, [double]$x, [double]$y, [double]$w, [double]$h, [double]$r) {
    $path = RoundRect-Path $x $y $w $h $r
    $b = New-Object System.Drawing.SolidBrush($col); $g.FillPath($b, $path); $b.Dispose(); $path.Dispose()
}
function Stroke-RoundRect($g, $col, [double]$pw, [double]$x, [double]$y, [double]$w, [double]$h, [double]$r) {
    $path = RoundRect-Path $x $y $w $h $r
    $p = New-Pen $col $pw; $g.DrawPath($p, $path); $p.Dispose(); $path.Dispose()
}
# 원점에서 +X 방향으로 뻗는 물방울/불꽃 모양
function Fill-Teardrop($g, $col, [double]$len, [double]$wid) {
    Fill-Curve $g $col @(0, 0, ($len * 0.22), (-$wid / 2), ($len * 0.6), (-$wid * 0.28), $len, 0, ($len * 0.6), ($wid * 0.28), ($len * 0.22), ($wid / 2)) 0.45
}
$script:gfxStack = New-Object System.Collections.Stack
function Push($g, [double]$x, [double]$y, [double]$deg = 0, [double]$scale = 1) {
    $script:gfxStack.Push($g.Save())
    $g.TranslateTransform([single]$x, [single]$y)
    if ($deg -ne 0) { $g.RotateTransform([single]$deg) }
    if ($scale -ne 1) { $g.ScaleTransform([single]$scale, [single]$scale) }
}
function Pop($g) { $g.Restore($script:gfxStack.Pop()) }
function Star-Points([double]$cx, [double]$cy, [int]$n, [double]$rOut, [double]$rIn, [double]$rot = 0, [double]$skew = 0) {
    $v = New-Object 'System.Collections.Generic.List[double]'
    for ($i = 0; $i -lt $n * 2; $i++) {
        $outer = ($i % 2 -eq 0)
        $r = if ($outer) { $rOut } else { $rIn }
        $a = ($rot + $i * 180.0 / $n + $(if ($outer) { $skew } else { 0 })) * [Math]::PI / 180
        $v.Add($cx + [Math]::Cos($a) * $r); $v.Add($cy + [Math]::Sin($a) * $r)
    }
    return , $v.ToArray()
}

$white = Col 255 255 255 255
$outline = Col 255 30 24 48

# ---------------------------------------------------------------- 틴트용 기본 도형
$c = New-Canvas 128 128; Fill-Circle $c.G $white 64 64 62; Save-Canvas $c $spr 'circle'
$c = New-Canvas 256 256; Fill-Glow $c.G 128 128 126 (Col 255 255 255 255) (Col 0 255 255 255); Save-Canvas $c $spr 'soft_circle'
$c = New-Canvas 256 256
Stroke-Circle $c.G (Col 70 255 255 255) 22 128 128 112
Stroke-Circle $c.G (Col 255 255 255 255) 10 128 128 116
Save-Canvas $c $spr 'ring'
$c = New-Canvas 16 16; $c.G.Clear($white); Save-Canvas $c $spr 'square'

$c = New-Canvas 256 256
Fill-Poly $c.G (Col 150 255 255 255) (Star-Points 128 128 14 126 78 7)
Fill-Poly $c.G (Col 255 255 255 255) (Star-Points 128 128 14 108 66 0)
Fill-Circle $c.G $white 128 128 70
Save-Canvas $c $spr 'burst'

$c = New-Canvas 96 96; Fill-RoundRect $c.G $white 1 1 94 94 28; Save-Canvas $c $spr 'ui_rounded'
$c = New-Canvas 96 96; Stroke-RoundRect $c.G $white 5 4 4 88 88 25; Save-Canvas $c $spr 'ui_outline'

# ---------------------------------------------------------------- 플레이어 (마법사)
$c = New-Canvas 256 256; $g = $c.G
Fill-Ellipse $g (Col 70 0 0 0) 128 232 70 16
# 지팡이
Draw-Line $g $outline 15 206 86 194 232
Draw-Line $g (Col 255 150 100 60) 9 206 86 194 232
Fill-Glow $g 207 70 34 (Col 230 140 240 255) (Col 0 80 200 255)
Fill-Circle $g $outline 207 70 19
Fill-Circle $g (Col 255 110 225 255) 207 70 15
Fill-Circle $g (Col 255 235 252 255) 202 65 6
# 로브
$robe = @(128, 108, 84, 132, 62, 222, 128, 236, 194, 222, 172, 132)
Fill-Curve $g (Col 255 104 72 196) $robe 0.35
Stroke-Curve $g $outline 7 $robe 0.35
Fill-Curve $g (Col 255 138 104 228) @(128, 122, 110, 150, 104, 222, 128, 228, 152, 222, 146, 150) 0.35
Fill-RoundRect $g (Col 255 250 200 80) 92 176 72 14 6
Fill-Circle $g (Col 255 255 236 150) 128 183 9
# 얼굴
Fill-Circle $g $outline 128 108 44
Fill-Circle $g (Col 255 255 226 194) 128 108 39
Fill-Ellipse $g (Col 255 40 30 60) 113 112 5.5 8
Fill-Ellipse $g (Col 255 40 30 60) 143 112 5.5 8
Fill-Circle $g $white 115 108 2.4
Fill-Circle $g $white 145 108 2.4
Fill-Ellipse $g (Col 110 255 130 130) 101 124 8 5
Fill-Ellipse $g (Col 110 255 130 130) 155 124 8 5
# 모자
$hat = @(134, 8, 96, 60, 74, 92, 128, 100, 182, 92, 160, 56)
Fill-Curve $g (Col 255 84 56 170) $hat 0.3
Stroke-Curve $g $outline 7 $hat 0.3
Fill-Ellipse $g $outline 128 90 78 19
Fill-Ellipse $g (Col 255 104 72 196) 128 88 73 15
Fill-RoundRect $g (Col 255 250 200 80) 84 70 88 13 6
Fill-Poly $g (Col 255 255 240 160) (Star-Points 128 44 5 12 5 -90)
Save-Canvas $c $spr 'player'

# ---------------------------------------------------------------- 적
# 슬라임
$c = New-Canvas 256 256; $g = $c.G
Fill-Ellipse $g (Col 70 0 0 0) 128 222 86 16
$body = @(128, 52, 62, 96, 34, 190, 70, 222, 186, 222, 222, 190, 194, 96)
Fill-Curve $g (Col 255 108 204 96) $body 0.45
Stroke-Curve $g (Col 255 30 84 44) 8 $body 0.45
Fill-Curve $g (Col 255 150 232 132) @(120, 70, 78, 104, 62, 160, 96, 150, 126, 100) 0.5
Fill-Ellipse $g $white 80 96 12 8
Fill-Circle $g $white 100 150 19; Fill-Circle $g $white 158 150 19
Fill-Circle $g (Col 255 26 44 30) 104 154 10; Fill-Circle $g (Col 255 26 44 30) 154 154 10
Draw-OpenCurve $g (Col 255 30 84 44) 6 @(112, 186, 128, 196, 146, 186)
Save-Canvas $c $spr 'slime'

# 박쥐
$c = New-Canvas 256 256; $g = $c.G
Fill-Ellipse $g (Col 60 0 0 0) 128 232 60 12
$wingL = @(112, 120, 60, 62, 8, 84, 22, 122, 12, 160, 50, 150, 64, 182, 92, 160, 112, 168)
$wingR = @(144, 120, 196, 62, 248, 84, 234, 122, 244, 160, 206, 150, 192, 182, 164, 160, 144, 168)
Fill-Poly $g (Col 255 96 60 150) $wingL; Stroke-Poly $g $outline 7 $wingL
Fill-Poly $g (Col 255 96 60 150) $wingR; Stroke-Poly $g $outline 7 $wingR
Fill-Poly $g $outline @(92, 100, 84, 46, 124, 82); Fill-Poly $g $outline @(164, 100, 172, 46, 132, 82)
Fill-Poly $g (Col 255 150 100 200) @(98, 94, 92, 62, 116, 84); Fill-Poly $g (Col 255 150 100 200) @(158, 94, 164, 62, 140, 84)
Fill-Circle $g $outline 128 136 54
Fill-Circle $g (Col 255 128 84 186) 128 136 48
Fill-Ellipse $g (Col 255 160 118 214) 128 120 30 18
Fill-Ellipse $g (Col 255 255 226 90) 108 132 11 13; Fill-Ellipse $g (Col 255 255 226 90) 148 132 11 13
Fill-Ellipse $g (Col 255 40 20 50) 110 134 4.5 8; Fill-Ellipse $g (Col 255 40 20 50) 146 134 4.5 8
Fill-Poly $g $white @(114, 158, 122, 158, 118, 174); Fill-Poly $g $white @(134, 158, 142, 158, 138, 174)
Save-Canvas $c $spr 'bat'

# 골렘
$c = New-Canvas 256 256; $g = $c.G
Fill-Ellipse $g (Col 70 0 0 0) 128 234 92 16
Fill-RoundRect $g $outline 14 104 58 96 20; Fill-RoundRect $g (Col 255 132 120 112) 20 110 46 84 16
Fill-RoundRect $g $outline 184 104 58 96 20; Fill-RoundRect $g (Col 255 132 120 112) 190 110 46 84 16
Fill-RoundRect $g $outline 52 70 152 162 34; Fill-RoundRect $g (Col 255 152 140 130) 59 77 138 148 28
Fill-RoundRect $g (Col 255 176 164 152) 68 84 120 44 20
Fill-RoundRect $g $outline 78 22 100 78 24; Fill-RoundRect $g (Col 255 166 154 142) 85 29 86 64 18
Fill-Glow $g 108 62 20 (Col 255 255 200 90) (Col 0 255 120 20); Fill-Glow $g 148 62 20 (Col 255 255 200 90) (Col 0 255 120 20)
Fill-RoundRect $g (Col 255 255 170 50) 98 55 20 13 4; Fill-RoundRect $g (Col 255 255 170 50) 138 55 20 13 4
Draw-OpenCurve $g (Col 255 92 82 76) 5 @(92, 140, 112, 158, 104, 180, 122, 204) 0.2
Draw-OpenCurve $g (Col 255 92 82 76) 5 @(170, 128, 154, 150, 166, 168) 0.2
Fill-Circle $g (Col 255 110 190 110) 150 196 9; Fill-Circle $g (Col 255 110 190 110) 78 132 6
Save-Canvas $c $spr 'golem'

# 보스: 심연의 군주
$c = New-Canvas 384 384; $g = $c.G
Fill-Glow $g 192 200 188 (Col 150 150 30 190) (Col 0 90 10 130)
Fill-Ellipse $g (Col 80 0 0 0) 192 346 120 20
$hornL = @(118, 120, 70, 96, 40, 34, 96, 62, 150, 96)
$hornR = @(266, 120, 314, 96, 344, 34, 288, 62, 234, 96)
Fill-Curve $g (Col 255 236 224 200) $hornL 0.3; Stroke-Curve $g $outline 8 $hornL 0.3
Fill-Curve $g (Col 255 236 224 200) $hornR 0.3; Stroke-Curve $g $outline 8 $hornR 0.3
$bb = @(192, 70, 96, 112, 60, 230, 96, 322, 192, 344, 288, 322, 324, 230, 288, 112)
Fill-Curve $g (Col 255 112 26 58) $bb 0.45
Stroke-Curve $g $outline 10 $bb 0.45
Fill-Curve $g (Col 255 150 40 76) @(192, 92, 122, 124, 100, 210, 150, 190, 200, 130) 0.5
# 눈
Fill-Glow $g 140 190 46 (Col 255 255 220 90) (Col 0 255 120 20); Fill-Glow $g 244 190 46 (Col 255 255 220 90) (Col 0 255 120 20)
Fill-Poly $g $outline @(104, 166, 172, 186, 164, 214, 112, 204)
Fill-Poly $g (Col 255 255 226 80) @(112, 174, 164, 190, 158, 208, 118, 198)
Fill-Poly $g $outline @(280, 166, 212, 186, 220, 214, 272, 204)
Fill-Poly $g (Col 255 255 226 80) @(272, 174, 220, 190, 226, 208, 266, 198)
Fill-Ellipse $g (Col 255 60 10 20) 142 192 6 11; Fill-Ellipse $g (Col 255 60 10 20) 242 192 6 11
# 입
Fill-Curve $g (Col 255 40 8 24) @(126, 252, 192, 244, 258, 252, 240, 300, 192, 312, 144, 300) 0.4
for ($i = 0; $i -lt 6; $i++) {
    $x = 136 + $i * 22
    Fill-Poly $g (Col 255 244 236 220) @($x, 252, ($x + 20), 252, ($x + 10), 276)
    Fill-Poly $g (Col 255 244 236 220) @(($x + 2), 304, ($x + 18), 304, ($x + 10), 284)
}
# 왕관
Fill-Poly $g $outline @(144, 78, 150, 34, 172, 58, 192, 22, 212, 58, 234, 34, 240, 78)
Fill-Poly $g (Col 255 250 200 70) @(151, 73, 155, 46, 172, 66, 192, 34, 212, 66, 229, 46, 233, 73)
Fill-Circle $g (Col 255 120 220 255) 192 62 7
Save-Canvas $c $spr 'boss'

# 경험치 오브
$c = New-Canvas 64 64; $g = $c.G
Fill-Glow $g 32 32 31 (Col 170 120 255 220) (Col 0 60 220 180)
Fill-Poly $g (Col 255 16 92 84) @(32, 6, 54, 32, 32, 58, 10, 32)
Fill-Poly $g (Col 255 60 220 180) @(32, 11, 49, 32, 32, 53, 15, 32)
Fill-Poly $g (Col 255 170 255 232) @(32, 11, 49, 32, 32, 32, 15, 32)
Fill-Poly $g $white @(32, 15, 38, 26, 32, 28, 27, 26)
Save-Canvas $c $spr 'xp_orb'

# 중급 경험치 오브 (파란 보석, 면이 더 많다)
$c = New-Canvas 96 96; $g = $c.G
Fill-Glow $g 48 48 47 (Col 190 120 170 255) (Col 0 40 90 255)
Fill-Poly $g (Col 255 18 42 120) @(48, 8, 80, 34, 70, 78, 26, 78, 16, 34)
Fill-Poly $g (Col 255 60 120 245) @(48, 14, 74, 36, 66, 72, 30, 72, 22, 36)
Fill-Poly $g (Col 255 140 190 255) @(48, 14, 74, 36, 48, 44, 22, 36)
Fill-Poly $g (Col 255 40 90 210) @(48, 44, 66, 72, 30, 72)
Fill-Poly $g $white @(42, 22, 52, 22, 56, 30, 38, 30)
Fill-Circle $g (Col 230 255 255 255) 64 28 3
Save-Canvas $c $spr 'xp_orb_mid'

# 대급 경험치 오브 (보라 결정 + 금테, 반짝임)
$c = New-Canvas 128 128; $g = $c.G
Fill-Glow $g 64 64 63 (Col 210 210 140 255) (Col 0 120 40 200)
Fill-Poly $g (Col 255 255 200 80) @(64, 6, 102, 44, 90, 104, 38, 104, 26, 44)
Fill-Poly $g (Col 255 70 20 110) @(64, 12, 96, 46, 85, 98, 43, 98, 32, 46)
Fill-Poly $g (Col 255 165 80 235) @(64, 18, 90, 47, 80, 92, 48, 92, 38, 47)
Fill-Poly $g (Col 255 220 170 255) @(64, 18, 90, 47, 64, 58, 38, 47)
Fill-Poly $g (Col 255 120 50 200) @(64, 58, 80, 92, 48, 92)
Fill-Poly $g $white @(57, 28, 70, 28, 76, 40, 50, 40)
Fill-Poly $g (Col 255 255 255 255) (Star-Points 98 22 4 13 3)
Fill-Poly $g (Col 230 255 240 200) (Star-Points 28 86 4 9 2)
Save-Canvas $c $spr 'xp_orb_large'

# 자석 (빨간 말굽 자석 + 은색 끝)
$c = New-Canvas 128 128; $g = $c.G
Fill-Glow $g 64 64 63 (Col 150 255 220 120) (Col 0 255 160 60)
$pen = New-Pen (Col 255 120 20 30) 34
$g.DrawArc($pen, 26, 30, 76, 76, 0, 180); $pen.Dispose()
Fill-RoundRect $g (Col 255 120 20 30) 9 28 34 42 6
Fill-RoundRect $g (Col 255 120 20 30) 85 28 34 42 6
$pen = New-Pen (Col 255 235 60 55) 24
$g.DrawArc($pen, 26, 30, 76, 76, 0, 180); $pen.Dispose()
Fill-RoundRect $g (Col 255 235 60 55) 14 30 24 40 5
Fill-RoundRect $g (Col 255 235 60 55) 90 30 24 40 5
Fill-RoundRect $g (Col 255 120 20 30) 9 14 34 22 5
Fill-RoundRect $g (Col 255 120 20 30) 85 14 34 22 5
Fill-RoundRect $g (Col 255 225 230 240) 13 17 26 16 4
Fill-RoundRect $g (Col 255 225 230 240) 89 17 26 16 4
$pen = New-Pen (Col 200 255 160 150) 5
$g.DrawArc($pen, 34, 38, 60, 60, 20, 60); $pen.Dispose()
Save-Canvas $c $spr 'magnet'

# 바닥 오브 방향 화살표 (오른쪽을 가리킨다, 흰색 틴트용)
$c = New-Canvas 96 96; $g = $c.G
Fill-Poly $g (Col 255 30 20 50) @(90, 48, 18, 10, 34, 48, 18, 86)
Fill-Poly $g $white @(80, 48, 24, 18, 38, 48, 24, 78)
Save-Canvas $c $spr 'pickup_arrow'

# 바닥 타일 (이어 붙여도 끊기지 않게 가장자리에 걸친 무늬는 반대편에도 그린다)
$c = New-Canvas 512 512; $g = $c.G
$g.Clear((Col 255 40 62 56))
$rnd = New-Object System.Random(7)
function Wrap-Draw($g, [scriptblock]$draw) {
    foreach ($ox in @(-512, 0, 512)) { foreach ($oy in @(-512, 0, 512)) { $g.TranslateTransform($ox, $oy); & $draw; $g.ResetTransform() } }
}
for ($i = 0; $i -lt 26; $i++) {
    $x = $rnd.Next(0, 512); $y = $rnd.Next(0, 512); $rx = $rnd.Next(30, 90); $ry = $rnd.Next(20, 60)
    $dark = ($i % 2 -eq 0)
    $col = if ($dark) { Col 60 26 44 42 } else { Col 46 70 104 84 }
    Wrap-Draw $g { Fill-Ellipse $g $col $x $y $rx $ry }
}
for ($i = 0; $i -lt 46; $i++) {
    $x = $rnd.Next(0, 512); $y = $rnd.Next(0, 512)
    $col = Col 120 92 136 100
    Wrap-Draw $g {
        Draw-Line $g $col 3 $x $y ($x - 5) ($y - 12)
        Draw-Line $g $col 3 $x $y ($x + 1) ($y - 15)
        Draw-Line $g $col 3 $x $y ($x + 6) ($y - 11)
    }
}
for ($i = 0; $i -lt 18; $i++) {
    $x = $rnd.Next(0, 512); $y = $rnd.Next(0, 512); $r = $rnd.Next(3, 7)
    Wrap-Draw $g { Fill-Circle $g (Col 110 70 88 84) $x $y $r; Fill-Circle $g (Col 90 120 140 130) ($x - 1) ($y - 1) ($r * 0.5) }
}
Save-Canvas $c $spr 'ground'

# ---------------------------------------------------------------- 불 스킬
# 화염구 (오른쪽을 향함)
function Draw-Fireball($g) {
    Fill-Glow $g 78 64 62 (Col 160 255 170 40) (Col 0 255 80 0)
    Push $g 104 64 180; Fill-Teardrop $g (Col 255 240 84 16) 100 66; Pop $g
    Push $g 100 64 180; Fill-Teardrop $g (Col 255 255 160 30) 74 48; Pop $g
    Push $g 97 64 180; Fill-Teardrop $g (Col 255 255 236 150) 46 30; Pop $g
}
$c = New-Canvas 128 128; Draw-Fireball $c.G; Save-Canvas $c $spr 'fireball'

# 참격 (오른쪽으로 볼록한 초승달, 틴트용)
function Crescent-Points([double]$cx, [double]$cy, [double]$R, [double]$half, [double]$thick) {
    $v = New-Object 'System.Collections.Generic.List[double]'
    for ($a = - $half; $a -le $half; $a += 5) {
        $rad = $a * [Math]::PI / 180
        $v.Add($cx + [Math]::Cos($rad) * $R); $v.Add($cy + [Math]::Sin($rad) * $R)
    }
    for ($a = $half; $a -ge - $half; $a -= 5) {
        $rad = $a * [Math]::PI / 180
        $t = [Math]::Cos($a / $half * [Math]::PI / 2)
        $r2 = $R - $thick * $t
        $v.Add($cx + [Math]::Cos($rad) * $r2); $v.Add($cy + [Math]::Sin($rad) * $r2)
    }
    return , $v.ToArray()
}
$c = New-Canvas 512 512
Fill-Poly $c.G (Col 90 255 255 255) (Crescent-Points 256 256 250 70 120)
Fill-Poly $c.G (Col 170 255 255 255) (Crescent-Points 256 256 250 68 78)
Fill-Poly $c.G (Col 255 255 255 255) (Crescent-Points 256 256 250 65 38)
Save-Canvas $c $spr 'slash'

# 화염 검기 (세로로 긴 초승달, 틴트용)
$c = New-Canvas 128 256
Fill-Poly $c.G (Col 110 255 255 255) (Crescent-Points -10 128 132 62 62)
Fill-Poly $c.G (Col 255 255 255 255) (Crescent-Points -10 128 132 58 30)
Save-Canvas $c $spr 'sword_wave'

# 화염 고리 (틴트용)
function Draw-FlameRing($g, [double]$cx, [double]$cy, [double]$s) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddPolygon((Pts (Star-Points $cx $cy 18 (126 * $s) (100 * $s) 0 14)))
    $path.AddEllipse([single]($cx - 76 * $s), [single]($cy - 76 * $s), [single](152 * $s), [single](152 * $s))
    $b = New-Object System.Drawing.SolidBrush((Col 235 255 255 255)); $g.FillPath($b, $path); $b.Dispose(); $path.Dispose()
    Stroke-Circle $g (Col 255 255 255 255) (9 * $s) $cx $cy (86 * $s)
    Stroke-Circle $g (Col 90 255 255 255) (14 * $s) $cx $cy (66 * $s)
}
$c = New-Canvas 256 256; Draw-FlameRing $c.G 128 128 1; Save-Canvas $c $spr 'flame_ring'

# 운석 (머리가 아래)
function Draw-Meteor($g, $flameOut, $flameMid, $flameIn, $rock, $rockLight) {
    Fill-Glow $g 96 140 52 (Col 150 $flameMid.R $flameMid.G $flameMid.B) (Col 0 $flameOut.R $flameOut.G $flameOut.B)
    Push $g 96 164 -90; Fill-Teardrop $g $flameOut 160 96; Pop $g
    Push $g 96 160 -90; Fill-Teardrop $g $flameMid 124 70; Pop $g
    Push $g 96 156 -90; Fill-Teardrop $g $flameIn 84 44; Pop $g
    Fill-Circle $g $outline 96 142 37
    Fill-Circle $g $rock 96 142 32
    Fill-Circle $g $rockLight 86 132 14
    Fill-Circle $g (Col 120 20 14 20) 108 150 8; Fill-Circle $g (Col 120 20 14 20) 88 156 5
}
$c = New-Canvas 192 192
Draw-Meteor $c.G (Col 255 240 84 16) (Col 255 255 160 30) (Col 255 255 236 150) (Col 255 104 70 56) (Col 255 150 108 84)
Save-Canvas $c $spr 'meteor'
$c = New-Canvas 192 192
Draw-Meteor $c.G (Col 255 40 110 240) (Col 255 90 180 255) (Col 255 220 244 255) (Col 255 60 72 124) (Col 255 110 130 190)
Save-Canvas $c $spr 'meteor_blue'

# 화염 기둥 (위에서 본 소용돌이 불꽃)
function Draw-Pillar($g, [double]$cx, [double]$cy, [double]$s, $outer, $mid, $inner) {
    Fill-Glow $g $cx $cy (126 * $s) (Col 150 $mid.R $mid.G $mid.B) (Col 0 $outer.R $outer.G $outer.B)
    for ($i = 0; $i -lt 8; $i++) { Push $g $cx $cy ($i * 45) $s; $g.TranslateTransform(18, 14); $g.RotateTransform(24); Fill-Teardrop $g $outer 100 54; Pop $g }
    for ($i = 0; $i -lt 8; $i++) { Push $g $cx $cy ($i * 45 + 22) $s; $g.TranslateTransform(10, 10); $g.RotateTransform(24); Fill-Teardrop $g $mid 72 38; Pop $g }
    for ($i = 0; $i -lt 6; $i++) { Push $g $cx $cy ($i * 60 + 10) $s; $g.TranslateTransform(4, 6); $g.RotateTransform(24); Fill-Teardrop $g $inner 46 24; Pop $g }
    Fill-Glow $g $cx $cy (44 * $s) (Col 255 255 255 255) (Col 0 $inner.R $inner.G $inner.B)
}
$fo = Col 255 236 76 14; $fm = Col 255 255 156 30; $fi = Col 255 255 232 140
$bo = Col 255 36 104 236; $bm = Col 255 86 176 255; $bi = Col 255 214 242 255
$c = New-Canvas 256 256; Draw-Pillar $c.G 128 128 1 $fo $fm $fi; Save-Canvas $c $spr 'pillar'
$c = New-Canvas 256 256; Draw-Pillar $c.G 128 128 1 $bo $bm $bi; Save-Canvas $c $spr 'pillar_blue'

# ---------------------------------------------------------------- 물 스킬
function Draw-Droplet($g) {
    Push $g 60 32 180; Fill-Teardrop $g (Col 255 24 96 190) 58 42; Pop $g
    Push $g 57 32 180; Fill-Teardrop $g (Col 255 84 204 255) 50 34; Pop $g
    Fill-Ellipse $g (Col 230 230 250 255) 46 25 6 4
}
$c = New-Canvas 64 64; Draw-Droplet $c.G; Save-Canvas $c $spr 'droplet'

$c = New-Canvas 128 48; $g = $c.G
$blade = @(126, 24, 70, 8, 6, 24, 70, 40)
Fill-Curve $g (Col 255 24 96 190) $blade 0.25
Fill-Curve $g (Col 255 96 212 255) @(118, 24, 70, 12, 14, 24, 70, 36) 0.25
Fill-Curve $g (Col 255 232 250 255) @(112, 24, 74, 19, 40, 24, 74, 29) 0.25
Save-Canvas $c $spr 'water_blade'

# 수압 물줄기 (왼쪽이 시작점)
$c = New-Canvas 256 64; $g = $c.G
Fill-RoundRect $g (Col 110 60 170 255) 0 4 254 56 28
Fill-RoundRect $g (Col 200 96 206 255) 0 12 250 40 20
Fill-RoundRect $g (Col 235 190 240 255) 0 22 246 20 10
for ($i = 0; $i -lt 5; $i++) { Draw-Line $g (Col 200 255 255 255) 3 (14 + $i * 48) (18 + ($i % 2) * 28) (44 + $i * 48) (18 + ($i % 2) * 28) }
Save-Canvas $c $spr 'jet'

$c = New-Canvas 96 96; $g = $c.G
Fill-Glow $g 48 48 46 (Col 255 235 252 255) (Col 0 90 200 255)
Fill-Poly $g (Col 255 255 255 255) (Crescent-Points 10 48 70 46 22)
Save-Canvas $c $spr 'jet_pulse'

# 격류 몸통 (가로로 늘려 쓰는 띠, 위아래 가장자리가 부드러움, 틴트용)
$c = New-Canvas 64 64; $g = $c.G
$rect = New-Object System.Drawing.RectangleF(0, 0, 64, 64)
$lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $white, $white, [single]90)
$blend = New-Object System.Drawing.Drawing2D.ColorBlend(5)
$blend.Colors = [System.Drawing.Color[]]@((Col 0 255 255 255), (Col 215 255 255 255), (Col 255 255 255 255), (Col 215 255 255 255), (Col 0 255 255 255))
$blend.Positions = [single[]]@(0, 0.16, 0.5, 0.84, 1)
$lg.InterpolationColors = $blend
$g.FillRectangle($lg, $rect); $lg.Dispose()
Save-Canvas $c $spr 'torrent'

$c = New-Canvas 64 64; $g = $c.G
Draw-OpenCurve $g (Col 235 255 255 255) 11 @(14, 8, 46, 32, 14, 56) 0.1
Save-Canvas $c $spr 'chevron'

$c = New-Canvas 16 64; $g = $c.G
$rect = New-Object System.Drawing.RectangleF(0, 0, 16, 64)
$lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, (Col 0 200 240 255), (Col 255 235 250 255), [single]90)
$p = New-Object System.Drawing.Pen($lg, [single]7); $p.StartCap = 'Round'; $p.EndCap = 'Round'
$g.DrawLine($p, [single]8, [single]5, [single]8, [single]58); $p.Dispose(); $lg.Dispose()
Save-Canvas $c $spr 'rain_streak'

# 심해 소용돌이
function Draw-Vortex($g, [double]$cx, [double]$cy, [double]$s) {
    Fill-Glow $g $cx $cy (126 * $s) (Col 190 20 70 150) (Col 0 20 90 190)
    for ($arm = 0; $arm -lt 5; $arm++) {
        $v = New-Object 'System.Collections.Generic.List[double]'
        for ($t = 0; $t -le 1.0001; $t += 0.08) {
            $r = (14 + 108 * $t) * $s
            $a = ($arm * 72 + $t * 250) * [Math]::PI / 180
            $v.Add($cx + [Math]::Cos($a) * $r); $v.Add($cy + [Math]::Sin($a) * $r)
        }
        Draw-OpenCurve $g (Col 150 70 170 255) (15 * $s) $v.ToArray()
        Draw-OpenCurve $g (Col 235 190 240 255) (6 * $s) $v.ToArray()
    }
    Fill-Glow $g $cx $cy (30 * $s) (Col 255 8 24 60) (Col 0 20 60 120)
}
$c = New-Canvas 256 256; Draw-Vortex $c.G 128 128 1; Save-Canvas $c $spr 'vortex'

# ---------------------------------------------------------------- 아이콘
function New-Icon($top, $bottom, $border) {
    $c = New-Canvas 128 128; $g = $c.G
    $path = RoundRect-Path 4 4 120 120 26
    $rect = New-Object System.Drawing.RectangleF(0, 0, 128, 128)
    $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $top, $bottom, [single]90)
    $g.FillPath($lg, $path); $lg.Dispose()
    $p = New-Pen $border 6; $g.DrawPath($p, $path); $p.Dispose()
    $g.SetClip($path); $path.Dispose()
    return $c
}
$fireTop = Col 255 122 36 28; $fireBot = Col 255 58 14 22; $fireBorder = Col 255 255 150 60
$waterTop = Col 255 24 78 140; $waterBot = Col 255 12 30 76; $waterBorder = Col 255 110 210 255
$passTop = Col 255 78 84 108; $passBot = Col 255 36 40 58; $passBorder = Col 255 190 198 220

$c = New-Icon $fireTop $fireBot $fireBorder; Push $c.G 8 8 0 0.875; Draw-Fireball $c.G; Pop $c.G; Save-Canvas $c $ico 'icon_fireball'

$c = New-Icon $fireTop $fireBot $fireBorder; $g = $c.G
Push $g 64 64 -45
Fill-Glow $g 10 0 58 (Col 190 255 150 30) (Col 0 255 80 0)
Fill-Poly $g (Col 255 255 120 20) @(-18, -13, 44, -11, 62, 0, 44, 11, -18, 13)
Fill-Poly $g (Col 255 255 236 170) @(-18, -6, 42, -5, 54, 0, 42, 5, -18, 6)
Fill-RoundRect $g (Col 255 250 200 80) -26 -22 9 44 3
Fill-RoundRect $g (Col 255 120 80 50) -50 -6 26 12 4
Pop $g
Save-Canvas $c $ico 'icon_flame_sword'

$c = New-Icon $fireTop $fireBot $fireBorder; $g = $c.G
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$path.AddPolygon((Pts (Star-Points 64 64 14 50 38 0 14))); $path.AddEllipse(36, 36, 56, 56)
$b = New-Object System.Drawing.SolidBrush((Col 255 255 140 30)); $g.FillPath($b, $path); $b.Dispose(); $path.Dispose()
Stroke-Circle $g (Col 255 255 230 150) 5 64 64 32
Save-Canvas $c $ico 'icon_flame_ring'

$c = New-Icon $fireTop $fireBot $fireBorder; $g = $c.G
Push $g 64 64 -32 0.62; $g.TranslateTransform(-96, -104)
Draw-Meteor $g (Col 255 240 84 16) (Col 255 255 160 30) (Col 255 255 236 150) (Col 255 104 70 56) (Col 255 150 108 84)
Pop $g
Save-Canvas $c $ico 'icon_meteor'

$c = New-Icon $waterTop $waterBot $waterBorder; $g = $c.G
Push $g 18 14 -20 0.9; Draw-Droplet $g; Pop $g
Push $g 48 44 -8 1.0; Draw-Droplet $g; Pop $g
Push $g 14 66 10 0.85; Draw-Droplet $g; Pop $g
Save-Canvas $c $ico 'icon_water_swarm'

$c = New-Icon $waterTop $waterBot $waterBorder; $g = $c.G
Fill-Poly $g (Col 150 70 180 255) @(18, 64, 112, 26, 112, 102)
Fill-Poly $g (Col 255 110 215 255) @(18, 64, 112, 40, 112, 88)
Fill-Poly $g (Col 255 225 248 255) @(18, 64, 112, 54, 112, 74)
Fill-Circle $g (Col 255 235 250 255) 20 64 10
Save-Canvas $c $ico 'icon_water_jet'

$c = New-Icon $waterTop $waterBot $waterBorder; $g = $c.G
foreach ($y in @(38, 64, 90)) {
    Draw-OpenCurve $g (Col 255 60 150 240) 13 @(10, $y, 36, ($y - 12), 64, $y, 92, ($y + 12), 118, $y)
    Draw-OpenCurve $g (Col 255 205 242 255) 5 @(10, $y, 36, ($y - 12), 64, $y, 92, ($y + 12), 118, $y)
}
Fill-Poly $g $white @(96, 50, 116, 64, 96, 78)
Save-Canvas $c $ico 'icon_torrent'

$c = New-Icon $waterTop $waterBot $waterBorder; $g = $c.G
Fill-Circle $g (Col 255 215 228 245) 44 48 19; Fill-Circle $g (Col 255 235 242 252) 66 40 24; Fill-Circle $g (Col 255 215 228 245) 88 50 17
Fill-RoundRect $g (Col 255 225 235 250) 28 46 76 22 11
foreach ($x in @(42, 64, 86)) { Draw-Line $g (Col 255 110 215 255) 6 $x 80 ($x - 7) 104 }
Save-Canvas $c $ico 'icon_rain'

$c = New-Icon $fireTop $fireBot (Col 255 255 200 90); Draw-Pillar $c.G 64 64 0.44 $fo $fm $fi; Save-Canvas $c $ico 'icon_fire_pillar'
$c = New-Icon $waterTop $waterBot (Col 255 150 235 255); Draw-Vortex $c.G 64 64 0.44; Save-Canvas $c $ico 'icon_abyss_vortex'

$c = New-Icon (Col 255 30 60 150) (Col 255 16 20 70) (Col 255 255 214 90); $g = $c.G
Draw-Pillar $g 64 66 0.5 $bo $bm $bi
Fill-Poly $g (Col 255 255 226 110) (Star-Points 100 28 5 15 6.5 -90)
Save-Canvas $c $ico 'icon_great_pillar'

# 패시브
$c = New-Icon $passTop $passBot $passBorder; $g = $c.G
Stroke-Circle $g (Col 255 235 240 255) 6 64 64 16
Stroke-Circle $g (Col 170 235 240 255) 5 64 64 32
Stroke-Circle $g (Col 90 235 240 255) 4 64 64 46
foreach ($a in @(45, 135, 225, 315)) { Push $g 64 64 $a; Fill-Poly $g (Col 255 255 220 120) @(40, -8, 56, 0, 40, 8); Pop $g }
Save-Canvas $c $ico 'icon_area'

$c = New-Icon $passTop $passBot $passBorder; $g = $c.G
Fill-Poly $g (Col 255 255 120 60) (Star-Points 64 64 8 48 24 22)
Fill-Poly $g (Col 255 255 220 130) (Star-Points 64 64 8 30 15 22)
Save-Canvas $c $ico 'icon_damage'

$c = New-Icon $passTop $passBot $passBorder; $g = $c.G
Fill-Circle $g (Col 255 235 240 255) 64 66 40; Fill-Circle $g (Col 255 60 66 92) 64 66 32
Draw-Line $g (Col 255 235 240 255) 6 64 66 64 44; Draw-Line $g (Col 255 140 220 255) 6 64 66 82 74
Fill-RoundRect $g (Col 255 235 240 255) 54 16 20 10 4
Save-Canvas $c $ico 'icon_cooldown'

$c = New-Icon $passTop $passBot $passBorder; $g = $c.G
foreach ($x in @(24, 52, 80)) { Draw-OpenCurve $g (Col 255 150 240 190) 11 @($x, 36, ($x + 24), 64, $x, 92) 0.1 }
Save-Canvas $c $ico 'icon_speed'

# 원소 문장
$c = New-Canvas 128 128; $g = $c.G
Fill-Glow $g 64 70 62 (Col 150 255 140 30) (Col 0 255 60 0)
Push $g 64 118 -90; Fill-Teardrop $g (Col 255 236 76 14) 112 78; Pop $g
Push $g 64 114 -90; Fill-Teardrop $g (Col 255 255 156 30) 84 54; Pop $g
Push $g 64 110 -90; Fill-Teardrop $g (Col 255 255 236 150) 54 32; Pop $g
Save-Canvas $c $ico 'emblem_fire'

$c = New-Canvas 128 128; $g = $c.G
Fill-Glow $g 64 70 62 (Col 140 90 200 255) (Col 0 40 120 255)
Push $g 64 118 -90; Fill-Teardrop $g (Col 255 24 96 190) 110 80; Pop $g
Push $g 64 113 -90; Fill-Teardrop $g (Col 255 84 204 255) 96 66; Pop $g
Fill-Ellipse $g (Col 230 235 250 255) 48 84 9 14
Save-Canvas $c $ico 'emblem_water'

Write-Output ("완료: " + (Get-ChildItem $spr -Filter *.png).Count + " sprites, " + (Get-ChildItem $ico -Filter *.png).Count + " icons")

# ---------------------------------------------------------------- lobby: equipment icons
$eqTop = Col 255 84 70 140; $eqBot = Col 255 38 30 72; $eqBorder = Col 255 196 180 255

$c = New-Icon $eqTop $eqBot $eqBorder; $g = $c.G
Draw-Line $g $outline 15 34 100 84 42
Draw-Line $g (Col 255 160 108 64) 9 34 100 84 42
Fill-Glow $g 90 34 30 (Col 230 140 240 255) (Col 0 80 200 255)
Fill-Circle $g $outline 90 34 17; Fill-Circle $g (Col 255 110 225 255) 90 34 13; Fill-Circle $g $white 86 30 5
Save-Canvas $c $ico 'eq_staff'

$c = New-Icon $eqTop $eqBot $eqBorder; $g = $c.G
Draw-Line $g $outline 14 38 98 74 56
Draw-Line $g (Col 255 120 84 60) 8 38 98 74 56
Fill-Glow $g 86 42 34 (Col 200 255 170 40) (Col 0 255 80 0)
Push $g 74 56 -48; Fill-Teardrop $g (Col 255 240 84 16) 50 34; Pop $g
Push $g 76 54 -48; Fill-Teardrop $g (Col 255 255 200 80) 32 20; Pop $g
Save-Canvas $c $ico 'eq_wand'

$c = New-Icon $eqTop $eqBot $eqBorder; $g = $c.G
Draw-OpenCurve $g $outline 10 @(28, 26, 34, 62, 64, 82, 94, 62, 100, 26) 0.6
Draw-OpenCurve $g (Col 255 250 205 90) 5 @(28, 26, 34, 62, 64, 82, 94, 62, 100, 26) 0.6
Fill-Poly $g $outline @(64, 68, 84, 90, 64, 114, 44, 90)
Fill-Poly $g (Col 255 90 215 255) @(64, 74, 78, 90, 64, 107, 50, 90)
Fill-Poly $g (Col 255 215 248 255) @(64, 74, 78, 90, 64, 90, 50, 90)
Save-Canvas $c $ico 'eq_necklace'

$c = New-Icon $eqTop $eqBot $eqBorder; $g = $c.G
Fill-RoundRect $g $outline 34 26 60 66 22; Fill-Ellipse $g $outline 32 62 16 22
Fill-RoundRect $g $outline 40 84 48 28 8
Fill-RoundRect $g (Col 255 196 140 88) 40 32 48 56 17; Fill-Ellipse $g (Col 255 196 140 88) 33 62 10 16
Fill-RoundRect $g (Col 255 150 100 62) 45 89 38 18 5
Draw-Line $g (Col 255 150 100 62) 4 56 40 56 62; Draw-Line $g (Col 255 150 100 62) 4 72 40 72 62
Save-Canvas $c $ico 'eq_gloves'

$c = New-Icon $eqTop $eqBot $eqBorder; $g = $c.G
$rb = @(64, 22, 40, 34, 26, 106, 64, 114, 102, 106, 88, 34)
Fill-Curve $g (Col 255 124 92 220) $rb 0.3; Stroke-Curve $g $outline 6 $rb 0.3
Fill-Curve $g (Col 255 160 130 245) @(64, 34, 54, 46, 50, 104, 64, 108, 78, 104, 74, 46) 0.3
Fill-RoundRect $g (Col 255 250 200 80) 40 70 48 10 4
Save-Canvas $c $ico 'eq_robe'

$c = New-Icon $eqTop $eqBot $eqBorder; $g = $c.G
$ck = @(64, 24, 30, 40, 16, 108, 44, 98, 64, 112, 84, 98, 112, 108, 98, 40)
Fill-Curve $g (Col 255 60 150 190) $ck 0.3; Stroke-Curve $g $outline 6 $ck 0.3
Fill-Curve $g (Col 255 110 205 230) @(64, 36, 46, 48, 40, 92, 64, 100, 88, 92, 82, 48) 0.3
Fill-Circle $g (Col 255 250 205 90) 64 34 9
Save-Canvas $c $ico 'eq_cloak'

$c = New-Icon $eqTop $eqBot $eqBorder; $g = $c.G
Fill-RoundRect $g $outline 10 46 108 36 10; Fill-RoundRect $g (Col 255 150 100 62) 14 51 100 26 7
Fill-RoundRect $g $outline 44 38 40 52 9; Fill-RoundRect $g (Col 255 250 205 90) 49 43 30 42 6
Fill-RoundRect $g (Col 255 150 100 62) 56 51 16 26 4
Save-Canvas $c $ico 'eq_belt'

$c = New-Icon $eqTop $eqBot $eqBorder; $g = $c.G
$bt = @(40, 20, 76, 20, 76, 70, 104, 82, 108, 104, 40, 104)
Fill-Poly $g (Col 255 176 120 74) $bt; Stroke-Poly $g $outline 7 $bt
Fill-RoundRect $g (Col 255 250 205 90) 38 20 40 12 4
Fill-RoundRect $g (Col 255 110 74 48) 38 98 72 10 3
Save-Canvas $c $ico 'eq_boots'

# ---------------------------------------------------------------- lobby: tab icons (no background)
$c = New-Canvas 128 128; $g = $c.G
$sh = @(64, 12, 22, 28, 26, 78, 64, 116, 102, 78, 106, 28)
Fill-Curve $g (Col 255 236 240 252) $sh 0.25; Stroke-Curve $g $outline 7 $sh 0.25
Fill-Curve $g (Col 255 150 130 240) @(64, 28, 38, 38, 40, 74, 64, 100, 88, 74, 90, 38) 0.25
Fill-Poly $g (Col 255 255 226 110) (Star-Points 64 62 5 17 7.5 -90)
Save-Canvas $c $ico 'tab_equip'

$c = New-Canvas 128 128; $g = $c.G
foreach ($sgn in @(1, -1)) {
    Push $g 64 64 (45 * $sgn)
    Fill-Poly $g $outline @(-9, -56, 0, -66, 9, -56, 9, 22, -9, 22)
    Fill-Poly $g (Col 255 236 242 255) @(-5, -53, 0, -59, 5, -53, 5, 20, -5, 20)
    Fill-RoundRect $g $outline -22 18 44 13 5; Fill-RoundRect $g (Col 255 250 205 90) -19 21 38 7 3
    Fill-RoundRect $g $outline -7 28 14 28 5; Fill-RoundRect $g (Col 255 160 108 64) -4 30 8 23 3
    Pop $g
}
Save-Canvas $c $ico 'tab_battle'

$c = New-Canvas 128 128; $g = $c.G
Fill-RoundRect $g $outline 12 24 104 84 12
Fill-RoundRect $g (Col 255 110 215 255) 18 30 92 72 8
Fill-RoundRect $g (Col 255 244 246 252) 24 34 37 62 5; Fill-RoundRect $g (Col 255 244 246 252) 67 34 37 62 5
foreach ($y in @(46, 58, 70, 82)) { Draw-Line $g (Col 255 150 160 190) 4 31 $y 54 $y; Draw-Line $g (Col 255 150 160 190) 4 74 $y 97 $y }
Save-Canvas $c $ico 'tab_codex'

$c = New-Canvas 128 128; $g = $c.G
$p = New-Pen $outline 22; $g.DrawArc($p, [single]38, [single]14, [single]52, [single]64, [single]180, [single]180); $p.Dispose()
$p = New-Pen (Col 255 210 216 232) 11; $g.DrawArc($p, [single]38, [single]14, [single]52, [single]64, [single]180, [single]180); $p.Dispose()
Fill-RoundRect $g $outline 20 52 88 66 14; Fill-RoundRect $g (Col 255 250 205 90) 26 58 76 54 10
Fill-Circle $g $outline 64 80 9; Fill-RoundRect $g $outline 60 82 8 20 3
Save-Canvas $c $ico 'icon_lock'

Write-Output ("lobby icons done: " + (Get-ChildItem $ico -Filter *.png).Count + " icons")

# 체력 회복 카드 아이콘 (초록 바탕 + 하트 + 십자)
$c = New-Icon (Col 255 52 150 78) (Col 255 20 70 40) (Col 255 150 255 170); $g = $c.G
Fill-Glow $g 64 66 52 (Col 120 200 255 200) (Col 0 120 255 140)
$heart = @(64, 108, 22, 66, 18, 44, 30, 26, 48, 22, 64, 36, 80, 22, 98, 26, 110, 44, 106, 66)
Fill-Curve $g $outline $heart 0.35
Push $g 64 66 0 0.86; Push $g -64 -66
Fill-Curve $g (Col 255 240 80 90) $heart 0.35
Pop $g; Pop $g
Fill-Ellipse $g (Col 200 255 190 190) 44 46 9 6
Fill-RoundRect $g $white 57 50 14 36 4
Fill-RoundRect $g $white 46 61 36 14 4
Save-Canvas $c $ico 'icon_heal'
Write-Output 'heal icon done'
