using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kbd=BeatlesLauncher.Controls;
namespace BeatlesLauncher {
 public sealed class ControlsForm : ResponsiveWindow {
  readonly Dictionary<string,string> keyboard,drumKeyboard;
  readonly Dictionary<string,Dictionary<string,string>> profiles,drumProfiles;
  Dictionary<string,string> bindings;
  readonly Dictionary<string,Button> keyButtons=new Dictionary<string,Button>();
  readonly List<Control[]> rows=new List<Control[]>();
  ComboBox device,instrument;Timer deviceTimer;ControllerDevices.State baseline;bool hasBaseline,refreshing;DateTime captureStarted;
  string selected="keyboard",selectedInstrument="guitar",deviceSignature="",pending;int deviceTicks;bool rebuilding;
  Label title,hint,deviceLabel,instrumentLabel,deviceStatus,message,groupTitle,menuTitle;
  Button refresh,reset,cancel,save;AeroPane body;Card pads,menu;readonly bool preview;
  public string DeviceId {get{return selected;}}
  public string Instrument {get{return selectedInstrument;}}
  public Dictionary<string,string> Bindings {get{return new Dictionary<string,string>(keyboard);}}
  public Dictionary<string,string> DrumBindings {get{return new Dictionary<string,string>(drumKeyboard);}}
  public Dictionary<string,Dictionary<string,string>> DeviceProfiles {get{return Copy(profiles,"guitar");}}
  public Dictionary<string,Dictionary<string,string>> DrumDeviceProfiles {get{return Copy(drumProfiles,"drums");}}
  static Dictionary<string,Dictionary<string,string>> Copy(Dictionary<string,Dictionary<string,string>> source,string kind){return source==null?new Dictionary<string,Dictionary<string,string>>():source.ToDictionary(p=>p.Key,p=>ControllerDevices.Normalize(p.Value,kind));}
  public ControlsForm(Dictionary<string,string> current,bool rendering):this(new Settings {Keys=current},rendering){}
  public ControlsForm(Settings current,bool rendering){
   keyboard=Kbd.Normalize(current.Keys);drumKeyboard=Kbd.Normalize(current.DrumKeys,"drums");profiles=Copy(current.DeviceBindings,"guitar");drumProfiles=Copy(current.DrumDeviceBindings,"drums");selected=String.IsNullOrEmpty(current.InputDevice)?"keyboard":current.InputDevice;selectedInstrument=current.Instrument=="drums"?"drums":"guitar";bindings=keyboard;preview=rendering;ShowInTaskbar=!rendering;
   Text="Controls · The Beatles: Rock Band";ClientSize=new Size(940,740);MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;KeyPreview=true;WindowButtons(false);
   body=new AeroPane {AutoScroll=true};Controls.Add(body);
   title=AddLabel(body,"Controls",32,Aero.Ink);hint=AddLabel(body,"Choose an instrument and device. Click an action, then press a key, button or pad, or move an axis.",10,Aero.Muted);
   instrumentLabel=AddLabel(body,"Instrument",10,Aero.Muted);deviceLabel=AddLabel(body,"Device",10,Aero.Muted);
   instrument=new AeroCombo();instrument.Items.AddRange(new object[]{"Guitar","Drums"});instrument.SelectedIndex=selectedInstrument=="drums"?1:0;instrument.SelectedIndexChanged+=delegate{selectedInstrument=instrument.SelectedIndex==1?"drums":"guitar";pending=null;SelectBindings();BuildGroups();};body.Controls.Add(instrument);
   device=new AeroCombo();device.SelectedIndexChanged+=delegate {if(refreshing||device.SelectedItem==null)return;selected=((InputDevice)device.SelectedItem).Id;pending=null;hasBaseline=false;SelectBindings();};body.Controls.Add(device);
   refresh=Aero.Button("Refresh devices",Rectangle.Empty);refresh.Click+=delegate {RefreshDevices(true);};body.Controls.Add(refresh);
   deviceStatus=AddLabel(body,"Scanning for controllers…",9,Aero.Muted);message=AddLabel(body,"",9,Aero.Muted);message.TextChanged+=delegate{RefreshLayout();};
   reset=Aero.Button("Restore defaults",Rectangle.Empty);reset.Click+=delegate{pending=null;var values=selected=="keyboard"?Kbd.Defaults(selectedInstrument):ControllerDevices.Defaults(selectedInstrument);foreach(var item in values)bindings[item.Key]=item.Value;RefreshKeys();};Controls.Add(reset);
   cancel=Aero.Button("Cancel",Rectangle.Empty);cancel.Click+=delegate{DialogResult=DialogResult.Cancel;Close();};Controls.Add(cancel);
   save=Aero.Button("Save",Rectangle.Empty,true);save.Click+=delegate{if(Problem()!=null)return;DialogResult=DialogResult.OK;Close();};Controls.Add(save);CancelButton=cancel;
   RefreshDevices(true);BuildGroups();RefreshLayout();
   if(!rendering){deviceTimer=new Timer {Interval=16};deviceTimer.Tick+=PollDevice;deviceTimer.Start();}
  }
  protected override Size PreferredSizeLogical {get{return new Size(940,860);}}
  static Label AddLabel(Control parent,string text,float size,Color color){return Aero.Label(parent,text,Rectangle.Empty,size>=16?Aero.Display(size):Aero.UI(size),color);}
  void BuildGroups(){
   rebuilding=true;
   keyButtons.Clear();rows.Clear();if(pads!=null){body.Controls.Remove(pads);pads.Dispose();body.Controls.Remove(menu);menu.Dispose();}
   pads=new Card();menu=new Card();body.Controls.Add(pads);body.Controls.Add(menu);
   groupTitle=AddLabel(pads,selectedInstrument=="drums"?"DRUM PADS & PEDAL":"GUITAR",10,Aero.Muted);menuTitle=AddLabel(menu,"MENU & EXTRAS",10,Aero.Muted);
   var actions=Kbd.ForInstrument(selectedInstrument);int split=selectedInstrument=="drums"?5:9;
   for(int i=0;i<actions.Length;i++){
    var action=actions[i];var card=i<split?pads:menu;
    int color=Array.FindIndex(Kbd.Actions,a=>a.Id==action.Id);
    var label=AddLabel(card,action.Label,10,color>=0&&color<5?Aero.FretInk[color]:Aero.Ink);
    var key=Aero.Button("",Rectangle.Empty);key.Font=Aero.UI(9,FontStyle.Bold);key.AccessibleName=action.Label;key.Click+=delegate{pending=action.Id;captureStarted=DateTime.UtcNow;hasBaseline=selected!="keyboard"&&ControllerDevices.Read(selected,out baseline);RefreshKeys();};card.Controls.Add(key);keyButtons.Add(action.Id,key);
    var clear=Aero.Button("×",Rectangle.Empty);clear.AccessibleName="Clear "+action.Label;((AeroButton)clear).Link=true;clear.Click+=delegate{pending=null;bindings[action.Id]="";RefreshKeys();};card.Controls.Add(clear);rows.Add(new Control[]{label,key,clear});
   }RefreshKeys();rebuilding=false;RefreshLayout();
  }
  int ArrangeCard(Card card,Label heading,int width){
   Place(heading,S(14),S(12),width-S(28),S(24));int y=S(46),gap=S(7);
   foreach(var row in rows.Where(r=>r[0].Parent==card)){
    int bw=Math.Min(S(124),width*34/100),lw=Math.Max(S(75),width-bw-S(64)),height=Math.Max(S(38),TextHeight((Label)row[0],lw,30));
    Place(row[0],S(14),y,lw,height);Place(row[1],width-bw-S(43),y,bw,height);Place(row[2],width-S(32),y,S(26),height);y+=height+gap;
   }return y+S(10);
  }
  protected override void LayoutContent(){
   if(save==null||pads==null||rebuilding)return;ChromeLayout();int pad=S(22),footer=ClientSize.Height-pad-S(42),width=ClientSize.Width-pad*2;
   if(width>=S(500)){Place(reset,pad,footer,S(178),S(40));Place(cancel,ClientSize.Width-pad-S(250),footer,S(110),S(40));Place(save,ClientSize.Width-pad-S(126),footer,S(126),S(40));}
   else{int w=(width-S(16))/3;Place(reset,pad,footer,w,S(40));Place(cancel,pad+w+S(8),footer,w,S(40));Place(save,pad+2*(w+S(8)),footer,w,S(40));}
   Place(body,pad,S(43),width,Math.Max(S(80),footer-S(57)));int wbody=width-SystemInformation.VerticalScrollBarWidth-S(4),y=0;
   Place(title,0,y,wbody,S(55));y+=S(62);int h=TextHeight(hint,wbody,30);Place(hint,0,y,wbody,h);y+=h+S(14);
   int combo=Math.Max(S(35),device.PreferredHeight);
   if(wbody>=S(650)){
    Place(instrumentLabel,0,y,S(150),S(24));Place(deviceLabel,S(180),y,wbody-S(180),S(24));y+=S(29);
    Place(instrument,0,y,S(160),combo);Place(device,S(180),y,wbody-S(344),combo);Place(refresh,wbody-S(150),y,S(150),combo);y+=combo+S(10);
   }else{
    Place(instrumentLabel,0,y,S(100),S(24));Place(instrument,S(112),y,wbody-S(112),combo);y+=combo+S(12);
    Place(deviceLabel,0,y,wbody,S(24));y+=S(28);Place(device,0,y,wbody,combo);y+=combo+S(8);Place(refresh,0,y,S(160),S(36));y+=S(44);
   }
   h=TextHeight(deviceStatus,wbody,30);Place(deviceStatus,0,y,wbody,h);y+=h+S(12);
   bool columns=wbody>=S(680);int cardWidth=columns?(wbody-S(18))/2:wbody;
   int ph=ArrangeCard(pads,groupTitle,cardWidth),mh=ArrangeCard(menu,menuTitle,cardWidth);
   Place(pads,0,y,cardWidth,ph);Place(menu,columns?cardWidth+S(18):0,columns?y:y+ph+S(14),cardWidth,mh);y+=columns?Math.Max(ph,mh):ph+S(14)+mh;y+=S(14);
   h=TextHeight(message,wbody,65);Place(message,0,y,wbody,h);body.AutoScrollMinSize=new Size(0,y+h+S(12));
  }
  void SelectBindings(){
   if(selected=="keyboard")bindings=selectedInstrument=="drums"?drumKeyboard:keyboard;
   else{var all=selectedInstrument=="drums"?drumProfiles:profiles;if(!all.ContainsKey(selected))all[selected]=ControllerDevices.Defaults(selectedInstrument);bindings=all[selected];}
   if(save!=null)RefreshKeys();
  }
  void RefreshKeys(){
   foreach(var pair in keyButtons){pair.Value.Text=pending==pair.Key?"Press input…":selected=="keyboard"?Kbd.Display(bindings[pair.Key]):ControllerDevices.Display(bindings[pair.Key]);((AeroButton)pair.Value).Highlight=pending==pair.Key;}
   string problem=Problem();save.Enabled=pending==null&&problem==null;
   message.Text=pending!=null?"Press the new input for "+Kbd.ForInstrument(selectedInstrument).First(a=>a.Id==pending).Label+". Escape cancels controller capture.":problem??(selectedInstrument=="drums"?"Four pads and a kick pedal. Hit the pads directly; no strumming.\nBeatlemania: complete a drum fill and hit the final green note.\nChanges apply the next time you start the game.":"Frets, strum and whammy can be used together.\nEach instrument and device keeps its own profile.\nChanges apply the next time you start the game.");message.ForeColor=problem==null?Aero.Muted:Color.FromArgb(157,80,46);RefreshLayout();
  }
  string Problem(){return selected=="keyboard"?Kbd.Validate(bindings,selectedInstrument):ControllerDevices.Validate(bindings,selectedInstrument);}
  void RefreshDevices(bool force){
   var devices=ControllerDevices.List();deviceStatus.Text=ControllerDevices.Status;var signature=String.Join("|",devices.Select(d=>d.Id+":"+d.Name));if(!force&&signature==deviceSignature)return;deviceSignature=signature;
   refreshing=true;device.Items.Clear();device.Items.Add(new InputDevice{Id="keyboard",Name="Keyboard"});foreach(var d in devices)device.Items.Add(d);if(selected!="keyboard"&&!devices.Any(d=>d.Id==selected))device.Items.Add(new InputDevice{Id=selected,Name="Saved controller (disconnected)"});device.SelectedIndex=Enumerable.Range(0,device.Items.Count).First(i=>((InputDevice)device.Items[i]).Id==selected);refreshing=false;SelectBindings();RefreshLayout();
  }
  void PollDevice(object sender,EventArgs args){
   if(++deviceTicks>=60){deviceTicks=0;if(pending==null)RefreshDevices(false);}
   if(pending==null||selected=="keyboard"||!ContainsFocus)return;
   if(DateTime.UtcNow-captureStarted>TimeSpan.FromSeconds(15)){pending=null;RefreshKeys();return;}
   ControllerDevices.State now;if(!ControllerDevices.Read(selected,out now)){message.Text="Device disconnected. Reconnect it or choose Keyboard.";return;}
   if(!hasBaseline){baseline=now;hasBaseline=true;return;}baseline.Buttons&=now.Buttons;for(int i=0;i<Math.Min(baseline.HatCount,now.HatCount);i++)baseline.Hats[i]&=now.Hats[i];string captured=ControllerDevices.Capture(baseline,now);if(captured!=null){bindings[pending]=captured;pending=null;RefreshKeys();}
  }
  protected override bool ProcessCmdKey(ref Message msg,Keys keyData){
   if(pending!=null){if(selected!="keyboard"){if((keyData&System.Windows.Forms.Keys.KeyCode)==System.Windows.Forms.Keys.Escape){pending=null;RefreshKeys();}return true;}string key=KeyName(keyData&System.Windows.Forms.Keys.KeyCode);if(key!=null){bindings[pending]=key;pending=null;RefreshKeys();}else message.Text="That key is unavailable. Try another one.";return true;}return base.ProcessCmdKey(ref msg,keyData);
  }
  protected override void Dispose(bool disposing){if(disposing&&deviceTimer!=null){deviceTimer.Stop();deviceTimer.Dispose();}base.Dispose(disposing);}
  public static string KeyName(Keys key) {
   int code=(int)key;
   if(code>=48&&code<=57||code>=65&&code<=90)return ((char)code).ToString();
   if(code>=96&&code<=105)return "Numpad"+(code-96);
   if(code>=112&&code<=135)return "F"+(code-111);
   switch(key) {
    case Keys.ShiftKey:case Keys.LShiftKey:case Keys.RShiftKey:return "Shift";
    case Keys.ControlKey:case Keys.LControlKey:case Keys.RControlKey:return "Control";
    case Keys.Menu:case Keys.LMenu:case Keys.RMenu:return "Alt";
    case Keys.Back:return "Backspace";case Keys.Enter:return "Return";case Keys.Tab:return "Tab";
    case Keys.Space:return "Space";case Keys.Escape:return "Escape";
    case Keys.Up:return "Up";case Keys.Down:return "Down";case Keys.Left:return "Left";case Keys.Right:return "Right";
    case Keys.Delete:return "Delete";case Keys.Insert:return "Insert";case Keys.Home:return "Home";case Keys.End:return "End";
    case Keys.PageUp:return "PageUp";case Keys.PageDown:return "PageDown";
    case Keys.Add:return "NumpadPlus";case Keys.Subtract:return "NumpadMinus";case Keys.Multiply:return "NumpadStar";case Keys.Divide:return "NumpadSlash";
    case Keys.Oemtilde:return "Backtick";case Keys.OemMinus:return "Minus";case Keys.Oemplus:return "Plus";
    case Keys.Oemcomma:return "Comma";case Keys.OemPeriod:return "Period";case Keys.OemSemicolon:return "Semicolon";
    case Keys.OemQuestion:return "Slash";case Keys.OemPipe:return "Backslash";case Keys.OemOpenBrackets:return "LBracket";
    case Keys.OemCloseBrackets:return "RBracket";case Keys.OemQuotes:return "Quote";
    case Keys.CapsLock:return "CapsLock";case Keys.NumLock:return "NumLock";case Keys.Scroll:return "ScrollLock";
    case Keys.Pause:return "Pause";case Keys.PrintScreen:return "PrintScreen";
   }
   return null;
  }
  protected override bool ShowWithoutActivation {get{return preview;}}
  public void Render(string output) {Show();Application.DoEvents();PerformLayout();using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(output,System.Drawing.Imaging.ImageFormat.Png);}}
 }
}
