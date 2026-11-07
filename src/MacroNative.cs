using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AkilliKisayol {
 public sealed class PhysicalKey {public int Key,Mods;public bool Up,Repeat,AltGr;public IntPtr Window;}
 public static partial class Native {
  [StructLayout(LayoutKind.Sequential)] public struct Point {public int X,Y;}
  [StructLayout(LayoutKind.Sequential)] public struct MouseEvent {public Point Point;public uint Data,Flags,Time;public UIntPtr Extra;}
  [DllImport("user32.dll")]public static extern IntPtr GetAncestor(IntPtr window,uint flags);
  [DllImport("user32.dll")]public static extern IntPtr WindowFromPoint(Point point);
  [DllImport("user32.dll")]public static extern IntPtr WindowFromPhysicalPoint(Point point);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr window,StringBuilder text,int count);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr window,StringBuilder text,int count);
  [DllImport("user32.dll",SetLastError=true)]static extern bool SetPhysicalCursorPos(int x,int y);
  [DllImport("user32.dll")]static extern bool GetPhysicalCursorPos(out Point point);
  public static string WindowTitle(IntPtr window){var text=new StringBuilder(1024);GetWindowText(window,text,text.Capacity);return text.ToString();}
  public static string WindowClass(IntPtr window){var text=new StringBuilder(256);GetClassName(window,text,text.Capacity);return text.ToString();}
  public static bool ClassMatches(string actual,string expected){return string.IsNullOrEmpty(expected)||actual==expected||expected.StartsWith("HwndWrapper[")&&actual.StartsWith("HwndWrapper[");}
  static int[] ModKeys(int mods,bool altGr=false){var keys=new List<int>();if((mods&2)!=0||altGr)keys.Add(162);if(altGr)keys.Add(165);else if((mods&1)!=0)keys.Add(164);if((mods&4)!=0)keys.Add(160);if((mods&8)!=0)keys.Add(91);return keys.ToArray();}
  static void Modifiers(int mods,bool up,bool altGr=false){var keys=ModKeys(mods,altGr);if(up)Array.Reverse(keys);var inputs=keys.Select(k=>KeyInput(k,up)).ToArray();if(inputs.Length>0&&SendInput((uint)inputs.Length,inputs,Marshal.SizeOf(typeof(INPUT)))!=inputs.Length)throw new Win32Exception("The mouse step's modifiers could not be sent.");}
  public static void SendChord(int mods,int key,bool altGr){if(InputKeys.IsPointer(key)){SendPointer(mods,key,altGr);return;}if(!altGr){Send(mods,key);return;}var keys=ModKeys(mods,true).Concat(new[]{key}).ToArray();var input=keys.Select(k=>KeyInput(k,false)).Concat(keys.Reverse().Select(k=>KeyInput(k,true))).ToArray();if(SendInput((uint)input.Length,input,Marshal.SizeOf(typeof(INPUT)))!=input.Length)throw new Win32Exception("AltGr key was not sent.");}
  static void Mouse(uint flags,int data=0){var input=new[]{new INPUT{type=0,u=new InputUnion{mouse=new MOUSEINPUT{flags=flags,data=unchecked((uint)data),extra=new UIntPtr(0x414B534D)}}}};if(SendInput(1,input,Marshal.SizeOf(typeof(INPUT)))!=1)throw new Win32Exception("Mouse input could not be sent.");}
  internal static uint PointerDown(int button){return button==1?8u:button==2?32u:button>=3?128u:2u;}
  internal static uint PointerUp(int button){return button==1?16u:button==2?64u:button>=3?256u:4u;}
  internal static int PointerData(int button){return button>=3?button-2:0;}
  static void SendPointer(int mods,int key,bool altgr=false){Point point;if(!GetPhysicalCursorPos(out point)||GetAncestor(WindowFromPhysicalPoint(point),2)!=GetForegroundWindow())throw new InvalidOperationException("The mouse cursor must be over the target window in the foreground.");try{Modifiers(mods,false,altgr);if(InputKeys.IsWheel(key)){Mouse(key==InputKeys.WheelLeft||key==InputKeys.WheelRight?0x1000u:0x800u,key==InputKeys.WheelDown||key==InputKeys.WheelLeft?-120:120);return;}int button=key==2?1:key==4?2:key==5?3:key==6?4:0;try{Mouse(PointerDown(button),PointerData(button));}finally{Mouse(PointerUp(button),PointerData(button));}}finally{Modifiers(mods,true,altgr);}}
  internal static async Task MouseAction(ActionStep step,System.Windows.Rect bounds,IntPtr window,CancellationToken cancellation){
   double scaleX=step.ScaleWithWindow?bounds.Width/step.WindowWidth:1,scaleY=step.ScaleWithWindow?bounds.Height/step.WindowHeight:1;
   int x=(int)Math.Round(bounds.Left+step.X*scaleX),y=(int)Math.Round(bounds.Top+step.Y*scaleY);uint down=PointerDown(step.MouseButton),up=PointerUp(step.MouseButton);int buttonData=PointerData(step.MouseButton);bool pressed=false;
   Actions.EnsureTarget(window);if(!SetPhysicalCursorPos(x,y))throw new Win32Exception("Mouse position could not be set.");
   try{Modifiers(step.Mods,false,step.AltGr);cancellation.ThrowIfCancellationRequested();Actions.EnsureTarget(window);if(GetAncestor(WindowFromPhysicalPoint(new Point{X=x,Y=y}),2)!=window)throw new InvalidOperationException("The registered mouse point is being closed by another window; click was not sent. Remove the window on top of the target window and try again.");if(step.Kind=="Scroll"){Mouse(step.Horizontal?0x1000u:0x800u,step.ScrollDelta);return;}Mouse(down,buttonData);pressed=true;
    int duration=step.Duration>0?step.Duration:step.Kind=="Mouse drag"?240:35;
    if(step.Kind=="Mouse drag"){int parts=Math.Max(1,Math.Min(120,duration/20));for(int part=1;part<=parts;part++){await Task.Delay(Math.Max(1,duration/parts),cancellation);Actions.EnsureTarget(window);if(!SetPhysicalCursorPos((int)Math.Round(bounds.Left+(step.X+(step.X2-step.X)*part/parts)*scaleX),(int)Math.Round(bounds.Top+(step.Y+(step.Y2-step.Y)*part/parts)*scaleY)))throw new Win32Exception("Drop position could not be set.");}}
    else await Task.Delay(duration,cancellation);
   }finally{try{if(pressed)Mouse(up,buttonData);}finally{Modifiers(step.Mods,true,step.AltGr);}}
  }
  delegate bool EnumWindowsProc(IntPtr window,IntPtr data);
  [DllImport("user32.dll")]static extern bool EnumWindows(EnumWindowsProc callback,IntPtr data);
  [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr window);
  [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")]static extern bool AttachThreadInput(uint first,uint second,bool attach);
  [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr window);
  [DllImport("user32.dll")]static extern bool ShowWindowAsync(IntPtr window,int command);
  [DllImport("user32.dll")]static extern uint GetDpiForWindow(IntPtr window);
  [DllImport("user32.dll",SetLastError=true)]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
  public static double DpiScale(IntPtr window){try{uint dpi=GetDpiForWindow(window);return dpi>0?dpi/96.0:1;}catch(EntryPointNotFoundException){return 1;}}
  public static void PlaceGuide(IntPtr window,IntPtr target){var area=System.Windows.Forms.Screen.FromHandle(target).WorkingArea;double scale=DpiScale(target);int width=Math.Min((int)Math.Round(760*scale),area.Width-40),height=Math.Min((int)Math.Round(640*scale),area.Height-40);SetWindowPos(window,IntPtr.Zero,area.Left+(area.Width-width)/2,area.Top+(area.Height-height)/2,width,height,0x14);}
  public static bool ActivateWindow(IntPtr window){
   ShowWindowAsync(window,9);if(SetForegroundWindow(window))return true;uint pid;uint foreground=GetWindowThreadProcessId(GetForegroundWindow(),out pid),current=GetCurrentThreadId();bool attached=foreground!=0&&foreground!=current&&AttachThreadInput(current,foreground,true);
   try{return SetForegroundWindow(window);}finally{if(attached)AttachThreadInput(current,foreground,false);}
  }
  public static List<IntPtr> AppWindows(string path){var list=new List<IntPtr>();EnumWindows((window,data)=>{if(IsWindowVisible(window)&&Rules.SameApp(ProcessPath(window),path))list.Add(window);return true;},IntPtr.Zero);return list;}
 }
}
