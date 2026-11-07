using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace AkilliKisayol {
 public partial class MainWindow {
  IEnumerable<SystemShortcut> AllSystem(){return detected.Concat(ShortcutConflicts.References());}
  static Row SystemRow(SystemShortcut item){return new Row{Active=item.Reference?"—":item.Enabled?"●":"○",Name=item.Name,Combo=item.Combo,Action=item.Description,Scope=item.Scope,Source=item.Provider,Status=item.Reference?"Reference • can be assigned to a new key":item.Provider=="Windows .lnk"?(item.Key==0?"Key not assigned • editable":"Entries in file • Editable"):item.Command.StartsWith("-")?"Remove default rule":"Entries in file • Conditions are evaluated in the application",SystemShortcut=item};}
  UIElement ScanBar(){
   var stack=new StackPanel{Margin=new Thickness(0,0,0,12)};var bar=new WrapPanel();var scan=Theme.Button(scanning?"Scanning…":"Rescan the computer",ScanLinks,true);scan.IsEnabled=!scanning;bar.Children.Add(scan);bar.Children.Add(Theme.Button("Add source file",AddShortcutFiles));bar.Children.Add(Theme.Button("Add folder",AddShortcutFolder));bar.Children.Add(Theme.Button("Scan sources",ManageShortcutSources));stack.Children.Add(bar);
   var text=Theme.Text(scanInfo,12,"Muted");text.Margin=new Thickness(0,10,0,6);stack.Children.Add(text);stack.Children.Add(Theme.Text("The Desktop, Start menu, and application user settings are scanned. A source record is not proof that the button is currently being captured by the application. Select and edit an entry from the list.",12,"Muted"));
   if(scanWarnings.Count>0){var expander=new Expander{Header=scanWarnings.Count+" tarama bildirimi",Margin=new Thickness(0,8,0,0),Content=new ScrollViewer{MaxHeight=140,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=Theme.Text(string.Join("\n",scanWarnings),11,"Muted")}};stack.Children.Add(expander);}return stack;
  }
  async void ScanLinks(){await ScanSystemAsync();}
  async Task ScanSystemAsync(){
   if(scanning)return;scanning=true;scanInfo="Scanning shortcut sources...";status.Text=scanInfo;if(view=="Computer shortcuts"||view=="Conflicts")Render();
   var sources=state.ShortcutSources.ToArray();try{var found=await Task.Run(()=>SystemShortcuts.Scan(sources));if(closed)return;detected.Clear();detected.AddRange(found.Items);scanWarnings.Clear();scanWarnings.AddRange(found.Warnings);scanInfo=found.Files+" sources • "+found.Items.Count+" entry • "+found.Items.Count(i=>i.Enabled)+" key assignment • "+DateTime.Now.ToString("HH:mm");status.Text=scanInfo;}
   catch(Exception e){if(!closed){scanInfo="Scan could not complete: "+e.Message;status.Text=scanInfo;}}
   finally{scanning=false;if(!closed){if(view=="Computer shortcuts"||view=="Conflicts")Render();else Refresh();}}
  }
  void AddShortcutFiles(){var picker=new OpenFileDialog{Title="Select shortcut source",Multiselect=true,Filter="Shortcut sources|*.lnk;keybindings.json|Windows shortcuts|*.lnk|Application shortcuts|keybindings.json"};if(picker.ShowDialog(this)==true)RememberShortcutSources(picker.FileNames);}
  void AddShortcutFolder(){using(var picker=new System.Windows.Forms.FolderBrowserDialog{Description="Select the folder to be scanned for .lnk files"})if(picker.ShowDialog()==System.Windows.Forms.DialogResult.OK)RememberShortcutSources(new[]{picker.SelectedPath});}
  void RememberShortcutSources(IEnumerable<string> paths){var previous=state.ShortcutSources.ToList();foreach(string path in paths)if(!state.ShortcutSources.Contains(path,StringComparer.OrdinalIgnoreCase))state.ShortcutSources.Add(Path.GetFullPath(path));if(!Save()){state.ShortcutSources=previous;return;}ScanLinks();}
  void ManageShortcutSources(){
   var window=new Window{Title="Scan sources",Owner=this,Width=720,Height=520,WindowStartupLocation=WindowStartupLocation.CenterOwner,Style=(Style)Application.Current.FindResource(typeof(Window))};var panel=new StackPanel{Margin=new Thickness(24)};window.Content=panel;panel.Children.Add(Theme.Text("Otomatik kaynaklar",21));panel.Children.Add(Theme.Text(string.Join("\n",SystemShortcuts.DefaultRoots())+"\nVS Code / Insiders / Cursor / VSCodium user and profile shortcuts",12,"Muted"));panel.Children.Add(Theme.Text("Ek kaynaklar",18));var list=new ListBox{ItemsSource=state.ShortcutSources.ToArray(),Height=180,Margin=new Thickness(0,10,0,12)};panel.Children.Add(list);panel.Children.Add(Theme.Button("Remove selected additional source",()=>{string selected=list.SelectedItem as string;if(selected==null)return;var previous=state.ShortcutSources.ToList();state.ShortcutSources.Remove(selected);if(!Save()){state.ShortcutSources=previous;return;}list.ItemsSource=state.ShortcutSources.ToArray();ScanLinks();}));window.ShowDialog();
  }
  void EditSystem(SystemShortcut item){
   if(item.Reference){
    string issue=Rules.Sendable(item.Mods,item.Key);if(issue!=null){status.Text=issue;return;}
    string app="";if(item.App.Length>0){app=state.ApplicationPaths.Concat(AppLibrary.Running()).FirstOrDefault(p=>Rules.SameApp(Path.GetFileName(p),item.App)&&File.Exists(p))??"";if(app.Length==0){var picker=new OpenFileDialog{Title=item.App+" select the application",Filter="Applications|*.exe",FileName=item.App};if(picker.ShowDialog(this)!=true)return;app=picker.FileName;}}
    Edit(null,new Binding{Name=item.Name+" (new key)",AppPath=app,Kind="Send key",SendKey=item.Key,SendMods=item.Mods});return;
   }
   var dialog=new SystemShortcutWindow(engine,item,state.Bindings,AllSystem()){Owner=this};if(dialog.ShowDialog()==true)ScanLinks();
  }
  void UpdateRowActions(){if(editRow==null)return;var row=Selected();if(row!=null&&row.Binding!=null){var b=row.Binding;bool outside=!string.IsNullOrEmpty(b.ProfileId)&&b.ProfileId!=state.ActiveProfileId;toggleRow.Content=outside?state.Profiles.First(p=>p.Id==b.ProfileId).Name+" go to your profile and enable":state.Paused?"Keep engine running and enable":b.Enabled?"Disable":"Enable";}else toggleRow.Content="Enable / disable";var item=row==null?null:row.SystemShortcut;editRow.IsEnabled=row!=null;editRow.Content=item!=null&&item.Reference?"Assign to new key":"Edit";toggleRow.IsEnabled=row!=null&&(row.Binding!=null||(item!=null&&item.Provider=="Windows .lnk"&&(item.Enabled||File.Exists(SystemShortcuts.Backup(item.FilePath)))));deleteRow.IsEnabled=row!=null&&(row.Binding!=null||(item!=null&&item.Provider=="Windows .lnk"&&item.Enabled));deleteRow.Content=item!=null?"Remove key assignment":"Delete";restoreRow.IsEnabled=item!=null&&item.Editable&&File.Exists(SystemShortcuts.Backup(item.FilePath));sourceRow.IsEnabled=item!=null&&item.FilePath.Length>0;}
  void ToggleSystem(SystemShortcut item){if(item.Provider!="Windows .lnk")return;try{if(item.Enabled)SystemShortcuts.WriteLink(item,0,0);else if(File.Exists(SystemShortcuts.Backup(item.FilePath)))SystemShortcuts.Restore(item);ScanLinks();}catch(Exception e){status.Text="Could not change: "+e.Message;}}
  void ClearSystemKey(SystemShortcut item){if(item.Provider!="Windows .lnk"||!item.Enabled)return;if(MessageBox.Show(this,item.Name+" remove key binding?\nShortcut file and target are preserved.","Remove key assignment",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;try{SystemShortcuts.WriteLink(item,0,0);ScanLinks();}catch(Exception e){status.Text="Could not remove: "+e.Message;}}
  void RestoreSource(){var row=Selected();if(row==null||row.SystemShortcut==null||!row.SystemShortcut.Editable){status.Text="Select an editable source read from the computer to restore your backup.";return;}var item=row.SystemShortcut;
   if(!File.Exists(SystemShortcuts.Backup(item.FilePath))){status.Text="No Smart Shortcut backup found for this source.";return;}
   if(MessageBox.Show(this,item.FilePath+"\n\nDo you want to restore the previous version before the last change?","Restore source",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
   try{SystemShortcuts.Restore(item);ScanLinks();}catch(Exception e){status.Text="Failed to reload: "+e.Message;}
  }
  void ShowSource(){var row=Selected();if(row==null||row.SystemShortcut==null||row.SystemShortcut.FilePath.Length==0){status.Text="Select a registry entry that has a file source.";return;}Process.Start(new ProcessStartInfo("explorer.exe","/select,"+AppIntegration.Quote(row.SystemShortcut.FilePath)){UseShellExecute=true});}
  internal void SystemRowsForTest(IEnumerable<SystemShortcut> items){detected.Clear();detected.AddRange(items);scanInfo=detected.Count+" test entry";view="Computer shortcuts";source.SelectedIndex=3;Render();UpdateLayout();}
  internal int SystemRowCountForTest {get{return table.Items.Count;}}
  internal bool SelectSystemForTest(int index){table.SelectedIndex=index;return editRow.IsEnabled;}
  internal void ConflictsForTest(){view="Conflicts";Render();UpdateLayout();}
 }
}
