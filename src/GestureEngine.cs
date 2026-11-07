using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace AkilliKisayol {
 public sealed class RawInputLease:IDisposable {
  static RawPointerSource source;static readonly List<RawInputLease> listeners=new List<RawInputLease>();readonly Action<PointerFrame> receive;readonly Func<bool> needed;
  public static string Status {get{return source==null?"Pointer input is not active.":source.Status;}}
  public RawInputLease(Action<PointerFrame> input,Func<bool> wanted){receive=input;needed=wanted;if(source==null){source=new RawPointerSource();source.Needed=()=>listeners.Any(l=>l.needed());source.Input+=frame=>{foreach(var listener in listeners.ToArray())if(listener.needed())listener.receive(frame);};}listeners.Add(this);}
  public void Dispose(){listeners.Remove(this);if(listeners.Count==0&&source!=null){source.Dispose();source=null;}}
 }
 public sealed class GestureEngine:IDisposable {
  readonly KeyboardEngine keyboard;readonly Func<State> state;readonly Func<IEnumerable<Binding>> getBindings;readonly RawInputLease lease;readonly DispatcherTimer timer=new DispatcherTimer();readonly List<GesturePoint> stroke=new List<GesturePoint>();List<Binding> bindings=new List<Binding>();
  readonly Dictionary<Binding,PreparedShape> prepared=new Dictionary<Binding,PreparedShape>();readonly Dictionary<Tuple<Binding,Binding>,double> similarities=new Dictionary<Tuple<Binding,Binding>,double>();readonly int ownProcess=System.Diagnostics.Process.GetCurrentProcess().Id;static readonly int[] PointerButtons={1,2,4,5,6};DateTime lastTrace;
  DateTime last,started,lastSample,blockedUntil;double x,y;IntPtr target;string device;bool recording,calibrating,disposed;double absoluteX,absoluteY;bool haveAbsolute;
  public event Action<IList<GesturePoint>> Trace,Recorded;public event Action<string> DeviceDetected,StatusChanged;public event Action<ShapeMatch> Evaluated;
  public string Status="Listening starts when you save a shape.";
  public bool Busy {get{return recording||calibrating;}}
  public GestureEngine(KeyboardEngine engine,Func<State> data,Func<IEnumerable<Binding>> available,bool useRawInput=true){keyboard=engine;state=data;getBindings=available;timer.Interval=TimeSpan.FromMilliseconds(50);timer.Tick+=(s,e)=>Finish(false);keyboard.PhysicalInput+=OnKey;if(useRawInput)lease=new RawInputLease(Feed,NeedsInput);Refresh();}
  public void Refresh(){bindings=getBindings().Where(b=>b.Enabled&&b.Gesture!=null).ToList();prepared.Clear();similarities.Clear();foreach(var binding in bindings)prepared[binding]=new PreparedShape(binding);if(!state().Gestures.Enabled&&!recording&&!calibrating)Reset();}
  bool NeedsInput(){return !disposed&&(Busy||state().Gestures.Enabled&&bindings.Count>0&&!keyboard.Paused&&!Actions.WorkflowRunning&&keyboard.Capture==null);}
  void OnKey(PhysicalKey input){if(!input.Up&&!InputKeys.IsPointer(input.Key)&&!recording)Reset();}
  bool ButtonsDown(){foreach(int key in PointerButtons)if((Native.GetAsyncKeyState(key)&0x8000)!=0)return true;return false;}
  bool OwnWindow(IntPtr window){uint process;Native.GetWindowThreadProcessId(window,out process);return process==ownProcess;}
  double Similarity(Binding a,Binding b){var key=Tuple.Create(a,b);double value;if(!similarities.TryGetValue(key,out value)){value=ShapeRecognition.SimilarityNormalized(prepared[a].Normalized,prepared[b].Normalized);similarities[key]=value;}return value;}
  void Say(string message){Status=message;var handler=StatusChanged;if(handler!=null)handler(message);}
  public void BeginCalibration(){Reset();recording=false;calibrating=true;blockedUntil=DateTime.UtcNow.AddMilliseconds(700);Say("Move one finger on your touchpad. No key or click is required.");}
  public void BeginRecording(){Reset();calibrating=false;recording=true;blockedUntil=DateTime.UtcNow.AddMilliseconds(700);Say("Draw a shape with one finger, then pause.");}
  public void CancelCapture(){recording=calibrating=false;Reset();Say("Capture stopped.");}
  public void Feed(PointerFrame frame){
   if(frame.Window==IntPtr.Zero||!NeedsInput()||DateTime.UtcNow<blockedUntil)return;if(frame.Buttons){Reset();return;}if(frame.X==0&&frame.Y==0)return;
   if(calibrating){calibrating=false;Reset();var detected=DeviceDetected;if(detected!=null)detected(frame.Device??"");Say(string.IsNullOrEmpty(frame.Device)?"Windows Precision Touchpad detected.":"Touchpad source selected. Shape recognition uses this pointer device.");return;}
   if(!string.Equals(frame.Device??"",state().Gestures.Device??"",StringComparison.OrdinalIgnoreCase)){if(stroke.Count>0)Reset();return;}
   if(!recording&&(keyboard.Paused||keyboard.Capture!=null||keyboard.CurrentMods!=0||Actions.WorkflowRunning||ButtonsDown())){Reset();return;}
   if(frame.Absolute){if(!haveAbsolute||device!=frame.Device){absoluteX=frame.X;absoluteY=frame.Y;haveAbsolute=true;device=frame.Device;return;}double dx=frame.X-absoluteX,dy=frame.Y-absoluteY;absoluteX=frame.X;absoluteY=frame.Y;frame=new PointerFrame{Device=frame.Device,X=dx,Y=dy,Window=frame.Window};}
   if(!ShapeRecognition.Finite(frame.X)||!ShapeRecognition.Finite(frame.Y)||Math.Abs(frame.X)>5000||Math.Abs(frame.Y)>5000){Reset();return;}
   DateTime now=DateTime.UtcNow;
   if(stroke.Count>0&&(frame.Window!=target||device!=frame.Device||now-started>TimeSpan.FromSeconds(5))){Reset();return;}
   if(stroke.Count==0){if(!recording&&!AllowOwnWindowForTest&&OwnWindow(frame.Window))return;target=frame.Window;device=frame.Device;started=now;lastSample=DateTime.MinValue;x=y=0;stroke.Add(new GesturePoint(0,0));}
   x+=frame.X;y+=frame.Y;last=now;if((now-lastSample).TotalMilliseconds>=6&&ShapeRecognition.Distance(stroke[stroke.Count-1],new GesturePoint(x,y))>=1){stroke.Add(new GesturePoint(x,y));lastSample=now;if(stroke.Count>512){Reset();Say("Movement was too long; draw a shorter shape.");return;}var trace=Trace;if(recording&&trace!=null&&(now-lastTrace).TotalMilliseconds>=33){lastTrace=now;trace(stroke);}}
   timer.Start();
  }
  public void Finish(bool force){
   if(stroke.Count==0){timer.Stop();return;}if(!force&&(DateTime.UtcNow-last).TotalMilliseconds<state().Gestures.PauseMilliseconds)return;
   var points=stroke.Select(p=>new GesturePoint(p.X,p.Y)).ToList();IntPtr window=target;Reset();
   if(recording){if(ShapeRecognition.Validate(new ShapeTemplate{Points=points})!=null||ShapeRecognition.Length(points)<state().Gestures.MinimumLength){Say("Draw a larger, distinct shape and pause.");return;}recording=false;var handler=Recorded;if(handler!=null)handler(points);Say("Shape recorded. Review it before saving.");return;}
   if(!NeedsInput()||keyboard.CurrentMods!=0||ButtonsDown()||Native.GetForegroundWindow()!=window)return;string app=Native.ProcessPath(window);var choices=bindings.Where(b=>b.Enabled&&(b.AppPath.Length==0||Rules.SameApp(b.AppPath,app))).ToList();
   choices=choices.Where(b=>!string.IsNullOrEmpty(b.ProfileId)||!choices.Any(other=>!string.IsNullOrEmpty(other.ProfileId)&&Rules.SameApp(b.AppPath,other.AppPath)&&Similarity(b,other)>=0.92)).ToList();
   // A matching app-specific shape takes precedence over an identical global shape.
   choices=choices.Where(b=>b.AppPath.Length>0||!choices.Any(other=>other.AppPath.Length>0&&Similarity(b,other)>=0.92)).ToList();var result=ShapeRecognition.MatchPrepared(points,choices.Select(b=>prepared[b]),state().Gestures);var evaluated=Evaluated;if(evaluated!=null)evaluated(result);Say(result.Reason);
   if(result.Binding!=null){keyboard.ScheduleGesture(result.Binding,window);blockedUntil=DateTime.UtcNow.AddMilliseconds(900);}
  }
  public void Reset(){stroke.Clear();timer.Stop();x=y=0;target=IntPtr.Zero;haveAbsolute=false;}
  internal bool AllowOwnWindowForTest;
  internal void PointForTest(double dx,double dy,IntPtr window){blockedUntil=DateTime.MinValue;lastSample=DateTime.MinValue;Feed(new PointerFrame{X=dx,Y=dy,Device=state().Gestures.Device,Window=window});}
  public void Dispose(){if(disposed)return;disposed=true;timer.Stop();keyboard.PhysicalInput-=OnKey;if(lease!=null)lease.Dispose();}
 }
}
