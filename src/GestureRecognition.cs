using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace AkilliKisayol {
 [DataContract] public sealed class GesturePoint {
  [DataMember]public double X,Y;
  public GesturePoint(){}public GesturePoint(double x,double y){X=x;Y=y;}
 }
 [DataContract] public sealed class ShapeTemplate {
  [DataMember]public List<GesturePoint> Points=new List<GesturePoint>();
  public ShapeTemplate Copy(){return new ShapeTemplate{Points=(Points??new List<GesturePoint>()).Where(p=>p!=null).Take(513).Select(p=>new GesturePoint(p.X,p.Y)).ToList()};}
 }
 [DataContract] public sealed class GestureOptions {
  [DataMember]public bool Enabled=true;
  [DataMember]public string Device="";
  [DataMember]public int PauseMilliseconds=600;
  [DataMember]public double MinimumLength=120,MinimumScore=0.88,MinimumMargin=0.06;
 }
 public sealed class ShapeMatch {
  public Binding Binding;public double Score,Margin;public string Reason="No matching shape.";
 }
 internal sealed class PreparedShape {
  public Binding Binding;public List<GesturePoint> Normalized;
  public PreparedShape(Binding binding){Binding=binding;Normalized=ShapeRecognition.Validate(binding.Gesture)==null?ShapeRecognition.Normalize(binding.Gesture.Points):new List<GesturePoint>();}
 }
 public static class ShapeRecognition {
  public static bool Finite(double value){return !double.IsNaN(value)&&!double.IsInfinity(value);}
  public static double Distance(GesturePoint a,GesturePoint b){double x=a.X-b.X,y=a.Y-b.Y;return Math.Sqrt(x*x+y*y);}
  public static double Length(IList<GesturePoint> points){double sum=0;for(int i=1;i<points.Count;i++)sum+=Distance(points[i-1],points[i]);return sum;}
  public static string Validate(ShapeTemplate template){if(template==null||template.Points==null||template.Points.Count<8||template.Points.Count>512||template.Points.Any(p=>p==null||!Finite(p.X)||!Finite(p.Y)||Math.Abs(p.X)>10000000||Math.Abs(p.Y)>10000000))return "Record a valid shape with 8–512 points.";if(Length(template.Points)<20)return "The shape is too small. Draw a larger shape.";return null;}
  public static List<GesturePoint> Normalize(IList<GesturePoint> source){
   if(source==null)return new List<GesturePoint>();
   var clean=new List<GesturePoint>();foreach(var p in source){if(p==null||!Finite(p.X)||!Finite(p.Y))return new List<GesturePoint>();if(clean.Count==0||Distance(clean[clean.Count-1],p)>0.00001)clean.Add(p);}
   if(clean.Count<2)return new List<GesturePoint>();double length=Length(clean);if(length<0.0001)return new List<GesturePoint>();var result=new List<GesturePoint>();int segment=1;double passed=0;
   for(int i=0;i<64;i++){double wanted=length*i/63;while(segment<clean.Count-1&&passed+Distance(clean[segment-1],clean[segment])<wanted){passed+=Distance(clean[segment-1],clean[segment]);segment++;}double span=Distance(clean[segment-1],clean[segment]),t=span>0?(wanted-passed)/span:0;result.Add(new GesturePoint(clean[segment-1].X+(clean[segment].X-clean[segment-1].X)*t,clean[segment-1].Y+(clean[segment].Y-clean[segment-1].Y)*t));}
   double minX=result.Min(p=>p.X),minY=result.Min(p=>p.Y),size=Math.Max(result.Max(p=>p.X)-minX,result.Max(p=>p.Y)-minY);if(size<0.0001)return new List<GesturePoint>();double cx=result.Average(p=>p.X),cy=result.Average(p=>p.Y);return result.Select(p=>new GesturePoint((p.X-cx)/size,(p.Y-cy)/size)).ToList();
  }
  static double Error(IList<GesturePoint> a,IList<GesturePoint> b,bool reverse,int shift,double angle,bool closed){double sum=0,c=Math.Cos(angle),s=Math.Sin(angle);int count=closed?63:64;for(int i=0;i<count;i++){int index=closed?(i+shift)%count:i;if(reverse)index=count-1-index;double x=a[index].X*c-a[index].Y*s,y=a[index].X*s+a[index].Y*c;double dx=x-b[i].X,dy=y-b[i].Y;sum+=Math.Sqrt(dx*dx+dy*dy);}return sum/count;}
  public static double Similarity(IList<GesturePoint> first,IList<GesturePoint> second){return SimilarityNormalized(Normalize(first),Normalize(second));}
  internal static double SimilarityNormalized(IList<GesturePoint> a,IList<GesturePoint> b){if(a.Count!=64||b.Count!=64)return 0;bool closed=Distance(a[0],a[63])<0.23&&Distance(b[0],b[63])<0.23;double best=double.MaxValue;for(int degrees=-12;degrees<=12;degrees+=6){double angle=degrees*Math.PI/180;if(closed){for(int shift=0;shift<63;shift+=3){best=Math.Min(best,Error(a,b,false,shift,angle,true));best=Math.Min(best,Error(a,b,true,shift,angle,true));}}else best=Math.Min(best,Error(a,b,false,0,angle,false));}return Math.Max(0,1-best/0.70710678118);}
  public static ShapeMatch Match(IList<GesturePoint> stroke,IEnumerable<Binding> candidates,GestureOptions options){
   return MatchPrepared(stroke,candidates.Where(b=>b.Enabled&&b.Gesture!=null).Select(b=>new PreparedShape(b)),options);
  }
  internal static ShapeMatch MatchPrepared(IList<GesturePoint> stroke,IEnumerable<PreparedShape> candidates,GestureOptions options){var result=new ShapeMatch();if(stroke==null||stroke.Count<8||stroke.Count>512||Length(stroke)<options.MinimumLength){result.Reason="Movement is too short or invalid.";return result;}var normalized=Normalize(stroke);if(normalized.Count!=64)return result;PreparedShape best=null;double first=-1,second=-1;foreach(var candidate in candidates){if(!candidate.Binding.Enabled||candidate.Normalized.Count!=64)continue;double score=SimilarityNormalized(normalized,candidate.Normalized);if(score>first){second=first;first=score;best=candidate;}else if(score>second)second=score;}if(best==null)return result;result.Score=first;result.Margin=second<0?1:first-second;if(result.Score<options.MinimumScore){result.Reason="No confident match.";return result;}if(result.Margin<options.MinimumMargin){result.Reason="The shape is ambiguous; no shortcut ran.";return result;}result.Binding=best.Binding;result.Reason="Matched: "+best.Binding.Name;return result;}
  public static List<string> Conflicts(Binding binding,IEnumerable<Binding> others){var messages=new List<string>();if(binding.Gesture==null||Validate(binding.Gesture)!=null)return messages;foreach(var other in others.Where(b=>b.Id!=binding.Id&&b.Enabled&&binding.Enabled&&b.Gesture!=null&&Profiles.Overlap(b,binding)&&Validate(b.Gesture)==null)){if(binding.AppPath.Length>0&&other.AppPath.Length>0&&!Rules.SameApp(binding.AppPath,other.AppPath))continue;double score=Similarity(binding.Gesture.Points,other.Gesture.Points);if(score>=0.92)messages.Add((Rules.SameApp(binding.AppPath,other.AppPath)&&binding.ProfileId==other.ProfileId?"CONFLICT":"PRIORITY")+" • Similar shape: "+other.Name+" ("+Math.Round(score*100)+"%).");}return messages;}
 }
}
