using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using BeatlesLauncher;
class ControllerMappingTests {
 static int checks;
 static void Check(bool result,string label){if(!result)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static int Main(){try{
  var settings=GameFiles.LoadSettings();Check(settings.InputDevice=="keyboard"&&settings.DeviceBindings!=null,"old settings migrate to keyboard");
  var mappings=ControllerDevices.Defaults();Check(ControllerDevices.Validate(mappings)==null,"controller defaults valid");
  settings.InputDevice="test-guitar";settings.DeviceBindings[settings.InputDevice]=mappings;settings.Keys["whammy"]="Numpad0";
  mappings["whammy"]="axis:2:-32768:32767";mappings["star_power"]="button:12";
  GameFiles.WriteJson(GameFiles.SettingsPath,settings);settings=GameFiles.LoadSettings();Check(settings.InputDevice=="test-guitar"&&settings.DeviceBindings["test-guitar"]["star_power"]=="button:12"&&settings.Keys["whammy"]=="Numpad0","device profile and keyboard survive save/reload");
  var args=GameFiles.Arguments(settings,"C:/Legal game","test.log");Check(args.Contains("--controller_device=test-guitar")&&args.Contains("whammy=axis:2:-32768:32767"),"physical mapping reaches runtime arguments");
  settings.InputDevice="keyboard";var keyboardArgs=GameFiles.Arguments(settings,"C:/Legal game","test.log");Check(keyboardArgs.Contains("--controller_device=keyboard")&&!keyboardArgs.Contains("\"--controller_device=\""),"keyboard uses nonempty device selector accepted by SDK");
  var before=new ControllerDevices.State {Axes=new int[16],Hats=new byte[4],AxisCount=3,HatCount=1};var now=before;now.Axes=(int[])before.Axes.Clone();now.Hats=(byte[])before.Hats.Clone();
  now.Buttons=1ul<<33;Check(ControllerDevices.Capture(before,now)=="button:33","captures buttons above 32");
  now.Buttons=0;now.Hats[0]=4;Check(ControllerDevices.Capture(before,now)=="hat:0:4","captures strum D-pad down");
  now.Hats[0]=0;before.Axes[2]=-32768;now.Axes[2]=1000;Check(ControllerDevices.Capture(before,now)=="axis:2:-32768:32767","captures analog whammy rest and direction");
  var bad=ControllerDevices.Defaults();bad["star_power"]="button:0";Check(ControllerDevices.Validate(bad)!=null,"rejects conflicting controller buttons");
  foreach(string token in new[]{"axis:99:0:32767","axis:1:32767:32767","hat:0:99","button:64","button:-1","key:5"})Check(!ControllerDevices.ValidBinding(token),"rejects invalid binding "+token);
  for(int i=0;i<10;i++){ControllerDevices.List();Thread.Sleep(100);}Console.WriteLine("DEVICES "+ControllerDevices.List().Count+" / "+ControllerDevices.Status);
  Check(ControllerDevices.BR_Value(ref now,"axis:2:-32768:32767")>.5f,"native analog mapper receives C# state correctly");
  now.Axes[2]=-32768;Check(ControllerDevices.BR_Value(ref now,"axis:2:-32768:32767")==0,"whammy rests at zero");now.Axes[2]=32767;Check(ControllerDevices.BR_Value(ref now,"axis:2:-32768:32767")==1,"whammy reaches full range");
  now.Axes[2]=0;float middle=ControllerDevices.BR_Value(ref now,"axis:2:-32768:32767");Check(middle>.49f&&middle<.51f,"whammy preserves analog midpoint");
  now.Buttons=1ul<<33;Check(ControllerDevices.BR_Value(ref now,"button:33")==1,"native high-button mapping");now.Hats[0]=5;Check(ControllerDevices.BR_Value(ref now,"hat:0:1")==1&&ControllerDevices.BR_Value(ref now,"hat:0:4")==1,"native diagonal D-pad handles both directions");
  Console.WriteLine(checks+" controller checks passed");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
