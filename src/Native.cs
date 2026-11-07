using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace AkilliKisayol {
 public static partial class Native {
  public delegate IntPtr HookProc(int code,IntPtr w,IntPtr l);
  [StructLayout(LayoutKind.Sequential)] public struct Keyboard {public uint vk,scan,flags,time; public UIntPtr extra;}
  [DllImport("user32.dll",SetLastError=true)] public static extern IntPtr SetWindowsHookEx(int id,HookProc proc,IntPtr module,uint thread);
  [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hook);
  [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr w,IntPtr l);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
  [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
  [DllImport("user32.dll",SetLastError=true)] public static extern bool RegisterHotKey(IntPtr hwnd,int id,uint mods,uint key);
  [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd,int id);
  [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool QueryFullProcessImageName(IntPtr process,int flags,StringBuilder name,ref int size);
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetKeyNameText(int param,StringBuilder name,int size);
  [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code,uint type);
  [StructLayout(LayoutKind.Sequential)] struct INPUT {public uint type;public InputUnion u;}
  [StructLayout(LayoutKind.Explicit)] struct InputUnion {[FieldOffset(0)] public KEYBDINPUT key; [FieldOffset(0)] public MOUSEINPUT mouse;}
  [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT {public ushort vk,scan;public uint flags,time;public UIntPtr extra;}
  [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT {public int x,y;public uint data,flags,time;public UIntPtr extra;}
  [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint count,INPUT[] inputs,int size);
  public static bool Modifier(int k) {return k==16||k==17||k==18||k==91||k==92||(k>=160&&k<=165);}
  public static string Label(int mods,int key,int prefix=0) {
   string s=((mods&2)!=0?"Ctrl + ":"")+((mods&1)!=0?"Alt + ":"")+((mods&4)!=0?"Shift + ":"")+((mods&8)!=0?"Win + ":"");
   if(key==0) return "Key not selected";
   if(InputKeys.IsPointer(key))return s+(prefix>0?Label(0,prefix)+" + ":"")+InputKeys.Name(key);
   string label=key==32?"Space":key==13?"Enter":key==27?"Esc":key==46?"Delete":key==45?"Insert":key==8?"Backspace":key>=96&&key<=105?"Numpad "+(key-96):key==109?"Numpad -":key==107?"Numpad +":key==106?"Numpad *":key==111?"Numpad /":key==110?"Numpad .":key>=48&&key<=57?((char)key).ToString():((System.Windows.Forms.Keys)key).ToString();
   return s+(prefix>0?Label(0,prefix)+" + ":"")+label;
  }
  public static string ProcessPath(IntPtr hwnd) {uint pid;GetWindowThreadProcessId(hwnd,out pid); return ProcessPath(pid);}
  public static string ProcessPath(uint pid) {
   IntPtr p=OpenProcess(0x1000,false,pid); if(p==IntPtr.Zero) return "";
   try {int n=32768;var text=new StringBuilder(n);return QueryFullProcessImageName(p,0,text,ref n)?text.ToString():"";} finally {CloseHandle(p);}
  }
  public static string Probe(Binding b) {
   if(b.Gesture!=null)return "Touchpad shapes use pointer recognition; keyboard registration does not apply.";
   if(InputKeys.IsPointer(b.Key))return "Mouse assignment is managed via mouse key along with keyboard key; RegisterHotKey test is not applied.";
   if(b.PrefixKey>0)return null;
   if(!RegisterHotKey(IntPtr.Zero,0x6AA1,(uint)b.Mods|0x4000,(uint)b.Key)) return "External registry warning (Win32 "+Marshal.GetLastWin32Error()+"): it could be another program or system restriction. This diagnostic result does not prevent enabling with keyboard hook; try additionally at runtime.";
   UnregisterHotKey(IntPtr.Zero,0x6AA1);return null;
  }
  public static void Send(int mods,int key) {
   if(InputKeys.IsPointer(key)){SendPointer(mods,key);return;}
   var keys=new List<int>();if((mods&2)!=0)keys.Add(162);if((mods&1)!=0)keys.Add(164);if((mods&4)!=0)keys.Add(160);if((mods&8)!=0)keys.Add(91);keys.Add(key);
   var input=new List<INPUT>();
   foreach(int k in keys) input.Add(KeyInput(k,false));
   keys.Reverse();foreach(int k in keys) input.Add(KeyInput(k,true));
   if(SendInput((uint)input.Count,input.ToArray(),Marshal.SizeOf(typeof(INPUT)))!=input.Count) throw new Win32Exception("Key could not be sent. The target application may be running with higher privileges.");
  }
  public static void TypeText(string text){
   var inputs=new List<INPUT>();foreach(char c in text){inputs.Add(new INPUT{type=1,u=new InputUnion{key=new KEYBDINPUT{scan=c,flags=4,extra=new UIntPtr(0x414B534D)}}});inputs.Add(new INPUT{type=1,u=new InputUnion{key=new KEYBDINPUT{scan=c,flags=6,extra=new UIntPtr(0x414B534D)}}});}
   if(SendInput((uint)inputs.Count,inputs.ToArray(),Marshal.SizeOf(typeof(INPUT)))!=inputs.Count)throw new Win32Exception("Text could not be sent; check the target's permissions.");
  }
  public static void SendRaw(params int[] keys){var inputs=keys.Select(k=>KeyInput(Math.Abs(k),k<0)).ToArray();if(SendInput((uint)inputs.Length,inputs,Marshal.SizeOf(typeof(INPUT)))!=inputs.Length)throw new Win32Exception("Shortcut key could not be sent to the source application.");}
  static readonly bool[] Extended=ExtendedKeys();
  static bool[] ExtendedKeys(){var table=new bool[256];foreach(int key in new[]{33,34,35,36,37,38,39,40,45,46,91,92,111,144,165,163,173,174,175,176,177,179})table[key]=true;return table;}
  static INPUT KeyInput(int key,bool up) {
   bool extended=key>=0&&key<Extended.Length&&Extended[key];
   return new INPUT {type=1,u=new InputUnion {key=new KEYBDINPUT {vk=(ushort)key,flags=(uint)((up?2:0)|(extended?1:0)),extra=new UIntPtr(0x414B534D)}}};
  }
 }
 public sealed class KeyboardEngine : IDisposable {
  static readonly string OwnPath=System.Reflection.Assembly.GetExecutingAssembly().Location;
  internal bool AllowOwnPointerForTest;readonly PointerEngine pointer;IntPtr cachedWindow;string cachedPath="";DateTime cacheUntil;
  readonly Native.HookProc callback; readonly IntPtr hook;
  readonly HashSet<int> pressed=new HashSet<int>(); readonly HashSet<int> blocked=new HashSet<int>();
  readonly DispatcherTimer timer=new DispatcherTimer();
  public bool Paused,AllowRemappedInput;Action<int,int,int,bool> capture;int capturePrefix;
  public Action<int,int,int,bool> Capture {get{return capture;}set{capture=value;capturePrefix=0;if(value!=null&&prefixKey>0)prefixUsed=true;}}
  public Func<List<Binding>> Bindings;
  public Action<Binding,IntPtr> Fire; public Action<string> Fault;
  public event Action<PhysicalKey> PhysicalInput;
  public Func<PhysicalKey,bool> InputObserver;
  public int CurrentMods {get{return Mods;}}
  internal bool WouldHandlePointer(int key){if(Paused||pressed.Contains(165))return false;IntPtr window=Native.GetForegroundWindow();string app=ForegroundPath(window);if(!AllowOwnPointerForTest&&Rules.SameApp(app,OwnPath))return false;return Bindings!=null&&Rules.Resolve(Bindings(),Mods,key,app,prefixKey>0&&!prefixForwarded&&Mods==prefixMods?prefixKey:0)!=null;}
  internal void ScheduleGesture(Binding binding,IntPtr window){if(!Paused&&Capture==null&&Native.GetForegroundWindow()==window)Queue(binding,window);}
  public void ResetPending(){pending=null;replay.Clear();prefixUsed=true;timer.Stop();cacheUntil=DateTime.MinValue;}
  bool flushPosted,disposed;
  void PostFlush(){if(flushPosted||disposed)return;flushPosted=true;Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(()=>{flushPosted=false;if(!disposed)Tick(null,EventArgs.Empty);}));}
  Binding pending;IntPtr pendingWindow;DateTime deadline;
  int prefixKey,prefixMods;bool prefixUsed,prefixForwarded;IntPtr prefixWindow;
  readonly List<Tuple<int,int,IntPtr>> replay=new List<Tuple<int,int,IntPtr>>();
  internal Action<int,int> ReplayKey=Native.Send;internal Action<int[]> ReplayRaw=keys=>Native.SendRaw(keys);
  public KeyboardEngine() {
   callback=OnKey;hook=Native.SetWindowsHookEx(13,callback,Native.GetModuleHandle(null),0);
   if(hook==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(),"Keyboard switch could not be installed.");
   try{pointer=new PointerEngine(this);}catch{Native.UnhookWindowsHookEx(hook);throw;}
   timer.Interval=TimeSpan.FromMilliseconds(8);timer.Tick+=Tick;
  }
  int Mods {get {return (pressed.Contains(162)||pressed.Contains(163)||pressed.Contains(17)?2:0)|(pressed.Contains(164)||pressed.Contains(18)?1:0)|(pressed.Contains(160)||pressed.Contains(161)||pressed.Contains(16)?4:0)|(pressed.Contains(91)||pressed.Contains(92)?8:0);}}
  IntPtr OnKey(int code,IntPtr w,IntPtr l) {
   if(code<0) return Native.CallNextHookEx(hook,code,w,l);
   try {
    var e=(Native.Keyboard)Marshal.PtrToStructure(l,typeof(Native.Keyboard));
    if(Handle(e))return new IntPtr(1);
   } catch(Exception ex) {if(Fault!=null) Application.Current.Dispatcher.BeginInvoke(new Action(()=>Fault(ex.Message)));}
   return Native.CallNextHookEx(hook,code,w,l);
  }
  internal bool Handle(Native.Keyboard e) {
    if(e.extra==new UIntPtr(0x414B534D)||(e.flags&0x10)!=0&&!AllowRemappedInput) return false;
    int key=(int)e.vk;bool up=(e.flags&0x80)!=0;
    if(key==27&&!up&&Actions.WorkflowRunning)Actions.CancelWorkflow();
    bool repeat=pressed.Contains(key); if(up)pressed.Remove(key);else pressed.Add(key);
    if(up&&pressed.Count==0&&(pending!=null||replay.Count>0))PostFlush();
    bool eat=blocked.Contains(key);if(up)blocked.Remove(key);
    if(PhysicalInput!=null||InputObserver!=null){var observation=new PhysicalKey{Key=key,Mods=Mods,Up=up,Repeat=repeat,AltGr=pressed.Contains(165),Window=Native.GetForegroundWindow()};if(PhysicalInput!=null)PhysicalInput(observation);if(InputObserver!=null&&InputObserver(observation)){if(!up)blocked.Add(key);return true;}}
    if(Capture!=null) {
     if(!Native.Modifier(key)){
      if(!up&&!repeat){if(key==27){capturePrefix=0;EmitCapture(Mods,key,0);}else if(capturePrefix>0&&capturePrefix!=key){int prefix=capturePrefix;capturePrefix=0;EmitCapture(Mods,key,prefix);}else if(TriggerSpec.PrefixAllowed(key))capturePrefix=key;else EmitCapture(Mods,key,0);}
      else if(up&&capturePrefix==key){capturePrefix=0;EmitCapture(Mods,key,0);}
     }
     if(!up)blocked.Add(key);return true;
    }
    if(prefixKey>0&&key==prefixKey){
     if(!up)return !prefixForwarded;
     bool forwarded=prefixForwarded;if(!prefixUsed&&!forwarded&&!Paused)AddReplay(prefixMods,prefixKey,prefixWindow);prefixKey=0;prefixUsed=prefixForwarded=false;return !forwarded;
    }
    if(eat) return true;
    if(!up && !repeat && !Paused && !Native.Modifier(key) && !pressed.Contains(165)) {
     IntPtr window=Native.GetForegroundWindow();var bindings=Bindings==null?new List<Binding>():Bindings();int mods=Mods;
     // The process path costs OpenProcess/QueryFullProcessImageName; skip it for keys no binding can use.
     string app=InputKeys.IsPointer(key)||bindings.Any(item=>item.Enabled&&item.Mods==mods&&(item.Key==key||item.PrefixKey==key))?ForegroundPath(window):"";
     if(!AllowOwnPointerForTest&&InputKeys.IsPointer(key)&&Rules.SameApp(app,OwnPath))return false;
     if(prefixKey>0&&!prefixForwarded){
      if(window!=prefixWindow){prefixUsed=true;return false;}
      var chord=Rules.Resolve(bindings,Mods,key,app,prefixKey);
      if(chord!=null&&Mods==prefixMods){prefixUsed=true;Queue(chord,window);blocked.Add(key);return true;}
      if(!prefixUsed&&Mods!=prefixMods){prefixUsed=true;AddReplay(prefixMods,prefixKey,prefixWindow);return false;}
      if(!prefixUsed){prefixForwarded=true;ReplayRaw(new[]{prefixKey,key});return true;}
      return false;
     }
     if(prefixKey==0&&bindings.Any(item=>item.Enabled&&item.Mods==Mods&&item.PrefixKey==key&&(item.AppPath.Length==0||Rules.SameApp(item.AppPath,app)))){prefixKey=key;prefixMods=Mods;prefixWindow=window;prefixUsed=prefixForwarded=false;return true;}
     Binding b=Rules.Resolve(bindings,Mods,key,app);
     if(b!=null) {
      Queue(b,window);
      if(!b.PassThrough) {blocked.Add(key);return true;}
     }
    }
   return false;
  }
  void AddReplay(int mods,int key,IntPtr window){replay.Add(Tuple.Create(mods,key,window));timer.Start();}
  void EmitCapture(int mods,int key,int prefix){var handler=Capture;bool altgr=pressed.Contains(165);Application.Current.Dispatcher.BeginInvoke(new Action(()=>{if(Capture==handler)handler(mods,key,prefix,altgr);}));}
  string ForegroundPath(IntPtr window){if(window!=cachedWindow||DateTime.UtcNow>=cacheUntil){cachedWindow=window;cachedPath=Native.ProcessPath(window);cacheUntil=DateTime.UtcNow.AddMilliseconds(500);}return cachedPath;}
  internal void ReportFault(string message){if(Fault!=null)Application.Current.Dispatcher.BeginInvoke(new Action(()=>Fault(message)));}
  void Queue(Binding b,IntPtr window){if(pending==null){pending=b.Copy();pendingWindow=window;deadline=DateTime.UtcNow.AddSeconds(3);timer.Start();if(pressed.Count==0)PostFlush();}}
  internal void FlushForTest(){Tick(null,EventArgs.Empty);}
  void Tick(object sender,EventArgs e) {
   if(Paused||Capture!=null)replay.Clear();else if(pressed.Count==0&&replay.Count>0){var items=replay.ToArray();replay.Clear();foreach(var item in items)if(Native.GetForegroundWindow()==item.Item3)ReplayKey(item.Item1,item.Item2);}
   if(pending==null){if(replay.Count==0)timer.Stop();return;}
   if(Paused || Capture!=null) {pending=null;return;}
   if(pressed.Count==0) {var b=pending;var win=pendingWindow;pending=null;timer.Stop();if(Fire!=null)Fire(b,win);}
   else if(DateTime.UtcNow>deadline){pending=null;pressed.RemoveWhere(k=>(Native.GetAsyncKeyState(k)&0x8000)==0);blocked.RemoveWhere(k=>!pressed.Contains(k));timer.Stop();if(Fault!=null)Fault("Keys were not released within 3 seconds; pending action cancelled.");}
  }
  public void Dispose() {disposed=true;timer.Stop();pointer.Dispose();Native.UnhookWindowsHookEx(hook);}
  internal bool MouseForTest(int message,Native.MouseEvent input){return pointer.Handle(message,input);}
  internal bool IdleForTest {get{return !timer.IsEnabled;}}
  internal void ExpireForTest(){deadline=DateTime.UtcNow.AddSeconds(-1);Tick(null,EventArgs.Empty);}
 }
}
