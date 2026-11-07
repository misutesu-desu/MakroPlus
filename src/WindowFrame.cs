using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace AkilliKisayol {
 public partial class MainWindow {
  HwndSource frameSource;Button maximizeWindow;
  void InstallWindowFrame(Grid root){
   WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.CanResize;
   WindowChrome.SetWindowChrome(this,new WindowChrome{CaptionHeight=30,ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(0),UseAeroCaptionButtons=false});
   var caption=new DockPanel{Height=30,VerticalAlignment=VerticalAlignment.Top,LastChildFill=true,Background=Brushes.Transparent};Grid.SetColumnSpan(caption,2);Panel.SetZIndex(caption,10);var controls=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(controls,Dock.Right);caption.Children.Add(controls);controls.Children.Add(FrameButton("—","Minimize",()=>SystemCommands.MinimizeWindow(this)));maximizeWindow=FrameButton("□","Maximize",()=>{if(WindowState==WindowState.Maximized)SystemCommands.RestoreWindow(this);else SystemCommands.MaximizeWindow(this);});controls.Children.Add(maximizeWindow);controls.Children.Add(FrameButton("×","Close",()=>SystemCommands.CloseWindow(this)));caption.Children.Add(new Border{Background=Brushes.Transparent});root.Children.Add(caption);
   StateChanged+=(s,e)=>{bool large=WindowState==WindowState.Maximized;maximizeWindow.Content=large?"❐":"□";maximizeWindow.ToolTip=large?"Restore":"Maximize";System.Windows.Automation.AutomationProperties.SetName(maximizeWindow,large?"Restore window":"Maximize window");};
   SourceInitialized+=(s,e)=>{frameSource=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);if(frameSource!=null)frameSource.AddHook(FrameMessage);};Closed+=(s,e)=>{if(frameSource!=null){frameSource.RemoveHook(FrameMessage);frameSource=null;}};
  }
  Button FrameButton(string symbol,string name,Action action){var button=Theme.Button(symbol,action);button.Style=(Style)Application.Current.FindResource("WindowButton");button.Width=44;button.Height=30;button.Padding=new Thickness(0);button.Margin=new Thickness(0);button.FontSize=17;button.HorizontalContentAlignment=HorizontalAlignment.Center;button.Background=Brushes.Transparent;button.ToolTip=name;button.Focusable=true;System.Windows.Automation.AutomationProperties.SetName(button,name+" window");WindowChrome.SetIsHitTestVisibleInChrome(button,true);return button;}
  IntPtr FrameMessage(IntPtr handle,int message,IntPtr w,IntPtr l,ref bool handled){if(message!=0x24)return IntPtr.Zero;IntPtr monitor=WindowFrame.MonitorFromWindow(handle,2);var area=new WindowFrame.MonitorArea{Size=Marshal.SizeOf(typeof(WindowFrame.MonitorArea))};if(monitor==IntPtr.Zero||!WindowFrame.GetMonitorInfo(monitor,ref area))return IntPtr.Zero;var info=(WindowFrame.MinMax)Marshal.PtrToStructure(l,typeof(WindowFrame.MinMax));var scale=frameSource==null||frameSource.CompositionTarget==null?Matrix.Identity:frameSource.CompositionTarget.TransformToDevice;WindowFrame.SetBounds(ref info,area,(int)Math.Ceiling(MinWidth*scale.M11),(int)Math.Ceiling(MinHeight*scale.M22));Marshal.StructureToPtr(info,l,false);handled=true;return IntPtr.Zero;}
 }
 public static class WindowFrame {
  [StructLayout(LayoutKind.Sequential)]public struct Point {public int X,Y;}
  [StructLayout(LayoutKind.Sequential)]public struct Rect {public int Left,Top,Right,Bottom;}
  [StructLayout(LayoutKind.Sequential)]public struct MonitorArea {public int Size;public Rect Monitor,Work;public uint Flags;}
  [StructLayout(LayoutKind.Sequential)]public struct MinMax {public Point Reserved,MaxSize,MaxPosition,MinTrack,MaxTrack;}
  [DllImport("user32.dll")]public static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
  [DllImport("user32.dll",CharSet=CharSet.Auto)]public static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorArea info);
  [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr window,out Rect rect);
  internal static void SetBounds(ref MinMax info,MonitorArea area,int minWidth,int minHeight){int width=area.Work.Right-area.Work.Left,height=area.Work.Bottom-area.Work.Top;info.MaxPosition=new Point{X=area.Work.Left-area.Monitor.Left,Y=area.Work.Top-area.Monitor.Top};info.MaxSize=new Point{X=width,Y=height};info.MinTrack=new Point{X=Math.Min(width,Math.Max(1,minWidth)),Y=Math.Min(height,Math.Max(1,minHeight))};info.MaxTrack=new Point{X=Math.Max(info.MaxTrack.X,width),Y=Math.Max(info.MaxTrack.Y,height)};}
 }
}
