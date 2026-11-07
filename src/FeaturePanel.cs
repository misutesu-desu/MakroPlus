using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AkilliKisayol {
 public partial class MainWindow {
  ShortcutGuide guide;readonly ComboBox activeProfile=new ComboBox();bool updatingProfile;
  void InitializeFeatures(){Profiles.Normalize(state);guide=new ShortcutGuide(engine,()=>state,()=>AllSystem());InitializeGestures();}
  UIElement ProfileSelector(){var panel=new StackPanel{Margin=new Thickness(0,0,0,12)};panel.Children.Add(Theme.Text("ACTIVE PROFILE",10,"Muted"));activeProfile.DisplayMemberPath="Name";activeProfile.SelectedValuePath="Id";activeProfile.ItemsSource=state.Profiles;activeProfile.SelectedValue=state.ActiveProfileId;activeProfile.Margin=new Thickness(0,5,0,0);activeProfile.SelectionChanged+=(s,e)=>{var selected=activeProfile.SelectedItem as ShortcutProfile;if(!updatingProfile&&selected!=null&&selected.Id!=state.ActiveProfileId)SwitchProfile(selected.Id);};panel.Children.Add(activeProfile);return panel;}
  void SyncProfileChoice(){updatingProfile=true;try{activeProfile.SelectedValue=state.ActiveProfileId;}finally{updatingProfile=false;}}
  void SwitchProfile(string id){if(!state.Profiles.Any(p=>p.Id==id))return;string old=state.ActiveProfileId;state.ActiveProfileId=id;if(!Save()){state.ActiveProfileId=old;SyncProfileChoice();return;}SyncProfileChoice();engine.ResetPending();Actions.CancelWorkflow();guide.Hide();UpdatePause();probes.Clear();Render();status.Text="Active profile: "+Profiles.Name(state);}
  void OpenProfiles(){guide.Hide();bool paused=engine.Paused;engine.Paused=true;engine.ResetPending();try{var dialog=new ProfilesWindow(state,(profiles,assignments)=>{
    var bindings=state.Bindings.Select(b=>b.Copy()).ToList();foreach(var b in bindings){var item=assignments.FirstOrDefault(a=>a.Id==b.Id);if(item!=null)b.ProfileId=item.ProfileId??"";}
    var errors=bindings.Where(b=>b.Enabled).SelectMany(b=>Rules.Conflicts(b,bindings)).Where(c=>c.StartsWith("CONFLICT")).Distinct().ToList();if(errors.Count>0){MessageBox.Show(this,string.Join("\n",errors),"Profile conflicts");return false;}
    var oldProfiles=state.Profiles;var oldBindings=state.Bindings;state.Profiles=profiles;state.Bindings=bindings;if(!Save()){state.Profiles=oldProfiles;state.Bindings=oldBindings;return false;}engine.ResetPending();Actions.CancelWorkflow();activeProfile.ItemsSource=state.Profiles;activeProfile.SelectedValue=state.ActiveProfileId;return true;
   }){Owner=this};dialog.ShowDialog();}finally{engine.Paused=paused;UpdatePause();Render();}}
  void OpenPreferences(){guide.Hide();var dialog=new PreferencesWindow(state.Preferences,state.ApplicationPaths,preferences=>{var old=state.Preferences;state.Preferences=preferences;if(Save())return true;state.Preferences=old;return false;}){Owner=this};dialog.ShowDialog();}
  void OpenMacro(Binding previous=null){guide.Hide();var dialog=new MacroWindow(engine,state.Bindings,state.Profiles,state.ActiveProfileId,()=>AllSystem(),previous){Owner=this};if(dialog.ShowDialog()==true)StoreBinding(dialog.Result,previous);}
  UIElement GuideSettings(){
   var form=new StackPanel();form.Children.Add(Theme.Text("View shortcuts of the open application",23));form.Children.Add(Theme.Text("Press and hold the Right Ctrl button for about half a second. The Help panel will appear without focus; it closes when you release the button. If you press another key, the panel closes and normal Ctrl combinations work.",14));
   var enabled=new CheckBox{Content="Show shortcut guide when holding the guide key",IsChecked=state.GuideEnabled};form.Children.Add(enabled);var keys=new ComboBox{DisplayMemberPath="Name",SelectedValuePath="Key",Margin=new Thickness(0,10,0,14),ItemsSource=new[]{new GuideKey{Key=163,Name="Right Ctrl"},new GuideKey{Key=162,Name="Left Ctrl"},new GuideKey{Key=161,Name="Right Shift"},new GuideKey{Key=160,Name="Left Shift"}}};keys.SelectedValue=state.GuideHoldKey;form.Children.Add(keys);
   form.Children.Add(Theme.Button("Save help settings",()=>{bool old=state.GuideEnabled;int key=state.GuideHoldKey;state.GuideEnabled=enabled.IsChecked==true;state.GuideHoldKey=keys.SelectedValue is int?(int)keys.SelectedValue:163;if(!Save()){state.GuideEnabled=old;state.GuideHoldKey=key;}guide.Hide();}));form.Children.Add(Theme.Button("Show example panel (6 seconds)",()=>guide.ShowOnce(Native.GetForegroundWindow())));return form;
  }
  sealed class GuideKey {public int Key {get;set;}public string Name {get;set;}}
  internal bool ProfileForTest(string id){SwitchProfile(id);return state.ActiveProfileId==id;}
 }
}
