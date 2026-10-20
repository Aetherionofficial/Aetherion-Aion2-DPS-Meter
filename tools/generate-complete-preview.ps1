$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$W=2400; $H=1280
$bmp=[System.Drawing.Bitmap]::new($W,$H); $g=[System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode='AntiAlias'; $g.TextRenderingHint='AntiAliasGridFit'; $g.InterpolationMode='HighQualityBicubic'
function B($c){[System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($c))}
function F($s,$weight='Regular'){[System.Drawing.Font]::new('Segoe UI',$s,[System.Drawing.FontStyle]::$weight,[System.Drawing.GraphicsUnit]::Pixel)}
function T($t,$x,$y,$s,$c='#EDF2F5',$weight='Regular'){$g.DrawString($t,(F $s $weight),(B $c),[single]$x,[single]$y)}
function FillRect($x,$y,$w,$h,$c){$g.FillRectangle((B $c),$x,$y,$w,$h)}
FillRect 0 0 $W $H '#0C141B'; FillRect 0 0 $W 5 '#D4AD62'
T 'AETHERION  /  AION2 HUB' 52 32 17 '#E7BE68' 'Bold'; T 'DPS im Blick. Karte, News und Guides direkt dabei.' 52 64 32 '#F1F4F5' 'Bold'; T 'KAMPF-OVERLAY  ·  QUESTLOG.GG-KARTE  ·  OFFIZIELLE AION2 NEWS  ·  WAKAYASHI.GG' 53 112 14 '#6ACAB4' 'Bold'
# Existing full-resolution concept portions: Questlog + real NC news and separate Wakayashi window.
$source=[System.Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot '..\dist\aetherion-aion2-hub-news-preview.png'))
$g.DrawImage($source,[System.Drawing.Rectangle]::new(665,186,1115,820),[System.Drawing.Rectangle]::new(44,164,1115,820),[System.Drawing.GraphicsUnit]::Pixel)
$g.DrawImage($source,[System.Drawing.Rectangle]::new(1810,270,562,672),[System.Drawing.Rectangle]::new(1190,284,562,672),[System.Drawing.GraphicsUnit]::Pixel)
$source.Dispose()
# DPS overlay window, shown as a distinct floating game overlay.
FillRect 35 214 600 850 '#28343B'; FillRect 39 218 592 842 '#0D141B'; FillRect 39 218 592 58 '#17232B'
T 'AETHERION  /  LIVE-KAMPF' 61 230 13 '#E7BE68' 'Bold'; T 'VERBLASSTER PAPIS' 61 252 19 '#F1F4F5' 'Bold'; T 'BEISPIELKAMPF' 492 237 10 '#6ACAB4' 'Bold'
FillRect 58 291 266 86 '#14212A'; FillRect 336 291 277 86 '#10221F'; T 'GRUPPEN-DPS' 77 306 11 '#98ADB5' 'Bold'; T '15.33K/s' 77 327 27 '#F1F4F5' 'Bold'; T 'GRUPPEN-HPS' 354 306 11 '#74C8B4' 'Bold'; T '1.12K/s' 354 327 27 '#F1F4F5' 'Bold'
T 'RANG  ·  SPIELER' 64 400 11 '#91A4AD' 'Bold'; T 'DPS' 474 400 10 '#91A4AD' 'Bold'; T 'ANTEIL' 542 400 10 '#91A4AD' 'Bold'
$rows=@(@('01','Zerovx','GLADIATOR · DU','5.82K/s','38%'),@('02','Aelyra','CLERIC · HEILER','3.22K/s','21%'),@('03','Noxx','ASSASSINE','3.99K/s','26%'),@('04','Mirea','SORCERER','2.30K/s','15%'))
$yy=423; foreach($row in $rows){ FillRect 58 $yy 555 88 '#15212A'; FillRect 72 ($yy+18) 42 42 '#354A55'; T $row[0] 79 ($yy+28) 15 '#F0C66F' 'Bold'; T $row[1] 128 ($yy+13) 16 '#F1F4F5' 'Bold'; T $row[2] 128 ($yy+39) 10 '#9BAEB6' 'Bold'; T $row[3] 465 ($yy+26) 14 '#F1F4F5' 'Bold'; T $row[4] 548 ($yy+27) 12 '#B7C5CB'; FillRect 128 ($yy+66) 404 4 '#2C3A42'; $bar=if($row[0] -eq '01'){270}else{150}; FillRect 128 ($yy+66) $bar 4 '#E7BE68'; $yy+=98 }
FillRect 58 834 555 102 '#13212A'; T 'KAMPF-HIGHLIGHTS' 76 849 11 '#E7BE68' 'Bold'; T 'Stärkste Sekunde' 76 873 12 '#AFC0C7'; T '18.4K/s' 76 896 18 '#F1F4F5' 'Bold'; T 'Live-Rangliste · DPS/HPS · Vergleich' 308 875 12 '#E0E7E9'; T 'Drag & Drop · Transparenz · kompakt skalierbar' 76 954 11 '#9CAFB7'
FillRect 58 980 150 42 '#1B2A32'; FillRect 220 980 125 42 '#1B2A32'; FillRect 357 980 237 42 '#302A20'; T 'KAMPFDETAILS' 73 994 11 '#C7D2D6' 'Bold'; T 'VERGLEICH' 242 994 11 '#C7D2D6' 'Bold'; T 'DPS / HPS WECHSELN' 378 994 11 '#F0C66F' 'Bold'
T 'VORSCHAU  ·  Meterwerte sind Beispieldaten; die Questlog-Karte lädt live, AION2-News zeigen offizielle NC-Patchnotes.' 53 1196 14 '#A8B7BD'
$out=[System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\dist\aetherion-komplett-preview.png')); $bmp.Save($out,[System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose(); Write-Output $out
