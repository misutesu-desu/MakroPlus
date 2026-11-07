using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace AkilliKisayol {
 [DataContract] public class Binding {
  [DataMember] public string Id = Guid.NewGuid().ToString();
  [DataMember] public string Name = "";
  [DataMember] public int Key;
  [DataMember] public int Mods;
  [DataMember(EmitDefaultValue=false)] public int PrefixKey;
  [DataMember(EmitDefaultValue=false)] public string BrowserPath;
  [DataMember] public string AppPath = "";
  [DataMember] public string Kind = "Application";
  [DataMember] public string Target = "";
  [DataMember] public string Arguments = "";
  [DataMember] public bool Enabled = true;
  [DataMember] public bool PassThrough;
  [DataMember] public int SendKey;
  [DataMember] public int SendMods;
  [DataMember(EmitDefaultValue=false)] public string Prompt;
  [DataMember(EmitDefaultValue=false)] public List<ActionStep> Steps;
  [DataMember(EmitDefaultValue=false)] public string ProfileId="";
  [DataMember(EmitDefaultValue=false)] public bool IsMacro;
  [DataMember(EmitDefaultValue=false)] public ShapeTemplate Gesture;
  public string Scope { get { return string.IsNullOrEmpty(AppPath) ? "Global" : Path.GetFileName(AppPath); } }
  public string Combo { get { return Gesture!=null?"Shape: "+Name:Native.Label(Mods, Key,PrefixKey); } }
  public string Summary { get { return Kind=="Browser website"?AppLibrary.Name(BrowserPath??"")+" → "+Target:Kind=="Workflow"?string.Join(" → ",(Steps??new List<ActionStep>()).Select(s=>s.Summary)):Kind == "Send key" ? Native.Label(SendMods,SendKey) : Target; } }
  public Binding Copy() { var copy=(Binding)MemberwiseClone();if(Steps!=null)copy.Steps=Steps.Select(s=>s.Copy()).ToList();if(Gesture!=null)copy.Gesture=Gesture.Copy();return copy; }
 }
 [DataContract] public class ActionStep {
  [DataMember] public string Kind="Key";
  [DataMember(EmitDefaultValue=false)] public string AppPath;
  [DataMember] public int Key,Mods,Delay;
  [DataMember] public string Text="";
  [DataMember(EmitDefaultValue=false)] public string AutomationId,WindowTitle,WindowClass;
  [DataMember(EmitDefaultValue=false)] public int ControlType,MouseButton,ScrollDelta;
  [DataMember(EmitDefaultValue=false)] public bool AltGr;
  [DataMember(EmitDefaultValue=false)] public int Duration;
  [DataMember(EmitDefaultValue=false)] public bool Horizontal,ScaleWithWindow;
  [DataMember(EmitDefaultValue=false)] public double X,Y,X2,Y2,WindowWidth,WindowHeight;
  public string Summary {get {if(Kind=="Mouse click"||Kind=="Mouse drag"||Kind=="Scroll")return Kind+" • "+(Kind=="Scroll"?(Horizontal?"horizontal ":"vertical ")+ScrollDelta:InputKeys.ButtonName(MouseButton))+" • ("+Math.Round(X)+", "+Math.Round(Y)+")"+(Kind=="Mouse drag"?" → ("+Math.Round(X2)+", "+Math.Round(Y2)+")":"")+(Duration>0?" • "+Duration+" ms":"")+(ScaleWithWindow?" • proportional":"");return Kind=="Key"?Native.Label(Mods,Key)+(AltGr?" (AltGr)":""):Kind=="Mouse click"||Kind=="Mouse drag"||Kind=="Scroll"?Kind+(Kind=="Scroll"?" ("+(Horizontal?"horizontal ":"")+ScrollDelta+")":" • "+InputKeys.ButtonName(MouseButton))+" (window: "+Math.Round(X)+", "+Math.Round(Y)+")":Kind=="Click element"?"Item: "+Text+" ["+(AutomationId??"")+"]":Kind=="Wait"?Delay+" ms wait":Kind=="Click button"?"Button: "+Text:Kind=="Open application"||Kind=="Open content"||Kind=="Focus"?Kind+": "+BrowserSupport.DisplayName(AppPath??"")+" → "+Text:Kind=="PowerShell"||Kind=="Terminal (cmd)"?Kind+":\n"+Text:"Type text: “"+Text+"”";}}
  public ActionStep Copy(){return (ActionStep)MemberwiseClone();}
 }
 [DataContract] public class Activity {
  [DataMember] public string Time;
  [DataMember] public string Name;
  [DataMember] public string Result;
 }
 [DataContract] public class State {
  [DataMember] public List<Binding> Bindings = new List<Binding>();
  [DataMember] public List<Activity> History = new List<Activity>();
  [DataMember] public bool Dark = true;
  [DataMember(EmitDefaultValue=false)] public string ThemeId;
  [DataMember(EmitDefaultValue=false)] public List<ThemePalette> CustomThemes=new List<ThemePalette>();
  [DataMember] public bool Paused;
  [DataMember] public List<string> ApplicationPaths = new List<string>();
  [DataMember] public string ModelPath = LocalLlm.DefaultModelPath;
  [DataMember(EmitDefaultValue=false)] public ModelOptions ModelOptions=new ModelOptions();
  [DataMember(EmitDefaultValue=false)] public int? StepIntervalMs;
  [DataMember(EmitDefaultValue=false)] public List<string> ShortcutSources = new List<string>();
  [DataMember] public List<ShortcutProfile> Profiles=AkilliKisayol.Profiles.Defaults();
  [DataMember] public string ActiveProfileId="work";
  [DataMember] public UserPreferences Preferences=new UserPreferences();
  [DataMember] public bool GuideEnabled=true;
  [DataMember] public int GuideHoldKey=163;
  [DataMember(EmitDefaultValue=false)] public bool? UseGpu;
  [DataMember(EmitDefaultValue=false)] public GestureOptions Gestures=new GestureOptions();
  [DataMember(EmitDefaultValue=false)] public bool AllowRemappedInput;
 }
 public static class Storage {
  static readonly object writeGate=new object();
  public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AkilliKisayol");
  public static readonly string FilePath = Path.Combine(Folder,"settings.json");
  public static State Read(string path) {
   if (!File.Exists(path)) return new State();
   if(new FileInfo(path).Length>16777216)throw new InvalidDataException("Settings file exceeds 16 MB limit.");
   using(var s=File.OpenRead(path)) {
    var state=(State)new DataContractJsonSerializer(typeof(State)).ReadObject(s);
    if(state==null || state.Bindings==null || state.History==null || state.Bindings.Any(b=>b==null || b.Id==null || b.AppPath==null || b.Target==null || b.Name==null || b.Kind==null)) throw new InvalidDataException("Invalid settings file.");
    if(state.Bindings.Count>5000||state.Bindings.Select(b=>b.Id).Distinct().Count()!=state.Bindings.Count||state.Bindings.Any(b=>string.IsNullOrWhiteSpace(b.Id)||b.Steps!=null&&b.Steps.Any(step=>step==null)))throw new InvalidDataException("Number of assignments, identities, or steps is invalid.");
    LegacyData.Normalize(state);
    ThemeCatalog.Normalize(state);
    if(state.ApplicationPaths==null) {
     state.ApplicationPaths=new List<string>();
     foreach(var binding in state.Bindings) {
      if(binding.AppPath.Length>0) AppLibrary.Remember(state.ApplicationPaths,binding.AppPath);
      if(binding.Kind=="Application" && binding.Target.Length>0) AppLibrary.Remember(state.ApplicationPaths,binding.Target);
     }
    }
    state.ApplicationPaths=state.ApplicationPaths.Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    state.ModelPath=ModelFiles.Resolve(state.ModelPath);
    if(state.ModelOptions==null)state.ModelOptions=new ModelOptions();if(state.StepIntervalMs.HasValue)state.StepIntervalMs=Math.Max(0,Math.Min(500,state.StepIntervalMs.Value));
    if(state.ShortcutSources==null)state.ShortcutSources=new List<string>();
    Profiles.Normalize(state);if(state.Preferences==null){state.Preferences=new UserPreferences();foreach(var saved in state.Bindings)PreferenceMemory.Learn(state.Preferences,saved);}else state.Preferences=state.Preferences.Copy();if(state.GuideHoldKey==0){state.GuideHoldKey=163;state.GuideEnabled=true;}
    foreach(var binding in state.Bindings)LinkIntent.Repair(binding);
    return state;
   }
  }
  static long snapshotSequence;static readonly Dictionary<string,long> writtenSequence=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
  public sealed class Snapshot {internal byte[] Data;internal long Sequence;}
  public static Snapshot Capture(State state){using(var stream=new MemoryStream()){new DataContractJsonSerializer(typeof(State)).WriteObject(stream,state);return new Snapshot{Data=stream.ToArray(),Sequence=System.Threading.Interlocked.Increment(ref snapshotSequence)};}}
  public static void Write(State state,string path) {Write(Capture(state),path);}
  // Snapshots may finish writing out of order; an older one must never replace a newer file.
  public static void Write(Snapshot snapshot,string path) {
   string full=Path.GetFullPath(path);long written;
   lock(writeGate){if(writtenSequence.TryGetValue(full,out written)&&written>snapshot.Sequence)return;WriteCore(snapshot.Data,full);writtenSequence[full]=snapshot.Sequence;}
  }
  static void WriteCore(byte[] data,string path){
   Directory.CreateDirectory(Path.GetDirectoryName(path));
   string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{
   using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) {
    stream.Write(data,0,data.Length); stream.Flush(true);
   }
   if(File.Exists(path)) File.Replace(temp,path,path+".bak"); else File.Move(temp,path);
   }finally{if(File.Exists(temp))File.Delete(temp);}
  }
  public static State LoadRecoverable(string path,out string notice){
   notice="";try{return Read(path);}catch(Exception error){
    if(!(error is SerializationException)&&!(error is InvalidDataException)&&!(error is System.Xml.XmlException))throw;
    string copy=path+".corrupt-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss")+"-"+Guid.NewGuid().ToString("N")+".json";File.Copy(path,copy);State restored;
    try{restored=Read(path+".bak");if(!File.Exists(path+".bak"))throw new InvalidDataException();notice="Settings file could not be read; previous backup restored. Corrupt copy: "+copy;}catch{restored=new State{Paused=true};notice="Settings file and backup could not be read. Corrupt file preserved; empty and suspended session started. File: "+copy;}
    restored.Paused=true;Write(restored,path);return restored;
   }
  }
 }
 public class CatalogEntry {
  public string Name, App, Description; public int Mods, Key;
  public CatalogEntry(string name,int mods,int key,string app,string description) {Name=name;Mods=mods;Key=key;App=app;Description=description;}
 }
 public static class Rules {
  public static readonly CatalogEntry[] Catalog = {
   new CatalogEntry("Copy",2,67,"Windows","Copy selection to clipboard"),
   new CatalogEntry("Paste",2,86,"Windows","Paste from clipboard"),
   new CatalogEntry("Undo",2,90,"Windows","Undo last action"),
   new CatalogEntry("File Explorer",8,69,"Windows","Opens File Explorer"),
   new CatalogEntry("Lock the computer",8,76,"Windows","Oturumu kilitler"),
   new CatalogEntry("Change application",1,9,"Windows","Valid between open windows"),
   new CatalogEntry("Task Manager",6,27,"Windows","Opens Task Manager"),
   new CatalogEntry("Start / Pause",0,32,"Spotify.exe","Spotify is in the foreground"),
   new CatalogEntry("Next track",2,39,"Spotify.exe","Spotify is in the foreground"),
   new CatalogEntry("Previous track",2,37,"Spotify.exe","Spotify is in the foreground"),
   new CatalogEntry("Command palette",6,80,"Code.exe","When Visual Studio Code is in the foreground"),
   new CatalogEntry("Go to file",2,80,"Code.exe","When Visual Studio Code is in the foreground")
  };
  public static bool SameApp(string a,string b) {return string.Equals(a,b,StringComparison.OrdinalIgnoreCase);}
  public static Binding Resolve(IEnumerable<Binding> items,int mods,int key,string app,int prefix=0) {
   var matches=items.Where(b=>b.Gesture==null && b.Enabled && b.Mods==mods && b.Key==key && b.PrefixKey==prefix).OrderByDescending(b=>!string.IsNullOrEmpty(b.ProfileId));
   return matches.FirstOrDefault(b=>b.AppPath.Length>0 && SameApp(b.AppPath,app)) ?? matches.FirstOrDefault(b=>b.AppPath.Length==0);
  }
  public static string Reserved(int mods,int key) {
   if(mods<0||mods>15||!InputKeys.Valid(key)) return "Invalid key identifier or modifier.";
   if(key==0 || Native.Modifier(key)) return "Select a primary key.";
   if((mods&8)!=0) return "Windows-key combinations are reserved by the operating system and cannot be assigned in this version.";
   if(key==123) return "F12 is assigned to the debugger.";
   if((mods==3 && key==46)||(mods==1 && (key==9||key==27||key==115))||(mods==2&&key==27)||(mods==6&&key==27)) return "This system combination cannot be assigned securely.";
   return null;
  }
  public static string Sendable(int mods,int key){
   if(mods<0||mods>15||!InputKeys.Valid(key)||Native.Modifier(key))return "Key to send is invalid.";
   if(mods==3&&key==46)return "Ctrl+Alt+Del belongs to the Windows secure desktop and cannot be synthesized by applications.";
   return null;
  }
  public static List<string> Conflicts(Binding b,IEnumerable<Binding> items) {
   if(b.Gesture!=null)return ShapeRecognition.Conflicts(b,items);var result=new List<string>();
   foreach(var old in items.Where(x=>x.Gesture==null && x.Id!=b.Id && x.Enabled && b.Enabled && Profiles.Overlap(b,x) && x.Mods==b.Mods && ((x.Key==b.Key&&x.PrefixKey==b.PrefixKey)||(x.PrefixKey==0&&x.Key==b.PrefixKey)||(b.PrefixKey==0&&b.Key==x.PrefixKey)))) {
    if((b.PrefixKey!=old.PrefixKey)&&(b.AppPath.Length==0||old.AppPath.Length==0||SameApp(b.AppPath,old.AppPath))){result.Add("CONFLICT • "+b.Combo+" ↔ "+old.Combo+" • The shortcut key is linked to another assignment. Change the key or close the current assignment.");continue;}
    if(SameApp(b.AppPath,old.AppPath)&&!string.Equals(b.ProfileId??"",old.ProfileId??"",StringComparison.Ordinal))result.Add("PRIORITY • "+b.Combo+" • Profile-specific assignment runs before general assignment.");else if(SameApp(b.AppPath,old.AppPath)) result.Add("CONFLICT • "+b.Combo+" • "+b.Name+" ["+b.Scope+"] ↔ "+old.Name+" ["+old.Scope+"]. Change/disable existing assignment or select a new key.");
    else if(b.AppPath.Length==0 || old.AppPath.Length==0) result.Add("PRIORITY • "+b.Combo+" • "+b.Name+" ["+b.Scope+"] ↔ "+old.Name+" ["+old.Scope+"]. Custom assignment works; general assignment does not work in this application. You can change the scope or key.");
   }
   foreach(var c in Catalog.Where(c=>b.PrefixKey==0 && c.Mods==b.Mods && c.Key==b.Key && (c.App=="Windows" || b.AppPath.Length==0 || SameApp(Path.GetFileName(b.AppPath),c.App)))) result.Add("POSSIBLE • "+b.Combo+" • "+c.Name+" ["+c.App+"], known default; not verified. You can select a new key or a narrower scope.");
   return result;
  }
  public static string NormalizeSpotify(string text) {
   Uri u;
   if(Uri.TryCreate(text,UriKind.Absolute,out u) && u.Host.Equals("open.spotify.com",StringComparison.OrdinalIgnoreCase)) {
    var parts=u.AbsolutePath.Trim('/').Split('/');
    if(parts.Length>2 && parts[0].StartsWith("intl-")) parts=parts.Skip(1).ToArray();
    if(parts.Length==2 && new[]{"track","album","playlist","artist","episode","show"}.Contains(parts[0]) && System.Text.RegularExpressions.Regex.IsMatch(parts[1],"^[a-zA-Z0-9]+$")) return "spotify:"+parts[0]+":"+parts[1];
   }
   if(System.Text.RegularExpressions.Regex.IsMatch(text,@"^spotify:(track|album|playlist|artist|episode|show):[a-zA-Z0-9]+$")) return text;
   throw new ArgumentException("Enter a valid Spotify content link or URI.");
  }
  static bool Finite(double value){return !double.IsNaN(value)&&!double.IsInfinity(value);}
  public static string Validate(Binding b,IEnumerable<Binding> items) {
   if(string.IsNullOrWhiteSpace(b.Name)) return "Enter a name for the shortcut.";
   var reserved=b.Gesture==null?Reserved(b.Mods,b.Key):ShapeRecognition.Validate(b.Gesture); if(reserved!=null) return reserved;
   if(b.PrefixKey>0){if(!TriggerSpec.PrefixAllowed(b.PrefixKey)||b.PrefixKey==b.Key)return "The shortcut key must be Delete, Insert, or F1–F24 (excluding F12) and must be different from the main key.";reserved=Reserved(b.Mods,b.PrefixKey);if(reserved!=null)return reserved;if(b.PassThrough)return "Source application is not supported for two-key shortcuts.";}
   if(b.AppPath.Length>0 && (!Path.IsPathRooted(b.AppPath) || !File.Exists(b.AppPath) || !b.AppPath.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))) return "Select an existing .exe file for the scope.";
   if(b.Enabled){var conflicts=Conflicts(b,items).Where(c=>c.StartsWith("CONFLICT")).ToList();if(conflicts.Count>0)return string.Join("\n",conflicts);}
   if(b.Kind=="Browser website"){if(!BrowserSupport.IsBrowser(b.BrowserPath)||!File.Exists(b.BrowserPath))return "Select an existing supported browser .exe file.";Uri web;if(!Uri.TryCreate(b.Target,UriKind.Absolute,out web)||(web.Scheme!="https"&&web.Scheme!="http"))return "Enter a valid HTTP(S) web address.";return null;}
   if(b.Kind=="Workflow") {
    if(b.Steps==null||b.Steps.Count<1||b.Steps.Count>(b.IsMacro?500:20))return b.IsMacro?"Macro must contain 1–500 steps.":"Operation sequence must contain 1–20 steps.";
    foreach(var step in b.Steps){if(step==null)return "Invalid operation step.";if(!string.IsNullOrWhiteSpace(step.AppPath)&&(!Path.IsPathRooted(step.AppPath)||!File.Exists(step.AppPath)||!step.AppPath.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)))return "The target application for this step was not found: "+step.AppPath;if(step.Kind=="Key"){var issue=Sendable(step.Mods,step.Key);if(issue!=null)return issue;}else if(step.Kind=="Wait"){if(step.Delay<0||step.Delay>10000)return "Wait time must be between 0-10000 ms.";}else if(step.Kind=="Text"||step.Kind=="Click button"){if(string.IsNullOrWhiteSpace(step.Text)||step.Text.Length>4000)return "Enter a text of 1–4000 characters for this step.";}else if(step.Kind=="Open application"||step.Kind=="Open content"||step.Kind=="Focus"){if(string.IsNullOrWhiteSpace(step.AppPath)||!Path.IsPathRooted(step.AppPath)||!File.Exists(step.AppPath)||!step.AppPath.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))return "Select an existing registered application for this step.";if(step.Kind=="Open content"&&string.IsNullOrWhiteSpace(step.Text))return "Connection or file to open is missing.";if(step.Kind=="Focus"&&!string.IsNullOrEmpty(step.Text))return "The focus step cannot have additional text.";}else if(step.Kind=="Mouse click"||step.Kind=="Mouse drag"||step.Kind=="Scroll"){if(!b.IsMacro||step.WindowWidth<=0||step.WindowHeight<=0||step.WindowWidth>100000||step.WindowHeight>100000||step.X<0||step.Y<0||step.X>=step.WindowWidth||step.Y>=step.WindowHeight||step.Mods<0||step.Mods>15||step.MouseButton<0||step.MouseButton>4||step.Duration<0||step.Duration>30000||!Finite(step.X)||!Finite(step.Y)||!Finite(step.X2)||!Finite(step.Y2)||!Finite(step.WindowWidth)||!Finite(step.WindowHeight))return "Invalid registered mouse step.";if(step.Kind=="Scroll"&&(step.ScrollDelta==0||step.ScrollDelta>120000||step.ScrollDelta< -120000))return "Shift amount cannot be zero and must be between -120000 and 120000.";if(step.Kind=="Mouse drag"&&(step.X2<0||step.Y2<0||step.X2>=step.WindowWidth||step.Y2>=step.WindowHeight))return "Drop target is outside the window.";}else if(step.Kind=="Click element"){if(!b.IsMacro||string.IsNullOrEmpty(step.AutomationId)&&string.IsNullOrEmpty(step.Text))return "Invalid accessible item step.";}else if(step.Kind=="PowerShell"||step.Kind=="Terminal (cmd)"){if(string.IsNullOrWhiteSpace(step.Text)||step.Text.Length>4000)return "Command step is empty or too long.";}else return "Unsupported operation step.";}
    return null;
   }
   if(b.Kind=="Send key") return Sendable(b.SendMods,b.SendKey);
   if(string.IsNullOrWhiteSpace(b.Target)) return "Eylem hedefini doldurun.";
   if(b.Kind=="Application" && (!File.Exists(b.Target) || !b.Target.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))) return "Select an existing .exe file.";
   if(b.Kind=="File / folder" && !File.Exists(b.Target) && !Directory.Exists(b.Target)) return "File or folder not found.";
   Uri uri;
   if(b.Kind=="Web address" && (!Uri.TryCreate(b.Target,UriKind.Absolute,out uri) || (uri.Scheme!="https" && uri.Scheme!="http"))) return "http:// veya https:// adresi girin.";
   if(b.Kind=="URI / deep link" && (!Uri.TryCreate(b.Target,UriKind.Absolute,out uri) || uri.IsFile || uri.Scheme=="javascript" || uri.Scheme=="data")) return "Enter a valid application URI.";
   if(b.Kind=="Spotify") {try {NormalizeSpotify(b.Target);} catch(Exception e) {return e.Message;}}
   if(!new[]{"Application","File / folder","Web address","URI / deep link","Spotify","Send key","Media","PowerShell","Terminal (cmd)"}.Contains(b.Kind)) return "Unknown action type.";
   return null;
  }
 }
}
