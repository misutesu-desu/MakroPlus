using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace AkilliKisayol {
 public static class BrowserTests {
  public const string UserPrompt="open youtube website on brave browser do it when ı press del + 5";
  static string Reply(string app,string url){return LocalLlm.Json().Serialize(new {status="ready",name="YouTube in Brave",trigger="Del+5",app_id=app,message="",notes=new string[0],steps=new[]{new {kind="open_website",combo="",text=url,ms=0}}});}
  public static void Unit(Action<bool,string> check){
   TriggerSpec gesture;check(TriggerSpec.TryParse("del + 5",out gesture)&&gesture.Prefix==46&&gesture.Key==53&&gesture.Mods==0,"Del + 5 iki ana tuş olarak çözümlenir");check(TriggerSpec.Mentioned(UserPrompt,gesture),"Kullanıcının özgün promptunda Del + 5 bulunur");check(TriggerSpec.FindInPrompt(UserPrompt)=="Del+5","İngilizce promptta Türkçe ı harfi tuş çözümünü bozmaz");
   check(!TriggerSpec.TryParse("A+5",out gesture),"Yazı tuşları ön tuş olarak yanlışlıkla seçilmez");check(TriggerSpec.TryParse("Ctrl+Alt+5",out gesture)&&gesture.Prefix==0&&gesture.Mods==3,"Standart kısayollar korunur");
   string root=Path.Combine(Path.GetTempPath(),"BraveRegression-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string brave=Path.Combine(root,"brave.exe"),chrome=Path.Combine(root,"chrome.exe");File.WriteAllText(brave,"");File.WriteAllText(chrome,"");
   try{
    var request=new LlmRequest{Prompt=UserPrompt,Applications=new List<string>{chrome,brave},SelectedApp="",Trigger="",ModelPath=LocalLlm.DefaultModelPath};
    check(BrowserSupport.Target(request)==brave,"Kayıtlı Brave prompttan kesin olarak eşleşir");var apps=LlmProtocol.AppMap(request);string id=apps.Single(p=>p.Value==brave).Key;
    var payload=(Dictionary<string,object>)LocalLlm.Json().DeserializeObject(LocalLlm.Json().Serialize(LlmProtocol.CreatePayload(request)));var messages=(object[])payload["messages"];var user=(Dictionary<string,object>)LocalLlm.Json().DeserializeObject((string)((Dictionary<string,object>)messages.Last())["content"]);check((string)user["target_app_id"]==id&&(string)user["selected_app_id"]=="","Hedef tarayıcı kimliği boş kapsama rağmen modele verilir");
    var plan=LlmProtocol.ReadPlan(Reply(id,"https://www.youtube.com/"),request);check(plan.Ready&&plan.Binding.BrowserPath==brave&&plan.Binding.AppPath==""&&plan.Binding.PrefixKey==46&&plan.Binding.Key==53,"Özgün prompt genel kapsamda Brave web atamasına dönüşür");
    var start=BrowserSupport.StartInfo(plan.Binding);check(start.FileName==brave&&start.Arguments=="\"https://www.youtube.com/\""&&!start.UseShellExecute,"URL varsayılan tarayıcı yerine seçili exe’ye ayrı argüman olur");
    request.TargetApp=brave;string chromeId=apps.Single(p=>p.Value==chrome).Key;bool changed=false;try{LlmProtocol.ReadPlan(Reply(chromeId,"https://www.youtube.com/"),request);}catch(InvalidDataException){changed=true;}check(changed,"LLM seçili hedef tarayıcıyı değiştiremez");
    bool urlChanged=false;try{LlmProtocol.ReadPlan(Reply(id,"https://example.com/"),request);}catch(InvalidDataException){urlChanged=true;}check(urlChanged,"LLM istenmeyen başka web adresi açamaz");
    var state=new State();state.Bindings.Add(plan.Binding);string file=Path.Combine(root,"settings.json");Storage.Write(state,file);var saved=Storage.Read(file).Bindings.Single();check(saved.PrefixKey==46&&saved.BrowserPath==brave&&saved.Target=="https://www.youtube.com/","Tarayıcı ve iki tuşlu tetikleyici kalıcı saklanır");
    var single=new Binding{Key=46,Mods=0,Name="Delete",Kind="Web address",Target="https://example.com"};check(Rules.Conflicts(plan.Binding,new[]{single}).Any(x=>x.StartsWith("CONFLICT")),"Ön tuşun ayrı atamayla çakışması yakalanır");
    check(Rules.Resolve(new[]{plan.Binding},0,53,"",0)==null&&Rules.Resolve(new[]{plan.Binding},0,53,"",46)==plan.Binding,"Del + 5 yalnızca 5 tuşuyla tetiklenmez");
   }finally{foreach(string file in Directory.GetFiles(root))File.Delete(file);Directory.Delete(root);}
  }
  public static void Engine(Action<bool,string> check){
   var binding=new Binding{Name="Del5",PrefixKey=46,Key=53,Kind="Web address",Target="https://example.com"};var bindings=new List<Binding>{binding};int fires=0,replays=0,raws=0;using(var engine=new KeyboardEngine()){
    engine.Bindings=()=>bindings;engine.ReplayKey=(m,k)=>{if(k==46)replays++;};engine.ReplayRaw=keys=>{if(keys.SequenceEqual(new[]{46,54}))raws++;};engine.Fire=(b,w)=>fires++;
    check(engine.Handle(new Native.Keyboard{vk=46}),"Del ön tuşu beklerken kaynağa sızmaz");check(engine.Handle(new Native.Keyboard{vk=53}),"Del + 5 ana tuşu engellenir");engine.Handle(new Native.Keyboard{vk=53});engine.Handle(new Native.Keyboard{vk=53,flags=128});engine.Handle(new Native.Keyboard{vk=46,flags=128});engine.FlushForTest();check(fires==1&&replays==0,"Del + 5 bırakılınca yalnızca bir eylem çalışır");
    engine.Handle(new Native.Keyboard{vk=46});engine.Handle(new Native.Keyboard{vk=46,flags=128});engine.FlushForTest();check(replays==1&&fires==1,"Tek başına Del normal işlevine döner");
    engine.Handle(new Native.Keyboard{vk=46});engine.Handle(new Native.Keyboard{vk=162});engine.Handle(new Native.Keyboard{vk=54});engine.Handle(new Native.Keyboard{vk=54,flags=128});engine.Handle(new Native.Keyboard{vk=162,flags=128});engine.Handle(new Native.Keyboard{vk=46,flags=128});engine.FlushForTest();check(replays==2&&raws==0,"Ön tuştan sonra değiştirici eklenirse Del yanlış değiştiriciyle gönderilmez");
    engine.Handle(new Native.Keyboard{vk=46});check(engine.Handle(new Native.Keyboard{vk=54}),"Eşleşmeyen ikinci tuş sıralı geri iletilir");check(!engine.Handle(new Native.Keyboard{vk=46,flags=128}),"Geri iletilen Del’in bırakılması engellenmez");engine.Handle(new Native.Keyboard{vk=54,flags=128});engine.FlushForTest();check(raws==1&&fires==1,"Del + başka tuş kısayolu yanlışlıkla tetiklemez");
    binding.Enabled=false;check(!engine.Handle(new Native.Keyboard{vk=46}),"Devre dışı birleşim Del’i geciktirmez");engine.Handle(new Native.Keyboard{vk=46,flags=128});binding.Enabled=true;
    int captured=-1;engine.Capture=(m,k,p,a)=>{captured=p;};engine.Handle(new Native.Keyboard{vk=46});engine.Handle(new Native.Keyboard{vk=53});Application.Current.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(()=>{}));check(captured==46,"Klavye kaydı Del + 5 ön tuşunu yakalar");engine.Capture=null;engine.Handle(new Native.Keyboard{vk=53,flags=128});engine.Handle(new Native.Keyboard{vk=46,flags=128});
    engine.Handle(new Native.Keyboard{vk=46});engine.Paused=true;engine.Handle(new Native.Keyboard{vk=46,flags=128});engine.FlushForTest();check(replays==2&&fires==1,"Duraklatma bekleyen ön tuştan eylem çıkarmaz");
   }
  }
  public static void Ui(Window owner,KeyboardEngine keyboard,Action<bool,string> check,Action<Window,string> screenshot){
   string root=Path.Combine(Path.GetTempPath(),"BraveUi-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string brave=Path.Combine(root,"brave.exe");File.WriteAllText(brave,"");var library=new List<string>();
   try{
    var window=new PromptWindow(keyboard,new Binding[0],library,appProvider:()=>library){Owner=owner};window.Show();try{
     library.Add(brave);check(window.SelectTargetForTest(brave),"Yeni kayıtlı Brave hedef listesinden doğrudan seçilir");LlmRequest captured=null;
     window.Planner=(request,progress,cancellation)=>{captured=request;string id=LlmProtocol.AppMap(request).Single(p=>p.Value==brave).Key;return System.Threading.Tasks.Task.FromResult(LlmProtocol.ReadPlan(Reply(id,"https://www.youtube.com/"),request));};
     var work=window.StartModelForTest(UserPrompt);check(work.IsCompleted,"Web planı arayüz önizlemesine aktarılır");work.GetAwaiter().GetResult();check(captured.SelectedApp==""&&captured.TargetApp==brave,"Genel kapsam ve Brave hedefi birbirinden ayrıdır");check(window.CanSaveForTest()&&window.PlanForTest().Binding.BrowserPath==brave,"Web planı null adım hatası olmadan kayda hazır görünür");screenshot(window,"brave-prompt-preview.png");
     var saved=window.PlanForTest().Binding;window.Hide();var edit=new PromptWindow(keyboard,new Binding[0],new string[0],previous:saved){Owner=owner};edit.Show();try{check(edit.TriggerForTest()=="Del+5"&&edit.TargetForTest()==brave&&edit.CanSaveForTest(),"Panelden kaldırılmış tarayıcı düzenlemede de seçili kalır");}finally{edit.Close();}
    }finally{window.Close();}
   }finally{File.Delete(brave);Directory.Delete(root);}
  }
  public static int Real(){
   string report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"brave-model-test.json");using(var model=new LocalLlm())try{
    var state=Storage.Read(Storage.FilePath);string brave=state.ApplicationPaths.FirstOrDefault(p=>Path.GetFileName(p).Equals("brave.exe",StringComparison.OrdinalIgnoreCase));if(brave==null)throw new InvalidOperationException("Kayıtlı Brave bulunamadı.");
    var request=new LlmRequest{Prompt=UserPrompt,Applications=state.ApplicationPaths,SelectedApp="",Trigger="",ModelPath=state.ModelPath,Gpu=true};var watch=Stopwatch.StartNew();var plan=model.AnalyzeAsync(request,message=>{},CancellationToken.None).GetAwaiter().GetResult();
    bool correct=plan.Ready&&plan.Binding.AppPath==""&&plan.Binding.PrefixKey==46&&plan.Binding.Key==53&&((plan.Binding.Kind=="Browser website"&&Rules.SameApp(plan.Binding.BrowserPath,brave)&&plan.Binding.Target=="https://www.youtube.com/")||(plan.Binding.Kind=="Workflow"&&plan.Binding.Steps.Count==1&&plan.Binding.Steps[0].Kind=="Open content"&&Rules.SameApp(plan.Binding.Steps[0].AppPath,brave)&&plan.Binding.Steps[0].Text=="https://www.youtube.com/"));
    File.WriteAllText(report,LocalLlm.Json().Serialize(new {status=correct?"PASS":"FAIL",prompt=UserPrompt,milliseconds=watch.ElapsedMilliseconds,errors=plan.Errors,binding=plan.Binding,response=model.LastResponse}));return correct?0:1;
   }catch(Exception e){File.WriteAllText(report,LocalLlm.Json().Serialize(new{status="FAIL",error=e.ToString(),response=model.LastResponse}));return 1;}
  }
 }
}
