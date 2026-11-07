using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace AkilliKisayol {
 [DataContract] public sealed class ShortcutProfile {
  [DataMember] public string Id {get;set;}
  [DataMember] public string Name {get;set;}
  [DataMember] public bool SuppressTypingKeys {get;set;}
  public ShortcutProfile Copy(){return (ShortcutProfile)MemberwiseClone();}
 }
 public static class Profiles {
  public static List<ShortcutProfile> Defaults(){return new List<ShortcutProfile>{new ShortcutProfile{Id="work",Name="Work"},new ShortcutProfile{Id="game",Name="Game",SuppressTypingKeys=true},new ShortcutProfile{Id="study",Name="Study"}};}
  public static void Normalize(State state){
   if(state.Profiles==null||state.Profiles.Count==0)state.Profiles=Defaults();
   if(state.Profiles.Any(p=>p==null||string.IsNullOrWhiteSpace(p.Id)||string.IsNullOrWhiteSpace(p.Name))||state.Profiles.Select(p=>p.Id).Distinct().Count()!=state.Profiles.Count)throw new System.IO.InvalidDataException("Profile list is invalid.");
   if(!state.Profiles.Any(p=>p.Id==state.ActiveProfileId))state.ActiveProfileId=state.Profiles[0].Id;
   foreach(var binding in state.Bindings){if(binding.ProfileId==null)binding.ProfileId="";if(binding.ProfileId.Length>0&&!state.Profiles.Any(p=>p.Id==binding.ProfileId))binding.ProfileId="";}
  }
  public static bool Overlap(Binding a,Binding b){return string.IsNullOrEmpty(a.ProfileId)||string.IsNullOrEmpty(b.ProfileId)||a.ProfileId==b.ProfileId;}
  public static bool Active(Binding b,ShortcutProfile profile){return (string.IsNullOrEmpty(b.ProfileId)||profile!=null&&b.ProfileId==profile.Id)&&!(profile!=null&&profile.SuppressTypingKeys&&b.Gesture==null&&b.AppPath.Length==0&&b.Mods==0&&b.PrefixKey==0&&b.Key<112&&!InputKeys.IsPointer(b.Key));}
  public static List<Binding> Effective(State state){var profile=state.Profiles.FirstOrDefault(p=>p.Id==state.ActiveProfileId);return state.Bindings.Where(b=>Active(b,profile)).OrderByDescending(b=>!string.IsNullOrEmpty(b.ProfileId)).ToList();}
  public static string Name(State state){var profile=state.Profiles.FirstOrDefault(p=>p.Id==state.ActiveProfileId);return profile==null?"":profile.Name;}
  public static string Describe(Binding binding,IEnumerable<ShortcutProfile> profiles,string activeId,bool paused){
   var list=profiles.ToList();var active=list.FirstOrDefault(p=>p.Id==activeId);string group=string.IsNullOrEmpty(binding.ProfileId)?"All profiles":list.Where(p=>p.Id==binding.ProfileId).Select(p=>p.Name+" profile").FirstOrDefault()??"Unknown profile";
   var reasons=new List<string>();if(!binding.Enabled)reasons.Add("Disabled");if(!string.IsNullOrEmpty(binding.ProfileId)&&binding.ProfileId!=activeId)reasons.Add("Outside active profile (active: "+(active==null?activeId:active.Name)+")");
   else if(!Active(binding,active))reasons.Add("General keyboard shortcuts are disabled in the game profile; add a modifier or select an application scope");
   if(paused)reasons.Add("Shortcut engine paused");return (reasons.Count==0?"Enabled":string.Join(" • ",reasons))+" • "+group;
  }
 }
}
