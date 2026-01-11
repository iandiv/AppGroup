using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace AppGroup {
    public class SystemTrayManager {
        private static IntPtr windowHandle;
        private static IntPtr hIcon;
        private static IntPtr hMenu;
        private static NativeMethods.WndProcDelegate wndProcDelegate;
        private static Action onShowCallback;
        private static Action onExitCallback;
        private static bool isInitialized = false;
        private static bool isVisible = false;

        // Add this field to store the TaskbarCreated message ID
        private static int WM_TASKBARCREATED;

        public static void Initialize(Action showCallback, Action exitCallback) {
            onShowCallback = showCallback;
            onExitCallback = exitCallback;
            isInitialized = true;

            // Register for the TaskbarCreated message
            WM_TASKBARCREATED = NativeMethods.RegisterWindowMessage("TaskbarCreated");

            // Check settings to determine if we should show the tray icon
            _ = InitializeBasedOnSettingsAsync();
        }

        private static async System.Threading.Tasks.Task InitializeBasedOnSettingsAsync() {
            try {
                var settings = await SettingsHelper.LoadSettingsAsync();
                if (settings.ShowSystemTrayIcon) {
                    ShowSystemTray();
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error loading settings for system tray: {ex.Message}");
                // Default to showing system tray on error
                ShowSystemTray();
            }
        }

        public static void ShowSystemTray() {
            if (!isInitialized) return;

            if (!isVisible) {
                InitializeSystemTray();
                isVisible = true;
            }
        }

        public static void HideSystemTray() {
            if (isVisible) {
                RemoveSystemTray();
                isVisible = false;
            }
        }

        private static void RemoveSystemTray() {
            if (windowHandle != IntPtr.Zero) {
                // Remove tray icon
                var notifyIconData = new NativeMethods.NOTIFYICONDATA {
                    cbSize = Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA)),
                    hWnd = windowHandle,
                    uID = 1
                };
                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref notifyIconData);
            }
        }

        private static void InitializeSystemTray() {
            try {
                // Create a hidden window for message processing (only if not already created)
                if (windowHandle == IntPtr.Zero) {
                    wndProcDelegate = new NativeMethods.WndProcDelegate(WndProc);

                    var wndClass = new NativeMethods.WNDCLASSEX {
                        cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.WNDCLASSEX)),
                        style = 0,
                        lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProcDelegate),
                        cbClsExtra = 0,
                        cbWndExtra = 0,
                        hInstance = NativeMethods.GetModuleHandle(null),
                        hIcon = IntPtr.Zero,
                        hCursor = NativeMethods.LoadCursor(IntPtr.Zero, 32512u), // IDC_ARROW
                        hbrBackground = IntPtr.Zero,
                        lpszMenuName = null,
                        lpszClassName = "WinUI3AppGroupTrayWndClass",
                        hIconSm = IntPtr.Zero
                    };

                    NativeMethods.RegisterClassEx(ref wndClass);

                    windowHandle = NativeMethods.CreateWindowEx(
                        0,
                        "WinUI3AppGroupTrayWndClass",
                        "WinUI3 AppGroup Tray Window",
                        0,
                        0, 0, 0, 0,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        NativeMethods.GetModuleHandle(null),
                        IntPtr.Zero);
                }

                CreateTrayIcon();
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error initializing system tray: {ex.Message}");
            }
        }

        private static void CreateTrayIcon() {
            try {
                // Load custom icon from file (only if not already loaded)
                if (hIcon == IntPtr.Zero) {
                    string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppGroup.ico");
                    if (File.Exists(iconPath)) {
                        hIcon = NativeMethods.LoadImage(IntPtr.Zero, iconPath, NativeMethods.IMAGE_ICON, 16, 16, NativeMethods.LR_LOADFROMFILE);
                        if (hIcon == IntPtr.Zero) {
                            // Fallback to system icon if loading fails
                            hIcon = NativeMethods.LoadImage(IntPtr.Zero, "#32516", NativeMethods.IMAGE_ICON, 16, 16, 0);
                        }
                    }
                    else {
                        // Fallback to system icon if file not found
                        hIcon = NativeMethods.LoadImage(IntPtr.Zero, "#32516", NativeMethods.IMAGE_ICON, 16, 16, 0);
                        Debug.WriteLine($"Icon file not found at: {iconPath}, using system icon");
                    }
                }

                // Create the tray icon
                var notifyIconData = new NativeMethods.NOTIFYICONDATA {
                    cbSize = Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA)),
                    hWnd = windowHandle,
                    uID = 1,
                    uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
                    uCallbackMessage = NativeMethods.WM_TRAYICON,
                    hIcon = hIcon,
                    szTip = "App Group"
                };

                bool result = NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref notifyIconData);
                if (!result) {
                    Debug.WriteLine("Failed to add system tray icon");
                }

                // Create popup menu (only if not already created)
                if (hMenu == IntPtr.Zero) {
                    hMenu = NativeMethods.CreatePopupMenu();
                    NativeMethods.AppendMenu(hMenu, 0, (uint)NativeMethods.ID_SHOW, "Show");
                    NativeMethods.AppendMenu(hMenu, 0, (uint)NativeMethods.ID_EXIT, "Exit");
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error creating tray icon: {ex.Message}");
            }
        }

        private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam) {
            // Handle TaskbarCreated message - this is sent when Explorer restarts
            if (msg == WM_TASKBARCREATED) {
                Debug.WriteLine("TaskbarCreated message received - recreating tray icon");
                if (isVisible) {
                    // Recreate the tray icon since Explorer was restarted
                    CreateTrayIcon();
                }
                return IntPtr.Zero;
            }

            switch (msg) {
                case NativeMethods.WM_TRAYICON:
                    HandleTrayIconMessage(lParam);
                    break;

                case NativeMethods.WM_COMMAND:
                    int command = wParam.ToInt32() & 0xFFFF;
                    HandleMenuCommand(command);
                    break;

                case NativeMethods.WM_DESTROY:
                    Cleanup();
                    break;
            }

            return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private static void HandleTrayIconMessage(IntPtr lParam) {
            switch (lParam.ToInt32()) {
                case (int)NativeMethods.WM_LBUTTONDBLCLK:
                    // Double-click to show main window
                    onShowCallback?.Invoke();
                    break;

                case (int)NativeMethods.WM_RBUTTONUP:
                    // Right-click to show context menu
                    ShowContextMenu();
                    break;
            }
        }

        private static void HandleMenuCommand(int command) {
            switch (command) {
                case NativeMethods.ID_SHOW:
                    onShowCallback?.Invoke();
                    break;

                case NativeMethods.ID_EXIT:
                    onExitCallback?.Invoke();
                    break;
            }
        }

        private static void ShowContextMenu() {
            if (hMenu != IntPtr.Zero) {
                // Get cursor position
                NativeMethods.POINT pt;
                NativeMethods.GetCursorPos(out pt);

                // CRITICAL FIX: Set foreground window and post a dummy message
                // This ensures the menu will dismiss properly when clicking outside
                NativeMethods.SetForegroundWindow(windowHandle);

                // Show the context menu
                uint result = NativeMethods.TrackPopupMenu(
                    hMenu,
                    NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_RIGHTBUTTON,
                    pt.X, pt.Y,
                    0,
                    windowHandle,
                    IntPtr.Zero);

                // CRITICAL FIX: Post a dummy message to ensure menu closes properly
                NativeMethods.PostMessage(windowHandle, NativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);

                // Handle the menu selection
                if (result != 0) {
                    HandleMenuCommand((int)result);
                }
            }
        }

        public static void UpdateTooltip(string tooltip) {
            if (windowHandle != IntPtr.Zero && isVisible) {
                var notifyIconData = new NativeMethods.NOTIFYICONDATA {
                    cbSize = Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA)),
                    hWnd = windowHandle,
                    uID = 1,
                    uFlags = NativeMethods.NIF_TIP,
                    szTip = tooltip
                };

                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref notifyIconData);
            }
        }

        public static void Cleanup() {
            if (windowHandle != IntPtr.Zero) {
                // Remove tray icon
                var notifyIconData = new NativeMethods.NOTIFYICONDATA {
                    cbSize = Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA)),
                    hWnd = windowHandle,
                    uID = 1
                };
                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref notifyIconData);

                // Cleanup resources
                if (hIcon != IntPtr.Zero) {
                    NativeMethods.DestroyIcon(hIcon);
                    hIcon = IntPtr.Zero;
                }

                if (hMenu != IntPtr.Zero) {
                    NativeMethods.DestroyMenu(hMenu);
                    hMenu = IntPtr.Zero;
                }

                if (windowHandle != IntPtr.Zero) {
                    NativeMethods.DestroyWindow(windowHandle);
                    windowHandle = IntPtr.Zero;
                }

                isVisible = false;
            }
        }
    }
}