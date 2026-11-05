using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Threading;

namespace BeatlesLauncher {
 [DataContract] public sealed class DlcRecord {
  [DataMember] public string Id,Name,Sha256,ImportedUtc;
  [DataMember] public long Bytes;
  [DataMember] public int Files;
  public override string ToString(){return Name+"  ·  "+(Bytes/1048576.0).ToString("0.0")+" MB";}
 }
 [DataContract] public sealed class DlcNativeResult {
  [DataMember] public bool Ok;
  [DataMember] public string Name,Error;
  [DataMember] public long Bytes;
  [DataMember] public int Files;
 }
 public sealed class DlcImportResult {public bool AlreadyInstalled;public DlcRecord Record;}
 public static class DlcInstaller {
  static DlcInstaller(){AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling",false);AppContext.SetSwitch("Switch.System.IO.BlockLongPaths",false);}
  public static string UserDataRoot {get{return Path.Combine(GameFiles.AppRoot,"userdata");}}
  static string PackageRoot(string root){return Path.Combine(root,"0000000000000000","454108B1","00000002");}
  static string HeaderRoot(string root){return Path.Combine(root,"0000000000000000","454108B1","Headers","00000002");}
  static string IndexRoot(string root){return Path.Combine(root,".dlc-index");}
  static bool ValidId(string id){return id!=null&&id.Length==40&&id.All(c=>c>='0'&&c<='9'||c>='A'&&c<='F');}
  static string Full(string path){
   // Normalize before adding the extended prefix. Do not depend on the global
   // Windows long-path policy; native extraction already uses this same form.
   if(path.StartsWith(@"\\?\UNC\",StringComparison.OrdinalIgnoreCase))path=@"\\"+path.Substring(8);
   else if(path.StartsWith(@"\\?\",StringComparison.Ordinal))path=path.Substring(4);
   string full=Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
   return full.StartsWith(@"\\",StringComparison.Ordinal)?@"\\?\UNC\"+full.Substring(2):@"\\?\"+full;
  }
  static void NoLinks(string root,string path){
   root=Full(root);path=Full(path);
   if(path!=root&&!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("The DLC destination is outside its data folder.");
   for(var current=path;;current=Path.GetDirectoryName(current)){
    if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException("The DLC data folder contains a redirected path. Use a normal application folder.");
    if(String.Equals(current,root,StringComparison.OrdinalIgnoreCase))break;
   }
  }
  static void EnsureDirectory(string root,string path){NoLinks(root,path);Directory.CreateDirectory(path);NoLinks(root,path);}
  static string Digest(Stream stream,CancellationToken cancel){
   using(var hash=SHA256.Create()){var buffer=new byte[1024*1024];int count;while((count=stream.Read(buffer,0,buffer.Length))!=0){cancel.ThrowIfCancellationRequested();hash.TransformBlock(buffer,0,count,buffer,0);}hash.TransformFinalBlock(new byte[0],0,0);return BitConverter.ToString(hash.Hash).Replace("-","");}
  }
  static bool SameFile(string a,string b){using(var left=File.OpenRead(a))using(var right=File.OpenRead(b))return Digest(left,CancellationToken.None)==Digest(right,CancellationToken.None);}
  static void CleanScratch(string root,string scratch){
   scratch=Full(scratch);root=Full(root);
   if(!String.Equals(Path.GetDirectoryName(scratch),root,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(scratch).StartsWith(".dlc-import-",StringComparison.Ordinal))throw new IOException("Invalid temporary import folder.");
   if(!Directory.Exists(scratch))return;
   var queue=new Queue<string>();queue.Enqueue(scratch);while(queue.Count>0){string path=queue.Dequeue();NoLinks(root,path);foreach(string file in Directory.GetFiles(path))NoLinks(root,file);foreach(string sub in Directory.GetDirectories(path))queue.Enqueue(sub);}
   Directory.Delete(scratch,true);
  }
  public static List<DlcRecord> ListInstalled(string userDataRoot=null){
   var result=new List<DlcRecord>();string root=Full(userDataRoot??UserDataRoot),index=IndexRoot(root);NoLinks(root,index);
   if(!Directory.Exists(index))return result;
   foreach(var file in Directory.GetFiles(index,"*.json")){try{NoLinks(root,file);if(new FileInfo(file).Length>65536)continue;var record=GameFiles.ReadJson<DlcRecord>(file);if(record!=null&&ValidId(record.Id)&&Directory.Exists(Path.Combine(PackageRoot(root),record.Id))&&File.Exists(Path.Combine(HeaderRoot(root),record.Id+".header")))result.Add(record);}catch{}}
   return result.OrderBy(r=>r.Name,StringComparer.OrdinalIgnoreCase).ToList();
  }
  public static DlcImportResult Import(string source,CancellationToken cancel,Action<string> progress=null,string userDataRoot=null){
   string root=Full(userDataRoot??UserDataRoot),scratch=Path.Combine(root,".dlc-import-"+Guid.NewGuid().ToString("N"));
   EnsureDirectory(root,root);NoLinks(root,Path.Combine(root,".dlc-import.lock"));
   using(var importLock=new FileStream(Path.Combine(root,".dlc-import.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
   using(var package=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read)){
    cancel.ThrowIfCancellationRequested();if(progress!=null)progress("Checking "+Path.GetFileName(source)+"…");
    string hash=Digest(package,cancel),id=hash.Substring(0,40),destination=Path.Combine(PackageRoot(root),id),header=Path.Combine(HeaderRoot(root),id+".header"),index=Path.Combine(IndexRoot(root),id+".json");
    NoLinks(root,destination);NoLinks(root,header);NoLinks(root,index);
    if(Directory.Exists(destination)){
     var old=ListInstalled(root).FirstOrDefault(r=>r.Id==id);if(old==null||old.Sha256!=hash)throw new IOException("An unrecognized folder already occupies this package's location. No files were replaced.");return new DlcImportResult{AlreadyInstalled=true,Record=old};
    }
    if(File.Exists(destination))throw new IOException("A file already occupies this package's destination.");
    EnsureDirectory(root,scratch);
    try{
     string stage=Path.Combine(scratch,"content"),report=Path.Combine(scratch,"result.json"),helper=Path.Combine(GameFiles.AppRoot,"runtime","beatles-content.exe");
     if(!File.Exists(helper))throw new IOException("The DLC importer is missing. Extract the complete application package again.");
     if(progress!=null)progress("Reading and extracting DLC…");
     var start=new ProcessStartInfo(helper,String.Join(" ",new[]{Full(source),stage,id,report}.Select(GameFiles.Quote))){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(helper)};
     using(var process=Process.Start(start)){
      if(process==null)throw new IOException("Windows could not start the DLC reader.");var elapsed=Stopwatch.StartNew();
      while(!process.WaitForExit(100)){
       if(cancel.IsCancellationRequested||elapsed.Elapsed.TotalMinutes>30){try{process.Kill();process.WaitForExit();}catch{}cancel.ThrowIfCancellationRequested();throw new IOException("The DLC reader timed out. No package was installed.");}
      }
      cancel.ThrowIfCancellationRequested();
      if(!File.Exists(report))throw new IOException("The DLC reader exited without a result (code 0x"+unchecked((uint)process.ExitCode).ToString("X8")+"). Extract the complete application package with its runtime DLLs. No package was installed.");
      var native=GameFiles.ReadJson<DlcNativeResult>(report);
      if(process.ExitCode!=0||native==null||!native.Ok)throw new IOException(native==null||String.IsNullOrEmpty(native.Error)?"Could not read this DLC package.":native.Error);
      if(progress!=null)progress("Finishing DLC installation…");
      string stagedPackage=Path.Combine(PackageRoot(stage),id),stagedHeader=Path.Combine(HeaderRoot(stage),id+".header");
      if(!Directory.Exists(stagedPackage)||!File.Exists(stagedHeader))throw new IOException("The DLC reader did not finish preparing the package.");
      var record=new DlcRecord{Id=id,Sha256=hash,Name=native.Name,Files=native.Files,Bytes=native.Bytes,ImportedUtc=DateTime.UtcNow.ToString("o")};
      string stagedIndex=Path.Combine(scratch,"index.json");GameFiles.WriteJson(stagedIndex,record);
      EnsureDirectory(root,PackageRoot(root));EnsureDirectory(root,HeaderRoot(root));EnsureDirectory(root,IndexRoot(root));
      NoLinks(root,destination);NoLinks(root,header);NoLinks(root,index);cancel.ThrowIfCancellationRequested();
      bool headerAdded=false,indexAdded=false;
      try{
       // Publish the complete directory last: the game never sees half an extraction.
       // An identical orphan header/index from a power loss can be reused safely.
       if(File.Exists(header)){if(!SameFile(header,stagedHeader))throw new IOException("Conflicting DLC metadata already exists; no files were replaced.");}else{File.Move(stagedHeader,header);headerAdded=true;}
       if(File.Exists(index)){var previous=GameFiles.ReadJson<DlcRecord>(index);if(previous.Sha256!=hash)throw new IOException("Conflicting DLC import record.");}else{File.Move(stagedIndex,index);indexAdded=true;}
       Directory.Move(stagedPackage,destination);
      }catch{if(!Directory.Exists(destination)){if(headerAdded)File.Delete(header);if(indexAdded)File.Delete(index);}throw;}
      return new DlcImportResult{Record=record};
     }
    }finally{try{CleanScratch(root,scratch);}catch{}}
   }
  }
 }
}
