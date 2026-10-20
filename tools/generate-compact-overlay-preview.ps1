$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$scale = 2
$w = 760; $h = 500
$bmp = [Drawing.Bitmap]::new($w,$h)
$g = [Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'
function Brush($c) { [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($c)) }
function Font($s,$style='Regular') { [Drawing.Font]::new('Segoe UI',[single]$s,[Drawing.FontStyle]::$style,[Drawing.GraphicsUnit]::Pixel) }
function Text($t,$x,$y,$s,$c='#F1F4F5',$style='Regular') { $g.DrawString($t,(Font $s $style),(Brush $c),[single]$x,[single]$y) }
function Rect($x,$y,$ww,$hh,$c) { $g.FillRectangle((Brush $c),$x,$y,$ww,$hh) }
Rect 0 0 $w $h '#0A0B0D'
for($i=0;$i -lt 12;$i++){ Rect (20+$i*70) (60+($i%3)*80) 44 2 '#241814' }
Rect 28 28 704 444 '#09090B'; Rect 28 28 704 444 '#D10B0B0D'
Text 'DPS-Statistiken' 48 46 20 '#F28A45' 'Bold'; Text '01:58' 320 47 18 '#F0F1F2' 'Bold'
Text ([string][char]0x21BB) 596 43 19 '#DDE7EA' 'Bold'; Text ([string][char]0x2699) 634 43 18 '#F28A45' 'Bold'; Text ([string][char]0x00D7) 680 41 22 '#DDE7EA' 'Bold'
Text ' ' 48 82 12 '#A7A9AE' 'Bold'
$rows = @(@('1','CunIs','114.9K','22,049/s','49.28%','#C85D2A'),@('2','Cri30000','98.1K','26,022/s','41.95%','#9B4525'),@('3','Sceptix','90.0K','29,888/s','8.76%','#74351F'),@('4','Aquaria','75.7K','6,500/s','0.15%','#4D2A22'))
$y=100
foreach($r in $rows){
    Rect 42 $y 676 54 '#20191A'; Rect 42 $y (260 - ($r[0]-1)*38) 54 $r[5]
    Text $r[0] 54 ($y+17) 15 '#E9EEF0' 'Bold'; Text $r[1] 96 ($y+9) 17 '#FFFFFF' 'Bold'; Text ([string][char]0x2726) 96 ($y+31) 11 '#DDE7EA'; Text $r[2] 108 ($y+31) 12 '#DDE7EA'
    Text $r[3] 514 ($y+13) 17 '#F1F4F5' 'Bold'; Text $r[4] 672 ($y+17) 15 '#F28A45' 'Bold'
    $y += 62
}
Text '0 (+815)' 52 382 13 '#D4DDE0'; Text '23' 270 382 13 '#D4DDE0'; Text 'LIVE' 652 382 12 '#F28A45' 'Bold'
Rect 42 416 244 34 '#201719'; Rect 294 416 130 34 '#201719'
Text 'Kampfdetails' 54 425 13 '#E2E9EC'; Text 'Vergleich' 310 425 13 '#E2E9EC'
$out = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\preview\compact-overlay-top-controls-preview.png'))
$bmp.Save($out,[Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose(); $out
