using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AkilliKisayol {
 public class PromptPlan {
  internal bool ModelProposedReady;
  public Binding Binding;
  public readonly List<string> Errors=new List<string>(),Notes=new List<string>();
  public bool Ready {get{return Binding!=null&&Errors.Count==0;}}
 }
 public static class PromptParser {
  static readonly string ComboPattern=@"(?<![\p{L}\p{N}])(?:(?:ctrl|control|kontrol|alt|shift|win|windows)(?:\s*\+\s*|\s+))*"+TriggerSpec.KeyPattern+@"(?=$|[^\p{L}\p{N}])";
  static readonly Regex Separator=new Regex(@"\G(?:\s*(?:;|\r?\n|,(?!\d))\s*(?:then\s+)?|\s*\b(?:sonra|ardindan|then|and)\b\s*)",RegexOptions.Compiled);
  public static string Normalize(string text){return text.Replace('İ','i').Replace('I','i').ToLowerInvariant().Replace('ı','i').Replace('ş','s').Replace('ğ','g').Replace('ü','u').Replace('ö','o').Replace('ç','c').Replace('’','\'');}
  public static bool TryCombo(string text,out int mods,out int key){
   mods=key=0;string clean=TriggerSpec.NormalizeKeys(text).Trim().ToLowerInvariant();clean=Regex.Replace(clean,@"\b(ctrl|control|kontrol|alt|shift|win|windows)\s+(?=[^+\s])","$1+");var parts=clean.Split('+').Select(x=>x.Trim()).ToArray();if(parts.Length==0)return false;
   foreach(string part in parts.Take(parts.Length-1)){int flag=part=="ctrl"||part=="control"||part=="kontrol"?2:part=="alt"?1:part=="shift"?4:part=="win"||part=="windows"?8:0;if(flag==0||(mods&flag)!=0)return false;mods|=flag;}
   string last=parts.Last();if(last.Length==1&&char.IsLetterOrDigit(last[0]))key=char.ToUpperInvariant(last[0]);
   else if(Regex.IsMatch(last,@"^f([1-9]|1[0-9]|2[0-4])$"))key=111+int.Parse(last.Substring(1));
   else{var names=new Dictionary<string,int>{{"enter",13},{"return",13},{"escape",27},{"esc",27},{"tab",9},{"space",32},{"bosluk",32},{"backspace",8},{"delete",46},{"del",46},{"insert",45},{"home",36},{"end",35},{"pageup",33},{"pagedown",34},{"up",38},{"yukari",38},{"down",40},{"asagi",40},{"left",37},{"sol",37},{"right",39},{"sag",39}};names.TryGetValue(last,out key);}
   if(key==0)key=InputKeys.Parse(last);
   if(key==0){System.Windows.Forms.Keys parsed;if(Enum.TryParse<System.Windows.Forms.Keys>(last,true,out parsed)&&((int)parsed)>0&&((int)parsed)<255)key=(int)parsed;}
   return key>0;
  }
  internal static bool MentionsCombo(string text,int mods,int key){
   string normalized=Regex.Replace(Normalize(text),@"'(?:e|a|ye|ya|de|da|te|ta|nde|nda)\b","");
   foreach(Match match in Regex.Matches(normalized,ComboPattern)){int m,k;if(TryCombo(match.Value,out m,out k)&&m==mods&&k==key)return true;}return false;
  }
  static string[] Aliases(string path){
   string name=Normalize(AppLibrary.Name(path));
   if(name=="code")return new[]{"visual studio code","vs code","vscode","code"};
   if(name=="notepad")return new[]{"not defteri","notepad"};
   if(name=="msedge")return new[]{"microsoft edge","msedge","edge"};
   if(name=="chrome")return new[]{"google chrome","chrome"};
   if(name=="explorer")return new[]{"dosya gezgini","explorer"};return new[]{name};
  }
  public static PromptPlan Parse(string text,IEnumerable<string> applications,string selectedApp="",string triggerOverride="",ShapeTemplate shape=null){
   var plan=new PromptPlan();if(string.IsNullOrWhiteSpace(text)){plan.Errors.Add("Write what you want to do.");return plan;}
   string normalized=Normalize(text);var delimiter=Regex.Match(normalized,@"\b(?:basinca|bastigimda|basildiginda|ile)\b|=>|->|→");
   string header=delimiter.Success?text.Substring(0,delimiter.Index):text;
   string action=delimiter.Success?text.Substring(delimiter.Index+delimiter.Length):text;
   int mods=0,key=0,prefixKey=0;string trigger=(triggerOverride??"").Trim();
   var english=Regex.Match(text,@"^\s*(?:when\s+i\s+press\s+(?<key>.+?)\s*,\s*|assign\s+(?<key>.+?)\s+to\s+)(?<action>.+)$",RegexOptions.IgnoreCase|RegexOptions.Singleline);if(shape==null&&english.Success){header=english.Groups["key"].Value;action=english.Groups["action"].Value;if(trigger.Length==0)trigger=header;}
   if(trigger.Length==0&&delimiter.Success){string keyHeader=Regex.Replace(Normalize(header),@"'(?:e|a|ye|ya|de|da|te|ta|nde|nda)\b","");var matches=Regex.Matches(keyHeader,ComboPattern,RegexOptions.IgnoreCase);if(matches.Count>0)trigger=matches[matches.Count-1].Value;}
   if(shape==null){TriggerSpec activation;if(!TriggerSpec.TryParse(trigger,out activation))plan.Errors.Add("Specify the shortcut key: for example, press 'F8' to... or type Ctrl+Alt+K in the Key field above.");else{mods=activation.Mods;key=activation.Key;prefixKey=activation.Prefix;}}
   if(shape!=null&&ShapeRecognition.Validate(shape)!=null){plan.Errors.Add(ShapeRecognition.Validate(shape));}
   string app=selectedApp??"";
   bool global=Regex.IsMatch(Normalize(header),@"\b(genel|her uygulamada|her yerde)\b");
   var matchesApps=applications.Distinct(StringComparer.OrdinalIgnoreCase).Where(path=>Aliases(path).Any(alias=>Regex.IsMatch(Normalize(header),@"(?<![a-z0-9])"+Regex.Escape(alias)+@"(?=$|['\s,.:]|da\b|de\b|ta\b|te\b|nda\b|nde\b)"))).ToList();
   if(global&&matchesApps.Count>0)plan.Errors.Add("Your request contains both general scope and application name. Select one and clarify the request.");
   if(global)app="";else if(matchesApps.Count==1)app=matchesApps[0];else if(matchesApps.Count>1&&!matchesApps.Any(p=>Rules.SameApp(p,app)))plan.Errors.Add("Multiple applications found with the same name. Select the full file path from the top.");
   if(!global&&matchesApps.Count==0&&Regex.IsMatch(Normalize(header),@"['’](?:da|de|ta|te|nda|nde)(?:yken)?\b|uygulamasinda|icinde|on plandayken|acikken"))plan.Errors.Add("The application you typed is not found. Select from the application selection area or add .exe.");
   action=Regex.Replace(action,@"^[\s,'’:\-]+","");
   if(!delimiter.Success){var prefix=Regex.Match(Normalize(action),@"^.*?(?:'(?:da|de|ta|te)\b|\b(?:icinde|on plandayken|uygulamasinda)\b)\s*[:,]?\s*");if(prefix.Success)action=action.Substring(prefix.Length);}
   var steps=new List<ActionStep>();foreach(string clause in Split(action)){
    var parsed=ParseStep(clause,app,plan);if(parsed==null)plan.Errors.Add("Unrecognized step: \""+clause+"\". Write more explicitly; for example, 'Send Ctrl+S', 'Write \"Hello\"', or 'Click the \"Save\" button'.");else steps.AddRange(parsed);
   }
   if(steps.Count==0&&plan.Errors.Count==0)plan.Errors.Add("Write the action to be performed.");
   if(steps.Count>20)plan.Errors.Add("In an assignment, up to 20 steps can be used.");
   if(plan.Errors.Count>0)return plan;
   var b=new Binding{Name=(app.Length>0?AppLibrary.Name(app)+" • ":"")+action.Trim(),Key=key,Mods=mods,PrefixKey=prefixKey,Gesture=shape==null?null:shape.Copy(),AppPath=app,Kind="Workflow",Steps=steps,Prompt=text,Target=""};if(b.Name.Length>100)b.Name=b.Name.Substring(0,100);
   string issue=Rules.Validate(b,new Binding[0]);if(issue!=null){plan.Errors.Add(issue);return plan;}
   if(app.Length==0)plan.Notes.Add("General scope: steps are sent to the window at the time of triggering. Optionally, select an application from the top.");
   plan.Notes.Add("Playback uses your configured speed. Add explicit Wait steps when the target application needs time. A foreground-window change stops the remaining steps.");
   plan.Binding=b;return plan;
  }
  static List<string> Split(string text){
   var parts=new List<string>();var current=new StringBuilder();bool quoted=false;string normalized=Normalize(text);
   for(int i=0;i<text.Length;i++){
    char c=text[i];if(c=='"'||c=='“'||c=='”')quoted=!quoted;
    var sep=!quoted?Separator.Match(normalized,i):Match.Empty;
    if(sep.Success&&sep.Length>0){if(current.ToString().Trim().Length>0)parts.Add(current.ToString().Trim().TrimEnd(','));current.Clear();i+=sep.Length-1;}else current.Append(c);
   }
   if(quoted) return new List<string>{text};
   if(current.ToString().Trim().Length>0)parts.Add(current.ToString().Trim());return parts;
  }
  static List<ActionStep> ParseStep(string original,string app,PromptPlan plan){
   string action=Normalize(original).Trim().TrimEnd('.');action=Regex.Replace(action,@"^(?:once|lutfen)\s+","");int mods,key;
   var englishText=Regex.Match(original.Trim(),"^(?:type|write)\\s+[\\\"“](?<text>.*)[\\\"”]\\s*\\.?$",RegexOptions.IgnoreCase|RegexOptions.Singleline);if(englishText.Success)return One(new ActionStep{Kind="Text",Text=englishText.Groups["text"].Value});
   var englishWait=Regex.Match(action,@"^wait\s+(\d+(?:[.,]\d+)?)\s*(ms|milliseconds?|s|seconds?)$");if(englishWait.Success){double delay=double.Parse(englishWait.Groups[1].Value.Replace(',','.'),CultureInfo.InvariantCulture)*(englishWait.Groups[2].Value.StartsWith("s")?1000:1);if(delay>10000){plan.Errors.Add("A wait step cannot exceed 10 seconds.");return new List<ActionStep>();}return One(new ActionStep{Kind="Wait",Delay=(int)delay});}
   string englishCombo=Regex.Replace(action,@"^(?:press|send)\s+","");if(englishCombo!=action&&TryCombo(englishCombo,out mods,out key))return One(new ActionStep{Kind="Key",Mods=mods,Key=key});
   var englishCommon=new Dictionary<string,int>{{"select all",65},{"select all text",65},{"copy",67},{"copy it",67},{"copy text",67},{"copy the selected text",67},{"paste",86},{"cut",88},{"save",83},{"undo",90},{"redo",89},{"find",70}};if(englishCommon.TryGetValue(action,out key))return One(new ActionStep{Kind="Key",Mods=2,Key=key});
   var quoted=Regex.Match(original,"^[\"“](?<text>.*?)[\"”]\\s*(?<tail>.+)$",RegexOptions.Singleline);
   if(quoted.Success){string tail=Normalize(quoted.Groups["tail"].Value).Trim().TrimEnd('.');string value=quoted.Groups["text"].Value;
    if(Regex.IsMatch(tail,@"^(?:metnini\s+)?(?:yaz|yazdir)$"))return One(new ActionStep{Kind="Text",Text=value});
    if(Regex.IsMatch(tail,@"^(?:adli\s+)?(?:dugmesine|butonuna|menu ogesine)\s+tikla$")){plan.Notes.Add("Button name must match exactly in the Windows Accessibility tree; if not found or if multiple matches are found, the operation will stop.");return One(new ActionStep{Kind="Click button",Text=value});}
   }
   if(action.StartsWith("metin yaz:"))return One(new ActionStep{Kind="Text",Text=original.Substring(original.IndexOf(':')+1).Trim()});
   var wait=Regex.Match(action,@"^(\d+(?:[.,]\d+)?)\s*(ms|milisaniye|saniye|sn)\s+bekle$");
   if(wait.Success){double delay=double.Parse(wait.Groups[1].Value.Replace(',','.'),CultureInfo.InvariantCulture)*(wait.Groups[2].Value=="saniye"||wait.Groups[2].Value=="sn"?1000:1);if(delay>10000){plan.Errors.Add("A wait step cannot exceed 10 seconds.");return new List<ActionStep>();}return One(new ActionStep{Kind="Wait",Delay=(int)delay});}
   string explicitCombo=Regex.Replace(action,@"\s+(?:kombinasyonunu\s+)?(?:gonder|tuslarina bas|tusuna bas|bas)$","");
   if(TryCombo(explicitCombo,out mods,out key))return One(new ActionStep{Kind="Key",Mods=mods,Key=key});
   var common=new Dictionary<string,int>{{"tumunu sec",65},{"hepsini sec",65},{"kopyala",67},{"yapistir",86},{"kes",88},{"kaydet",83},{"dosyayi kaydet",83},{"geri al",90},{"yeniden yap",89},{"bul",70},{"arama ac",70}};
   if(common.TryGetValue(action,out key)){plan.Notes.Add("General edit actions use the application's known Ctrl shortcuts; they may be modified in the application.");return One(new ActionStep{Kind="Key",Mods=2,Key=key});}
   string exe=Normalize(Path.GetFileName(app));
   if(exe=="chrome.exe"||exe=="msedge.exe"||exe=="firefox.exe"){
    if(action=="yeni sekme ac")return Key(2,84,plan);
    if(action=="kapali sekmeyi ac"||action=="kapatilan sekmeyi ac")return Key(6,84,plan);
    if(action=="sekmeyi kapat")return Key(2,87,plan);
    if(action=="adres cubuguna git")return Key(2,76,plan);
   }
   if(exe=="code.exe"&&(action=="komut paletini ac"||action=="komut paleti ac"))return Key(6,80,plan);
   if(exe=="discord.exe"&&(action=="mikrofonu kapat"||action=="mikrofonu ac"||action=="mikrofonu ac/kapat")){plan.Notes.Add("Discord: Ctrl+Shift+M toggles the microphone; its current on/off state is not read.");return Key(6,77,plan);}
   var media=new Dictionary<string,int>{{"oynat duraklat",179},{"oynat/duraklat",179},{"sonraki parcaya gec",176},{"onceki parcaya gec",177},{"sesi artir",175},{"sesi azalt",174}};if(media.TryGetValue(action,out key))return One(new ActionStep{Kind="Key",Key=key});
   return null;
  }
  static List<ActionStep> Key(int mods,int key,PromptPlan plan){plan.Notes.Add("Known default shortcut will be used; application-specific key setting not verified.");return One(new ActionStep{Kind="Key",Mods=mods,Key=key});}
  static List<ActionStep> One(ActionStep step){return new List<ActionStep>{step};}
 }
}
