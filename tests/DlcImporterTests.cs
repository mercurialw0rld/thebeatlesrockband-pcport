using System;
using System.IO;
using System.Linq;
using System.Threading;
using BeatlesLauncher;
class DlcImporterTests {
 static int checks;
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
 static int Main(string[] args){try{
  string fixtures=args[0],root=Path.Combine(GameFiles.AppRoot,"import-tests","good");
  var success=DlcInstaller.Import(Path.Combine(fixtures,"good"),CancellationToken.None,null,root);
  Check(!success.AlreadyInstalled&&success.Record.Files==2,"synthetic STFS package extracted");
  Check(File.ReadAllText(Path.Combine(root,"0000000000000000","454108B1","00000002",success.Record.Id,"songs","songs.dta")).Contains("synthetic importer test"),"extracted bytes are exact");
  Check(DlcInstaller.ListInstalled(root).Count==1,"installed package is indexed");
  string alias=Path.Combine(GameFiles.AppRoot,"renamed-fixture");File.Copy(Path.Combine(fixtures,"good"),alias,true);
  Check(DlcInstaller.Import(alias,CancellationToken.None,null,root).AlreadyInstalled,"renamed duplicate is detected without overwriting");
  var custom=DlcInstaller.Import(Path.Combine(fixtures,"generic-good"),CancellationToken.None,null,Path.Combine(GameFiles.AppRoot,"import-tests","custom"));
  Check(custom.Record.Files==4,"generic-title TBRB catalog with matching assets accepted");
  Check(DlcInstaller.ListInstalled(Path.Combine(GameFiles.AppRoot,"import-tests","custom")).Count==1,"verified custom is indexed under TBRB");
  foreach(string name in new[]{"wrong-title","save-game","unsafe-path","executable","truncated","bad-magic","generic-other-origin","other-title-beatles-catalog","generic-missing-milo","generic-bad-midi","generic-comment-spoof","generic-string-spoof"}){
   string target=Path.Combine(GameFiles.AppRoot,"import-tests",name);bool rejected=false;
   try{DlcInstaller.Import(Path.Combine(fixtures,name),CancellationToken.None,null,target);}catch(IOException e){rejected=true;Console.WriteLine("REJECT "+name+": "+e.Message);}
   Check(rejected,"rejects "+name);Check(DlcInstaller.ListInstalled(target).Count==0,"failed import not published: "+name);Check(!Directory.GetDirectories(target,".dlc-import-*").Any(),"failed staging removed: "+name);
  }
  var cancelled=new CancellationTokenSource();cancelled.Cancel();bool cancelledOk=false;try{DlcInstaller.Import(Path.Combine(fixtures,"good"),cancelled.Token,null,Path.Combine(GameFiles.AppRoot,"import-tests","cancelled"));}catch(OperationCanceledException){cancelledOk=true;}
  Check(cancelledOk,"cancellation does not install a package");
  string incomplete=Path.Combine(GameFiles.AppRoot,"import-tests","interrupted");Directory.CreateDirectory(Path.Combine(incomplete,"0000000000000000","454108B1","Headers","00000002"));
  File.Copy(Path.Combine(root,"0000000000000000","454108B1","Headers","00000002",success.Record.Id+".header"),Path.Combine(incomplete,"0000000000000000","454108B1","Headers","00000002",success.Record.Id+".header"));
  Check(!DlcInstaller.Import(Path.Combine(fixtures,"good"),CancellationToken.None,null,incomplete).AlreadyInstalled,"recovers matching orphan metadata after an interrupted commit");
  if(args.Length>1){foreach(string package in Directory.GetFiles(args[1])){var installed=DlcInstaller.Import(package,CancellationToken.None,Console.WriteLine);Check(!installed.AlreadyInstalled,"real package: "+installed.Record.Name+" ("+installed.Record.Files+" files)");}Check(DlcInstaller.ListInstalled().Count==4,"all four supplied DLC packages indexed in isolated userdata");}
  Console.WriteLine(checks+" DLC checks passed");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
