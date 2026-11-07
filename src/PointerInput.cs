using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;

namespace AkilliKisayol {
 public static class InputKeys {
  public const int WheelUp=256,WheelDown=257,WheelLeft=258,WheelRight=259;
  public static bool IsWheel(int key){return key>=WheelUp&&key<=WheelRight;}
  public static bool IsPointer(int key){return key==1||key==2||key==4||key==5||key==6||IsWheel(key);}
  public static bool Valid(int key){return key>=1&&key<=254||IsWheel(key);}
  public static string Name(int key){switch(key){case 1:return "Mouse left";case 2:return "Mouse right";case 4:return "Mouse middle";case 5:return "Mouse X1 (back)";case 6:return "Mouse X2 (forward)";case WheelUp:return "Wheel up";case WheelDown:return "Wheel down";case WheelLeft:return "Wheel left";case WheelRight:return "Wheel right";default:return "";}}
  public static string Token(int key){switch(key){case 1:return "LButton";case 2:return "RButton";case 4:return "MButton";case 5:return "XButton1";case 6:return "XButton2";case WheelUp:return "WheelUp";case WheelDown:return "WheelDown";case WheelLeft:return "WheelLeft";case WheelRight:return "WheelRight";default:return "";}}
  public static int Parse(string text){var keys=new[]{1,2,4,5,6,256,257,258,259};string clean=text.Replace(" ","").Replace("_","").Replace("-","").ToLowerInvariant();return keys.FirstOrDefault(k=>Token(k).ToLowerInvariant()==clean||Name(k).Replace(" ","").ToLowerInvariant()==clean);}
  public static IEnumerable<int> Pointers {get{return new[]{1,2,4,5,6,256,257,258,259};}}
  public static string ButtonName(int button){return button==1?"Right":button==2?"Middle":button==3?"X1 (back)":button==4?"X2 (forward)":"Left";}
 }
 public sealed class PointerEngine:IDisposable {
  readonly KeyboardEngine keyboard;readonly Native.HookProc callback;IntPtr hook;int vertical,horizontal,lastMods;IntPtr lastWindow;
  public PointerEngine(KeyboardEngine engine){keyboard=engine;callback=OnMouse;hook=Native.SetWindowsHookEx(14,callback,Native.GetModuleHandle(null),0);if(hook==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error(),"Mouse hook could not be created.");}
  IntPtr OnMouse(int code,IntPtr message,IntPtr data){if(code>=0)try{if(Handle(message.ToInt32(),(Native.MouseEvent)Marshal.PtrToStructure(data,typeof(Native.MouseEvent))))return new IntPtr(1);}catch(Exception e){keyboard.ReportFault(e.Message);}return Native.CallNextHookEx(hook,code,message,data);}
  internal bool Handle(int message,Native.MouseEvent input){
   if(input.Extra==new UIntPtr(0x414B534D)||(input.Flags&1)!=0&&!keyboard.AllowRemappedInput)return false;int key=0;bool up=false;
   switch(message){case 0x201:key=1;break;case 0x202:key=1;up=true;break;case 0x204:key=2;break;case 0x205:key=2;up=true;break;case 0x207:key=4;break;case 0x208:key=4;up=true;break;case 0x20B:case 0x20C:int button=(int)(input.Data>>16);if(button!=1&&button!=2)return false;key=button==1?5:6;up=message==0x20C;break;
    case 0x20A:case 0x20E:int delta=(short)(input.Data>>16);if(delta==0)return false;key=message==0x20A?(delta>0?InputKeys.WheelUp:InputKeys.WheelDown):(delta>0?InputKeys.WheelRight:InputKeys.WheelLeft);break;default:return false;}
   if(InputKeys.IsWheel(key)&&keyboard.Capture==null){
    IntPtr window=Native.GetForegroundWindow();if(lastWindow!=window||lastMods!=keyboard.CurrentMods){vertical=horizontal=0;lastWindow=window;lastMods=keyboard.CurrentMods;}
    if(!keyboard.WouldHandlePointer(key)){if(message==0x20A)vertical=0;else horizontal=0;return false;}
    int delta=(short)(input.Data>>16);int total=(message==0x20A?vertical:horizontal)+delta;bool complete=Math.Abs(total)>=120;if(message==0x20A)vertical=total%120;else horizontal=total%120;if(!complete)return true;
   }
   bool eat=keyboard.Handle(new Native.Keyboard{vk=(uint)key,flags=up?128u:0u});if(InputKeys.IsWheel(key))eat=keyboard.Handle(new Native.Keyboard{vk=(uint)key,flags=128})||eat;return eat;
  }
  public void Dispose(){if(hook!=IntPtr.Zero){Native.UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}}
 }
}
