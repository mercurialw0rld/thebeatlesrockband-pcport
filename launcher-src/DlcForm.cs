using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace BeatlesLauncher {
 public sealed class DlcForm : ResponsiveWindow {
  Label title,hint,status;ListBox installed;Button add,close;ProgressBar progress;AeroPane body;CancellationTokenSource cancellation;bool busy;readonly bool preview;
  public DlcForm(bool rendering=false){
   preview=rendering;ShowInTaskbar=!rendering;Text="Import DLC · The Beatles: Rock Band";ClientSize=new Size(760,560);WindowButtons(false);StartPosition=FormStartPosition.CenterParent;
   body=new AeroPane{AutoScroll=true};Controls.Add(body);
   title=Aero.Label(body,"Downloadable content",Rectangle.Empty,Aero.Display(27),Aero.Ink);
   hint=Aero.Label(body,"Import Xbox 360 DLC packages from your own copy. Select the original LIVE, PIRS or CON files, including files with no extension. Extract ZIP/RAR archives first. Restart the game after importing and allow its song cache to update.",Rectangle.Empty,Aero.UI(10),Aero.Muted);
   installed=new ListBox{Font=Aero.UI(10),BackColor=Aero.Cream,ForeColor=Aero.Ink,BorderStyle=BorderStyle.FixedSingle,HorizontalScrollbar=true,IntegralHeight=false};body.Controls.Add(installed);
   status=Aero.Label(body,"",Rectangle.Empty,Aero.UI(10),Aero.Muted);
   progress=new ProgressBar{Visible=false,Style=ProgressBarStyle.Marquee};body.Controls.Add(progress);
   add=Aero.Button("Choose DLC files…",Rectangle.Empty,true);add.Click+=Choose;Controls.Add(add);
   close=Aero.Button("Close",Rectangle.Empty);close.Click+=delegate{if(busy)cancellation.Cancel();else Close();};Controls.Add(close);CancelButton=close;
   FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy){e.Cancel=true;cancellation.Cancel();status.Text="Cancelling the current import…";}};
   RefreshInstalled();RefreshLayout();
  }
  protected override Size PreferredSizeLogical {get{return new Size(760,600);}}
  protected override bool ShowWithoutActivation {get{return preview;}}
  protected override void LayoutContent(){
   if(close==null)return;ChromeLayout();int pad=S(24),w=ClientSize.Width-pad*2,y=0,bottom=ClientSize.Height-pad;
   int closeWidth=Math.Min(S(128),(w-S(16))*40/100),addWidth=w-S(16)-closeWidth;
   Place(add,pad,bottom-S(42),addWidth,S(42));Place(close,pad+addWidth+S(16),bottom-S(42),closeWidth,S(42));
   Place(body,pad,S(48),w,Math.Max(S(80),bottom-S(108)));int bw=w-SystemInformation.VerticalScrollBarWidth-S(4);
   int th=TextHeight(title,bw,48);Place(title,0,y,bw,th);y+=th+S(12);int hh=TextHeight(hint,bw,72);Place(hint,0,y,bw,hh);y+=hh+S(16);
   int sh=TextHeight(status,bw,54),lh=Math.Max(S(130),body.ClientSize.Height-y-sh-S(44));Place(installed,0,y,bw,lh);y+=lh+S(12);
   Place(progress,0,y,bw,S(12));y+=S(22);Place(status,0,y,bw,sh);body.AutoScrollMinSize=new Size(0,y+sh+S(8));
  }
  void RefreshInstalled(){
   installed.Items.Clear();try{var records=DlcInstaller.ListInstalled();foreach(var record in records)installed.Items.Add(record);status.Text=records.Count==0?"No DLC imported yet.":records.Count+" DLC package(s) installed. Existing packages are never replaced.";}catch(Exception e){status.Text=e.Message;}
  }
  async void Choose(object sender,EventArgs args){
   using(var dialog=new OpenFileDialog{Title="Choose The Beatles: Rock Band Xbox 360 DLC packages",Filter="Xbox 360 DLC packages (all files)|*.*",CheckFileExists=true,Multiselect=true}){
    if(dialog.ShowDialog(this)!=DialogResult.OK)return;
    busy=true;cancellation=new CancellationTokenSource();add.Enabled=false;close.Text="Cancel import";progress.Visible=true;int imported=0,skipped=0,failed=0;string lastError=null;
    var feedback=new Progress<string>(text=>{if(!IsDisposed){status.Text=text;RefreshLayout();}});
    try{
     foreach(string file in dialog.FileNames){
      try{var result=await Task.Run(()=>DlcInstaller.Import(file,cancellation.Token,text=>((IProgress<string>)feedback).Report(text)));if(result.AlreadyInstalled)skipped++;else imported++;}
      catch(OperationCanceledException){lastError="Import cancelled. Completed packages were kept.";break;}
      catch(Exception e){failed++;lastError=Path.GetFileName(file)+": "+e.Message;}
     }
    }finally{busy=false;cancellation.Dispose();cancellation=null;progress.Visible=false;add.Enabled=true;close.Text="Close";RefreshInstalled();status.Text=imported+" imported · "+skipped+" already installed · "+failed+" failed. "+(lastError??"Restart the game to scan the new songs.");RefreshLayout();}
   }
  }
 }
}
