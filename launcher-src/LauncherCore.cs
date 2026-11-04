using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace BeatlesLauncher {
 [DataContract] public class Settings {
  [DataMember] public string XexPath = "";
  [DataMember] public int Width = 1280;
  [DataMember] public int Height = 720;
  [DataMember] public bool Fullscreen = false;
  [DataMember] public bool PerformanceMode = true;
  [DataMember] public int Volume = 100;
  [DataMember] public Dictionary<string,string> Keys = Controls.Defaults();
  [DataMember] public string InputDevice = "keyboard";
  [DataMember] public string Instrument = "guitar";
  [DataMember] public Dictionary<string,string> DrumKeys = Controls.Defaults("drums");
  [DataMember] public Dictionary<string,Dictionary<string,string>> DrumDeviceBindings = new Dictionary<string,Dictionary<string,string>>();
  [DataMember] public Dictionary<string,Dictionary<string,string>> DeviceBindings = new Dictionary<string,Dictionary<string,string>>();
  [OnDeserializing] void DefaultsBeforeRead(StreamingContext context){Width=1280;Height=720;Volume=100;PerformanceMode=true;InputDevice="keyboard";Instrument="guitar";}
 }
 [DataContract] public class GameFile {
  [DataMember] public string Path;
  [DataMember] public long Size;
 }
 [DataContract] public class GameManifest {
  [DataMember] public string XexSha256;
  [DataMember] public List<GameFile> Files;
 }
 public class CheckResult {
  public bool Ready;
  public string Message;
  public string Root;
  public string Hash;
 }
 public static class GameFiles {
  public static readonly string AppRoot = AppDomain.CurrentDomain.BaseDirectory;
  public static readonly string SettingsPath = Path.Combine(AppRoot, "launcher-settings.json");
  public static T ReadJson<T>(string path) {
   using (var input = File.OpenRead(path)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(input);
  }
  public static void WriteJson<T>(string path, T value) {
   string temp = path + ".tmp";
   using (var output = File.Create(temp)) new DataContractJsonSerializer(typeof(T)).WriteObject(output, value);
   if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
  }
  public static Settings LoadSettings() {
   try { var value = ReadJson<Settings>(SettingsPath); if(value != null) {value.Keys=Controls.Normalize(value.Keys);value.DrumKeys=Controls.Normalize(value.DrumKeys,"drums");value.Instrument=value.Instrument=="drums"?"drums":"guitar";if(value.DrumDeviceBindings==null)value.DrumDeviceBindings=new Dictionary<string,Dictionary<string,string>>();if(String.IsNullOrEmpty(value.InputDevice))value.InputDevice="keyboard";if(value.DeviceBindings==null)value.DeviceBindings=new Dictionary<string,Dictionary<string,string>>();return value;} } catch { }
   return new Settings();
  }
  public static CheckResult Check(string xex) {
   try {
    if (String.IsNullOrWhiteSpace(xex) || !File.Exists(xex)) return Bad("Choose default.xex from your extracted copy of the game.");
    if (!String.Equals(Path.GetFileName(xex), "default.xex", StringComparison.OrdinalIgnoreCase)) return Bad("The executable must be named default.xex inside the game folder.");
    var manifest = ReadJson<GameManifest>(Path.Combine(AppRoot, "game-files.json"));
    if (manifest == null || manifest.Files == null || String.IsNullOrWhiteSpace(manifest.XexSha256)) return Bad("The launcher verification file is missing. Extract the complete package again.");
    string hash;
    using (var sha = SHA256.Create()) using (var file = File.OpenRead(xex)) hash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
    if (!String.Equals(hash, manifest.XexSha256, StringComparison.OrdinalIgnoreCase)) return new CheckResult { Message="This XEX version is not supported by this port yet.", Hash=hash };
    string root = Path.GetDirectoryName(Path.GetFullPath(xex));
    var missing = new List<string>(); var incomplete = new List<string>();
    foreach (var item in manifest.Files) {
     var file = new FileInfo(Path.Combine(root, item.Path.Replace('/', Path.DirectorySeparatorChar)));
     if (!file.Exists) missing.Add(item.Path); else if (file.Length != item.Size) incomplete.Add(item.Path);
    }
    if (missing.Count != 0) return Bad("Missing disc files: " + String.Join(", ", missing.Take(3)) + (missing.Count > 3 ? " and others." : ".") + " Keep the XEX beside the complete gen folder.");
    if (incomplete.Count != 0) return Bad("Some files are incomplete or from another version: " + String.Join(", ", incomplete.Take(3)) + ". Check your disc extraction.");
    if (!File.Exists(RuntimeExe)) return Bad("The port executable is missing. Extract the complete package before playing.");
    return new CheckResult { Ready=true, Root=root, Hash=hash, Message="Game files found. Ready to play." };
   } catch (UnauthorizedAccessException) { return Bad("Could not read that folder. Choose a location you can access."); }
   catch (Exception error) { return Bad("Could not verify the game: " + error.Message); }
  }
  private static CheckResult Bad(string message) { return new CheckResult { Message=message }; }
  public static string RuntimeExe { get { return Path.Combine(AppRoot, "runtime", "beatlesrockband-guitar.exe"); } }
  // Windows command-line quoting, including embedded quotes and trailing slashes.
  public static string Quote(string argument) {
   var text = new StringBuilder("\""); int slashes = 0;
   foreach (char ch in argument) {
    if (ch == '\\') { slashes++; continue; }
    if (ch == '"') { text.Append('\\', slashes * 2 + 1); text.Append('"'); slashes=0; continue; }
    text.Append('\\', slashes); slashes=0; text.Append(ch);
   }
   text.Append('\\', slashes * 2); text.Append('"'); return text.ToString();
  }
  public static string Arguments(Settings settings, string root, string log) {
   string instrument=settings.Instrument=="drums"?"drums":"guitar";
   var args = new List<string> {
    "--game_data_root=" + root.Replace('\\','/'),
    "--user_data_root=" + Path.Combine(AppRoot,"userdata").Replace('\\','/'),
    "--cache_root=" + Path.Combine(AppRoot,"cache").Replace('\\','/'),
    // The bundled SDK opens narrow log paths; keep this relative to runtime.
    "--log_file=../logs/" + Path.GetFileName(log),
    "--gpu_plugin=xenos", "--windows_audio", "--audio_render_pacing",
    "--mnk_mode", "--keyboard_guitar", "--input_instrument="+instrument,
    "--window_width=" + settings.Width, "--window_height=" + settings.Height,
    "--fullscreen=" + settings.Fullscreen.ToString().ToLowerInvariant(),
    "--d3d12_nonblocking_pipeline_creation=" + settings.PerformanceMode.ToString().ToLowerInvariant(),
    "--game_volume=" + Math.Max(0, Math.Min(100, settings.Volume))
   };
   args.AddRange(Controls.Arguments(instrument=="drums"?settings.DrumKeys:settings.Keys,instrument));
   if(!String.IsNullOrEmpty(settings.InputDevice)&&settings.InputDevice!="keyboard") {
    Dictionary<string,string> bindings;
    var profiles=instrument=="drums"?settings.DrumDeviceBindings:settings.DeviceBindings;
    if(profiles==null||!profiles.TryGetValue(settings.InputDevice,out bindings))bindings=ControllerDevices.Defaults(instrument);
    args.Add("--controller_device="+settings.InputDevice);
    args.Add("--controller_bindings="+ControllerDevices.Serialize(ControllerDevices.Normalize(bindings,instrument),instrument));
   } else args.Add("--controller_device=keyboard");
   return String.Join(" ", args.Select(Quote));
  }
  public static Process Start(Settings settings, string root) {
   Directory.CreateDirectory(Path.Combine(AppRoot,"userdata"));
   Directory.CreateDirectory(Path.Combine(AppRoot,"cache"));
   Directory.CreateDirectory(Path.Combine(AppRoot,"logs"));
   string log = Path.Combine(AppRoot,"logs","game-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".log");
   WriteJson(SettingsPath, settings);
   var start = new ProcessStartInfo(RuntimeExe, Arguments(settings, root, log));
   start.WorkingDirectory = Path.GetDirectoryName(RuntimeExe); start.UseShellExecute=false;
   return Process.Start(start);
  }
 }
}
