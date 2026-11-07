using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace AkilliKisayol {
 public static class AppLibrary {
  public static string Name(string path){return Path.GetFileNameWithoutExtension(path);}
  public static bool Remember(List<string> paths,string path){
   if(string.IsNullOrWhiteSpace(path)||!Path.IsPathRooted(path)||!path.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))return false;
   path=Path.GetFullPath(path);
   if(paths.Any(p=>Rules.SameApp(p,path)))return false;
   paths.Add(path);return true;
  }
  public static Binding Draft(string path,bool launch){return new Binding{Name=Name(path)+(launch?" open":" shortcut"),AppPath=launch?"":path,Kind=launch?"Application":"Send key",Target=launch?path:""};}
  public static List<string> Running(){
   var paths=new List<string>();foreach(var process in Process.GetProcesses())using(process){try{if(process.MainWindowHandle==IntPtr.Zero)continue;string path=Native.ProcessPath((uint)process.Id);if(path.Length>0)Remember(paths,path);}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}}
   return paths.OrderBy(Name,StringComparer.CurrentCultureIgnoreCase).ToList();
  }
 }
 public sealed class AppPicker : Window {
  readonly StackPanel items=new StackPanel();readonly TextBlock note=Theme.Text("",12,"Muted");
  readonly Dictionary<string,CheckBox> choices=new Dictionary<string,CheckBox>(StringComparer.OrdinalIgnoreCase);
  readonly HashSet<string> saved;
  public List<string> SelectedPaths=new List<string>();
  public AppPicker(IEnumerable<string> existing){
   saved=new HashSet<string>(existing,StringComparer.OrdinalIgnoreCase);Style=(Style)Application.Current.FindResource(typeof(Window));Title="Add application";Width=730;Height=650;MinWidth=600;MinHeight=480;WindowStartupLocation=WindowStartupLocation.CenterOwner;
   var dock=new DockPanel{Margin=new Thickness(26)};Content=dock;
   var header=new StackPanel();header.Children.Add(Theme.Text("Quickly add your applications",23));header.Children.Add(Theme.Text("Applications do not need to be open. Choose their .exe files, or select from running applications below. Registered applications stay available when closed.",13,"Muted"));
   var bar=new WrapPanel{Margin=new Thickness(0,18,0,16)};bar.Children.Add(Theme.Button("＋ Choose .exe files",Browse,true));bar.Children.Add(Theme.Button("Refresh open applications",LoadRunning));header.Children.Add(bar);DockPanel.SetDock(header,Dock.Top);dock.Children.Add(header);
   var footer=new StackPanel{Margin=new Thickness(0,16,0,0)};note.Margin=new Thickness(0,0,0,12);footer.Children.Add(note);var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};buttons.Children.Add(Theme.Button("Cancel",()=>DialogResult=false));buttons.Children.Add(Theme.Button("Add selected",Commit,true));footer.Children.Add(buttons);DockPanel.SetDock(footer,Dock.Bottom);dock.Children.Add(footer);
   dock.Children.Add(new ScrollViewer{Content=items,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});LoadRunning();
  }
  void LoadRunning(){foreach(string path in saved.OrderBy(AppLibrary.Name))AddChoice(path,false);foreach(string path in AppLibrary.Running())AddChoice(path,false);UpdateNote();}
  internal bool ListedForTest(string path){return choices.ContainsKey(path);}
  void AddChoice(string path,bool select){
   CheckBox existing;if(choices.TryGetValue(path,out existing)){if(select&&existing.IsEnabled)existing.IsChecked=true;return;}
   var description=new StackPanel();description.Children.Add(Theme.Text(AppLibrary.Name(path)+(saved.Contains(path)?" • already added":""),15));var label=Theme.Text(path,11,"Muted");label.ToolTip=path;description.Children.Add(label);
   var box=new CheckBox{Content=description,IsEnabled=!saved.Contains(path),IsChecked=select&&!saved.Contains(path),Margin=new Thickness(0,10,0,10)};
   box.Checked+=(s,e)=>UpdateNote();box.Unchecked+=(s,e)=>UpdateNote();choices.Add(path,box);items.Children.Add(box);
  }
  void Browse(){var picker=new OpenFileDialog{Filter="Windows applications (*.exe)|*.exe",Multiselect=true,Title="Select applications to add"};if(picker.ShowDialog(this)!=true)return;foreach(string path in picker.FileNames)AddChoice(path,true);UpdateNote();}
  void UpdateNote(){note.Text=choices.Values.Count(b=>b.IsEnabled&&b.IsChecked==true)+" application selected. Adding the application will not run it; you can select it again when creating a shortcut.";}
  void Commit(){var selected=choices.Where(p=>p.Value.IsEnabled&&p.Value.IsChecked==true).Select(p=>p.Key).ToList();if(selected.Count==0){note.Text="Select at least one application to add.";return;}if(selected.Any(p=>!File.Exists(p))){note.Text="One of the selected files cannot be found. Refresh the list or reselect the file.";return;}SelectedPaths=selected;DialogResult=true;}
 }
}
