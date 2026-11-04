using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using BeatlesLauncher;
class DlcUiTests {
 static int checks;
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;}
 static void Children(Control parent,string name){
  var children=parent.Controls.Cast<Control>().Where(c=>c.Visible).ToArray();
  for(int i=0;i<children.Length;i++)for(int j=i+1;j<children.Length;j++)Check(!children[i].Bounds.IntersectsWith(children[j].Bounds),name+": controls overlap: "+children[i].Text+" / "+children[j].Text);
  foreach(var child in children)if(child is AeroPane)Children(child,name);
 }
 static void Render(ResponsiveWindow form,Size size,float dpi,string output,string name){
  form.PreviewLayout(size,dpi);form.Show();Application.DoEvents();form.RefreshLayout();Children(form,name);
  foreach(var button in form.Controls.OfType<Button>())Check(button.Left>=0&&button.Top>=0&&button.Right<=form.ClientSize.Width&&button.Bottom<=form.ClientSize.Height,name+": footer visible");
  foreach(var pane in form.Controls.OfType<AeroPane>())pane.AutoScrollPosition=Point.Empty;
  using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.Combine(output,name+".png"));}form.Hide();
 }
 [STAThread]static int Main(string[] args){try{
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Directory.CreateDirectory(args[0]);
  using(var form=new DlcForm(true)){
   Render(form,new Size(760,600),1,args[0],"dlc-100");Render(form,new Size(960,650),1.25f,args[0],"dlc-125");Render(form,new Size(1140,680),1.5f,args[0],"dlc-150");Render(form,new Size(1520,1040),2,args[0],"dlc-200");Render(form,new Size(640,480),1,args[0],"dlc-compact");
   var status=(Label)typeof(DlcForm).GetField("status",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);status.Text="1 imported · 1 already installed · 1 failed. An example package could not be installed because it contains an unsupported file. Existing packages and your original files were not replaced.";
   var progress=(ProgressBar)typeof(DlcForm).GetField("progress",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);progress.Visible=true;Render(form,new Size(640,480),1,args[0],"dlc-long-status");
  }
  using(var main=new LauncherForm(null,true)){Render(main,new Size(640,480),1,args[0],"launcher-import-compact");Render(main,new Size(1000,740),1,args[0],"launcher-import-100");}
  Console.WriteLine(checks+" DLC layout checks passed");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
