using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using BeatlesLauncher;
using Kbd=BeatlesLauncher.Controls;
class InstrumentUiTests {
 static int checks;static string output;
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static IEnumerable<Control> All(Control root){foreach(Control c in root.Controls){yield return c;foreach(var child in All(c))yield return child;}}
 static void Layout(ResponsiveWindow form,int width,int height,float scale,string name){
  form.PreviewLayout(new Size(width,height),scale);form.Show();Application.DoEvents();form.RefreshLayout();Application.DoEvents();
  foreach(var pane in All(form).Where(c=>c is AeroPane)){
   var children=pane.Controls.Cast<Control>().Where(c=>c.Visible).ToArray();for(int i=0;i<children.Length;i++)for(int j=i+1;j<children.Length;j++)Check(!children[i].Bounds.IntersectsWith(children[j].Bounds),name+" content blocks do not overlap");
   ((ScrollableControl)pane).AutoScrollPosition=Point.Empty;
  }
  foreach(var parent in All(form).Where(c=>c is Card)){
   var children=parent.Controls.Cast<Control>().Where(c=>c.Visible).ToArray();
   foreach(var c in children)Check(c.Left>=0&&c.Top>=0&&c.Right<=parent.ClientSize.Width&&c.Bottom<=parent.ClientSize.Height,name+" card contains "+c.Text);
   for(int i=0;i<children.Length;i++)for(int j=i+1;j<children.Length;j++)Check(!children[i].Bounds.IntersectsWith(children[j].Bounds),name+" card rows do not overlap");
  }
  foreach(var c in form.Controls.Cast<Control>().Where(c=>c is Button&&c.Visible))Check(c.Left>=0&&c.Top>=0&&c.Right<=form.ClientSize.Width&&c.Bottom<=form.ClientSize.Height,name+" footer/chrome visible: "+c.Text);
  using(var bitmap=new Bitmap(form.Width,form.Height)){bitmap.SetResolution(96,96);form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save(Path.Combine(output,name+".png"));}
  form.Hide();
 }
 [STAThread] static int Main(string[] args){try{
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);output=args[0];Directory.CreateDirectory(output);
  File.WriteAllText(GameFiles.SettingsPath,"{\"XexPath\":\"C:/Legal/default.xex\",\"Width\":1920,\"Volume\":73,\"Keys\":[{\"Key\":\"whammy\",\"Value\":\"Numpad0\"}]}",new UTF8Encoding(false));
  var settings=GameFiles.LoadSettings();Console.WriteLine("Loaded: "+settings.Instrument+" / "+settings.Keys["whammy"]+" / "+settings.Width+" / "+settings.Volume);Check(settings.Instrument=="guitar"&&settings.Keys["whammy"]=="Numpad0"&&settings.Width==1920&&settings.Volume==73,"old settings preserve guitar and display");
  Check(Kbd.Validate(settings.DrumKeys,"drums")==null,"drum defaults valid");
  settings.Instrument="drums";settings.DrumKeys["orange"]="V";settings.DrumDeviceBindings["kit"]=ControllerDevices.Defaults("drums");settings.DrumDeviceBindings["kit"]["orange"]="axis:1:0:32767";
  GameFiles.WriteJson(GameFiles.SettingsPath,settings);settings=GameFiles.LoadSettings();Check(settings.DrumKeys["orange"]=="V"&&settings.Keys["whammy"]=="Numpad0","independent instrument keys survive save/reload");
  string a=GameFiles.Arguments(settings,"C:/Legal game","test.log");Check(a.Contains("--input_instrument=drums")&&a.Contains("--keybind_left_shoulder=V")&&a.Contains("--keybind_b=D,2")&&a.Contains("--keybind_a=K,1"),"drums route pads/pedal and instrument to runtime");
  Check(a.Contains("\"--keybind_rstick_right=\"")&&a.Contains("\"--keybind_right_shoulder=\""),"drums clear whammy and solo outputs");Check(a.Contains("--controller_device=keyboard")&&a.Contains("--game_data_root=C:/Legal game"),"keyboard selector and game root remain nonempty");
  settings.InputDevice="kit";a=GameFiles.Arguments(settings,"C:/Legal game","test.log");Check(a.Contains("orange=axis:1:0:32767")&&a.Contains("--controller_device=kit"),"physical drum pedal axis survives serialization");
  var duplicate=Kbd.Defaults("drums");duplicate["orange"]="D";Check(Kbd.Validate(duplicate,"drums")!=null,"duplicate kick/pad rejected");
  settings.Instrument="guitar";settings.InputDevice="keyboard";a=GameFiles.Arguments(settings,"C:/Legal game","test.log");Check(a.Contains("--input_instrument=guitar")&&a.Contains("--keybind_rstick_right=Numpad0"),"switching back preserves custom guitar whammy");
  using(var editor=new ControlsForm(settings,true)){
   var combo=(ComboBox)typeof(ControlsForm).GetField("instrument",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(editor);combo.SelectedIndex=1;
   Check(editor.Instrument=="drums"&&editor.DrumBindings["orange"]=="V"&&editor.Bindings["whammy"]=="Numpad0","instrument selector keeps both profiles");combo.SelectedIndex=0;combo.SelectedIndex=1;Check(settings.Instrument=="guitar"&&settings.DrumKeys["orange"]=="V","editing remains isolated until Save");
   Layout(editor,940,860,1,"controls-drums-100");Layout(editor,760,660,1,"controls-drums-compact");Layout(editor,1180,800,1.25f,"controls-drums-125");Layout(editor,1140,680,1.5f,"controls-drums-150");Layout(editor,1520,1040,2,"controls-drums-200");Layout(editor,640,480,1,"controls-drums-640");combo.SelectedIndex=0;Layout(editor,940,860,1,"controls-guitar-100");
  }
  using(var launcher=new LauncherForm(null,true)){
   Layout(launcher,1000,740,1,"launcher-100");Layout(launcher,760,660,1,"launcher-compact");Layout(launcher,1180,700,1.25f,"launcher-125");Layout(launcher,1140,680,1.5f,"launcher-150");Layout(launcher,2000,1400,2,"launcher-200");Layout(launcher,640,480,1,"launcher-640");
   var status=(Label)typeof(LauncherForm).GetField("status",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(launcher);status.Text="Missing disc files: gen/main_0.ark, gen/main_1.ark, gen/main_2.ark and others. Keep the XEX beside the complete gen folder.";Layout(launcher,640,480,1,"launcher-long-status");
   var state=(Settings)typeof(LauncherForm).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(launcher);state.Instrument="drums";typeof(LauncherForm).GetMethod("RefreshControls",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(launcher,null);Layout(launcher,1000,740,1,"launcher-drums-100");
  }
  Console.WriteLine(checks+" checks passed");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
