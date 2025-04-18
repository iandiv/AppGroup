using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BackgroundClient {
    internal class Program {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;

        private static Dictionary<string, GroupData> activeGroups;
        private static CancellationTokenSource cancellationTokenSource;

        // Constants for the system tray
        private const int WM_APP = 0x8000;
        private const int WM_TRAYICON = WM_APP + 1;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_COMMAND = 0x0111;
        private const int NIM_ADD = 0x00000000;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIM_DELETE = 0x00000002;
        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_TIP = 0x00000004;
        private const int TPM_RIGHTBUTTON = 0x0002;

        // ID values for the menu items
        private const int ID_SHOW = 1000;
        private const int ID_EXIT = 1001;

        // Tray icon data
        private static IntPtr windowHandle;
        private static IntPtr hIcon;
        private static IntPtr hMenu;

        // Add this to hide the console window
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName,
            uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu,
            IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        static extern bool TranslateMessage([In] ref MSG lpMsg);

        [DllImport("user32.dll")]
        static extern IntPtr DispatchMessage([In] ref MSG lpMsg);

        [DllImport("shell32.dll")]
        static extern bool Shell_NotifyIcon(int dwMessage, [In] ref NOTIFYICONDATA pnid);

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll")]
        public static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        public static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        public static extern int TrackPopupMenuEx(IntPtr hMenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

        [DllImport("user32.dll")]
        static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll")]
        static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        // For loading custom icon
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x00000010;

        // Delegate for window procedure
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private static WndProcDelegate wndProcDelegate;

        [StructLayout(LayoutKind.Sequential)]
        public struct NOTIFYICONDATA {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT {
            public int X;
            public int Y;
        }

        public struct MSG {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
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
            public string lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }
        private static FileSystemWatcher fileWatcher;
        private static object _fileChangeLock = new object();
        private static DateTime _lastFileChangeTime = DateTime.MinValue;
        private static readonly TimeSpan _debounceInterval = TimeSpan.FromSeconds(1);
        private static bool _fileChangeHandlingInProgress = false;
        private static HashSet<string> _previousGroupNames = new HashSet<string>();
        static void Main(string[] args) {
            // Create mutex to ensure only one instance runs
            bool createdNew;
            using (var mutex = new Mutex(true, "AppGroupBackgroundClientMutex", out createdNew)) {
                if (!createdNew) {
                    return;
                }

                // Hide console window
                IntPtr consoleWindow = GetConsoleWindow();
                if (consoleWindow != IntPtr.Zero) {
                    ShowWindow(consoleWindow, SW_HIDE);
                }


                // Initialize cancellation token source
                cancellationTokenSource = new CancellationTokenSource();

                // Initialize system tray with native API
                InitializeSystemTray();
                SetupFileWatcher();

                Task.Run(() => PreloadPopupWindows());
                Task.Run(() => MonitorGroupWindows(cancellationTokenSource.Token));

                RunMessageLoop();
            }
        }


        private static void SetupFileWatcher() {
            string jsonFilePath = GetDefaultConfigPath();
            if (File.Exists(jsonFilePath)) {
                fileWatcher = new FileSystemWatcher();
                fileWatcher.Path = Path.GetDirectoryName(jsonFilePath);
                fileWatcher.Filter = Path.GetFileName(jsonFilePath);
                fileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                fileWatcher.Changed += OnJsonFileChanged;
                fileWatcher.EnableRaisingEvents = true;
                Debug.WriteLine($"File watcher set up for: {jsonFilePath}");

                _previousGroupNames = ExtractGroupNames(jsonFilePath);
            }
            else {
                Debug.WriteLine($"JSON file not found at path: {jsonFilePath}");
            }
        }

        private static void OnJsonFileChanged(object sender, FileSystemEventArgs e) {
            lock (_fileChangeLock) {
                DateTime now = DateTime.Now;

                if (_fileChangeHandlingInProgress) {
                    _lastFileChangeTime = now;
                    Debug.WriteLine("File change detected while another is being processed - updated timestamp");
                    return;
                }

                if ((now - _lastFileChangeTime) < _debounceInterval) {
                    _lastFileChangeTime = now;
                    Debug.WriteLine("File change debounced - will handle after cooldown period");

                    if (!_fileChangeHandlingInProgress) {
                        _fileChangeHandlingInProgress = true;
                        Task.Run(async () =>
                        {
                            await DebouncedHandleFileChange();
                        });
                    }
                    return;
                }

                _lastFileChangeTime = now;
                _fileChangeHandlingInProgress = true;
                Debug.WriteLine($"File change detected, handling immediately: {e.FullPath}");
                Task.Run(async () =>
                {
                    await DebouncedHandleFileChange();
                });
            }
        }

        private static async Task DebouncedHandleFileChange() {
            try {
                await Task.Delay(_debounceInterval);

                DateTime lastChangeTime;
                lock (_fileChangeLock) {
                    lastChangeTime = _lastFileChangeTime;
                }

                while ((DateTime.Now - lastChangeTime) < _debounceInterval) {
                    await Task.Delay(_debounceInterval);
                    lock (_fileChangeLock) {
                        lastChangeTime = _lastFileChangeTime;
                    }
                }

                Debug.WriteLine("Debounce period completed, handling file change now");

                string jsonFilePath = GetDefaultConfigPath();
                if (File.Exists(jsonFilePath)) {
                    await Task.Delay(500);

                    HashSet<string> currentGroupNames = ExtractGroupNames(jsonFilePath);
                    if (!_previousGroupNames.SetEquals(currentGroupNames)) {
                        KillAllGroupWindows();
                        _previousGroupNames = currentGroupNames;
                    }
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error in DebouncedHandleFileChange: {ex.Message}");
            }
            finally {
                lock (_fileChangeLock) {
                    _fileChangeHandlingInProgress = false;
                }
            }
        }

        private static HashSet<string> ExtractGroupNames(string filePath) {
            HashSet<string> groupNames = new HashSet<string>();
            string fileContent = File.ReadAllText(filePath);
            MatchCollection matches = Regex.Matches(fileContent, @"""groupName"":\s*""([^""]+)""");

            foreach (Match match in matches) {
                groupNames.Add(match.Groups[1].Value);
            }

            return groupNames;
        }


        private static void KillAllGroupWindows() {
            try {
                Debug.WriteLine("Killing all group windows before reload");

                // First, kill groups from the currently loaded activeGroups
                if (activeGroups != null) {
                    foreach (var group in activeGroups.Values) {
                        IntPtr hWnd = FindWindow(null, group.groupName);
                        if (hWnd != IntPtr.Zero) {
                            Debug.WriteLine($"Found window for group '{group.groupName}', killing process");

                            // Get the process ID from the window handle
                            uint processId;
                            GetWindowThreadProcessId(hWnd, out processId);

                            if (processId > 0) {
                                try {
                                    // Open and kill the process
                                    Process process = Process.GetProcessById((int)processId);
                                    process.Kill();
                                    Debug.WriteLine($"Killed process for group '{group.groupName}' with ID: {processId}");
                                }
                                catch (Exception ex) {
                                    Debug.WriteLine($"Error killing process: {ex.Message}");
                                }
                            }
                        }
                    }
                }

                // Then reload the new groups after killing the old ones
                string jsonFilePath = GetDefaultConfigPath();
                if (File.Exists(jsonFilePath)) {
                    string jsonContent = File.ReadAllText(jsonFilePath);
                    activeGroups = JsonSerializer.Deserialize<Dictionary<string, GroupData>>(jsonContent);
                    Debug.WriteLine($"Reloaded groups from config file: {activeGroups?.Count ?? 0} groups");
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error in KillAllGroupWindows: {ex.Message}");
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        private const int WM_CLOSE = 0x0010;

        private static void InitializeSystemTray() {
            // Create a hidden window for message processing
            wndProcDelegate = new WndProcDelegate(WndProc);

            var wndClass = new WNDCLASSEX {
                cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX)),
                style = 0,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProcDelegate),
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = GetModuleHandle(null),
                hIcon = IntPtr.Zero,
                hCursor = LoadCursor(IntPtr.Zero, 32512), // IDC_ARROW
                hbrBackground = IntPtr.Zero,
                lpszMenuName = null,
                lpszClassName = "BackgroundClientTrayWndClass",
                hIconSm = IntPtr.Zero
            };

            RegisterClassEx(ref wndClass);

            windowHandle = CreateWindowEx(
                0,
                "BackgroundClientTrayWndClass",
                "BackgroundClient Tray Window",
                0,
                0, 0, 0, 0,
                IntPtr.Zero,
                IntPtr.Zero,
                GetModuleHandle(null),
                IntPtr.Zero);

            // Load custom icon from file
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppGroup.ico");
            if (File.Exists(iconPath)) {
                hIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
                if (hIcon == IntPtr.Zero) {
                    // Fallback to system icon if loading fails
                    hIcon = LoadImage(IntPtr.Zero, "#32516", IMAGE_ICON, 16, 16, 0); // IDI_APPLICATION
                }
            }
            else {
                // Fallback to system icon if file not found
                hIcon = LoadImage(IntPtr.Zero, "#32516", IMAGE_ICON, 16, 16, 0); // IDI_APPLICATION
                Debug.WriteLine($"Icon file not found at: {iconPath}, using system icon");
            }

            // Create the tray icon
            var notifyIconData = new NOTIFYICONDATA {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = windowHandle,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = hIcon,
                szTip = "App Group"
            };

            Shell_NotifyIcon(NIM_ADD, ref notifyIconData);

            // Create popup menu
            hMenu = CreatePopupMenu();
            AppendMenu(hMenu, 0, ID_SHOW, "Show");
            AppendMenu(hMenu, 0, ID_EXIT, "Exit");
        }

        private static void RunMessageLoop() {
            MSG msg;
            while (GetMessage(out msg, IntPtr.Zero, 0, 0)) {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }

        private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam) {
            if (msg == WM_TRAYICON && wParam.ToInt32() == 1) {
                if (lParam.ToInt32() == WM_LBUTTONDBLCLK) {
                    // Double click - show AppGroup
                    ShowAppGroup();
                    return IntPtr.Zero;
                }
                else if (lParam.ToInt32() == WM_RBUTTONUP) {
                    // Right click - show context menu
                    ShowContextMenu();
                    return IntPtr.Zero;
                }
            }

            // Handle menu commands
            if (msg == WM_COMMAND) {
                int menuId = wParam.ToInt32() & 0xFFFF; // Extract the lower 16 bits which contain the menu ID
                Debug.WriteLine($"Received WM_COMMAND with ID: {menuId}");

                if (menuId == ID_SHOW) {
                    ShowAppGroup();
                    return IntPtr.Zero;
                }
                else if (menuId == ID_EXIT) {
                    KillAppGroup();
                    return IntPtr.Zero;
                }
            }

            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private static void ShowContextMenu() {
            POINT pt;
            GetCursorPos(out pt);

            // Need to bring the message window to the foreground otherwise the menu won't disappear properly
            SetForegroundWindow(windowHandle);

            // Use TrackPopupMenuEx instead of TrackPopupMenu for better handling
            TrackPopupMenuEx(
                hMenu,
                TPM_RIGHTBUTTON,
                pt.X,
                pt.Y,
                windowHandle,
                IntPtr.Zero);

            // Send a dummy message to dismiss the menu when clicking elsewhere
            PostMessage(windowHandle, 0, IntPtr.Zero, IntPtr.Zero);
        }

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        // This is a critical method - improved to ensure we don't launch multiple instances
        private static void ShowAppGroup() {
            try {
                // First check if AppGroup is already running by window title
                IntPtr appGroupWindow = FindWindow(null, "AppGroup");

                if (appGroupWindow != IntPtr.Zero) {
                    // If window exists, make sure it's visible and bring it to front
                    Debug.WriteLine("AppGroup.exe window found, bringing to front");
                    ShowWindow(appGroupWindow, SW_RESTORE);
                    SetForegroundWindow(appGroupWindow);
                    return; // Exit early - no need to launch a new instance
                }

                // Next check if the process is running even if window is not found
                Process[] existingProcesses = Process.GetProcessesByName("AppGroup");
                if (existingProcesses.Length > 0) {
                    Debug.WriteLine("AppGroup.exe process found, attempting to show window");
                    foreach (var process in existingProcesses) {
                        // Try to bring its main window to front if it has one
                        if (process.MainWindowHandle != IntPtr.Zero) {
                            ShowWindow(process.MainWindowHandle, SW_RESTORE);
                            SetForegroundWindow(process.MainWindowHandle);
                            return; // Exit if we successfully showed a window
                        }
                    }

                    // If we got here, there's a process but no window - kill and restart
                    foreach (var process in existingProcesses) {
                        try {
                            process.Kill();
                            Debug.WriteLine($"Killed existing AppGroup process with ID: {process.Id}");
                        }
                        catch (Exception ex) {
                            Debug.WriteLine($"Failed to kill process: {ex.Message}");
                        }
                    }
                }

                // AppGroup is not running or we killed stuck processes, start it normally
                string appGroupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppGroup.exe");
                if (File.Exists(appGroupPath)) {
                    ProcessStartInfo startInfo = new ProcessStartInfo(appGroupPath);
                    startInfo.WindowStyle = ProcessWindowStyle.Normal; // Make sure it's visible
                    Process.Start(startInfo);
                    Debug.WriteLine("AppGroup.exe started");
                }
                else {
                    Debug.WriteLine($"AppGroup.exe not found at: {appGroupPath}");
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error showing AppGroup: {ex.Message}");
            }
        }

        private static void KillAppGroup() {
            try {
                // Find and kill all AppGroup.exe processes
                foreach (var process in Process.GetProcessesByName("AppGroup")) {
                    try {
                        process.Kill();
                        Debug.WriteLine($"Killed AppGroup.exe process with ID: {process.Id}");
                    }
                    catch (Exception ex) {
                        Debug.WriteLine($"Failed to kill AppGroup.exe process: {ex.Message}");
                    }
                }

                // Exit the background client as well
                ExitApplication();
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error killing AppGroup: {ex.Message}");
            }
        }

        private static void ExitApplication() {
            // Remove tray icon
            var notifyIconData = new NOTIFYICONDATA {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = windowHandle,
                uID = 1
            };

            Shell_NotifyIcon(NIM_DELETE, ref notifyIconData);

            // Cleanup resources
            DestroyMenu(hMenu);
            DestroyWindow(windowHandle);

            // Cancel monitoring tasks
            cancellationTokenSource?.Cancel();

            // Exit the application
            Environment.Exit(0);
        }

        delegate bool ConsoleCtrlHandlerDelegate(int eventType);

        [DllImport("kernel32.dll")]
        static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandlerDelegate handler, bool add);

        static bool ConsoleCtrlHandler(int eventType) {
            // Cancel monitoring tasks
            cancellationTokenSource?.Cancel();

            // Remove tray icon
            var notifyIconData = new NOTIFYICONDATA {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = windowHandle,
                uID = 1
            };

            Shell_NotifyIcon(NIM_DELETE, ref notifyIconData);

            return false;
        }

        private static void PreloadPopupWindows() {
            try {
                // Load and parse the JSON file
                string jsonFilePath = GetDefaultConfigPath();
                if (File.Exists(jsonFilePath)) {
                    string jsonContent = File.ReadAllText(jsonFilePath);
                    activeGroups = JsonSerializer.Deserialize<Dictionary<string, GroupData>>(jsonContent);

                    if (activeGroups != null) {
                        var tasks = new List<Task>();
                        foreach (var group in activeGroups.Values) {
                            // Check if this group is already running before launching
                            IntPtr hWnd = FindWindow(null, group.groupName);
                            if (hWnd != IntPtr.Zero) {
                                Debug.WriteLine($"Group '{group.groupName}' already running, skipping preload.");
                                continue;
                            }

                            tasks.Add(Task.Run(() => LaunchAppGroupInSeparateProcess(group.groupName)));
                        }

                        // Wait for all tasks to complete
                        Task.WhenAll(tasks).Wait();
                    }
                }
                else {
                    Debug.WriteLine($"JSON file not found at path: {jsonFilePath}");
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Exception in PreloadPopupWindows: {ex.Message}");
            }
        }

        private static async Task MonitorGroupWindows(CancellationToken cancellationToken) {
            // Wait a bit for initial launching to complete
            await Task.Delay(5000, cancellationToken);

            while (!cancellationToken.IsCancellationRequested) {
                try {
                    if (activeGroups != null) {
                        var tasks = new List<Task>();
                        foreach (var group in activeGroups.Values) {
                            IntPtr hWnd = FindWindow(null, group.groupName);
                            if (hWnd == IntPtr.Zero) {
                                Debug.WriteLine($"Group '{group.groupName}' not running, relaunching...");
                                tasks.Add(Task.Run(() => LaunchAppGroupInSeparateProcess(group.groupName)));
                            }
                        }

                        await Task.WhenAll(tasks);
                    }

                    await Task.Delay(2000, cancellationToken);
                }
                catch (OperationCanceledException) {
                    break;
                }
                catch (Exception ex) {
                    Debug.WriteLine($"Exception in MonitorGroupWindows: {ex.Message}");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        // Shell execute for launching processes
        [DllImport("shell32.dll")]
        static extern IntPtr ShellExecute(
            IntPtr hwnd,
            string lpOperation,
            string lpFile,
            string lpParameters,
            string lpDirectory,
            int nShowCmd);

        private static void LaunchAppGroupInSeparateProcess(string groupName) {
            try {
              

                string executableDir = AppDomain.CurrentDomain.BaseDirectory;
                string shortcutPath = Path.Combine(executableDir, "Groups", groupName, $"{groupName}.lnk");

                // Check if the shortcut exists
                if (File.Exists(shortcutPath)) {
                    ShellExecute(
                        IntPtr.Zero,
                        "open",
                        shortcutPath,
                        " --silent",
                        null,
                        SW_HIDE);

                    Debug.WriteLine($"Launched shortcut for group: {groupName} with --silent");
                }
                else {
                    Debug.WriteLine($"Shortcut not found at: {shortcutPath}");
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error launching shortcut for group {groupName}: {ex.Message}");
            }
        }

        private static string GetDefaultConfigPath() {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appGroupPath = Path.Combine(appDataPath, "AppGroup");
            string configFilePath = Path.Combine(appGroupPath, "appgroups.json");

            if (!Directory.Exists(appGroupPath)) {
                Directory.CreateDirectory(appGroupPath);
            }

            if (!File.Exists(configFilePath)) {
                string emptyJson = "{}";
                File.WriteAllText(configFilePath, emptyJson);
            }

            return configFilePath;
        }
    }

    public class GroupData {
        public string groupName { get; set; }
    }
}