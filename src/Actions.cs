using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.Generic;
using System.Windows.Automation;
using System.Threading;
using System.Linq;
using System.Runtime.InteropServices;

namespace AkilliKisayol {
 public static class Actions {
  static bool workflowBusy;
  public static int StepIntervalMs=15;
  static CancellationTokenSource workflowCancellation;
  public static bool WorkflowRunning{get{return workflowBusy;}}
  public static void CancelWorkflow(){if(workflowCancellation!=null)workflowCancellation.Cancel();}
  public static async void Run(Binding b,IntPtr window,Action<string> done) {
   try {
    if(b.Kind=="Workflow") {
     if(workflowBusy){done("Another operation sequence is running; new trigger was skipped.");return;}
     workflowBusy=true;workflowCancellation=new CancellationTokenSource();try{await RunSteps(b,window,workflowCancellation.Token);done("Operation sequence sent • "+b.Steps.Count+" step. The application's result was not verified.");}finally{workflowCancellation.Dispose();workflowCancellation=null;workflowBusy=false;}return;
    }
    if(b.Kind=="Send key" || b.Kind=="Media") {
     if(b.Kind=="Send key" && Native.GetForegroundWindow()!=window) throw new InvalidOperationException("Foreground window changed; key was not sent to the wrong application.");
     int key=b.SendKey;
     if(b.Kind=="Media") switch(b.Target) {case "Play / pause":key=179;break;case "Next track":key=176;break;case "Previous track":key=177;break;case "Volume up":key=175;break;case "Volume down":key=174;break;case "Mute / unmute":key=173;break;default:throw new ArgumentException("Unknown media action.");}
     Native.Send(b.Kind=="Media"?0:b.SendMods,key);done("Key sent to system; target's processing status not verified.");return;
    }
    string result=await Task.Run(()=>Execute(b));done(result);
   } catch(OperationCanceledException) {done("Operation sequence cancelled; sent steps cannot be retrieved.");} catch(Exception e) {done("ERROR • "+e.Message);}
  }
  internal static async Task RunSteps(Binding b,IntPtr window,CancellationToken cancellation=default(CancellationToken)){
   for(int index=0;index<b.Steps.Count;index++){var step=b.Steps[index];
    cancellation.ThrowIfCancellationRequested();
    if(step.Kind=="Open application"||step.Kind=="Open content"){
     await Task.Run(()=>AppIntegration.Open(step.AppPath,step.Kind=="Open content"?step.Text:"",step.Kind=="Open application"?step.Text:""));continue;
    }
    if(step.Kind=="Focus"){window=await Focus(step.AppPath,cancellation,step.WindowTitle,step.WindowClass);continue;}
    if(step.Kind=="PowerShell"||step.Kind=="Terminal (cmd)"){string result=await Task.Run(()=>Execute(new Binding{Kind=step.Kind,Target=step.Text},cancellation));if(!result.EndsWith(" 0"))throw new InvalidOperationException(result);continue;}
    if(step.Kind!="Wait"){EnsureTarget(window);if(!string.IsNullOrEmpty(step.AppPath)&&!Rules.SameApp(step.AppPath,Native.ProcessPath(window)))throw new InvalidOperationException("The target application for this step is not in the foreground. First, add a step to focus on the application.");}
    if(step.Kind=="Wait")await Task.Delay(step.Delay,cancellation);
    else if(step.Kind=="Key")Native.SendChord(step.Mods,step.Key,step.AltGr);
    else if(step.Kind=="Text")Native.TypeText(step.Text);
    else if(step.Kind=="Click button")await Task.Run(()=>InvokeElement(window,step.Text));
    else if(step.Kind=="Click element")await Task.Run(()=>InvokeRecorded(window,step));
    else if(step.Kind=="Mouse click"||step.Kind=="Mouse drag"||step.Kind=="Scroll"){
     var rect=await Task.Run(()=>AutomationElement.FromHandle(window).Current.BoundingRectangle);if(!step.ScaleWithWindow&&(Math.Abs(rect.Width-step.WindowWidth)>2||Math.Abs(rect.Height-step.WindowHeight)>2))throw new InvalidOperationException("The window size changed for the registered mouse step. Restore to the old size or re-record.");await Native.MouseAction(step,rect,window,cancellation);
    }
    if(index<b.Steps.Count-1&&step.Kind!="Wait"&&StepIntervalMs>0)await Task.Delay(StepIntervalMs,cancellation);
   }
  }
  [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr window);
  [DllImport("user32.dll")]static extern bool ShowWindowAsync(IntPtr window,int command);
  internal static async Task<IntPtr> Focus(string path,CancellationToken cancellation,string title=null,string windowClass=null){
   cancellation.ThrowIfCancellationRequested();IntPtr current=Native.GetForegroundWindow();if(current!=IntPtr.Zero&&Rules.SameApp(Native.ProcessPath(current),path)&&Native.ClassMatches(Native.WindowClass(current),windowClass)&&(string.IsNullOrEmpty(title)||Native.WindowTitle(current)==title)){EnsureTarget(current);return current;}
   for(int attempt=0;attempt<60;attempt++){
    cancellation.ThrowIfCancellationRequested();var windows=Native.AppWindows(path).Where(h=>Native.ClassMatches(Native.WindowClass(h),windowClass)).ToList();var exact=windows.Where(h=>!string.IsNullOrEmpty(title)&&Native.WindowTitle(h)==title).ToList();if(exact.Count==1)windows=exact;else if(string.IsNullOrEmpty(title)&&windows.Contains(Native.GetForegroundWindow()))windows=new List<IntPtr>{Native.GetForegroundWindow()};
    if(windows.Count>1)throw new InvalidOperationException("Multiple target windows exist in the same application. Re-record the macro with the target window open.");
    if(windows.Count==1){IntPtr handle=windows[0];Native.ActivateWindow(handle);await Task.Delay(100,cancellation);EnsureTarget(handle);return handle;}
    await Task.Delay(100,cancellation);
   }throw new InvalidOperationException("Target application window not found: "+path);
  }
  internal static void InvokeRecorded(IntPtr window,ActionStep step){
   EnsureTarget(window);var conditions=new List<System.Windows.Automation.Condition>{new PropertyCondition(AutomationElement.IsEnabledProperty,true),new PropertyCondition(AutomationElement.IsOffscreenProperty,false)};
   if(!string.IsNullOrEmpty(step.AutomationId))conditions.Add(new PropertyCondition(AutomationElement.AutomationIdProperty,step.AutomationId));if(!string.IsNullOrEmpty(step.Text))conditions.Add(new PropertyCondition(AutomationElement.NameProperty,step.Text));if(step.ControlType>0)conditions.Add(new PropertyCondition(AutomationElement.ControlTypeProperty,System.Windows.Automation.ControlType.LookupById(step.ControlType)));
   var elements=AutomationElement.FromHandle(window).FindAll(TreeScope.Descendants,new AndCondition(conditions.ToArray()));if(elements.Count!=1)throw new InvalidOperationException("Registered item not found or multiple matches: "+step.Text);object pattern;if(!elements[0].TryGetCurrentPattern(InvokePattern.Pattern,out pattern))throw new InvalidOperationException("Registered item no longer supports the click contract.");EnsureTarget(window);((InvokePattern)pattern).Invoke();
  }
  internal static void EnsureTarget(IntPtr window){if(window==IntPtr.Zero||Native.GetForegroundWindow()!=window)throw new InvalidOperationException("The foreground window changed; the remaining steps were cancelled. Sent steps cannot be undone.");}
  internal static void InvokeElement(IntPtr window,string name){
   EnsureTarget(window);var root=AutomationElement.FromHandle(window);
   var elements=root.FindAll(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.NameProperty,name),new PropertyCondition(AutomationElement.IsEnabledProperty,true),new PropertyCondition(AutomationElement.IsOffscreenProperty,false)));
   var targets=new List<InvokePattern>();foreach(AutomationElement element in elements){object pattern;if(element.TryGetCurrentPattern(InvokePattern.Pattern,out pattern))targets.Add((InvokePattern)pattern);}
   if(targets.Count!=1)throw new InvalidOperationException(targets.Count==0?"“"+name+"\" accessible button not found. Check the visible label or use key sending.":"“"+name+"\" multiple buttons exist; operation was stopped because the target is ambiguous.");
   EnsureTarget(window);targets[0].Invoke();
  }
  internal static string Execute(Binding b,CancellationToken cancellation=default(CancellationToken)) {
   if(b.Kind=="Application"){AppIntegration.Open(b.Target,"",b.Arguments);return "Open request sent to application • "+AppLibrary.Name(b.Target);}
   if(b.Kind=="Browser website"){using(var browser=Process.Start(BrowserSupport.StartInfo(b))){}return "Open request "+BrowserSupport.DisplayName(b.BrowserPath)+" sent to your browser: "+b.Target+" • Page loading was not verified.";}
   if(b.Kind=="PowerShell" || b.Kind=="Terminal (cmd)") {
    var info=new ProcessStartInfo();info.UseShellExecute=false;info.CreateNoWindow=true;
    if(b.Kind=="PowerShell") {info.FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell\\v1.0\\powershell.exe");info.Arguments="-NoLogo -NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(b.Target));}
    else {info.FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"cmd.exe");info.Arguments="/d /s /c \""+b.Target+"\"";}
    using(var p=Process.Start(info)) {while(!p.WaitForExit(100)){if(cancellation.IsCancellationRequested){try{p.Kill();}catch(InvalidOperationException){}cancellation.ThrowIfCancellationRequested();}}return "Command completed • exit code "+p.ExitCode;}
   }
   string target=b.Kind=="Spotify"?Rules.NormalizeSpotify(b.Target):b.Target;
   var start=new ProcessStartInfo(target) {UseShellExecute=true};
   if(b.Kind=="Application") {start.Arguments=b.Arguments;start.WorkingDirectory=Path.GetDirectoryName(target);}
   Process.Start(start);
   if(b.Kind=="Spotify")return "Spotify content is being sent to the system; playback not verified. Network may be required.";
   return "Request to open sent to system • "+b.Kind;
  }
 }
}
