using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using BeatlesLauncher;
// Opaque WinForms controls receive paint without a background-paint pass.
// DrawToBitmap alone does not reproduce that contract.
class AeroPanePaintTests {
 [STAThread]static int Main(){try{
  int checks=0;using(var window=new AeroWindow()){window.ClientSize=new Size(800,600);using(var pane=new AeroPane()){window.Controls.Add(pane);
   foreach(var size in new[]{new Size(240,160),new Size(480,320),new Size(160,80)}){
    pane.Bounds=new Rectangle(24,40,size.Width,size.Height);
    foreach(bool partial in new[]{false,true})using(var bitmap=new Bitmap(size.Width,size.Height))using(var graphics=Graphics.FromImage(bitmap)){
     graphics.Clear(Color.Black);var clip=partial?new Rectangle(10,10,size.Width-20,size.Height-20):new Rectangle(Point.Empty,size);graphics.SetClip(clip);
     typeof(AeroPane).GetMethod("OnPaint",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(pane,new object[]{new PaintEventArgs(graphics,clip)});
     foreach(var point in new[]{new Point(clip.Left+1,clip.Top+1),new Point(clip.Right-2,clip.Bottom-2),new Point(clip.Left+clip.Width/2,clip.Top+clip.Height/2)}){var c=bitmap.GetPixel(point.X,point.Y);if(c.R<100||c.G<100||c.B<100)throw new Exception("Opaque pane left an unpainted gap during a paint-only pass.");checks++;}
     if(partial&&bitmap.GetPixel(0,0).ToArgb()!=Color.Black.ToArgb())throw new Exception("Painting escaped the invalidated region.");
    }
   }
  }}Console.WriteLine(checks+" opaque-pane repaint checks passed");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
