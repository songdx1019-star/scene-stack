using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.Diagnostics;
using System.Runtime.InteropServices;
class Tests {
  static int count;
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 static void Check(bool value,string name){if(!value)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
 static Scene S(string id,int handle,string title,bool done,bool pin){return new Scene{Id=id,Handle=handle,ProcessId=123,Title=title,Project="",Note="",ImagePath="",Done=done,Pinned=pin,Time=DateTime.Now};}
 [STAThread] static void Main(string[] args){
    if(args.Length>0&&args[0]=="--target"){
   Application.EnableVisualStyles();var targetA=new Form{Text="SceneStack integration A",Size=new Size(500,300),StartPosition=FormStartPosition.Manual,Location=new Point(20,20)};var targetB=new Form{Text="SceneStack integration B",Size=new Size(500,300),StartPosition=FormStartPosition.Manual,Location=new Point(560,20)};
   targetA.Shown+=delegate{targetB.Show();File.WriteAllText(args[1],targetA.Handle.ToInt64()+","+targetB.Handle.ToInt64());};var timeout=new System.Windows.Forms.Timer{Interval=30000};timeout.Tick+=delegate{targetB.Close();targetA.Close();};timeout.Start();Application.Run(targetA);return;
  }
  var a=S("a",1,"文档A",false,true);a.Project="甲项目";a.Note="核对数字";a.ProcessStartTicks=10;
  var b=S("b",2,"文档B",true,false);var c=S("c",3,"文档C",false,false);
  var scenes=new List<Scene>{a,b,c};
  Check(SceneLogic.Eviction(scenes)==b,"evict completed before unfinished; preserve pin");b.Pinned=true;Check(SceneLogic.Eviction(scenes)==c,"evict oldest unpinned");c.Pinned=true;Check(SceneLogic.Eviction(scenes)==null,"all pinned blocks eviction");
  var replacement=S("x",1,"文档A",false,false);replacement.ProcessStartTicks=10;Check(SceneLogic.Existing(scenes,replacement)==a,"match same unresolved window");replacement.ProcessStartTicks=11;Check(SceneLogic.Existing(scenes,replacement)==null,"reject recycled process identity");replacement.ProcessStartTicks=0;Check(SceneLogic.Existing(scenes,replacement)==a,"compatible legacy process identity");a.Done=true;Check(SceneLogic.Existing(scenes,replacement)==null,"completed scene is not overwritten");a.Done=false;
  Check(SceneLogic.ProjectFor(scenes,"文档A")=="甲项目","inherit exact title project");Check(SceneLogic.ProjectFor(scenes,"文档B")=="","different title stays unclassified");
  Check(SceneLogic.Filter(scenes,"核对","甲项目",true).Count==1,"combine note and project filtering");Check(SceneLogic.Filter(scenes,"","",true).Count==2,"unfinished filter");Check(SceneLogic.Filter(scenes,"文档B","",true).Count==0,"completed hidden in unfinished filter");
  string legacy="<ArrayOfScene><Scene><Id>old</Id><Title>旧文档</Title><Time>2026-10-10T00:00:00</Time><Handle>1</Handle><ProcessId>1</ProcessId><Done>false</Done></Scene></ArrayOfScene>";
  List<Scene> migrated;using(var reader=new StringReader(legacy))migrated=(List<Scene>)new XmlSerializer(typeof(List<Scene>)).Deserialize(reader);Check(migrated.Count==1&&!migrated[0].Pinned&&migrated[0].ProcessStartTicks==0,"read first version XML");
  string temp=Path.Combine(Path.GetTempPath(),"SceneStackTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
  Assembly asm=typeof(Scene).Assembly;Type widgetType=asm.GetType("Widget");widgetType.GetField("Root").SetValue(null,temp);
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  var widget=(Form)Activator.CreateInstance(widgetType);var live=(List<Scene>)widgetType.GetField("Scenes").GetValue(widget);var settings=(AppSettings)widgetType.GetField("Settings").GetValue(widget);settings.TitleOnly=true;Check(widget.Width==48&&widget.Height==48,"widget remains 48 by 48");
  Check(Brand.Mark.Width==32&&Brand.Mark.Height==32&&Brand.Mark.GetPixel(0,0).A==0,"transparent small logo embedded");Check(Brand.AppIcon.Width==32&&Brand.AppIcon.Height==32,"application icon embedded");using(var widgetImage=new Bitmap(48,48)){widget.DrawToBitmap(widgetImage,new Rectangle(0,0,48,48));widgetImage.Save(Path.Combine(Path.GetDirectoryName(args[0]),"assets","widget-preview.png"));}
  var capture=widgetType.GetMethod("CaptureItem",BindingFlags.NonPublic|BindingFlags.Instance);
  a.Pinned=true;a.Done=false;live.Add(a);replacement.ProcessStartTicks=10;replacement.Time=DateTime.Now;
  Check((bool)capture.Invoke(widget,new object[]{replacement}),"record title-only scene without screenshot");Check(live.Count==1&&live[0].Project=="甲项目"&&live[0].Note=="核对数字"&&live[0].Pinned,"merge preserves project note and pin");
  using(var file=File.OpenRead(Path.Combine(temp,"scenes.xml"))){var saved=(List<Scene>)new XmlSerializer(typeof(List<Scene>)).Deserialize(file);Check(saved.Count==1&&saved[0].Id==replacement.Id,"merged scene persisted");}
  live.Clear();for(int i=0;i<40;i++)live.Add(S(i.ToString(),i+5,"项目文档 "+i,false,true));Check(!(bool)capture.Invoke(widget,new object[]{S("overflow",99,"额外文档",false,false)})&&live.Count==40,"full pinned store refuses new capture");
  live[39].Pinned=false;live[39].Done=true;Check((bool)capture.Invoke(widget,new object[]{S("new",99,"新文档",false,false)})&&live.Count==40&&live[0].Id=="new","capacity stays bounded");
  live.Clear();live.Add(a);live.Add(b);live.Add(c);
  Type panelType=asm.GetType("PanelForm");var panel=(Form)Activator.CreateInstance(panelType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{widget},null);
  panel.Show();Application.DoEvents();panel.Size=panel.MinimumSize;using(var compact=new Bitmap(panel.Width,panel.Height)){panel.DrawToBitmap(compact,new Rectangle(Point.Empty,compact.Size));compact.Save(Path.Combine(Path.GetDirectoryName(args[0]),"紧凑界面预览.png"));}panel.Size=new Size(1020,720);using(var bmp=new Bitmap(panel.Width,panel.Height)){panel.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save(args[0]);}
  var note=(TextBox)panelType.GetField("note",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(panel);note.Text="自动保存测试";Check((bool)panelType.GetMethod("Commit").Invoke(panel,null)&&a.Note=="自动保存测试","panel commits draft note");
    panel.Close();panel.Dispose();
  string handles=Path.Combine(temp,"target-handles.txt");var start=new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"--target \""+handles+"\"");start.UseShellExecute=false;
  using(var target=Process.Start(start)){
   try{
    for(int i=0;i<100&&!File.Exists(handles);i++){System.Threading.Thread.Sleep(50);Application.DoEvents();}
    Check(File.Exists(handles),"external test windows started");var parts=File.ReadAllText(handles).Split(',');long ha=long.Parse(parts[0]),hb=long.Parse(parts[1]);
    live.Clear();settings.TitleOnly=false;var sa=S("ia",(int)ha,"SceneStack integration A",false,false);sa.ProcessId=target.Id;sa.Handle=ha;sa.ProcessStartTicks=target.StartTime.ToUniversalTime().Ticks;
    var sb=S("ib",(int)hb,"SceneStack integration B",false,false);sb.ProcessId=target.Id;sb.Handle=hb;sb.ProcessStartTicks=sa.ProcessStartTicks;
    Check((bool)capture.Invoke(widget,new object[]{sa}),"real visible window screenshot captured");using(var screenshot=Image.FromFile(live[0].ImagePath))Check(screenshot.Width>0&&screenshot.Width<=1400,"screenshot decodes and is bounded");
    Check((bool)capture.Invoke(widget,new object[]{sb}),"second window screenshot captured");
    SetForegroundWindow(new IntPtr(hb));System.Threading.Thread.Sleep(180);Application.DoEvents();Check(GetForegroundWindow().ToInt64()==hb,"external B has foreground");
    try { widgetType.GetMethod("QuickReturn").Invoke(widget,null); } catch(Exception ex) { Console.WriteLine("Invoke failure: "+ex.GetType().Name+" "+ex.Message); if(ex.InnerException!=null)Console.WriteLine("Inner: "+ex.InnerException.GetType().Name+" "+ex.InnerException.Message); throw; } System.Threading.Thread.Sleep(180); Application.DoEvents(); Console.WriteLine("Focus actual="+GetForegroundWindow().ToInt64()+" expected="+ha);Check(GetForegroundWindow().ToInt64()==ha,"quick return switches B to A");Check(live.Count==2&&live[0].Title=="SceneStack integration B","quick return preserves and merges interrupted B");
    widgetType.GetMethod("QuickReturn").Invoke(widget,null);System.Threading.Thread.Sleep(180); Application.DoEvents();Check(GetForegroundWindow().ToInt64()==hb,"quick return switches back A to B");Check(live.Count==2&&live[0].Title=="SceneStack integration A","quick return alternation keeps two scenes");
   }finally{target.CloseMainWindow();if(!target.WaitForExit(3000))target.Kill();}
  }
  widget.Close();widget.Dispose();Console.WriteLine("TOTAL "+count+" checks passed");Console.WriteLine("Temporary data: "+temp);
 }
}
