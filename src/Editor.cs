using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace AkilliKisayol {
 public class Editor : Window {
  readonly KeyboardEngine engine;readonly bool wasPaused;readonly IEnumerable<Binding> existing;readonly Binding draft;
  readonly TextBox name=new TextBox(),target=new TextBox(),arguments=new TextBox(),appPath=new TextBox{IsReadOnly=true};
  readonly ComboBox profileChoice=new ComboBox();
  readonly ComboBox kind=new ComboBox(),scope=new ComboBox(),running=new ComboBox(),media=new ComboBox(),savedLaunch=new ComboBox();
  readonly List<string> library;readonly Func<IEnumerable<SystemShortcut>> external;
  readonly CheckBox enabled=new CheckBox{Content="Shortcut enabled"},pass=new CheckBox{Content="Also send the main key to the source application"};
  readonly StackPanel actionFields=new StackPanel(),scopeFields=new StackPanel();
  readonly TextBlock combo=Theme.Text("",21),sendCombo=Theme.Text("",18),feedback=Theme.Text("",12,"Muted");
  readonly Button record,sendRecord;int key,mods,prefixKey,sendKey,sendMods;bool recording;
  public Binding Result;
  public Editor(KeyboardEngine keyboard,Binding old,IEnumerable<Binding> all,IEnumerable<string> savedApps=null,Binding initial=null,Func<IEnumerable<SystemShortcut>> externalShortcuts=null,IEnumerable<ShortcutProfile> profiles=null,string activeProfile="",bool actionOnly=false) {
   external=externalShortcuts??(()=>new SystemShortcut[0]);library=(savedApps??new string[0]).ToList();engine=keyboard;wasPaused=engine.Paused;engine.Paused=true;existing=all;draft=(old??initial??new Binding()).Copy();
   Style=(Style)Application.Current.FindResource(typeof(Window));Title=old==null?"New shortcut":"Edit shortcut";Width=760;Height=860;MinWidth=640;MinHeight=650;WindowStartupLocation=WindowStartupLocation.CenterOwner;Brand.Fit(this);
   key=draft.Key;mods=draft.Mods;prefixKey=draft.PrefixKey;sendKey=draft.SendKey;sendMods=draft.SendMods;
   var dock=new DockPanel{Margin=new Thickness(28)};Content=dock;
   var heading=new StackPanel{Margin=new Thickness(0,0,0,18)};heading.Children.Add(Theme.Text(Title,26));heading.Children.Add(Theme.Text("One key, one action. You define the scope.",13,"Muted"));DockPanel.SetDock(heading,Dock.Top);dock.Children.Add(heading);
   var footer=new StackPanel{Margin=new Thickness(0,14,0,0)};feedback.Margin=new Thickness(0,0,0,12);footer.Children.Add(feedback);var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};buttons.Children.Add(Theme.Button("Cancel",()=>{DialogResult=false;}));buttons.Children.Add(Theme.Button("Save assignment",Commit,true));footer.Children.Add(buttons);DockPanel.SetDock(footer,Dock.Bottom);dock.Children.Add(footer);
   var form=new StackPanel();dock.Children.Add(new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
   AddLabel(form,"Assignment name");name.Text=draft.Name;form.Children.Add(name);AddLabel(form,"Profile");profileChoice.DisplayMemberPath="Name";profileChoice.SelectedValuePath="Id";profileChoice.ItemsSource=new[]{new ShortcutProfile{Id="",Name="All profiles"}}.Concat(profiles??Profiles.Defaults()).ToList();profileChoice.SelectedValue=old!=null?old.ProfileId??"":activeProfile;form.Children.Add(profileChoice);
   AddLabel(form,"Trigger combination");var capture=new StackPanel();combo.Text=Native.Label(mods,key,prefixKey);combo.Margin=new Thickness(0,0,0,12);capture.Children.Add(combo);
   var captureBar=new WrapPanel();record=Theme.Button("● Capture keyboard / mouse",()=>StartCapture(false));captureBar.Children.Add(record);captureBar.Children.Add(Theme.Button("Clear",()=>{StopCapture();key=mods=prefixKey=0;combo.Text=Native.Label(mods,key);UpdateFeedback();}));capture.Children.Add(captureBar);capture.Children.Add(Theme.Text("Press and hold Del, then press 5. Escape cancels; actions do not work during recording.",12,"Muted"));var captureCard=Theme.Card(capture,"Surface");captureCard.Visibility=actionOnly?Visibility.Collapsed:Visibility.Visible;form.Children.Add(captureCard);
   var mouseChoice=new ComboBox{ItemsSource=InputKeys.Pointers.Select(k=>new MouseChoice{Key=k,Name=InputKeys.Name(k)}).ToList(),DisplayMemberPath="Name",Margin=new Thickness(0,8,0,0)};mouseChoice.ToolTip="You can select the mouse button by hand or save with Ctrl/Alt/Shift.";mouseChoice.SelectionChanged+=(s,e)=>{var choice=mouseChoice.SelectedItem as MouseChoice;if(choice!=null){StopCapture();key=choice.Key;prefixKey=0;combo.Text=Native.Label(mods,key);UpdateFeedback();}};form.Children.Add(Theme.Text("Mouse button or wheel selection",12,"Muted"));mouseChoice.Visibility=actionOnly?Visibility.Collapsed:Visibility.Visible;form.Children.Add(mouseChoice);
   AddLabel(form,"Scope");scope.ItemsSource=new[]{"Global — in every application","Application-specific"};scope.SelectedIndex=draft.AppPath.Length==0?0:1;form.Children.Add(scope);form.Children.Add(scopeFields);
   appPath.Text=draft.AppPath;
   var apps=library.Select(p=>new AppItem{Path=p,Saved=true}).ToList();foreach(string path in AppLibrary.Running())if(!apps.Any(x=>Rules.SameApp(x.Path,path)))apps.Add(new AppItem{Path=path});
   running.ItemsSource=apps.OrderByDescending(x=>x.Saved).ThenBy(x=>x.Path).ToList();running.ToolTip="Added and running applications • full executable file path";running.SelectionChanged+=(s,e)=>{if(running.SelectedItem!=null)appPath.Text=((AppItem)running.SelectedItem).Path;UpdateFeedback();};
   scope.SelectionChanged+=(s,e)=>{BuildScope();UpdateFeedback();};BuildScope();
   AddLabel(form,"Action type");kind.ItemsSource=new[]{"Application","File / folder","Web address","URI / deep link","Spotify","Send key","Media","PowerShell","Terminal (cmd)"};kind.SelectedItem=draft.Kind;form.Children.Add(kind);form.Children.Add(actionFields);
   target.Text=draft.Target;arguments.Text=draft.Arguments;
   savedLaunch.ItemsSource=library.Select(p=>new AppItem{Path=p,Saved=true}).ToList();savedLaunch.SelectionChanged+=(s,e)=>{if(savedLaunch.SelectedItem!=null)target.Text=((AppItem)savedLaunch.SelectedItem).Path;};
   media.ItemsSource=new[]{"Play / pause","Next track","Previous track","Volume up","Volume down","Mute / unmute"};media.SelectedItem=draft.Target;if(media.SelectedIndex<0)media.SelectedIndex=0;
   sendRecord=Theme.Button("Save the combination to send",()=>StartCapture(true));sendCombo.Text=Native.Label(sendMods,sendKey);
   kind.SelectionChanged+=(s,e)=>{StopCapture();BuildAction();UpdateFeedback();};BuildAction();
   enabled.IsChecked=draft.Enabled;pass.IsChecked=draft.PassThrough;form.Children.Add(enabled);form.Children.Add(pass);form.Children.Add(Theme.Text("When transmission is closed, the main key is blocked; Ctrl/Alt/Shift events may reach the source application. Full control over external applications is not guaranteed.",12,"Muted"));
   foreach(var t in new[]{name,target,arguments,appPath})t.TextChanged+=(s,e)=>UpdateFeedback();profileChoice.SelectionChanged+=(s,e)=>UpdateFeedback();enabled.Checked+=(s,e)=>UpdateFeedback();enabled.Unchecked+=(s,e)=>UpdateFeedback();
   Closed+=(s,e)=>{engine.Capture=null;engine.Paused=wasPaused;};Deactivated+=(s,e)=>StopCapture();UpdateFeedback();
  }
  void AddLabel(StackPanel panel,string text){var label=Theme.Text(text,13);label.Margin=new Thickness(0,18,0,8);panel.Children.Add(label);}
  void BuildScope(){scopeFields.Children.Clear();if(scope.SelectedIndex!=1)return;AddLabel(scopeFields,"Select a registered application or choose its .exe. The application can be closed.");scopeFields.Children.Add(running);appPath.Margin=new Thickness(0,8,0,8);scopeFields.Children.Add(appPath);scopeFields.Children.Add(Theme.Button("Choose an .exe file",()=>{var dialog=new OpenFileDialog{Filter="Applications (*.exe)|*.exe"};if(dialog.ShowDialog(this)==true){running.SelectedItem=null;appPath.Text=dialog.FileName;UpdateFeedback();}}));}
  void BuildAction(){
   actionFields.Children.Clear();string type=kind.SelectedItem as string;
   if(type=="Application" && library.Count>0){AddLabel(actionFields,"Quick select from added applications");actionFields.Children.Add(savedLaunch);}
   if(type=="Send key"){AddLabel(actionFields,"Keys to send");actionFields.Children.Add(sendCombo);actionFields.Children.Add(sendRecord);actionFields.Children.Add(Theme.Text("After releasing the shortcut, it is sent to the same window in the foreground. It may not work in management applications.",12,"Muted"));return;}
   if(type=="Media"){AddLabel(actionFields,"Media action");actionFields.Children.Add(media);return;}
   AddLabel(actionFields,type=="PowerShell"||type=="Terminal (cmd)"?"Command to run (full text)":type=="Spotify"?"Spotify content link or URI":type=="Application"?"Executable file":type=="File / folder"?"File or folder path":"Address");
   target.AcceptsReturn=type=="PowerShell"||type=="Terminal (cmd)";target.TextWrapping=target.AcceptsReturn?TextWrapping.Wrap:TextWrapping.NoWrap;target.MinHeight=target.AcceptsReturn?100:36;target.MaxHeight=180;target.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;actionFields.Children.Add(target);
   if(type=="Application"||type=="File / folder"){
    var bar=new WrapPanel{Margin=new Thickness(0,8,0,0)};bar.Children.Add(Theme.Button("Select file",()=>{var dialog=new OpenFileDialog{Filter=type=="Application"?"Applications (*.exe)|*.exe":"All files|*.*"};if(dialog.ShowDialog(this)==true)target.Text=dialog.FileName;}));
    if(type=="File / folder")bar.Children.Add(Theme.Button("Select folder",()=>{using(var picker=new System.Windows.Forms.FolderBrowserDialog()){if(picker.ShowDialog()==System.Windows.Forms.DialogResult.OK)target.Text=picker.SelectedPath;}}));actionFields.Children.Add(bar);
   }
   if(type=="Application"){AddLabel(actionFields,"Startup parameters (optional)");actionFields.Children.Add(arguments);}
   if(type=="Spotify")actionFields.Children.Add(Theme.Text("Connection converted to Spotify URI and content opens. Direct playback is not supported; playback is not verified. Spotify must be installed; network may be required for content.",12,"Muted"));
   if(type=="PowerShell"||type=="Terminal (cmd)")actionFields.Children.Add(Theme.Text("This text is a command and operates with your user privileges. Do not write passwords/keys. PowerShell code is passed as a separate argument in UTF-16 Base64; cmd interprets its own shell syntax. Output is not captured; results and exit code are logged to history.",12,"Muted"));
  }
  void StartCapture(bool send){
   StopCapture();recording=true;(send?sendRecord:record).Content="Listening… Escape to cancel";
   engine.Capture=(m,k,prefix,altgr)=>{
    if(!recording)return;
    if(k==27){StopCapture();feedback.Text="Record was cancelled; previous combination preserved.";return;}
    if(altgr){StopCapture();feedback.Text="AltGr is reserved for character entry and is not a shortcut trigger. Use Left Alt.";return;}
    if(send&&prefix>0){StopCapture();feedback.Text="The output supports modifiers and one keyboard key, mouse button or wheel direction.";return;}
    if(send){sendMods=m;sendKey=k;sendCombo.Text=Native.Label(m,k);}else{mods=m;key=k;prefixKey=prefix;combo.Text=Native.Label(m,k,prefix);}
    StopCapture();UpdateFeedback();
   };
  }
  void StopCapture(){engine.Capture=null;recording=false;record.Content="● Capture keyboard / mouse";if(sendRecord!=null)sendRecord.Content="Save the combination to send";}
  Binding Read(){var b=draft.Copy();b.Name=name.Text.Trim();b.ProfileId=profileChoice.SelectedValue as string??"";b.Key=key;b.Mods=mods;b.PrefixKey=prefixKey;b.AppPath=scope.SelectedIndex==1?appPath.Text:"";b.Kind=kind.SelectedItem as string??"Application";b.Target=b.Kind=="Media"?media.SelectedItem as string:target.Text.Trim();b.Arguments=arguments.Text;b.Enabled=enabled.IsChecked==true;b.PassThrough=pass.IsChecked==true;b.SendKey=sendKey;b.SendMods=sendMods;return b;}
  void UpdateFeedback(){if(kind.SelectedItem==null)return;var b=Read();var warnings=Rules.Conflicts(b,existing);warnings.AddRange(ShortcutConflicts.ForBinding(b,external()));feedback.Text=warnings.Count>0?string.Join("\n",warnings):"Application-specific assignment takes precedence over general assignments on the same key.";}
  void Commit(){
   StopCapture();var b=Read();if(scope.SelectedIndex==1&&b.AppPath.Length==0){feedback.Text="Select a .exe for the application-specific scope.";return;}
   string error=Rules.Validate(b,existing);
   if(error!=null){feedback.Text=error;return;}
   var warnings=Rules.Conflicts(b,existing);warnings.AddRange(ShortcutConflicts.ForBinding(b,external()));if(b.Mods==0&&b.Key<112)warnings.Add("Unrestricted key may affect normal text input. You can limit the scope to an application.");
   if(warnings.Count>0&&MessageBox.Show(this,string.Join("\n\n",warnings)+"\n\nSave with this selection?","Scope and conflict information",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
   Result=b;DialogResult=true;
  }
  class MouseChoice {public int Key;public string Name{get;set;}}
  class AppItem {public string Path;public bool Saved;public override string ToString(){return (Saved?"Registered • ":"Open • ")+System.IO.Path.GetFileName(Path)+" — "+Path;}}
  internal Binding CurrentDraft(){return Read();}
  internal string FeedbackForTest {get{return feedback.Text;}}
  internal void SmokeFields(){foreach(string type in new[]{"Spotify","PowerShell","Terminal (cmd)","Media","Send key","File / folder","Web address","URI / deep link","Application"}){kind.SelectedItem=type;UpdateLayout();}scope.SelectedIndex=1;UpdateLayout();scope.SelectedIndex=0;}
 }
}
