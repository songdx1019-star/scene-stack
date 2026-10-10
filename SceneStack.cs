using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;

public class Scene {
 public string Id, Title, Project, Note, ImagePath;
 public DateTime Time;
 public long Handle, ProcessStartTicks;
 public int ProcessId;
 public bool Done, Pinned;
 public override string ToString() { return (Pinned ? "★ " : "") + (Done ? "✓ " : "↩ ") + Time.ToString("MM-dd HH:mm") + "  " + (String.IsNullOrEmpty(Project) ? "未归类" : Project) + " · " + Title; }
}
public class AppSettings {
 public int X=int.MinValue, Y=int.MinValue;
 public bool AlternateKeys, TitleOnly;
}
public static class SceneLogic {
 public const int Limit=40;
 public static bool SameWindow(Scene a, Scene b) {
  return a.Handle==b.Handle && a.ProcessId==b.ProcessId && a.Title==b.Title &&
   (a.ProcessStartTicks==0 || b.ProcessStartTicks==0 || a.ProcessStartTicks==b.ProcessStartTicks);
 }
 public static string ProjectFor(List<Scene> scenes,string title) {
  foreach(var s in scenes) if(s.Title==title && !String.IsNullOrEmpty(s.Project)) return s.Project;
  return "";
 }
 public static Scene Existing(List<Scene> scenes, Scene item) {return scenes.Find(delegate(Scene s){return !s.Done && SameWindow(s,item);});}
 public static Scene Eviction(List<Scene> scenes) {
  for(int i=scenes.Count-1;i>=0;i--) if(!scenes[i].Pinned && scenes[i].Done) return scenes[i];
  for(int i=scenes.Count-1;i>=0;i--) if(!scenes[i].Pinned) return scenes[i];
  return null;
 }
 public static List<Scene> Filter(List<Scene> scenes,string query,string project,bool unfinished) {
  var result=new List<Scene>(); foreach(var s in scenes) if((!unfinished||!s.Done) &&
   (String.IsNullOrEmpty(project)||s.Project==project) &&
   ((s.Title+" "+s.Project+" "+s.Note).IndexOf(query??"",StringComparison.OrdinalIgnoreCase)>=0)) result.Add(s);
  return result;
 }
}
static class Native {
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
 [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int n);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
 [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint m,uint k);
 [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
 public struct RECT { public int Left,Top,Right,Bottom; }
}
class Widget : Form {
 public static string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SceneStack");
 public List<Scene> Scenes=new List<Scene>();
 public AppSettings Settings=new AppSettings();
 NotifyIcon tray; PanelForm panel; ToolTip tip=new ToolTip(); Point dragOrigin,windowOrigin; bool moved;
 Font glyph=new Font("Segoe UI",17); System.Windows.Forms.Timer feedback=new System.Windows.Forms.Timer();
 public string Keys {get {return Settings.AlternateKeys ? "Ctrl+Shift+" : "Ctrl+Alt+";}}
 public Widget() {
  Directory.CreateDirectory(Root);Scenes=ReadData<List<Scene>>("scenes.xml",Scenes);Settings=ReadData<AppSettings>("settings.xml",Settings);
  FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;MinimumSize=new Size(48,48);MaximumSize=new Size(48,48);Size=new Size(48,48);
  BackColor=Color.FromArgb(39,57,75);ForeColor=Color.White;StartPosition=FormStartPosition.Manual;
  var a=Screen.PrimaryScreen.WorkingArea;Location=Settings.X==int.MinValue ? new Point(a.Right-65,a.Top+a.Height/2) : new Point(Settings.X,Settings.Y);ClampPosition();
  MouseDown+=delegate(object s,MouseEventArgs e){dragOrigin=Cursor.Position;windowOrigin=Location;moved=false;};
  MouseMove+=delegate(object s,MouseEventArgs e){if(e.Button==MouseButtons.Left){var delta=new Size(Cursor.Position.X-dragOrigin.X,Cursor.Position.Y-dragOrigin.Y);if(moved||Math.Abs(delta.Width)>4||Math.Abs(delta.Height)>4){moved=true;Location=windowOrigin+delta;}}};
  MouseUp+=delegate(object s,MouseEventArgs e){if(moved){ClampPosition();StorePosition();}else if(e.Button==MouseButtons.Left) CaptureScene();else if(e.Button==MouseButtons.Right)OpenPanel();else if(e.Button==MouseButtons.Middle)QuickReturn();};
  tray=new NotifyIcon {Icon=SystemIcons.Application,Visible=true,Text="现场栈"};
  var menu=new ContextMenuStrip();menu.Items.Add("查看现场栈",null,delegate{OpenPanel();});menu.Items.Add("留现场并返回上一项",null,delegate{QuickReturn();});menu.Items.Add("设置",null,delegate{OpenSettings();});menu.Items.Add("退出",null,delegate{Close();});tray.ContextMenuStrip=menu;tray.DoubleClick+=delegate{OpenPanel();};
  feedback.Interval=850;feedback.Tick+=delegate{feedback.Stop();BackColor=Color.FromArgb(39,57,75);};UpdateTip();
 }
 protected override bool ShowWithoutActivation {get{return true;}}
 protected override CreateParams CreateParams {get{var c=base.CreateParams;c.ExStyle|=0x08000000;return c;}}
 void ClampPosition(){var a=Screen.FromPoint(Location).WorkingArea;Location=new Point(Math.Max(a.Left,Math.Min(a.Right-Width,Left)),Math.Max(a.Top,Math.Min(a.Bottom-Height,Top)));}
 void StorePosition(){Settings.X=Left;Settings.Y=Top;Write("settings.xml",Settings);}
 void UpdateTip(){tip.SetToolTip(this,"单击留现场 · 右键查看 · 中键返回\n"+Keys+"空格 留现场\n"+Keys+"B 查看 · "+Keys+"R 留现场并返回");}
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);RegisterKeys();}
 void RegisterKeys(){for(int i=1;i<=3;i++)Native.UnregisterHotKey(Handle,i);uint mod=Settings.AlternateKeys?0x4006u:0x4003u;bool a=Native.RegisterHotKey(Handle,1,mod,0x20),b=Native.RegisterHotKey(Handle,2,mod,0x42),c=Native.RegisterHotKey(Handle,3,mod,0x52);if(!a||!b||!c)Notify("快捷键冲突","可以用挂件操作，或在设置中换另一组快捷键。");UpdateTip();}
 protected override void WndProc(ref Message m){if(m.Msg==0x312){int id=m.WParam.ToInt32();if(id==1)CaptureScene();else if(id==2)OpenPanel();else if(id==3)QuickReturn();}base.WndProc(ref m);}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);int n=Scenes.FindAll(delegate(Scene s){return !s.Done;}).Count;TextRenderer.DrawText(e.Graphics,"↩",glyph,new Rectangle(0,0,48,30),ForeColor,TextFormatFlags.HorizontalCenter);TextRenderer.DrawText(e.Graphics,n.ToString(),Font,new Rectangle(0,30,48,18),ForeColor,TextFormatFlags.HorizontalCenter);}
 void Notify(string title,string text){tray.ShowBalloonTip(3000,title,text,ToolTipIcon.Info);}
 static long ProcessStamp(int id){try{using(var p=Process.GetProcessById(id))return p.StartTime.ToUniversalTime().Ticks;}catch{return 0;}}
 Scene Foreground(){IntPtr h=Native.GetForegroundWindow();uint pid;Native.GetWindowThreadProcessId(h,out pid);if(h==IntPtr.Zero||pid==(uint)Process.GetCurrentProcess().Id)return null;var title=new StringBuilder(512);Native.GetWindowText(h,title,512);if(title.Length==0)return null;return new Scene {Id=Guid.NewGuid().ToString("N"),Title=title.ToString(),Time=DateTime.Now,Handle=h.ToInt64(),ProcessId=(int)pid,ProcessStartTicks=ProcessStamp((int)pid),Project="",Note="",ImagePath=""};}
 public bool CaptureScene(){var item=Foreground();if(item==null){Notify("先回到工作窗口","回到 WPS 或消息窗口，再按 "+Keys+"空格。");return false;}return CaptureItem(item);}
 bool CaptureItem(Scene item){
  if(panel!=null&&!panel.IsDisposed&&!panel.Commit())return false;
  var old=SceneLogic.Existing(Scenes,item);var victim=old==null&&Scenes.Count>=SceneLogic.Limit?SceneLogic.Eviction(Scenes):null;
  if(old==null&&Scenes.Count>=SceneLogic.Limit&&victim==null){Notify("收藏已满","请取消一个收藏或删除一个现场，再继续记录。");return false;}
  item.Project=old==null?SceneLogic.ProjectFor(Scenes,item.Title):old.Project;item.Note=old==null?"":old.Note;item.Pinned=old!=null&&old.Pinned;
  try {
   if(!Settings.TitleOnly){Native.RECT r;if(!Native.GetWindowRect(new IntPtr(item.Handle),out r))throw new Exception("无法读取窗口范围");var rect=Rectangle.Intersect(Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom),SystemInformation.VirtualScreen);if(rect.Width<=0||rect.Height<=0)throw new Exception("窗口当前不可见");
    // Hide only our non-activating widget during this user-triggered capture.
    bool visible=Visible;if(visible)Hide();try{using(var bmp=new Bitmap(rect.Width,rect.Height)){using(var g=Graphics.FromImage(bmp))g.CopyFromScreen(rect.Location,Point.Empty,rect.Size);double scale=Math.Min(1.0,1400.0/bmp.Width);using(var small=new Bitmap(bmp,new Size(Math.Max(1,(int)(bmp.Width*scale)),Math.Max(1,(int)(bmp.Height*scale))))){item.ImagePath=Path.Combine(Root,item.Id+".jpg");small.Save(item.ImagePath,ImageFormat.Jpeg);}}}finally{if(visible)Show();}
   }
   var previous=new List<Scene>(Scenes);if(old!=null)Scenes.Remove(old);if(victim!=null)Scenes.Remove(victim);Scenes.Insert(0,item);
   try{Save();}catch{Scenes=previous;DeleteImage(item);throw;}
   if(old!=null)DeleteImage(old);if(victim!=null)DeleteImage(victim);
   BackColor=Color.FromArgb(33,133,106);feedback.Stop();feedback.Start();Invalidate();tray.Text="现场栈 · 已记录 "+DateTime.Now.ToString("HH:mm");if(panel!=null&&!panel.IsDisposed)panel.RefreshList(item.Id);return true;
  }catch(Exception ex){Notify("记录失败",ex.Message);return false;}
 }
 void DeleteImage(Scene s){try{if(!String.IsNullOrEmpty(s.ImagePath)&&File.Exists(s.ImagePath))File.Delete(s.ImagePath);}catch{/* Metadata remains authoritative; locked obsolete images are harmless. */}}
 public bool Available(Scene s){IntPtr h=new IntPtr(s.Handle);uint pid;Native.GetWindowThreadProcessId(h,out pid);if(!Native.IsWindow(h)||pid!=(uint)s.ProcessId)return false;if(s.ProcessStartTicks!=0&&ProcessStamp(s.ProcessId)!=s.ProcessStartTicks)return false;var title=new StringBuilder(512);Native.GetWindowText(h,title,512);return title.ToString()==s.Title;}
 public void QuickReturn(){
  if(panel!=null&&!panel.IsDisposed&&!panel.Commit())return;var current=Foreground();Scene target=null;
  foreach(var s in Scenes)if(!s.Done&&(current==null||!SceneLogic.SameWindow(s,current))&&Available(s)){target=s;break;}
  if(target==null){Notify("没有可返回的窗口","查看现场栈，根据截图找回已关闭的工作。");OpenPanel();return;}
  if(current!=null&&!CaptureItem(current))return;Resume(target);
 }
 public bool Resume(Scene s){if(panel!=null&&!panel.IsDisposed&&!panel.Commit())return false;if(!Available(s)){Notify("原窗口不可直接返回","窗口已关闭或标题变化，请根据现场截图找到文档。");return false;}IntPtr h=new IntPtr(s.Handle);if(Native.IsIconic(h))Native.ShowWindow(h,9);if(panel!=null)panel.Hide();if(!Native.SetForegroundWindow(h)){Notify("切换未成功","Windows 未允许此次切换，请从任务栏打开原窗口。");return false;}return true;}
 public void OpenPanel(){if(panel==null||panel.IsDisposed)panel=new PanelForm(this);panel.Show();panel.Activate();}
 public void OpenSettings(){using(var f=new SettingsForm(this))f.ShowDialog();}
 public void ApplySettings(bool alternate,bool titleOnly){bool oldAlternate=Settings.AlternateKeys,oldTitle=Settings.TitleOnly;Settings.AlternateKeys=alternate;Settings.TitleOnly=titleOnly;try{Write("settings.xml",Settings);}catch{Settings.AlternateKeys=oldAlternate;Settings.TitleOnly=oldTitle;throw;}RegisterKeys();if(panel!=null&&!panel.IsDisposed)panel.RefreshShortcuts();}
 public void Save(){Write("scenes.xml",Scenes);}
 public static void Write<T>(string name,T value){string p=Path.Combine(Root,name),tmp=p+".tmp";using(var f=File.Create(tmp))new XmlSerializer(typeof(T)).Serialize(f,value);if(File.Exists(p))File.Replace(tmp,p,null);else File.Move(tmp,p);}
 T ReadData<T>(string name,T fallback){string p=Path.Combine(Root,name);if(!File.Exists(p))return fallback;try{using(var f=File.OpenRead(p))return (T)new XmlSerializer(typeof(T)).Deserialize(f);}catch(Exception ex){string backup=p+".backup-"+Guid.NewGuid().ToString("N");File.Copy(p,backup);MessageBox.Show("读取失败，已保留备份："+backup+"\n"+ex.Message,"现场栈");return fallback;}}
 public bool Delete(Scene s){var index=Scenes.IndexOf(s);if(index<0)return false;Scenes.RemoveAt(index);try{Save();}catch(Exception ex){Scenes.Insert(index,s);Notify("删除失败",ex.Message);return false;}DeleteImage(s);Invalidate();return true;}
 protected override void OnFormClosing(FormClosingEventArgs e){if(panel!=null&&!panel.IsDisposed&&!panel.Commit())e.Cancel=true;try{StorePosition();}catch{}base.OnFormClosing(e);}
 protected override void OnFormClosed(FormClosedEventArgs e){for(int i=1;i<=3;i++)Native.UnregisterHotKey(Handle,i);feedback.Dispose();tip.Dispose();glyph.Dispose();tray.Dispose();if(panel!=null)panel.Dispose();base.OnFormClosed(e);}
}
class SettingsForm:Form {
 public SettingsForm(Widget w){Text="现场栈设置";Size=new Size(410,250);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterScreen;Font=new Font("Microsoft YaHei UI",9);
  var flow=new FlowLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(18),FlowDirection=FlowDirection.TopDown,WrapContents=false};Controls.Add(flow);
  flow.Controls.Add(new Label {Text="快捷键组（空格：记录；B：查看；R：返回）",AutoSize=true});var keys=new ComboBox {Width=330,DropDownStyle=ComboBoxStyle.DropDownList};keys.Items.AddRange(new object[]{"Ctrl + Alt","Ctrl + Shift"});keys.SelectedIndex=w.Settings.AlternateKeys?1:0;flow.Controls.Add(keys);
  var titleOnly=new CheckBox {Text="只留标题，不保存截图",AutoSize=true,Checked=w.Settings.TitleOnly,Margin=new Padding(3,16,3,3)};flow.Controls.Add(titleOnly);flow.Controls.Add(new Label {Text="仅影响之后的记录；再次记录会更新原现场。",AutoSize=true});var save=new Button {Text="保存设置",AutoSize=true,Margin=new Padding(3,16,3,3)};flow.Controls.Add(save);save.Click+=delegate{try{w.ApplySettings(keys.SelectedIndex==1,titleOnly.Checked);Close();}catch(Exception ex){MessageBox.Show(ex.Message,"保存失败");}};
 }
}
class PanelForm:Form {
 Widget owner;ListBox list=new ListBox();PictureBox pic=new PictureBox();TextBox search=new TextBox(),note=new TextBox();ComboBox project=new ComboBox(),projectFilter=new ComboBox();CheckBox unfinished=new CheckBox(),done=new CheckBox(),pinned=new CheckBox();Label detail=new Label(),empty=new Label(),shortcutInfo=new Label();bool binding;Scene editing;
 public PanelForm(Widget w){owner=w;Text="现场栈 · 找回刚才的工作";Size=new Size(1020,720);MinimumSize=new Size(820,600);StartPosition=FormStartPosition.CenterScreen;Font=new Font("Microsoft YaHei UI",9);BackColor=Color.FromArgb(246,247,249);KeyPreview=true;
  var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,Padding=new Padding(16)};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,43));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,57));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,55));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,190));Controls.Add(layout);
  var top=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2};top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,95));shortcutInfo.Dock=DockStyle.Fill;RefreshShortcuts();top.Controls.Add(shortcutInfo,0,0);var settings=new Button {Text="设置",Dock=DockStyle.Fill};settings.Click+=delegate{w.OpenSettings();};top.Controls.Add(settings,1,0);layout.Controls.Add(top,0,0);layout.SetColumnSpan(top,2);
  var left=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=4};left.RowStyles.Add(new RowStyle(SizeType.Absolute,28));left.RowStyles.Add(new RowStyle(SizeType.Absolute,32));left.RowStyles.Add(new RowStyle(SizeType.Absolute,32));left.RowStyles.Add(new RowStyle(SizeType.Percent,100));left.Controls.Add(new Label {Text="搜索项目 / 窗口标题 / 备注",Dock=DockStyle.Fill},0,0);search.Dock=DockStyle.Fill;left.Controls.Add(search,0,1);
  var filters=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2};filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));projectFilter.Dock=DockStyle.Fill;projectFilter.DropDownStyle=ComboBoxStyle.DropDownList;filters.Controls.Add(projectFilter,0,0);unfinished.Text="仅未完成";unfinished.Checked=true;unfinished.Dock=DockStyle.Fill;filters.Controls.Add(unfinished,1,0);left.Controls.Add(filters,0,2);list.Dock=DockStyle.Fill;list.HorizontalScrollbar=true;list.IntegralHeight=false;left.Controls.Add(list,0,3);layout.Controls.Add(left,0,1);
  var preview=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Padding=new Padding(10,0,0,0)};preview.RowStyles.Add(new RowStyle(SizeType.Absolute,50));preview.RowStyles.Add(new RowStyle(SizeType.Percent,100));preview.RowStyles.Add(new RowStyle(SizeType.Absolute,30));detail.Dock=DockStyle.Fill;preview.Controls.Add(detail,0,0);pic.Dock=DockStyle.Fill;pic.SizeMode=PictureBoxSizeMode.Zoom;pic.BackColor=Color.FromArgb(227,232,238);preview.Controls.Add(pic,0,1);empty.Dock=DockStyle.Fill;preview.Controls.Add(empty,0,2);layout.Controls.Add(preview,1,1);
  var bottom=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(0,12,0,0)};bottom.RowStyles.Add(new RowStyle(SizeType.Absolute,35));bottom.RowStyles.Add(new RowStyle(SizeType.Absolute,60));bottom.RowStyles.Add(new RowStyle(SizeType.Absolute,40));bottom.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.Controls.Add(bottom,0,2);layout.SetColumnSpan(bottom,2);
  var tags=new FlowLayoutPanel {Dock=DockStyle.Fill};tags.Controls.Add(new Label {Text="项目",AutoSize=true,Margin=new Padding(3,8,6,0)});project.Width=230;tags.Controls.Add(project);done.Text="已处理";done.AutoSize=true;pinned.Text="收藏，不被自动清理";pinned.AutoSize=true;tags.Controls.Add(done);tags.Controls.Add(pinned);bottom.Controls.Add(tags,0,0);
  note.Dock=DockStyle.Fill;note.Multiline=true;note.ScrollBars=ScrollBars.Vertical;bottom.Controls.Add(note,0,1);var buttons=new FlowLayoutPanel {Dock=DockStyle.Fill};var resume=new Button {Text="回到此窗口",AutoSize=true};var quick=new Button {Text="返回上一项",AutoSize=true};var save=new Button {Text="保存归类 / 备注",AutoSize=true};var delete=new Button {Text="删除此现场",AutoSize=true};buttons.Controls.AddRange(new Control[]{resume,quick,save,delete});bottom.Controls.Add(buttons,0,2);bottom.Controls.Add(new Label {Text="项目和备注可不填；切换记录、返回窗口或关闭面板时自动保存。收藏保护记录，最多 40 条。",Dock=DockStyle.Fill},0,3);
  search.TextChanged+=delegate{RefreshList();};projectFilter.SelectedIndexChanged+=delegate{if(!binding)RefreshList();};unfinished.CheckedChanged+=delegate{RefreshList();};list.SelectedIndexChanged+=delegate{if(!binding){var selected=list.SelectedItem as Scene;if(!Commit()){binding=true;list.SelectedItem=editing;binding=false;return;}Bind(selected);}};
  resume.Click+=delegate{var s=list.SelectedItem as Scene;if(s!=null)owner.Resume(s);};list.DoubleClick+=delegate{var s=list.SelectedItem as Scene;if(s!=null)owner.Resume(s);};quick.Click+=delegate{owner.QuickReturn();};save.Click+=delegate{if(Commit())RefreshList();};
  delete.Click+=delegate{var s=list.SelectedItem as Scene;if(s==null)return;if(MessageBox.Show("删除这条现场及截图？","现场栈",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;if(owner.Delete(s)){editing=null;RefreshList();}};
  KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Escape){if(Commit())Hide();e.Handled=true;}else if(e.Control&&e.KeyCode==Keys.F){search.Focus();search.SelectAll();e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.Enter&&list.Focused){var s=list.SelectedItem as Scene;if(s!=null)owner.Resume(s);e.SuppressKeyPress=true;}};RefreshList();
 }
 public void RefreshShortcuts(){shortcutInfo.Text="回来先接上一件事\n双击现场返回窗口 · "+owner.Keys+"R 留住当前现场并返回上一项";}
 public bool Commit(){if(binding||editing==null||!owner.Scenes.Contains(editing))return true;string p=project.Text.Trim(),n=note.Text;if(editing.Project==p&&editing.Note==n&&editing.Done==done.Checked&&editing.Pinned==pinned.Checked)return true;var beforeProject=editing.Project;var beforeNote=editing.Note;bool beforeDone=editing.Done,beforePin=editing.Pinned;editing.Project=p;editing.Note=n;editing.Done=done.Checked;editing.Pinned=pinned.Checked;try{owner.Save();owner.Invalidate();return true;}catch(Exception ex){editing.Project=beforeProject;editing.Note=beforeNote;editing.Done=beforeDone;editing.Pinned=beforePin;MessageBox.Show("保存失败："+ex.Message,"现场栈");return false;}}
 public void RefreshList(){RefreshList(null);}
 public void RefreshList(string selectId){if(binding||!Commit())return;string oldId=selectId??(editing==null?null:editing.Id),selectedProject=projectFilter.SelectedIndex>0?projectFilter.SelectedItem as string:"";binding=true;try{
  var projects=new List<string>();foreach(var s in owner.Scenes)if(!String.IsNullOrEmpty(s.Project)&&!projects.Contains(s.Project))projects.Add(s.Project);projects.Sort(StringComparer.CurrentCulture);projectFilter.Items.Clear();projectFilter.Items.Add("全部项目");foreach(var p in projects)projectFilter.Items.Add(p);projectFilter.SelectedIndex=projects.Contains(selectedProject)?projectFilter.Items.IndexOf(selectedProject):0;project.Items.Clear();foreach(var p in projects)project.Items.Add(p);
  list.Items.Clear();var rows=SceneLogic.Filter(owner.Scenes,search.Text.Trim(),projectFilter.SelectedIndex>0?projectFilter.SelectedItem as string:"",unfinished.Checked);foreach(var s in rows)list.Items.Add(s);foreach(var s in rows)if(s.Id==oldId){list.SelectedItem=s;break;}if(list.SelectedIndex<0&&list.Items.Count>0)list.SelectedIndex=0;
 }finally{binding=false;}Bind(list.SelectedItem as Scene);}
 void ClearImage(){var old=pic.Image;pic.Image=null;if(old!=null)old.Dispose();}
 void Bind(Scene s){binding=true;try{editing=s;ClearImage();project.Text=s==null?"":s.Project??"";note.Text=s==null?"":s.Note??"";done.Checked=s!=null&&s.Done;pinned.Checked=s!=null&&s.Pinned;project.Enabled=note.Enabled=done.Enabled=pinned.Enabled=s!=null;detail.Text=s==null?"暂时没有现场，回到工作窗口按快捷键记录。":s.Title+"\n"+s.Time.ToString("yyyy-MM-dd HH:mm:ss")+(owner.Available(s)?" · 窗口可返回":" · 请根据截图找回工作");empty.Text=s==null?"": "此现场仅记录标题";if(s!=null&&!String.IsNullOrEmpty(s.ImagePath)&&File.Exists(s.ImagePath)){try{using(var f=Image.FromFile(s.ImagePath))pic.Image=new Bitmap(f);empty.Text="";}catch{empty.Text="截图无法读取，窗口标题仍保留。";}}}finally{binding=false;}}
 protected override void OnFormClosing(FormClosingEventArgs e){if(!Commit())e.Cancel=true;base.OnFormClosing(e);}
 protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(!Visible)ClearImage();else {RefreshShortcuts();if(editing!=null)Bind(editing);}}
 protected override void OnFormClosed(FormClosedEventArgs e){ClearImage();base.OnFormClosed(e);}
}
class Program {
 [STAThread] static void Main(){bool fresh;using(var mutex=new System.Threading.Mutex(true,"Local\\SceneStackWidget",out fresh)){if(!fresh){MessageBox.Show("现场栈已经在运行，请查看屏幕边缘或系统托盘。");return;}Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new Widget());}}
}
