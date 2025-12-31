using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AppGroup {
    public static partial class NativeMethods {
        public static readonly IntPtr HWND_TOP = IntPtr.Zero;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SPI_GETWORKAREA = 0x0030;
        public const int SW_SHOW = 5;
        public const int SW_RESTORE = 9;
        public const int SW_SHOWNOACTIVATE = 4;
        public const int SW_HIDE = 0;
        public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_ASYNCWINDOWPOS = 0x4000;
        public const int MDT_EFFECTIVE_DPI = 0;
        public const int SM_CXSCREEN = 0;
        public const int SM_CYSCREEN = 1;
        public const int WM_USER = 0x0400;
        public const int SW_MAXIMIZE = 3;
        public const int SW_MINIMIZE = 6;
        public const int SW_NORMAL = 1;
        public const int SWP_HIDEWINDOW = 0x0080;
        public const int SHCNE_RENAMEITEM = 0x00000001;
        public const int SHCNE_CREATE = 0x00000002;
        public const int SHCNE_DELETE = 0x00000004;
        public const int SHCNE_UPDATEIMAGE = 0x00008000;
        public const int SHCNE_UPDATEDIR = 0x00001000;
        public const int SHCNE_RENAMEFOLDER = 0x00020000;
        public const uint SHCNF_PATH = 0x0005;
        public const uint SHCNF_IDLIST = 0x0000;
        public const uint RDW_ERASE = 0x0004;
        public const uint RDW_FRAME = 0x0400;
        public const uint RDW_INVALIDATE = 0x0001;
        public const uint WM_SHOWWINDOW = 0x0018;
        public const uint RDW_ALLCHILDREN = 0x0080;
        public const uint WM_TRAYICON = 0x8000;
        public const uint WM_COMMAND = 0x0111;
        public const uint WM_DESTROY = 0x0002;
        public const uint WM_LBUTTONDBLCLK = 0x0203;
        public const uint WM_RBUTTONUP = 0x0205;
        public const uint WM_NULL = 0x0000;
        public const uint NIF_MESSAGE = 0x00000001;
        public const uint NIF_ICON = 0x00000002;
        public const uint NIF_TIP = 0x00000004;
        public const uint NIM_ADD = 0x00000000;
        public const uint NIM_MODIFY = 0x00000001;
        public const uint NIM_DELETE = 0x00000002;
        public const uint IMAGE_ICON = 1;
        public const uint LR_LOADFROMFILE = 0x00000010;
        public const uint TPM_RETURNCMD = 0x0100;
        public const uint TPM_RIGHTBUTTON = 0x0002;
        public const int ID_SHOW = 1001;
        public const int ID_EXIT = 1002;
        public const uint MIN_ALL = 419;
        public const uint RESTORE_ALL = 416;
        public const uint SHCNE_ASSOCCHANGED = 0x08000000;
        public const uint SHCNF_FLUSH = 0x1000;
        public const int WM_COPYDATA = 0x004A;
        // Constants for SHAppBarMessage
        public const uint ABM_GETSTATE = 0x4;
        public const uint ABM_GETTASKBARPOS = 0x5;

        // Constants for taskbar edge positions
        public const int ABE_LEFT = 0;
        public const int ABE_TOP = 1;
        public const int ABE_RIGHT = 2;
        public const int ABE_BOTTOM = 3;


        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool AllowSetForegroundWindow(uint dwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FlashWindow(IntPtr hwnd, bool bInvert);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, out RECT pvParam, uint fWinIni);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }


        public static void ForceForegroundWindow(IntPtr hWnd) {

            
            if (GetForegroundWindow() == hWnd)
                return;


            // Get the thread ID of the foreground window
            IntPtr foregroundWindow = GetForegroundWindow();
            uint foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, out _);
            uint currentThreadId = GetCurrentThreadId();

            // Attach to the foreground thread's input queue
            if (currentThreadId != foregroundThreadId) {
                AttachThreadInput(currentThreadId, foregroundThreadId, true);
            }

            // Bring the window to the top and set it as the foreground window
            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);
            SetFocus(hWnd);

            // Detach from the foreground thread's input queue
            if (currentThreadId != foregroundThreadId) {
                AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
        }



        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")]
        public static extern IntPtr SetActiveWindow(IntPtr hWnd);



        [StructLayout(LayoutKind.Sequential)]
        public struct POINT {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

    
      

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);


        // Constants

        // Delegates
        public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        // Structures
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
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        // Replace your NOTIFYICONDATA structure with this fixed version:
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


        [DllImport("user32.dll")]
        public static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        public static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);


        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern uint RegisterWindowMessage(string lpString);



        // Also update the Shell_NotifyIcon declaration to explicitly use Unicode:
        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);


        // P/Invoke declarations
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern ushort RegisterClassEx(ref WNDCLASSEX lpWndClass);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateWindowEx(
            uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
            int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu,
            IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        public static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        public static extern IntPtr LoadCursor(IntPtr hInstance, uint lpCursorName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType,
            int cxDesired, int cyDesired, uint fuLoad);



        [DllImport("user32.dll")]
        public static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        public static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y,
            int nReserved, IntPtr hWnd, IntPtr prcRect);


        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool DestroyWindow(IntPtr hWnd);



        [DllImport("user32.dll")]
        public static extern bool DestroyMenu(IntPtr hMenu);



        [DllImport("shell32.dll")]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        public const int WM_SETICON = 0x0080;
        public const int ICON_SMALL = 0;
        public const int ICON_BIG = 1;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public static IntPtr LoadIcon(string iconPath) {
            return LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_DEFAULTSIZE | LR_LOADFROMFILE);
        }

        public const uint LR_DEFAULTSIZE = 0x00000040;

        public const int MONITOR_DEFAULTTOPRIMARY = 0x00000001;

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
        public const int SW_SHOWNORMAL = 1;

        //[DllImport("psapi.dll")]
        //public static extern int EmptyWorkingSet(IntPtr hwProc);
        //public const int SW_SHOWNOACTIVATE = 4;  // Shows window without activating/focusing it

        public static void PositionWindowAboveTaskbar(IntPtr hWnd) {
            try {



                // Get window dimensions
                NativeMethods.RECT windowRect;
                if (!NativeMethods.GetWindowRect(hWnd, out windowRect)) {
                    return;
                }
                int windowWidth = windowRect.right - windowRect.left;
                int windowHeight = windowRect.bottom - windowRect.top;

                // Get current cursor position
                NativeMethods.POINT cursorPos;
                if (!NativeMethods.GetCursorPos(out cursorPos)) {
                    return;
                }

                // Get monitor information
                IntPtr monitor = NativeMethods.MonitorFromPoint(cursorPos, NativeMethods.MONITOR_DEFAULTTONEAREST);
                NativeMethods.MONITORINFO monitorInfo = new NativeMethods.MONITORINFO();
                monitorInfo.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
                if (!NativeMethods.GetMonitorInfo(monitor, ref monitorInfo)) {
                    return;
                }


                // Calculate position based on taskbar position
                float dpiScale = GetDpiScaleForMonitor(monitor);
                int baseTaskbarHeight = 52;
                int taskbarHeight = (int)(baseTaskbarHeight * dpiScale);

                // Define a consistent spacing value for all sides
                int spacing = 6; // Pixels of space between window and taskbar

                // Check if taskbar is auto-hidden and adjust spacing if needed
                bool isTaskbarAutoHide = IsTaskbarAutoHide();
                Debug.WriteLine($"Taskbar Auto-Hide: {isTaskbarAutoHide}");

                // Determine taskbar position by comparing work area with monitor area
                TaskbarPosition taskbarPosition = GetTaskbarPosition(monitorInfo);
                Debug.WriteLine($"Taskbar Position: {taskbarPosition}");

                if (isTaskbarAutoHide) {
                    if (IsCursorOnTaskbar(cursorPos, monitorInfo, taskbarPosition)) {
                        int autoHideSpacing = (int)((baseTaskbarHeight) * dpiScale);
                        spacing = autoHideSpacing;    }
                    else {

                        spacing += (int)(5 * dpiScale); // DPI-scaled spacing for auto-hide

                    }
                }

               
                // Initial position (centered horizontally relative to cursor)
                int x = cursorPos.X - (windowWidth / 2);
                int y;

                // Set position based on taskbar position
                switch (taskbarPosition) {
                    //case TaskbarPosition.Top:
                    //    if (isTaskbarAutoHide)
                    //        y = monitorInfo.rcMonitor.top + spacing;
                    //    else
                    //        y = monitorInfo.rcMonitor.top + spacing;
                    //    break;
                    //case TaskbarPosition.Bottom:
                    //    if (isTaskbarAutoHide)
                    //        y = monitorInfo.rcMonitor.bottom - windowHeight - spacing + 5;
                    //    else
                    //        y = monitorInfo.rcMonitor.bottom - windowHeight - spacing + 5;
                    //    break;
                    case TaskbarPosition.Top:
                    case TaskbarPosition.Bottom:
                        // Check if cursor is on the taskbar
                        if (IsCursorOnTaskbar(cursorPos, monitorInfo, taskbarPosition)) {
                            // Position above/below taskbar
                            if (taskbarPosition == TaskbarPosition.Top)
                                y = monitorInfo.rcWork.top + spacing;
                            else
                                y = monitorInfo.rcWork.bottom - windowHeight - spacing;
                        }
                        else {
                            // Cursor is NOT on taskbar (desktop/explorer) - position near cursor
                            y = cursorPos.Y - windowHeight - spacing;
                            // Clamp to work area
                            if (y < monitorInfo.rcWork.top + spacing)
                                y = monitorInfo.rcWork.top + spacing;
                            if (y + windowHeight > monitorInfo.rcWork.bottom - spacing)
                                y = monitorInfo.rcWork.bottom - windowHeight - spacing;
                        }
                        break;
                    case TaskbarPosition.Left:
                        // For auto-hide, work area might be full screen, so use monitor left with spacing
                        if (isTaskbarAutoHide)
                            x = monitorInfo.rcMonitor.left + spacing;
                        else
                            x = monitorInfo.rcWork.left + spacing;
                        y = cursorPos.Y - (windowHeight / 2);
                        break;
                    case TaskbarPosition.Right:
                        // For auto-hide, work area might be full screen, so use monitor right with spacing
                        if (isTaskbarAutoHide)
                            x = monitorInfo.rcMonitor.right - windowWidth - spacing;
                        else
                            x = monitorInfo.rcWork.right - windowWidth - spacing;
                        y = cursorPos.Y - (windowHeight / 2);
                        break;
                    default:
                        // Default to bottom positioning
                        if (isTaskbarAutoHide)
                            y = monitorInfo.rcMonitor.bottom - windowHeight - spacing;
                        else
                            y = monitorInfo.rcWork.bottom - windowHeight - spacing;
                        break;
                }

                Debug.WriteLine($"Calculated Position (before bounds check): X={x}, Y={y}");

                // Ensure window stays within monitor bounds horizontally
                if (x < monitorInfo.rcWork.left)
                    x = monitorInfo.rcWork.left;
                if (x + windowWidth > monitorInfo.rcWork.right)
                    x = monitorInfo.rcWork.right - windowWidth;

                Debug.WriteLine($"Final Position (after bounds check): X={x}, Y={y}");
                Debug.WriteLine($"================================");

                // Move the window (maintain size, only change position)
                NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER);
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error positioning window: {ex.Message}");
            }
        }
     
        public static void PositionWindowBelowTaskbar(IntPtr hWnd) {
            try {
                // Get window dimensions
                NativeMethods.RECT windowRect;
                if (!NativeMethods.GetWindowRect(hWnd, out windowRect))
                    return;

                int windowWidth = windowRect.right - windowRect.left;
                int windowHeight = windowRect.bottom - windowRect.top;

                // Get cursor position
                NativeMethods.POINT cursorPos;
                if (!NativeMethods.GetCursorPos(out cursorPos))
                    return;

                // Get monitor info
                IntPtr monitor = NativeMethods.MonitorFromPoint(cursorPos, NativeMethods.MONITOR_DEFAULTTONEAREST);
                NativeMethods.MONITORINFO monitorInfo = new NativeMethods.MONITORINFO();
                monitorInfo.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
                if (!NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
                    return;

                // DPI scaling
                float dpiScale = GetDpiScaleForMonitor(monitor);
                int baseTaskbarHeight = 52;
                int taskbarHeight = (int)(baseTaskbarHeight * dpiScale);

                // Spacing
                int spacing = 99999;
                if (IsTaskbarAutoHide()) {
                    int autoHideSpacing = (int)(baseTaskbarHeight * dpiScale);
                    spacing += autoHideSpacing;
                }

                // Taskbar position
                TaskbarPosition taskbarPosition = GetTaskbarPosition(monitorInfo);

                // Initial x = center horizontally on cursor
                int x = cursorPos.X - (windowWidth / 2);
                int y;

                // Position BELOW taskbar (off-screen if bottom)
                switch (taskbarPosition) {
                    case TaskbarPosition.Top:
                        y = monitorInfo.rcMonitor.top + taskbarHeight + spacing;
                        break;

                    case TaskbarPosition.Bottom:
                        y = monitorInfo.rcMonitor.bottom + spacing; // 👈 off-screen below
                        break;

                    case TaskbarPosition.Left:
                        x = monitorInfo.rcMonitor.left + taskbarHeight + spacing;
                        y = cursorPos.Y - (windowHeight / 2);
                        break;

                    case TaskbarPosition.Right:
                        x = monitorInfo.rcMonitor.right - windowWidth - taskbarHeight - spacing;
                        y = cursorPos.Y - (windowHeight / 2);
                        break;

                    default:
                        y = monitorInfo.rcMonitor.bottom + spacing; // 👈 off-screen below
                        break;
                }

                // Keep window horizontally inside screen
                if (x < monitorInfo.rcWork.left)
                    x = monitorInfo.rcWork.left;
                if (x + windowWidth > monitorInfo.rcWork.right)
                    x = monitorInfo.rcWork.right - windowWidth;

                // Move window (don’t clamp vertically so it can go off-screen)
                NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0,
                    NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER);
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error positioning window: {ex.Message}");
            }
        }

        public static void PositionWindowOffScreen(IntPtr hWnd) {
            // Freeze window updates to prevent flicker
            NativeMethods.SetWindowPos(hWnd, IntPtr.Zero,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE |
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE |
                NativeMethods.SWP_HIDEWINDOW);

            // Move offscreen while hidden
            NativeMethods.SetWindowPos(hWnd, IntPtr.Zero,
                -1000, -1000, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER |
                NativeMethods.SWP_NOACTIVATE);
        }

        public static void PositionWindowOffScreenBelow(IntPtr hWnd) {
            try {
                // Get current cursor position
                NativeMethods.POINT cursorPos;
                if (!NativeMethods.GetCursorPos(out cursorPos)) {
                    // Fallback to center of screen if cursor position fails
                    cursorPos.X = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN) / 2;
                    cursorPos.Y = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN) / 2;
                }

                // Get window dimensions
                NativeMethods.RECT windowRect;
                if (!NativeMethods.GetWindowRect(hWnd, out windowRect)) {
                    return;
                }
                int windowWidth = windowRect.right - windowRect.left;
                int windowHeight = windowRect.bottom - windowRect.top;

                // Get primary monitor information (most reliable for off-screen positioning)
                IntPtr primaryMonitor = NativeMethods.MonitorFromPoint(new NativeMethods.POINT { X = 0, Y = 0 },
                    NativeMethods.MONITOR_DEFAULTTOPRIMARY);
                NativeMethods.MONITORINFO monitorInfo = new NativeMethods.MONITORINFO();
                monitorInfo.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));

                if (!NativeMethods.GetMonitorInfo(primaryMonitor, ref monitorInfo)) {
                    // Fallback to system metrics if monitor info fails
                    int screenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);

                    // Position window off-screen to the right, aligned with cursor Y position
                    int x = screenWidth + 100; // 100 pixels to the right of screen
                    int y = cursorPos.Y; // Align with cursor Y position

                    NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0,
                        NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER);
                    return;
                }

                // Calculate off-screen position to the right of the primary monitor, aligned with cursor Y
                int offScreenX = monitorInfo.rcMonitor.right + 100; // 100 pixels to the right of monitor
                int offScreenY = cursorPos.Y; // Use cursor Y position

                // Additional safety margin to ensure it's completely off-screen horizontally
                int safetyMargin = Math.Max(windowWidth, 200);
                offScreenX += safetyMargin;

                // Move the window to off-screen position
                NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, offScreenX, offScreenY, 0, 0,
                    NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER);

                Debug.WriteLine($"Window positioned off-screen at: ({offScreenX}, {offScreenY}), cursor at: ({cursorPos.X}, {cursorPos.Y})");
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error positioning window off-screen: {ex.Message}");
            }
        }

     
        private static bool IsCursorOnTaskbar(POINT cursorPos, MONITORINFO monitorInfo, TaskbarPosition taskbarPosition) {
            float dpiScale = GetDpiScaleForMonitor(MonitorFromPoint(cursorPos, MONITOR_DEFAULTTONEAREST));
            int taskbarThickness = (int)(52 * dpiScale); // Base taskbar height/width scaled

            switch (taskbarPosition) {
                case TaskbarPosition.Top:
                    return cursorPos.Y >= monitorInfo.rcMonitor.top &&
                           cursorPos.Y <= monitorInfo.rcWork.top + taskbarThickness;
                case TaskbarPosition.Bottom:
                    return cursorPos.Y >= monitorInfo.rcWork.bottom - taskbarThickness &&
                           cursorPos.Y <= monitorInfo.rcMonitor.bottom;
                case TaskbarPosition.Left:
                    return cursorPos.X >= monitorInfo.rcMonitor.left &&
                           cursorPos.X <= monitorInfo.rcWork.left + taskbarThickness;
                case TaskbarPosition.Right:
                    return cursorPos.X >= monitorInfo.rcWork.right - taskbarThickness &&
                           cursorPos.X <= monitorInfo.rcMonitor.right;
                default:
                    return false;
            }
        }
        private static TaskbarPosition GetTaskbarPosition(NativeMethods.MONITORINFO monitorInfo) {
            // If work area equals monitor area (which can happen with auto-hide taskbar), 
            // fall back to detecting taskbar position via other means
            if (monitorInfo.rcWork.top == monitorInfo.rcMonitor.top &&
                monitorInfo.rcWork.bottom == monitorInfo.rcMonitor.bottom &&
                monitorInfo.rcWork.left == monitorInfo.rcMonitor.left &&
                monitorInfo.rcWork.right == monitorInfo.rcMonitor.right) {
                // For auto-hide taskbar, try to get the position using AppBar info
                return GetTaskbarPositionFromAppBarInfo();
            }

            // Compare work area with screen area to determine taskbar position
            if (monitorInfo.rcWork.top > monitorInfo.rcMonitor.top)
                return TaskbarPosition.Top;
            else if (monitorInfo.rcWork.bottom < monitorInfo.rcMonitor.bottom)
                return TaskbarPosition.Bottom;
            else if (monitorInfo.rcWork.left > monitorInfo.rcMonitor.left)
                return TaskbarPosition.Left;
            else if (monitorInfo.rcWork.right < monitorInfo.rcMonitor.right)
                return TaskbarPosition.Right;
            else
                return TaskbarPosition.Bottom; // Default
        }

        private enum TaskbarPosition {
            Top,
            Bottom,
            Left,
            Right
        }
        public static bool IsTaskbarAutoHide() {
            NativeMethods.APPBARDATA appBarData = new NativeMethods.APPBARDATA();
            appBarData.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.APPBARDATA));

            // Get taskbar state
            IntPtr result = NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETSTATE, ref appBarData);

            // Check if auto-hide bit is set (ABS_AUTOHIDE = 0x01)
            return ((uint)result & 0x01) != 0;
        }


        /// <summary>
        /// Gets the taskbar position using AppBar information (works for auto-hide taskbars)
        /// </summary>
        private static TaskbarPosition GetTaskbarPositionFromAppBarInfo() {
            NativeMethods.APPBARDATA appBarData = new NativeMethods.APPBARDATA();
            appBarData.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.APPBARDATA));

            // Get taskbar position data
            IntPtr result = NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETTASKBARPOS, ref appBarData);
            if (result != IntPtr.Zero) {
                // uEdge field contains the edge the taskbar is docked to
                switch (appBarData.uEdge) {
                    case NativeMethods.ABE_TOP: return TaskbarPosition.Top;
                    case NativeMethods.ABE_BOTTOM: return TaskbarPosition.Bottom;
                    case NativeMethods.ABE_LEFT: return TaskbarPosition.Left;
                    case NativeMethods.ABE_RIGHT: return TaskbarPosition.Right;
                }
            }

            // Default to bottom if we couldn't determine
            return TaskbarPosition.Bottom;
        }


        [DllImport("shell32.dll")]
        public static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);


        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public IntPtr lParam;
        }
        public static float GetDpiScaleForMonitor(IntPtr hMonitor) {
            try {
                if (Environment.OSVersion.Version.Major > 6 ||
                    (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor >= 3)) {
                    uint dpiX, dpiY;
                    // Try to get DPI for the monitor
                    if (NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out dpiX, out dpiY) == 0) {
                        return dpiX / 96.0f;
                    }
                }
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) {
                    return g.DpiX / 96.0f;
                }
            }
            catch {
                return 1.0f;
            }
        }


        [StructLayout(LayoutKind.Sequential)]
        public struct SHFILEINFO {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }
        public const uint SHGFI_ICON = 0x000000100;
        public const uint SHGFI_LARGEICON = 0x000000000;
        public const uint SHGFI_SMALLICON = 0x000000001;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern uint ExtractIconEx(string szFileName, int nIconIndex,
       IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);


        [DllImport("shell32.dll")]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr handle);



        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hwnd);


        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);


        [DllImport("Shcore.dll")]
        public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFOEX {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);


    }
}