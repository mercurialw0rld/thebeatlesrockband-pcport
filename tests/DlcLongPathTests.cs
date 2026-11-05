using System;
using System.IO;
using System.Linq;
using System.Threading;
using BeatlesLauncher;
class DlcLongPathTests {
 static int checks;
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static int Main(string[] args){try{
  // Exercise Path before DlcInstaller's static constructor, as the real UI does.
  string root=Path.Combine(GameFiles.AppRoot,"long-"+new string('p',140));
  var result=DlcInstaller.Import(Path.Combine(args[0],"generic-good"),CancellationToken.None,null,root);
  string header=Path.Combine(root,"0000000000000000","454108B1","Headers","00000002",result.Record.Id+".header");
  Check(header.Length>260,"regression fixture really exceeds MAX_PATH");
  Check(File.Exists(@"\\?\"+header),"completed native header exists at a long path");
  Check(DlcInstaller.ListInstalled(root).Count==1,"long-path import is indexed");
  Check(DlcInstaller.Import(Path.Combine(args[0],"generic-good"),CancellationToken.None,null,root).AlreadyInstalled,"long-path duplicate recognized");
  Check(!Directory.GetDirectories(root,".dlc-import-*").Any(),"long-path scratch is cleaned");
  Console.WriteLine(checks+" long-path checks passed");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
