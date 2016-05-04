
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ODIF;
using ODIF.Extensions;
using System.Threading;
using System.Drawing;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace HIDMouse
{
    [PluginInfo(
        PluginName = "Mouse HID Input",
        PluginDescription = "Allows for mouse connectivity in InputMapper.",
        PluginID = 35,
        PluginAuthorName = "InputMapper",
        PluginAuthorEmail = "jhebbel@gmail.com",
        PluginAuthorURL = "http://inputmapper.com",
        PluginIconPath = @"pack://application:,,,/HIDMouse;component/Resources/Mouse.ico"
    )]
    public class iMouse_Plugin : InputDevicePlugin, IMPlugin
    {
        //public bool PluginActive { get { return true; } set { } }

        //public Dictionary<string, InputDevice> Devices { get; set; }
        //public AsyncObservableCollection<InputDevice> Devices { get; set; }
        public iMouse_Plugin()
        {
            //Devices = new AsyncObservableCollection<InputDevice>();
            iMouse_Device Mouse = new iMouse_Device();
            Devices.Add(Mouse);
        }

        public void Dispose()
        {
            foreach (InputDevice Device in Devices.ToList())
            {
                Device.Dispose();
                Devices.Remove(Device);
            }
        }

    }
    public static class mouseExtensions
    {
        public static iMouse_Device instance {get;set;}
    }
    internal class MouseDevice
    {
        public InputChannelTypes.JoyAxis mouseX { get; set; }
        public InputChannelTypes.JoyAxis mouseY { get; set; }
        public InputChannelTypes.JoyAxis mouseDx { get; set; }
        public InputChannelTypes.JoyAxis mouseDy { get; set; }

        public InputChannelTypes.Button mouseB1 { get; set; }
        public InputChannelTypes.Button mouseB2 { get; set; }

        public OutputChannelTypes.Toggle trapPosition { get; set; }
        public OutputChannelTypes.Toggle trapPositionToggle { get; set; }

        public MouseDevice()
        {
            mouseX = new InputChannelTypes.JoyAxis("Mouse X","This is the X position relitive to Windows' defined work area", Properties.Resources.Keyboard_White_Mouse_x.ToImageSource());
            mouseY = new InputChannelTypes.JoyAxis("Mouse Y", "This is the Y position relitive to Windows' defined work area", Properties.Resources.Keyboard_White_Mouse_y.ToImageSource());
            mouseDx = new InputChannelTypes.JoyAxis("Mouse Delta X" , "This is the speed at which the mouse is moving across the X axis", Properties.Resources.Keyboard_White_Mouse_dX.ToImageSource());
            mouseDy = new InputChannelTypes.JoyAxis("Mouse Delta Y", "This is the speed at which the mouse is moving across the Y axis", Properties.Resources.Keyboard_White_Mouse_dY.ToImageSource());
            mouseB1 = new InputChannelTypes.Button("Mouse Button 1", "Left mouse button",Properties.Resources.Keyboard_White_Mouse_Left.ToImageSource());
            mouseB2 = new InputChannelTypes.Button("Mouse Button 2", "Right mouse button",Properties.Resources.Keyboard_White_Mouse_Right.ToImageSource());
            trapPosition = new OutputChannelTypes.Toggle("Trap Mouse Position", "Use this when emulating mouse to controller stick to keep mouse for reaching screen edge");
            trapPositionToggle = new OutputChannelTypes.Toggle("Trap Mouse Toggle", "Toggles on/off the 'Trap Mouse' setting");

            trapPositionToggle.PropertyChanged += (s, e) => {
                if ((s as DeviceChannel).Value == true)
                {
                    trapPosition.Value = !(bool)trapPosition.Value;
                }
            };
        }
    }
    public class iMouse_Device : InputDevice
    {
        public MappingWindow mappingWindow { get; }
        public enum ThreadStatus { None, Starting, Running, RequestStop, Stoped, Error };

        public ThreadStatus Status { get; set; }
        private Thread DeltaListenerThread;
        public ConnectionTypes DeviceConnectionType { get { return ConnectionTypes.USB | ConnectionTypes.BT; } }
        private MouseDevice mouseDevice { get; set; }

        private Timer checkDeltaTimer;

        protected override void Dispose(bool disposing)
        {
            checkDeltaTimer.Dispose();
            CallbackTimestamp.Stop();
            UnhookWindowsHookEx(_hookID);
            mouseExtensions.instance = null;

            base.Dispose(disposing);
        }

        public iMouse_Device()
        {
            mouseDevice = new MouseDevice();
            //InputChannels = new ObservableCollection<InputChannel>();
            //OutputChannels = new ObservableCollection<OutputChannel>();
            this.StatusIcon = new BitmapImage(new Uri("pack://application:,,,/HIDMouse;component/Resources/Mouse.ico"));
            this.DeviceName = "Generic HID Mouse";
            this.mappingWindow = new MouseMapping(this);
            //mappingWindow = new test();

            InputChannels.Add(mouseDevice.mouseX);
            InputChannels.Add(mouseDevice.mouseY);

            InputChannels.Add(mouseDevice.mouseDx);
            InputChannels.Add(mouseDevice.mouseDy);

            InputChannels.Add(mouseDevice.mouseB1);
            InputChannels.Add(mouseDevice.mouseB2);

            OutputChannels.Add(mouseDevice.trapPosition);
            OutputChannels.Add(mouseDevice.trapPositionToggle);


            mouseExtensions.instance = this;
            CallbackTimestamp.Start();
            _hookID = SetHook(_proc);

            checkDeltaTimer = new Timer(checkDeltaCallback, null, 2, 2);
            
        }
        private double safeStickThrow(double value)
        {
            value = Math.Min(value,1);
            value = Math.Max(value, -1);
            return value;
        }
        private void checkDeltaCallback(object state)
        {
            if (mouseDevice.trapPosition.Value == true)
            {
                int centerX = Convert.ToInt32(SystemParameters.WorkArea.Left + SystemParameters.WorkArea.Width / 2);
                int centerY = Convert.ToInt32(SystemParameters.WorkArea.Top + SystemParameters.WorkArea.Height / 2);

                int timeDif = (int)CallbackTimestamp.ElapsedMilliseconds;

                if (timeDif > 0)
                {
                    int xDif = CurrentHook.pt.x - centerX;
                    int yDif = CurrentHook.pt.y - centerY;

                    double xDelta = safeStickThrow(((double)xDif / (double)timeDif) * .05);
                    double yDelta = safeStickThrow(((double)yDif / (double)timeDif) * .05);


                    xDeltaH.Add(xDelta);
                    if (xDeltaH.Count > 5)
                        xDeltaH.RemoveAt(0);

                    yDeltaH.Add(yDelta);
                    if (yDeltaH.Count > 5)
                        yDeltaH.RemoveAt(0);

                    mouseExtensions.instance.mouseDevice.mouseDx.Value = xDeltaH.ToArray().Average();
                    mouseExtensions.instance.mouseDevice.mouseDy.Value = yDeltaH.ToArray().Average();

                    //if (Global.Hashes.ContainsKey("MouseHIDInput.GenericHIDMouse.MouseDeltaX"))
                    //    Console.WriteLine(Global.Hashes["MouseHIDInput.GenericHIDMouse.MouseDeltaX"].Value);

                    LastHook = CurrentHook;
                    CallbackTimestamp.Restart();
                    NativeMethods.SetCursorPos(centerX, centerY);
                }
            }
            else {
                int timeDif = (int)CallbackTimestamp.ElapsedMilliseconds;

                if (timeDif > 0)
                {
                    int xDif = CurrentHook.pt.x - LastHook.pt.x;
                    int yDif = CurrentHook.pt.y - LastHook.pt.y;

                    double xDelta = safeStickThrow(((double)xDif / (double)timeDif) * .05);
                    double yDelta = safeStickThrow(((double)yDif / (double)timeDif) * -.05);


                    xDeltaH.Add(xDelta);
                    if (xDeltaH.Count > 5)
                        xDeltaH.RemoveAt(0);

                    yDeltaH.Add(yDelta);
                    if (yDeltaH.Count > 5)
                        yDeltaH.RemoveAt(0);

                    mouseExtensions.instance.mouseDevice.mouseDx.Value = xDeltaH.ToArray().Average();
                    mouseExtensions.instance.mouseDevice.mouseDy.Value = yDeltaH.ToArray().Average();

                    //if (Global.Hashes.ContainsKey("MouseHIDInput.GenericHIDMouse.MouseDeltaX"))
                    //    Console.WriteLine(Global.Hashes["MouseHIDInput.GenericHIDMouse.MouseDeltaX"].Value);

                    LastHook = CurrentHook;
                    CallbackTimestamp.Restart();
                }
                //System.Threading.Thread.Sleep(2);
            }
        }


        private static LowLevelMouseProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;


        private static IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static Stopwatch CallbackTimestamp = new Stopwatch();
        private static MSLLHOOKSTRUCT LastHook = new MSLLHOOKSTRUCT();
        private static MSLLHOOKSTRUCT CurrentHook = new MSLLHOOKSTRUCT();
        private static List<double> xDeltaH = new List<double>();
        private static List<double> yDeltaH = new List<double>();
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {

            if (nCode >= 0 && MouseMessages.WM_MOUSEMOVE == (MouseMessages)wParam)
            {
                MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                mouseExtensions.instance.mouseDevice.mouseX.Value = (hookStruct.pt.x ) / SystemParameters.VirtualScreenWidth;
                mouseExtensions.instance.mouseDevice.mouseY.Value = (hookStruct.pt.y ) / SystemParameters.VirtualScreenHeight;
                
                //LastHook = CurrentHook;
                CurrentHook = hookStruct;
            }
            lock (mouseExtensions.instance)
            {
                if (nCode >= 0 && ((MouseMessages)wParam).HasFlag(MouseMessages.WM_LBUTTONDOWN))
                    mouseExtensions.instance.mouseDevice.mouseB1.Value = true;

                if (nCode >= 0 && ((MouseMessages)wParam).HasFlag(MouseMessages.WM_LBUTTONUP))
                    mouseExtensions.instance.mouseDevice.mouseB1.Value = false;

                if (nCode >= 0 && ((MouseMessages)wParam).HasFlag(MouseMessages.WM_RBUTTONDOWN))
                    mouseExtensions.instance.mouseDevice.mouseB2.Value = true;

                if (nCode >= 0 && ((MouseMessages)wParam).HasFlag(MouseMessages.WM_RBUTTONUP))
                    mouseExtensions.instance.mouseDevice.mouseB2.Value = false;
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private const int WH_MOUSE_LL = 14;

        private enum MouseMessages
        {
            WM_LBUTTONDOWN = 0x0201,
            WM_LBUTTONUP = 0x0202,
            WM_LBUTTONDBLCLK = 0x0203,
            WM_MOUSEMOVE = 0x0200,
            WM_MOUSEWHEEL = 0x020A,
            WM_RBUTTONDOWN = 0x0204,
            WM_RBUTTONUP = 0x0205,
            WM_RBUTTONDBLCLK = 0x0206,
            WM_MBUTTONDOWN = 0x0207,
            WM_MBUTTONUP = 0x0208,
            WM_MBUTTONDBLCLK = 0x0209,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

    }


}
