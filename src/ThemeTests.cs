using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AkilliKisayol {
 public static class ThemeTests {
  static double Channel(byte value){double c=value/255.0;return c<=0.04045?c/12.92:Math.Pow((c+0.055)/1.055,2.4);}
  static double Luminance(Color color){return 0.2126*Channel(color.R)+0.7152*Channel(color.G)+0.0722*Channel(color.B);}
  static double Contrast(ThemePalette palette,string first,string second){double a=Luminance(palette.Brush(first).Color),b=Luminance(palette.Brush(second).Color);return (Math.Max(a,b)+0.05)/(Math.Min(a,b)+0.05);}
  public static void Unit(Action<bool,string> check){
   check(ThemeCatalog.All.Count()==6&&ThemeCatalog.All.Count(t=>t.Dark)==3,"Six themes offer three light and three dark base palettes");
   foreach(var palette in ThemeCatalog.All){check(new[]{palette.Background,palette.Panel,palette.Surface,palette.Hero}.All(surface=>Contrast(palette,palette.Ink,surface)>=4.5)&&Contrast(palette,palette.AccentInk,palette.Accent)>=4.5,"Readable text and primary action contrast: "+palette.Name);}
   check(ThemeCatalog.Resolve(null,true).Id=="graphite"&&ThemeCatalog.Resolve(null,false).Id=="paper"&&ThemeCatalog.Resolve("unknown",false).Id=="paper","Legacy light/dark settings and unknown themes receive a valid fallback");
   var key=new Binding{Key=119,Kind="Send key",SendKey=67,SendMods=2};var mouse=key.Copy();mouse.Key=5;var shape=key.Copy();shape.Gesture=new ShapeTemplate();var mixed=key.Copy();mixed.Steps=new List<ActionStep>{new ActionStep{Kind="Scroll",ScrollDelta=120}};
   check(InputSections.Keyboard(key,null)&&InputSections.Keyboard(mixed,null)&&!InputSections.Keyboard(mouse,null)&&!InputSections.Keyboard(shape,null),"Keyboard page filters by activation input, including keyboard-triggered mouse macros and excluding shapes");key.Enabled=false;check(InputSections.Keyboard(key,null)&&!InputSections.Keyboard(new Binding(),null),"Disabled keyboard assignments stay editable; unassigned drafts are excluded");check(InputSections.Keyboard(null,new SystemShortcut{KeyText="ctrl+k ctrl+c"})&&!InputSections.Keyboard(null,new SystemShortcut{KeyText="XButton1"})&&!InputSections.Keyboard(null,new SystemShortcut()),"External keyboard chords are supported without including pointer or unassigned entries");
   string folder=Path.Combine(Path.GetTempPath(),"MakroPlus-themes-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);try{string file=Path.Combine(folder,"state.json");var state=new State{ThemeId="forest",Dark=false};Storage.Write(state,file);var restored=Storage.Read(file);check(restored.ThemeId=="forest"&&restored.Dark,"Theme choice survives restart and synchronizes legacy dark mode");Transfer.Export(state,file);restored=Storage.Read(file);var imported=Transfer.Import(new State(),restored,new Dictionary<string,string>(),false);check(imported.State.ThemeId=="forest"&&imported.State.Dark,"Theme preference survives export and import");File.WriteAllText(file,"{\"Bindings\":[],\"History\":[],\"Dark\":false}");check(Storage.Read(file).ThemeId=="paper","Older settings without a theme ID migrate according to the saved light/dark mode");}finally{Directory.Delete(folder,true);}
  }
  static IEnumerable<T> Descendants<T>(DependencyObject parent) where T:DependencyObject {for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is T)yield return (T)child;foreach(var nested in Descendants<T>(child))yield return nested;}}
  public static void Ui(Window owner,Action<bool,string> check,Action<Window,string> screenshot){
   string folder=Path.Combine(Path.GetTempPath(),"MakroPlus-theme-ui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string file=Path.Combine(folder,"settings.json");
   var state=new State();state.Bindings.Add(new Binding{Name="Keyboard fixture",Key=119,Kind="Send key",SendKey=67,SendMods=2});state.Bindings.Add(new Binding{Name="Mouse fixture",Key=5,Kind="Send key",SendKey=67,SendMods=2});var window=new MainWindow(state,file){Owner=owner};window.Show();try{
    window.ProductViewForTest("Keyboard shortcuts");var rows=Descendants<DataGrid>(window).Single().Items.Cast<Row>().ToList();check(rows.Count==1&&rows[0].Name=="Keyboard fixture","Keyboard page defaults to personal keyboard assignments and excludes mouse-triggered actions");check(Descendants<Button>(window).Any(b=>Convert.ToString(b.Content)=="Keyboard shortcuts"),"INPUTS exposes the keyboard section");screenshot(window,"keyboard-shortcuts-preview.png");
    WindowFrameTests.Ui(window,check,screenshot);
    CustomThemeTests.Ui(window,check,screenshot);
    foreach(var palette in ThemeCatalog.All){window.ProductViewForTest("Appearance");var button=Descendants<Button>(window).Single(b=>System.Windows.Automation.AutomationProperties.GetName(b).StartsWith(palette.Name+" theme"));button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var saved=Storage.Read(file);check(saved.ThemeId==palette.Id&&saved.Dark==palette.Dark&&((SolidColorBrush)Application.Current.Resources["Bg"]).Color==palette.Brush(palette.Background).Color,"Theme gallery applies and saves "+palette.Name);window.ProductViewForTest("Home");screenshot(window,"theme-"+palette.Id+"-preview.png");}
    window.ProductViewForTest("Appearance");screenshot(window,"theme-gallery-preview.png");
   }finally{window.Close();Theme.Apply(true);foreach(string saved in Directory.GetFiles(folder))File.Delete(saved);Directory.Delete(folder);}
  }
 }
}
