using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AkilliKisayol {
 public static class Brand {
  public const string Name="Makro+",Version="2.6.2";
  static BitmapSource cover;
  public static BitmapSource Cover {get{if(cover==null){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("MakroPlus.Cover.png")){var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();cover=bitmap;}}return cover;}}
  public static Image Picture(double height){return new Image{Source=Cover,Height=height,Stretch=Stretch.Uniform,Margin=new Thickness(4),IsHitTestVisible=false};}
  public static System.Drawing.Icon TrayIcon(){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("MakroPlus.Icon.ico"))using(var icon=new System.Drawing.Icon(stream))return (System.Drawing.Icon)icon.Clone();}
  public static void Fit(Window window){var area=SystemParameters.WorkArea;window.MinWidth=Math.Min(window.MinWidth,Math.Max(640,area.Width-24));window.MinHeight=Math.Min(window.MinHeight,Math.Max(460,area.Height-36));window.Width=Math.Min(window.Width,area.Width-24);window.Height=Math.Min(window.Height,area.Height-36);window.Icon=Cover;}
 }
}
