using System;
using System.Collections.Generic;
namespace AkilliKisayol {
 // Old persisted action identifiers are converted without changing user text.
 public static class LegacyData {
  static readonly Dictionary<string,string> actions=new Dictionary<string,string>(StringComparer.Ordinal){
   {"Uygulama","Application"},{"İşlem dizisi","Workflow"},{"Tarayıcıda web","Browser website"},{"Tuş gönder","Send key"},{"Medya","Media"},{"Dosya / klasör","File / folder"},{"Web adresi","Web address"},
   {"Tuş","Key"},{"Metin","Text"},{"Bekle","Wait"},{"Tıkla","Click button"},{"Uygulama aç","Open application"},{"İçerik aç","Open content"},{"Odakla","Focus"},{"Fare tıkla","Mouse click"},{"Fare sürükle","Mouse drag"},{"Kaydır","Scroll"},{"Öğe tıkla","Click element"}
  };
  static readonly Dictionary<string,string> media=new Dictionary<string,string>{{"Oynat / duraklat","Play / pause"},{"Sonraki parça","Next track"},{"Önceki parça","Previous track"},{"Sesi artır","Volume up"},{"Sesi azalt","Volume down"},{"Sesi kapat / aç","Mute / unmute"}};
  public static void Normalize(State state){
   foreach(var binding in state.Bindings){string value;if(actions.TryGetValue(binding.Kind,out value))binding.Kind=value;if(binding.Kind=="Media"&&media.TryGetValue(binding.Target,out value))binding.Target=value;if(binding.Steps!=null)foreach(var step in binding.Steps)if(actions.TryGetValue(step.Kind??"",out value))step.Kind=value;}
   if(state.Profiles!=null)foreach(var profile in state.Profiles){if(profile==null)continue;if(profile.Id=="work"&&profile.Name=="İş")profile.Name="Work";if(profile.Id=="game"&&profile.Name=="Oyun")profile.Name="Game";if(profile.Id=="study"&&profile.Name=="Çalışma")profile.Name="Study";}
   if(state.Gestures==null)state.Gestures=new GestureOptions();var options=state.Gestures;options.Device=options.Device??"";options.PauseMilliseconds=Math.Max(300,Math.Min(1500,options.PauseMilliseconds));if(!ShapeRecognition.Finite(options.MinimumLength)||options.MinimumLength<40||options.MinimumLength>4000)options.MinimumLength=120;if(!ShapeRecognition.Finite(options.MinimumScore)||options.MinimumScore<0.75||options.MinimumScore>0.99)options.MinimumScore=0.88;if(!ShapeRecognition.Finite(options.MinimumMargin)||options.MinimumMargin<0.02||options.MinimumMargin>0.3)options.MinimumMargin=0.06;
  }
 }
}
