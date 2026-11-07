using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AkilliKisayol {
 // Preserve explicit playback times even when a small model omits them.
 public static class LinkIntent {
  static readonly Regex Urls=new Regex(@"https?://[^\s\""'<>]+",RegexOptions.IgnoreCase);
  static string VideoId(Uri uri){
   string host=uri.Host.ToLowerInvariant();var parts=uri.AbsolutePath.Trim('/').Split('/');string id="";
   if(host=="youtu.be"&&parts.Length==1)id=parts[0];
   else if(new[]{"youtube.com","www.youtube.com","m.youtube.com","music.youtube.com","www.youtube-nocookie.com","youtube-nocookie.com"}.Contains(host)){
    if(parts.Length==2&&new[]{"shorts","embed","live"}.Contains(parts[0]))id=parts[1];
    else if(uri.AbsolutePath=="/watch")id=Query(uri).Where(x=>x.StartsWith("v=",StringComparison.Ordinal)).Select(x=>x.Substring(2)).FirstOrDefault()??"";
   }
   return Regex.IsMatch(id,@"^[A-Za-z0-9_-]{11}$")?id:"";
  }
  static string[] Query(Uri uri){return uri.Query.TrimStart('?').Split(new[]{'&'},StringSplitOptions.RemoveEmptyEntries);}
  static bool Seconds(string prompt,out int seconds){
   seconds=0;string text=PromptParser.Normalize(Urls.Replace(prompt??""," "));var found=new List<int>();
   foreach(Match clock in Regex.Matches(text,@"(?<![\d:])(?:(?<h>\d{1,3}):)?(?<m>\d{1,3}):(?<s>[0-5]\d)(?![\d:])")){
    int h=clock.Groups["h"].Success?int.Parse(clock.Groups["h"].Value):0,m=int.Parse(clock.Groups["m"].Value);if(h>0&&m>59)continue;found.Add(h*3600+m*60+int.Parse(clock.Groups["s"].Value));
   }
   string hUnit=@"(?<h>\d{1,4})(?:\.|st|nd|rd|th)?\s*(?:hours?|hrs?|h|saat)(?![a-z])",mUnit=@"(?<m>\d{1,4})(?:\.|st|nd|rd|th)?\s*(?:minutes?|mins?|m|dakika|dk)(?![a-z])",sUnit=@"(?<s>\d{1,4})(?:\.|st|nd|rd|th)?\s*(?:seconds?|secs?|s|saniye(?:sini|sinden|den|si)?|sn)(?![a-z])";
   foreach(Match units in Regex.Matches(text,@"(?<![\da-z])(?=\d)(?:"+hUnit+@"\s*(?:and\s*)?)?(?:"+mUnit+@"\s*(?:and\s*)?)?(?:"+sUnit+@")?(?![\da-z])")){
    if(!units.Groups["h"].Success&&!units.Groups["m"].Success&&!units.Groups["s"].Success)continue;
    // A delay instruction is not a playback position.
    string before=text.Substring(0,units.Index);if(Regex.IsMatch(before,@"\b(?:wait|delay|bekle)\s*$"))continue;
    int total=0;foreach(string unit in new[]{"h","m","s"})if(units.Groups[unit].Success)total+=int.Parse(units.Groups[unit].Value)*(unit=="h"?3600:unit=="m"?60:1);found.Add(total);
   }
   var distinct=found.Distinct().ToArray();if(distinct.Length!=1)return false;seconds=distinct[0];return true;
  }
  public static string Apply(string prompt,string resource){
   Uri destination;if(!Uri.TryCreate(resource,UriKind.Absolute,out destination))return resource;string video=VideoId(destination);if(video.Length==0)return resource;
   var links=Urls.Matches(prompt??"").Cast<Match>().Select(m=>m.Value.TrimEnd('.',',',';',')')).ToArray();int seconds;
   Uri source;if(links.Length!=1||!Uri.TryCreate(links[0],UriKind.Absolute,out source)||VideoId(source)!=video||!Seconds(prompt,out seconds))return resource;
   bool embed=source.AbsolutePath.StartsWith("/embed/",StringComparison.Ordinal);var query=Query(source).Where(x=>!Regex.IsMatch(x,@"^(?:t|start|time_continue)=",RegexOptions.IgnoreCase)).ToList();query.Add((embed?"start=":"t=")+seconds.ToString(CultureInfo.InvariantCulture));
   var builder=new UriBuilder(source){Query=string.Join("&",query)};if(Regex.IsMatch(builder.Fragment,@"^#t=",RegexOptions.IgnoreCase))builder.Fragment="";return builder.Uri.AbsoluteUri;
  }
  public static bool Repair(Binding binding){
   if(string.IsNullOrWhiteSpace(binding.Prompt))return false;bool changed=false;
   if(binding.Kind=="Browser website"||binding.Kind=="Web address"){string target=Apply(binding.Prompt,binding.Target);changed=target!=binding.Target;binding.Target=target;}
   if(binding.Steps!=null)foreach(var step in binding.Steps.Where(s=>s!=null&&s.Kind=="Open content")){string text=Apply(binding.Prompt,step.Text);changed|=text!=step.Text;step.Text=text;}
   return changed;
  }
 }
}
