using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using BeatlesLauncher;
using Kbd=BeatlesLauncher.Controls;
class LauncherControlsTests {
 static int checks;
 static void Check(bool value,string description){if(!value)throw new Exception(description);checks++;Console.WriteLine("PASS "+description);}
 // Independent evaluator for the SDK's exact-modifier binding grammar.
 static bool Pressed(string bind,params string[] held) {
  var down=new HashSet<string>(held);int live=(down.Contains("Shift")?1:0)|(down.Contains("Control")?2:0)|(down.Contains("Alt")?4:0);
  foreach(var token in bind.Split(',')) {var pieces=token.Split('+');int mask=0;for(int i=0;i<pieces.Length-1;i++)mask|=pieces[i]=="Shift"?1:pieces[i]=="Ctrl"?2:pieces[i]=="Alt"?4:0;if(mask==live&&down.Contains(pieces[pieces.Length-1]))return true;}
  return false;
 }
 static int Main() {
  try {
   File.WriteAllText(GameFiles.SettingsPath,"{\"XexPath\":\"C:/Mi juego/default.xex\",\"Width\":1920,\"Height\":1080,\"Fullscreen\":true,\"Volume\":73}",new UTF8Encoding(false));
   var old=GameFiles.LoadSettings();
   Check(old.Width==1920&&old.Height==1080&&old.Fullscreen&&old.Volume==73,"migration keeps display and volume");
   Check(old.XexPath=="C:/Mi juego/default.xex"&&old.Keys["green"]=="1"&&old.Keys["whammy"]=="W"&&old.Keys["star_power"]=="Tab","migration adds controls without losing XEX");
   Check(Kbd.Validate(old.Keys)==null,"default controls valid");
   old.Keys["green"]="A";old.Keys["whammy"]="Shift";old.Keys["star_power"]="V";
   GameFiles.WriteJson(GameFiles.SettingsPath,old);var loaded=GameFiles.LoadSettings();
   Check(loaded.Keys["green"]=="A"&&loaded.Keys["whammy"]=="Shift"&&loaded.Keys["star_power"]=="V","custom bindings survive save/reload");
   GameFiles.WriteJson(GameFiles.SettingsPath,loaded);Check(GameFiles.LoadSettings().Volume==73,"atomic replacement keeps other settings");
   var args=Kbd.Arguments(loaded.Keys).Select(s=>s.Split(new[]{'='},2)).ToDictionary(s=>s[0],s=>s[1]);
   File.WriteAllLines(Path.Combine(GameFiles.AppRoot,"runtime-arguments.txt"),Kbd.Arguments(loaded.Keys),new UTF8Encoding(false));
   Check(Pressed(args["--keybind_a"],"A"),"remapped green works without modifier");
   Check(Pressed(args["--keybind_a"],"A","Shift"),"green remains pressed during Shift whammy");
   Check(Pressed(args["--keybind_rstick_right"],"A","Shift"),"Shift whammy works with green");
   Check(Pressed(args["--keybind_a"],"A","Shift","Control"),"frets work with multiple modifiers");
   Check(Pressed(args["--keybind_rstick_right"],"A","Shift","Control"),"whammy works with multiple modifiers");
   Check(!Pressed(args["--keybind_rstick_right"],"A"),"whammy releases when Shift is released");
   Check(Pressed(args["--keybind_back"],"V","Shift"),"custom energy key works while whammy held");
   Check(Pressed(args["--keybind_dpad_up"],"Up","Shift"),"strum works while whammy held");
   Check(args["--keybind_rstick_left"]==""&&args["--keybind_rstick_up"]==""&&args["--keybind_right_shoulder"]!="","unused axes cleared and solo enabled");
   var duplicate=Kbd.Defaults();duplicate["whammy"]="1";Check(Kbd.Validate(duplicate)!=null,"incompatible duplicate blocked");
   duplicate=Kbd.Defaults();duplicate["green"]="Space";Check(Kbd.Validate(duplicate)==null,"same-action accept alias allowed");
   var optional=Kbd.Defaults();optional["whammy"]="";optional["solo"]="";Check(Kbd.Validate(optional)==null&&Kbd.Arguments(optional).Contains("--keybind_rstick_right="),"clearing optional action overrides old TOML binding");
   var missing=Kbd.Defaults();missing["strum_up"]="";missing["strum_down"]="";Check(Kbd.Validate(missing)!=null,"cannot save without any strum key");
   missing=Kbd.Defaults();missing["green"]="";Check(Kbd.Validate(missing)!=null,"required fret cannot be unassigned");
   Check(ControlsForm.KeyName(Keys.D1)=="1"&&ControlsForm.KeyName(Keys.NumPad1)=="Numpad1","top-row and numpad keys stay distinct");
   Check(ControlsForm.KeyName(Keys.LShiftKey)=="Shift"&&ControlsForm.KeyName(Keys.RControlKey)=="Control","modifier capture normalized");
   Check(ControlsForm.KeyName(Keys.Enter)=="Return"&&ControlsForm.KeyName(Keys.Back)=="Backspace"&&ControlsForm.KeyName(Keys.Up)=="Up","special key capture uses runtime names");
   Check(ControlsForm.KeyName(Keys.OemQuotes)=="Quote"&&Kbd.ValidKey(ControlsForm.KeyName(Keys.OemQuotes)),"punctuation capture accepted");
   var defaults=Kbd.Arguments(Kbd.Defaults()).ToArray();Check(defaults.Contains("--keybind_a=1,Space")&&defaults.Contains("--keybind_b=2,Backspace"),"original 1-5 menu aliases preserved");
   Check(defaults.Contains("--keybind_rstick_right=W")&&defaults.Contains("--keybind_back=Tab"),"whammy and energy route to guitar channels");
   Check(Kbd.Normalize(new Dictionary<string,string>{{"whammy","F8"}})["green"]=="1","partial settings add missing actions");
   Console.WriteLine(checks+" checks passed");return 0;
  }catch(Exception error){Console.WriteLine(error);return 1;}
 }
}
