using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
namespace AkilliKisayol {
 public static class Tests {
  static int count;
  static readonly List<string> skips=new List<string>();
  static void Check(bool condition,string name){if(!condition)throw new Exception(name);count++;}
  static void Screenshot(Window window,string name){window.UpdateLayout();Application.Current.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(()=>{}));var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using(var output=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name)))encoder.Save(output);}
  public static int Run(){
   string report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results.txt");
   try{
    var global=new Binding{Name="Global",Key=83,Mods=3,Kind="Web address",Target="https://example.com"};
    var app=new Binding{Name="Editör",Key=83,Mods=3,AppPath=@"C:\Apps\editor.exe"};
    var other=new Binding{Name="Diğer",Key=83,Mods=3,AppPath=@"C:\Apps\other.exe"};
    var items=new List<Binding>{global,app,other};
    Check(Rules.Resolve(items,3,83,@"C:\Apps\EDITOR.exe")==app,"Uygulama önceliği ve yol eşitliği");
    Check(Rules.Resolve(items,3,83,@"C:\Apps\unknown.exe")==global,"Genel geri dönüş");
    Check(Rules.Resolve(items,3,83,other.AppPath)==other,"Ayrı uygulama kapsamı");
    Check(Rules.Resolve(items,2,83,app.AppPath)==null,"Değiştirici tam eşleşmesi");
    app.Enabled=false;Check(Rules.Resolve(items,3,83,app.AppPath)==global,"Pasif atama çalışmaz");app.Enabled=true;
    Check(!Rules.Conflicts(app,new[]{other}).Any(),"Ayrı uygulamalar çakışmaz");
    Check(Rules.Conflicts(app,new[]{global}).Single().StartsWith("PRIORITY"),"Genel ve uygulama öncelik uyarısı");
    var duplicate=app.Copy();duplicate.Id=Guid.NewGuid().ToString();Check(Rules.Conflicts(app,new[]{duplicate}).Single().StartsWith("CONFLICT"),"Aynı uygulamada kesin çakışma");
    duplicate.Enabled=false;Check(!Rules.Conflicts(app,new[]{duplicate}).Any(),"Pasif kayıt çakışmaz");
    Check(Rules.Reserved(8,65)!=null&&Rules.Reserved(0,123)!=null&&Rules.Reserved(3,46)!=null,"Ayrılmış tuşlar");
    Check(Rules.Validate(global,new Binding[0])==null,"Geçerli web eylemi");
    var bad=global.Copy();bad.Target="javascript:alert(1)";Check(Rules.Validate(bad,new Binding[0])!=null,"Web şema doğrulama");
    Check(Rules.NormalizeSpotify("https://open.spotify.com/intl-tr/track/123abc?si=test")=="spotify:track:123abc","Spotify yerelleştirilmiş bağlantı");
    Check(Rules.NormalizeSpotify("spotify:track:ABC123")=="spotify:track:ABC123","Spotify URI");
    PromptTests.Parser(Check);
    LlmTests.Unit(Check);AppIntegrationTests.Unit(Check);
    BrowserTests.Unit(Check);SystemShortcutTests.Unit(Check);FeatureTests.Unit(Check);ActivationTests.Unit(Check);ProductTests.Unit(Check);TouchpadTests.Unit(Check);ModernTests.Unit(Check);ModelCompatibilityTests.Unit(Check);ModelFolderTests.Unit(Check);ThemeTests.Unit(Check);WindowFrameTests.Unit(Check);CustomThemeTests.Unit(Check);GesturePromptTests.Unit(Check);
    bool invalid=false;try{Rules.NormalizeSpotify("https://evil.test/track/id");}catch(ArgumentException){invalid=true;}Check(invalid,"Spotify alan adı doğrulama");
    string folder=Path.Combine(Path.GetTempPath(),"AkilliKisayolTest-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string file=Path.Combine(folder,"state.json");
    var state=new State{Dark=false};state.Bindings.Add(global);Storage.Write(state,file);var restored=Storage.Read(file);
    Check(restored.Bindings[0].Name=="Global"&&!restored.Dark,"JSON kaydet/yükle");state.Bindings[0].Name="Türkçe ığüşöç";Storage.Write(state,file);Check(Storage.Read(file).Bindings[0].Name==state.Bindings[0].Name&&File.Exists(file+".bak"),"Atomik güncelleme ve Türkçe");
    File.WriteAllText(file,"bozuk");bool corrupt=false;try{Storage.Read(file);}catch{corrupt=true;}Check(corrupt,"Bozuk dosya sessizce ezilmez");
    string appExecutable=System.Reflection.Assembly.GetExecutingAssembly().Location;
    var library=new State();Check(AppLibrary.Remember(library.ApplicationPaths,appExecutable),"Uygulama panele eklenir");Check(!AppLibrary.Remember(library.ApplicationPaths,appExecutable.ToUpperInvariant()),"Aynı uygulama iki kez eklenmez");Check(!AppLibrary.Remember(library.ApplicationPaths,"relative.exe"),"Panel tam dosya yolu kullanır");
    Storage.Write(library,file);Check(Storage.Read(file).ApplicationPaths.Single()==appExecutable,"Uygulama paneli kalıcı kaydedilir");
    File.WriteAllText(file,"{\"Bindings\":[{\"Id\":\"old\",\"Name\":\"Eski\",\"AppPath\":\"C:\\\\Apps\\\\editor.exe\",\"Target\":\"\",\"Kind\":\"Tuş gönder\"}],\"History\":[],\"Dark\":true}");
    Check(Storage.Read(file).ApplicationPaths.Single()==@"C:\Apps\editor.exe","Eski ayarlardaki uygulama kapsamları panele taşınır");
    library.ApplicationPaths.Clear();Storage.Write(library,file);Check(Storage.Read(file).ApplicationPaths.Count==0,"Panelden kaldırılan uygulama geri eklenmez");
    var scopedDraft=AppLibrary.Draft(appExecutable,false);Check(scopedDraft.AppPath==appExecutable&&scopedDraft.Kind=="Send key","Panel uygulama kapsamını doldurur");
    var launchDraft=AppLibrary.Draft(appExecutable,true);Check(launchDraft.AppPath==""&&launchDraft.Kind=="Application"&&launchDraft.Target==appExecutable,"Panel genel başlatma eylemini doldurur");
    string marker=Path.Combine(folder,"Türkçe test ' dosyası.txt");
    string command="[System.IO.File]::WriteAllText('"+marker.Replace("'","''")+"','doğrulandı')";
    string execution=Actions.Execute(new Binding{Kind="PowerShell",Target=command});
    Check(File.ReadAllText(marker)=="doğrulandı"&&execution.EndsWith("0"),"Gerçek PowerShell eylemi: Türkçe ve tek tırnaklı yol");
    string cmdMarker=Path.Combine(folder,"cmd test.txt");execution=Actions.Execute(new Binding{Kind="Terminal (cmd)",Target="echo passed>\""+cmdMarker+"\""});
    Check(File.ReadAllText(cmdMarker).Trim()=="passed"&&execution.EndsWith("0"),"Gerçek cmd eylemi: boşluklu yol");
    foreach(string f in Directory.GetFiles(folder))File.Delete(f);Directory.Delete(folder);
    var application=new Application();System.Threading.SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(application.Dispatcher));Theme.Install(application);Theme.Apply(true);
    var previewState=new State();previewState.ApplicationPaths.Add(appExecutable);previewState.ApplicationPaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"));
    string uiSettings=Path.Combine(Path.GetTempPath(),"ShortcutUi-"+Guid.NewGuid().ToString("N"),"settings.json");var window=new MainWindow(previewState,uiSettings);window.Show();application.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(()=>{}));
    Check(window.IsVisible,"WPF pencere ve yerel hook başlatma");
    window.SmokeViews();Check(true,"Tüm gezinme bölümleri");
    BrowserTests.Engine(Check);
    using(var keyboard=new KeyboardEngine()){
     int fires=0;keyboard.Bindings=()=>new List<Binding>{global};keyboard.Fire=(b,w)=>fires++;
     keyboard.Handle(new Native.Keyboard{vk=162});keyboard.Handle(new Native.Keyboard{vk=164});
     Check(keyboard.Handle(new Native.Keyboard{vk=83}),"Eşleşen ana tuş engellenir");
     Check(keyboard.Handle(new Native.Keyboard{vk=83}),"Basılı tutulan ana tuş engellenir");
     keyboard.FlushForTest();Check(fires==0,"Tuş bırakılmadan eylem çalışmaz");
     keyboard.Handle(new Native.Keyboard{vk=83,flags=128});keyboard.Handle(new Native.Keyboard{vk=162,flags=128});keyboard.Handle(new Native.Keyboard{vk=164,flags=128});keyboard.FlushForTest();Check(fires==1,"Tek tetikleme, tekrar yok");
     keyboard.Handle(new Native.Keyboard{vk=162,flags=16});keyboard.Handle(new Native.Keyboard{vk=164,flags=16});keyboard.Handle(new Native.Keyboard{vk=83,flags=16});keyboard.FlushForTest();Check(fires==1,"Sentetik olaylar tetiklemez");
     keyboard.Handle(new Native.Keyboard{vk=162});keyboard.Handle(new Native.Keyboard{vk=165});keyboard.Handle(new Native.Keyboard{vk=83});keyboard.Handle(new Native.Keyboard{vk=83,flags=128});keyboard.Handle(new Native.Keyboard{vk=162,flags=128});keyboard.Handle(new Native.Keyboard{vk=165,flags=128});keyboard.FlushForTest();Check(fires==1,"AltGr karakter girişi korunur");
     global.PassThrough=true;keyboard.Handle(new Native.Keyboard{vk=162});keyboard.Handle(new Native.Keyboard{vk=164});Check(!keyboard.Handle(new Native.Keyboard{vk=83}),"İletim açıkken ana tuş geçer");keyboard.Handle(new Native.Keyboard{vk=83,flags=128});keyboard.Handle(new Native.Keyboard{vk=162,flags=128});keyboard.Handle(new Native.Keyboard{vk=164,flags=128});keyboard.FlushForTest();Check(fires==2,"İletim açıkken de tek eylem");
     keyboard.Paused=true;keyboard.Handle(new Native.Keyboard{vk=162});keyboard.Handle(new Native.Keyboard{vk=164});keyboard.Handle(new Native.Keyboard{vk=83});keyboard.Handle(new Native.Keyboard{vk=83,flags=128});keyboard.Handle(new Native.Keyboard{vk=162,flags=128});keyboard.Handle(new Native.Keyboard{vk=164,flags=128});keyboard.FlushForTest();Check(fires==2,"Duraklatma eylemi durdurur");
     var editor=new Editor(keyboard,null,new Binding[0],previewState.ApplicationPaths,scopedDraft){Owner=window};editor.Show();Check(editor.CurrentDraft().AppPath==appExecutable&&editor.CurrentDraft().Kind=="Send key","Panelden açılan düzenleyicide kapsam hazır");editor.SmokeFields();Screenshot(editor,"editor-preview.png");Check(true,"Tüm eylem girişleri ve uygulama kapsamı");editor.Close();
     var launchEditor=new Editor(keyboard,null,new Binding[0],previewState.ApplicationPaths,launchDraft){Owner=window};launchEditor.Show();Check(launchEditor.CurrentDraft().Target==appExecutable&&launchEditor.CurrentDraft().AppPath=="","Panelden açılan başlatma düzenleyicisi hazır");launchEditor.Close();
     var picker=new AppPicker(previewState.ApplicationPaths){Owner=window};picker.Show();Screenshot(picker,"app-picker-preview.png");Check(picker.IsVisible,"Hızlı uygulama ekleme seçicisi açılır");picker.Close();
     var promptWindow=new PromptWindow(keyboard,new Binding[0],previewState.ApplicationPaths,appExecutable){Owner=window};promptWindow.Show();promptWindow.PreviewForTest("F8'e basınca tümünü seç; sonra kopyala");Check(promptWindow.PlanForTest().Ready,"Prompt ekranı gerçek bir plan hazırlar");Screenshot(promptWindow,"prompt-preview.png");promptWindow.PreviewForTest("F8'e basınca bilinmeyen bir eylem yap");Check(!promptWindow.PlanForTest().Ready,"Prompt değişince geçersiz plan kaydedilmez");promptWindow.Close();
     LlmTests.Ui(window,keyboard,previewState.ApplicationPaths,Check);
     BrowserTests.Ui(window,keyboard,Check,Screenshot);AppIntegrationTests.Ui(window,keyboard,Check,Screenshot);SystemShortcutTests.Ui(window,keyboard,Check,Screenshot);FeatureTests.Ui(window,keyboard,Check,Screenshot,message=>skips.Add(message));ActivationTests.Ui(window,keyboard,Check,Screenshot);ProductTests.Ui(window,keyboard,Check,Screenshot,message=>skips.Add(message));TouchpadTests.Ui(window,keyboard,Check,Screenshot);
    }
    GesturePromptTests.Ui(window,Check,Screenshot);ModernTests.Ui(window,Check,Screenshot);ModelFolderTests.Ui(window,Check,Screenshot);ModelCompatibilityTests.Ui(Check);ThemeTests.Ui(window,Check,Screenshot);window.ShowApplicationsForTest();Screenshot(window,"applications-preview.png");window.SmokeViews();Theme.Apply(false);Screenshot(window,"light-preview.png");Theme.Apply(true);Check(true,"Açık/koyu tema geçişi");Screenshot(window,"preview.png");
    PromptTests.NativeWorkflow(window,Check,message=>skips.Add(message));
    window.Close();if(Directory.Exists(Path.GetDirectoryName(uiSettings))){foreach(string saved in Directory.GetFiles(Path.GetDirectoryName(uiSettings)))File.Delete(saved);Directory.Delete(Path.GetDirectoryName(uiSettings));}
    File.WriteAllText(report,"PASS: "+count+" checks\n"+DateTimeOffset.Now+(skips.Count>0?"\nSKIP: "+string.Join("\nSKIP: ",skips):""));return 0;
   }catch(Exception e){File.WriteAllText(report,"FAIL after "+count+" checks\n"+e);return 1;}
  }
 }
}
