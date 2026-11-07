using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Interop;
using Forms=System.Windows.Forms;

namespace AkilliKisayol {
 public static class Program {
  [STAThread] public static int Main(string[] args) {
   var culture=System.Globalization.CultureInfo.GetCultureInfo("en-US");System.Globalization.CultureInfo.DefaultThreadCurrentCulture=culture;System.Globalization.CultureInfo.DefaultThreadCurrentUICulture=culture;Thread.CurrentThread.CurrentCulture=culture;Thread.CurrentThread.CurrentUICulture=culture;
   if(args.Contains("--device-model-test"))return ProductTests.Real();
   if(args.Contains("--server-model-test"))return ModernTests.Real();
   if(args.Contains("--gesture-model-test"))return GesturePromptTests.Real();
   if(args.Contains("--compat-model-test"))return CompatibilityTests.Real(args.Length>1?args[1]:Storage.Read(Storage.FilePath).ModelPath);
   if(args.Contains("--compat-api-test"))return CompatibilityTests.Real(args.Length>1?args[1]:Storage.Read(Storage.FilePath).ModelPath,true);
   if(args.Contains("--activation-model-test"))return ActivationTests.Real();
   if(args.Contains("--features-model-test"))return FeatureTests.Real();
   if(args.Contains("--model-folder-test"))return ModelFolderTests.Real();
   if(args.Contains("--self-test")) return Tests.Run();
   if(args.Contains("--llm-test")) return LlmTests.Real(args.Length>1?args[1]:null);
   if(args.Contains("--brave-test"))return BrowserTests.Real();if(args.Contains("--app-model-test"))return AppIntegrationTests.Real();
   bool created; using(var mutex=new Mutex(true,"Local\\AkilliKisayolManager",out created)) {
    if(!created) {MessageBox.Show("Macro+ is already running. Check the system tray.");return 0;}
    try {
     var app=new Application();app.ShutdownMode=ShutdownMode.OnMainWindowClose;
     Theme.Install(app);string notice;var loaded=Storage.LoadRecoverable(Storage.FilePath,out notice);var main=new MainWindow(loaded);if(notice.Length>0)main.Loaded+=(s,e)=>MessageBox.Show(main,notice,"Makro+ • Settings recovery");app.Run(main);return 0;
    } catch(Exception ex) {MessageBox.Show(ex.Message+"\n\nSettings file: "+Storage.FilePath+"\nIf there is a corrupted file, retrieve your backup and examine the .bak copy.","Macro+ could not start");return 1;}
   }
  }
 }
 public static class Theme {
  public static void Install(Application app) {
   app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
   <Style TargetType='Window'><Setter Property='FontFamily' Value='Segoe UI'/><Setter Property='FontSize' Value='13'/><Setter Property='Background' Value='{DynamicResource Bg}'/><Setter Property='Foreground' Value='{DynamicResource Ink}'/></Style>
   <Style TargetType='TextBlock'/><Style TargetType='Expander'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Margin' Value='0,8,0,8'/></Style>
   <Style TargetType='Control'><Setter Property='Foreground' Value='{DynamicResource Ink}'/></Style>
   <Style TargetType='Button'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Background' Value='{DynamicResource Surface}'/><Setter Property='Padding' Value='15,10'/><Setter Property='Margin' Value='0,0,8,0'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='frame' Background='{TemplateBinding Background}' CornerRadius='7' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='frame' Property='Opacity' Value='0.75'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='frame' Property='Opacity' Value='0.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
   <Style x:Key='NavigationButton' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='frame' Background='{TemplateBinding Background}' CornerRadius='7' BorderThickness='1' BorderBrush='Transparent'><Grid><Border x:Name='rail' Width='3' HorizontalAlignment='Left' Margin='0,7,0,7' CornerRadius='2' Background='{DynamicResource Accent}' Visibility='Collapsed'/><ContentPresenter Margin='{TemplateBinding Padding}' VerticalAlignment='Center' HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}'/></Grid></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='frame' Property='Background' Value='{DynamicResource Surface}'/></Trigger><Trigger Property='Tag' Value='selected'><Setter TargetName='frame' Property='Background' Value='{DynamicResource Selection}'/><Setter TargetName='frame' Property='BorderBrush' Value='{DynamicResource Line}'/><Setter TargetName='rail' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='frame' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='frame' Property='Opacity' Value='0.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
   <Style x:Key='WindowButton' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='frame' Background='{TemplateBinding Background}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='frame' Property='Background' Value='{DynamicResource Surface}'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='frame' Property='Background' Value='{DynamicResource Selection}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
   <Style TargetType='TextBox'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Background' Value='{DynamicResource Surface}'/><Setter Property='CaretBrush' Value='{DynamicResource Ink}'/><Setter Property='BorderBrush' Value='{DynamicResource Line}'/><Setter Property='Padding' Value='10,8'/><Setter Property='VerticalContentAlignment' Value='Center'/></Style>
   <Style TargetType='ComboBox'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Background' Value='{DynamicResource Surface}'/><Setter Property='Padding' Value='10,8'/><Setter Property='MinHeight' Value='38'/><Setter Property='ScrollViewer.VerticalScrollBarVisibility' Value='Auto'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBox'><Grid><ToggleButton Focusable='False' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'><ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border Background='{DynamicResource Surface}' BorderBrush='{DynamicResource Line}' BorderThickness='1' CornerRadius='7'><TextBlock Text='⌄' Foreground='{DynamicResource Muted}' HorizontalAlignment='Right' Margin='0,0,12,0' VerticalAlignment='Center'/></Border></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter Margin='12,8,32,8' Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' ContentTemplateSelector='{TemplateBinding ItemTemplateSelector}' IsHitTestVisible='False' VerticalAlignment='Center'/><Popup x:Name='PART_Popup' IsOpen='{TemplateBinding IsDropDownOpen}' Placement='Bottom' AllowsTransparency='True' Focusable='False' PopupAnimation='Fade'><Border Background='{DynamicResource Panel}' BorderBrush='{DynamicResource Line}' BorderThickness='1' CornerRadius='7' MinWidth='{Binding ActualWidth,RelativeSource={RelativeSource TemplatedParent}}' MaxHeight='360'><ScrollViewer CanContentScroll='True'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/></ScrollViewer></Border></Popup></Grid><ControlTemplate.Triggers><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
   <Style TargetType='ComboBoxItem'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Padding' Value='12,9'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBoxItem'><Border x:Name='item' Background='Transparent' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='item' Property='Background' Value='{DynamicResource Selection}'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='item' Property='Background' Value='{DynamicResource Selection}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
   <Style TargetType='ListBox'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Background' Value='{DynamicResource Panel}'/><Setter Property='BorderBrush' Value='{DynamicResource Line}'/><Setter Property='Padding' Value='6'/></Style>
   <Style TargetType='ListBoxItem'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Padding' Value='6,5'/><Style.Triggers><Trigger Property='IsSelected' Value='True'><Setter Property='Background' Value='{DynamicResource Selection}'/></Trigger></Style.Triggers></Style>
   <Style TargetType='CheckBox'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Margin' Value='0,8,0,8'/></Style>
   <Style TargetType='DataGrid'><Setter Property='Background' Value='{DynamicResource Panel}'/><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='BorderThickness' Value='0'/><Setter Property='RowBackground' Value='{DynamicResource Panel}'/><Setter Property='AlternatingRowBackground' Value='{DynamicResource Panel}'/><Setter Property='HorizontalGridLinesBrush' Value='{DynamicResource Line}'/><Setter Property='GridLinesVisibility' Value='Horizontal'/><Setter Property='HeadersVisibility' Value='Column'/><Setter Property='RowHeight' Value='62'/><Setter Property='ColumnHeaderHeight' Value='42'/><Setter Property='AutoGenerateColumns' Value='False'/><Setter Property='CanUserAddRows' Value='False'/><Setter Property='IsReadOnly' Value='True'/><Setter Property='SelectionMode' Value='Single'/></Style>
   <Style TargetType='DataGridColumnHeader'><Setter Property='Background' Value='{DynamicResource Surface}'/><Setter Property='Foreground' Value='{DynamicResource Muted}'/><Setter Property='Padding' Value='12,8'/><Setter Property='BorderThickness' Value='0'/><Setter Property='FontWeight' Value='SemiBold'/></Style>
   <Style TargetType='DataGridCell'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Padding' Value='12,0'/><Setter Property='VerticalContentAlignment' Value='Center'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='DataGridCell'><Border Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}'><ContentPresenter VerticalAlignment='Center'/></Border></ControlTemplate></Setter.Value></Setter></Style>
   <Style TargetType='DataGridRow'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Style.Triggers><Trigger Property='IsSelected' Value='True'><Setter Property='Background' Value='{DynamicResource Selection}'/><Setter Property='Foreground' Value='{DynamicResource Ink}'/></Trigger></Style.Triggers></Style>
   </ResourceDictionary>"));
  }
  public static void Apply(bool dark) {
   Apply(null,dark);
  }
  public static void Apply(string id,bool dark,IEnumerable<ThemePalette> custom=null) {
   var r=Application.Current.Resources;
   var palette=ThemeCatalog.Resolve(id,dark,custom);foreach(var color in palette.Colors())r[color.Key]=palette.Brush(color.Value);
  }
  public static TextBlock Text(string text,double size=13,string resource=null) {var t=new TextBlock{Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap};if(resource!=null)t.SetResourceReference(TextBlock.ForegroundProperty,resource);return t;}
  public static Button Button(string text,Action click,bool accent=false) {var b=new Button{Content=text,VerticalContentAlignment=VerticalAlignment.Center};if(accent){b.SetResourceReference(Control.BackgroundProperty,"Accent");b.SetResourceReference(Control.ForegroundProperty,"AccentInk");}b.Click+=(s,e)=>click();return b;}
  public static Border Card(UIElement child,string resource="Panel") {var b=new Border{Child=child,CornerRadius=new CornerRadius(12),Padding=new Thickness(20),Margin=new Thickness(0,0,14,0)};b.SetResourceReference(Border.BackgroundProperty,resource);return b;}
 }
 public class Row {
  public string Active{get;set;} public string Name{get;set;} public string Combo{get;set;} public string Action{get;set;} public string Scope{get;set;} public string Source{get;set;} public string Status{get;set;}
  public Binding Binding;
  public SystemShortcut SystemShortcut;
 }
 public partial class MainWindow : Window {
  readonly State state;readonly string settingsFile;readonly KeyboardEngine engine;readonly Forms.NotifyIcon tray;
  readonly System.Windows.Threading.DispatcherTimer historySave=new System.Windows.Threading.DispatcherTimer();bool historyDirty;
  readonly Grid content=new Grid();readonly TextBlock heading=Theme.Text("",28);readonly TextBlock subtitle=Theme.Text("",13,"Muted");
  readonly TextBox search=new TextBox{MinWidth=210,Width=300};readonly ComboBox source=new ComboBox{Width=180,Margin=new Thickness(10,0,0,0)};
  readonly DataGrid table=new DataGrid(); readonly TextBlock detail=Theme.Text("Select a record.",12,"Muted");
  readonly TextBlock count=Theme.Text("",12,"Muted");readonly TextBlock status=Theme.Text("",12,"Muted");
  readonly List<SystemShortcut> detected=new List<SystemShortcut>();readonly List<string> scanWarnings=new List<string>();readonly Dictionary<string,string> probes=new Dictionary<string,string>();bool scanning,closed;string scanInfo="Computer shortcuts have not yet been scanned.";
  string view="Home";Button pause,editRow,toggleRow,deleteRow,restoreRow,sourceRow;
  public MainWindow(State data,string settingsPath=null) {
   settingsFile=settingsPath??Storage.FilePath;state=data;ThemeCatalog.Normalize(state);if(state.ModelOptions==null)state.ModelOptions=new ModelOptions();Actions.StepIntervalMs=state.StepIntervalMs??15;Profiles.Normalize(state);Theme.Apply(state.ThemeId,state.Dark,state.CustomThemes);Style=(Style)Application.Current.FindResource(typeof(Window));Title="Makro+";Width=1320;Height=860;MinWidth=1000;MinHeight=650;WindowStartupLocation=WindowStartupLocation.CenterScreen;Brand.Fit(this);
   historySave.Interval=TimeSpan.FromMilliseconds(750);historySave.Tick+=(s,e)=>{historySave.Stop();if(historyDirty){historyDirty=false;Save();}};
   engine=new KeyboardEngine{Bindings=()=>Profiles.Effective(state),Paused=state.Paused,AllowRemappedInput=state.AllowRemappedInput};
   engine.Fire=(b,w)=>Actions.Run(b,w,result=>Log(b.Name,result));engine.Fault=message=>Log("Input engine",message);
   InitializeFeatures();
   var root=new Grid();root.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(264)});root.ColumnDefinitions.Add(new ColumnDefinition());Content=root;
   var side=new DockPanel{Margin=new Thickness(16,35,16,18)};
   var brand=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(5,0,0,20)};brand.Children.Add(Brand.Picture(42));var brandWords=new StackPanel{Margin=new Thickness(9,0,0,0),VerticalAlignment=VerticalAlignment.Center};brandWords.Children.Add(Theme.Text("MAKRO+",23));brandWords.Children.Add(Theme.Text("Make it yours.",11,"Muted"));brand.Children.Add(brandWords);DockPanel.SetDock(brand,Dock.Top);side.Children.Add(brand);
   var foot=new StackPanel();foot.Children.Add(ProfileSelector());pause=Theme.Button("",()=>{state.Paused=!state.Paused;engine.Paused=state.Paused;if(state.Paused)Actions.CancelWorkflow();Save();UpdatePause();Render();});foot.Children.Add(pause);foot.Children.Add(Theme.Text("MAKRO+ • v"+Brand.Version,10,"Muted"));DockPanel.SetDock(foot,Dock.Bottom);side.Children.Add(foot);
   side.Children.Add(Navigation());var sidebar=new Border{Child=side,BorderThickness=new Thickness(0,0,1,0)};sidebar.SetResourceReference(Border.BackgroundProperty,"Panel");sidebar.SetResourceReference(Border.BorderBrushProperty,"Line");root.Children.Add(sidebar);
   Grid.SetColumn(content,1);content.Margin=new Thickness(26,35,26,20);root.Children.Add(content);InstallWindowFrame(root);
   source.ItemsSource=new[]{"Show all resources","My assignments","Known defaults","Detected on this computer","Computer + defaults"};source.SelectedIndex=0;
   source.SelectionChanged+=(s,e)=>Refresh();search.TextChanged+=(s,e)=>Refresh();
   foreach(var pair in new[]{new[]{"Active","Status"},new[]{"Name","Assignment name"},new[]{"Combo","Combination"},new[]{"Action","Action"},new[]{"Scope","Scope"},new[]{"Source","Source"},new[]{"Status","Details"}}) {
    var style=new Style(typeof(TextBlock));style.Setters.Add(new Setter(TextBlock.TextWrappingProperty,TextWrapping.Wrap));style.Setters.Add(new Setter(TextBlock.FontSizeProperty,12.0));style.Setters.Add(new Setter(TextBlock.ToolTipProperty,new System.Windows.Data.Binding(pair[0])));
    var col=new DataGridTextColumn{Header=pair[1],Binding=new System.Windows.Data.Binding(pair[0]),ElementStyle=style,Width=new DataGridLength(pair[0]=="Name"||pair[0]=="Action"?1.4:1,DataGridLengthUnitType.Star)};if(pair[0]=="Active")col.Width=65;table.Columns.Add(col);
   }
   table.SelectionChanged+=(s,e)=>{var row=table.SelectedItem as Row;detail.Text=row==null?"Select a record.":row.Binding!=null?row.Binding.Name+" • "+row.Binding.Kind+"\n"+Profiles.Describe(row.Binding,state.Profiles,state.ActiveProfileId,state.Paused)+"\n"+row.Binding.Summary+"\n"+(row.Binding.AppPath.Length==0?"Global scope":row.Binding.AppPath):row.SystemShortcut!=null?row.SystemShortcut.Description+"\n"+row.SystemShortcut.FilePath+"\n"+(row.SystemShortcut.Editable?"Edit: changes the actual key assignment in the source file.":"Assign to new key: creates a personal assignment that triggers the command with a different key; the source is preserved by default."):row.Action;UpdateRowActions();};
   table.MouseDoubleClick+=(s,e)=>EditSelected();
   tray=new Forms.NotifyIcon{Text="Makro+",Icon=Brand.TrayIcon(),Visible=true};
   var menu=new Forms.ContextMenuStrip();menu.Items.Add("Show",null,(s,e)=>Restore());menu.Items.Add("Pause / resume",null,(s,e)=>pause.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));var profileMenu=new Forms.ToolStripMenuItem("Profile");profileMenu.DropDownOpening+=(s,e)=>{profileMenu.DropDownItems.Clear();foreach(var item in state.Profiles){string id=item.Id;var choice=new Forms.ToolStripMenuItem(item.Name){Checked=id==state.ActiveProfileId};choice.Click+=(a,b)=>SwitchProfile(id);profileMenu.DropDownItems.Add(choice);}};menu.Items.Add(profileMenu);menu.Items.Add("Toggle touchpad shapes",null,(s,e)=>{SetGestureEnabled(!state.Gestures.Enabled);Render();});menu.Items.Add("Exit",null,(s,e)=>Close());tray.ContextMenuStrip=menu;tray.DoubleClick+=(s,e)=>Restore();
   Closed+=(s,e)=>{closed=true;historySave.Stop();if(historyDirty)Save();gestures.Dispose();guide.Dispose();Actions.CancelWorkflow();LocalLlm.Shared.Dispose();engine.Dispose();tray.Dispose();};UpdatePause();Render();
   Loaded+=(s,e)=>{if(!Environment.GetCommandLineArgs().Contains("--self-test"))ScanLinks();};
   foreach(var b in state.Bindings.Where(b=>b.Enabled)) {string error=Rules.Validate(b,state.Bindings);if(error!=null){b.Enabled=false;Log(b.Name,"Safe install: disabled • "+error);}}
   Refresh();
  }
  void Restore(){Show();WindowState=WindowState.Normal;Activate();}
  void UpdatePause(){pause.Content=state.Paused?"▶ Resume shortcuts":"Ⅱ Pause shortcuts";string text="Makro+ • "+Profiles.Name(state)+(state.Paused?" • paused":"");tray.Text=text.Length>63?text.Substring(0,63):text;}
  bool Save(){try{Storage.Write(state,settingsFile);return true;}catch(Exception e){MessageBox.Show(this,"Could not save settings: "+e.Message,"Disk error");return false;}}
  void Log(string name,string result){state.History.Insert(0,new Activity{Time=DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"),Name=name,Result=result});if(state.History.Count>200)state.History.RemoveRange(200,state.History.Count-200);historyDirty=true;if(!historySave.IsEnabled)historySave.Start();status.Text=result;if(view=="History")Render();}
  StackPanel Header(string title,string note){var p=new StackPanel{Margin=new Thickness(0,0,0,22)};heading.Text=title;subtitle.Text=note;p.Children.Add(heading);p.Children.Add(subtitle);return p;}
  void Render(){if(gestures!=null)gestures.Refresh();
   UpdateNavigation();
   content.Children.Clear();content.RowDefinitions.Clear();var dock=new DockPanel();content.Children.Add(dock);
   // Reusable controls must be detached before adding to their new parents.
   Detach(heading);Detach(subtitle);Detach(table);Detach(search);Detach(source);Detach(detail);Detach(count);Detach(status);
   var header=Header(view,PageNote());DockPanel.SetDock(header,Dock.Top);dock.Children.Add(header);
   if(view=="Home"){dock.Children.Add(Welcome());return;}
   if(view=="AI model"){dock.Children.Add(ModelPanel());return;}
   if(view=="Transfer and diagnostics"){dock.Children.Add(DevicePanel());return;}
   if(view=="Touchpad gestures"){dock.Children.Add(GesturePanel());return;}
   if(view=="Shortcut guide"){dock.Children.Add(GuideSettings());return;}
   if(view=="Settings"){dock.Children.Add(Settings());return;}
   if(view=="Appearance"){dock.Children.Add(new ScrollViewer{Content=Appearance(),VerticalScrollBarVisibility=ScrollBarVisibility.Auto});return;}
   if(view=="Applications"){dock.Children.Add(ApplicationPanel(false));return;}
   if(view=="History"){
    var stack=new StackPanel();stack.Children.Add(Theme.Button("Clear history",()=>{state.History.Clear();Save();Render();}));
    foreach(var h in state.History){var item=new StackPanel{Margin=new Thickness(0,12,0,0)};item.Children.Add(Theme.Text(h.Time+"  •  "+h.Name,13));item.Children.Add(Theme.Text(h.Result,12,"Muted"));stack.Children.Add(Theme.Card(item));}
    if(state.History.Count==0)stack.Children.Add(Theme.Text("No operations yet. Results will appear here when a shortcut is triggered."));
    dock.Children.Add(new ScrollViewer{Content=stack,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});return;
   }
   if(view=="Conflicts"){
    var stack=new StackPanel();stack.Children.Add(ScanBar());var check=Theme.Button("Check current system compatibility",ProbeAll);check.Margin=new Thickness(0,12,0,12);stack.Children.Add(check);
    var conflicts=ShortcutConflicts.Find(Profiles.Effective(state),AllSystem());stack.Children.Add(Theme.Text(conflicts.Count+" match • personal assignments + entries on the computer + known defaults",16));
    foreach(var conflict in conflicts.OrderBy(c=>c.Severity=="CONFLICT"?0:c.Severity=="PRIORITY"?1:2)){var card=Theme.Card(Theme.Text(conflict.ToString(),14));card.Margin=new Thickness(0,12,0,0);stack.Children.Add(card);}
    foreach(var b in state.Bindings.Where(x=>x.Enabled)){string reserved=b.Gesture==null?Rules.Reserved(b.Mods,b.Key):ShapeRecognition.Validate(b.Gesture);if(reserved!=null)stack.Children.Add(Theme.Text(b.Name+" • "+reserved,13));if(probes.ContainsKey(b.Id))stack.Children.Add(Theme.Text(b.Name+" • "+probes[b.Id],13));}
    if(conflicts.Count==0)stack.Children.Add(Theme.Text("No conflicts found among the read recordings.",14));
    stack.Children.Add(Theme.Text("CONFLICT: same key and overlapping scope. PRIORITY: personal application scope takes precedence over general assignment. POSSIBLE: default, condition, or array start match. Windows does not list all hooks active when external applications run.",12,"Muted"));
    dock.Children.Add(new ScrollViewer{Content=stack,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});return;
   }
   if(view!="Computer shortcuts"){var quick=new WrapPanel{Margin=new Thickness(0,0,0,16)};quick.Children.Add(Theme.Button("Create with Prompt",()=>OpenPrompt()));quick.Children.Add(Theme.Button("Record macro",()=>OpenMacro()));quick.Children.Add(Theme.Button("By application",()=>Navigate("By application")));quick.Children.Add(Theme.Button("Check conflicts",()=>Navigate("Conflicts")));DockPanel.SetDock(quick,Dock.Top);dock.Children.Add(quick);}else {var scan=ScanBar();DockPanel.SetDock(scan,Dock.Top);dock.Children.Add(scan);}
   var bar=new DockPanel{Margin=new Thickness(0,0,0,16)};var add=Theme.Button("＋ Add shortcut",()=>Edit(null),true);DockPanel.SetDock(add,Dock.Right);bar.Children.Add(add);var filters=new StackPanel{Orientation=Orientation.Horizontal};var hint=Theme.Text("Search",13,"Muted");hint.VerticalAlignment=VerticalAlignment.Center;hint.Margin=new Thickness(0,0,10,0);filters.Children.Add(hint);search.ToolTip="Search by name, trigger, scope or target";filters.Children.Add(search);filters.Children.Add(source);bar.Children.Add(filters);DockPanel.SetDock(bar,Dock.Top);dock.Children.Add(bar);
   var bottom=new StackPanel{Margin=new Thickness(0,14,0,0)};bottom.Children.Add(count);detail.Margin=new Thickness(0,10,0,12);bottom.Children.Add(detail);var actions=new WrapPanel();editRow=Theme.Button("Edit",EditSelected);toggleRow=Theme.Button("Enable / disable",ToggleSelected);deleteRow=Theme.Button("Delete",DeleteSelected);actions.Children.Add(editRow);actions.Children.Add(toggleRow);actions.Children.Add(deleteRow);actions.Children.Add(Theme.Button("Rescan",ScanLinks));restoreRow=Theme.Button("Restore backup",RestoreSource);sourceRow=Theme.Button("Show source",ShowSource);actions.Children.Add(restoreRow);actions.Children.Add(sourceRow);actions.Children.Add(Theme.Button("Shrink to tray",()=>Hide()));bottom.Children.Add(actions);status.Margin=new Thickness(0,10,0,0);bottom.Children.Add(status);DockPanel.SetDock(bottom,Dock.Bottom);dock.Children.Add(bottom);dock.Children.Add(table);Refresh();
  }
  static void Detach(UIElement element){var parent=LogicalTreeHelper.GetParent(element);if(parent is Panel)((Panel)parent).Children.Remove(element);else if(parent is Decorator)((Decorator)parent).Child=null;}
  Border Stat(string value,string label){var s=new StackPanel();s.Children.Add(Theme.Text(value,30));s.Children.Add(Theme.Text(label,10,"Muted"));return Theme.Card(s);}
  void Refresh(){
   if(table==null||(view!="All shortcuts"&&view!="Keyboard shortcuts"&&view!="Mouse shortcuts"&&view!="By application"&&view!="Computer shortcuts"))return;var rows=new List<Row>();
   if(source.SelectedIndex==0||source.SelectedIndex==1)rows.AddRange(state.Bindings.Select(b=>new Row{Active=b.Enabled&&!state.Paused&&Profiles.Active(b,state.Profiles.First(p=>p.Id==state.ActiveProfileId))?"●":"○",Name=b.Name,Combo=b.Combo,Action=b.Kind+" • "+b.Summary,Scope=b.Scope,Source="Makro+ assignment",Status=Profiles.Describe(b,state.Profiles,state.ActiveProfileId,state.Paused),Binding=b}));
   if(source.SelectedIndex==0||source.SelectedIndex==2||source.SelectedIndex==4)rows.AddRange(ShortcutConflicts.References().Select(SystemRow));
   if(source.SelectedIndex==0||source.SelectedIndex==3||source.SelectedIndex==4)rows.AddRange(detected.Select(SystemRow));
   var conflicts=ShortcutConflicts.Find(Profiles.Effective(state),AllSystem());var matchesById=new Dictionary<string,int>();foreach(var conflict in conflicts){foreach(string id in new[]{conflict.FirstId,conflict.SecondId}.Where(id=>id!=null).Distinct()){int matches;matchesById.TryGetValue(id,out matches);matchesById[id]=matches+1;}}foreach(var row in rows){string id=row.Binding!=null?row.Binding.Id:row.SystemShortcut==null?"":row.SystemShortcut.Id;int matches;if(matchesById.TryGetValue(id,out matches)&&matches>0)row.Status+=" • "+matches+" match";}
   if(view=="Mouse shortcuts")rows=rows.Where(r=>r.Binding!=null&&(InputKeys.IsPointer(r.Binding.Key)||InputKeys.IsPointer(r.Binding.SendKey)||(r.Binding.Steps??new List<ActionStep>()).Any(step=>step.Kind.StartsWith("Mouse ")||step.Kind=="Scroll"||step.Kind=="Click element"||InputKeys.IsPointer(step.Key)))).ToList();
   if(view=="Keyboard shortcuts")rows=rows.Where(r=>InputSections.Keyboard(r.Binding,r.SystemShortcut)).ToList();
   string query=search.Text??"";rows=rows.Where(r=>string.Join(" ",new[]{r.Name,r.Combo,r.Action,r.Scope,r.Source}).IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0).ToList();
   table.ItemsSource=view=="By application"?rows.OrderBy(x=>x.Scope).ThenBy(x=>x.Name).ToList():rows;
   count.Text=rows.Count+" entry • "+scanInfo;UpdateRowActions();
  }
  Row Selected(){return table.SelectedItem as Row;}
  void EditSelected(){var r=Selected();if(r==null)return;if(r.Binding!=null)Edit(r.Binding);else if(r.SystemShortcut!=null)EditSystem(r.SystemShortcut);}
  void Edit(Binding old,Binding initial=null){
   if(old!=null&&old.Gesture!=null){OpenGesture(old);return;}
   if(old!=null&&old.IsMacro){OpenMacro(old);return;}
   if(old!=null&&(old.Kind=="Workflow"||old.Kind=="Browser website")){OpenPrompt(old.AppPath,old);return;}
   var dialog=new Editor(engine,old,state.Bindings,state.ApplicationPaths,initial,()=>AllSystem(),state.Profiles,state.ActiveProfileId){Owner=this};if(dialog.ShowDialog()!=true)return;
   StoreBinding(dialog.Result,old);
  }
  void StoreBinding(Binding b,Binding old){
   int index=old==null?-1:state.Bindings.IndexOf(old);if(index<0)state.Bindings.Add(b);else state.Bindings[index]=b;
   var previousApps=state.ApplicationPaths.ToList();if(b.AppPath.Length>0)AppLibrary.Remember(state.ApplicationPaths,b.AppPath);if(b.Kind=="Application")AppLibrary.Remember(state.ApplicationPaths,b.Target);
   if(b.Kind=="Browser website")AppLibrary.Remember(state.ApplicationPaths,b.BrowserPath);if(b.Steps!=null)foreach(var step in b.Steps)if(!string.IsNullOrWhiteSpace(step.AppPath))AppLibrary.Remember(state.ApplicationPaths,step.AppPath);
   var previousPreferences=state.Preferences.Copy();PreferenceMemory.Learn(state.Preferences,b);
   if(!Save()){state.Preferences=previousPreferences;if(index<0)state.Bindings.Remove(b);else state.Bindings[index]=old;state.ApplicationPaths=previousApps;return;}
   probes.Clear();Log(b.Name,"Shortcut saved • "+b.Combo+" • "+b.Scope);Render();
  }
  void OpenPrompt(string application="",Binding old=null){var dialog=CreatePromptWindow(application,old);if(dialog.ShowDialog()==true)StoreBinding(dialog.Result,old);}
  PromptWindow CreatePromptWindow(string application="",Binding old=null,ShapeTemplate gesture=null){return new PromptWindow(engine,gesture==null?state.Bindings:new List<Binding>(),state.ApplicationPaths,application,old,state.ModelPath,path=>{string previous=state.ModelPath;state.ModelPath=path;if(Save())return true;state.ModelPath=previous;return false;},()=>state.ApplicationPaths,()=>AllSystem(),state.Profiles,state.ActiveProfileId,state.Preferences,state.UseGpu??true,value=>{var previous=state.UseGpu;state.UseGpu=value;if(!Save())state.UseGpu=previous;},state.ModelOptions,(options,path)=>{var oldOptions=state.ModelOptions;string oldPath=state.ModelPath;state.ModelOptions=options;state.ModelPath=path;if(Save())return true;state.ModelOptions=oldOptions;state.ModelPath=oldPath;return false;},gesture){Owner=this};}
  void ToggleSelected(){var r=Selected();if(r!=null&&r.SystemShortcut!=null){ToggleSystem(r.SystemShortcut);return;}if(r==null||r.Binding==null)return;SetActivation(r.Binding);}
  void SetActivation(Binding b){
   bool previous=b.Enabled,paused=state.Paused;string profile=state.ActiveProfileId;bool outside=!string.IsNullOrEmpty(b.ProfileId)&&b.ProfileId!=profile;
   b.Enabled=outside||paused||!b.Enabled;if(outside)state.ActiveProfileId=b.ProfileId;if(b.Enabled)state.Paused=false;
   string error=b.Enabled?Rules.Validate(b,state.Bindings):null;
   if(error!=null){b.Enabled=previous;state.ActiveProfileId=profile;state.Paused=paused;status.Text=error;return;}
   if(!Save()){b.Enabled=previous;state.ActiveProfileId=profile;state.Paused=paused;return;}
   engine.ResetPending();Actions.CancelWorkflow();guide.Hide();engine.Paused=state.Paused;SyncProfileChoice();UpdatePause();probes.Clear();Render();status.Text=b.Name+" • "+Profiles.Describe(b,state.Profiles,state.ActiveProfileId,state.Paused);
  }
  internal void ActivationForTest(string id){SetActivation(state.Bindings.Single(b=>b.Id==id));}
  void DeleteSelected(){var r=Selected();if(r!=null&&r.SystemShortcut!=null){ClearSystemKey(r.SystemShortcut);return;}if(r==null||r.Binding==null)return;if(MessageBox.Show(this,r.Name+" delete assignment?","Delete assignment",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;int index=state.Bindings.IndexOf(r.Binding);state.Bindings.RemoveAt(index);if(!Save())state.Bindings.Insert(index,r.Binding);probes.Clear();Render();}
  void ProbeAll(){probes.Clear();foreach(var b in Profiles.Effective(state).Where(x=>x.Enabled)){if(b.PrefixKey>0){probes[b.Id]="Two-key shortcuts are managed with a keyboard latch. RegisterHotKey cannot test this combination; external conflicts could not be verified.";continue;}var err=Native.Probe(b);probes[b.Id]=err??"Verified in system: RegisterHotKey test successful. Registration left; future compatibility and behavior of external applications are not guaranteed.";}Render();}
  UIElement Settings(){
   var s=new StackPanel();s.Children.Add(Brand.Picture(140));s.Children.Add(Theme.Text("Makro+ "+Brand.Version,24));s.Children.Add(Theme.Button("Open user guide",()=>{string guide=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"USER_GUIDE.html");if(File.Exists(guide))System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(guide){UseShellExecute=true});else status.Text="USER_GUIDE.html was not found. Extract the complete app ZIP.";}));s.Children.Add(Theme.Button("Transfer, backup and device inspection",()=>Navigate("Transfer and diagnostics")));s.Children.Add(Theme.Text("Appearance and behavior",20));s.Children.Add(Theme.Text("Current theme: "+ThemeCatalog.Resolve(state).Name,13,"Muted"));s.Children.Add(Theme.Button("Customize appearance",()=>Navigate("Appearance"),true));
   s.Children.Add(Theme.Text("Playback speed",20));var speed=new ComboBox{Width=320,HorizontalAlignment=HorizontalAlignment.Left,DisplayMemberPath="Name",SelectedValuePath="Milliseconds",ItemsSource=new[]{new PlaybackSpeed{Name="Instant • 0 ms between steps",Milliseconds=0},new PlaybackSpeed{Name="Fast • 15 ms (default)",Milliseconds=15},new PlaybackSpeed{Name="Balanced • 50 ms",Milliseconds=50},new PlaybackSpeed{Name="Compatible • 100 ms",Milliseconds=100},new PlaybackSpeed{Name="Slow applications • 250 ms",Milliseconds=250}}};speed.SelectedValue=state.StepIntervalMs??15;if(speed.SelectedIndex<0)speed.SelectedValue=15;speed.SelectionChanged+=(a,b)=>{if(!(speed.SelectedValue is int))return;var old=state.StepIntervalMs;state.StepIntervalMs=(int)speed.SelectedValue;if(Save())Actions.StepIntervalMs=state.StepIntervalMs.Value;else state.StepIntervalMs=old;};s.Children.Add(speed);s.Children.Add(Theme.Text("Saved plans execute without calling the model. Explicit Wait steps and recorded pauses are preserved. If an app misses rapid key sequences, choose a slower speed.",12,"Muted"));s.Children.Add(Theme.Button("Configure AI model",()=>ConfigureModel()));
   var remapped=new CheckBox{Content="Accept software-remapped keyboard and mouse inputs from device utilities",IsChecked=state.AllowRemappedInput};remapped.Checked+=(s1,e1)=>{state.AllowRemappedInput=engine.AllowRemappedInput=true;Save();};remapped.Unchecked+=(s1,e1)=>{state.AllowRemappedInput=engine.AllowRemappedInput=false;engine.ResetPending();Save();};s.Children.Add(remapped);s.Children.Add(Theme.Text("When enabled, remapped input from other software can also trigger shortcuts. Makro+ playback is always excluded. Firmware-only Fn keys and vendor buttons must be exposed by the device driver as Windows keys or mouse buttons.",12,"Muted"));
   s.Children.Add(Theme.Text("Keyboard behavior",20));s.Children.Add(Theme.Text("Left/Right Ctrl and Shift are equivalent. Left Alt is supported; Right Alt (AltGr) combinations are not triggered and are not saved. Windows virtual key identifiers are used for Turkish Q/F; physical key position is not locked. Refresh the list to see the label after a layout change. Fn hardware keys depend on the driver.\n\nActions execute once after all keys are released. If not released within 3 seconds, they are cancelled. Synthetic events do not count as triggers. Profile-specific assignments take precedence over general assignments. When communication is closed, press/release events for the matching main key are blocked; modifiers may have been sent earlier. Behavior of other keys, protected, or administrator applications cannot be guaranteed.",13,"Muted"));
   s.Children.Add(Theme.Text("Spotify and network",20));s.Children.Add(Theme.Text("This version opens Spotify URIs. It does not verify whether playback has started or a track is playing. There is no direct playback/OAuth integration. Web and Spotify content may require a network connection; basic shortcut management is offline.",13,"Muted"));
   s.Children.Add(Theme.Text("Data and commands",20));s.Children.Add(Theme.Text("Settings: "+Storage.FilePath+"\nAtomic recording and previous version keep a .bak backup. The result of the last 200 operations is saved locally. Terminal commands are saved as text; do not write passwords or access keys to commands. Administrator privileges are not required. PowerShell profiles are not loaded; cmd commands run with shell syntax. Application parameters are not passed from the shell. If the window is closed, the engine stops; you can run it in the background by minimizing to the tray.",13,"Muted"));
   s.Children.Add(Theme.Button("Open data folder",()=>System.Diagnostics.Process.Start("explorer.exe",Storage.Folder)));
   s.Children.Add(Theme.Text("The shortcuts section in the computer reads .lnk files and VS Code-compatible user shortcut files and edits them in the sources. You can add additional files/folders to the scan. The previous version source file is preserved alongside .akilli-kisayol.bak. New key assignments are provided for Windows defaults. There is no common Windows API that provides all shortcuts from other applications.",12,"Muted"));
   foreach(var t in s.Children.OfType<TextBlock>())t.Margin=new Thickness(0,14,0,10);
   return new ScrollViewer{Content=s,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
  }
  sealed class PlaybackSpeed {public string Name{get;set;}public int Milliseconds{get;set;}}
  UIElement ApplicationPanel(bool compact){
   var dock=new DockPanel();var header=new DockPanel{Margin=new Thickness(0,0,0,10)};
   var add=Theme.Button("＋ Add application",AddApplications,true);DockPanel.SetDock(add,Dock.Right);header.Children.Add(add);
   var prompt=Theme.Button("✎ Prompt shortcut",()=>OpenPrompt());DockPanel.SetDock(prompt,Dock.Right);header.Children.Add(prompt);
   if(compact){var all=Theme.Button("Show all",()=>{view="Applications";Render();});DockPanel.SetDock(all,Dock.Right);header.Children.Add(all);}
   var title=new StackPanel();title.Children.Add(Theme.Text("Quick Applications",compact?16:21));title.Children.Add(Theme.Text("Select the application, create a shortcut for it.",12,"Muted"));header.Children.Add(title);DockPanel.SetDock(header,Dock.Top);dock.Children.Add(header);
   var apps=new WrapPanel();
   foreach(string path in state.ApplicationPaths){
    string selected=path;int number=state.Bindings.Count(b=>Rules.SameApp(b.AppPath,path)||(b.Kind=="Application"&&Rules.SameApp(b.Target,path))||(b.Kind=="Browser website"&&Rules.SameApp(b.BrowserPath,path))||(b.Steps!=null&&b.Steps.Any(step=>Rules.SameApp(step.AppPath,path))));
    if(compact){
     var group=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,8,8)};
     var button=Theme.Button(AppLibrary.Name(path)+"  ·  "+number,()=>OpenPrompt(selected));button.ToolTip=path+"\nAdd a shortcut to the app by typing a prompt";button.Margin=new Thickness(0,0,3,0);button.MaxWidth=230;group.Children.Add(button);
     var more=Theme.Button("⋯",()=>{});more.Margin=new Thickness(0);more.Padding=new Thickness(10,7,10,7);var menu=new ContextMenu();
     var scopeItem=new MenuItem{Header="Add shortcut for the application"};scopeItem.Click+=(s,e)=>NewApplicationShortcut(selected,false);menu.Items.Add(scopeItem);
     var promptItem=new MenuItem{Header="Create assignment with Prompt"};promptItem.Click+=(s,e)=>OpenPrompt(selected);menu.Items.Insert(0,promptItem);
     var launchItem=new MenuItem{Header="Add shortcut for launching the application"};launchItem.Click+=(s,e)=>NewApplicationShortcut(selected,true);menu.Items.Add(launchItem);
     var remove=new MenuItem{Header="Remove from clipboard"};remove.Click+=(s,e)=>RemoveApplication(selected);menu.Items.Add(remove);more.Click+=(s,e)=>{menu.PlacementTarget=more;menu.IsOpen=true;};group.Children.Add(more);apps.Children.Add(group);
    }else{
     var card=new StackPanel{Width=285};card.Children.Add(Theme.Text(AppLibrary.Name(path),18));var label=Theme.Text(path,11,"Muted");label.Margin=new Thickness(0,6,0,8);label.ToolTip=path;card.Children.Add(label);card.Children.Add(Theme.Text(number+" shortcuts"+(File.Exists(path)?"":" • file not found"),12,"Muted"));
     var promptButton=Theme.Button("Write the assignment I want",()=>OpenPrompt(selected),true);promptButton.Margin=new Thickness(0,12,0,7);card.Children.Add(promptButton);
     var scopeButton=Theme.Button("Shortcut for the application",()=>NewApplicationShortcut(selected,false));scopeButton.Margin=new Thickness(0,12,0,7);card.Children.Add(scopeButton);
     var launchButton=Theme.Button("Shortcut",()=>NewApplicationShortcut(selected,true));launchButton.Margin=new Thickness(0,0,0,7);card.Children.Add(launchButton);
     var removeButton=Theme.Button("Remove from clipboard",()=>RemoveApplication(selected));removeButton.Margin=new Thickness(0);card.Children.Add(removeButton);
     var border=Theme.Card(card,"Surface");border.Margin=new Thickness(0,0,12,12);apps.Children.Add(border);
    }
   }
   if(state.ApplicationPaths.Count==0)apps.Children.Add(Theme.Text("No applications added yet. Add from open applications or .exe files.",13,"Muted"));
   var scroll=new ScrollViewer{Content=apps,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};if(compact)scroll.MaxHeight=86;dock.Children.Add(scroll);
   var panel=Theme.Card(dock);panel.Margin=new Thickness(0,0,0,16);panel.Padding=new Thickness(16);return panel;
  }
  void AddApplications(){
   bool pausedBefore=engine.Paused;engine.Paused=true;
   try{var picker=new AppPicker(state.ApplicationPaths){Owner=this};if(picker.ShowDialog()!=true)return;var previous=state.ApplicationPaths.ToList();int added=0;foreach(string path in picker.SelectedPaths)if(AppLibrary.Remember(state.ApplicationPaths,path))added++;if(!Save()){state.ApplicationPaths=previous;return;}status.Text=added+" application added. You can create a shortcut by clicking the application.";Render();}
   finally{engine.Paused=pausedBefore;}
  }
  void RemoveApplication(string path){var previous=state.ApplicationPaths.ToList();state.ApplicationPaths.RemoveAll(p=>Rules.SameApp(p,path));if(!Save()){state.ApplicationPaths=previous;return;}Render();}
  void NewApplicationShortcut(string path,bool launch){if(!File.Exists(path)){MessageBox.Show(this,"Application file not found. Remove from the sidebar and add the current .exe file.\n\n"+path);return;}Edit(null,AppLibrary.Draft(path,launch));}
  internal void ShowApplicationsForTest(){view="Applications";Render();UpdateLayout();}
  internal void SmokeViews(){foreach(string name in new[]{"Applications","By application","Computer shortcuts","Conflicts","History","Transfer and diagnostics","Settings","All shortcuts"}){view=name;Render();UpdateLayout();}}
 }
 public class UniformGridCompat : System.Windows.Controls.Primitives.UniformGrid {public UniformGridCompat(){Rows=1;Columns=3;}}
}
