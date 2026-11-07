using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace AkilliKisayol {
 public sealed class LlmRequest {
  public string Prompt,SelectedApp,TargetApp,Trigger,ModelPath;public bool Gpu=true;public List<string> Applications;
  public UserPreferences Preferences;
  public ModelOptions Options;
  public ShapeTemplate Gesture;
 }
 public sealed class LocalLlm : IDisposable {
  public static readonly string DefaultModelPath="";
  public static readonly LocalLlm Shared=new LocalLlm();
  readonly ModelTransport remoteTransport=new ModelTransport();
  readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);readonly object sync=new object();Session session;
  internal string LastDiagnostics="",LastResponse="";internal bool SimulateGpuFailureForTest;internal int StartupCount,FallbackCount;internal string BackendForTest {get{return session==null?"none":session.Gpu?"GPU":"CPU";}}
  public static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=262144,RecursionLimit=48};}
  public static string FindRuntime(){
   var root=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);for(int i=0;root!=null&&i<5;i++,root=root.Parent){string path=Path.Combine(root.FullName,"runtime","llama","llama-server.exe");if(File.Exists(path))return path;}
   throw new FileNotFoundException("Local model engine not found. Install with setup-runtime.ps1 and rebuild the application.");
  }
  public async Task<PromptPlan> AnalyzeAsync(LlmRequest request,Action<string> progress,CancellationToken cancellation){
   if(string.IsNullOrWhiteSpace(request.Prompt))throw new ArgumentException("Write what you want to do.");
   if(request.Prompt.Length>6000)throw new ArgumentException("Limit request to 6000 characters or split into separate batches.");
   if(request.Gesture!=null){string issue=ShapeRecognition.Validate(request.Gesture);if(issue!=null)throw new ArgumentException(issue);}
   if(request.Options!=null&&request.Options.Remote)return await AnalyzeRemote(request,progress,cancellation);
   request.ModelPath=ModelFiles.Resolve(request.ModelPath);
   if(!File.Exists(request.ModelPath))throw new FileNotFoundException("GGUF model file not found. Put your downloaded .gguf in the models folder next to MakroPlus.exe, or choose a file using Change model.");
   using(var stream=File.OpenRead(request.ModelPath)){var header=new byte[4];if(stream.Read(header,0,4)!=4||Encoding.ASCII.GetString(header)!="GGUF")throw new InvalidDataException("Selected file is not a GGUF model.");}
   await gate.WaitAsync(cancellation);
   try{
    Session active=null;bool fallback=false;
    bool fast=request.Options==null||request.Options.FastMode;
    try{lock(sync){if(session==null||session.Exited||!Rules.SameApp(session.Model,request.ModelPath)||session.RequestedGpu!=request.Gpu||session.Fast!=fast){if(session!=null)session.Dispose();if(request.Gpu&&SimulateGpuFailureForTest)throw new InvalidOperationException("Test GPU unavailable");session=Start(request.ModelPath,request.Gpu,fast);StartupCount++;}active=session;}await Prepare(active,progress,cancellation);}
    catch(Exception error){if(!request.Gpu||error is OperationCanceledException||cancellation.IsCancellationRequested)throw;Stop();progress("GPU could not be started. Preparing the same model on CPU…");lock(sync){session=Start(request.ModelPath,false,fast);session.RequestedGpu=true;active=session;StartupCount++;FallbackCount++;}fallback=true;}
    if(fallback)await Prepare(active,progress,cancellation);
    using(var client=Client(active)){
     progress((active.Gpu?"GPU":"CPU")+" • Interpreting your request...");
     string previousOutput=null,validationError=null;
     for(int attempt=0;attempt<2;attempt++){
     var payload=await Task.Run(()=>LlmProtocol.CreatePayload(request,previousOutput,validationError),cancellation);
     using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation)){
      timeout.CancelAfter(TimeSpan.FromMinutes(active.Gpu?2:10));
      using(var message=new HttpRequestMessage(HttpMethod.Post,"v1/chat/completions")){message.Content=new StringContent(Json().Serialize(payload),Encoding.UTF8,"application/json");
      using(var response=await client.SendAsync(message,HttpCompletionOption.ResponseHeadersRead,timeout.Token)){
       string body=await ModelTransport.ReadBody(response.Content,timeout.Token);LastDiagnostics=active.ErrorTail();
       if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Model request could not be completed (HTTP "+(int)response.StatusCode+"). Check GPU memory or try with CPU.");
       string content=ModelWire.Completion(body);
       LastResponse=content;
       try{var plan=LlmProtocol.ReadPlan(content,request);if(plan.ModelProposedReady&&!plan.Ready)throw new InvalidDataException(string.Join(" ",plan.Errors));return plan;}
       catch(InvalidDataException error){if(attempt!=0)throw;previousOutput=content;validationError=error.Message;progress("Model plan is being validated; one correction is requested for the format error…");}
       catch(ArgumentException error){if(attempt!=0)throw;previousOutput=content;validationError=error.Message;progress("Model plan is being validated; JSON format is being corrected once…");}
      }}
     }
     }
     throw new InvalidDataException("Could not create a valid operation plan.");
    }
   }finally{gate.Release();}
  }
  async Task Prepare(Session active,Action<string> progress,CancellationToken cancellation){
   cancellation.ThrowIfCancellationRequested();if(active.Ready&&!active.Exited)return;
   using(var client=Client(active)){progress((active.Gpu?"GPU":"CPU")+" • Model is being prepared…");DateTime deadline=DateTime.UtcNow.AddMinutes(3);
    while(true){cancellation.ThrowIfCancellationRequested();if(active.Exited)throw new InvalidOperationException("Model engine closed. "+active.ErrorTail());using(var probe=CancellationTokenSource.CreateLinkedTokenSource(cancellation)){probe.CancelAfter(1000);try{using(var response=await client.GetAsync("health",probe.Token)){if(response.IsSuccessStatusCode){active.Ready=true;return;}}}catch(HttpRequestException){}catch(OperationCanceledException){cancellation.ThrowIfCancellationRequested();}}if(DateTime.UtcNow>deadline)throw new TimeoutException("Model is not ready within 3 minutes. Check memory and model file.");await Task.Delay(100,cancellation);}
   }
  }
  static HttpClient Client(Session active){var client=new HttpClient(new HttpClientHandler{UseProxy=false,AllowAutoRedirect=false}){BaseAddress=new Uri("http://127.0.0.1:"+active.Port+"/"),Timeout=TimeSpan.FromMinutes(12)};client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",active.Key);return client;}
  async Task<PromptPlan> AnalyzeRemote(LlmRequest request,Action<string> progress,CancellationToken cancellation){
   string error=request.Options.Validate("");if(error!=null)throw new ArgumentException(error);string previous=null,validation=null;progress("Server • Interpreting your request…");
   for(int attempt=0;attempt<2;attempt++){
    var payload=(Dictionary<string,object>)await Task.Run(()=>LlmProtocol.CreatePayload(request,previous,validation),cancellation);
    string content=await remoteTransport.Complete(request.Options,payload,progress,cancellation);LastResponse=content;
    try{var plan=LlmProtocol.ReadPlan(content,request);if(plan.ModelProposedReady&&!plan.Ready)throw new InvalidDataException(string.Join(" ",plan.Errors));return plan;}
    catch(InvalidDataException e){if(attempt!=0)throw;previous=content;validation=e.Message;}
    catch(ArgumentException e){if(attempt!=0)throw;previous=content;validation=e.Message;}
    progress("Requesting one correction to the model's plan…");
   }throw new InvalidDataException("The model did not return a valid plan.");
  }
  static Session Start(string model,bool gpu,bool fast){
   string exe=FindRuntime();int port;var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
   var active=new Session{Model=model,Gpu=gpu,RequestedGpu=gpu,Fast=fast,Port=port,Key=Guid.NewGuid().ToString("N")+Guid.NewGuid().ToString("N")};
   var start=new ProcessStartInfo(exe){WorkingDirectory=Path.GetDirectoryName(exe),UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardError=true,RedirectStandardOutput=true};
   start.Arguments="--model "+Quote(model)+" --host 127.0.0.1 --port "+port+" --alias makro-local --ctx-size 8192 --parallel 1 --batch-size 512 --ubatch-size 256 --flash-attn auto --cache-prompt --gpu-layers "+(gpu?"auto":"0")+" --reasoning "+(fast?"off":"auto")+" --jinja --no-webui --no-agent";
   foreach(string name in start.EnvironmentVariables.Keys.Cast<string>().Where(k=>k.StartsWith("LLAMA_",StringComparison.OrdinalIgnoreCase)).ToList())start.EnvironmentVariables.Remove(name);
   start.EnvironmentVariables["LLAMA_API_KEY"]=active.Key;
   active.Process=new Process{StartInfo=start};active.Process.ErrorDataReceived+=(s,e)=>active.AddLog(e.Data);active.Process.OutputDataReceived+=(s,e)=>{};
   try{active.Job=new ProcessJob();active.Process.Start();active.Job.Assign(active.Process);active.Process.BeginErrorReadLine();active.Process.BeginOutputReadLine();return active;}
   catch{active.Dispose();throw;}
  }
  internal static string Quote(string value){return "\""+value.Replace("\"","\\\"")+"\"";}
  public void Stop(){lock(sync){if(session!=null){session.Dispose();session=null;}}}
  public void Dispose(){Stop();remoteTransport.Dispose();}
  sealed class Session:IDisposable {
   public string Model,Key;public int Port;public bool Gpu,RequestedGpu,Fast,Ready;public Process Process;public ProcessJob Job;readonly Queue<string> errors=new Queue<string>();
   public bool Exited{get{try{return Process==null||Process.HasExited;}catch(InvalidOperationException){return true;}}}
   public void AddLog(string text){if(string.IsNullOrWhiteSpace(text))return;lock(errors){errors.Enqueue(text);while(errors.Count>12)errors.Dequeue();}}
   public string ErrorTail(){lock(errors){string text=string.Join("\n",errors);return text.Length>1800?text.Substring(text.Length-1800):text;}}
   public void Dispose(){if(Job!=null){Job.Dispose();Job=null;}if(Process!=null){try{if(!Process.HasExited)Process.Kill();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}Process.Dispose();}}
  }
 }
 internal sealed class ProcessJob:IDisposable {
  IntPtr handle;
  [StructLayout(LayoutKind.Sequential)]struct BasicLimits{public long ProcessTime,JobTime;public uint Flags;public UIntPtr MinWorking,MaxWorking;public uint ActiveProcesses;public UIntPtr Affinity;public uint Priority,Scheduling;}
  [StructLayout(LayoutKind.Sequential)]struct Io{public ulong ReadOps,WriteOps,OtherOps,ReadBytes,WriteBytes,OtherBytes;}
  [StructLayout(LayoutKind.Sequential)]struct Limits{public BasicLimits Basic;public Io Io;public UIntPtr ProcessMemory,JobMemory,PeakProcessMemory,PeakJobMemory;}
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateJobObject(IntPtr attributes,string name);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetInformationJobObject(IntPtr job,int type,ref Limits limits,int size);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
  [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
  public ProcessJob(){handle=CreateJobObject(IntPtr.Zero,null);var limits=new Limits();limits.Basic.Flags=0x2000;if(handle==IntPtr.Zero||!SetInformationJobObject(handle,9,ref limits,Marshal.SizeOf(typeof(Limits)))){Dispose();throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"Model process could not be managed.");}}
  public void Assign(Process process){if(!AssignProcessToJobObject(handle,process.Handle))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"Model process could not connect to the application.");}
  public void Dispose(){if(handle!=IntPtr.Zero){CloseHandle(handle);handle=IntPtr.Zero;}}
 }
}
