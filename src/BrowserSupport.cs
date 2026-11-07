using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AkilliKisayol {
 public static class BrowserSupport {
  public static bool IsBrowser(string path){return !string.IsNullOrWhiteSpace(path)&&new[]{"brave.exe","chrome.exe","msedge.exe","firefox.exe","opera.exe"}.Contains(Path.GetFileName(path).ToLowerInvariant());}
  public static string DisplayName(string path){switch(Path.GetFileName(path).ToLowerInvariant()){case "brave.exe":return "Brave";case "chrome.exe":return "Google Chrome";case "msedge.exe":return "Microsoft Edge";case "firefox.exe":return "Firefox";case "code.exe":return "Visual Studio Code";default:return AppLibrary.Name(path);}}
  public static IEnumerable<string> MentionedApps(LlmRequest request){
   string prompt=PromptParser.Normalize(Regex.Replace(request.Prompt??"",@"\b(?:https?://|[a-z][a-z0-9+.-]*://)[^\s]+"," ",RegexOptions.IgnoreCase));return (request.Applications??new List<string>()).Where(path=>{
    string name=PromptParser.Normalize(AppLibrary.Name(path)),display=PromptParser.Normalize(DisplayName(path));return new[]{name,display}.Any(alias=>alias.Length>1&&Regex.IsMatch(prompt,@"(?<![a-z0-9])"+Regex.Escape(alias)+@"(?![a-z0-9])"));
   }).Distinct(StringComparer.OrdinalIgnoreCase);
  }
  public static string Target(LlmRequest request){if(!string.IsNullOrWhiteSpace(request.TargetApp))return request.TargetApp;var matches=MentionedApps(request).Where(IsBrowser).ToArray();return matches.Length==1?matches[0]:"";}
  public static string Website(string prompt){
   var explicitUrl=Regex.Match(prompt,@"https?://[^\s\""'<>]+",RegexOptions.IgnoreCase);if(explicitUrl.Success)return LinkIntent.Apply(prompt,NormalizeUrl(explicitUrl.Value.TrimEnd('.',',',';',')')));
   var domain=Regex.Match(prompt,@"\b(?:www\.)?[a-z0-9][a-z0-9.-]+\.(?:com|org|net|io|dev|gov|edu)(?:/[^\s\""'<>]*)?",RegexOptions.IgnoreCase);if(domain.Success)return NormalizeUrl("https://"+domain.Value.TrimEnd('.',',',';',')'));
   foreach(var site in new Dictionary<string,string>{{"youtube","https://www.youtube.com/"},{"github","https://github.com/"},{"wikipedia","https://www.wikipedia.org/"},{"google","https://www.google.com/"}})if(Regex.IsMatch(prompt,@"\b"+site.Key+@"\b",RegexOptions.IgnoreCase))return site.Value;
   return "";
  }
  public static string NormalizeUrl(string text){Uri uri;if(!Uri.TryCreate(text,UriKind.Absolute,out uri)||(uri.Scheme!="http"&&uri.Scheme!="https")||uri.UserInfo.Length>0)throw new ArgumentException("A valid HTTP(S) web address is required.");return uri.AbsoluteUri;}
  public static ProcessStartInfo StartInfo(Binding binding){string error=Rules.Validate(binding,new Binding[0]);if(error!=null)throw new ArgumentException(error);return new ProcessStartInfo(binding.BrowserPath,LocalLlm.Quote(NormalizeUrl(binding.Target))){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(binding.BrowserPath)};}
 }
}
