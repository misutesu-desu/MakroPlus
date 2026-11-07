using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Text;

namespace AkilliKisayol {
 public static class ModelFiles {
  public static string DefaultDirectory {get{return DirectoryFor(AppDomain.CurrentDomain.BaseDirectory);}}
  internal static string DirectoryFor(string root){return Path.Combine(root,"models");}
  internal static string[] Candidates(string root){try{string directory=DirectoryFor(root);return Directory.Exists(directory)?Directory.GetFiles(directory,"*.gguf").OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).ToArray():new string[0];}catch(IOException){return new string[0];}catch(UnauthorizedAccessException){return new string[0];}}
  public static string Portable(string file){return Portable(file,AppDomain.CurrentDomain.BaseDirectory);}
  internal static string Portable(string file,string root){if(string.IsNullOrWhiteSpace(file))return "";string absolute=Path.GetFullPath(Path.IsPathRooted(file)?file:Path.Combine(root,file));string directory=Path.GetFullPath(DirectoryFor(root))+Path.DirectorySeparatorChar;return absolute.StartsWith(directory,StringComparison.OrdinalIgnoreCase)?Path.Combine("models",absolute.Substring(directory.Length)):absolute;}
  public static string Resolve(string preferred){return Resolve(preferred,AppDomain.CurrentDomain.BaseDirectory,Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));}
  internal static string Resolve(string preferred,string root,string user){
   if(!string.IsNullOrWhiteSpace(preferred))return Path.IsPathRooted(preferred)?preferred:Path.Combine(root,preferred);
   var files=Candidates(root);if(files.Length==1)return files[0];
   return "";
  }
 }
 public sealed class ImportResult {public State State;public int Added,Disabled;public readonly List<string> Issues=new List<string>();}
 public static class Transfer {
  public static State Clone(State state){using(var stream=new MemoryStream()){var serializer=new DataContractJsonSerializer(typeof(State));serializer.WriteObject(stream,state);stream.Position=0;return (State)serializer.ReadObject(stream);}}
  public static void Export(State state,string file){var copy=Clone(state);copy.History.Clear();if(copy.ModelOptions!=null)copy.ModelOptions.ProtectedKey=null;Storage.Write(copy,file);}
  public static List<string> Paths(State state){return state.ApplicationPaths.Concat(state.Bindings.SelectMany(b=>new[]{b.AppPath,b.Kind=="Application"?b.Target:"",b.BrowserPath??""}.Concat((b.Steps??new List<ActionStep>()).Select(s=>s.AppPath??"")))).Concat(new[]{state.Preferences.Browser,state.Preferences.MediaApp}).Concat(state.Preferences.Rules.Select(r=>r.AppPath)).Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p=>p).ToList();}
  static string PathMap(string path,IDictionary<string,string> map){if(string.IsNullOrEmpty(path))return path;string replacement;return map.TryGetValue(path,out replacement)&&!string.IsNullOrWhiteSpace(replacement)?replacement:path;}
  public static ImportResult Import(State current,State incoming,IDictionary<string,string> paths,bool enable){
   if(incoming.Bindings.Count>5000)throw new InvalidDataException("In a transfer, up to 5000 assignments can be imported.");var result=new ImportResult{State=Clone(current)};var state=result.State;var profileMap=new Dictionary<string,string>();
   foreach(var profile in incoming.Profiles){var existing=state.Profiles.FirstOrDefault(p=>p.Id==profile.Id);string id=profile.Id;if(existing==null)state.Profiles.Add(profile.Copy());else if(existing.Name!=profile.Name){var copy=profile.Copy();copy.Id=Guid.NewGuid().ToString("N");id=copy.Id;state.Profiles.Add(copy);}profileMap[profile.Id]=id;}
   if(current.Bindings.Count==0){state.Gestures=Clone(incoming).Gestures;state.AllowRemappedInput=incoming.AllowRemappedInput;state.Dark=incoming.Dark;state.ThemeId=ThemeCatalog.Resolve(incoming).Id;state.UseGpu=incoming.UseGpu;state.StepIntervalMs=incoming.StepIntervalMs;state.ModelOptions=(incoming.ModelOptions??new ModelOptions()).Copy();state.ModelOptions.ProtectedKey=null;state.ModelPath=incoming.ModelPath;state.GuideEnabled=incoming.GuideEnabled;state.GuideHoldKey=incoming.GuideHoldKey;state.ActiveProfileId=profileMap[incoming.ActiveProfileId];}
   ThemeFiles.Merge(state,incoming);ThemeCatalog.Normalize(state);
   foreach(var source in incoming.Bindings){var b=source.Copy();if(state.Bindings.Any(x=>x.Id==b.Id))b.Id=Guid.NewGuid().ToString();if(!string.IsNullOrEmpty(b.ProfileId))b.ProfileId=profileMap[b.ProfileId];b.AppPath=PathMap(b.AppPath,paths);b.BrowserPath=PathMap(b.BrowserPath,paths);if(b.Kind=="Application")b.Target=PathMap(b.Target,paths);if(b.Steps!=null)foreach(var step in b.Steps)step.AppPath=PathMap(step.AppPath,paths);
    b.Enabled=enable&&source.Enabled;string error=Rules.Validate(b,state.Bindings);if(error!=null){b.Enabled=false;result.Issues.Add(b.Name+": "+error);}if(!b.Enabled)result.Disabled++;state.Bindings.Add(b);result.Added++;
   }
   foreach(string path in Paths(incoming).Select(p=>PathMap(p,paths)))if(File.Exists(path))AppLibrary.Remember(state.ApplicationPaths,path);
   if(string.IsNullOrEmpty(state.Preferences.Browser))state.Preferences.Browser=PathMap(incoming.Preferences.Browser,paths);if(string.IsNullOrEmpty(state.Preferences.MediaApp))state.Preferences.MediaApp=PathMap(incoming.Preferences.MediaApp,paths);
   foreach(var rule in incoming.Preferences.Rules){var copy=rule.Copy();copy.AppPath=PathMap(copy.AppPath,paths);if(!state.Preferences.Rules.Any(r=>r.Match==copy.Match))state.Preferences.Rules.Add(copy);}
   return result;
  }
 }
 public static class Diagnostics {
  [StructLayout(LayoutKind.Sequential)]struct MemoryInfo{public uint Length,Load;public ulong TotalPhysical,AvailablePhysical,TotalPage,AvailablePage,TotalVirtual,AvailableVirtual,AvailableExtended;}
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool GlobalMemoryStatusEx(ref MemoryInfo memory);
  public static string Report(State state){var memory=new MemoryInfo{Length=(uint)Marshal.SizeOf(typeof(MemoryInfo))};GlobalMemoryStatusEx(ref memory);var lines=new List<string>{Brand.Name+" "+Brand.Version,Environment.OSVersion+" • "+(Environment.Is64BitOperatingSystem?"64 bit":"32 bit"),"Processor core: "+Environment.ProcessorCount,"RAM: "+(memory.TotalPhysical/1073741824.0).ToString("F1")+" GB • empty: "+(memory.AvailablePhysical/1073741824.0).ToString("F1")+" GB","Screen workspace: "+System.Windows.SystemParameters.WorkArea.Width+" × "+System.Windows.SystemParameters.WorkArea.Height,"Model: "+ModelFiles.Resolve(state.ModelPath),"Model available: "+File.Exists(ModelFiles.Resolve(state.ModelPath)),"Shortcuts: "+state.Bindings.Count+" • enabled: "+Profiles.Effective(state).Count(b=>b.Enabled),"Profile: "+Profiles.Name(state)};
   var devices=RawPointerSource.Devices();lines.Add("Windows input devices: "+devices.Count(d=>d.Type=="Keyboard")+" keyboards, "+devices.Count(d=>d.Type=="Pointer")+" pointers, "+devices.Count(d=>d.Type=="HID")+" other HID devices.");lines.Add("Input support: standard Windows virtual keys, 5 mouse buttons, high-resolution wheels and calibrated touchpad pointer shapes. Firmware-only/vendor-specific signals depend on the driver.");
   try{lines.Add("Model runtime: "+LocalLlm.FindRuntime());}catch(Exception error){lines.Add(error.Message);}
   foreach(var b in state.Bindings){string issue=Rules.Validate(b,state.Bindings);if(issue!=null)lines.Add(b.Name+" • "+issue);}return string.Join(Environment.NewLine,lines);
  }
 }
}
