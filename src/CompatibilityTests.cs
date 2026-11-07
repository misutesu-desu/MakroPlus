using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace AkilliKisayol {
 public static class CompatibilityTests {
  public static int Real(string modelPath,bool remote=false){
   string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,(remote?"compat-api-":"compat-")+Path.GetFileNameWithoutExtension(modelPath)+".json");var results=new List<object>();
   using(var engine=new LocalLlm())using(var job=new ProcessJob())using(var process=new Process())try{
    var options=new ModelOptions();if(remote){var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();string credential=Guid.NewGuid().ToString("N");process.StartInfo=new ProcessStartInfo(LocalLlm.FindRuntime()){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true,Arguments="--model "+LocalLlm.Quote(modelPath)+" --alias compatibility-model --host 127.0.0.1 --port "+port+" --ctx-size 8192 --parallel 1 --gpu-layers auto --reasoning off --jinja --no-webui --no-agent"};process.StartInfo.EnvironmentVariables["LLAMA_API_KEY"]=credential;process.OutputDataReceived+=(s,e)=>{};process.ErrorDataReceived+=(s,e)=>{};process.Start();job.Assign(process);process.BeginOutputReadLine();process.BeginErrorReadLine();using(var client=new HttpClient(new HttpClientHandler{UseProxy=false}){Timeout=TimeSpan.FromSeconds(2)}){client.DefaultRequestHeaders.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",credential);var deadline=DateTime.UtcNow.AddMinutes(3);while(true){if(process.HasExited)throw new Exception("Compatibility server exited.");try{using(var response=client.GetAsync("http://127.0.0.1:"+port+"/health").GetAwaiter().GetResult())if(response.IsSuccessStatusCode)break;}catch(HttpRequestException){}catch(System.Threading.Tasks.TaskCanceledException){}if(DateTime.UtcNow>deadline)throw new TimeoutException("Compatibility server startup timed out.");Thread.Sleep(100);}}options=new ModelOptions{Remote=true,Endpoint="http://127.0.0.1:"+port+"/v1/",Model="compatibility-model"};options.ApiKey=credential;}
    var state=Storage.Read(Storage.FilePath);string browser=state.ApplicationPaths.FirstOrDefault(p=>File.Exists(p)&&Path.GetFileName(p).Equals("brave.exe",StringComparison.OrdinalIgnoreCase))??state.ApplicationPaths.FirstOrDefault(p=>File.Exists(p)&&BrowserSupport.IsBrowser(p));
    string[] prompts={"When I press F8, select all text and copy it.","Assign F9 to type \"Hello\", wait 100 ms, then press Enter.","When I press XButton1, press Ctrl+S.","Copy the selected text.","Copy the selected text.","Open https://www.youtube.com/ in the selected browser when I press F7.","When I press F8, copy the selected text."};
    for(int i=0;i<prompts.Length;i++){
     var chosen=options.Copy();chosen.StructuredOutput=i!=6;var request=new LlmRequest{Prompt=prompts[i],ModelPath=modelPath,Applications=i==5?new List<string>{browser}:new List<string>(),TargetApp=i==5?browser:"",Gesture=i==3?GesturePromptTests.Shape():null,Options=chosen};
     var watch=Stopwatch.StartNew();try{
      var plan=engine.AnalyzeAsync(request,p=>{},CancellationToken.None).GetAwaiter().GetResult();bool valid;
      if(i==4)valid=!plan.Ready;
      else if(!plan.Ready)valid=false;
      else if(i==5)valid=plan.Binding.Key==118&&plan.Binding.AppPath==""&&(plan.Binding.Kind=="Browser website"?Rules.SameApp(plan.Binding.BrowserPath,browser)&&plan.Binding.Target=="https://www.youtube.com/":plan.Binding.Steps.Any(s=>s.Kind=="Open content"&&Rules.SameApp(s.AppPath,browser)&&s.Text=="https://www.youtube.com/"));
      else{var steps=plan.Binding.Steps;valid=i==0?plan.Binding.Key==119&&steps.Count==2&&steps[0].Key==65&&steps[1].Key==67:i==1?plan.Binding.Key==120&&steps.Count==3&&steps[0].Text=="Hello"&&steps[1].Delay==100&&steps[2].Key==13:i==2?plan.Binding.Key==5&&steps.Count==1&&steps[0].Key==83&&steps[0].Mods==2:i==3?plan.Binding.Gesture!=null&&plan.Binding.Key==0&&steps.Count==1&&steps[0].Key==67:plan.Binding.Key==119&&steps.Count==1&&steps[0].Key==67;}
      results.Add(new{prompt=prompts[i],structured=i!=6,passed=valid,milliseconds=watch.ElapsedMilliseconds,ready=plan.Ready,errors=plan.Errors,binding=plan.Binding,response=valid?null:engine.LastResponse});
     }catch(Exception error){results.Add(new{prompt=prompts[i],structured=i!=6,passed=false,milliseconds=watch.ElapsedMilliseconds,error=error.Message,response=engine.LastResponse,diagnostics=engine.LastDiagnostics});}
    }
    bool passed=results.All(r=>(bool)r.GetType().GetProperty("passed").GetValue(r,null));File.WriteAllText(file,LocalLlm.Json().Serialize(new{status=passed?"PASS":"FAIL",model=Path.GetFileName(modelPath),results=results}));return passed?0:1;
   }catch(Exception error){File.WriteAllText(file,LocalLlm.Json().Serialize(new{status="FAIL",model=Path.GetFileName(modelPath),error=error.ToString(),results=results}));return 1;}
  }
 }
}
