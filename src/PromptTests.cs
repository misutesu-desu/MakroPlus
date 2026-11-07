using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace AkilliKisayol {
 public static class PromptTests {
  public static void Parser(Action<bool,string> check){
   string folder=Path.Combine(Path.GetTempPath(),"PromptTest-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
   try{
    string[] paths=new[]{"chrome.exe","Code.exe","notepad.exe","Discord.exe"}.Select(filename=>Path.Combine(folder,filename)).ToArray();foreach(string path in paths)File.WriteAllText(path,"");
    var p=PromptParser.Parse("Chrome'da F8'e basınca yeni sekme aç",paths);check(p.Ready&&p.Binding.Key==119&&p.Binding.AppPath==paths[0]&&p.Binding.Steps[0].Key==84,"Türkçe uygulama adı ve F8 eki");
    p=PromptParser.Parse("Not Defteri'nde Ctrl+Alt+K'ye basınca Ctrl+Shift+S gönder",paths);check(p.Ready&&p.Binding.Mods==3&&p.Binding.Key==75&&p.Binding.Steps[0].Mods==6&&p.Binding.Steps[0].Key==83,"Tetikleyici ve gönderilecek tuşlar ayrılır");
    p=PromptParser.Parse("F8'e basınca tümünü seç; sonra kopyala",paths,paths[2]);check(p.Ready&&p.Binding.Steps.Count==2&&p.Binding.Steps[0].Key==65&&p.Binding.Steps[1].Key==67,"Çok adımlı düzenleme");
    p=PromptParser.Parse("F9'a basınca \"Merhaba; sonra dünya\" yaz; ardından Enter gönder",paths,paths[2]);check(p.Ready&&p.Binding.Steps.Count==2&&p.Binding.Steps[0].Text=="Merhaba; sonra dünya"&&p.Binding.Steps[1].Key==13,"Tırnak içindeki metin bölünmez");
    p=PromptParser.Parse("F8'e basınca 0,5 saniye bekle; sonra \"Dışa aktar\" düğmesine tıkla",paths,paths[2]);check(p.Ready&&p.Binding.Steps[0].Delay==500&&p.Binding.Steps[1].Kind=="Click button"&&p.Binding.Steps[1].Text=="Dışa aktar","Bekleme ve erişilebilir düğme adımı");
    p=PromptParser.Parse("VS Code'da F8'e basınca komut paletini aç",paths);check(p.Ready&&p.Binding.AppPath==paths[1]&&p.Binding.Steps[0].Mods==6&&p.Binding.Steps[0].Key==80,"VS Code varsayılan eylemi");
    p=PromptParser.Parse("Discord'da F9'a basınca mikrofonu kapat",paths);check(p.Ready&&p.Notes.Any(n=>n.Contains("toggles")),"Discord geçiş davranışı açıklanır");
    p=PromptParser.Parse("kaydet",paths,paths[2],"Ctrl+Alt+K");check(p.Ready&&p.Binding.Key==75,"Ayrı tuş alanıyla kısa istek");
    p=PromptParser.Parse("Chrome'da Ctrl Alt K ile yeni sekme aç",paths);check(p.Ready&&p.Binding.Mods==3&&p.Binding.Key==75,"Boşlukla yazılan tuşlar ve ile ifadesi");
    p=PromptParser.Parse("F8'e basınca önce tümünü seç, sonra kopyala",paths,paths[2]);check(p.Ready&&p.Binding.Steps.Count==2,"Doğal sıra ifadeleri ve virgül");
    p=PromptParser.Parse("F8'e basınca kopyala",paths,paths[2],"F10");check(p.Ready&&p.Binding.Key==121,"Tuş alanı açıkça önceliklidir");
    check(!PromptParser.Parse("F8'e basınca kaydetme",paths,paths[2]).Ready,"Olumsuz istek eyleme dönüştürülmez");
    check(!PromptParser.Parse("F8'e basınca kopyala; sonra videoyu güzelleştir",paths,paths[2]).Ready,"Bilinmeyen adım kısmen kaydedilmez");
    check(!PromptParser.Parse("Photoshop'ta F8'e basınca kopyala",paths,paths[2]).Ready,"Bilinmeyen uygulama başka kapsama düşmez");
    check(!PromptParser.Parse("Photoshop'tayken F8'e basınca kopyala",paths,paths[2]).Ready,"Bilinmeyen uygulamanın zaman eki algılanır");
    check(!PromptParser.Parse("Chrome'da her yerde F8'e basınca kopyala",paths).Ready,"Çelişkili kapsam reddedilir");
    check(!PromptParser.Parse("F12'ye basınca kopyala",paths,paths[2]).Ready,"Ayrılmış tetikleyici reddedilir");
    check(!PromptParser.Parse("F8'e basınca 11 saniye bekle",paths,paths[2]).Ready,"Uzun bekleme sınırı");
    check(!PromptParser.Parse("kopyala",paths,paths[2]).Ready,"Eksik tetikleyici açıklanır");
    check(!PromptParser.Parse("F8'e basınca Ctrl+Alt+Delete gönder",paths,paths[2]).Ready,"Güvenli olmayan gönderim reddedilir");
    string duplicate=Path.Combine(folder,"other","chrome.exe");Directory.CreateDirectory(Path.GetDirectoryName(duplicate));File.WriteAllText(duplicate,"");
    check(!PromptParser.Parse("Chrome'da F8'e basınca yeni sekme aç",paths.Concat(new[]{duplicate})).Ready,"Aynı adlı uygulamalar için seçim istenir");
    check(PromptParser.Parse("Chrome'da F8'e basınca yeni sekme aç",paths.Concat(new[]{duplicate}),duplicate).Ready,"Seçilen dosya belirsizliği çözer");
    var workflow=PromptParser.Parse("F8'e basınca \"Türkçe ığüşöç\" yaz; sonra Enter gönder",paths,paths[2]).Binding;
    var state=new State();state.Bindings.Add(workflow);string settings=Path.Combine(folder,"settings.json");Storage.Write(state,settings);var restored=Storage.Read(settings).Bindings[0];check(restored.Prompt==workflow.Prompt&&restored.Steps.Count==2&&restored.Steps[0].Text=="Türkçe ığüşöç","Prompt ve adımlar kalıcı kaydedilir");
    var copy=workflow.Copy();copy.Steps[0].Text="değişti";check(workflow.Steps[0].Text!="değişti","Düzenleme özgün adımları değiştirmez");
    workflow.Steps[0].Kind="Kabuk";check(Rules.Validate(workflow,new Binding[0])!=null,"İşlem dizisinde bilinmeyen çalıştırıcı reddedilir");
   }finally{foreach(string file in Directory.GetFiles(folder,"*",SearchOption.AllDirectories))File.Delete(file);foreach(string dir in Directory.GetDirectories(folder))Directory.Delete(dir);Directory.Delete(folder);}
  }
  static void Pump(int milliseconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
  public static void NativeWorkflow(Window owner,Action<bool,string> check,Action<string> skip){
   var host=new Window{Title="Akıllı Kısayol — otomasyon testi",Width=420,Height=260,Owner=owner};var stack=new StackPanel();var field=new TextBox();int invoked=0;var button=new Button{Content="Test düğmesi"};button.Click+=(s,e)=>invoked++;stack.Children.Add(field);stack.Children.Add(button);host.Content=stack;
   try{
    host.Show();host.Activate();field.Focus();Keyboard.Focus(field);Pump(200);var handle=new WindowInteropHelper(host).Handle;if(Native.GetForegroundWindow()!=handle){skip("Yerel giriş testi: Windows test penceresine ön plan izni vermedi. Başka pencereye giriş gönderilmedi.");return;}
    var workflow=new Binding{Kind="Workflow",Steps=new List<ActionStep>{new ActionStep{Kind="Text",Text="Türkçe ığüşöç"},new ActionStep{Kind="Wait",Delay=100},new ActionStep{Kind="Click button",Text="Test düğmesi"}}};
    var task=Actions.RunSteps(workflow,handle);DateTime deadline=DateTime.UtcNow.AddSeconds(8);while(!task.IsCompleted&&DateTime.UtcNow<deadline)Pump(50);check(task.IsCompleted,"Yerel işlem dizisi zamanında tamamlanır");task.GetAwaiter().GetResult();
    check(field.Text=="Türkçe ığüşöç","Gerçek SendInput ile Unicode metin gönderimi");check(invoked==1,"Gerçek UI Automation ile adı verilen düğmeye basma");
    bool stopped=false;try{Actions.EnsureTarget(IntPtr.Zero);}catch(InvalidOperationException){stopped=true;}check(stopped,"Pencere değişimi adımları durdurur");
    var cancel=new System.Threading.CancellationTokenSource();cancel.Cancel();task=Actions.RunSteps(workflow,handle,cancel.Token);check(task.IsCanceled,"İptal edilen dizi hiçbir adım göndermez");cancel.Dispose();
   }finally{host.Close();}
  }
 }
}
