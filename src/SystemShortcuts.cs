using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace AkilliKisayol {
 public sealed class SystemShortcut {
  public string Id,Provider,Name,FilePath="",App="",Profile="",KeyText="",Command="",When="",Target="",Arguments="";
  public byte[] Snapshot;
  public int Key,Mods,Offset,Length;
  public bool Enabled=true,Reference;
  public bool Editable {get{return !Reference&&(Provider=="Windows .lnk"||Provider=="Application settings");}}
  public string Scope {get{return App.Length==0?"Global":App;}}
  public string Combo {get{return Provider=="Windows .lnk"?(Key==0?"Unassigned":Native.Label(Mods,Key)):KeyText;}}
  public string Description {get{return Provider=="Windows .lnk"?Target+(Arguments.Length>0?" "+Arguments:""):Command+(When.Length>0?" • "+When:"");}}
  public SystemShortcut Copy(){return (SystemShortcut)MemberwiseClone();}
 }
 public sealed class ShortcutScan {
  public readonly List<SystemShortcut> Items=new List<SystemShortcut>();
  public readonly List<string> Warnings=new List<string>();
  public int Files;
 }
 public static class SystemShortcuts {
  const string BackupSuffix=".akilli-kisayol.bak";
  public static string Backup(string path){return path+BackupSuffix;}
  static readonly byte[] LinkGuid=new Guid("00021401-0000-0000-C000-000000000046").ToByteArray();
  public static IEnumerable<string> DefaultRoots(){return new[]{Environment.SpecialFolder.DesktopDirectory,Environment.SpecialFolder.CommonDesktopDirectory,Environment.SpecialFolder.StartMenu,Environment.SpecialFolder.CommonStartMenu}.Select(Environment.GetFolderPath).Where(p=>p.Length>0).Distinct(StringComparer.OrdinalIgnoreCase);}
  public static ShortcutScan Scan(IEnumerable<string> extra=null,bool defaults=true){
   var result=new ShortcutScan();var files=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var roots=new List<string>();if(defaults)roots.AddRange(DefaultRoots());roots.AddRange(extra??new string[0]);
   if(defaults)foreach(string product in new[]{"Code","Code - Insiders","Cursor","VSCodium"}){
    string user=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),product,"User");string main=Path.Combine(user,"keybindings.json");if(File.Exists(main))files.Add(main);
    string profiles=Path.Combine(user,"profiles");if(Directory.Exists(profiles))try{foreach(string dir in Directory.GetDirectories(profiles)){string path=Path.Combine(dir,"keybindings.json");if(File.Exists(path))files.Add(path);}}catch(Exception e){ReadError(result,profiles,e);}
   }
   foreach(string root in roots.Distinct(StringComparer.OrdinalIgnoreCase)){
    if(File.Exists(root)){files.Add(Path.GetFullPath(root));continue;}if(!Directory.Exists(root)){result.Warnings.Add("Source not found: "+root);continue;}
    var pending=new Stack<string>();pending.Push(root);int visits=0;while(pending.Count>0){string dir=pending.Pop();if(++visits>5000){result.Warnings.Add("Scan limit reached: "+root);break;}
     try{foreach(string file in Directory.GetFiles(dir,"*.lnk"))files.Add(Path.GetFullPath(file));foreach(string child in Directory.GetDirectories(dir))if((File.GetAttributes(child)&FileAttributes.ReparsePoint)==0)pending.Push(child);}catch(Exception e){ReadError(result,dir,e);}
    }
   }
   foreach(string file in files.OrderBy(p=>p))try{
    if(file.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase))result.Items.Add(ReadLink(file));
    else if(Path.GetFileName(file).Equals("keybindings.json",StringComparison.OrdinalIgnoreCase))result.Items.AddRange(ReadCode(file));
    else {result.Warnings.Add("Source format not recognized (select .lnk or keybindings.json): "+file);continue;}result.Files++;
   }catch(Exception e){ReadError(result,file,e);}
   foreach(var item in result.Items.Where(i=>i.Provider=="Application settings"&&i.Enabled))if(!ShortcutKeys.Comparable(item.KeyText))result.Warnings.Add("This key format cannot be fully compared with personal assignments: "+item.KeyText+" • "+item.FilePath);
   return result;
  }
  static void ReadError(ShortcutScan scan,string path,Exception error){if(error is IOException||error is InvalidDataException||error is UnauthorizedAccessException||error is System.Security.SecurityException||error is ArgumentException||error is InvalidOperationException||error is TargetInvocationException||error is COMException)scan.Warnings.Add(path+": "+error.Message);else throw error;}
  public static SystemShortcut ReadLink(string path){
   byte[] data=File.ReadAllBytes(path);if(data.Length<76||BitConverter.ToUInt32(data,0)!=76||!data.Skip(4).Take(16).SequenceEqual(LinkGuid))throw new InvalidDataException("Not a valid Windows shortcut file.");
   int flags=data[65],mods=((flags&1)!=0?4:0)|((flags&2)!=0?2:0)|((flags&4)!=0?1:0);
   var item=new SystemShortcut{Id="lnk:"+Path.GetFullPath(path),Provider="Windows .lnk",Name=Path.GetFileNameWithoutExtension(path),FilePath=path,Snapshot=data,Key=data[64],Mods=mods,Enabled=data[64]!=0};
   // Only read metadata. Do not resolve, launch or re-save the link through WSH.
   Type type=Type.GetTypeFromProgID("WScript.Shell");object shell=null,link=null;try{
    shell=Activator.CreateInstance(type);link=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});
    item.Target=Convert.ToString(link.GetType().InvokeMember("TargetPath",BindingFlags.GetProperty,null,link,null));item.Arguments=Convert.ToString(link.GetType().InvokeMember("Arguments",BindingFlags.GetProperty,null,link,null));
   }finally{if(link!=null)Marshal.FinalReleaseComObject(link);if(shell!=null)Marshal.FinalReleaseComObject(shell);}return item;
  }
  public static string ValidateLink(int mods,int key){
   if(key==0)return null;if(mods<0||mods>7||key<1||key>254||InputKeys.IsPointer(key)||Native.Modifier(key))return "Windows .lnk assignment must contain Ctrl/Alt/Shift and one key.";
   if(key<112||key>135){if((mods&3)!=3)return "In Windows shell, use keys like letters and numbers with Ctrl+Alt; F1–F24 can be used alone.";}return null;
  }
  public static void WriteLink(SystemShortcut original,int mods,int key){
   string error=ValidateLink(mods,key);if(error!=null)throw new ArgumentException(error);byte[] bytes=(byte[])original.Snapshot.Clone();
   bytes[64]=(byte)key;bytes[65]=(byte)(key==0?0:((mods&4)!=0?1:0)|((mods&2)!=0?2:0)|((mods&1)!=0?4:0)|(new[]{33,34,35,36,37,38,39,40,45,46,111,144}.Contains(key)?8:0));
   Replace(original.FilePath,original.Snapshot,bytes);SHChangeNotify(0x2000,5,original.FilePath,IntPtr.Zero);
  }
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern void SHChangeNotify(uint change,uint flags,string path,IntPtr extra);
  static byte[] TextBytes(string text,byte[] original){using(var stream=new MemoryStream(original))using(var reader=new StreamReader(stream,Encoding.UTF8,true)){reader.ReadToEnd();Encoding encoding=reader.CurrentEncoding;byte[] preamble=encoding.GetPreamble(),bytes=encoding.GetBytes(text);return preamble.Length>0&&original.Take(preamble.Length).SequenceEqual(preamble)?preamble.Concat(bytes).ToArray():bytes;}}
  static string Text(byte[] data){using(var stream=new MemoryStream(data))using(var reader=new StreamReader(stream,Encoding.UTF8,true))return reader.ReadToEnd();}
  // Keep character offsets stable while removing JSONC comments and trailing commas.
  internal static string Jsonc(string source){
   var chars=source.ToCharArray();bool quoted=false,escaped=false;for(int i=0;i<chars.Length;i++){
    char c=source[i];if(quoted){if(escaped)escaped=false;else if(c=='\\')escaped=true;else if(c=='"')quoted=false;continue;}if(c=='"'){quoted=true;continue;}
    if(c=='/'&&i+1<chars.Length&&source[i+1]=='/'){while(i<chars.Length&&source[i]!='\n'&&source[i]!='\r')chars[i++]=' ';i--;}
    else if(c=='/'&&i+1<chars.Length&&source[i+1]=='*'){chars[i++]=chars[i]=' ';bool ended=false;while(++i<chars.Length){if(source[i]=='*'&&i+1<chars.Length&&source[i+1]=='/'){chars[i++]=chars[i]=' ';ended=true;break;}if(chars[i]!='\n'&&chars[i]!='\r')chars[i]=' ';}if(!ended)throw new InvalidDataException("JSONC comment incomplete.");}
   }
   quoted=escaped=false;for(int i=0;i<chars.Length;i++){char c=chars[i];if(quoted){if(escaped)escaped=false;else if(c=='\\')escaped=true;else if(c=='"')quoted=false;}else if(c=='"')quoted=true;else if(c==','){int j=i+1;while(j<chars.Length&&char.IsWhiteSpace(chars[j]))j++;if(j<chars.Length&&(chars[j]==']'||chars[j]=='}'))chars[i]=' ';}}
   return new string(chars);
  }
  static List<Tuple<int,int>> ObjectSpans(string json){
   var spans=new List<Tuple<int,int>>();bool quoted=false,escaped=false;int depth=0,start=-1;for(int i=0;i<json.Length;i++){char c=json[i];if(quoted){if(escaped)escaped=false;else if(c=='\\')escaped=true;else if(c=='"')quoted=false;continue;}if(c=='"'){quoted=true;continue;}if(c=='{'){if(depth==0)start=i;depth++;}else if(c=='}'&&--depth==0)spans.Add(Tuple.Create(start,i-start+1));}return spans;
  }
  static string Field(Dictionary<string,object> rule,string key){object value;return rule.TryGetValue(key,out value)&&value is string?(string)value:"";}
  public static List<SystemShortcut> ReadCode(string path){
   byte[] bytes=File.ReadAllBytes(path);string source=Text(bytes),json=Jsonc(source);object[] rules=LocalLlm.Json().DeserializeObject(json) as object[];if(rules==null)throw new InvalidDataException("keybindings.json must be a JSON array.");
   var spans=ObjectSpans(json);if(spans.Count!=rules.Length||rules.Any(r=>!(r is Dictionary<string,object>)))throw new InvalidDataException("There is a non-object entry in the shortcut list.");
   string app=path.IndexOf("Cursor",StringComparison.OrdinalIgnoreCase)>=0?"Cursor.exe":path.IndexOf("VSCodium",StringComparison.OrdinalIgnoreCase)>=0?"VSCodium.exe":path.IndexOf("Code - Insiders",StringComparison.OrdinalIgnoreCase)>=0?"Code - Insiders.exe":"Code.exe";
   var result=new List<SystemShortcut>();for(int i=0;i<rules.Length;i++){var rule=(Dictionary<string,object>)rules[i];string command=Field(rule,"command"),key=Field(rule,"key");object wide;bool global=rule.TryGetValue("systemWide",out wide)&&wide is bool&&(bool)wide;var item=new SystemShortcut{Id=path+"#"+i,Provider="Application settings",Name=command.Length>0?command:"Empty command (key blocked)",FilePath=path,App=global?"":app,Profile=Path.GetDirectoryName(path),KeyText=key,Command=command,When=Field(rule,"when"),Snapshot=bytes,Offset=spans[i].Item1,Length=spans[i].Item2,Enabled=!command.StartsWith("-")&&key.Length>0};result.Add(item);}return result;
  }
  public static void WriteCode(SystemShortcut item,string key,string command,string when){
   if(string.IsNullOrWhiteSpace(key)||key.Length>150||key.Contains('\n')||key.Contains('\r'))throw new ArgumentException("Enter a valid key/sequence; for example, ctrl+alt+k or ctrl+k ctrl+c.");
   string source=Text(item.Snapshot);var rule=(Dictionary<string,object>)LocalLlm.Json().DeserializeObject(Jsonc(source.Substring(item.Offset,item.Length)));rule["key"]=key.Trim();rule["command"]=command;if(string.IsNullOrWhiteSpace(when))rule.Remove("when");else rule["when"]=when;
   string updated=source.Substring(0,item.Offset)+LocalLlm.Json().Serialize(rule)+source.Substring(item.Offset+item.Length);Replace(item.FilePath,item.Snapshot,TextBytes(updated,item.Snapshot));
  }
  public static void Restore(SystemShortcut item){string backup=Backup(item.FilePath);if(!File.Exists(backup))throw new FileNotFoundException("No backup found for this source.");byte[] bytes=File.ReadAllBytes(backup);if(item.Provider=="Windows .lnk"){if(bytes.Length<76||BitConverter.ToUInt32(bytes,0)!=76||!bytes.Skip(4).Take(16).SequenceEqual(LinkGuid))throw new InvalidDataException("Backup is not a valid shortcut file.");}else if(!(LocalLlm.Json().DeserializeObject(Jsonc(Text(bytes))) is object[]))throw new InvalidDataException("Backup is not a valid shortcut sequence.");Replace(item.FilePath,item.Snapshot,bytes);if(item.Provider=="Windows .lnk")SHChangeNotify(0x2000,5,item.FilePath,IntPtr.Zero);}
  static void Replace(string path,byte[] expected,byte[] updated){
   if(!File.ReadAllBytes(path).SequenceEqual(expected))throw new IOException("Source has changed since the scan. Refresh the list and apply again.");
   string temp=path+".akilli-"+Guid.NewGuid().ToString("N")+".tmp";try{
    using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(updated,0,updated.Length);output.Flush(true);}if(!File.ReadAllBytes(path).SequenceEqual(expected))throw new IOException("Source changed during recording; refresh list.");File.Replace(temp,path,Backup(path));
   }finally{if(File.Exists(temp))File.Delete(temp);}
  }
 }
 public sealed class ShortcutConflict {
  public string Severity,Message;public string FirstId,SecondId;
  public override string ToString(){return Severity+" • "+Message;}
 }
 public static class ShortcutConflicts {
  sealed class Entry {public string Id,Name,Scope,Context,Profile,Provider,Group;public string[] Keys;public bool Reference,Custom,Simultaneous;}
  static string Key(int mods,int key){return mods+":"+key;}
  static string[] Keys(string text){return Regex.Split(text.Trim().ToLowerInvariant(),@"\s+").Where(s=>s.Length>0).Select(s=>{
   int m,k;if(ShortcutKeys.TryCode(s,out m,out k))return Key(m,k);return "raw:"+s.Replace("control+","ctrl+").Replace("windows+","win+");}).ToArray();}
  static string Scope(string path){return string.IsNullOrEmpty(path)?"":Path.GetFileName(path).ToLowerInvariant();}
  public static List<ShortcutConflict> Find(IEnumerable<Binding> bindings,IEnumerable<SystemShortcut> external,bool references=true){
   var shapes=bindings.Where(b=>b.Enabled&&b.Gesture!=null).ToArray();
   var entries=bindings.Where(b=>b.Enabled&&b.Gesture==null).Select(b=>new Entry{Id=b.Id,Name=b.Name,Scope=b.AppPath.ToLowerInvariant(),Context="",Profile="",Provider="Makro+",Group=b.ProfileId??"",Custom=true,Keys=b.PrefixKey>0?new[]{Key(b.Mods,b.PrefixKey),Key(b.Mods,b.Key)}:new[]{Key(b.Mods,b.Key)},Simultaneous=b.PrefixKey>0}).ToList();
   entries.AddRange(external.Where(e=>e.Enabled&&(!e.Reference||references)).Select(e=>new Entry{Id=e.Id,Name=e.Name,Scope=Scope(e.App),Context=e.When,Profile=e.Profile,Provider=e.Provider,Reference=e.Reference,Keys=e.Provider=="Windows .lnk"?new[]{Key(e.Mods,e.Key)}:Keys(e.KeyText)}));
   var result=new List<ShortcutConflict>();foreach(var group in entries.Where(e=>e.Keys.Length>0).GroupBy(e=>e.Keys[0])){var bucket=group.ToArray();for(int i=0;i<bucket.Length;i++)for(int j=i+1;j<bucket.Length;j++){
    var a=bucket[i];var b=bucket[j];if(a.Custom&&b.Custom&&a.Group.Length>0&&b.Group.Length>0&&a.Group!=b.Group)continue;if(a.Scope.Length>0&&b.Scope.Length>0&&(a.Custom&&b.Custom?a.Scope!=b.Scope:Scope(a.Scope)!=Scope(b.Scope)))continue;
    if(a.Provider=="Application settings"&&b.Provider==a.Provider&&a.Scope.Length>0&&a.Scope==b.Scope&&a.Profile!=b.Profile)continue;
    if(a.Reference&&b.Reference)continue;if(a.Keys.Length==0||b.Keys.Length==0)continue;
    bool exact=a.Keys.SequenceEqual(b.Keys)&&a.Simultaneous==b.Simultaneous;
    bool prefix=!exact&&a.Keys.Take(Math.Min(a.Keys.Length,b.Keys.Length)).SequenceEqual(b.Keys.Take(Math.Min(a.Keys.Length,b.Keys.Length)));if(!exact&&!prefix)continue;
    bool conditional=a.Context.Length>0||b.Context.Length>0;
    string severity=a.Reference||b.Reference||conditional||prefix?"POSSIBLE":a.Custom&&b.Custom&&(a.Scope!=b.Scope||a.Group!=b.Group)?"PRIORITY":"CONFLICT";
    string reason=prefix?"Key sequences must start with the same key.":a.Reference||b.Reference?"Matches known default; application-specific setting not verified.":conditional?"Application conditions may be valid at the same time; conditions were not evaluated.":a.Custom&&b.Custom&&(a.Scope!=b.Scope||a.Group!=b.Group)?"Application/profile assignment works before shared assignment.":"Same key and overlapping scope; one source may block the other.";
    result.Add(new ShortcutConflict{Severity=severity,FirstId=a.Id,SecondId=b.Id,Message=a.Name+" ["+a.Provider+"] ↔ "+b.Name+" ["+b.Provider+"] • "+reason});
   }}
   for(int i=0;i<shapes.Length;i++)for(int j=i+1;j<shapes.Length;j++){var messages=ShapeRecognition.Conflicts(shapes[i],new[]{shapes[j]});foreach(string message in messages)result.Add(new ShortcutConflict{Severity=message.StartsWith("CONFLICT")?"CONFLICT":"PRIORITY",FirstId=shapes[i].Id,SecondId=shapes[j].Id,Message=shapes[i].Name+" ↔ "+shapes[j].Name+" • "+message});}
   return result;
  }
  public static IEnumerable<SystemShortcut> References(){
   var common=new[]{
    new CatalogEntry("Cut",2,88,"Windows","Cut selection"),new CatalogEntry("Select all",2,65,"Windows","Select all"),new CatalogEntry("Save",2,83,"Windows","Saves the active document"),new CatalogEntry("Find",2,70,"Windows","Opens the search field"),new CatalogEntry("Redo",2,89,"Windows","Repeat the undone action"),new CatalogEntry("Print",2,80,"Windows","Opens the print screen"),new CatalogEntry("New document/window",2,78,"Windows","Application's new document or window action"),new CatalogEntry("Open file",2,79,"Windows","Open file dialog"),new CatalogEntry("Close document/tab",2,87,"Windows","Closes the active document or tab."),
    new CatalogEntry("Close window",1,115,"Windows","Closes the active window."),new CatalogEntry("Previous application",5,9,"Windows","Revert between applications"),new CatalogEntry("Show Desktop",8,68,"Windows","Shows/hides the Desktop"),new CatalogEntry("Run",8,82,"Windows","Opens the run dialog"),new CatalogEntry("Windows Settings",8,73,"Windows","Opens Windows Settings"),new CatalogEntry("Window history",8,86,"Windows","Opens the panel history if enabled"),new CatalogEntry("Task view",8,9,"Windows","Opens task view"),new CatalogEntry("Screenshot",12,83,"Windows","Opens the screenshot tool"),new CatalogEntry("Minimize windows",8,77,"Windows","Minimize all windows"),new CatalogEntry("Restore windows",12,77,"Windows","Restores minimized windows"),new CatalogEntry("Quick Links",8,88,"Windows","Opens Windows Quick Launch menu"),
    new CatalogEntry("Rename",0,113,"explorer.exe","Rename selected file"),new CatalogEntry("New folder",6,78,"explorer.exe","Creates new folder"),new CatalogEntry("File properties",1,13,"explorer.exe","Open properties of selected item"),new CatalogEntry("Previous folder",1,37,"explorer.exe","Returns to the previous folder")};
   return Rules.Catalog.Concat(common).GroupBy(c=>c.App.ToLowerInvariant()+":"+c.Mods+":"+c.Key).Select(g=>g.First()).Select((c,i)=>new SystemShortcut{Id="default:"+i,Provider="Known default",Name=c.Name,App=c.App=="Windows"?"":c.App,KeyText=TriggerSpec.Format(c.Mods,c.Key),Key=c.Key,Mods=c.Mods,Command=c.Description,Reference=true});
  }
  public static List<string> ForBinding(Binding draft,IEnumerable<SystemShortcut> entries){return Find(new[]{draft},entries).Where(c=>c.FirstId==draft.Id||c.SecondId==draft.Id).Select(c=>c.ToString()).ToList();}
 }
}
