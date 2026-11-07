using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace AkilliKisayol {
 public static class SystemShortcutTests {
  static void CreateLink(string path,string hotkey){
   Type type=Type.GetTypeFromProgID("WScript.Shell");object shell=Activator.CreateInstance(type),link=null;try{link=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});
    foreach(var pair in new[]{new[]{"TargetPath",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"notepad.exe")},new[]{"Arguments","\"Türkçe ığüş.txt\" /test"},new[]{"Description","Test açıklaması"},new[]{"WorkingDirectory",Path.GetTempPath()},new[]{"Hotkey",hotkey}})link.GetType().InvokeMember(pair[0],BindingFlags.SetProperty,null,link,new object[]{pair[1]});link.GetType().InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);
   }finally{if(link!=null)Marshal.FinalReleaseComObject(link);Marshal.FinalReleaseComObject(shell);}
  }
  public static void Unit(Action<bool,string> check){
   string root=Path.Combine(Path.GetTempPath(),"SystemShortcutTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
   try{
    string path=Path.Combine(root,"Türkçe kısayol.lnk");CreateLink(path,"CTRL+ALT+9");var link=SystemShortcuts.ReadLink(path);
    check(link.Key==57&&link.Mods==3&&link.Enabled&&link.Editable,"Windows .lnk tuş bayrakları yerel metin etiketinden bağımsız okunur");
    check(link.Arguments=="\"Türkçe ığüş.txt\" /test"&&Path.GetFileName(link.Target).Equals("notepad.exe",StringComparison.OrdinalIgnoreCase),"Gerçek kabuk kısayolunun hedefi ve parametreleri okunur");
    byte[] before=link.Snapshot;SystemShortcuts.WriteLink(link,7,78);var changed=SystemShortcuts.ReadLink(path);
    check(changed.Key==78&&changed.Mods==7,"Kaynak dosyadaki gerçek Ctrl+Alt+Shift ataması değiştirilir");
    check(before.Where((b,i)=>i!=64&&i!=65).SequenceEqual(changed.Snapshot.Where((b,i)=>i!=64&&i!=65)),"Tuş alanı dışındaki hedef, parametre ve kabuk verileri aynen korunur");
    check(File.ReadAllBytes(SystemShortcuts.Backup(path)).SequenceEqual(before),"Kaynak değişmeden önce birebir yedeği saklanır");
    bool stale=false;try{SystemShortcuts.WriteLink(link,3,75);}catch(IOException){stale=true;}check(stale&&File.ReadAllBytes(path).SequenceEqual(changed.Snapshot),"Başka düzenleme sonrası eski ekran kaynağın üzerine yazamaz");
    SystemShortcuts.Restore(changed);var restored=SystemShortcuts.ReadLink(path);check(restored.Snapshot.SequenceEqual(before),"Kaynak dosya yedeğinden tam geri alınır");
    SystemShortcuts.WriteLink(restored,0,0);check(!SystemShortcuts.ReadLink(path).Enabled&&File.Exists(path),"Tuş kaldırılırken .lnk dosyası silinmez");
    check(SystemShortcuts.ValidateLink(8,65)!=null&&SystemShortcuts.ValidateLink(0,120)==null&&SystemShortcuts.ValidateLink(0,65)!=null,"Kabuk tuşunun desteklenen biçimi denetlenir");
    string code=Path.Combine(root,"keybindings.json");string original="// Keep this header\r\n[\r\n // first rule\r\n {\"key\":\"ctrl+alt+k\",\"command\":\"first\",\"args\":{\"literal\":\"https://x.test/,}/*not comment*/\"},},\r\n /* keep other rule */ {\"key\":\"ctrl+k ctrl+c\",\"command\":\"comment\",\"when\":\"editorTextFocus\"},\r\n]\r\n";
    File.WriteAllText(code,original,new UTF8Encoding(true));var rules=SystemShortcuts.ReadCode(code);check(rules.Count==2&&rules[1].When=="editorTextFocus","JSONC yorumları, son virgüller ve ardışık tuşlar okunur");
    SystemShortcuts.WriteCode(rules[0],"ctrl+alt+l","updated","editorTextFocus");string updated=File.ReadAllText(code);var newRules=SystemShortcuts.ReadCode(code);
    check(newRules[0].KeyText=="ctrl+alt+l"&&newRules[0].Command=="updated"&&newRules[0].When=="editorTextFocus","VS Code tuş, komut ve koşulu kaynakta değiştirilir");
    check(updated.Contains("// Keep this header")&&updated.Contains("/* keep other rule */")&&updated.Contains("https://x.test/,}/*not comment*/"),"Diğer kurallar, yorumlar ve args içeriği korunur");
    check(File.ReadAllBytes(code).Take(3).SequenceEqual(new byte[]{239,187,191}),"UTF-8 BOM korunur");
    int mods,key;check(ShortcutKeys.TryCode("ctrl+shift+oem_plus",out mods,out key)&&mods==6&&key==187&&ShortcutKeys.Code(mods,key)=="ctrl+shift+oem_plus","Uygulama OEM tuş adları doğru sanal tuşla eşleşir");check(ShortcutKeys.TryCode("alt+numpad_add",out mods,out key)&&mods==1&&key==107,"Numpad uygulama tuşları çözümlenir");TriggerSpec captured;check(TriggerSpec.TryParse("Ctrl+Alt+NumPad5",out captured)&&captured.Key==101,"Kabuk düzenleyicisinde kaydedilen Numpad tuşu yeniden okunabilir");
    stale=false;try{SystemShortcuts.WriteCode(rules[0],"f9","stale","");}catch(IOException){stale=true;}check(stale,"Eski JSONC kaydı yeni dosyanın üzerine yazamaz");
    SystemShortcuts.Restore(newRules[0]);check(File.ReadAllText(code)==original,"Uygulama kısayol dosyası yedeğinden geri yüklenir");
    string unicode=Path.Combine(root,"unicode");Directory.CreateDirectory(unicode);string unicodeFile=Path.Combine(unicode,"keybindings.json");File.WriteAllText(unicodeFile,"[{\"key\":\"f8\",\"command\":\"Türkçe ığüş\",\"systemWide\":true}]",Encoding.Unicode);var unicodeRule=SystemShortcuts.ReadCode(unicodeFile).Single();check(unicodeRule.App=="","Uygulamanın systemWide kuralı genel kapsamda okunur");SystemShortcuts.WriteCode(unicodeRule,"f9",unicodeRule.Command,"");check(File.ReadAllBytes(unicodeFile).Take(2).SequenceEqual(new byte[]{255,254})&&SystemShortcuts.ReadCode(unicodeFile).Single().Command=="Türkçe ığüş","UTF-16 kaynak kodlaması ve Unicode içerik korunur");foreach(string file in Directory.GetFiles(unicode))File.Delete(file);Directory.Delete(unicode);
    check(!ShortcutKeys.Comparable("ctrl+[UnknownPhysicalKey]"),"Çözülemeyen fiziksel tuş karşılaştırması tam doğrulanmış sayılmaz");
    File.WriteAllText(Path.Combine(root,"broken.lnk"),"broken");var scan=SystemShortcuts.Scan(new[]{root,code},false);check(scan.Items.Count==3&&scan.Warnings.Count==1,"Bozuk kaynak raporlanır ve diğer kaynakların taraması sürer");
    var state=new State();state.ShortcutSources.Add(root);Storage.Write(state,Path.Combine(root,"state.json"));check(Storage.Read(Path.Combine(root,"state.json")).ShortcutSources.Single()==root,"Ek tarama kaynakları kalıcı saklanır");
    ConflictCases(check);
   }finally{foreach(string file in Directory.GetFiles(root))File.Delete(file);Directory.Delete(root);}
  }
  static void ConflictCases(Action<bool,string> check){
   SystemShortcut a=new SystemShortcut{Id="one",Provider="Windows .lnk",Name="First",Key=75,Mods=3},b=new SystemShortcut{Id="two",Provider="Windows .lnk",Name="Second",Key=75,Mods=3};
   var custom=new Binding{Name="Mine",Key=75,Mods=3};
   check(ShortcutConflicts.Find(new Binding[0],new[]{a,b}).Single().Severity=="CONFLICT","Bilgisayardaki iki kaynak aynı tuşu kullanıyorsa bulunur");
   check(ShortcutConflicts.Find(new[]{custom},new[]{a,b}).Count==3,"Harici kaynaklar kendi aralarında ve kişisel atamayla karşılaştırılır");
   b.Enabled=false;check(ShortcutConflicts.Find(new Binding[0],new[]{a,b}).Count==0,"Atanmamış/pasif harici tuş eşleşme sayılmaz");
   var code=new SystemShortcut{Id="code",Provider="Application settings",Name="Find",App="Code.exe",KeyText="ctrl+alt+k",When="editorTextFocus"};custom.AppPath=@"C:\Apps\Code.exe";
   check(ShortcutConflicts.Find(new[]{custom},new[]{code}).Single().Severity=="POSSIBLE","Kaynak koşulu çalışma anında bilinmediğinde olası eşleşme denir");
   custom.AppPath=@"C:\Apps\Other.exe";check(!ShortcutConflicts.Find(new[]{custom},new[]{code}).Any(),"Farklı uygulama kapsamları çakışma sayılmaz");custom.AppPath="";code.When="";
   check(ShortcutConflicts.Find(new[]{custom},new[]{code}).Single().Severity=="CONFLICT","Genel kişisel atama uygulama içindeki aynı tuşla karşılaştırılır");
   code.KeyText="ctrl+k ctrl+c";custom.Mods=2;check(ShortcutConflicts.Find(new[]{custom},new[]{code}).Single().Severity=="POSSIBLE","Tek tuş ataması ile uygulamanın dizi başlangıcı karşılaştırılır");
   var second=custom.Copy();second.Id="other";custom.AppPath=@"C:\One\Code.exe";second.AppPath=@"C:\Two\Code.exe";check(!ShortcutConflicts.Find(new[]{custom,second},new SystemShortcut[0]).Any(),"Farklı tam exe yolları aynı ada rağmen kişisel kapsamda ayrı tutulur");
   custom.AppPath="";second.AppPath=@"C:\Apps\Code.exe";check(ShortcutConflicts.Find(new[]{custom,second},new SystemShortcut[0]).Single().Severity=="PRIORITY","Genel/uygulama önceliği ayrı sınıflandırılır");
   code.KeyText="ctrl+k";code.Reference=true;check(ShortcutConflicts.Find(new[]{custom},new[]{code}).Single().Severity=="POSSIBLE","Varsayılan katalog gerçek kayıtla karıştırılmaz");
   code.Reference=false;code.Profile="profile-one";var profile=code.Copy();profile.Id="profile2";profile.Profile="profile-two";check(!ShortcutConflicts.Find(new Binding[0],new[]{code,profile}).Any(),"Farklı uygulama profilleri birlikte etkinmiş gibi sayılmaz");
   code.App=profile.App="";check(ShortcutConflicts.Find(new Binding[0],new[]{code,profile}).Any(),"Genel uygulama kısayolları profil farkından dolayı göz ardı edilmez");check(ShortcutConflicts.References().Count()>30,"Windows ve uygulama varsayılanları ayrı referans listesinde gösterilir");
  }
  public static void Ui(MainWindow owner,KeyboardEngine keyboard,Action<bool,string> check,Action<Window,string> screenshot){
   var sample=new SystemShortcut{Id="ui-link",Provider="Windows .lnk",Name="Örnek uygulama",FilePath=@"C:\Desktop\Örnek.lnk",Key=75,Mods=3,Target=@"C:\Apps\Example.exe"};var duplicate=sample.Copy();duplicate.Id="ui-duplicate";duplicate.Name="Aynı tuştaki başka uygulama";owner.SystemRowsForTest(new[]{sample,duplicate});check(owner.SystemRowCountForTest==2,"Bilgisayar kısayolları ayrı bölümde gösterilir");check(owner.SelectSystemForTest(0),"Kaynak seçildiğinde düzenleme eylemleri etkinleşir");screenshot(owner,"system-shortcuts-preview.png");owner.ConflictsForTest();screenshot(owner,"system-conflicts-preview.png");
   var dialog=new SystemShortcutWindow(keyboard,sample,new[]{new Binding{Name="Aynı tuş",Key=75,Mods=3}},new[]{sample}){Owner=owner};dialog.Show();try{check(dialog.CanSaveForTest,"Harici kaynak düzenleyicisi mevcut tuşu açar");dialog.KeyForTest("Ctrl+Alt+M");check(dialog.CanSaveForTest,"Kaynak düzenleyicisinde yeni kombinasyon kabul edilir");dialog.KeyForTest("Win+K");check(!dialog.CanSaveForTest,"Kabuk tarafından desteklenmeyen kombinasyon kaydedilmez");dialog.KeyForTest("Ctrl+Alt+M");screenshot(dialog,"system-shortcut-editor-preview.png");}finally{dialog.Close();}
   var initial=new Binding{Name="Atamam",Key=75,Mods=3,Kind="Web address",Target="https://example.com"};var editor=new Editor(keyboard,null,new Binding[0],initial:initial,externalShortcuts:()=>new[]{sample}){Owner=owner};editor.Show();try{check(editor.FeedbackForTest.Contains("Örnek uygulama"),"Klasik yeni atama ekranında dış kaynak çakışması görünür");}finally{editor.Close();}
   var prompt=new PromptWindow(keyboard,new Binding[0],new string[0],externalShortcuts:()=>new[]{sample}){Owner=owner};prompt.Show();try{prompt.PreviewForTest("Ctrl+Alt+K tuşuna basınca kopyala");check(prompt.FeedbackForTest.Contains("Örnek uygulama"),"Prompt önizlemesinde dış kaynak çakışması görünür");}finally{prompt.Close();}
  }
 }
}
