using Microsoft.UI.Windowing;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace AppGroup {
    public static partial class NativeMethods {

        #region Window Messages & Command IDs

        public const int WM_USER = 0x0400;
        public const int WM_COPYDATA = 0x004A;
        public const int WM_SETICON = 0x0080;
        public const uint WM_SHOWWINDOW = 0x0018;
        public const uint WM_COMMAND = 0x0111;
        public const uint WM_DESTROY = 0x0002;
        public const uint WM_LBUTTONDBLCLK = 0x0203;
        public const uint WM_RBUTTONUP = 0x0205;
        public const uint WM_NULL = 0x0000;
        public const uint WM_TRAYICON = 0x8000;

        public const int ID_SHOW = 1001;
        public const int ID_EXIT = 1002;

        // Registered at startup — unique cross-process message
        public static readonly int WM_UPDATE_GROUP = RegisterWindowMessage("AppGroup.WM_UPDATE_GROUP");

        #endregion

        #region ShowWindow / SetWindowPos Flags

        public const int SW_HIDE = 0;
        public const int SW_NORMAL = 1;
        public const int SW_SHOWNORMAL = 1;
        public const int SW_MINIMIZE = 6;
        public const int SW_MAXIMIZE = 3;
        public const int SW_SHOW = 5;
        public const int SW_RESTORE = 9;
        public const int SW_SHOWNOACTIVATE = 4;

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_FRAMECHANGED = 0x0020;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const int SWP_HIDEWINDOW = 0x0080;
        public const uint SWP_ASYNCWINDOWPOS = 0x4000;

        #endregion

        #region Window Style Flags

        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int GWL_EXSTYLE = -20;
        public const int GWL_WNDPROC = -4;

        public const int ICON_SMALL = 0;
        public const int ICON_BIG = 1;

        #endregion

        #region Monitor / DPI Constants

        public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        public const int MONITOR_DEFAULTTOPRIMARY = 0x00000001;
        public const uint SPI_GETWORKAREA = 0x0030;
        public const int MDT_EFFECTIVE_DPI = 0;
        public const int SM_CXSCREEN = 0;
        public const int SM_CYSCREEN = 1;

        #endregion

        #region DWM Constants

        public const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;

        #endregion

        #region Layered Window

        public const int LWA_ALPHA = 0x00000002;

        #endregion

        #region Notify Icon (Tray)

        public const uint NIF_MESSAGE = 0x00000001;
        public const uint NIF_ICON = 0x00000002;
        public const uint NIF_TIP = 0x00000004;
        public const uint NIM_ADD = 0x00000000;
        public const uint NIM_MODIFY = 0x00000001;
        public const uint NIM_DELETE = 0x00000002;

        #endregion

        #region Shell / SHChangeNotify

        public const int SHCNE_RENAMEITEM = 0x00000001;
        public const int SHCNE_CREATE = 0x00000002;
        public const int SHCNE_DELETE = 0x00000004;
        public const int SHCNE_UPDATEIMAGE = 0x00008000;
        public const int SHCNE_UPDATEDIR = 0x00001000;
        public const int SHCNE_RENAMEFOLDER = 0x00020000;
        public const uint SHCNE_ASSOCCHANGED = 0x08000000;
        public const uint SHCNF_PATH = 0x0005;
        public const uint SHCNF_IDLIST = 0x0000;
        public const uint SHCNF_FLUSH = 0x1000;

        #endregion

        #region AppBar (Taskbar)

        public const uint ABM_GETSTATE = 0x4;
        public const uint ABM_GETTASKBARPOS = 0x5;
        public const int ABE_LEFT = 0;
        public const int ABE_TOP = 1;
        public const int ABE_RIGHT = 2;
        public const int ABE_BOTTOM = 3;

        #endregion

        #region Shell Icon
        [DllImport("ole32.dll")]
public static extern int CoInitializeEx(IntPtr pvReserved, int dwCoInit);

[DllImport("ole32.dll")]
public static extern void CoUninitialize();

public const int COINIT_APARTMENTTHREADED = 0x2;
        public const uint IMAGE_ICON = 1;
        public const uint LR_LOADFROMFILE = 0x00000010;
        public const uint LR_DEFAULTSIZE = 0x00000040;
        public const uint TPM_RETURNCMD = 0x0100;
        public const uint TPM_RIGHTBUTTON = 0x0002;
        public const uint MIN_ALL = 419;
        public const uint RESTORE_ALL = 416;
        public const uint SHGFI_ICON = 0x000000100;
        public const uint SHGFI_LARGEICON = 0x000000000;
        public const uint SHGFI_SMALLICON = 0x000000001;
        public const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        public const uint SHGFI_SYSICONINDEX = 0x4000;
        public const int SHIL_JUMBO = 0x4;

        #endregion

        #region RedrawWindow Flags

        public const uint RDW_ERASE = 0x0004;
        public const uint RDW_FRAME = 0x0400;
        public const uint RDW_INVALIDATE = 0x0001;
        public const uint RDW_ALLCHILDREN = 0x0080;

        #endregion

        #region WinEvent Hook

        public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        public const uint WINEVENT_OUTOFCONTEXT = 0x0000;

        #endregion

        #region GetWindow

        public const uint GW_OWNER = 4;

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Delegates

        public delegate void WinEventDelegate(
            IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        public delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

        public delegate IntPtr SubclassProc(
            IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam,
            IntPtr uIdSubclass, IntPtr dwRefData);
        
        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Structs

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT {
            public int left, top, right, bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFOEX {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WNDCLASSEX {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NOTIFYICONDATA {
            public int cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public IntPtr lParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SHFILEINFO {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DEVMODE {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGELISTDRAWPARAMS {
            public int cbSize;
            public IntPtr himl;
            public int i, x, y, cx, cy, xBitmap, yBitmap;
            public IntPtr hdcDst;
            public int rgbBk, rgbFg, fStyle, dwRop, fState, Frame, crEffect;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region COM Interfaces

        [ComImport]
        [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IImageList {
            [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
            [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, ref int pi);
            [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
            [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
            [PreserveSig] int AddMasked(IntPtr hbmImage, int crMask, ref int pi);
            [PreserveSig] int Draw(ref IMAGELISTDRAWPARAMS pimldp);
            [PreserveSig] int Remove(int i);
            [PreserveSig] int GetIcon(int i, int flags, ref IntPtr picon);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region P/Invoke — user32.dll
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DrawIconEx(
    IntPtr hdc, int xLeft, int yTop,
    IntPtr hIcon, int cxWidth, int cyHeight,
    int istepIfAniCur, IntPtr hbrFlickerFreeDraw, int diFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool AllowSetForegroundWindow(uint dwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(
            IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        // Returns thread ID; lpdwProcessId receives the process ID
        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        // Overload when process ID is not needed
        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FlashWindow(IntPtr hwnd, bool bInvert);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        [DllImport("user32.dll")]
        public static extern bool RedrawWindow(
            IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindowEx(
            IntPtr parentHandle, IntPtr childAfter,
            string className, string windowTitle);

        [DllImport("user32.dll")]
        public static extern bool EnumThreadWindows(
            int dwThreadId, EnumThreadDelegate lpfn, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        // ── SendMessage overloads ────────────────────────────────────────────
        // Primary: wParam/lParam as IntPtr (preferred for pointer-sized values)
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SendMessage(
            IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        // Convenience overload for int wParam/lParam (e.g. icon index, simple flags)
        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(
            IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        // Convenience overload for int wParam/lParam with int return (legacy callers)
        [DllImport("user32.dll")]
        public static extern int SendMessage(
            IntPtr hWnd, int msg, int wParam, int lParam);
        // ────────────────────────────────────────────────────────────────────

        [DllImport("user32.dll")]
        public static extern bool PostMessage(
            IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemParametersInfo(
            uint uiAction, uint uiParam, out RECT pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern ushort RegisterClassEx(ref WNDCLASSEX lpWndClass);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateWindowEx(
            uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
            int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        public static extern IntPtr DefWindowProc(
            IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        public static extern IntPtr LoadCursor(IntPtr hInstance, uint lpCursorName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr LoadImage(
            IntPtr hinst, string lpszName, uint uType,
            int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("user32.dll")]
        public static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool AppendMenu(
            IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        public static extern uint TrackPopupMenu(
            IntPtr hMenu, uint uFlags, int x, int y,
            int nReserved, IntPtr hWnd, IntPtr prcRect);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        public static extern IntPtr CallWindowProc(
            IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool SetLayeredWindowAttributes(
            IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool AnimateWindow(IntPtr hWnd, int dwTime, int dwFlags);

        public const int AW_BLEND = 0x00080000;
        public const int AW_ACTIVATE = 0x00020000;

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        // Overload for MONITORINFO (no device name)
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        // Overload for MONITORINFOEX (includes device name string)
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("user32.dll")]
        public static extern bool MoveWindow(
            IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);  // FIX: was missing [DllImport]

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWinEventHook(
            uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc,
            uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr handle);

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region P/Invoke — kernel32.dll

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region P/Invoke — comctl32.dll (Subclassing)

        [DllImport("comctl32.dll", SetLastError = true)]
        public static extern bool SetWindowSubclass(
            IntPtr hWnd, SubclassProc pfnSubclass,
            IntPtr uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll", SetLastError = true)]
        public static extern bool RemoveWindowSubclass(
            IntPtr hWnd, SubclassProc pfnSubclass, IntPtr uIdSubclass);

        [DllImport("comctl32.dll", SetLastError = true)]
        public static extern IntPtr DefSubclassProc(
            IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region P/Invoke — dwmapi.dll

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(
            IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region P/Invoke — shell32.dll

        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("shell32.dll")]
        public static extern void SHChangeNotify(
            uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        [DllImport("shell32.dll")]
        public static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern uint ExtractIconEx(
            string szFileName, int nIconIndex,
            IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

        [DllImport("shell32.dll")]
        public static extern IntPtr SHGetFileInfo(
            string pszPath, uint dwFileAttributes,
            ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern void SetCurrentProcessExplicitAppUserModelID(
            [MarshalAs(UnmanagedType.LPWStr)] string AppID);

        [DllImport("shell32.dll", EntryPoint = "#727")]
        public static extern int SHGetImageList(
            int iImageList, ref Guid riid, out IImageList ppv);

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region P/Invoke — Shcore.dll

        // Returns HRESULT; 0 (S_OK) = success
        [DllImport("Shcore.dll")]
        public static extern int GetDpiForMonitor(
            IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region P/Invoke — gdi32 / System.Drawing helper

        // (No raw GDI P/Invokes needed; DpiX is retrieved via System.Drawing.Graphics)

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region P/Invoke — user32 (display settings)

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        public static extern bool EnumDisplaySettings(
            string deviceName, int modeNum, ref DEVMODE devMode);

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region AppGroup Message ID

        public const int APPGROUP_SHOW_MAIN = 0x1;

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region HWND Constants

        public static readonly IntPtr HWND_TOP = IntPtr.Zero;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Static Helper Methods

        public static IntPtr LoadIcon(string iconPath) =>
            LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_DEFAULTSIZE | LR_LOADFROMFILE);

        public static void SendString(IntPtr targetWindow, string message) {
            var cds = new COPYDATASTRUCT {
                dwData = (IntPtr)100,
                cbData = (message.Length + 1) * 2,
                lpData = Marshal.StringToHGlobalUni(message)
            };
            try {
                IntPtr cdsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(cds));
                Marshal.StructureToPtr(cds, cdsPtr, false);
                SendMessage(targetWindow, (uint)WM_COPYDATA, IntPtr.Zero, cdsPtr);
                Marshal.FreeHGlobal(cdsPtr);
            }
            finally {
                Marshal.FreeHGlobal(cds.lpData);
            }
        }

        public static void ForceForegroundWindow(IntPtr hWnd) {
            if (GetForegroundWindow() == hWnd) return;

            IntPtr foregroundWindow = GetForegroundWindow();
            uint foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, out _);
            uint currentThreadId = GetCurrentThreadId();

            if (currentThreadId != foregroundThreadId)
                AttachThreadInput(currentThreadId, foregroundThreadId, true);

            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);
            SetFocus(hWnd);

            if (currentThreadId != foregroundThreadId)
                AttachThreadInput(currentThreadId, foregroundThreadId, false);
        }

        public static float GetDpiScaleForMonitor(IntPtr hMonitor) {
            try {
                var v = Environment.OSVersion.Version;
                if (v.Major > 6 || (v.Major == 6 && v.Minor >= 3)) {
                    if (GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0)
                        return dpiX / 96.0f;
                }
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    return g.DpiX / 96.0f;
            }
            catch { return 1.0f; }
        }

        public static bool IsTaskbarAutoHide() {
            var data = new APPBARDATA();
            data.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));
            IntPtr result = SHAppBarMessage(ABM_GETSTATE, ref data);
            return ((uint)result & 0x01) != 0;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Taskbar-Aware Window Positioning

        private enum TaskbarPosition { Top, Bottom, Left, Right }

        private static bool IsCursorOnTaskbar(
                POINT cursorPos, MONITORINFO mi, TaskbarPosition pos) {
            return pos switch {
                TaskbarPosition.Top => cursorPos.Y < mi.rcWork.top,
                TaskbarPosition.Bottom => cursorPos.Y >= mi.rcWork.bottom,
                TaskbarPosition.Left => cursorPos.X < mi.rcWork.left,
                TaskbarPosition.Right => cursorPos.X >= mi.rcWork.right,
                _ => false
            };
        }

        private static TaskbarPosition GetTaskbarPosition(MONITORINFO mi) {
            // When work area == monitor area the taskbar is auto-hidden; query AppBar directly
            bool workEqualsMonitor =
                mi.rcWork.top == mi.rcMonitor.top &&
                mi.rcWork.bottom == mi.rcMonitor.bottom &&
                mi.rcWork.left == mi.rcMonitor.left &&
                mi.rcWork.right == mi.rcMonitor.right;

            if (workEqualsMonitor)
                return GetTaskbarPositionFromAppBarInfo();

            if (mi.rcWork.top > mi.rcMonitor.top) return TaskbarPosition.Top;
            if (mi.rcWork.bottom < mi.rcMonitor.bottom) return TaskbarPosition.Bottom;
            if (mi.rcWork.left > mi.rcMonitor.left) return TaskbarPosition.Left;
            if (mi.rcWork.right < mi.rcMonitor.right) return TaskbarPosition.Right;
            return TaskbarPosition.Bottom;
        }

        private static TaskbarPosition GetTaskbarPositionFromAppBarInfo() {
            var data = new APPBARDATA();
            data.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));
            if (SHAppBarMessage(ABM_GETTASKBARPOS, ref data) != IntPtr.Zero) {
                return data.uEdge switch {
                    (uint)ABE_TOP => TaskbarPosition.Top,
                    (uint)ABE_BOTTOM => TaskbarPosition.Bottom,
                    (uint)ABE_LEFT => TaskbarPosition.Left,
                    (uint)ABE_RIGHT => TaskbarPosition.Right,
                    _ => TaskbarPosition.Bottom
                };
            }
            return TaskbarPosition.Bottom;
        }

        public static void PositionWindowAboveTaskbar(IntPtr hWnd, bool show = true) {
            try {
                if (!GetWindowRect(hWnd, out RECT wr)) return;
                int windowWidth = wr.right - wr.left;
                int windowHeight = wr.bottom - wr.top;

                if (!GetCursorPos(out POINT cursor)) return;

                IntPtr monitor = MonitorFromPoint(cursor, MONITOR_DEFAULTTONEAREST);
                var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
                if (!GetMonitorInfo(monitor, ref mi)) return;

                float dpiScale = GetDpiScaleForMonitor(monitor);
                int baseTaskbarH = 52;
                bool autoHide = IsTaskbarAutoHide();
                TaskbarPosition tbPos = GetTaskbarPosition(mi);

                int spacing;
                if (autoHide) {
                    spacing = IsCursorOnTaskbar(cursor, mi, tbPos)
                        ? (int)(baseTaskbarH * dpiScale)
                        : (int)(11 * dpiScale);    // 6 + 5 original logic
                }
                else {
                    spacing = tbPos == TaskbarPosition.Top
                        ? (int)(10 * dpiScale)
                        : (int)(6 * dpiScale);
                }

                int x = cursor.X - windowWidth / 2;
                int y;

                switch (tbPos) {
                    case TaskbarPosition.Top:
                    case TaskbarPosition.Bottom:
                        if (IsCursorOnTaskbar(cursor, mi, tbPos)) {
                            y = tbPos == TaskbarPosition.Top
                                ? mi.rcWork.top + spacing
                                : mi.rcWork.bottom - windowHeight - spacing;
                        }
                        else {
                            y = cursor.Y - windowHeight - spacing;
                            if (y < mi.rcWork.top + spacing)
                                y = mi.rcWork.top + spacing;
                        }
                        break;
                    case TaskbarPosition.Left:
                        x = mi.rcWork.left + spacing;
                        y = cursor.Y - windowHeight / 2;
                        // Clamp vertically within work area
                        if (y < mi.rcWork.top + spacing)
                            y = mi.rcWork.top + spacing;
                        if (y + windowHeight > mi.rcWork.bottom - spacing)
                            y = mi.rcWork.bottom - windowHeight - spacing;
                        break;

                    case TaskbarPosition.Right:
                        x = mi.rcWork.right - windowWidth - spacing;
                        y = cursor.Y - windowHeight / 2;
                        // Clamp vertically within work area
                        if (y < mi.rcWork.top + spacing)
                            y = mi.rcWork.top + spacing;
                        if (y + windowHeight > mi.rcWork.bottom - spacing)
                            y = mi.rcWork.bottom - windowHeight - spacing;
                        break;

                    default:
                        y = autoHide
                            ? mi.rcMonitor.bottom - windowHeight - spacing
                            : mi.rcWork.bottom - windowHeight - spacing;
                        break;
                }

                // Clamp horizontally
                if (x < mi.rcWork.left) x = mi.rcWork.left;
                if (x + windowWidth > mi.rcWork.right) x = mi.rcWork.right - windowWidth;

                Debug.WriteLine($"PositionAboveTaskbar → X={x}, Y={y}");

                SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0,
                    SWP_NOSIZE | SWP_NOZORDER | (show ? SWP_SHOWWINDOW : 0));
            }
            catch (Exception ex) {
                Debug.WriteLine($"PositionWindowAboveTaskbar error: {ex.Message}");
            }
        }

        public static void PositionWindowBelowTaskbar(IntPtr hWnd) {
            try {
                if (!GetWindowRect(hWnd, out RECT wr)) return;
                int windowWidth = wr.right - wr.left;
                int windowHeight = wr.bottom - wr.top;

                if (!GetCursorPos(out POINT cursor)) return;

                IntPtr monitor = MonitorFromPoint(cursor, MONITOR_DEFAULTTONEAREST);
                var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
                if (!GetMonitorInfo(monitor, ref mi)) return;

                float dpiScale = GetDpiScaleForMonitor(monitor);
                int taskbarHeight = (int)(52 * dpiScale);
                int spacing = 99999;
                if (IsTaskbarAutoHide())
                    spacing += (int)(52 * dpiScale);

                TaskbarPosition tbPos = GetTaskbarPosition(mi);
                int x = cursor.X - windowWidth / 2;
                int y;

                switch (tbPos) {
                    case TaskbarPosition.Top:
                        y = mi.rcMonitor.top + taskbarHeight + spacing;
                        break;
                    case TaskbarPosition.Bottom:
                        y = mi.rcMonitor.bottom + spacing;
                        break;
                    case TaskbarPosition.Left:
                        x = mi.rcMonitor.left + taskbarHeight + spacing;
                        y = cursor.Y - windowHeight / 2;
                        break;
                    case TaskbarPosition.Right:
                        x = mi.rcMonitor.right - windowWidth - taskbarHeight - spacing;
                        y = cursor.Y - windowHeight / 2;
                        break;
                    default:
                        y = mi.rcMonitor.bottom + spacing;
                        break;
                }

                if (x < mi.rcWork.left) x = mi.rcWork.left;
                if (x + windowWidth > mi.rcWork.right) x = mi.rcWork.right - windowWidth;

                SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER);
            }
            catch (Exception ex) {
                Debug.WriteLine($"PositionWindowBelowTaskbar error: {ex.Message}");
            }
        }

        public static void PositionWindowOffScreen(IntPtr hWnd) {
            // Hide first to avoid flicker, then park below the screen
            int screenHeight = (int)DisplayArea.Primary.WorkArea.Height;
                
            SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | (uint)SWP_HIDEWINDOW);

            SetWindowPos(hWnd, IntPtr.Zero, 0, screenHeight + 100, 0, 0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }

        public static void PositionWindowOffScreenBelow(IntPtr hWnd) {
            try {
                if (!GetCursorPos(out POINT cursor)) {
                    cursor.X = GetSystemMetrics(SM_CXSCREEN) / 2;
                    cursor.Y = GetSystemMetrics(SM_CYSCREEN) / 2;
                }

                if (!GetWindowRect(hWnd, out RECT wr)) return;
                int windowWidth = wr.right - wr.left;
                int windowHeight = wr.bottom - wr.top;

                IntPtr primary = MonitorFromPoint(
                    new POINT { X = 0, Y = 0 }, (uint)MONITOR_DEFAULTTOPRIMARY);
                var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };

                if (!GetMonitorInfo(primary, ref mi)) {
                    int sx = GetSystemMetrics(SM_CXSCREEN);
                    SetWindowPos(hWnd, IntPtr.Zero, sx + 100, cursor.Y, 0, 0,
                        SWP_NOSIZE | SWP_NOZORDER);
                    return;
                }

                int safetyMargin = Math.Max(windowWidth, 200);
                int offX = mi.rcMonitor.right + 100 + safetyMargin;
                int offY = cursor.Y;

                SetWindowPos(hWnd, IntPtr.Zero, offX, offY, 0, 0,
                    SWP_NOSIZE | SWP_NOZORDER);

                Debug.WriteLine(
                    $"PositionOffScreenBelow → ({offX},{offY}), cursor at ({cursor.X},{cursor.Y})");
            }
            catch (Exception ex) {
                Debug.WriteLine($"PositionWindowOffScreenBelow error: {ex.Message}");
            }
        }

        #endregion
    }
}