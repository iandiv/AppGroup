using AppGroupBackground;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BackgroundClient {
    internal class Program {
        // Use NativeMethods class for P/Invoke definitions
        private static Dictionary<string, GroupData> activeGroups;
        private static CancellationTokenSource cancellationTokenSource;

        // Constants for the system tray
        private static IntPtr windowHandle;
        private static IntPtr hIcon;
        private static IntPtr hMenu;

        // Add this to hide the console window
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
                IntPtr consoleWindow = NativeMethods.GetConsoleWindow();
                if (consoleWindow != IntPtr.Zero) {
                    NativeMethods.ShowWindow(consoleWindow, NativeMethods.SW_HIDE);
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

        #region System Tray Methods
        private static void InitializeSystemTray() {
            // Create a hidden window for message processing
            NativeMethods.WndProcDelegate wndProcDelegate = new NativeMethods.WndProcDelegate(WndProc);

            var wndClass = new NativeMethods.WNDCLASSEX {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.WNDCLASSEX)),
                style = 0,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProcDelegate),
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = NativeMethods.GetModuleHandle(null),
                hIcon = IntPtr.Zero,
                hCursor = NativeMethods.LoadCursor(IntPtr.Zero, 32512), // IDC_ARROW
                hbrBackground = IntPtr.Zero,
                lpszMenuName = null,
                lpszClassName = "BackgroundClientTrayWndClass",
                hIconSm = IntPtr.Zero
            };

            NativeMethods.RegisterClassEx(ref wndClass);

            windowHandle = NativeMethods.CreateWindowEx(
                0,
                "BackgroundClientTrayWndClass",
                "BackgroundClient Tray Window",
                0,
                0, 0, 0, 0,
                IntPtr.Zero,
                IntPtr.Zero,
                NativeMethods.GetModuleHandle(null),
                IntPtr.Zero);

            // Load custom icon from file
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppGroup.ico");
            if (File.Exists(iconPath)) {
                hIcon = NativeMethods.LoadImage(IntPtr.Zero, iconPath, NativeMethods.IMAGE_ICON, 16, 16, NativeMethods.LR_LOADFROMFILE);
                if (hIcon == IntPtr.Zero) {
                    // Fallback to system icon if loading fails
                    hIcon = NativeMethods.LoadImage(IntPtr.Zero, "#32516", NativeMethods.IMAGE_ICON, 16, 16, 0); // IDI_APPLICATION
                }
            }
            else {
                // Fallback to system icon if file not found
                hIcon = NativeMethods.LoadImage(IntPtr.Zero, "#32516", NativeMethods.IMAGE_ICON, 16, 16, 0); // IDI_APPLICATION
                Debug.WriteLine($"Icon file not found at: {iconPath}, using system icon");
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

            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref notifyIconData);

            // Create popup menu
            hMenu = NativeMethods.CreatePopupMenu();
            NativeMethods.AppendMenu(hMenu, 0, NativeMethods.ID_SHOW, "Show");
            NativeMethods.AppendMenu(hMenu, 0, NativeMethods.ID_EXIT, "Exit");
        }

        private static void RunMessageLoop() {
            NativeMethods.MSG msg;
            while (NativeMethods.GetMessage(out msg, IntPtr.Zero, 0, 0)) {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }

        private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam) {
            if (msg == NativeMethods.WM_TRAYICON && wParam.ToInt32() == 1) {
                if (lParam.ToInt32() == NativeMethods.WM_LBUTTONDBLCLK) {
                    // Double click - show AppGroup
                    ShowAppGroup();
                    return IntPtr.Zero;
                }
                else if (lParam.ToInt32() == NativeMethods.WM_RBUTTONUP) {
                    // Right click - show context menu
                    ShowContextMenu();
                    return IntPtr.Zero;
                }
            }

            // Handle menu commands
            if (msg == NativeMethods.WM_COMMAND) {
                int menuId = wParam.ToInt32() & 0xFFFF; // Extract the lower 16 bits which contain the menu ID
                Debug.WriteLine($"Received WM_COMMAND with ID: {menuId}");

                if (menuId == NativeMethods.ID_SHOW) {
                    ShowAppGroup();
                    return IntPtr.Zero;
                }
                else if (menuId == NativeMethods.ID_EXIT) {
                    KillAppGroup();
                    return IntPtr.Zero;
                }
            }

            return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private static void ShowContextMenu() {
            NativeMethods.POINT pt;
            NativeMethods.GetCursorPos(out pt);

            // Need to bring the message window to the foreground otherwise the menu won't disappear properly
            NativeMethods.SetForegroundWindow(windowHandle);

            // Use TrackPopupMenuEx instead of TrackPopupMenu for better handling
            NativeMethods.TrackPopupMenuEx(
                hMenu,
                NativeMethods.TPM_RIGHTBUTTON,
                pt.X,
                pt.Y,
                windowHandle,
                IntPtr.Zero);

            // Send a dummy message to dismiss the menu when clicking elsewhere
            NativeMethods.PostMessage(windowHandle, 0, IntPtr.Zero, IntPtr.Zero);
        }
        #endregion

        #region File Watcher Methods
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
                        Task.Run(async () => {
                            await DebouncedHandleFileChange();
                        });
                    }
                    return;
                }

                _lastFileChangeTime = now;
                _fileChangeHandlingInProgress = true;
                Debug.WriteLine($"File change detected, handling immediately: {e.FullPath}");
                Task.Run(async () => {
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
        #endregion

        #region Group Window Methods
        private static void KillAllGroupWindows() {
            try {
                Debug.WriteLine("Killing all group windows before reload");

                // First, kill groups from the currently loaded activeGroups
                if (activeGroups != null) {
                    foreach (var group in activeGroups.Values) {
                        IntPtr hWnd = NativeMethods.FindWindow(null, group.groupName);
                        if (hWnd != IntPtr.Zero) {
                            Debug.WriteLine($"Found window for group '{group.groupName}', killing process");

                            // Get the process ID from the window handle
                            uint processId;
                            NativeMethods.GetWindowThreadProcessId(hWnd, out processId);

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
                            IntPtr hWnd = NativeMethods.FindWindow(null, group.groupName);
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

            // Dictionary to track process IDs for each group
            Dictionary<string, int> groupProcessIds = new Dictionary<string, int>();
            // Dictionary to track launch attempts to prevent continuous launch loops
            Dictionary<string, DateTime> lastLaunchAttempts = new Dictionary<string, DateTime>();
            // Minimum time between launch attempts (15 seconds)
            TimeSpan minTimeBetweenLaunches = TimeSpan.FromSeconds(15);

            while (!cancellationToken.IsCancellationRequested) {
                try {
                    if (activeGroups != null) {
                        var tasks = new List<Task>();
                        foreach (var group in activeGroups.Values) {
                            bool isRunning = false;

                            // First check: Look for window by exact name
                            IntPtr hWnd = NativeMethods.FindWindow(null, group.groupName);
                            if (hWnd != IntPtr.Zero) {
                                isRunning = true;
                            }
                            // Second check: Look for windows that contain the group name
                            else if (IsWindowWithPartialTitleRunning(group.groupName)) {
                                isRunning = true;
                            }
                            // Third check: Check if we have a process ID and if it's still running
                            else if (groupProcessIds.TryGetValue(group.groupName, out int processId)) {
                                try {
                                    var process = Process.GetProcessById(processId);
                                    if (!process.HasExited) {
                                        isRunning = true;
                                    }
                                }
                                catch (ArgumentException) {
                                    // Process no longer exists
                                    groupProcessIds.Remove(group.groupName);
                                }
                            }

                            if (!isRunning) {
                                // Check if we've attempted to launch recently to avoid rapid relaunching
                                bool canLaunch = true;
                                if (lastLaunchAttempts.TryGetValue(group.groupName, out DateTime lastLaunch)) {
                                    if (DateTime.Now - lastLaunch < minTimeBetweenLaunches) {
                                        canLaunch = false;
                                        Debug.WriteLine($"Skipping launch for '{group.groupName}' - too soon since last attempt");
                                    }
                                }

                                if (canLaunch) {
                                    Debug.WriteLine($"Group '{group.groupName}' not running, relaunching...");
                                    lastLaunchAttempts[group.groupName] = DateTime.Now;

                                    tasks.Add(Task.Run(() => {
                                        int? newProcessId = LaunchAppGroupInSeparateProcess(group.groupName);
                                        if (newProcessId.HasValue) {
                                            groupProcessIds[group.groupName] = newProcessId.Value;
                                        }
                                    }));
                                }
                            }
                        }
                        await Task.WhenAll(tasks);
                    }
                    await Task.Delay(3000, cancellationToken);
                }
                catch (OperationCanceledException) {
                    break;
                }
                catch (Exception ex) {
                    Debug.WriteLine($"Exception in MonitorGroupWindows: {ex.Message}");
                    await Task.Delay(2000, cancellationToken);
                }
            }
        }

        // Helper method to check if any window contains the group name in its title
        private static bool IsWindowWithPartialTitleRunning(string partialTitle) {
            bool found = false;
            NativeMethods.EnumWindows((hWnd, lParam) => {
                int textLength = NativeMethods.GetWindowTextLength(hWnd);
                if (textLength > 0) {
                    StringBuilder sb = new StringBuilder(textLength + 1);
                    NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
                    string windowTitle = sb.ToString();
                    if (windowTitle.Contains(partialTitle)) {
                        found = true;
                        return false; // Stop enumeration
                    }
                }
                return true; // Continue enumeration
            }, IntPtr.Zero);
            return found;
        }

        // Modified to return the process ID if launched successfully
        private static int? LaunchAppGroupInSeparateProcess(string groupName) {
            try {
                string executableDir = AppDomain.CurrentDomain.BaseDirectory;
                string shortcutPath = Path.Combine(executableDir, "Groups", groupName, $"{groupName}.lnk");

                // Check if the shortcut exists
                if (File.Exists(shortcutPath)) {
                    // Use ProcessStartInfo with UseShellExecute=true to handle .lnk files
                    // This is safer than using ShellExecute directly
                    ProcessStartInfo psi = new ProcessStartInfo {
                        FileName = shortcutPath,
                        Arguments = "--silent",
                        UseShellExecute = true,  // Required for .lnk files
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    Process process = Process.Start(psi);
                    Debug.WriteLine($"Launched shortcut for group: {groupName} with --silent (PID: {process.Id})");
                    return process.Id;
                }
                return null;
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error launching application for group {groupName}: {ex.Message}");
                return null;
            }
        }
        #endregion

        #region AppGroup Methods
        private static void ShowAppGroup() {
            try {
                // First check if AppGroup is already running by window title
                IntPtr appGroupWindow = NativeMethods.FindWindow(null, "AppGroup");

                if (appGroupWindow != IntPtr.Zero) {
                    // If window exists, make sure it's visible and bring it to front
                    Debug.WriteLine("AppGroup.exe window found, bringing to front");
                    NativeMethods.ShowWindow(appGroupWindow, NativeMethods.SW_RESTORE);
                    NativeMethods.SetForegroundWindow(appGroupWindow);
                    return; // Exit early - no need to launch a new instance
                }

                // Next check if the process is running even if window is not found
                Process[] existingProcesses = Process.GetProcessesByName("AppGroup");
                if (existingProcesses.Length > 0) {
                    Debug.WriteLine("AppGroup.exe process found, attempting to show window");
                    foreach (var process in existingProcesses) {
                        // Try to bring its main window to front if it has one
                        if (process.MainWindowHandle != IntPtr.Zero) {
                            NativeMethods.ShowWindow(process.MainWindowHandle, NativeMethods.SW_RESTORE);
                            NativeMethods.SetForegroundWindow(process.MainWindowHandle);
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
            var notifyIconData = new NativeMethods.NOTIFYICONDATA {
                cbSize = Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA)),
                hWnd = windowHandle,
                uID = 1
            };

            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref notifyIconData);

            // Cleanup resources
            NativeMethods.DestroyMenu(hMenu);
            NativeMethods.DestroyWindow(windowHandle);

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
            var notifyIconData = new NativeMethods.NOTIFYICONDATA {
                cbSize = Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA)),
                hWnd = windowHandle,
                uID = 1
            };

            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref notifyIconData);

            return false;
        }
        #endregion

        #region Helper Methods
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
        #endregion
    }

    public class GroupData {
        public string groupName { get; set; }
    }
}
