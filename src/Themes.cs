using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AkilliKisayol {
 [DataContract] public sealed class ThemePalette {
  [DataMember] public string Id,Name,Description;
  [DataMember] public bool Dark;
  [DataMember] public string Background,Panel,Surface,Ink,Muted,Line,Hero;
  [DataMember(EmitDefaultValue=false)] public string AccentColor,AccentTextColor,PeachColor,SelectionColor;
  public string Accent {get{return AccentColor??(Dark?"#FF8174":"#B63F43");}}
  public string AccentInk {get{return AccentTextColor??(Dark?"#321719":"#FFFFFF");}}
  public string Peach {get{return PeachColor??"#F5BC99";}}
  public string Selection {get{return SelectionColor??(Dark?"#493738":"#F9E2DB");}}
  public ThemePalette Copy(){return (ThemePalette)MemberwiseClone();}
  public SolidColorBrush Brush(string value){var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));brush.Freeze();return brush;}
  public IDictionary<string,string> Colors(){return new Dictionary<string,string>{{"Bg",Background},{"Panel",Panel},{"Surface",Surface},{"Ink",Ink},{"Muted",Muted},{"Line",Line},{"Selection",Selection},{"Accent",Accent},{"AccentInk",AccentInk},{"Peach",Peach},{"Hero",Hero},{"HeroInk",Ink}};}
 }
 public static class ThemeCatalog {
  static readonly ThemePalette[] palettes={
   new ThemePalette{Id="graphite",Name="Graphite",Description="Neutral charcoal",Dark=true,Background="#181A1E",Panel="#22252B",Surface="#2D323A",Ink="#F3F4F7",Muted="#B5BCC7",Line="#464D58",Hero="#262C34"},
   new ThemePalette{Id="midnight",Name="Midnight",Description="Deep navy",Dark=true,Background="#111827",Panel="#1C2536",Surface="#29354A",Ink="#EFF3FA",Muted="#B5C1D6",Line="#465773",Hero="#202D44"},
   new ThemePalette{Id="forest",Name="Forest",Description="Quiet evergreen",Dark=true,Background="#131E1A",Panel="#1C2B24",Surface="#2A3A31",Ink="#F0F5F1",Muted="#B4C9BD",Line="#465E50",Hero="#22372B"},
   new ThemePalette{Id="paper",Name="Paper",Description="Clean neutral light",Dark=false,Background="#F5F5F4",Panel="#FFFFFF",Surface="#EBEDED",Ink="#252C32",Muted="#59636D",Line="#CBD1D4",Hero="#E7EBE9"},
   new ThemePalette{Id="mist",Name="Mist",Description="Cool blue-gray",Dark=false,Background="#F0F4F8",Panel="#FFFFFF",Surface="#E5EBF3",Ink="#233044",Muted="#52647A",Line="#C6D2E1",Hero="#DFE8F3"},
   new ThemePalette{Id="sand",Name="Sand",Description="Soft warm beige",Dark=false,Background="#F5F1E9",Panel="#FFFDFA",Surface="#EDE6DA",Ink="#382F28",Muted="#706151",Line="#D8CCBB",Hero="#E9E0D1"}
  };
  public static IEnumerable<ThemePalette> All {get{return palettes;}}
  public static IEnumerable<ThemePalette> For(State state){return All.Concat(state.CustomThemes??new List<ThemePalette>());}
  public static ThemePalette Resolve(State state){return Resolve(state.ThemeId,state.Dark,state.CustomThemes);}
  public static ThemePalette Resolve(string id,bool dark,IEnumerable<ThemePalette> custom=null){return palettes.FirstOrDefault(p=>p.Id==id)??(custom??Enumerable.Empty<ThemePalette>()).FirstOrDefault(p=>p.Id==id)??palettes.First(p=>p.Id==(dark?"graphite":"paper"));}
  public static void Normalize(State state){state.CustomThemes=(state.CustomThemes??new List<ThemePalette>()).Where(t=>ThemeFiles.Validate(t)==null).GroupBy(t=>t.Id).Select(g=>g.First()).ToList();var selected=Resolve(state);state.ThemeId=selected.Id;state.Dark=selected.Dark;}
 }
 public partial class MainWindow {
  UIElement Appearance(){
   var form=new StackPanel();form.Children.Add(Theme.Text("Choose your atmosphere",24));form.Children.Add(Theme.Text("Pick a preset, create your own palette, or import a theme shared by someone else.",14,"Muted"));var actions=new WrapPanel{Margin=new Thickness(0,14,0,0)};actions.Children.Add(Theme.Button("Create theme",()=>EditCustomTheme(false),true));actions.Children.Add(Theme.Button("Import theme",ImportTheme));actions.Children.Add(Theme.Button("Export selected",ExportTheme));if(state.CustomThemes.Any(t=>t.Id==state.ThemeId)){actions.Children.Add(Theme.Button("Edit selected",()=>EditCustomTheme(true)));actions.Children.Add(Theme.Button("Delete selected",DeleteTheme));}form.Children.Add(actions);var grid=new System.Windows.Controls.Primitives.UniformGrid{Columns=3,Margin=new Thickness(0,20,0,4)};
   foreach(var palette in ThemeCatalog.For(state)){string id=palette.Id;bool selected=ThemeCatalog.Resolve(state).Id==id;var card=new StackPanel();var sample=new Grid{Height=72,Margin=new Thickness(0,0,0,12),Background=palette.Brush(palette.Background)};sample.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(0.28,GridUnitType.Star)});sample.ColumnDefinitions.Add(new ColumnDefinition());sample.Children.Add(new Border{Background=palette.Brush(palette.Surface),Margin=new Thickness(6)});var body=new StackPanel{Margin=new Thickness(8,12,10,8)};Grid.SetColumn(body,1);body.Children.Add(new Border{Background=palette.Brush(palette.Ink),Height=5,Width=72,HorizontalAlignment=HorizontalAlignment.Left,CornerRadius=new CornerRadius(2)});body.Children.Add(new Border{Background=palette.Brush(palette.Muted),Height=4,Width=110,HorizontalAlignment=HorizontalAlignment.Left,CornerRadius=new CornerRadius(2),Margin=new Thickness(0,6,0,8)});var accents=new StackPanel{Orientation=Orientation.Horizontal};accents.Children.Add(new Border{Background=palette.Brush(palette.Accent),Width=30,Height=10,CornerRadius=new CornerRadius(3),Margin=new Thickness(0,0,6,0)});accents.Children.Add(new Border{Background=palette.Brush(palette.Peach),Width=18,Height=10,CornerRadius=new CornerRadius(3)});body.Children.Add(accents);sample.Children.Add(body);card.Children.Add(sample);card.Children.Add(Theme.Text(palette.Name+(selected?"  ✓":""),17));card.Children.Add(Theme.Text((palette.Dark?"Dark":"Light")+(id.StartsWith("custom-",StringComparison.Ordinal)?" • Custom theme":" • "+palette.Description),12,"Muted"));var button=Theme.Button("",()=>SetTheme(id));button.Content=card;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Padding=new Thickness(15);button.Margin=new Thickness(0,0,12,12);button.SetResourceReference(Control.BackgroundProperty,selected?"Selection":"Panel");button.ToolTip="Apply "+palette.Name+" theme";button.SetValue(System.Windows.Automation.AutomationProperties.NameProperty,palette.Name+" theme"+(selected?", selected":""));grid.Children.Add(button);}
   form.Children.Add(grid);form.Children.Add(Theme.Text("Applies immediately to all open Makro+ windows and is saved for the next launch.",12,"Muted"));return form;
  }
  bool SetTheme(string id){var choice=ThemeCatalog.Resolve(id,state.Dark,state.CustomThemes);string previous=state.ThemeId;bool dark=state.Dark;state.ThemeId=choice.Id;state.Dark=choice.Dark;if(!Save()){state.ThemeId=previous;state.Dark=dark;return false;}Theme.Apply(state.ThemeId,state.Dark,state.CustomThemes);Render();return true;}
  internal bool ThemeForTest(string id){return SetTheme(id);}
 }
}
