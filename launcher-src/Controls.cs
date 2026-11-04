using System;
using System.Collections.Generic;
using System.Linq;

namespace BeatlesLauncher {
 public sealed class ControlAction {
  public string Id, Label, Default, Output;
  public ControlAction(string id,string label,string key,string output) {Id=id;Label=label;Default=key;Output=output;}
 }
 public static class Controls {
  public static readonly ControlAction[] Actions={
   new ControlAction("green","Green / select","1","a"),
   new ControlAction("red","Red / back","2","b"),
   new ControlAction("yellow","Yellow","3","y"),
   new ControlAction("blue","Blue","4","x"),
   new ControlAction("orange","Orange","5","left_shoulder"),
   new ControlAction("strum_up","Strum up","Up","dpad_up"),
   new ControlAction("strum_down","Strum down","Down","dpad_down"),
   new ControlAction("whammy","Whammy (hold)","W","rstick_right"),
   new ControlAction("star_power","Beatlemania / Star Power","Tab","back"),
   new ControlAction("solo","Solo frets (hold)","S","right_shoulder"),
   new ControlAction("start","Start / pause","Return","start"),
   new ControlAction("menu_left","Menu: left","Left","dpad_left"),
   new ControlAction("menu_right","Menu: right","Right","dpad_right"),
   new ControlAction("accept","Alternate select","Space","a"),
   new ControlAction("cancel","Alternate back","Backspace","b")
  };
  public static readonly ControlAction[] DrumActions={
   new ControlAction("red","Red pad / snare","D","b"),
   new ControlAction("yellow","Yellow pad","F","y"),
   new ControlAction("blue","Blue pad","J","x"),
   new ControlAction("green","Green pad / select","K","a"),
   new ControlAction("orange","Kick pedal","Space","left_shoulder"),
   new ControlAction("strum_up","Menu: up","Up","dpad_up"),
   new ControlAction("strum_down","Menu: down","Down","dpad_down"),
   new ControlAction("star_power","Back button","Tab","back"),
   new ControlAction("start","Start / pause","Return","start"),
   new ControlAction("menu_left","Menu: left","Left","dpad_left"),
   new ControlAction("menu_right","Menu: right","Right","dpad_right"),
   new ControlAction("accept","Alternate select","1","a"),
   new ControlAction("cancel","Alternate back","2","b")
  };
  public static ControlAction[] ForInstrument(string instrument){return instrument=="drums"?DrumActions:Actions;}
  public static Dictionary<string,string> Defaults(string instrument="guitar") {
   var result=Actions.ToDictionary(a=>a.Id,a=>"");foreach(var a in ForInstrument(instrument))result[a.Id]=a.Default;return result;
  }
  public static Dictionary<string,string> Normalize(Dictionary<string,string> source,string instrument="guitar") {
   var defaults=Defaults(instrument);return Actions.ToDictionary(a=>a.Id,a=>source!=null&&source.ContainsKey(a.Id)?source[a.Id]??"":defaults[a.Id]);
  }
  public static string Display(string value) {
   switch(value) {
    case "":return "Unassigned";case "Up":return "↑";case "Down":return "↓";case "Left":return "←";case "Right":return "→";
    case "Return":return "Enter";case "Space":return "Space";case "Backspace":return "Backspace";
    case "Control":return "Ctrl";case "Escape":return "Esc";case "PageUp":return "Page Up";case "PageDown":return "Page Down";
    case "Delete":return "Delete";case "Insert":return "Insert";case "Home":return "Home";case "End":return "End";
   }
   return value.StartsWith("Numpad",StringComparison.Ordinal)?"Num "+value.Substring(6):value;
  }
  public static string Validate(Dictionary<string,string> input,string instrument="guitar") {
   var keys=Normalize(input,instrument);var actions=ForInstrument(instrument);
   foreach(string required in new[]{"green","red","yellow","blue","orange","start"})
    if(String.IsNullOrEmpty(keys[required]))return "Assign a key to "+actions.First(a=>a.Id==required).Label+".";
   if(String.IsNullOrEmpty(keys["strum_up"])&&String.IsNullOrEmpty(keys["strum_down"]))return instrument=="drums"?"Assign a menu up or down key.":"Assign at least one strum key.";
   var used=new Dictionary<string,ControlAction>();
   foreach(var action in actions) {
    string key=keys[action.Id];if(String.IsNullOrEmpty(key))continue;
    if(!ValidKey(key))return "The key for "+action.Label+" is unavailable. Assign it again.";
    ControlAction previous;
    if(used.TryGetValue(key,out previous)&&previous.Output!=action.Output)
     return Display(key)+" is assigned to "+previous.Label+" and "+action.Label+". Choose different keys.";
    used[key]=action;
   }
   return null;
  }
  public static bool ValidKey(string key) {
   if(key.Length==1&&((key[0]>='A'&&key[0]<='Z')||(key[0]>='0'&&key[0]<='9')))return true;
   int n;if(key.StartsWith("F")&&Int32.TryParse(key.Substring(1),out n)&&n>=1&&n<=24)return true;
   if(key.StartsWith("Numpad")&&Int32.TryParse(key.Substring(6),out n)&&n>=0&&n<=9)return true;
   return new[]{"Escape","Return","Space","Tab","Backspace","Delete","Insert","Home","End","PageUp","PageDown","Left","Right","Up","Down","Shift","Control","Alt","NumpadPlus","NumpadMinus","NumpadStar","NumpadSlash","Backtick","Minus","Plus","Comma","Period","Semicolon","Slash","Backslash","LBracket","RBracket","Quote","CapsLock","NumLock","ScrollLock","Pause","PrintScreen"}.Contains(key);
  }
  // The SDK requires an exact modifier mask. Expand only when the user assigns
  // a modifier key, so holding Shift for whammy never releases the five frets.
  public static string Expand(string key,bool modifiers) {
   if(String.IsNullOrEmpty(key)||!modifiers)return key;
   var alternatives=new List<string>();
   for(int mask=0;mask<8;mask++) {
    if(key=="Shift"&&(mask&1)==0||key=="Control"&&(mask&2)==0||key=="Alt"&&(mask&4)==0)continue;
    string prefix=((mask&1)!=0?"Shift+":"")+((mask&2)!=0?"Ctrl+":"")+((mask&4)!=0?"Alt+":"");
    alternatives.Add(prefix+key);
   }
   return String.Join(",",alternatives);
  }
  public static IEnumerable<string> Arguments(Dictionary<string,string> input,string instrument="guitar") {
   var keys=Normalize(input,instrument);if(instrument=="drums"){keys["whammy"]="";keys["solo"]="";}string problem=Validate(keys,instrument);if(problem!=null)throw new ArgumentException(problem);
   bool modifiers=keys.Values.Any(k=>k=="Shift"||k=="Control"||k=="Alt");
   foreach(var group in Actions.GroupBy(a=>a.Output)) {
    string value=String.Join(",",group.Select(a=>Expand(keys[a.Id],modifiers)).Where(k=>k!="").Distinct());
    yield return "--keybind_"+group.Key+"="+value;
   }
   // Disable unrelated SDK defaults and mouse axes; the guitar uses RX for
   // whammy and Back for energy. RY tilt stays neutral on a keyboard.
   yield return "--mnk_mouse=false";
   foreach(string unused in new[]{"left_trigger","right_trigger","lstick_up","lstick_down","lstick_left","lstick_right","lstick_press","rstick_up","rstick_down","rstick_left","rstick_press","guide"})yield return "--keybind_"+unused+"=";
  }
 }
}
