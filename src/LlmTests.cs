using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace AkilliKisayol {
 public static class LlmTests {
  static LlmRequest Request(){return new LlmRequest{Prompt="F8'e basınca kopyala",Trigger="",SelectedApp=System.Reflection.Assembly.GetExecutingAssembly().Location,Applications=new List<string>(),ModelPath=LocalLlm.DefaultModelPath};}
  static string Reply(string status="ready",string kind="copy",string combo="",string text="",int ms=0,string app="app0",string trigger="F8"){
   return LocalLlm.Json().Serialize(new {status=status,name="Test",trigger=trigger,app_id=app,message=status=="ready"?"":"Hangi tuşu kullanmak istersiniz?",notes=new string[0],steps=status=="ready"?new[]{new {kind=kind,combo=combo,text=text,ms=ms}}:new object[0]});
  }
  static bool Rejected(string json,LlmRequest request){try{var plan=LlmProtocol.ReadPlan(json,request);return !plan.Ready;}catch(InvalidDataException){return true;}catch(ArgumentException){return true;}}
  public static void Unit(Action<bool,string> check){
   var request=Request();var plan=LlmProtocol.ReadPlan(Reply(),request);check(plan.Ready&&plan.Binding.AppPath==request.SelectedApp&&plan.Binding.Key==119,"LLM planı mevcut modele dönüştürülür");
   check(Rejected(Reply(app:"unknown"),request),"LLM bilinmeyen uygulama seçemez");check(Rejected(Reply(app:"global"),request),"LLM seçili uygulamayı genel kapsama çeviremez");
   check(Rejected(Reply(kind:"shell",combo:""),request),"Tanımsız araç adı çalıştırılmaz");check(Rejected(Reply(kind:"key",combo:"Ctrl+Alt+Delete"),request),"LLM ayrılmış tuşları gönderemez");
   check(Rejected(Reply(trigger:"F9"),request),"LLM promptta olmayan tetikleyici uyduramaz");
   var missing=Request();missing.Prompt="Copy the selected text in this application.";var question=LlmProtocol.ReadPlan(Reply(trigger:"Del+5"),missing);check(!question.Ready&&!question.ModelProposedReady&&question.Errors.Any(e=>e.Contains("trigger key")),"An invented trigger for a missing user choice becomes clarification without a futile model retry");
   check(Rejected(Reply(kind:"key",combo:"F8"),request),"LLM tetikleyiciyi yanlışlıkla eylem olarak gönderemez");
   check(LlmProtocol.ReadPlan(Reply(kind:"key",combo:"Ctrl+H"),request).Ready,"Model uygulama kısayolunu bilgisiyle önerebilir");
   check(LlmProtocol.ReadPlan(Reply(kind:"key",combo:"Win+E"),request).Ready&&LlmProtocol.ReadPlan(Reply(kind:"key",combo:"F12"),request).Ready,"Tetikleyici sınırlamaları gönderilen Windows/F12 tuşlarına uygulanmaz");
   request.Trigger="Ctrl+Alt+K";plan=LlmProtocol.ReadPlan(Reply(trigger:"F9"),request);check(plan.Ready&&plan.Binding.Mods==3&&plan.Binding.Key==75,"Kullanıcı tuş alanı LLM’den önce gelir");request.Trigger="";
   check(!LlmProtocol.ReadPlan(Reply(status:"clarify"),request).Ready,"LLM eksik bilgi için soru döndürebilir");
   var empty=(Dictionary<string,object>)LocalLlm.Json().DeserializeObject(Reply());empty["steps"]=new object[0];empty["message"]="Which trigger key should I use?";check(!LlmProtocol.ReadPlan(LocalLlm.Json().Serialize(empty),request).Ready,"Eylemsiz model sorusu çalıştırılabilir plana dönüşmez");
   check(Rejected(Reply(kind:"wait",combo:"",ms:10001),request),"LLM bekleme sınırını aşamaz");check(Rejected(Reply(kind:"text",combo:"",text:""),request),"LLM boş metin adımı üretemez");
   var map=(Dictionary<string,object>)LocalLlm.Json().DeserializeObject(Reply());map["code"]="anything";check(Rejected(LocalLlm.Json().Serialize(map),request),"LLM ek kod alanı enjekte edemez");
   var one=(Dictionary<string,object>)((object[])map["steps"])[0];map.Remove("code");one["ms"]=0.5;check(Rejected(LocalLlm.Json().Serialize(map),request),"LLM bekleme türü doğrulanır");
   string unicode="Türkçe ığüşöç";plan=LlmProtocol.ReadPlan(Reply(kind:"text",combo:"",text:unicode),request);check(plan.Binding.Steps[0].Text==unicode,"LLM Unicode metni korunur");
   check(Rejected("{invalid",request),"Bozuk model JSON’u çalıştırılmaz");
   check(File.Exists(LocalLlm.FindRuntime()),"Taşınabilir model motoru bulunur");
   var old=new State();check(old.ModelPath==""&&old.ModelOptions!=null,"New installations do not hardcode a model.");
   var ambiguous=Request();ambiguous.SelectedApp="";ambiguous.Applications=new List<string>{@"C:\A\editor.exe",@"C:\B\editor.exe"};check(!LlmProtocol.ReadPlan(Reply(),ambiguous).Ready,"Aynı adlı uygulamalar LLM sonrasında da denetlenir");
  }
  static void Pump(Task task){var frame=new DispatcherFrame();var timeout=DateTime.UtcNow.AddSeconds(3);var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};timer.Tick+=(s,e)=>{if(task.IsCompleted||DateTime.UtcNow>timeout){timer.Stop();frame.Continue=false;}};timer.Start();Dispatcher.PushFrame(frame);if(!task.IsCompleted)throw new TimeoutException("Asenkron arayüz testi tamamlanmadı.");task.GetAwaiter().GetResult();}
  public static void Ui(Window owner,KeyboardEngine keyboard,IEnumerable<string> paths,Action<bool,string> check){
   string app=System.Reflection.Assembly.GetExecutingAssembly().Location;var window=new PromptWindow(keyboard,new Binding[0],paths,app){Owner=owner};window.Show();
   try{
    var pending=new TaskCompletionSource<PromptPlan>();window.Planner=(r,p,c)=>pending.Task;
    var work=window.StartModelForTest("F8'e basınca kopyala");window.ChangePromptForTest("F9'a basınca yapıştır");pending.SetResult(PromptParser.Parse("F8'e basınca kopyala",new[]{app},app));Pump(work);check(window.PlanForTest()==null&&!window.CanSaveForTest(),"Eski LLM yanıtı değişen prompta uygulanmaz");
    window.Planner=(r,p,c)=>Task.FromResult(PromptParser.Parse("F8'e basınca kopyala",new[]{app},app));work=window.StartModelForTest("F8'e basınca kopyala");Pump(work);check(window.PlanForTest().Ready&&window.CanSaveForTest(),"Doğrulanan LLM planı kaydetmeye hazır gösterilir");
    window.Planner=(r,p,c)=>{throw new InvalidDataException("Test model hatası");};work=window.StartModelForTest("F8'e basınca kopyala");Pump(work);check(window.PlanForTest()==null&&!window.CanSaveForTest(),"Model hatasında gizli kurallı yorumlayıcı geçişi yok");
   }finally{window.Close();}
  }
  public static int Real(string model){
   var results=new List<object>();string report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"llm-test-results.json");int completed=0;
   using(var engine=new LocalLlm())try{
    var request=Request();request.SelectedApp=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"notepad.exe");request.Applications=new List<string>{request.SelectedApp};request.ModelPath=string.IsNullOrWhiteSpace(model)?Storage.Read(Storage.FilePath).ModelPath:model;
    string[] prompts={"When I press F8, select all the text and copy it.","Assign F9 to type \"Hello world\", wait half a second, then press Enter.","Copy the selected text in this application.","When I press F9, detect and remove all silent parts of the video."};
    for(int i=0;i<prompts.Length;i++){
     request.Prompt=prompts[i];var watch=Stopwatch.StartNew();var plan=engine.AnalyzeAsync(request,message=>{},CancellationToken.None).GetAwaiter().GetResult();watch.Stop();
     bool correct=i==0?plan.Ready&&plan.Binding.Steps.Count==2&&plan.Binding.Steps[0].Key==65&&plan.Binding.Steps[1].Key==67:i==1?plan.Ready&&plan.Binding.Steps.Count==3&&plan.Binding.Steps[0].Text=="Hello world"&&plan.Binding.Steps[1].Delay==500&&plan.Binding.Steps[2].Key==13:!plan.Ready;
     results.Add(new {prompt=prompts[i],passed=correct,milliseconds=watch.ElapsedMilliseconds,ready=plan.Ready,errors=plan.Errors,steps=plan.Ready?plan.Binding.Steps:null,diagnostics=engine.LastDiagnostics});if(!correct)throw new InvalidDataException("Gerçek model senaryosu beklentiyi karşılamadı: "+(i+1));completed++;
    }
    File.WriteAllText(report,LocalLlm.Json().Serialize(new {status="PASS",completed=completed,model=request.ModelPath,backend="Vulkan GPU",results=results}));return 0;
   }catch(Exception error){File.WriteAllText(report,LocalLlm.Json().Serialize(new {status="FAIL",completed=completed,error=error.ToString(),last_model_response=engine.LastResponse,results=results}));return 1;}
  }
 }
}
