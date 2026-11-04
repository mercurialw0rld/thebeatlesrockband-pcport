using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using BeatlesLauncher;
class PerformanceModeTests {
 static int checks;
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static T Field<T>(LauncherForm form,string name){return (T)typeof(LauncherForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);}
 [STAThread] static int Main(){try{
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  Check(new Settings().PerformanceMode,"new users keep the transition fix enabled");
  File.WriteAllText(GameFiles.SettingsPath,"{\"XexPath\":\"C:/Legal game/default.xex\",\"Volume\":73,\"Keys\":[{\"Key\":\"whammy\",\"Value\":\"Numpad0\"}]}");
  var settings=GameFiles.LoadSettings();Check(settings.PerformanceMode&&settings.Volume==73&&settings.Keys["whammy"]=="Numpad0","older profiles enable the fix without losing existing values");
  foreach(bool enabled in new[]{false,true}){
   settings.PerformanceMode=enabled;GameFiles.WriteJson(GameFiles.SettingsPath,settings);settings=GameFiles.LoadSettings();
   Check(settings.PerformanceMode==enabled,"performance mode survives save/reload: "+enabled);
   string args=GameFiles.Arguments(settings,"C:/Legal game","test.log");
   Check(args.Contains("--d3d12_nonblocking_pipeline_creation="+enabled.ToString().ToLowerInvariant()),"explicit runtime override: "+enabled);
   Check(args.Contains("--game_data_root=C:/Legal game")&&args.Contains("--controller_device=keyboard")&&args.Contains("--windows_audio"),"performance mode preserves root, device and audio arguments");
  }
  using(var form=new LauncherForm(null,false)){
   Check(Field<CheckBox>(form,"performanceMode").Checked,"launcher restores enabled profile");
   Field<CheckBox>(form,"performanceMode").Checked=false;
   Check(!GameFiles.LoadSettings().PerformanceMode,"unchecking is saved immediately without starting the game");
   Field<CheckBox>(form,"performanceMode").Checked=true;
   Check(GameFiles.LoadSettings().PerformanceMode,"checking is saved immediately without starting the game");
  }
  using(var form=new LauncherForm(null,true)){
   foreach(var scale in new[]{1f,1.25f,1.5f,2f})foreach(var size in new[]{new Size(640,480),new Size(1000,740),new Size(1920,1080)}){
    form.PreviewLayout(size,scale);form.Show();Application.DoEvents();form.RefreshLayout();
    var toggle=Field<CheckBox>(form,"performanceMode");var hint=Field<Label>(form,"performanceHint");var title=Field<Label>(form,"instrumentTitle");
    Check(toggle.Width>0&&toggle.Height>=26*scale&&!toggle.Bounds.IntersectsWith(hint.Bounds)&&!hint.Bounds.IntersectsWith(title.Bounds),"performance row has space at "+size+" scale "+scale);
    var pane=(ScrollableControl)toggle.Parent;pane.ScrollControlIntoView(toggle);Application.DoEvents();
    Check(toggle.Top>=0&&toggle.Bottom<=pane.ClientSize.Height,"performance mode is reachable by scrolling at "+size+" scale "+scale);
    pane.AutoScrollPosition=Point.Empty;form.Hide();
   }
  }
  Console.WriteLine(checks+" performance mode checks passed");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
