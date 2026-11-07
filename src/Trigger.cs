using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace AkilliKisayol {
 public sealed class TriggerSpec {
  static readonly string LegacyKeyPattern=@"(?:lbutton|rbutton|mbutton|xbutton[12]|wheel(?:up|down|left|right)|numpad[0-9]|num\s+[0-9]|num\s*[-+*/.]|subtract|add|multiply|divide|decimal|separator|numlock|scrolllock|capslock|oem[a-z0-9]+|f(?:2[0-4]|1[0-9]|[1-9])|enter|return|escape|esc|tab|space|bosluk|backspace|delete|del|insert|home|end|pageup|pagedown|up|down|left|right|yukari|asagi|sol|sag|[a-z0-9])";
  internal static readonly string KeyPattern=BuildPattern();
  static string BuildPattern(){var names=Enum.GetNames(typeof(System.Windows.Forms.Keys)).Where(n=>{var key=(int)(System.Windows.Forms.Keys)Enum.Parse(typeof(System.Windows.Forms.Keys),n);return key>0&&key<255&&!Native.Modifier(key);}).OrderByDescending(n=>n.Length).Select(n=>Regex.Escape(n.ToLowerInvariant()));return "(?:"+string.Join("|",names)+"|"+LegacyKeyPattern+")";}
  internal static string NormalizeKeys(string text){string clean=PromptParser.Normalize(text);clean=Regex.Replace(clean,@"\bnum(?:pad)?\s*(?<op>[-+*/.])",m=>m.Groups["op"].Value=="-"?"Subtract":m.Groups["op"].Value=="+"?"Add":m.Groups["op"].Value=="*"?"Multiply":m.Groups["op"].Value=="/"?"Divide":"Decimal",RegexOptions.IgnoreCase);return Regex.Replace(clean,@"\bnum\s+(?=\d\b)","numpad");}
  public int Mods,Key,Prefix;
  public static bool PrefixAllowed(int key){return key==46||key==45||(key>=112&&key<=135&&key!=123);}
  public static bool TryParse(string text,out TriggerSpec result){
   result=null;if(string.IsNullOrWhiteSpace(text))return false;
   string clean=NormalizeKeys(text).Trim().ToLowerInvariant();clean=Regex.Replace(clean,@"\b(ctrl|control|kontrol|alt|shift|win|windows)\s+(?=[^+\s])","$1+");var parts=clean.Split('+').Select(p=>p.Trim()).ToArray();
   int mods=0;var keys=new System.Collections.Generic.List<int>();foreach(string part in parts){int flag=part=="ctrl"||part=="control"||part=="kontrol"?2:part=="alt"?1:part=="shift"?4:part=="win"||part=="windows"?8:0;if(flag!=0){if((mods&flag)!=0)return false;mods|=flag;}else{int m,k;if(!PromptParser.TryCombo(part,out m,out k)||m!=0||Native.Modifier(k))return false;keys.Add(k);}}
   if(keys.Count<1||keys.Count>2||keys.Distinct().Count()!=keys.Count)return false;
   if(keys.Count==2&&!PrefixAllowed(keys[0]))return false;
   result=new TriggerSpec{Mods=mods,Key=keys.Last(),Prefix=keys.Count==2?keys[0]:0};return true;
  }
  public string Text {get{return Format(Mods,Key,Prefix);}}
  public static string Format(int mods,int key,int prefix=0){return ((mods&2)!=0?"Ctrl+":"")+((mods&1)!=0?"Alt+":"")+((mods&4)!=0?"Shift+":"")+((mods&8)!=0?"Win+":"")+(prefix>0?KeyName(prefix)+"+":"")+KeyName(key);}
  static string KeyName(int key){if(InputKeys.IsPointer(key))return InputKeys.Token(key);if(key==46)return "Del";if(key>=112&&key<=135)return "F"+(key-111);if(key>=48&&key<=57)return ((char)key).ToString();return ((System.Windows.Forms.Keys)key).ToString();}
  public static bool Mentioned(string prompt,TriggerSpec wanted){
   if(wanted.Prefix==0)return PromptParser.MentionsCombo(prompt,wanted.Mods,wanted.Key);
   string key=KeyPattern;
   foreach(Match match in Regex.Matches(PromptParser.Normalize(prompt),@"(?<![a-z0-9])(?:(?:ctrl|control|alt|shift|win|windows)\s*\+\s*)*"+key+@"\s*\+\s*"+key+@"(?![a-z0-9])")){TriggerSpec found;if(TryParse(match.Value,out found)&&found.Mods==wanted.Mods&&found.Key==wanted.Key&&found.Prefix==wanted.Prefix)return true;}return false;
  }
  public static string FindInPrompt(string prompt){
   string part=@"(?:(?:ctrl|control|kontrol|alt|shift|win|windows)\s*\+\s*)*"+KeyPattern+@"(?:\s*\+\s*"+KeyPattern+@")?";
   var match=Regex.Match(PromptParser.Normalize(prompt),@"\b(?:when\s+i\s+press|pressing|assign|bind|trigger(?:\s+key)?(?:\s+is)?)\s+(?<key>"+part+@")(?![a-z0-9])");TriggerSpec result;return match.Success&&TryParse(match.Groups["key"].Value,out result)?result.Text:"";
  }
 }
}
