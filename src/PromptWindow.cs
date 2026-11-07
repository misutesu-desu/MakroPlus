using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AkilliKisayol {
 public sealed class PromptWindow : Window {
  readonly KeyboardEngine engine;readonly bool pausedBefore;readonly List<Binding> existing;readonly Binding old;
  readonly ComboBox profileChoice=new ComboBox();readonly UserPreferences preferences;readonly string currentProfile;readonly CheckBox enabled=new CheckBox{Content="Shortcut active",IsChecked=true};
  readonly List<string> paths;readonly ComboBox application=new ComboBox(),targetApplication=new ComboBox();readonly TextBox trigger=new TextBox(),input=new TextBox();
  readonly Func<IEnumerable<string>> registeredApps;readonly Func<IEnumerable<SystemShortcut>> external;
  readonly StackPanel preview=new StackPanel();readonly TextBlock feedback=Theme.Text("",13,"Muted"),modelInfo=Theme.Text("",11,"Muted");readonly Button save,record,analyze;
  readonly ComboBox interpreter=new ComboBox{Width=200};readonly CheckBox gpu=new CheckBox{Content="Use GPU for local models",IsChecked=true};
  ModelOptions options;readonly Func<ModelOptions,string,bool> saveOptions;
  readonly ShapeTemplate shapeTrigger;
  readonly Func<string,bool> saveModel;string modelPath;CancellationTokenSource generation;int revision;bool closed;
  PromptPlan plan;bool recording;
  public Binding Result;
  public string ProfileId {get{return profileChoice.SelectedValue as string??"";}set{profileChoice.SelectedValue=value;}}
  internal Func<LlmRequest,Action<string>,CancellationToken,Task<PromptPlan>> Planner=(request,progress,cancellation)=>LocalLlm.Shared.AnalyzeAsync(request,progress,cancellation);
  public PromptWindow(KeyboardEngine keyboard,IEnumerable<Binding> bindings,IEnumerable<string> applications,string initialApp="",Binding previous=null,string configuredModel=null,Func<string,bool> saveModelPath=null,Func<IEnumerable<string>> appProvider=null,Func<IEnumerable<SystemShortcut>> externalShortcuts=null,IEnumerable<ShortcutProfile> profiles=null,string activeProfile="",UserPreferences savedPreferences=null,bool preferGpu=true,Action<bool> saveGpu=null,ModelOptions modelOptions=null,Func<ModelOptions,string,bool> saveModelOptions=null,ShapeTemplate gesture=null){
   shapeTrigger=gesture==null?null:gesture.Copy();
   options=(modelOptions??new ModelOptions()).Copy();saveOptions=saveModelOptions;
   gpu.IsChecked=preferGpu;preferences=(savedPreferences??new UserPreferences()).Copy();currentProfile=activeProfile;engine=keyboard;pausedBefore=engine.Paused;engine.Paused=true;engine.ResetPending();existing=bindings.ToList();old=previous;enabled.IsChecked=previous==null||previous.Enabled;
   modelPath=string.IsNullOrWhiteSpace(configuredModel)?LocalLlm.DefaultModelPath:configuredModel;saveModel=saveModelPath;
   external=externalShortcuts??(()=>new SystemShortcut[0]);registeredApps=appProvider??(()=>applications);
   paths=applications.Concat(AppLibrary.Running()).Concat(PreferenceMemory.Paths(preferences)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();if(initialApp.Length>0&&!paths.Contains(initialApp,StringComparer.OrdinalIgnoreCase))paths.Add(initialApp);
   if(previous!=null&&!string.IsNullOrWhiteSpace(previous.BrowserPath))AppLibrary.Remember(paths,previous.BrowserPath);if(previous!=null&&previous.Steps!=null)foreach(var step in previous.Steps)if(!string.IsNullOrWhiteSpace(step.AppPath))AppLibrary.Remember(paths,step.AppPath);
   Style=(Style)Application.Current.FindResource(typeof(Window));Title="Shortcut with Prompt";Width=940;Height=870;MinWidth=780;MinHeight=650;WindowStartupLocation=WindowStartupLocation.CenterOwner;Brand.Fit(this);
   var dock=new DockPanel{Margin=new Thickness(28)};Content=dock;
   var header=new StackPanel{Margin=new Thickness(0,0,0,18)};header.Children.Add(Theme.Text("Describe your shortcut",27));header.Children.Add(Theme.Text("1. Write your task   →   2. Review the plan   →   3. Save",13,"Muted"));var modelRow=new DockPanel{Margin=new Thickness(0,12,0,0)};var change=Theme.Button("Change model",SelectModel);DockPanel.SetDock(change,Dock.Right);modelRow.Children.Add(change);modelInfo.Text=options.Describe(modelPath);modelInfo.VerticalAlignment=VerticalAlignment.Center;modelRow.Children.Add(modelInfo);header.Children.Add(modelRow);DockPanel.SetDock(header,Dock.Top);dock.Children.Add(header);
   var footer=new StackPanel{Margin=new Thickness(0,12,0,0)};footer.Children.Add(new ScrollViewer{Content=feedback,MaxHeight=100,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});var buttons=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};buttons.Children.Add(Theme.Button("Cancel",()=>DialogResult=false));save=Theme.Button("Save assignment",Commit,true);save.IsEnabled=false;buttons.Children.Add(save);footer.Children.Add(buttons);DockPanel.SetDock(footer,Dock.Bottom);dock.Children.Add(footer);
   var form=new StackPanel();dock.Children.Add(new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
   var modelBar=new WrapPanel{Margin=new Thickness(0,0,0,8)};interpreter.ItemsSource=new[]{"Selected AI model","Rule interpreter"};interpreter.SelectedIndex=0;interpreter.Margin=new Thickness(0,0,10,0);modelBar.Children.Add(interpreter);modelBar.Children.Add(Theme.Button("Unload local model",()=>{if(generation!=null)generation.Cancel();LocalLlm.Shared.Stop();feedback.Text="Model memory released. It will reload on the next request.";}));gpu.Margin=new Thickness(8,6,0,0);modelBar.Children.Add(gpu);form.Children.Add(modelBar);
   profileChoice.DisplayMemberPath="Name";profileChoice.SelectedValuePath="Id";profileChoice.ItemsSource=new[]{new ShortcutProfile{Id="",Name="All profiles"}}.Concat(profiles??Profiles.Defaults()).ToList();profileChoice.SelectedValue=previous!=null?previous.ProfileId??"":activeProfile;var profileLabel=Theme.Text("Profile",12,"Muted");if(shapeTrigger!=null){profileLabel.Visibility=profileChoice.Visibility=enabled.Visibility=Visibility.Collapsed;}form.Children.Add(profileLabel);form.Children.Add(profileChoice);
   enabled.Margin=new Thickness(0,10,0,8);form.Children.Add(enabled);form.Children.Add(Theme.Text("General scope applies to all applications. Select Profile Independently; select All Profiles to have it work in all profiles.",12,"Muted"));
   var scopeRow=new Grid();scopeRow.ColumnDefinitions.Add(new ColumnDefinition());scopeRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(265)});
   var appField=new StackPanel{Margin=new Thickness(0,0,15,0)};appField.Children.Add(Theme.Text("Where should the shortcut run?",12,"Muted"));application.Margin=new Thickness(0,7,0,0);appField.Children.Add(application);scopeRow.Children.Add(appField);
   var keyField=new StackPanel();keyField.Children.Add(Theme.Text("Key (you can also type it in the prompt)",12,"Muted"));var keys=new DockPanel{Margin=new Thickness(0,7,0,0)};record=Theme.Button("Press key",CaptureKey);record.Margin=new Thickness(7,0,0,0);record.ToolTip="Select shortcut from keyboard";DockPanel.SetDock(record,Dock.Right);keys.Children.Add(record);trigger.ToolTip="For example, F8, Ctrl+Alt+K, XButton1, or Ctrl+WheelUp. This field is used before the key in the prompt if filled.";keys.Children.Add(trigger);keyField.Children.Add(keys);Grid.SetColumn(keyField,1);scopeRow.Children.Add(keyField);if(shapeTrigger!=null){keyField.Visibility=Visibility.Collapsed;scopeRow.ColumnDefinitions[1].Width=new GridLength(0);}form.Children.Add(scopeRow);
   form.Children.Add(Theme.Text("Target application — connection or operation which application should open?",12,"Muted"));targetApplication.Margin=new Thickness(0,7,0,0);form.Children.Add(targetApplication);
   var appsBar=new WrapPanel{Margin=new Thickness(0,8,0,12)};appsBar.Children.Add(Theme.Button("Select target .exe",Browse));appsBar.Children.Add(Theme.Button("Refresh application list",RefreshApps));form.Children.Add(appsBar);form.Children.Add(Theme.Text(shapeTrigger==null?"Example: scope General, target Brave → YouTube opens from every app in Brave. Hold Del and press 5 for Del+5.":"Choose the activation scope and destination app independently. Your recorded shape supplies the trigger.",12,"Muted"));
   form.Children.Add(Theme.Text("Registered apps and selected .exe files can be closed while creating this shortcut. Use Global scope to launch an app from anywhere. UI actions need their target window when the shortcut runs.",12,"Muted"));
   form.Children.Add(Theme.Text("Your request",14));input.AcceptsReturn=true;input.TextWrapping=TextWrapping.Wrap;input.VerticalContentAlignment=VerticalAlignment.Top;input.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;input.Height=145;input.Margin=new Thickness(0,8,0,10);input.FontSize=16;form.Children.Add(input);
   var examples=new WrapPanel();foreach(string example in shapeTrigger==null?new[]{"Open YouTube in Brave when I press Del + 5.","When I press F8, select all text and copy it.","Assign F9 to type \"Hello\", wait 500 ms and press Enter."}:new[]{"Open YouTube in Brave.","Select all text and copy it.","Type \"Hello\", wait 500 ms, then press Enter."}){string sample=example;var button=Theme.Button(example,()=>input.Text=sample);button.FontSize=11;button.Padding=new Thickness(9,7,9,7);button.Margin=new Thickness(0,0,7,7);examples.Children.Add(button);}form.Children.Add(examples);
   analyze=Theme.Button("Generate preview",Analyze,true);analyze.HorizontalAlignment=HorizontalAlignment.Left;analyze.Margin=new Thickness(0,7,0,16);form.Children.Add(analyze);
   int optionCount=form.Children.IndexOf(input)-1;var advanced=new StackPanel{Margin=new Thickness(0,12,0,12)};for(int i=0;i<optionCount;i++){var child=form.Children[0];form.Children.RemoveAt(0);advanced.Children.Add(child);}form.Children.Insert(3,new Expander{Header="Trigger, target app & advanced options",Content=advanced,Margin=new Thickness(0,6,0,12),IsExpanded=false});
   if(shapeTrigger!=null){Title="Touchpad action with prompt";((TextBlock)header.Children[0]).Text="Describe your touchpad action";((TextBlock)header.Children[1]).Text="Your recorded shape is the trigger. Describe only what should happen.";save.Content="Use this action";((Expander)form.Children[3]).Header="Target app & advanced options";}
   var previewCard=Theme.Card(preview);previewCard.Margin=new Thickness(0,0,0,12);form.Children.Add(previewCard);preview.Children.Add(Theme.Text("Preview will appear here. No step will run without saving.",13,"Muted"));
   form.Children.Add(Theme.Text("Local LLM can create open, connect, focus, key, text, button, and command steps for all registered applications. No restrictions are applied based on application name. You can preview all generated steps, including commands. The model does not see the application's screen; the application's result is not additionally verified. Escape cancels the remaining steps of the working sequence; sent steps cannot be retrieved. You can also select the rule interpreter while the model is closed.",12,"Muted"));
   ReloadApps(previous!=null?previous.AppPath:initialApp,previous!=null&&previous.Kind=="Browser website"?previous.BrowserPath:previous!=null&&previous.Steps!=null?previous.Steps.Where(s=>!string.IsNullOrWhiteSpace(s.AppPath)).Select(s=>s.AppPath).FirstOrDefault()??initialApp:initialApp);
   input.Text=previous!=null?previous.Prompt??"":initialApp.Length>0?"When I press F8, ":"";
   if(previous!=null&&shapeTrigger==null)trigger.Text=TriggerSpec.Format(previous.Mods,previous.Key,previous.PrefixKey);
   profileChoice.SelectionChanged+=(s,e)=>{if(plan!=null&&plan.Ready)ShowPlan();};enabled.Checked+=(s,e)=>{if(plan!=null&&plan.Ready)ShowPlan();};enabled.Unchecked+=(s,e)=>{if(plan!=null&&plan.Ready)ShowPlan();};application.SelectionChanged+=(s,e)=>Invalidate();targetApplication.SelectionChanged+=(s,e)=>Invalidate();trigger.TextChanged+=(s,e)=>{revision++;if(generation!=null)generation.Cancel();if(plan!=null&&plan.Ready)ShowPlan();else Invalidate();};input.TextChanged+=(s,e)=>Invalidate();
   interpreter.SelectionChanged+=(s,e)=>{Invalidate();analyze.Content=interpreter.SelectedIndex==0?"Generate preview":"Preview with rules";};gpu.Checked+=(s,e)=>{if(saveGpu!=null)saveGpu(true);Invalidate();};gpu.Unchecked+=(s,e)=>{if(saveGpu!=null)saveGpu(false);Invalidate();};
   Closed+=(s,e)=>{closed=true;if(generation!=null)generation.Cancel();engine.Capture=null;engine.Paused=pausedBefore;};Deactivated+=(s,e)=>StopCapture();
   if(previous!=null){plan=new PromptPlan{Binding=previous.Copy()};plan.Notes.Add("Saved plan is displayed. Press the preview button to re-parse.");ShowPlan();}
  }
  void ReloadApps(string selected,string target){
   foreach(var combo in new[]{application,targetApplication}){combo.DisplayMemberPath="Name";combo.IsTextSearchEnabled=true;System.Windows.Controls.TextSearch.SetTextPath(combo,"Name");var itemStyle=new Style(typeof(ComboBoxItem),(Style)Application.Current.FindResource(typeof(ComboBoxItem)));itemStyle.Setters.Add(new Setter(ComboBoxItem.ToolTipProperty,new System.Windows.Data.Binding("Path")));combo.ItemContainerStyle=itemStyle;}
   var sorted=paths.OrderBy(BrowserSupport.DisplayName).ToArray();
   application.ItemsSource=new[]{new AppChoice{Path="",EmptyLabel="Global — in every application"}}.Concat(sorted.Select(p=>new AppChoice{Path=p,Duplicate=sorted.Count(x=>Rules.SameApp(Path.GetFileName(x),Path.GetFileName(p)))>1})).ToList();
   targetApplication.ItemsSource=new[]{new AppChoice{Path="",EmptyLabel="Resolve from prompt"}}.Concat(sorted.Select(p=>new AppChoice{Path=p,Duplicate=sorted.Count(x=>Rules.SameApp(Path.GetFileName(x),Path.GetFileName(p)))>1})).ToList();
   application.SelectedItem=application.Items.Cast<AppChoice>().FirstOrDefault(p=>Rules.SameApp(p.Path,selected))??application.Items[0];targetApplication.SelectedItem=targetApplication.Items.Cast<AppChoice>().FirstOrDefault(p=>Rules.SameApp(p.Path,target))??targetApplication.Items[0];
  }
  void RefreshApps(){string scope=((AppChoice)application.SelectedItem).Path,target=((AppChoice)targetApplication.SelectedItem).Path;foreach(string path in registeredApps().Concat(AppLibrary.Running()))AppLibrary.Remember(paths,path);ReloadApps(scope,target);}
  void Browse(){StopCapture();var picker=new OpenFileDialog{Filter="Applications (*.exe)|*.exe"};if(picker.ShowDialog(this)!=true)return;string scope=((AppChoice)application.SelectedItem).Path;AppLibrary.Remember(paths,picker.FileName);ReloadApps(scope,picker.FileName);}
  void SelectModel(){StopCapture();var picker=new ModelSettingsWindow(options,modelPath){Owner=this};if(picker.ShowDialog()!=true)return;if(saveOptions!=null&&!saveOptions(picker.Result,picker.ModelPath))return;if(saveOptions==null&&saveModel!=null&&!saveModel(picker.ModelPath))return;options=picker.Result;modelPath=picker.ModelPath;modelInfo.Text=options.Describe(modelPath);LocalLlm.Shared.Stop();Invalidate();}
  void Invalidate(){revision++;if(generation!=null)generation.Cancel();plan=null;save.IsEnabled=false;feedback.Text="Request changed. Refresh preview for current plan.";preview.Opacity=0.5;}
  async void Analyze(){await AnalyzeAsync();}
  async Task AnalyzeAsync(){
   if(generation!=null){generation.Cancel();return;}StopCapture();var choice=application.SelectedItem as AppChoice;
   if(interpreter.SelectedIndex==1){plan=PromptParser.Parse(input.Text,paths,choice==null?"":choice.Path,trigger.Text,shapeTrigger);ShowPlan();return;}
   int mine=revision;var cancellation=new CancellationTokenSource();generation=cancellation;save.IsEnabled=false;plan=null;preview.Opacity=0.5;analyze.Content="Processing… Cancel";
   try{
    var destination=targetApplication.SelectedItem as AppChoice;var request=new LlmRequest{Preferences=preferences.Copy(),Prompt=input.Text,Applications=paths.ToList(),SelectedApp=choice==null?"":choice.Path,TargetApp=destination==null?"":destination.Path,Trigger=shapeTrigger==null?trigger.Text:"",ModelPath=modelPath,Gpu=gpu.IsChecked==true,Options=options.Copy(),Gesture=shapeTrigger==null?null:shapeTrigger.Copy()};
    var progress=new Progress<string>(message=>{if(!closed&&mine==revision)feedback.Text=message;});
    var result=await Planner(request,message=>((IProgress<string>)progress).Report(message),cancellation.Token);
    if(closed||mine!=revision)return;plan=result;if(shapeTrigger==null&&plan.Ready&&string.IsNullOrWhiteSpace(trigger.Text))trigger.Text=TriggerSpec.Format(plan.Binding.Mods,plan.Binding.Key,plan.Binding.PrefixKey);ShowPlan();
   }catch(OperationCanceledException){if(!closed&&mine==revision)feedback.Text=cancellation.IsCancellationRequested?"Model request was cancelled. No assignments were saved.":"Model could not complete within time limit. Shorten the request or try again.";}
   catch(Exception error){if(!closed&&mine==revision){preview.Opacity=1;preview.Children.Clear();preview.Children.Add(Theme.Text("Model request could not be completed",20));preview.Children.Add(Theme.Text(error.Message,13));feedback.Text="Check model file, try disabling GPU, or select a rule-based interpreter. Automatic transition was not performed.";}}
   finally{if(ReferenceEquals(generation,cancellation))generation=null;cancellation.Dispose();if(!closed)analyze.Content=interpreter.SelectedIndex==0?"Generate preview":"Preview with rules";}
  }
  void ShowPlan(){
   preview.Children.Clear();preview.Opacity=1;
   if(!plan.Ready){preview.Children.Add(Theme.Text("Let's clarify a few details",19));foreach(string error in plan.Errors){var text=Theme.Text("• "+error,13);text.Margin=new Thickness(0,10,0,0);preview.Children.Add(text);}feedback.Text="You can use examples or select the key/application at the top.";save.IsEnabled=false;return;}
   if(old!=null){plan.Binding.Id=old.Id;plan.Binding.PassThrough=old.PassThrough;}
   plan.Binding.Enabled=enabled.IsChecked==true;
   plan.Binding.ProfileId=profileChoice.SelectedValue as string??"";
   if(shapeTrigger!=null){plan.Binding.Gesture=shapeTrigger.Copy();plan.Binding.Key=plan.Binding.Mods=plan.Binding.PrefixKey=0;}
   TriggerSpec gesture;string keyText=string.IsNullOrWhiteSpace(trigger.Text)?TriggerSpec.FindInPrompt(input.Text):trigger.Text;
   if(shapeTrigger==null&&keyText.Length>0){if(!TriggerSpec.TryParse(keyText,out gesture)){feedback.Text="Shortcut could not be understood. Press a key to save, or type something like Ctrl+Subtract.";save.IsEnabled=false;return;}plan.Binding.Mods=gesture.Mods;plan.Binding.Key=gesture.Key;plan.Binding.PrefixKey=gesture.Prefix;}
   else if(shapeTrigger==null&&old!=null){feedback.Text="Select the key that will run the shortcut.";save.IsEnabled=false;return;}
   preview.Children.Add(Theme.Text(plan.Binding.Combo+"  →  "+plan.Binding.Scope,22));preview.Children.Add(Theme.Text(plan.Binding.AppPath.Length>0?plan.Binding.AppPath:"Valid in all applications",11,"Muted"));
   if(plan.Binding.Kind=="Browser website"){preview.Children.Add(Theme.Text("Target: "+BrowserSupport.DisplayName(plan.Binding.BrowserPath),18));preview.Children.Add(Theme.Text(plan.Binding.Target,15));preview.Children.Add(Theme.Text(plan.Binding.BrowserPath,11,"Muted"));}
   else if(plan.Binding.Kind=="Workflow"){int index=0;foreach(var step in plan.Binding.Steps??new List<ActionStep>()){var line=Theme.Text((++index)+".  "+step.Summary,15);line.Margin=new Thickness(0,10,0,0);preview.Children.Add(line);}}
   else preview.Children.Add(Theme.Text(plan.Binding.Summary,15));
   foreach(string note in plan.Notes.Distinct()){var line=Theme.Text(note,12,"Muted");line.Margin=new Thickness(0,10,0,0);preview.Children.Add(line);}
   var conflicts=Rules.Conflicts(plan.Binding,existing);conflicts.AddRange(ShortcutConflicts.ForBinding(plan.Binding,external()));string errorMessage=Rules.Validate(plan.Binding,existing);string activation=shapeTrigger==null?Profiles.Describe(plan.Binding,profileChoice.Items.Cast<ShortcutProfile>(),currentProfile,pausedBefore):"Recorded touchpad shape • "+((profileChoice.SelectedItem as ShortcutProfile)==null?"All profiles":((ShortcutProfile)profileChoice.SelectedItem).Name);feedback.Text=errorMessage??activation+"\n"+(conflicts.Count>0?string.Join("\n",conflicts):shapeTrigger==null?"Plan is ready. It will only work with the key you defined when saved.":"Action is ready. Confirm it here, then save your shape shortcut.");save.IsEnabled=errorMessage==null;
  }
  void Commit(){
   StopCapture();if(plan==null||!plan.Ready)return;ShowPlan();if(!save.IsEnabled)return;string error=Rules.Validate(plan.Binding,existing);if(error!=null){feedback.Text=error;return;}
   var warnings=Rules.Conflicts(plan.Binding,existing);warnings.AddRange(ShortcutConflicts.ForBinding(plan.Binding,external()));if(plan.Binding.Gesture==null&&plan.Binding.PrefixKey==0&&plan.Binding.Mods==0&&plan.Binding.Key<112)warnings.Add("A key without modifiers may affect normal text input.");
   if(warnings.Count>0&&MessageBox.Show(this,string.Join("\n\n",warnings)+"\n\nSave this plan?","Conflict information",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
   Result=plan.Binding;DialogResult=true;
  }
  void CaptureKey(){StopCapture();recording=true;record.Content="Listening…";engine.Capture=(mods,key,prefix,altgr)=>{if(!recording)return;StopCapture();if(key==27)return;if(altgr){feedback.Text="Use Left Alt instead of AltGr.";return;}trigger.Text=TriggerSpec.Format(mods,key,prefix);};}
  void StopCapture(){recording=false;engine.Capture=null;record.Content="Press key";}
  internal void PreviewForTest(string text,string key=""){interpreter.SelectedIndex=1;input.Text=text;trigger.Text=key;plan=PromptParser.Parse(input.Text,paths,((AppChoice)application.SelectedItem).Path,trigger.Text,shapeTrigger);ShowPlan();}
  internal PromptPlan PlanForTest(){return plan;}
  internal Task StartModelForTest(string text){interpreter.SelectedIndex=0;input.Text=text;return AnalyzeAsync();}
  internal void ChangePromptForTest(string text){input.Text=text;}
  internal bool CanSaveForTest(){return save.IsEnabled;}
  internal string FeedbackForTest {get{return feedback.Text;}}
  internal string TriggerForTest(){return trigger.Text;}
  internal void TriggerForTest(string value){trigger.Text=value;}
  internal void EnabledForTest(bool value){enabled.IsChecked=value;}
  internal void ProfileForTest(string value){profileChoice.SelectedValue=value;}
  internal void SaveForTest(){Commit();}
  internal string TargetForTest(){return ((AppChoice)targetApplication.SelectedItem).Path;}
  internal bool SelectTargetForTest(string path){RefreshApps();targetApplication.SelectedItem=targetApplication.Items.Cast<AppChoice>().FirstOrDefault(p=>Rules.SameApp(p.Path,path));return targetApplication.SelectedItem!=null;}
  class AppChoice {public string Path,EmptyLabel;public bool Duplicate;public string Name{get{return Path.Length==0?EmptyLabel:BrowserSupport.DisplayName(Path)+(Duplicate?" — "+Path:"");}}public override string ToString(){return Name;}}
 }
}
