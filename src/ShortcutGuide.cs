using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace AkilliKisayol {
 public sealed class GuideRow {public string Combo,Name,Source;}
 public static class GuideEntries {
  public static List<GuideRow> Build(State state,IEnumerable<SystemShortcut> system,string app){
   var all=Profiles.Effective(state).Where(b=>b.Enabled&&(b.AppPath.Length==0||Rules.SameApp(b.AppPath,app))).ToArray();
   var result=all.GroupBy(b=>b.Gesture!=null?"shape:"+b.Id:b.Mods+":"+b.Key+":"+b.PrefixKey).Select(g=>g.First().Gesture!=null?g.First():Rules.Resolve(g,g.First().Mods,g.First().Key,app,g.First().PrefixKey)).Where(b=>b!=null).Select(b=>new GuideRow{Combo=b.Combo,Name=b.Name,Source="My shortcut • "+b.Scope}).ToList();
   result.AddRange(system.Where(s=>s.Enabled&&(s.App.Length==0||Rules.SameApp(System.IO.Path.GetFileName(s.App),System.IO.Path.GetFileName(app)))).Select(s=>new GuideRow{Combo=s.Combo,Name=s.Name,Source=s.Reference?"Known default":s.Provider}));return result;
  }
 }
 public sealed class GuideWindow : Window {
  [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
  [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]static extern IntPtr SetWindowLongPtr(IntPtr window,int index,IntPtr value);
  public GuideWindow(string title,IEnumerable<GuideRow> rows){
   Style=(Style)Application.Current.FindResource(typeof(Window));Title="Shortcut guide";ShowActivated=false;Focusable=false;Topmost=true;ShowInTaskbar=false;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;Width=760;Height=640;
   var panel=new DockPanel{Margin=new Thickness(24)};Content=panel;var heading=new StackPanel{Margin=new Thickness(0,0,0,15)};heading.Children.Add(Theme.Text(title,23));heading.Children.Add(Theme.Text("Close on key release • Resource tags separate file records and defaults",12,"Muted"));DockPanel.SetDock(heading,Dock.Top);panel.Children.Add(heading);
   var list=new StackPanel();foreach(var row in rows){var line=new Grid{Margin=new Thickness(0,0,0,9)};line.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(200)});line.ColumnDefinitions.Add(new ColumnDefinition());line.Children.Add(Theme.Text(row.Combo,14));var info=new StackPanel();info.Children.Add(Theme.Text(row.Name,14));info.Children.Add(Theme.Text(row.Source,10,"Muted"));Grid.SetColumn(info,1);line.Children.Add(info);list.Children.Add(line);}if(list.Children.Count==0)list.Children.Add(Theme.Text("No registered shortcut found to display for this application.",14));panel.Children.Add(new ScrollViewer{Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
   SourceInitialized+=(s,e)=>{var handle=new WindowInteropHelper(this).Handle;SetWindowLongPtr(handle,-20,new IntPtr(GetWindowLongPtr(handle,-20).ToInt64()|0x08000000L|0x80L));};
  }
 }
 public sealed class ShortcutGuide : IDisposable {
  readonly KeyboardEngine engine;readonly Func<State> state;readonly Func<IEnumerable<SystemShortcut>> sources;readonly DispatcherTimer timer=new DispatcherTimer(),manual=new DispatcherTimer(),watch=new DispatcherTimer();GuideWindow window;IntPtr target;bool held,cancelled;
  public ShortcutGuide(KeyboardEngine keyboard,Func<State> data,Func<IEnumerable<SystemShortcut>> system){engine=keyboard;state=data;sources=system;timer.Interval=TimeSpan.FromMilliseconds(550);timer.Tick+=(s,e)=>{timer.Stop();if(held&&!cancelled&&!engine.Paused&&state().GuideEnabled&&Native.GetForegroundWindow()==target&&!Actions.WorkflowRunning)Show(target);};manual.Interval=TimeSpan.FromSeconds(6);manual.Tick+=(s,e)=>{manual.Stop();Hide();};engine.PhysicalInput+=Input;}
  void Input(PhysicalKey input){int key=state().GuideHoldKey;if(input.Key==key){if(input.Up){held=false;timer.Stop();Hide();}else if(!input.Repeat){held=true;cancelled=engine.CurrentMods!=(key==162||key==163?2:key==160||key==161?4:0);target=input.Window;if(!cancelled)timer.Start();}}else if(!input.Up){cancelled=true;timer.Stop();Hide();}}
  void Show(IntPtr handle){Hide();string app=Native.ProcessPath(handle);window=new GuideWindow((app.Length>0?AppLibrary.Name(app):"Global")+" • "+Profiles.Name(state()),GuideEntries.Build(state(),sources(),app));window.Show();Native.PlaceGuide(new WindowInteropHelper(window).Handle,handle);watch.Interval=TimeSpan.FromMilliseconds(180);watch.Tick+=Watch;target=handle;watch.Start();}
  void Watch(object sender,EventArgs e){if(Native.GetForegroundWindow()!=target||engine.Paused)Hide();}
  public void ShowOnce(IntPtr targetWindow){Show(targetWindow);manual.Start();}
  public void Hide(){watch.Stop();watch.Tick-=Watch;if(window!=null){window.Close();window=null;}}
  internal bool VisibleForTest {get{return window!=null&&window.IsVisible;}}
  internal void OpenForTest(IntPtr handle){Show(handle);}
  public void Dispose(){engine.PhysicalInput-=Input;timer.Stop();manual.Stop();Hide();}
 }
}
