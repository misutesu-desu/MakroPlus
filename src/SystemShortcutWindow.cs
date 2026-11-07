using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AkilliKisayol {
 public sealed class SystemShortcutWindow : Window {
  readonly KeyboardEngine engine;readonly bool paused;readonly SystemShortcut original;readonly List<Binding> bindings;readonly List<SystemShortcut> all;
  readonly TextBox key=new TextBox(),command=new TextBox(),when=new TextBox();readonly TextBlock feedback=Theme.Text("",13,"Muted");readonly Button record,save;
  public bool Saved;
  public SystemShortcutWindow(KeyboardEngine keyboard,SystemShortcut item,IEnumerable<Binding> personal,IEnumerable<SystemShortcut> system){
   engine=keyboard;paused=engine.Paused;engine.Paused=true;original=item;bindings=personal.ToList();all=system.ToList();
   Title="Edit shortcut source";Style=(Style)Application.Current.FindResource(typeof(Window));Width=820;Height=720;MinWidth=680;MinHeight=570;WindowStartupLocation=WindowStartupLocation.CenterOwner;Brand.Fit(this);
   var dock=new DockPanel{Margin=new Thickness(26)};Content=dock;var footer=new StackPanel();feedback.Margin=new Thickness(0,10,0,12);footer.Children.Add(new ScrollViewer{Content=feedback,MaxHeight=155,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});var buttons=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};buttons.Children.Add(Theme.Button("Cancel",()=>DialogResult=false));save=Theme.Button("Save to source",Commit,true);buttons.Children.Add(save);footer.Children.Add(buttons);DockPanel.SetDock(footer,Dock.Bottom);dock.Children.Add(footer);
   var form=new StackPanel();dock.Children.Add(new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});form.Children.Add(Theme.Text(item.Name,24));form.Children.Add(Theme.Text(item.Provider+" • "+item.Scope,13,"Muted"));var path=Theme.Text(item.FilePath,12,"Muted");path.Margin=new Thickness(0,8,0,16);form.Children.Add(path);
   form.Children.Add(Theme.Text("Key combination",14));var row=new DockPanel{Margin=new Thickness(0,8,0,12)};record=Theme.Button("Press key",Capture);DockPanel.SetDock(record,Dock.Right);row.Children.Add(record);row.Children.Add(key);form.Children.Add(row);
   key.Text=item.Provider=="Windows .lnk"?(item.Key>0?TriggerSpec.Format(item.Mods,item.Key):""):item.KeyText;
   if(item.Provider=="Windows .lnk"){
    form.Children.Add(Theme.Text("Example: Ctrl+Alt+K or F9. If left blank, only the key binding is removed; the shortcut file is preserved.",12,"Muted"));form.Children.Add(Theme.Text(item.Description,14));
   }else{
    form.Children.Add(Theme.Text("Example: ctrl+alt+k. Sequential series: ctrl+k ctrl+c.",12,"Muted"));Label(form,"Application command");command.Text=item.Command;form.Children.Add(command);Label(form,"Condition (when) — optional");when.Text=item.When;form.Children.Add(when);form.Children.Add(Theme.Text("Rules preserve other fields like args. A command starting with '-' is a default removal rule; an empty command blocks the operation for this key.",12,"Muted"));
   }
   Label(form,"Record and undo");form.Children.Add(Theme.Text("Changes are written to this source file and the previous version is backed up. After saving, you can restore it from the list using ",12,"Muted"));
   foreach(var box in new[]{key,command,when})box.TextChanged+=(s,e)=>Refresh();Closed+=(s,e)=>{engine.Capture=null;engine.Paused=paused;};Deactivated+=(s,e)=>StopCapture();Refresh();
  }
  static void Label(StackPanel form,string text){var label=Theme.Text(text,14);label.Margin=new Thickness(0,18,0,8);form.Children.Add(label);}
  void StopCapture(){engine.Capture=null;record.Content="Press key";}
  void Capture(){record.Content="Dinleniyor…";engine.Capture=(m,k,p,altgr)=>{StopCapture();if(k==27)return;if(altgr||p>0){feedback.Text="Save Ctrl/Alt/Shift and single key for this source; you can enter a sequence as text.";return;}key.Text=original.Provider=="Windows .lnk"?TriggerSpec.Format(m,k):ShortcutKeys.Code(m,k);};}
  SystemShortcut Draft(){var draft=original.Copy();if(draft.Provider=="Windows .lnk"){
   TriggerSpec gesture;if(key.Text.Trim().Length==0){draft.Key=draft.Mods=0;draft.Enabled=false;}else {if(!TriggerSpec.TryParse(key.Text,out gesture)||gesture.Prefix>0)throw new ArgumentException("Enter a valid combination; for example, Ctrl+Alt+K.");string error=SystemShortcuts.ValidateLink(gesture.Mods,gesture.Key);if(error!=null)throw new ArgumentException(error);draft.Key=gesture.Key;draft.Mods=gesture.Mods;draft.Enabled=true;}
   }else{if(string.IsNullOrWhiteSpace(key.Text))throw new ArgumentException("Key combination cannot be empty.");draft.KeyText=key.Text.Trim();draft.Command=command.Text;draft.Name=draft.Command;draft.When=when.Text;draft.Enabled=!draft.Command.StartsWith("-");}return draft;}
  void Refresh(){try{var draft=Draft();var conflicts=ShortcutConflicts.Find(bindings,all.Where(s=>s.Id!=original.Id).Concat(new[]{draft})).Where(c=>c.FirstId==draft.Id||c.SecondId==draft.Id).ToList();feedback.Text=conflicts.Count==0?"No match found for the records read from this source. You can save.":string.Join("\n",conflicts.Select(c=>c.ToString()));if(draft.Provider=="Application settings"&&!ShortcutKeys.Comparable(draft.KeyText))feedback.Text+="\nThis key format could not be fully matched with personal assignments; check the app's own key format.";save.IsEnabled=true;}catch(ArgumentException e){feedback.Text=e.Message;save.IsEnabled=false;}}
  void Commit(){StopCapture();try{var draft=Draft();if(original.Provider=="Windows .lnk")SystemShortcuts.WriteLink(original,draft.Mods,draft.Key);else SystemShortcuts.WriteCode(original,draft.KeyText,draft.Command,draft.When);Saved=true;DialogResult=true;}catch(Exception e){feedback.Text="Kaydedilemedi: "+e.Message;}}
  internal void KeyForTest(string text){key.Text=text;}
  internal bool CanSaveForTest {get{return save.IsEnabled;}}
 }
}
