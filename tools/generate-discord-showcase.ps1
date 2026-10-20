$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$source=@"
using System;using System.Drawing;using System.Drawing.Drawing2D;using System.Drawing.Imaging;using System.Drawing.Text;
public static class DiscordShowcase {
 static GraphicsPath Round(Rectangle r,int rad){var p=new GraphicsPath();int d=rad*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
 static void Text(Graphics g,string s,float x,float y,float size,Color c,bool bold=false,float spacing=0){using(var f=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel)){using(var b=new SolidBrush(c)){g.DrawString(s,f,b,new PointF(x,y));}}}
 static void Center(Graphics g,string s,Rectangle r,float size,Color c,bool bold=false){using(var f=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel)){using(var b=new SolidBrush(c)){using(var sf=new StringFormat()){sf.Alignment=StringAlignment.Center;sf.LineAlignment=StringAlignment.Center;g.DrawString(s,f,b,r,sf);}}}}
 static void Haze(Graphics g,int x,int y,int w,int h,int alpha){using(var path=new GraphicsPath()){path.AddEllipse(x,y,w,h);using(var b=new PathGradientBrush(path)){b.CenterColor=Color.FromArgb(alpha,255,103,35);b.SurroundColors=new[]{Color.FromArgb(0,255,103,35)};g.FillPath(b,path);}}}
 static void ImageRounded(Graphics g,Image im,Rectangle r,int rad){using(var p=Round(r,rad)){g.SetClip(p);g.DrawImage(im,r);g.ResetClip();using(var pen=new Pen(Color.FromArgb(195,244,142,65),2)){g.DrawPath(pen,p);}}}
 public static void Create(string outPath,string iconPath,string raidPath,string overlayPath){const int W=1600,H=1000;using(var b=new Bitmap(W,H,PixelFormat.Format32bppArgb)){using(var g=Graphics.FromImage(b)){g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
 using(var bg=new LinearGradientBrush(new Rectangle(0,0,W,H),Color.FromArgb(18,20,27),Color.FromArgb(7,9,13),110f)){g.FillRectangle(bg,0,0,W,H);}Haze(g,900,30,750,620,60);Haze(g,-230,650,820,480,30);
 // warm angular accents
 using(var p=new Pen(Color.FromArgb(30,245,125,53),2)){g.DrawLine(p,1330,0,1595,250);g.DrawLine(p,1460,0,1595,130);g.DrawLine(p,0,880,210,1000);} 
 using(var icon=Image.FromFile(iconPath)){g.DrawImage(icon,new Rectangle(62,48,68,68));}
 Text(g,"AETHERION",148,52,22,Color.FromArgb(246,239,229),true);Text(g,"DPS METER  |  AION 2 GLOBAL",149,82,11,Color.FromArgb(182,174,165),true);
 using(var p=Round(new Rectangle(1321,53,217,42),18)){using(var fill=new SolidBrush(Color.FromArgb(36,255,142,58)))g.FillPath(fill,p);using(var stroke=new Pen(Color.FromArgb(110,255,153,72),1))g.DrawPath(stroke,p);}Center(g,"LIVE-KAMPFANALYSE",new Rectangle(1321,53,217,42),12,Color.FromArgb(255,201,126),true);
 Text(g,"DEIN RAID.",70,192,58,Color.FromArgb(247,243,237),true);Text(g,"KLAR IM BLICK.",70,258,52,Color.FromArgb(255,152,66),true);
 Text(g,"Alle wichtigen Kampfdaten auf einen Blick:",74,341,19,Color.FromArgb(201,197,190));Text(g,"live im Spiel und im klaren Dashboard.",74,370,19,Color.FromArgb(201,197,190));
 string[] feats={"Gruppen-DPS und Heilung live","Boss-HP, Kampfzeit und Verlauf","Skill-Details und Kampfvergleiche","Boss- und Event-Timer im Blick"};int yy=444;foreach(string f in feats){using(var br=new SolidBrush(Color.FromArgb(255,166,86,46)))g.FillEllipse(br,78,yy+7,14,14);using(var br=new SolidBrush(Color.FromArgb(255,235,177,113)))g.FillEllipse(br,82,yy+11,6,6);Text(g,f,108,yy,19,Color.FromArgb(238,233,225),true);yy+=58;}
 using(var p=Round(new Rectangle(70,713,516,84),18)){using(var br=new LinearGradientBrush(new Rectangle(70,713,516,84),Color.FromArgb(255,255,159,68),Color.FromArgb(255,217,79,43),0f))g.FillPath(br,p);}Text(g,"ENTDECK AETHERION",96,729,18,Color.FromArgb(24,18,14),true);Text(g,"Fragen? Wir helfen dir hier im Discord.",97,756,14,Color.FromArgb(48,32,22));
 using(var raid=Image.FromFile(raidPath)){using(var shadow=new SolidBrush(Color.FromArgb(150,0,0,0)))using(var p=Round(new Rectangle(671,197,867,542),24))g.FillPath(shadow,p);ImageRounded(g,raid,new Rectangle(654,181,867,542),18);}
 using(var ov=Image.FromFile(overlayPath)){using(var sh=new SolidBrush(Color.FromArgb(190,0,0,0)))using(var p=Round(new Rectangle(1071,624,496,224),14))g.FillPath(sh,p);ImageRounded(g,ov,new Rectangle(1055,607,496,224),13);}
 // feature footer
 using(var pen=new Pen(Color.FromArgb(65,255,163,83),1))g.DrawLine(pen,64,881,1536,881);
 Text(g,"LIVE OVERLAY",72,908,11,Color.FromArgb(255,181,104),true);Text(g,"DASHBOARD",378,908,11,Color.FromArgb(255,181,104),true);Text(g,"KAMPFDETAILS",694,908,11,Color.FromArgb(255,181,104),true);Text(g,"AION 2 GLOBAL",1041,908,11,Color.FromArgb(255,181,104),true);
 Text(g,"Dein Kampf. Deine Analyse.",72,934,18,Color.FromArgb(235,229,220),true);Text(g,"Hol dir die Details im angepinnten Download-Post.",1050,934,15,Color.FromArgb(181,174,164));
 }
b.Save(outPath,ImageFormat.Png);}}
}
"@
Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing
$base=(Resolve-Path '.').Path
[DiscordShowcase]::Create((Join-Path $base 'preview/Aetherion-Discord-Showcase.png'),(Join-Path $base 'website/assets/app-icon.png'),(Join-Path $base 'website/assets/showcase-raid-command.png'),(Join-Path $base 'website/assets/showcase-overlay.png'))

