using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kbd = BeatlesLauncher.Controls;

namespace BeatlesLauncher {
 public sealed class LauncherForm : ResponsiveWindow {
  static readonly Color TextColor=Aero.Ink, Muted=Aero.Muted;
  Settings settings;
  FileDisplay path;
  Label status,volumeText;
  ComboBox resolution;
  CheckBox fullscreen,performanceMode;
  GlassSlider volume;
  Button play,browse;
  Frets frets;Label controlsSummary;
  CheckResult check;
  Process game;
  int validationGeneration;
  bool preview;
  SleeveArt art;AeroPane body;Label heading,fileHint,resolutionLabel,instrumentTitle,volumeLabel,version,performanceHint;Button editControls,saves,logs,importDlc;
  protected override void LayoutContent(){
   if(version==null)return;ChromeLayout();int pad=S(22),gap=S(16);bool cover=ClientSize.Width>=S(960)&&ClientSize.Height>=S(600);
   art.Visible=cover;int aw=cover?Math.Min(S(380),ClientSize.Width*38/100):0;
   if(cover)Place(art,pad,pad,aw,ClientSize.Height-pad*2);
   int x=cover?pad+aw+S(34):pad,w=ClientSize.Width-x-pad;
   int footer=ClientSize.Height-pad-S(26),buttonY=footer-S(78);version.Visible=w>=S(380);
   Place(play,x,buttonY,w,S(64));int link=w>=S(460)?S(85):S(64),dlcWidth=w>=S(460)?S(120):S(100);Place(saves,x,footer,link,S(26));Place(logs,x+link+S(8),footer,link,S(26));Place(importDlc,x+2*(link+S(8)),footer,dlcWidth,S(26));Place(version,x+w-S(60),footer,S(60),S(26));
   Place(body,x,S(48),w,Math.Max(S(80),buttonY-S(60)));
   int bw=Math.Max(S(240),w-SystemInformation.VerticalScrollBarWidth-S(4)),y=0;
   Place(heading,0,y,bw,S(51));y+=S(60);
   if(bw>=S(470)){Place(path,0,y,bw-S(166),S(62));Place(browse,bw-S(150),y+S(10),S(150),S(42));y+=S(72);}
   else{Place(path,0,y,bw,S(62));y+=S(70);Place(browse,0,y,bw,S(40));y+=S(48);}
   int h=TextHeight(fileHint,bw,24);Place(fileHint,0,y,bw,h);y+=h+S(14);
   Place(resolutionLabel,0,y,bw,S(24));y+=S(29);
   int combo=Math.Max(S(34),resolution.PreferredHeight);
   if(bw>=S(440)){Place(resolution,0,y,S(205),combo);Place(fullscreen,S(238),y,bw-S(238),combo);y+=combo+gap;}
   else{Place(resolution,0,y,bw,combo);y+=combo+S(8);Place(fullscreen,0,y,bw,S(34));y+=S(48);}
   Place(performanceMode,0,y,bw,S(32));y+=S(34);
   h=TextHeight(performanceHint,bw,24);Place(performanceHint,0,y,bw,h);y+=h+S(12);
   Place(instrumentTitle,0,y,Math.Max(S(110),bw-S(165)),S(46));Place(editControls,bw-S(150),y+S(5),S(150),S(36));y+=S(56);
   int fw=Math.Min(S(325),bw);Place(frets,0,y,fw,(int)(fw*55f/325));y+=(int)(fw*55f/325)+S(8);
   h=TextHeight(controlsSummary,bw,44);Place(controlsSummary,0,y,bw,h);y+=h+S(12);
   Place(volumeLabel,0,y,S(75),S(34));Place(volumeText,bw-S(52),y,S(52),S(34));Place(volume,S(85),y,Math.Max(S(80),bw-S(145)),S(34));y+=S(46);
   h=TextHeight(status,bw,36);Place(status,0,y,bw,h);y+=h+S(10);
   body.AutoScrollMinSize=new Size(0,y);
  }
  public LauncherForm(string initialXex,bool rendering) {
   preview=rendering;ShowInTaskbar=!rendering;settings=rendering?new Settings():GameFiles.LoadSettings();if(!String.IsNullOrEmpty(initialXex))settings.XexPath=initialXex;
   try { Icon=new Icon(Path.Combine(GameFiles.AppRoot,"launcher.ico")); } catch { }
   Text="The Beatles: Rock Band · PC";
   ClientSize=new Size(1000,700);MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;WindowButtons(true);
   art=new SleeveArt();Controls.Add(art);
   body=new AeroPane {AutoScroll=true};Controls.Add(body);
   heading=AddLabel(body,"Your game",Rectangle.Empty,28,FontStyle.Regular,TextColor);
   path=new FileDisplay {Value=settings.XexPath};body.Controls.Add(path);
   browse=MakeButton("Choose XEX",Rectangle.Empty,false);browse.Click+=Browse;body.Controls.Add(browse);
   fileHint=AddLabel(body,"Game files are read from their original folder.",Rectangle.Empty,9,FontStyle.Regular,Muted);
   resolutionLabel=AddLabel(body,"Resolution",Rectangle.Empty,9,FontStyle.Bold,Muted);
   resolution=new AeroCombo();resolution.Items.AddRange(new object[]{"1280 × 720","1600 × 900","1920 × 1080"});resolution.SelectedIndex=settings.Width==1920?2:settings.Width==1600?1:0;body.Controls.Add(resolution);
   fullscreen=new AeroCheckBox {Text="Fullscreen",Checked=settings.Fullscreen};body.Controls.Add(fullscreen);
   performanceMode=new AeroCheckBox {Name="performanceMode",Text="Performance mode",Checked=settings.PerformanceMode,AccessibleDescription="Reduce shader compilation stalls during scene transitions. New effects may appear a few frames later. Applies on the next game start."};body.Controls.Add(performanceMode);
   performanceHint=AddLabel(body,"Smoother scene transitions. New effects may appear a few frames later. Applies on next start.",Rectangle.Empty,9,FontStyle.Regular,Muted);
   performanceMode.CheckedChanged+=PerformanceChanged;
   instrumentTitle=AddLabel(body,"Guitar",Rectangle.Empty,25,FontStyle.Regular,TextColor);
   editControls=MakeButton("Edit controls",Rectangle.Empty,false);editControls.Click+=EditControls;body.Controls.Add(editControls);
   frets=new Frets();body.Controls.Add(frets);
   controlsSummary=AddLabel(body,"",Rectangle.Empty,9,FontStyle.Regular,Muted);
   volumeLabel=AddLabel(body,"Volume",Rectangle.Empty,10,FontStyle.Regular,Muted);
   volumeText=AddLabel(body,settings.Volume+"%",Rectangle.Empty,10,FontStyle.Bold,TextColor);volumeText.TextAlign=ContentAlignment.MiddleRight;
   volume=new GlassSlider {Value=Math.Max(0,Math.Min(100,settings.Volume))};volume.ValueChanged+=delegate {volumeText.Text=volume.Value+"%";};body.Controls.Add(volume);
   status=AddLabel(body,"Choose your executable to get started.",Rectangle.Empty,9,FontStyle.Regular,Muted);status.TextChanged+=delegate {RefreshLayout();};
   play=MakeButton("Play",Rectangle.Empty,true);play.Font=Aero.Display(25);((AeroButton)play).PlayIcon=true;play.Enabled=false;play.Click+=Play;Controls.Add(play);AcceptButton=play;
   saves=MakeButton("Saves",Rectangle.Empty,false);((AeroButton)saves).Link=true;saves.Click+=delegate {OpenFolder("userdata");};Controls.Add(saves);
   logs=MakeButton("Logs",Rectangle.Empty,false);((AeroButton)logs).Link=true;logs.Click+=delegate {OpenFolder("logs");};Controls.Add(logs);
   importDlc=MakeButton("Import DLC",Rectangle.Empty,false);((AeroButton)importDlc).Link=true;importDlc.Click+=delegate{using(var manager=new DlcForm())manager.ShowDialog(this);};Controls.Add(importDlc);
   version=AddLabel(this,"0.7.2",Rectangle.Empty,8,FontStyle.Regular,Muted);version.TextAlign=ContentAlignment.MiddleRight;
   RefreshControls();RefreshLayout();
   if(rendering){check=GameFiles.Check(settings.XexPath);ApplyCheck();}
   else Shown+=async delegate {if(!String.IsNullOrWhiteSpace(settings.XexPath))await ValidateSelection();};
  }
  void RefreshControls() {
   settings.Keys=Kbd.Normalize(settings.Keys);settings.DrumKeys=Kbd.Normalize(settings.DrumKeys,"drums");
   bool drums=settings.Instrument=="drums",physical=!String.IsNullOrEmpty(settings.InputDevice)&&settings.InputDevice!="keyboard";
   var keys=drums?settings.DrumKeys:settings.Keys;instrumentTitle.Text=drums?"Drums":"Guitar";
   frets.ColorOrder=drums?new[]{1,2,3,0,4}:new[]{0,1,2,3,4};
   var ids=drums?new[]{"red","yellow","blue","green","orange"}:new[]{"green","red","yellow","blue","orange"};
   if(physical){frets.KeyLabels=drums?new[]{"R","Y","B","G","Kick"}:new[]{"G","R","Y","B","O"};controlsSummary.Text=drums?"Drum controller · four pads and kick pedal\nSeparate instrument and device profiles in Edit controls.":"Guitar controller · custom device profile\nButtons, strum, whammy and Beatlemania in Edit controls.";}
   else{frets.KeyLabels=ids.Select(k=>Kbd.Display(keys[k])).ToArray();controlsSummary.Text=drums?"Red · yellow · blue · green · kick\n"+Kbd.Display(keys["start"])+" pause · Beatlemania: complete a fill and hit green.":Kbd.Display(keys["strum_up"])+" / "+Kbd.Display(keys["strum_down"])+" strum · "+Kbd.Display(keys["whammy"])+" whammy\n"+Kbd.Display(keys["star_power"])+" Beatlemania · "+Kbd.Display(keys["start"])+" pause";}
   RefreshLayout();
   frets.Invalidate();
  }
  void EditControls(object sender,EventArgs args) {
   using(var editor=new ControlsForm(settings,false)) {
    if(editor.ShowDialog(this)!=DialogResult.OK)return;
    var previous=settings.Keys;var previousDevice=settings.InputDevice;var previousProfiles=settings.DeviceBindings;var previousDrums=settings.DrumKeys;var previousDrumProfiles=settings.DrumDeviceBindings;var previousInstrument=settings.Instrument;
    settings.Keys=editor.Bindings;settings.InputDevice=editor.DeviceId;settings.DeviceBindings=editor.DeviceProfiles;settings.Instrument=editor.Instrument;settings.DrumKeys=editor.DrumBindings;settings.DrumDeviceBindings=editor.DrumDeviceProfiles;
    try {GameFiles.WriteJson(GameFiles.SettingsPath,settings);RefreshControls();status.Text="Controls saved. They apply the next time you start the game.";status.ForeColor=Muted;}
    catch(Exception error) {settings.Keys=previous;settings.InputDevice=previousDevice;settings.DeviceBindings=previousProfiles;settings.DrumKeys=previousDrums;settings.DrumDeviceBindings=previousDrumProfiles;settings.Instrument=previousInstrument;MessageBox.Show(this,"Could not save controls.\n\n"+error.Message,"Controls",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   }
  }
  void PerformanceChanged(object sender,EventArgs args) {
   bool previous=settings.PerformanceMode;settings.PerformanceMode=performanceMode.Checked;
   if(preview)return;
   try {GameFiles.WriteJson(GameFiles.SettingsPath,settings);}
   catch(Exception error) {
    performanceMode.CheckedChanged-=PerformanceChanged;settings.PerformanceMode=previous;performanceMode.Checked=previous;performanceMode.CheckedChanged+=PerformanceChanged;
    MessageBox.Show(this,"Could not save performance mode.\n\n"+error.Message,"Performance mode",MessageBoxButtons.OK,MessageBoxIcon.Error);
   }
  }
  static Label AddLabel(Control parent,string text,Rectangle box,float size,FontStyle style,Color color) {
   return Aero.Label(parent,text,box,size>=16?Aero.Display(size):Aero.UI(size,style),color);
  }
  static Button MakeButton(string text,Rectangle box,bool primary) {
   return Aero.Button(text,box,primary);
  }
  async void Browse(object sender,EventArgs args) {
   using(var dialog=new OpenFileDialog {Title="Choose The Beatles: Rock Band executable",Filter="Game executable (default.xex)|default.xex|Xbox 360 executables (*.xex)|*.xex",CheckFileExists=true,Multiselect=false}) {
    if(!String.IsNullOrWhiteSpace(settings.XexPath)&&File.Exists(settings.XexPath))dialog.InitialDirectory=Path.GetDirectoryName(settings.XexPath);
    if(dialog.ShowDialog(this)!=DialogResult.OK)return;settings.XexPath=dialog.FileName;path.Value=dialog.FileName;await ValidateSelection();
   }
  }
  async Task ValidateSelection() {
   int generation=++validationGeneration;play.Enabled=false;status.ForeColor=Muted;status.Text="Checking the executable and disc files…";
   string selected=settings.XexPath;var result=await Task.Run(delegate {return GameFiles.Check(selected);});
   if(IsDisposed||generation!=validationGeneration)return;check=result;ApplyCheck();
  }
  void ApplyCheck() {status.Text=check.Ready?"Your game is ready.":check.Message;status.ForeColor=check.Ready?Aero.Teal:Color.FromArgb(157,80,46);play.Enabled=check.Ready;Invalidate();}
  async void Play(object sender,EventArgs args) {
   await ValidateSelection();if(check==null||!check.Ready)return;
   if(Process.GetProcessesByName("beatlesrockband-guitar").Any()) {MessageBox.Show(this,"A game is already running. Close it before starting another.","Game already running",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
   if(!String.IsNullOrEmpty(settings.InputDevice)&&settings.InputDevice!="keyboard"){ControllerDevices.State state;if(!ControllerDevices.Read(settings.InputDevice,out state)){MessageBox.Show(this,"The selected controller is disconnected. Connect it, or choose Keyboard in Edit controls.","Connect your controller",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}}
   int[] widths={1280,1600,1920},heights={720,900,1080};settings.Width=widths[resolution.SelectedIndex];settings.Height=heights[resolution.SelectedIndex];settings.Fullscreen=fullscreen.Checked;settings.PerformanceMode=performanceMode.Checked;settings.Volume=volume.Value;
   try {
    play.Enabled=false;game=GameFiles.Start(settings,check.Root);if(game==null)throw new Exception("Windows could not start the game.");
    game.Exited+=GameExited;game.EnableRaisingEvents=true;status.Text="The game is running.";Hide();
   } catch(Exception error) {play.Enabled=true;MessageBox.Show(this,"Could not start the game. Extract the package to a writable folder.\n\n"+error.Message,"Could not start",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  void GameExited(object sender,EventArgs args) {
   if(IsDisposed||Disposing)return;
   try {BeginInvoke(new Action(delegate {
    int code=game.ExitCode;game.Dispose();game=null;Show();Activate();play.Enabled=check!=null&&check.Ready;
    status.Text=code==0?"Game closed. Ready to play again.":"The game closed. This session’s log is in Logs.";
    status.ForeColor=code==0?Muted:Color.FromArgb(157,80,46);
   }));}catch(InvalidOperationException){}
  }
  void OpenFolder(string name) {try {string folder=Path.Combine(GameFiles.AppRoot,name);Directory.CreateDirectory(folder);Process.Start(new ProcessStartInfo(folder){UseShellExecute=true});}catch(Exception error){MessageBox.Show(this,error.Message,"Could not open folder");}}

  protected override bool ShowWithoutActivation {get{return preview;}}
  public void Render(string output) {Show();Application.DoEvents();PerformLayout();using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(output,System.Drawing.Imaging.ImageFormat.Png);}}
 }
 static class Program {
  [DllImport("user32.dll")]static extern bool SetProcessDPIAware();
  [STAThread]static int Main(string[] args) {
   try {
    if(args.Length>=3&&args[0]=="--self-test") {
     var result=GameFiles.Check(args[2]);var settings=new Settings {XexPath=args[2]};
     File.WriteAllText(args[1],"ready="+result.Ready+"\nmessage="+result.Message+"\nhash="+result.Hash+"\narguments="+GameFiles.Arguments(settings,result.Root??"C:/No game",Path.Combine(GameFiles.AppRoot,"logs","test.log")),System.Text.Encoding.UTF8);return result.Ready?0:2;
    }
    if(args.Length>=3&&args[0]=="--launch-once") {
     var result=GameFiles.Check(args[2]);if(!result.Ready){File.WriteAllText(args[1],result.Message);return 2;}
     var process=GameFiles.Start(new Settings {XexPath=args[2]},result.Root);File.WriteAllText(args[1],process.Id.ToString());return 0;
    }
    Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
    if(args.Length>=2&&args[0]=="--render-icon") {SleeveArt.SaveIcon(args[1]);return 0;}
    if(args.Length>=2&&args[0]=="--render-controls") {using(var form=new ControlsForm(Kbd.Defaults(),true)){form.Render(args[1]);}return 0;}
    if(args.Length>=2&&args[0]=="--render-preview") {using(var form=new LauncherForm(args.Length>2?args[2]:null,true)){form.Render(args[1]);}return 0;}
    Application.Run(new LauncherForm(args.Length>1&&args[0]=="--xex"?args[1]:null,false));return 0;
   }catch(Exception error){if(args.Length>1&&args[0].StartsWith("--"))File.WriteAllText(args[1],error.ToString());else MessageBox.Show(error.Message,"The Beatles: Rock Band PC",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
  }
 }
}
