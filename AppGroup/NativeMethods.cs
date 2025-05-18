using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AppGroup {
    public static class NativeMethods {



        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

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

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        public const int SW_HIDE = 0;
        public const int SW_SHOW = 5;
        public const int SW_RESTORE = 9;
        public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_ASYNCWINDOWPOS = 0x4000;
        public const int MDT_EFFECTIVE_DPI = 0;
        public const int SM_CXSCREEN = 0;
        public const int SM_CYSCREEN = 1;
        public const int WM_USER = 0x0400;
        public const int SW_MAXIMIZE = 3;
        public const int SW_MINIMIZE = 6;
        public const int SW_NORMAL = 1;
        // Window Messages
        public const int WM_COPYDATA = 0x004A;
       
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

  



     
       [DllImport("psapi.dll")]
        public static extern int EmptyWorkingSet(IntPtr hwProc);

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
                int spacing = 8; // Pixels of space between window and taskbar

                // Check if taskbar is auto-hidden and adjust spacing if needed
                bool isTaskbarAutoHide = IsTaskbarAutoHide();
                if (isTaskbarAutoHide) {
                    // When taskbar is auto-hidden, we need to ensure we provide enough space
                    // The typical auto-hide taskbar shows a few pixels even when hidden
                    int autoHideSpacing = (int)((baseTaskbarHeight) * dpiScale); // Additional space for auto-hide taskbar
                    spacing += autoHideSpacing;
                }

                // Determine taskbar position by comparing work area with monitor area
                TaskbarPosition taskbarPosition = GetTaskbarPosition(monitorInfo);

                // Initial position (centered horizontally relative to cursor)
                int x = cursorPos.X - (windowWidth / 2);
                int y;

                // Set position based on taskbar position
                switch (taskbarPosition) {
                    case TaskbarPosition.Top:
                        // For auto-hide, work area might be full screen, so use monitor top with spacing
                        if (isTaskbarAutoHide)
                            y = monitorInfo.rcMonitor.top + spacing;
                        else
                            y = monitorInfo.rcWork.top + spacing;
                        break;
                    case TaskbarPosition.Bottom:
                        // For auto-hide, work area might be full screen, so use monitor bottom with spacing
                        if (isTaskbarAutoHide)
                            y = monitorInfo.rcMonitor.bottom - windowHeight - spacing + 5;
                        else
                            y = monitorInfo.rcWork.bottom - windowHeight - spacing;
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
                            y = monitorInfo.rcMonitor.bottom - windowHeight -spacing;
                        else
                            y = monitorInfo.rcWork.bottom - windowHeight - spacing;
                        break;
                }

                // Ensure window stays within monitor bounds horizontally
                if (x < monitorInfo.rcWork.left)
                    x = monitorInfo.rcWork.left;
                if (x + windowWidth > monitorInfo.rcWork.right)
                    x = monitorInfo.rcWork.right - windowWidth;

                // Ensure window stays within monitor bounds vertically
                if (y < monitorInfo.rcWork.top)
                    y = monitorInfo.rcWork.top;
                if (y + windowHeight > monitorInfo.rcWork.bottom)
                    y = monitorInfo.rcWork.bottom - windowHeight;

                // Move the window (maintain size, only change position)
                NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER);
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error positioning window: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines the position of the taskbar based on monitor work area
        /// </summary>
        /// <summary>
        /// Determines the position of the taskbar based on monitor work area
        /// </summary>
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
        private static bool IsTaskbarAutoHide() {
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
        // Constants for SHAppBarMessage
        public const uint ABM_GETSTATE = 0x4;
        public const uint ABM_GETTASKBARPOS = 0x5;

        // Constants for taskbar edge positions
        public const int ABE_LEFT = 0;
        public const int ABE_TOP = 1;
        public const int ABE_RIGHT = 2;
        public const int ABE_BOTTOM = 3;


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
        private static float GetDpiScaleForMonitor(IntPtr hMonitor) {
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


        //public static void PositionWindowAboveTaskbar(IntPtr hWnd) {
        //    try {
        //        // Get window dimensions
        //        NativeMethods.RECT windowRect;
        //        if (!NativeMethods.GetWindowRect(hWnd, out windowRect)) {
        //            return;
        //        }

        //        int windowWidth = windowRect.right - windowRect.left;
        //        int windowHeight = windowRect.bottom - windowRect.top;

        //        // Get current cursor position
        //        NativeMethods.POINT cursorPos;
        //        if (!NativeMethods.GetCursorPos(out cursorPos)) {
        //            return;
        //        }

        //        // Calculate new position
        //        int x = cursorPos.X - (windowWidth / 2);

        //        // Get monitor information
        //        IntPtr monitor = NativeMethods.MonitorFromPoint(cursorPos, NativeMethods.MONITOR_DEFAULTTONEAREST);
        //        NativeMethods.MONITORINFO monitorInfo = new NativeMethods.MONITORINFO();
        //        monitorInfo.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));

        //        if (NativeMethods.GetMonitorInfo(monitor, ref monitorInfo)) {
        //            // Calculate position based on taskbar
        //            float dpiScale = GetDpiScaleForMonitor(monitor);
        //            int baseTaskbarHeight = 52;
        //            int taskbarHeight = (int)(baseTaskbarHeight * dpiScale);
        //            int y = monitorInfo.rcMonitor.bottom - windowHeight - taskbarHeight;

        //            //int workAreaDifference = monitorInfo.rcMonitor.bottom - monitorInfo.rcWork.bottom;

        //            //if (workAreaDifference > 5) {
        //            //    y = monitorInfo.rcMonitor.bottom - windowHeight - workAreaDifference;
        //            //}

        //            // Ensure window stays within monitor bounds horizontally
        //            if (x < monitorInfo.rcWork.left)
        //                x = monitorInfo.rcWork.left;
        //            if (x + windowWidth > monitorInfo.rcWork.right)
        //                x = monitorInfo.rcWork.right - windowWidth;

        //            // Move the window (maintain size, only change position)
        //            NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER);
        //        }
        //    }
        //    catch (Exception ex) {
        //        Debug.WriteLine($"Error positioning window: {ex.Message}");
        //    }
        //}

        //private static float GetDpiScaleForMonitor(IntPtr hMonitor) {
        //    try {
        //        if (Environment.OSVersion.Version.Major > 6 ||
        //            (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor >= 3)) {

        //            uint dpiX, dpiY;

        //            // Try to get DPI for the monitor
        //            if (NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out dpiX, out dpiY) == 0) {
        //                return dpiX / 96.0f;
        //            }
        //        }

        //        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) {
        //            return g.DpiX / 96.0f;
        //        }
        //    }
        //    catch {
        //        return 1.0f;
        //    }
        //}


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
