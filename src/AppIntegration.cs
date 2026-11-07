using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using Microsoft.Win32;

namespace AkilliKisayol {
 public sealed class AppDescriptor {
  public string Path,Name,PackageId="";
  public string[] Protocols=new string[0];
  public Dictionary<string,string> ProtocolArguments=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
 }
 public static class AppIntegration {
  static readonly Dictionary<string,AppDescriptor> cache=new Dictionary<string,AppDescriptor>(StringComparer.OrdinalIgnoreCase);
  public static AppDescriptor Describe(string path){
   lock(cache){AppDescriptor found;if(cache.TryGetValue(path,out found))return found;
    var info=new AppDescriptor{Path=path,Name=BrowserSupport.DisplayName(path)};var protocols=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    try{
     var dir=new DirectoryInfo(System.IO.Path.GetDirectoryName(path));
     for(int depth=0;dir!=null&&depth<5;depth++,dir=dir.Parent){
      string manifest=System.IO.Path.Combine(dir.FullName,"AppxManifest.xml");if(!File.Exists(manifest))continue;
      var xml=new XmlDocument{XmlResolver=null};xml.Load(manifest);
      var identity=xml.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Identity']");
      foreach(XmlNode app in xml.SelectNodes("//*[local-name()='Applications']/*[local-name()='Application']")){
       var exe=app.Attributes["Executable"];bool matches=exe!=null&&Rules.SameApp(System.IO.Path.GetFullPath(System.IO.Path.Combine(dir.FullName,exe.Value)),System.IO.Path.GetFullPath(path));
       // Desktop Bridge packages may launch through a migrator; the real executable is registered in an extension.
       if(!matches)foreach(XmlNode extension in app.SelectNodes(".//*[@Executable]"))if(Rules.SameApp(System.IO.Path.GetFullPath(System.IO.Path.Combine(dir.FullName,extension.Attributes["Executable"].Value)),System.IO.Path.GetFullPath(path))){matches=true;break;}
       if(!matches)continue;
       int split=dir.Name.LastIndexOf('_');if(identity!=null&&split>=0&&app.Attributes["Id"]!=null)info.PackageId=identity.Attributes["Name"].Value+"_"+dir.Name.Substring(split+1)+"!"+app.Attributes["Id"].Value;
       foreach(XmlNode protocol in app.SelectNodes(".//*[local-name()='Protocol']"))if(protocol.Attributes["Name"]!=null){string scheme=protocol.Attributes["Name"].Value;protocols.Add(scheme);if(protocol.Attributes["Parameters"]!=null)info.ProtocolArguments[scheme]=protocol.Attributes["Parameters"].Value;}
       break;
      }break;
     }
     // Desktop URI handlers are discovered from Windows, without a program-name allowlist.
     using(var classes=RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot,RegistryView.Default))foreach(string scheme in classes.GetSubKeyNames()){
      if(scheme.Contains('.')||scheme.Contains('\\'))continue;
      using(var key=classes.OpenSubKey(scheme)){if(key==null||key.GetValue("URL Protocol")==null)continue;
       using(var command=key.OpenSubKey(@"shell\open\command")){string value=command==null?"":Convert.ToString(command.GetValue(""));int index=value.IndexOf(path,StringComparison.OrdinalIgnoreCase);if(index>=0){protocols.Add(scheme);int end=index+path.Length;if(end<value.Length&&value[end]=='"')end++;string args=value.Substring(end).Trim();if(args.Contains("%1"))info.ProtocolArguments[scheme]=args;}}
      }
     }
    }catch(Exception e){if(!(e is IOException)&&!(e is UnauthorizedAccessException)&&!(e is System.Security.SecurityException)&&!(e is XmlException)&&!(e is ArgumentException))throw;}
    info.Protocols=protocols.OrderBy(p=>p).ToArray();cache[path]=info;return info;
   }
  }
  public static string Target(LlmRequest request){if(!string.IsNullOrWhiteSpace(request.TargetApp))return request.TargetApp;var matches=BrowserSupport.MentionedApps(request).ToArray();return matches.Length==1?matches[0]:matches.Length>1?"":PreferenceMemory.Resolve(request);}
  public static string Resource(string path,string resource){
   resource=(resource??"").Trim();
   // Canonicalize the existing Spotify web adapter before dispatching through the common launcher.
   if(Path.GetFileName(path).Equals("spotify.exe",StringComparison.OrdinalIgnoreCase))try{return Rules.NormalizeSpotify(resource);}catch(ArgumentException){}
   return resource;
  }
  public static string Quote(string value){
   var result=new StringBuilder("\"");int slashes=0;foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='"')result.Append('\\',slashes*2+1);else result.Append('\\',slashes);result.Append(c);slashes=0;}return result.Append('\\',slashes*2).Append('"').ToString();
  }
  internal static ProcessStartInfo StartInfo(string path,string resource,string arguments=""){
   if(!File.Exists(path)||!path.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))throw new FileNotFoundException("Registered application not found.",path);
   string value=Resource(path,resource);return new ProcessStartInfo(path){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(path),Arguments=string.IsNullOrEmpty(value)?arguments:Quote(value)};
  }
  public static void Open(string path,string resource="",string arguments=""){
   var descriptor=Describe(path);string value=Resource(path,resource);
   if(descriptor.PackageId.Length==0){Uri address;string template;if(Uri.TryCreate(value,UriKind.Absolute,out address)&&descriptor.ProtocolArguments.TryGetValue(address.Scheme,out template)){arguments=ProtocolArgs(template,value);value="";}using(var process=Process.Start(StartInfo(path,value,arguments))){}return;}
   object instance=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")));IntPtr item=IntPtr.Zero,array=IntPtr.Zero;
   try{
    var manager=(IApplicationActivationManager)instance;uint pid;Uri uri;
    if(value.Length>0&&Uri.TryCreate(value,UriKind.Absolute,out uri)&&descriptor.Protocols.Contains(uri.Scheme,StringComparer.OrdinalIgnoreCase)){
     string template;if(descriptor.ProtocolArguments.TryGetValue(uri.Scheme,out template)){
      Marshal.ThrowExceptionForHR(manager.ActivateApplication(descriptor.PackageId,ProtocolArgs(template,value),0,out pid));
     }else{
      Guid itemId=new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),arrayId=new Guid("B63EA76D-1F85-456F-A19C-48159EFA858B");
      Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(value,IntPtr.Zero,ref itemId,out item));Marshal.ThrowExceptionForHR(SHCreateShellItemArrayFromShellItem(item,ref arrayId,out array));Marshal.ThrowExceptionForHR(manager.ActivateForProtocol(descriptor.PackageId,array,out pid));
     }
    }else Marshal.ThrowExceptionForHR(manager.ActivateApplication(descriptor.PackageId,value.Length>0?Quote(value):arguments,0,out pid));
   }finally{if(array!=IntPtr.Zero)Marshal.Release(array);if(item!=IntPtr.Zero)Marshal.Release(item);Marshal.FinalReleaseComObject(instance);}
  }
  static string ProtocolArgs(string template,string value){return template.Replace("\"%1\"",Quote(value)).Replace("%1",Quote(value));}
  [ComImport,Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  interface IApplicationActivationManager {
   [PreserveSig]int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)]string id,[MarshalAs(UnmanagedType.LPWStr)]string arguments,uint options,out uint pid);
   [PreserveSig]int ActivateForFile([MarshalAs(UnmanagedType.LPWStr)]string id,IntPtr items,[MarshalAs(UnmanagedType.LPWStr)]string verb,out uint pid);
   [PreserveSig]int ActivateForProtocol([MarshalAs(UnmanagedType.LPWStr)]string id,IntPtr items,out uint pid);
  }
  [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=true)]static extern int SHCreateItemFromParsingName(string path,IntPtr context,ref Guid id,out IntPtr item);
  [DllImport("shell32.dll",PreserveSig=true)]static extern int SHCreateShellItemArrayFromShellItem(IntPtr item,ref Guid id,out IntPtr items);
 }
}
