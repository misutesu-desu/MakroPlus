using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace AkilliKisayol {
 public sealed class InputDevice {public string Id,Name,Type;}
 public sealed class PointerFrame {public string Device;public double X,Y;public bool Absolute,Buttons;public long Time;public IntPtr Window;}
 public sealed class RawPointerSource:IDisposable {
  [StructLayout(LayoutKind.Sequential)]struct DeviceRegistration{public ushort Page,Usage;public uint Flags;public IntPtr Target;}
  [StructLayout(LayoutKind.Sequential)]struct DeviceList{public IntPtr Handle;public uint Type;}
  [DllImport("user32.dll",SetLastError=true)]static extern bool RegisterRawInputDevices(DeviceRegistration[] devices,uint count,uint size);
  [DllImport("user32.dll",SetLastError=true)]static extern uint GetRawInputData(IntPtr input,uint command,IntPtr data,ref uint size,uint headerSize);
  [DllImport("user32.dll",SetLastError=true)]static extern uint GetRawInputDeviceList([In,Out]DeviceList[] list,ref uint count,uint size);
  [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern uint GetRawInputDeviceInfo(IntPtr device,uint command,IntPtr data,ref uint size);
  readonly HwndSource window;readonly Dictionary<IntPtr,string> names=new Dictionary<IntPtr,string>();readonly int headerSize=8+IntPtr.Size*2;bool disposed;
  public event Action<PointerFrame> Input;public Func<bool> Needed=()=>true;
  public string Status {get;private set;}
  public RawPointerSource(){var options=new HwndSourceParameters("Makro+ input source"){ParentWindow=new IntPtr(-3),Width=0,Height=0};window=new HwndSource(options);window.AddHook(Message);if(!RegisterRawInputDevices(new[]{new DeviceRegistration{Page=1,Usage=2,Flags=0x2100,Target=window.Handle}},1,(uint)Marshal.SizeOf(typeof(DeviceRegistration)))){Status="Raw input unavailable ("+Marshal.GetLastWin32Error()+").";}else Status="Windows background pointer input is ready.";}
  static string Name(IntPtr device){uint count=0;GetRawInputDeviceInfo(device,0x20000007,IntPtr.Zero,ref count);if(count==0||count>4096)return "device:"+device.ToInt64();IntPtr memory=Marshal.AllocHGlobal((int)(count+1)*2);try{return GetRawInputDeviceInfo(device,0x20000007,memory,ref count)==uint.MaxValue?"device:"+device.ToInt64():Marshal.PtrToStringUni(memory)??"";}finally{Marshal.FreeHGlobal(memory);}}
  public static List<InputDevice> Devices(){uint count=0,size=(uint)Marshal.SizeOf(typeof(DeviceList));var result=new List<InputDevice>();if(GetRawInputDeviceList(null,ref count,size)==uint.MaxValue||count>4096)return result;var list=new DeviceList[count];uint actual=GetRawInputDeviceList(list,ref count,size);if(actual==uint.MaxValue)return result;int mouse=0,keyboard=0,hid=0;for(int i=0;i<Math.Min(actual,count);i++){string type=list[i].Type==0?"Pointer":list[i].Type==1?"Keyboard":"HID";int number=list[i].Type==0?++mouse:list[i].Type==1?++keyboard:++hid;result.Add(new InputDevice{Id=Name(list[i].Handle),Name=type+" "+number,Type=type});}return result;}
  IntPtr Message(IntPtr handle,int message,IntPtr w,IntPtr l,ref bool handled){if(message==0xFE){names.Clear();return IntPtr.Zero;}if(message!=0xFF||disposed||!Needed())return IntPtr.Zero;uint size=0;if(GetRawInputData(l,0x10000003,IntPtr.Zero,ref size,(uint)headerSize)==uint.MaxValue||size<headerSize+24||size>65536)return IntPtr.Zero;IntPtr memory=Marshal.AllocHGlobal((int)size);try{if(GetRawInputData(l,0x10000003,memory,ref size,(uint)headerSize)!=size||Marshal.ReadInt32(memory)!=0)return IntPtr.Zero;IntPtr device=Marshal.ReadIntPtr(memory,8);string id;if(device==IntPtr.Zero)id="";else if(!names.TryGetValue(device,out id)){id=Name(device);names[device]=id;}int offset=headerSize;ushort flags=(ushort)Marshal.ReadInt16(memory,offset),buttons=(ushort)Marshal.ReadInt16(memory,offset+4);var frame=new PointerFrame{Device=id,X=Marshal.ReadInt32(memory,offset+12),Y=Marshal.ReadInt32(memory,offset+16),Absolute=(flags&1)!=0,Buttons=buttons!=0,Time=Environment.TickCount&int.MaxValue,Window=Native.GetForegroundWindow()};var handler=Input;if(handler!=null)handler(frame);}catch(Exception error){Status="Pointer input error: "+error.Message;}finally{Marshal.FreeHGlobal(memory);}return IntPtr.Zero;}
  public void Dispose(){if(disposed)return;disposed=true;RegisterRawInputDevices(new[]{new DeviceRegistration{Page=1,Usage=2,Flags=1,Target=IntPtr.Zero}},1,(uint)Marshal.SizeOf(typeof(DeviceRegistration)));window.RemoveHook(Message);window.Dispose();}
 }
}
