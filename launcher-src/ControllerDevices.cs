using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace BeatlesLauncher {
 public sealed class InputDevice {
  public string Id,Name;
  public override string ToString(){return Name;}
 }
 public static class ControllerDevices {
  [StructLayout(LayoutKind.Sequential)] struct NativeDevice {
   [MarshalAs(UnmanagedType.ByValArray,SizeConst=128)] public byte[] Id;
   [MarshalAs(UnmanagedType.ByValArray,SizeConst=256)] public byte[] Name;
   public int Buttons,Axes,Hats;
  }
  [StructLayout(LayoutKind.Sequential)] public struct State {
   public ulong Buttons;
   [MarshalAs(UnmanagedType.ByValArray,SizeConst=16)] public int[] Axes;
   [MarshalAs(UnmanagedType.ByValArray,SizeConst=4)] public byte[] Hats;
   public int AxisCount,HatCount;
  }
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr LoadLibrary(string path);
  [DllImport("beatles-input.dll",CallingConvention=CallingConvention.Cdecl)]static extern int BR_List([Out] NativeDevice[] devices,int capacity);
  [DllImport("beatles-input.dll",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Ansi)]static extern int BR_Read(string id,out State state);
  [DllImport("beatles-input.dll",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Ansi)]public static extern float BR_Value(ref State state,string binding);
  static readonly IntPtr library=LoadLibrary(Path.Combine(GameFiles.AppRoot,"runtime","beatles-input.dll"));
  public static string Status="Scanning for controllers…";
  static string Utf8(byte[] value){int end=Array.IndexOf(value,(byte)0);return Encoding.UTF8.GetString(value,0,end<0?value.Length:end);}
  public static List<InputDevice> List(){
   var result=new List<InputDevice>();
   if(library==IntPtr.Zero){Status="Controller support is missing. Extract the complete package.";return result;}
   var devices=new NativeDevice[32];int count=BR_List(devices,devices.Length);
   Status=count==-2?"Scanning for controllers…":count<0?"Could not initialize controller support.":count==0?"No controller connected. Connect a Windows-compatible controller or guitar.":"Select a device, then click an action to assign its button, D-pad or axis.";
   for(int i=0;i<Math.Min(count,devices.Length);i++)result.Add(new InputDevice {Id=Utf8(devices[i].Id),Name=Utf8(devices[i].Name)});
   return result;
  }
  public static bool Read(string id,out State state){state=new State();return library!=IntPtr.Zero&&BR_Read(id,out state)!=0;}
  public static Dictionary<string,string> Defaults(string instrument="guitar"){
   string[] values={"button:0","button:1","button:3","button:2","button:4","hat:0:1","hat:0:4","","button:6","button:5","button:7","hat:0:8","hat:0:2","button:0","button:1"};
   if(instrument=="drums"){values[7]="";values[9]="";}return Controls.Actions.Select((a,i)=>new{a.Id,Value=values[i]}).ToDictionary(x=>x.Id,x=>x.Value);
  }
  public static Dictionary<string,string> Normalize(Dictionary<string,string> source,string instrument="guitar"){var defaults=Defaults(instrument);return Controls.Actions.ToDictionary(a=>a.Id,a=>source!=null&&source.ContainsKey(a.Id)?source[a.Id]??"":defaults[a.Id]);}
  public static string Display(string binding){
   if(String.IsNullOrEmpty(binding))return "Unassigned";
   string[] parts=binding.Split(':');int n;if(parts.Length<2||!Int32.TryParse(parts[1],out n))return "Unassigned";
   if(parts[0]=="button")return "Button "+(n+1);
   if(parts[0]=="hat"&&parts.Length==3)return "D-pad "+(parts[2]=="1"?"↑":parts[2]=="4"?"↓":parts[2]=="8"?"←":"→")+(n==0?"":" "+(n+1));
   if(parts[0]=="axis"&&parts.Length==4)return "Axis "+(n+1)+(parts[3]=="32767"?" +":" −");
   return "Unassigned";
  }
  public static bool ValidBinding(string value){
   if(String.IsNullOrEmpty(value))return true;var p=value.Split(':');int n,rest,full;
   if(p.Length<2||!Int32.TryParse(p[1],out n)||n<0)return false;
   if(p[0]=="button")return p.Length==2&&n<64;
   if(p[0]=="hat")return p.Length==3&&n<4&&new[]{"1","2","4","8"}.Contains(p[2]);
   return p[0]=="axis"&&p.Length==4&&n<16&&Int32.TryParse(p[2],out rest)&&Int32.TryParse(p[3],out full)&&rest>=-32768&&rest<=32767&&(full==-32768||full==32767)&&rest!=full;
  }
  public static string Validate(Dictionary<string,string> bindings,string instrument="guitar"){
   var actions=Controls.ForInstrument(instrument);
   foreach(string id in new[]{"green","red","yellow","blue","orange","start"})if(String.IsNullOrEmpty(bindings[id]))return "Assign an input to "+actions.First(a=>a.Id==id).Label+".";
   if(bindings["strum_up"]==""&&bindings["strum_down"]=="")return instrument=="drums"?"Assign a menu up or down input.":"Assign at least one strum input.";
   var used=new Dictionary<string,ControlAction>();
   foreach(var a in actions){string token=bindings[a.Id];if(!ValidBinding(token))return "Assign a valid input to "+a.Label+".";if(token=="")continue;var parts=token.Split(':');string identity=parts[0]=="axis"?parts[0]+":"+parts[1]+":"+parts[3]:token;ControlAction previous;if(used.TryGetValue(identity,out previous)&&previous.Output!=a.Output)return Display(token)+" is assigned to both "+previous.Label+" and "+a.Label+".";used[identity]=a;}
   return null;
  }
  public static string Capture(State before,State now){
   ulong pressed=now.Buttons&~before.Buttons;for(int i=0;i<64;i++)if((pressed&(1ul<<i))!=0)return "button:"+i;
   for(int i=0;i<Math.Min(before.HatCount,now.HatCount);i++)foreach(int bit in new[]{1,2,4,8})if((now.Hats[i]&bit)!=0&&(before.Hats[i]&bit)==0)return "hat:"+i+":"+bit;
   for(int i=0;i<Math.Min(before.AxisCount,now.AxisCount);i++)if(Math.Abs(now.Axes[i]-before.Axes[i])>14000)return "axis:"+i+":"+before.Axes[i]+":"+(now.Axes[i]>before.Axes[i]?32767:-32768);
   return null;
  }
  public static string Serialize(Dictionary<string,string> bindings,string instrument="guitar"){string error=Validate(bindings,instrument);if(error!=null)throw new ArgumentException(error);return String.Join(";",Controls.Actions.Select(a=>a.Id+"="+(instrument=="drums"&&(a.Id=="whammy"||a.Id=="solo")?"":bindings[a.Id])));}
 }
}
