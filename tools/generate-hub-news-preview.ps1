$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$w=1800; $h=1040
$bmp=[System.Drawing.Bitmap]::new($w,$h)
$g=[System.Drawing.Graphics]::FromImage($bmp); $g.SmoothingMode='AntiAlias'; $g.TextRenderingHint='AntiAliasGridFit'
function Brush($hex){ [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($hex)) }
function Font($size,$style='Regular'){ [System.Drawing.Font]::new('Segoe UI',$size,[System.Drawing.FontStyle]::$style,[System.Drawing.GraphicsUnit]::Pixel) }
function Txt($s,$x,$y,$size,$color='#EDF2F5',$style='Regular'){ $g.DrawString($s,(Font $size $style),(Brush $color),[single]$x,[single]$y) }
function Rect($x,$y,$ww,$hh,$color){ $g.FillRectangle((Brush $color),$x,$y,$ww,$hh) }
Rect 0 0 1800 1040 '#0C141B'; Rect 0 0 1800 5 '#D4AD62'
Txt 'AETHERION  /  AION2 HUB' 56 36 17 '#E7BE68' 'Bold'; Txt 'Karte, Patchnotes und Guides – direkt in der App.' 56 68 31 '#F1F4F5' 'Bold'; Txt 'NEU  ·  QUESTLOG-KARTE + OFFIZIELLE AION2 NEWS' 56 112 14 '#6ACAB4' 'Bold'
# Map and live official news window
Rect 44 164 1115 820 '#1C2831'; Rect 48 168 1107 812 '#0D141B'; Rect 48 168 1107 48 '#17232B'; Txt 'AETHERION' 68 181 16 '#E7BE68' 'Bold'; Txt 'QUESTLOG.GG  ·  AION2 KARTE' 200 181 15 '#F1F4F5' 'Bold'; Txt 'WAKAYASHI.GG ÖFFNET SEPARAT' 787 184 11 '#B7C5CB' 'Bold'
Rect 64 230 700 724 '#101A21'; Rect 64 230 700 37 '#17242C'; Txt 'VERTERON  /  WELT & REGIONEN' 82 240 12 '#E7BE68' 'Bold'; Rect 64 267 132 687 '#13212A'; Txt 'KARTE' 82 285 12 '#6ACAB4' 'Bold'; Txt 'Verteron' 82 324 16 '#F2F3F2' 'Bold'; Txt 'Altgard' 82 363 15 '#B8C5CA'; Txt 'Poeta' 82 400 15 '#B8C5CA'; Txt 'Morheim' 82 437 15 '#B8C5CA'; Txt 'Reshanta' 82 474 15 '#B8C5CA'
$mapShot=[System.Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot '..\dist\questlog-real-map-capture.png'))
$sourceRect=[System.Drawing.Rectangle]::new(0,116,364,440); $targetRect=[System.Drawing.Rectangle]::new(196,267,568,687)
$g.DrawImage($mapShot,$targetRect,$sourceRect,[System.Drawing.GraphicsUnit]::Pixel); $mapShot.Dispose()
$badge=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(220,13,20,27)); $g.FillRectangle($badge,210,281,220,30); $badge.Dispose(); Txt 'QUESTLOG.GG  ·  LIVE-KARTE' 221 288 12 '#F0C66F' 'Bold'
Rect 780 230 359 724 '#111A21'; Rect 780 230 359 67 '#17242C'; Txt 'AION2 NEWS' 801 241 18 '#F1F4F5' 'Bold'; Txt 'OFFIZIELLE PATCHNOTES  ·  LIVE' 801 266 10 '#6ACAB4' 'Bold'
Rect 795 312 329 232 '#18252D'; Txt 'OFFIZIELLE PATCHNOTES' 811 327 10 '#E7BE68' 'Bold'; Txt 'Oct. 6 (PDT) / Oct. 7 (CEST)' 811 348 12 '#AFC0C7'; Txt '[Notice] Patch Notes' 811 373 18 '#F1F4F5' 'Bold'; Txt 'Improvements & Bug Fixes' 811 398 12 '#6ACAB4' 'Bold'; Rect 811 429 3 36 '#E7BE68'; Txt 'Founder Pack-Inhalte' 825 428 12 '#E5EAEC' 'Bold'; Txt 'auf anderen Charakteren & Servern' 825 446 10 '#AFC0C7'; Rect 811 477 3 36 '#E7BE68'; Txt 'Höhere Raw-Leather-Droprate' 825 476 12 '#E5EAEC' 'Bold'; Txt 'in mehreren Gebieten' 825 494 10 '#AFC0C7'; Rect 811 525 3 36 '#E7BE68'
Rect 795 559 329 151 '#18252D'; Txt 'ECHTE MELDUNGEN. DIREKT VON NC.' 811 575 11 '#E7BE68' 'Bold'; Txt 'Neue Patchnotes erscheinen automatisch' 811 600 12 '#E5EAEC'; Txt 'im offiziellen Updates-Board.' 811 619 12 '#E5EAEC'; Txt 'Original-Link und Veröffentlichungsdatum.' 811 650 10 '#AFC0C7'; Rect 795 725 329 54 '#203039'; Txt 'LIVE-WEBSITE' 811 737 11 '#6ACAB4' 'Bold'; Txt 'Keine manuell gepflegten News' 811 756 11 '#B7C5CB'
# Separate Wakayashi window
Rect 1190 284 562 672 '#1C2831'; Rect 1194 288 554 664 '#0D141B'; Rect 1194 288 554 48 '#17232B'; Txt 'AETHERION' 1214 301 15 '#E7BE68' 'Bold'; Txt 'WAKAYASHI.GG' 1326 301 15 '#F1F4F5' 'Bold'; Rect 1210 352 522 574 '#111A21'; Txt 'AION2  /  GUIDES & DATABASE' 1232 370 11 '#6ACAB4' 'Bold'; Txt 'Alles für deinen nächsten Run.' 1232 400 22 '#F1F4F5' 'Bold'; Txt 'Builds, Klassen und Updates.' 1232 432 14 '#B7C5CB'
Rect 1232 478 478 118 '#18252D'; Txt 'KLASSEN & BUILDS' 1250 495 11 '#E7BE68' 'Bold'; Txt 'Finde deinen Build' 1250 522 18 '#F1F4F5' 'Bold'; Txt 'Skills · Ausrüstung · Spielstil' 1250 551 12 '#B7C5CB'; Rect 1232 612 478 118 '#18252D'; Txt 'GUIDES' 1250 630 11 '#6ACAB4' 'Bold'; Txt 'Wissen für Atreia' 1250 657 18 '#F1F4F5' 'Bold'; Txt 'Wakayashi.gg als eigenes Fenster' 1250 686 12 '#B7C5CB'; Rect 1232 757 478 130 '#13212A'; Txt 'DEIN GAME. DEINE INFOS.' 1250 776 11 '#E7BE68' 'Bold'; Txt 'Questlog-Karte und Wakayashi' 1250 805 14 '#F1F4F5' 'Bold'; Txt 'bleiben unabhängig voneinander.' 1250 829 14 '#F1F4F5' 'Bold'; Txt 'Ein Klick vom DPS Meter entfernt.' 1250 861 12 '#B7C5CB'
Txt 'VORSCHAU  ·  Patchnotes direkt vom offiziellen AION2-Updates-Board.' 56 1000 13 '#9FB0B8'
$out=Join-Path $PSScriptRoot '..\dist\aetherion-aion2-hub-news-preview.png'; $bmp.Save([System.IO.Path]::GetFullPath($out),[System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose();$bmp.Dispose(); Write-Output ([System.IO.Path]::GetFullPath($out))
