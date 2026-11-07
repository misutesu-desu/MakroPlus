using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace AkilliKisayol {
 public static class AppIntegrationTests {
  public const string Playlist="https://open.spotify.com/playlist/0123456789ABCDEFGHIJKL?si=example-share-token";
  static string Reply(string id,string kind,string text){return LocalLlm.Json().Serialize(new{status="ready",name="App task",trigger="F9",app_id=id,message="",notes=new string[0],steps=new[]{new{kind=kind,combo="",text=text,ms=0}}});}
  public static void Unit(Action<bool,string> check){
   string root=Path.Combine(Path.GetTempPath(),"AppRouting-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
   string spotify=Path.Combine(root,"Spotify.exe"),custom=Path.Combine(root,"CustomEditor.exe");File.WriteAllText(spotify,"");File.WriteAllText(custom,"");
   try{
    var request=new LlmRequest{Prompt="open this playlist on spotify app not browser "+Playlist,Trigger="F9",SelectedApp="",Applications=new List<string>{custom,spotify}};
    string id=LlmProtocol.AppMap(request).Single(p=>p.Value==spotify).Key;
    check(AppIntegration.Target(request)==spotify,"Tarayıcı olmayan kayıtlı uygulama prompttan belirlenir");
    var payload=(Dictionary<string,object>)LocalLlm.Json().DeserializeObject(LocalLlm.Json().Serialize(LlmProtocol.CreatePayload(request)));var messages=(object[])payload["messages"];var context=(Dictionary<string,object>)LocalLlm.Json().DeserializeObject((string)((Dictionary<string,object>)messages.Last())["content"]);
    check((string)context["target_app_id"]==id&&(string)context["resource_hint"]=="spotify:playlist:0123456789ABCDEFGHIJKL","Uygulama kimliği ve yerel bağlantı modele aktarılır");
    var plan=LlmProtocol.ReadPlan(Reply(id,"open_resource",Playlist),request);
    check(plan.Ready&&plan.Binding.AppPath==""&&plan.Binding.Steps.Single().AppPath==spotify&&plan.Binding.Steps[0].Text=="spotify:playlist:0123456789ABCDEFGHIJKL","Playlist Spotify uygulamasına genel kapsamda yönlendirilir");
    var info=AppIntegration.StartInfo(custom,"https://example.com/a?q=hello&x=2");check(info.FileName==custom&&info.Arguments=="\"https://example.com/a?q=hello&x=2\""&&!info.UseShellExecute,"Her exe bağlantıyı kabuktan geçirmeden argüman olarak alabilir");
    request.TargetApp=custom;request.Prompt="Open CustomEditor when I press F9";id=LlmProtocol.AppMap(request).Single(p=>p.Value==custom).Key;
    check(LlmProtocol.ReadPlan(Reply(id,"open_app",""),request).Ready,"Yeni uygulamayı açmak için kodda izin listesi gerekmez");
    check(LlmProtocol.ReadPlan(Reply(id,"command_palette",""),request).Ready,"Uygulama içi sembolik eylemler exe adına göre engellenmez");
    check(LlmProtocol.ReadPlan(Reply(id,"powershell","Get-Date"),request).Binding.Steps.Single().Text=="Get-Date","Üretilen komut metni önizleme için korunur");
    var state=new State();state.Bindings.Add(plan.Binding);Storage.Write(state,Path.Combine(root,"state.json"));var restored=Storage.Read(Path.Combine(root,"state.json")).Bindings.Single().Steps.Single();check(restored.AppPath==spotify&&restored.Text.StartsWith("spotify:playlist:"),"Genel uygulama adımları kalıcı saklanır");
    check(AppIntegration.Quote("C:\\folder\\")=="\"C:\\folder\\\\\""&&AppIntegration.Quote("a\"b")=="\"a\\\"b\"","Windows argümanlarında tırnak ve son ters eğik çizgi korunur");
    // Synthetic package fixture checks the common Store discovery path, including non-Spotify applications.
    string package=Path.Combine(root,"Example.Package_1.0.0.0_x64__publisherhash");Directory.CreateDirectory(package);string packagedExe=Path.Combine(package,"Example.exe");File.WriteAllText(packagedExe,"");
    File.WriteAllText(Path.Combine(package,"AppxManifest.xml"),"<Package><Identity Name='Example.Package'/><Applications><Application Id='Desktop' Executable='Example.exe'><Extensions><Extension><Protocol Name='example'/></Extension></Extensions></Application></Applications></Package>");
    var descriptor=AppIntegration.Describe(packagedExe);check(descriptor.PackageId=="Example.Package_publisherhash!Desktop"&&descriptor.Protocols.Contains("example"),"Store uygulaması kimliği ve protokolleri manifestten otomatik keşfedilir");
    string bridgedExe=Path.Combine(package,"Bridged.exe");File.WriteAllText(bridgedExe,"");File.WriteAllText(Path.Combine(package,"AppxManifest.xml"),"<Package><Identity Name='Example.Package'/><Applications><Application Id='Desktop' Executable='Migrator.exe'><Extensions><Protocol Name='example'/><ExeServer Executable='Bridged.exe'/></Extensions></Application></Applications></Package>");
    check(AppIntegration.Describe(bridgedExe).PackageId=="Example.Package_publisherhash!Desktop","Desktop Bridge başlatıcısı gerçek exe farklı olsa da keşfedilir");File.Delete(bridgedExe);
    string marker=Path.Combine(root,"workflow.txt");var workflow=new Binding{Steps=new List<ActionStep>{new ActionStep{Kind="PowerShell",Text="[IO.File]::WriteAllText('"+marker.Replace("'","''")+"','workflow passed')"}}};
    Actions.RunSteps(workflow,IntPtr.Zero).GetAwaiter().GetResult();check(File.ReadAllText(marker)=="workflow passed","Komut adımı ön plan penceresine gerek duymadan iş akışından çalışır");
    bool cancelled=false;using(var cancel=new CancellationTokenSource()){cancel.CancelAfter(250);try{Actions.Execute(new Binding{Kind="PowerShell",Target="Start-Sleep -Seconds 30"},cancel.Token);}catch(OperationCanceledException){cancelled=true;}}check(cancelled,"Escape/iptal çalışan komut sürecini durdurabilir");
    File.Delete(packagedExe);File.Delete(Path.Combine(package,"AppxManifest.xml"));Directory.Delete(package);
   }finally{foreach(string file in Directory.GetFiles(root))File.Delete(file);Directory.Delete(root);}
  }
  public static void Ui(System.Windows.Window owner,KeyboardEngine keyboard,Action<bool,string> check,Action<System.Windows.Window,string> screenshot){
   string app=System.Reflection.Assembly.GetExecutingAssembly().Location;var window=new PromptWindow(keyboard,new Binding[0],new[]{app}){Owner=owner};window.Show();try{
    check(window.SelectTargetForTest(app),"Kayıtlı herhangi bir exe hedef olarak seçilebilir");
    window.Planner=(request,progress,cancellation)=>{string id=LlmProtocol.AppMap(request).Single(p=>p.Value==app).Key;return System.Threading.Tasks.Task.FromResult(LlmProtocol.ReadPlan(Reply(id,"open_resource","example:content/123"),request));};
    window.StartModelForTest("Open example:content/123 in the selected app when I press F9.").GetAwaiter().GetResult();
    check(window.CanSaveForTest()&&window.PlanForTest().Binding.Steps.Single().AppPath==app,"Genel uygulama açma planı arayüzde kayda hazır görünür");screenshot(window,"app-prompt-preview.png");
    var saved=window.PlanForTest().Binding;window.Hide();var edit=new PromptWindow(keyboard,new Binding[0],new string[0],previous:saved){Owner=owner};edit.Show();try{check(edit.TargetForTest()==app&&edit.CanSaveForTest(),"Kaydedilmiş uygulama planının hedefi düzenlemede korunur");}finally{edit.Close();}
   }finally{window.Close();}
  }
  public static int Real(){
   string report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"app-model-test.json");var results=new List<object>();using(var model=new LocalLlm())try{
    var state=Storage.Read(Storage.FilePath);string spotify=state.ApplicationPaths.First(p=>Path.GetFileName(p).Equals("Spotify.exe",StringComparison.OrdinalIgnoreCase));string discord=state.ApplicationPaths.First(p=>Path.GetFileName(p).Equals("Discord.exe",StringComparison.OrdinalIgnoreCase));
    var descriptor=AppIntegration.Describe(spotify);if(descriptor.PackageId.Length==0||!descriptor.Protocols.Contains("spotify"))throw new InvalidDataException("Store Spotify kimliği/protokolü keşfedilemedi.");
    var cases=new[]{new{prompt="open this playlist on spotify app not browser "+Playlist,trigger="F9",target="",path=spotify,kind="Open content"},new{prompt="Open Discord when I press F10.",trigger="",target="",path=discord,kind="Open application"},new{prompt="open this playlist on spotify app not browser "+Playlist,trigger="",target=spotify,path=spotify,kind="missing-trigger"}};
    foreach(var test in cases){var request=new LlmRequest{Prompt=test.prompt,Trigger=test.trigger,TargetApp=test.target,SelectedApp="",Applications=state.ApplicationPaths,ModelPath=state.ModelPath};var plan=model.AnalyzeAsync(request,m=>{},CancellationToken.None).GetAwaiter().GetResult();
     bool correct=test.kind=="missing-trigger"?!plan.Ready&&plan.Errors.Any(e=>e.IndexOf("trigger",StringComparison.OrdinalIgnoreCase)>=0||e.IndexOf("key",StringComparison.OrdinalIgnoreCase)>=0):plan.Ready&&plan.Binding.AppPath==""&&plan.Binding.Steps!=null&&plan.Binding.Steps.Any(s=>s.Kind==test.kind&&Rules.SameApp(s.AppPath,test.path))&&(test.kind!="Open content"||plan.Binding.Steps.Any(s=>s.Text=="spotify:playlist:0123456789ABCDEFGHIJKL"));
     results.Add(new{prompt=test.prompt,passed=correct,binding=plan.Binding,errors=plan.Errors,response=model.LastResponse});if(!correct)throw new InvalidDataException("Model senaryosu beklentiyi karşılamadı: "+test.kind);
    }File.WriteAllText(report,LocalLlm.Json().Serialize(new{status="PASS",package_id=descriptor.PackageId,protocols=descriptor.Protocols,results=results}));return 0;
   }catch(Exception e){File.WriteAllText(report,LocalLlm.Json().Serialize(new{status="FAIL",error=e.ToString(),response=model.LastResponse,results=results}));return 1;}
  }
 }
}
