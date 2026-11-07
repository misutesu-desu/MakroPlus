using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AkilliKisayol {
 public static class ShortcutKeys {
  static readonly Dictionary<int,string> Names=new Dictionary<int,string>{{106,"numpad_multiply"},{107,"numpad_add"},{109,"numpad_subtract"},{110,"numpad_decimal"},{111,"numpad_divide"},{186,"oem_1"},{187,"oem_plus"},{188,"oem_comma"},{189,"oem_minus"},{190,"oem_period"},{191,"oem_2"},{192,"oem_3"},{219,"oem_4"},{220,"oem_5"},{221,"oem_6"},{222,"oem_7"},{223,"oem_8"},{226,"oem_102"}};
  public static string Code(int mods,int key){string name;return ((mods&2)!=0?"ctrl+":"")+((mods&1)!=0?"alt+":"")+((mods&4)!=0?"shift+":"")+((mods&8)!=0?"win+":"")+(Names.TryGetValue(key,out name)?name:TriggerSpec.Format(0,key).ToLowerInvariant());}
  public static bool Comparable(string value){return !string.IsNullOrWhiteSpace(value)&&Regex.Split(value.Trim(),@"\s+").All(s=>{int m,k;return TryCode(s,out m,out k);});}
  public static bool TryCode(string input,out int mods,out int key){
   string text=input.Trim().ToLowerInvariant();if(text=="+"||text.EndsWith("++"))text=text.Substring(0,text.Length-1)+"oem_plus";
   int split=text.LastIndexOf('+');string name=text.Substring(split+1);var pair=Names.FirstOrDefault(p=>p.Value==name);
   var symbols=new Dictionary<string,int>{{";",186},{"=",187},{",",188},{"-",189},{".",190},{"/",191},{"`",192},{"[",219},{"\\",220},{"]",221},{"'",222}};
   int code=pair.Key;if(code==0)symbols.TryGetValue(name,out code);if(code>0)text=text.Substring(0,split+1)+((System.Windows.Forms.Keys)code).ToString();
   return PromptParser.TryCombo(text,out mods,out key);
  }
 }
}
