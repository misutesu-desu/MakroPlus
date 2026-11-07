using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;

namespace AkilliKisayol {
 public static class WindowFrameTests {
  public static void Unit(Action<bool,string> check){var area=new WindowFrame.MonitorArea{Monitor=new WindowFrame.Rect{Left=-1920,Top=0,Right=0,Bottom=1080},Work=new WindowFrame.Rect{Left=-1880,Top=0,Right=0,Bottom=1040}};var bounds=new WindowFrame.MinMax();WindowFrame.SetBounds(ref bounds,area,2200,1200);check(bounds.MaxPosition.X==40&&bounds.MaxPosition.Y==0&&bounds.MaxSize.X==1880&&bounds.MaxSize.Y==1040,"Custom frame respects work areas on negative-origin monitors with side and bottom taskbars");check(bounds.MinTrack.X==1880&&bounds.MinTrack.Y==1040,"Minimum tracking dimensions cannot exceed a smaller monitor's available work area");}
  static IEnumerable<T> Find<T>(DependencyObject parent) where T:DependencyObject {for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is T)yield return (T)child;foreach(var nested in Find<T>(child))yield return nested;}}
  static void WaitFor(MainWindow window,WindowState desired){var frame=new DispatcherFrame();var deadline=DateTime.UtcNow.AddSeconds(3);var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};timer.Tick+=(s,e)=>{if(window.WindowState==desired||DateTime.UtcNow>=deadline){timer.Stop();frame.Continue=false;}};timer.Start();Dispatcher.PushFrame(frame);if(window.WindowState!=desired)throw new Exception("Window did not change state: "+desired);window.UpdateLayout();}
  static Button Control(MainWindow window,string name){return Find<Button>(window).Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)==name+" window");}
  public static void Ui(MainWindow window,Action<bool,string> check,Action<Window,string> screenshot){
   check(window.WindowStyle==WindowStyle.None&&WindowChrome.GetWindowChrome(window)!=null,"Main window replaces the native title bar while retaining a resizable Windows chrome");
   check(new[]{"OVERVIEW","INPUTS","WORKSPACE","TOOLS & SETTINGS"}.All(name=>Find<TextBlock>(window).Any(t=>t.Text==name&&t.FontWeight==FontWeights.Bold)),"Sidebar category headings are distinct, bold groups");
   var active=Find<Button>(window).Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="Keyboard shortcuts");check(Convert.ToString(active.Tag)=="selected"&&active.FontWeight==FontWeights.Bold,"Active navigation retains a visible selection marker and strong label");
   var handle=new WindowInteropHelper(window).Handle;var normal=new WindowFrame.Rect();WindowFrame.GetWindowRect(handle,out normal);Control(window,"Maximize").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));WaitFor(window,WindowState.Maximized);var area=new WindowFrame.MonitorArea{Size=Marshal.SizeOf(typeof(WindowFrame.MonitorArea))};bool monitor=WindowFrame.GetMonitorInfo(WindowFrame.MonitorFromWindow(handle,2),ref area);WindowFrame.Rect large;bool rect=WindowFrame.GetWindowRect(handle,out large);check(monitor&&rect&&large.Left>=area.Work.Left-1&&large.Top>=area.Work.Top-1&&large.Right<=area.Work.Right+1&&large.Bottom<=area.Work.Bottom+1,"Maximized native window stays inside the monitor work area and leaves the taskbar accessible");check(Control(window,"Restore").IsVisible&&Control(window,"Close").IsVisible,"Restore and close controls stay accessible when maximized");screenshot(window,"maximized-sidebar-preview.png");Control(window,"Restore").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));WaitFor(window,WindowState.Normal);WindowFrame.Rect restored;WindowFrame.GetWindowRect(handle,out restored);check(Math.Abs(restored.Left-normal.Left)<=1&&Math.Abs(restored.Top-normal.Top)<=1&&Math.Abs(restored.Right-normal.Right)<=1&&Math.Abs(restored.Bottom-normal.Bottom)<=1,"Restoring the custom frame preserves the normal window's bounds");Control(window,"Minimize").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));WaitFor(window,WindowState.Minimized);SystemCommands.RestoreWindow(window);WaitFor(window,WindowState.Normal);check(Control(window,"Maximize").IsVisible,"Minimizing and restoring retain functional window controls");
  }
 }
}
