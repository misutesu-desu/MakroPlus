using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Automation;

namespace AkilliKisayol {
 public sealed class MacroRecorder : IDisposable {
  readonly KeyboardEngine engine;readonly Stopwatch clock=new Stopwatch();readonly object sync=new object();readonly List<ActionStep> steps=new List<ActionStep>();
  readonly Native.HookProc mouseCallback;IntPtr mouseHook,lastWindow;Task queue=Task.FromResult(0);long previousTime;bool paused,active,disposed;ActionStep mouseDown;IntPtr mouseWindow;long mouseTime;
  public event Action Changed,StopRequested;
  public string Error="";
  int notificationPending;
  internal bool AllowOwnProcess;
  public bool Recording {get{return active;}}
  public List<ActionStep> Steps {get{lock(sync)return steps.Select(s=>s.Copy()).ToList();}}
  public MacroRecorder(KeyboardEngine keyboard){engine=keyboard;mouseCallback=MouseHook;}
  public void Start(bool mouse=true){
   if(active||disposed)throw new InvalidOperationException("Recording is already open or closed.");if(Actions.WorkflowRunning)throw new InvalidOperationException("Terminate the running process sequence or stop with Escape.");if(engine.Capture!=null||engine.InputObserver!=null)throw new InvalidOperationException("Another key recording is active.");
   lock(sync)steps.Clear();Error="";lastWindow=IntPtr.Zero;mouseDown=null;previousTime=0;paused=engine.Paused;engine.Paused=true;engine.ResetPending();active=true;clock.Restart();engine.InputObserver=OnKey;
   if(mouse){mouseHook=Native.SetWindowsHookEx(14,mouseCallback,Native.GetModuleHandle(null),0);if(mouseHook==IntPtr.Zero){StopInput();throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"Mouse capture hook could not be installed.");}}
  }
  bool Own(IntPtr window){return !AllowOwnProcess&&Rules.SameApp(Native.ProcessPath(window),System.Reflection.Assembly.GetExecutingAssembly().Location);}
  bool OnKey(PhysicalKey input){
   if(!active)return false;if(input.Key==121){if(!input.Up&&!input.Repeat){var handler=StopRequested;if(handler!=null)Application.Current.Dispatcher.BeginInvoke(handler);}return true;}
   if(input.Up||InputKeys.IsPointer(input.Key)||Native.Modifier(input.Key)||Own(input.Window))return false;
   // Window switching is captured as a target focus step on the next actual action.
   if((input.Mods&1)!=0&&input.Key==9)return false;
   IntPtr window=input.Window;long time=clock.ElapsedMilliseconds;var step=new ActionStep{Kind="Key",Key=input.Key,Mods=input.Mods,AltGr=input.AltGr,AppPath=Native.ProcessPath(window),WindowTitle=Native.WindowTitle(window),WindowClass=Native.WindowClass(window)};
   Enqueue(()=>{if(mouseDown!=null)throw new InvalidOperationException("Keys typed while the mouse is held are not supported; record operations separately.");Add(step,window,time);});return false;
  }
  IntPtr MouseHook(int code,IntPtr message,IntPtr data){
   if(code>=0&&active)try{var input=(Native.MouseEvent)Marshal.PtrToStructure(data,typeof(Native.MouseEvent));if((input.Flags&1)==0){int msg=message.ToInt32();if(new[]{0x201,0x202,0x204,0x205,0x207,0x208,0x20A,0x20B,0x20C,0x20E}.Contains(msg)){
    IntPtr window=Native.GetAncestor(Native.WindowFromPhysicalPoint(input.Point),2);if(!Own(window)){int mods=engine.CurrentMods;long time=clock.ElapsedMilliseconds;Enqueue(()=>MouseEvent(msg,input,window,mods,time));}
   }}}catch(Exception error){Fail(error.Message);}return Native.CallNextHookEx(mouseHook,code,message,data);
  }
  void Enqueue(Action action){lock(sync){Task previous=queue;queue=Task.Run(async()=>{await previous.ConfigureAwait(false);if(Error.Length>0)return;try{action();Notify();}catch(Exception e){Fail(e.Message);}});}}
  void MouseEvent(int message,Native.MouseEvent input,IntPtr window,int mods,long time){
   if(message==0x20A||message==0x20E){var scroll=Position(input,window,mods);scroll.Kind="Scroll";scroll.Horizontal=message==0x20E;scroll.ScrollDelta=(short)(input.Data>>16);Add(scroll,window,time);return;}
   bool down=message==0x201||message==0x204||message==0x207||message==0x20B;int button=message==0x20B||message==0x20C?((input.Data>>16)==1?3:4):message==0x204||message==0x205?1:message==0x207||message==0x208?2:0;
   if(down){if(mouseDown!=null)throw new InvalidOperationException("Multiple mouse buttons cannot be recorded at the same time.");mouseDown=Position(input,window,mods);mouseDown.MouseButton=button;mouseDown.Kind="Mouse click";mouseWindow=window;mouseTime=time;return;}
   if(mouseDown==null)return;if(window!=mouseWindow||button!=mouseDown.MouseButton)throw new InvalidOperationException("Drag and drop between windows could not be completed in this record.");var step=mouseDown;step.Duration=(int)Math.Min(30000,Math.Max(1,time-mouseTime));mouseDown=null;var end=Position(input,window,mods);
   if(Math.Abs(end.X-step.X)>5||Math.Abs(end.Y-step.Y)>5){step.Kind="Mouse drag";step.X2=end.X;step.Y2=end.Y;}
   else if(button==0&&mods==0&&step.Duration<200)TryAccessible(step,input,window);
   Add(step,window,mouseTime,time);
  }
  static ActionStep Position(Native.MouseEvent input,IntPtr window,int mods){var rect=AutomationElement.FromHandle(window).Current.BoundingRectangle;if(rect.IsEmpty||rect.Width<=0||rect.Height<=0)throw new InvalidOperationException("Record window dimensions could not be read.");return new ActionStep{X=input.Point.X-rect.Left,Y=input.Point.Y-rect.Top,WindowWidth=rect.Width,WindowHeight=rect.Height,ScaleWithWindow=true,Mods=mods};}
  internal static void TryAccessible(ActionStep step,Native.MouseEvent input,IntPtr window){try{
   var element=AutomationElement.FromPoint(new Point(input.Point.X,input.Point.Y));if(element==null)return;object pattern;if(!element.TryGetCurrentPattern(InvokePattern.Pattern,out pattern))return;
   string id=element.Current.AutomationId,name=element.Current.Name;if(id.Length==0&&name.Length==0)return;var conditions=new List<System.Windows.Automation.Condition>{new PropertyCondition(AutomationElement.ControlTypeProperty,element.Current.ControlType)};if(id.Length>0)conditions.Add(new PropertyCondition(AutomationElement.AutomationIdProperty,id));if(name.Length>0)conditions.Add(new PropertyCondition(AutomationElement.NameProperty,name));
   var root=AutomationElement.FromHandle(window);var matches=root.FindAll(TreeScope.Descendants,new AndCondition(conditions.ToArray()));if(matches.Count!=1)return;step.Kind="Click element";step.AutomationId=id;step.Text=name;step.ControlType=element.Current.ControlType.Id;
  }catch(ElementNotAvailableException){}catch(InvalidOperationException){}catch(COMException){} }
  void Add(ActionStep step,IntPtr window,long time,long endTime=-1){
   string path=string.IsNullOrEmpty(step.AppPath)?Native.ProcessPath(window):step.AppPath;if(string.IsNullOrWhiteSpace(path))throw new InvalidOperationException("Record target could not be determined.");step.AppPath=path;if(step.WindowTitle==null)step.WindowTitle=Native.WindowTitle(window);if(step.WindowClass==null)step.WindowClass=Native.WindowClass(window);
   lock(sync){if(steps.Count>0){long gap=Math.Max(0,time-previousTime);while(gap>=180){int delay=(int)Math.Min(10000,gap);steps.Add(new ActionStep{Kind="Wait",Delay=delay});gap-=delay;}}
    if(lastWindow!=window){steps.Add(new ActionStep{Kind="Focus",AppPath=path,WindowTitle=step.WindowTitle,WindowClass=step.WindowClass});lastWindow=window;}
    steps.Add(step);previousTime=endTime<0?time:endTime;if(steps.Count>480)throw new InvalidOperationException("Record reached 480 steps. Split into shorter macros.");
   }
  }
  void Notify(){if(Application.Current==null||Interlocked.Exchange(ref notificationPending,1)!=0)return;Application.Current.Dispatcher.BeginInvoke(new Action(()=>{Interlocked.Exchange(ref notificationPending,0);var handler=Changed;if(handler!=null)handler();}));}
  void Fail(string message){lock(sync){if(Error.Length==0)Error=message;}var handler=StopRequested;if(handler!=null&&Application.Current!=null)Application.Current.Dispatcher.BeginInvoke(handler);}
  void StopInput(){active=false;engine.InputObserver=null;if(mouseHook!=IntPtr.Zero){Native.UnhookWindowsHookEx(mouseHook);mouseHook=IntPtr.Zero;}clock.Stop();engine.ResetPending();engine.Paused=paused;}
  public async Task<List<ActionStep>> StopAsync(){if(active)StopInput();Task pending;lock(sync)pending=queue;await pending;if(mouseDown!=null&&Error.Length==0)Error="Recording stopped without releasing the mouse button. Record again.";return Steps;}
  internal void FeedForTest(ActionStep step,IntPtr window,long time){Enqueue(()=>Add(step,window,time));}
  internal bool ObserveForTest(PhysicalKey input){return OnKey(input);}
  internal void MouseForTest(int message,Native.MouseEvent input,IntPtr window,int mods,long time){Enqueue(()=>MouseEvent(message,input,window,mods,time));}
  public void Dispose(){if(disposed)return;disposed=true;if(active)StopInput();}
 }
}
