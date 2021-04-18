using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ChatOverlay.Core
{
    public class MainWindow : Window
    {
        private SettingsWindow? _settingsWindow;
        private LowLevelKeyboardListener _listener;

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        internal delegate void KeyUpDelegate(KeyEventArgs e);
        private static event KeyUpDelegate StaticKeyUpEvent;

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            InitWindowClicks();

            if (DataContext is MainWindowViewModel mainViewModel && mainViewModel.ShowSettingsOnStartUp)
                OpenSettings();
        }

        private static void EvdevReader()
        {
            try
            {
                FileStream stream = new FileStream("/dev/input/event0", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byte[] buffer = new byte[24];

                while (true)
                {
                    stream.Read(buffer, 0, buffer.Length);

                    // parse timeval (8 bytes)
                    int offset = 8;
                    short type = BitConverter.ToInt16(new byte[] { buffer[offset], buffer[++offset] }, 0);
                    short code = BitConverter.ToInt16(new byte[] { buffer[++offset], buffer[++offset] }, 0);
                    int value = BitConverter.ToInt32(
                        new byte[] { buffer[++offset], buffer[++offset], buffer[++offset], buffer[++offset] }, 0);

                    if (value == 1 && code != 28)
                    {
                        //Console.WriteLine("Code={1}, Value={2}", type, code, value);

                        //var key = ((KEY_CODE_LINUX)code).ToString().Replace("KEY_", "");
                        //key = key.Replace("MINUS", "-");
                        //key = key.Replace("EQUAL", "=");
                        //key = key.Replace("SEMICOLON", ";");
                        //key = key.Replace("COMMA", ",");
                        //key = key.Replace("SLASH", "/");

                        if ((KEY_CODE_LINUX)code == KEY_CODE_LINUX.KEY_Q)
                            StaticKeyUpEvent?.Invoke(new KeyEventArgs() { Key = Key.Q, KeyModifiers = KeyModifiers.Control | KeyModifiers.Alt });
                        if ((KEY_CODE_LINUX)code == KEY_CODE_LINUX.KEY_S)
                            StaticKeyUpEvent?.Invoke(new KeyEventArgs() { Key = Key.Q, KeyModifiers = KeyModifiers.Control | KeyModifiers.Alt });
                    }

                    if ((KEY_CODE_LINUX)code == KEY_CODE_LINUX.KEY_Q)
                    {
                        return;
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public enum KEY_CODE_LINUX
        {
            KEY_1 = 2,
            KEY_2,
            KEY_3,
            KEY_4,
            KEY_5,
            KEY_6,
            KEY_7,
            KEY_8,
            KEY_9,
            KEY_0,
            KEY_MINUS,
            KEY_EQUAL,
            KEY_BACKSPACE,
            KEY_TAB,
            KEY_Q,
            KEY_W,
            KEY_E,
            KEY_R,
            KEY_T,
            KEY_Y,
            KEY_U,
            KEY_I,
            KEY_O,
            KEY_P,
            KEY_LEFTBRACE,
            KEY_RIGHTBRACE,
            KEY_ENTER,
            KEY_LEFTCTRL,
            KEY_A,
            KEY_S,
            KEY_D,
            KEY_F,
            KEY_G,
            KEY_H,
            KEY_J,
            KEY_K,
            KEY_L,
            KEY_SEMICOLON,
            KEY_APOSTROPHE,
            KEY_GRAVE,
            KEY_LEFTSHIFT,
            KEY_BACKSLASH,
            KEY_Z,
            KEY_X,
            KEY_C,
            KEY_V,
            KEY_B,
            KEY_N,
            KEY_M,
            KEY_COMMA,
            KEY_DOT,
            KEY_SLASH,
            KEY_RIGHTSHIFT,
            KEY_KPASTERISK,
            KEY_LEFTALT,
            KEY_SPACE,
            KEY_CAPSLOCK,
            KEY_F1,
            KEY_F2,
            KEY_F3,
            KEY_F4,
            KEY_F5,
            KEY_F6,
            KEY_F7,
            KEY_F8,
            KEY_F9,
            KEY_F10,
            KEY_NUMLOCK,
            KEY_SCROLLLOCK,
            KEY_KP7,
            KEY_KP8,
            KEY_KP9,
            KEY_KPMINUS,
            KEY_KP4,
            KEY_KP5,
            KEY_KP6,
            KEY_KPPLUS,
            KEY_KP1,
            KEY_KP2,
            KEY_KP3,
            KEY_KP0,
            KEY_KPDOT
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", CharSet = CharSet.Auto)]
        private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", CharSet = CharSet.Auto)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", CharSet = CharSet.Auto)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetLayeredWindowAttributes")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, int crKey, byte alpha, uint dwFlags);

        public static IntPtr GetWindowLong(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 4) return GetWindowLong32(hWnd, nIndex);
            else return GetWindowLongPtr64(hWnd, nIndex);
        }

        private void InitWindowClicks()
        {
            if (Design.IsDesignMode) return;

            StaticKeyUpEvent += MainWindow_StaticKeyUpEvent;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (PlatformImpl is not WindowImpl impl) return;

                Topmost = true;
                const int GWL_EXSTYLE = -20, WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20;
                int initialStyle = (int)GetWindowLong(impl.Handle.Handle, GWL_EXSTYLE);
                if (SetWindowLong(impl.Handle.Handle, GWL_EXSTYLE, initialStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT) == 0)
                    Close();
                SetLayeredWindowAttributes(impl.Handle.Handle, 0x000000, 255, 0x00000002);

                _listener = new LowLevelKeyboardListener();
                _listener.OnKeyPressed += MainWindow_StaticKeyUpEvent;
                _listener.HookKeyboard();
            }

            if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Task.Run(() => EvdevReader());
            }
        }

        private void MainWindow_StaticKeyUpEvent(KeyEventArgs e)
        {
            if (e.Key == Key.Q && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
                Close();

            if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            {
                OpenSettings();
            }
        }

        private void OpenSettings()
        {
            if (Design.IsDesignMode) return;

            if (_settingsWindow == null || _settingsWindow.IsVisible == false)
                (_settingsWindow = new SettingsWindow() { DataContext = new SettingsWindowViewModel(DataContext as MainWindowViewModel) })
                    .Show();
            else
            {
                _settingsWindow.Topmost = true;
                _settingsWindow.Activate();
                _settingsWindow.Topmost = false;
            }
        }

        public class LowLevelKeyboardListener
        {
            private const int WH_KEYBOARD_LL = 13;
            private const int WM_KEYDOWN = 0x0100;
            private const int WM_KEYUP = 0x0101;
            private const int WM_SYSKEYDOWN = 0x0104;
            private const int WM_SYSKEYUP = 0x0105;
            bool controlPressed, altPressed;

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool UnhookWindowsHookEx(IntPtr hhk);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern IntPtr GetModuleHandle(string lpModuleName);

            public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

            internal event KeyUpDelegate OnKeyPressed;

            private LowLevelKeyboardProc _proc;
            private IntPtr _hookID = IntPtr.Zero;

            public LowLevelKeyboardListener()
            {
                _proc = HookCallback;
            }

            public void HookKeyboard()
            {
                _hookID = SetHook(_proc);
            }

            public void UnHookKeyboard()
            {
                UnhookWindowsHookEx(_hookID);
            }

            private IntPtr SetHook(LowLevelKeyboardProc proc)
            {
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
                }
            }

            private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
            {
                if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                {
                    int vkCode = Marshal.ReadInt32(lParam);

                    if (vkCode == 162 /*VK_CONTROL*/)
                        controlPressed = true;
                    if (vkCode == 164 /*VK_MENU - alt*/)
                        altPressed = true;
                }
                if (nCode >= 0 && wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
                {
                    int vkCode = Marshal.ReadInt32(lParam);

                    if (vkCode == 162 /*VK_CONTROL*/)
                        controlPressed = false;
                    if (vkCode == 164 /*VK_MENU - alt*/)
                        altPressed = false;

                    if (vkCode == 81 /*Q*/ && altPressed && controlPressed)
                        OnKeyPressed?.Invoke(new KeyEventArgs() { Key = Key.Q, KeyModifiers = KeyModifiers.Control | KeyModifiers.Alt });
                    if (vkCode == 83 /*S*/ && altPressed && controlPressed)
                        OnKeyPressed?.Invoke(new KeyEventArgs() { Key = Key.S, KeyModifiers = KeyModifiers.Control | KeyModifiers.Alt });

                }

                return CallNextHookEx(_hookID, nCode, wParam, lParam);
            }
        }
    }
}
