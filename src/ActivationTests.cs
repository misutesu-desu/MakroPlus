using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace AkilliKisayol {
 public static class ActivationTests {
  public const string Video="https://youtu.be/AbCdEfGhI12",UserPrompt=Video+" bu videonun 17. dakika 54. saniyesini aç";
  static Binding Saved(string app){return new Binding{Id="activation",Name="Open YouTube video",Key=109,Mods=2,ProfileId="work",Enabled=false,Kind="Workflow",Prompt=UserPrompt,Steps=new List<ActionStep>{new ActionStep{Kind="Open content",AppPath=app,Text=Video}}};}
  static string Reply(string id,string kind,string url){return LocalLlm.Json().Serialize(new{status="ready",name="Video",trigger="Ctrl+Subtract",app_id=id,message="",notes=new string[0],steps=new[]{new{kind=kind,combo="",text=url,ms=0}}});}
  public static void Unit(Action<bool,string> check){
   check(BrowserSupport.Website(UserPrompt)==Video+"?t=1074","Özgün Türkçe istekte 17:54 bağlantıya eklenir");
   foreach(string time in new[]{"17:54","17 minutes 54 seconds","17m54s"})check(BrowserSupport.Website("Open "+Video+" at "+time)==Video+"?t=1074","İngilizce video zamanı korunur: "+time);
   check(BrowserSupport.Website("Open "+Video+" at 1:02:03")==Video+"?t=3723","Saatli video zamanı desteklenir");
   check(BrowserSupport.Website("Open "+Video+"?si=abc&t=10#t=20 at 0:00")==Video+"?si=abc&t=0","Önceki zaman değiştirilir ve paylaşım parametreleri korunur");
   check(BrowserSupport.Website("Open https://www.youtube.com/embed/AbCdEfGhI12?autoplay=1 at 17:54")=="https://www.youtube.com/embed/AbCdEfGhI12?autoplay=1&start=1074","Gömülü oynatıcı başlangıç zamanı korunur");
   check(LinkIntent.Apply(UserPrompt,"https://www.youtube.com/watch?v=AbCdEfGhI12&t=2")==Video+"?t=1074","Model aynı videoyu farklı biçimde döndürse de istenen zaman kullanılır");
   check(LinkIntent.Apply(UserPrompt,"https://youtu.be/abcdefghijk")=="https://youtu.be/abcdefghijk","Başka videonun zamanı değiştirilmez");
   check(BrowserSupport.Website("Open https://youtube.com.evil.test/watch?v=AbCdEfGhI12 at 17:54")=="https://youtube.com.evil.test/watch?v=AbCdEfGhI12","Benzer alan adına video zamanı eklenmez");
   check(BrowserSupport.Website("Open "+Video+" when I press F8")==Video,"Zaman içermeyen videonun adresi korunur");
   check(BrowserSupport.Website("Open "+Video+" and wait 17 minutes")==Video,"Bekleme süresi video zamanı sayılmaz");
   check(BrowserSupport.Website("Open "+Video+" at 1:00 or 2:00")==Video,"Belirsiz iki başlangıç zamanı tahmin edilmez");
   check(LinkIntent.Apply(UserPrompt,"https://www.youtube.com/watch?v")=="https://www.youtube.com/watch?v","Eksik video kimliği eski ayarların yüklenmesini bozmaz");
   foreach(string key in new[]{"Ctrl+Subtract","Ctrl + Num -","Ctrl+NumPad5","Ctrl+Num +","Alt+OemMinus"}){TriggerSpec parsed;check(TriggerSpec.TryParse(key,out parsed)&&TriggerSpec.Mentioned("When I press "+key+", open a video",parsed)&&TriggerSpec.FindInPrompt("When I press "+key+", open a video")==parsed.Text,"Numpad/OEM prompt ve alan eşleşmesi: "+key);}
   var state=new State{ActiveProfileId="study",Paused=true};var saved=Saved(System.Reflection.Assembly.GetExecutingAssembly().Location);string reason=Profiles.Describe(saved,state.Profiles,state.ActiveProfileId,state.Paused);check(reason.Contains("Disabled")&&reason.Contains("Study")&&reason.Contains("Work profile")&&reason.Contains("paused"),"Tüm etkinleşmeme nedenleri birlikte gösterilir");
   string root=Path.Combine(Path.GetTempPath(),"Activation-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{
    string brave=Path.Combine(root,"brave.exe");File.WriteAllText(brave,"");state.Bindings.Add(saved);string file=Path.Combine(root,"state.json");Storage.Write(state,file);var restored=Storage.Read(file).Bindings.Single();check(restored.Steps[0].Text==Video+"?t=1074"&&!restored.Enabled&&restored.ProfileId=="work","Eski kayıt zamanı düzelir; etkinlik ve profil korunur");
    var request=new LlmRequest{Prompt=UserPrompt,Trigger="Ctrl+Subtract",TargetApp=brave,Applications=new List<string>{brave}};string id=LlmProtocol.AppMap(request).Single().Key;
    foreach(string kind in new[]{"open_website","open_resource"}){var plan=LlmProtocol.ReadPlan(Reply(id,kind,Video),request);check(plan.Ready&&(plan.Binding.Kind=="Browser website"?plan.Binding.Target:plan.Binding.Steps.Single().Text)==Video+"?t=1074","Eksik model zamanı plana uygulanır: "+kind);}
   }finally{foreach(string file in Directory.GetFiles(root))File.Delete(file);Directory.Delete(root);}
  }
  public static void Ui(MainWindow owner,KeyboardEngine keyboard,Action<bool,string> check,Action<Window,string> screenshot){
   string app=System.Reflection.Assembly.GetExecutingAssembly().Location;var saved=Saved(app);bool registered=Native.RegisterHotKey(IntPtr.Zero,0x6B22,2|0x4000,109);
   try{
    check(Native.Probe(saved)!=null,"Dolu tuş sistem tanılamasında bildirilir");
    var edit=new PromptWindow(keyboard,new[]{saved},new[]{app},previous:saved,profiles:Profiles.Defaults(),activeProfile:"study"){Owner=owner};edit.Show();try{
     check(edit.FeedbackForTest.Contains("Disabled")&&edit.FeedbackForTest.Contains("Outside active profile"),"Prompt devre dışı ve profil durumunu açıklar");edit.EnabledForTest(true);check(edit.PlanForTest().Binding.Enabled,"Devre dışı prompt doğrudan etkinleştirilebilir");
     edit.TriggerForTest("Ctrl+NumPad5");check(edit.CanSaveForTest()&&edit.PlanForTest().Binding.Key==101,"Tuş değişikliği modeli yeniden çalıştırmaz");edit.TriggerForTest("Ctrl+UnknownKey");check(!edit.CanSaveForTest(),"Geçersiz tetikleyici eski planla kaydedilemez");edit.ProfileForTest("");check(!edit.CanSaveForTest(),"Profil değişimi geçersiz tetikleyiciyi kabul etmez");edit.TriggerForTest("Ctrl+Subtract");check(edit.CanSaveForTest(),"Numpad eksi tekrar seçilince plan kaydedilebilir");screenshot(edit,"activation-prompt-preview.png");edit.Hide();
     Application.Current.Dispatcher.BeginInvoke(new Action(()=>edit.SaveForTest()));check(edit.ShowDialog()==true&&edit.Result.Enabled&&edit.Result.Key==109,"Sistem kaydı doluyken kanca ataması kaydedilir");
    }finally{edit.Close();}
    var macro=Saved(app);macro.IsMacro=true;macro.Prompt=null;macro.Steps=new List<ActionStep>{new ActionStep{Kind="Key",Key=65}};var macroEdit=new MacroWindow(keyboard,new[]{macro},Profiles.Defaults(),"study",()=>new SystemShortcut[0],macro){Owner=owner};macroEdit.Show();macroEdit.EnabledForTest(true);check(macroEdit.DraftForTest().Enabled,"Devre dışı makro doğrudan etkinleştirilebilir");macroEdit.Hide();Application.Current.Dispatcher.BeginInvoke(new Action(()=>macroEdit.SaveForTest()));check(macroEdit.ShowDialog()==true&&macroEdit.Result.Enabled,"Makro kaydı bağımsız sistem kayıt testiyle engellenmez");macroEdit.Close();
    string root=Path.Combine(Path.GetTempPath(),"ActivationUi-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var data=new State{ActiveProfileId="study",Paused=true};data.Bindings.Add(saved.Copy());string file=Path.Combine(root,"state.json");var main=new MainWindow(data,file){Owner=owner};main.Show();try{
     main.ActivationForTest(saved.Id);var loaded=Storage.Read(file);check(loaded.Bindings.Single().Enabled&&loaded.ActiveProfileId=="work"&&!loaded.Paused,"Etkinleştirme profile geçer, motoru sürdürür ve kaydeder: "+LocalLlm.Json().Serialize(new{enabled=loaded.Bindings.Single().Enabled,profile=loaded.ActiveProfileId,paused=loaded.Paused,original=data.ActiveProfileId,history=data.History}));main.ActivationForTest(saved.Id);check(!Storage.Read(file).Bindings.Single().Enabled,"Etkin atama aynı düğmeden kapatılabilir");
     var duplicate=data.Bindings.Single().Copy();duplicate.Id="duplicate";duplicate.Enabled=true;data.Bindings.Add(duplicate);main.ActivationForTest(saved.Id);check(!data.Bindings.First().Enabled,"Gerçek çakışma etkinleştirmeyi engellemeye devam eder");
    }finally{main.Close();foreach(string fileName in Directory.GetFiles(root))File.Delete(fileName);Directory.Delete(root);}
    int fires=0;using(var engine=new KeyboardEngine()){saved.Enabled=true;engine.Bindings=()=>new List<Binding>{saved};engine.Fire=(b,w)=>fires++;engine.Handle(new Native.Keyboard{vk=162});engine.Handle(new Native.Keyboard{vk=109});engine.Handle(new Native.Keyboard{vk=109,flags=128});engine.Handle(new Native.Keyboard{vk=162,flags=128});engine.FlushForTest();check(fires==1,"Ctrl+Numpad eksi kanca motorunda bir kez tetiklenir");}
   }finally{if(registered)Native.UnregisterHotKey(IntPtr.Zero,0x6B22);}
  }
  public static int Real(){
   string report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"activation-model-test.json");var results=new List<object>();using(var model=new LocalLlm())try{
    var state=Storage.Read(Storage.FilePath);string brave=state.ApplicationPaths.First(p=>Path.GetFileName(p).Equals("brave.exe",StringComparison.OrdinalIgnoreCase));
    foreach(string prompt in new[]{UserPrompt,"Open "+Video+" in Brave at 17 minutes 54 seconds when I press Ctrl+Subtract."}){
     var request=new LlmRequest{Prompt=prompt,Trigger=prompt==UserPrompt?"Ctrl+Subtract":"",TargetApp=brave,Applications=state.ApplicationPaths,ModelPath=state.ModelPath,Gpu=true};var plan=model.AnalyzeAsync(request,m=>{},CancellationToken.None).GetAwaiter().GetResult();bool correct=plan.Ready&&plan.Binding.Mods==2&&plan.Binding.Key==109&&(plan.Binding.Kind=="Browser website"?Rules.SameApp(plan.Binding.BrowserPath,brave)&&plan.Binding.Target==Video+"?t=1074":plan.Binding.Steps.Any(s=>s.Kind=="Open content"&&Rules.SameApp(s.AppPath,brave)&&s.Text==Video+"?t=1074"));results.Add(new{prompt=prompt,passed=correct,binding=plan.Binding,errors=plan.Errors,response=model.LastResponse});if(!correct)throw new InvalidDataException("Video zamanı veya numpad tetikleyicisi yanlış yorumlandı.");
    }
    File.WriteAllText(report,LocalLlm.Json().Serialize(new{status="PASS",results=results}));return 0;
   }catch(Exception e){File.WriteAllText(report,LocalLlm.Json().Serialize(new{status="FAIL",error=e.ToString(),results=results,response=model.LastResponse}));return 1;}
  }
 }
}
