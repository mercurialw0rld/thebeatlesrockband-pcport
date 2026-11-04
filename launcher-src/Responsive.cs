using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BeatlesLauncher {
 public class ResponsiveWindow : AeroWindow {
  [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr window);
  [StructLayout(LayoutKind.Sequential)] struct NativeRect {public int Left,Top,Right,Bottom;}
  readonly Dictionary<Control,Font> fonts=new Dictionary<Control,Font>();
  readonly List<Font> appliedFonts=new List<Font>();
  bool arranging,simulated;
  public float UiScale {get;private set;}
  protected ResponsiveWindow(){UiScale=1;AutoScaleMode=AutoScaleMode.None;}
  protected int S(float value){return (int)Math.Round(value*UiScale);}
  protected virtual Size PreferredSizeLogical {get{return new Size(1000,740);}}
  protected virtual void LayoutContent(){}
  void FontsFor(Control root){foreach(Control c in root.Controls){
   if(c is Label||c is ButtonBase||c is ComboBox||c is ListBox){Font original;if(!fonts.TryGetValue(c,out original)){original=(Font)c.Font.Clone();fonts.Add(c,original);}float size=original.Size*UiScale;if(Math.Abs(c.Font.Size-size)>.02f||c.Font.Unit!=GraphicsUnit.Pixel){var old=c.Font;var next=new Font(original.FontFamily,size,original.Style,GraphicsUnit.Pixel);c.Font=next;appliedFonts.Add(next);if(appliedFonts.Remove(old))old.Dispose();}}
   FontsFor(c);
  }}
  public void RefreshLayout(){if(arranging||IsDisposed)return;arranging=true;try{FontsFor(this);LayoutContent();Invalidate(true);}finally{arranging=false;}}
  protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);RefreshLayout();}
  protected override void OnLoad(EventArgs e){
   if(!simulated){try{UiScale=Math.Max(1,GetDpiForWindow(Handle)/96f);}catch(EntryPointNotFoundException){using(var g=CreateGraphics())UiScale=g.DpiX/96f;}
    var area=Screen.FromControl(this).WorkingArea;var desired=PreferredSizeLogical;
    if(area.Height/UiScale<720)desired.Width=Math.Min(desired.Width,760);
    ClientSize=new Size(Math.Min(S(desired.Width),Math.Max(280,area.Width-S(20))),Math.Min(S(desired.Height),Math.Max(280,area.Height-S(20))));
    Location=new Point(area.X+(area.Width-Width)/2,area.Y+(area.Height-Height)/2);
   }base.OnLoad(e);RefreshLayout();
  }
  protected override void WndProc(ref Message m){
   if(m.Msg==0x02e0){UiScale=Math.Max(.75f,(m.WParam.ToInt64()&0xffff)/96f);var r=(NativeRect)Marshal.PtrToStructure(m.LParam,typeof(NativeRect));var area=Screen.FromRectangle(Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom)).WorkingArea;int w=Math.Min(r.Right-r.Left,area.Width),h=Math.Min(r.Bottom-r.Top,area.Height);Bounds=new Rectangle(Math.Max(area.Left,Math.Min(r.Left,area.Right-w)),Math.Max(area.Top,Math.Min(r.Top,area.Bottom-h)),w,h);RefreshLayout();return;}
   base.WndProc(ref m);
  }
  protected void Place(Control c,int x,int y,int width,int height){var parent=c.Parent as ScrollableControl;var scroll=parent==null?Point.Empty:parent.AutoScrollPosition;c.Bounds=new Rectangle(x+scroll.X,y+scroll.Y,Math.Max(1,width),Math.Max(1,height));}
  protected int TextHeight(Label label,int width,int minimum){using(var g=CreateGraphics())return Math.Max(S(minimum),(int)Math.Ceiling(g.MeasureString(label.Text,label.Font,Math.Max(1,width)).Height)+S(6));}
  protected void ChromeLayout(){foreach(Control c in Controls){var b=c as AeroButton;if(b!=null&&b.Chrome!=0)b.Bounds=new Rectangle(ClientSize.Width-S(b.Chrome==2?48:83),S(10),S(30),S(28));}}
  // The preview harness exercises real form layout at several scales without
  // changing Windows settings or the user's saved launcher configuration.
  public void PreviewLayout(Size pixels,float scale){simulated=true;UiScale=scale;ClientSize=pixels;RefreshLayout();}
  protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing){foreach(var font in fonts.Values)font.Dispose();foreach(var font in appliedFonts)font.Dispose();}}
 }
 public sealed class AeroPane : Panel {
  public AeroPane(){SetStyle(ControlStyles.UserPaint|ControlStyles.Opaque|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);BackColor=Aero.Cream;}
  // Opaque controls skip OnPaintBackground during a normal WM_PAINT. Paint
  // every exposed gap in OnPaint too, as the other owner-drawn controls do.
  protected override void OnPaintBackground(PaintEventArgs e){}
  protected override void OnPaint(PaintEventArgs e){Aero.PaintBackground(this,e.Graphics);base.OnPaint(e);}
  protected override void OnScroll(ScrollEventArgs e){base.OnScroll(e);Invalidate(true);}
 }
}
