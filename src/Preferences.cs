using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace AkilliKisayol {
 [DataContract] public sealed class AppPreference {
  [DataMember] public string Match="";
  [DataMember] public string AppPath="";
  public AppPreference Copy(){return new AppPreference{Match=Match??"",AppPath=AppPath??""};}
 }
 [DataContract] public sealed class UserPreferences {
  [DataMember] public string Browser="",MediaApp="";
  [DataMember] public bool LearnOnSave=true;
  [DataMember] public List<AppPreference> Rules=new List<AppPreference>();
  public UserPreferences Copy(){return new UserPreferences{Browser=Browser??"",MediaApp=MediaApp??"",LearnOnSave=LearnOnSave,Rules=(Rules??new List<AppPreference>()).Where(r=>r!=null).Select(r=>r.Copy()).ToList()};}
 }
 public static class PreferenceMemory {
  public static IEnumerable<string> Paths(UserPreferences p){if(p==null)return new string[0];return new[]{p.Browser,p.MediaApp}.Concat((p.Rules??new List<AppPreference>()).Where(r=>r!=null).Select(r=>r.AppPath)).Where(s=>!string.IsNullOrWhiteSpace(s)&&File.Exists(s)).Distinct(StringComparer.OrdinalIgnoreCase);}
  public static string Resolve(LlmRequest request){
   var p=request.Preferences;if(p==null)return "";string prompt=PromptParser.Normalize(request.Prompt??"");
   if(File.Exists(p.Browser)&&Regex.IsMatch(prompt,@"\b(?:in|using|with|on)\s+(?:the\s+)?(?:web\s+)?browser\b"))return p.Browser;
   // A default must never turn an explicitly named, unregistered destination into a different app.
   if(Regex.IsMatch(prompt,@"\b(?:in|using|with|on)\s+(?!this\b|the\b|a\b)([a-z0-9_-]+)\s+(?:app|browser)\b")||Regex.IsMatch(prompt,@"\b(?:in|using)\s+[a-z0-9_-]+\s+(?:when|browser|app)\b"))return "";
   foreach(var rule in (p.Rules??new List<AppPreference>()).Where(r=>r!=null&&!string.IsNullOrWhiteSpace(r.Match)).OrderByDescending(r=>r.Match.Length)){
    if(!File.Exists(rule.AppPath))continue;string match=PromptParser.Normalize(rule.Match.Trim());if(Regex.IsMatch(match,@"^[a-z0-9-]+(?:\.[a-z0-9-]+)+$")){Uri url;if(Uri.TryCreate(BrowserSupport.Website(request.Prompt),UriKind.Absolute,out url)&&(url.Host.Equals(match,StringComparison.OrdinalIgnoreCase)||url.Host.EndsWith("."+match,StringComparison.OrdinalIgnoreCase)))return rule.AppPath;}else if(Regex.IsMatch(prompt,@"(?<![a-z0-9])"+Regex.Escape(match)+@"(?![a-z0-9])"))return rule.AppPath;
   }
   if(File.Exists(p.MediaApp)&&Regex.IsMatch(prompt,@"\b(?:playlist|album|music|song|track)\b")&&!Regex.IsMatch(prompt,@"\b(?:in|using|with|on)\s+(?:the\s+)?browser\b"))return p.MediaApp;
   if(File.Exists(p.Browser)&&BrowserSupport.Website(request.Prompt).Length>0&&!Regex.IsMatch(prompt,@"\b(?:app|application|desktop)\b"))return p.Browser;return "";
  }
  public static bool Learn(UserPreferences preferences,Binding binding){
   if(preferences==null||!preferences.LearnOnSave||string.IsNullOrWhiteSpace(binding.Prompt)||binding.IsMacro)return false;
   string target=binding.Kind=="Browser website"?binding.BrowserPath:(binding.Steps??new List<ActionStep>()).Where(s=>s.Kind=="Open content").Select(s=>s.AppPath).Distinct(StringComparer.OrdinalIgnoreCase).SingleOrDefaultSafe();
   Uri url;string address=BrowserSupport.Website(binding.Prompt);if(string.IsNullOrWhiteSpace(target)||!Uri.TryCreate(address,UriKind.Absolute,out url))return false;
   if(preferences.Rules==null)preferences.Rules=new List<AppPreference>();var existing=preferences.Rules.FirstOrDefault(r=>string.Equals(r.Match,url.Host,StringComparison.OrdinalIgnoreCase));if(existing!=null&&Rules.SameApp(existing.AppPath,target))return false;
   if(existing==null)preferences.Rules.Add(new AppPreference{Match=url.Host,AppPath=target});else existing.AppPath=target;return true;
  }
  static string SingleOrDefaultSafe(this IEnumerable<string> items){var array=items.Where(s=>!string.IsNullOrWhiteSpace(s)).Take(2).ToArray();return array.Length==1?array[0]:null;}
 }
}
