

using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.UI.StartScreen;
using WinRT.Interop;
using WinUIEx;

namespace AppGroup {

    public partial class App : Application {
        private MainWindow? m_window;
        private PopupWindow? popupWindow;
        private EditGroupWindow? editWindow;

        private nint hWnd;

        public App() {
            try {
                string[] cmdArgs = Environment.GetCommandLineArgs();
                bool isSilent = HasSilentFlag(cmdArgs);

                // Kill if --silent is used with a group name (invalid combination)
                if (isSilent && cmdArgs.Length > 2) {
                    Environment.Exit(0);
                    return;
                }

                // Check if running without arguments and another instance is already running
                if (cmdArgs.Length <= 1 && !isSilent) {
                    // No arguments provided - check for existing main window instance
                    IntPtr existingMainHWnd = NativeMethods.FindWindow(null, "App Group");
                    if (existingMainHWnd != IntPtr.Zero) {
                        // Bring existing instance to foreground and exit
                        NativeMethods.SetForegroundWindow(existingMainHWnd);
                        NativeMethods.ShowWindow(existingMainHWnd, NativeMethods.SW_RESTORE);
                        Environment.Exit(0);
                        return;
                    }
                }

                if (cmdArgs.Length > 1 && !isSilent) {
                    string groupName = cmdArgs[1];

                    if (groupName != "EditGroupWindow" && groupName != "LaunchAll") {
                        // Quick JSON check
                        if (!JsonConfigHelper.GroupExistsInJson(groupName)) {
                            Environment.Exit(0);
                        }
                    }
                }

                // Find existing windows - only check if we have arguments (not first launch)
                if (!isSilent && cmdArgs.Length > 1) {
                    IntPtr existingPopupHWnd = NativeMethods.FindWindow(null, "Popup Window");
                    IntPtr existingEditHWnd = NativeMethods.FindWindow(null, "Edit Group");
                    IntPtr existingMainHWnd = NativeMethods.FindWindow(null, "App Group");

                    // Handle existing windows in constructor for faster response
                    string command = cmdArgs[1];

                    if (command == "EditGroupWindow") {
                        // AppGroup.exe EditGroupWindow --id
                        int groupId = ExtractIdFromCommandLine(cmdArgs);
                        SaveGroupIdToFile(groupId.ToString());

                        // Initialize jump list BEFORE handling existing window

                        if (existingEditHWnd != IntPtr.Zero) {

                            EditGroupHelper editGroup = new EditGroupHelper("Edit Group", groupId);
                            editGroup.Activate();
                        InitializeJumpListSync();

                            Environment.Exit(0);
                            return;
                        }
                        else if (existingMainHWnd != IntPtr.Zero || existingPopupHWnd != IntPtr.Zero) {
                            Environment.Exit(0);
                            return;
                        }
                    }
                    else if (command == "LaunchAll") {
                    

                        string targetGroupName = ExtractGroupNameFromCommandLine(cmdArgs);
                        Task.Run(async () => {
                            await JsonConfigHelper.LaunchAll(targetGroupName);
                            InitializeJumpListSync();

                            Environment.Exit(0);
                        });
                        Environment.Exit(0);
                        return;
                    }
                    else {
                        // AppGroup.exe "GroupName" - like "CH"
                        SaveGroupNameToFile(command);

                        // Also save the group ID for this group name
                        try {
                            int groupId = JsonConfigHelper.FindKeyByGroupName(command);
                            SaveGroupIdToFile(groupId.ToString());
                        }
                        catch (Exception ex) {
                            System.Diagnostics.Debug.WriteLine($"Failed to find group ID for '{command}': {ex.Message}");
                        }


                        if (existingPopupHWnd != IntPtr.Zero) {

                            
                           
                            BringWindowToFront(existingPopupHWnd);
                            InitializeJumpListSync();

                            Environment.Exit(0);
                            return;
                        }
                        else if (existingMainHWnd != IntPtr.Zero || existingEditHWnd != IntPtr.Zero) {
                           
                            Environment.Exit(0);
                            return;
                        }


                    }
                }

                // Initialize settings early - this will apply the default startup setting if needed
                _ = InitializeSettingsAsync();

                this.InitializeComponent();
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"App initialization failed: {ex.Message}");
                Environment.Exit(1);
            }
        }


        private async Task InitializeSettingsAsync() {
            try {
                // Load settings - this will automatically apply the startup setting if needed
                await SettingsHelper.LoadSettingsAsync();
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Settings initialization failed: {ex.Message}");
            }
        }

        // Synchronous version for constructor
        private void InitializeJumpListSync() {
            try {
                Task.Run(async () => await InitializeJumpListAsync()).Wait();
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Sync jump list initialization failed: {ex.Message}");
            }
        }



        private async Task InitializeJumpListAsync() {
            try {
                string[] cmdArgs = Environment.GetCommandLineArgs();
                JumpList jumpList = await JumpList.LoadCurrentAsync();

                System.Diagnostics.Debug.WriteLine($"Jump list initialization started with args: {string.Join(", ", cmdArgs)}");

                // Only modify jump list when there ARE arguments
                if (cmdArgs.Length > 1) {
                    string command = cmdArgs[1];

                    System.Diagnostics.Debug.WriteLine($"Processing command: '{command}'");

                    jumpList.Items.Clear();

                    if (command == "EditGroupWindow") {
                        // For EditGroupWindow command
                        System.Diagnostics.Debug.WriteLine("Creating jump list for EditGroupWindow");
                        var jumpListItem = CreateJumpListItemTask();
                        var launchAllItem = CreateLaunchAllJumpListItem();

                        jumpList.Items.Add(jumpListItem);
                        jumpList.Items.Add(launchAllItem);
                    }
                    else if (command == "LaunchAll") {
                        // For LaunchAll command
                        System.Diagnostics.Debug.WriteLine("Creating jump list for LaunchAll");
                        // Don't create jump list items for LaunchAll since it's a one-time action
                        // But we could add items based on the target group if needed
                    }
                    else {
                        // This is a group name like "CH"
                        System.Diagnostics.Debug.WriteLine($"Creating jump list for group name: '{command}'");

                        // Verify the group exists before creating jump list items
                        if (JsonConfigHelper.GroupExistsInJson(command)) {
                            var jumpListItem = CreateJumpListItemTask();
                            var launchAllItem = CreateLaunchAllJumpListItem();

                            jumpList.Items.Add(jumpListItem);
                            jumpList.Items.Add(launchAllItem);

                            System.Diagnostics.Debug.WriteLine($"Jump list items created for group '{command}'");
                        }
                        else {
                            System.Diagnostics.Debug.WriteLine($"Group '{command}' does not exist in JSON");
                        }
                    }

                    await jumpList.SaveAsync();
                    System.Diagnostics.Debug.WriteLine("Jump list saved successfully");
                }
                else {
                    System.Diagnostics.Debug.WriteLine("No arguments provided, jump list not modified");
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Jump list initialization failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }



        private JumpListItem CreateJumpListItemTask() {
            try {
                string[] cmdArgs = Environment.GetCommandLineArgs();
                System.Diagnostics.Debug.WriteLine($"CreateJumpListItemTask called with args: {string.Join(", ", cmdArgs)}");

                if (cmdArgs.Length > 1) {
                    string command = cmdArgs[1];
                    System.Diagnostics.Debug.WriteLine($"Processing command: '{command}'");

                    if (command == "EditGroupWindow") {
                        int groupId = ExtractIdFromCommandLine(cmdArgs);
                        SaveGroupIdToFile(groupId.ToString());
                        var taskItem = JumpListItem.CreateWithArguments("EditGroupWindow --id=" + groupId, "Edit this Group");
                        System.Diagnostics.Debug.WriteLine($"Created EditGroupWindow jump list item with ID: {groupId}");
                        return taskItem;
                    }
                    else if (command != "LaunchAll") {
                        // This is a group name like "CH"
                        try {
                            int groupId = JsonConfigHelper.FindKeyByGroupName(command);
                            SaveGroupIdToFile(groupId.ToString());

                            var taskItem = JumpListItem.CreateWithArguments("EditGroupWindow --id=" + groupId, "Edit this Group");
                            System.Diagnostics.Debug.WriteLine($"Created jump list item for group '{command}' with ID: {groupId}");
                            return taskItem;
                        }
                        catch (Exception ex) {
                            System.Diagnostics.Debug.WriteLine($"Failed to find group ID for '{command}': {ex.Message}");
                        }
                    }
                }

                // Fallback
                System.Diagnostics.Debug.WriteLine("Using fallback jump list item");
                return JumpListItem.CreateWithArguments("EditGroupWindow --id=0", "Edit Group");
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to create edit jump list item: {ex.Message}");
                return JumpListItem.CreateWithArguments("EditGroupWindow --id=0", "Edit Group");
            }
        }

        private JumpListItem CreateLaunchAllJumpListItem() {
            try {
                string[] cmdArgs = Environment.GetCommandLineArgs();
                System.Diagnostics.Debug.WriteLine($"CreateLaunchAllJumpListItem called with args: {string.Join(", ", cmdArgs)}");

                if (cmdArgs.Length > 1) {
                    string command = cmdArgs[1];

                    if (command == "EditGroupWindow") {
                        int groupId = ExtractIdFromCommandLine(cmdArgs);
                        var taskItem = JumpListItem.CreateWithArguments($"LaunchAll --groupId={groupId}", "Launch All");
                        System.Diagnostics.Debug.WriteLine($"Created LaunchAll item for EditGroupWindow with ID: {groupId}");
                        return taskItem;
                    }
                    else if (command != "LaunchAll") {
                        // This is a group name like "CH"
                        string groupName = command;
                        var taskItem = JumpListItem.CreateWithArguments($"LaunchAll --groupName=\"{groupName}\"", "Launch All");
                        System.Diagnostics.Debug.WriteLine($"Created LaunchAll item for group: '{groupName}'");
                        return taskItem;
                    }
                }

                // Fallback
                System.Diagnostics.Debug.WriteLine("Using fallback LaunchAll item");
                return JumpListItem.CreateWithArguments("LaunchAll", "Launch All");
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to create launch all jump list item: {ex.Message}");
                return JumpListItem.CreateWithArguments("LaunchAll", "Launch All");
            }
        }

        protected async override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args) {
            try {
                string[] cmdArgs = Environment.GetCommandLineArgs();
                bool isSilent = HasSilentFlag(cmdArgs);

                // Handle --silent flag (special case)
                if (isSilent) {
                    IntPtr existingPopupHWnd = NativeMethods.FindWindow(null, "Popup Window");
                    if (existingPopupHWnd != IntPtr.Zero) {
                        Environment.Exit(0);
                        return;
                    }
                    CreateAllWindows();
                    await InitializeJumpListAsync();
                    InitializeSystemTray();
                    return;
                }

                // ALWAYS update jump list when we have arguments
                if (cmdArgs.Length > 1) {
                    await InitializeJumpListAsync();
                }

                // Create all windows for first launch
                CreateAllWindows();

                // Initialize system tray after windows are created
                InitializeSystemTray();

                // Show the appropriate window based on arguments
                if (cmdArgs.Length > 1) {
                    string command = cmdArgs[1];

                    if (command == "EditGroupWindow") {
                        ShowEditWindow();
                        HideMainWindow();
                        HidePopupWindow();
                    }
                    else if (command != "LaunchAll") {
                        // Show PopupWindow with group name
                        ShowPopupWindow();
                        HideMainWindow();
                        HideEditWindow();
                    }
                }
                else {
                    HidePopupWindow();
                    HideEditWindow();
                    ShowMainWindow();
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"OnLaunched failed: {ex.Message}");
                Environment.Exit(1);
            }
        }

        //private void BringWindowToFront(IntPtr hWnd) {
        //    if (hWnd == IntPtr.Zero) return;

        //    try {
        //        // Hide window instantly (no flicker)
        //        NativeMethods.ShowWindow(hWnd, NativeMethods.SW_HIDE);

        //        // Calculate target position while hidden
        //        var pos = CalculateTargetPosition(hWnd);

        //        // Move and show in ONE atomic operation
        //        NativeMethods.SetWindowPos(hWnd, NativeMethods.HWND_TOPMOST,
        //            pos.x, pos.y, 0, 0,
        //            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_NOACTIVATE);

        //        // Remove topmost flag (keeps it on top but not always-on-top)
        //        NativeMethods.SetWindowPos(hWnd, NativeMethods.HWND_NOTOPMOST,
        //            0, 0, 0, 0,
        //            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

        //        // Activate
        //        NativeMethods.ForceForegroundWindow(hWnd);
        //    }
        //    catch (Exception ex) {
        //        System.Diagnostics.Debug.WriteLine($"Failed to bring window to front: {ex.Message}");
        //    }
        //}

        //private (int x, int y) CalculateTargetPosition(IntPtr hWnd) {
        //    // Get window dimensions
        //    NativeMethods.RECT windowRect;
        //    if (!NativeMethods.GetWindowRect(hWnd, out windowRect)) {
        //        return (100, 100); // Fallback
        //    }
        //    int windowWidth = windowRect.right - windowRect.left;
        //    int windowHeight = windowRect.bottom - windowRect.top;

        //    // Get cursor position
        //    NativeMethods.POINT cursorPos;
        //    if (!NativeMethods.GetCursorPos(out cursorPos)) {
        //        return (100, 100); // Fallback
        //    }

        //    // Get monitor info
        //    IntPtr monitor = NativeMethods.MonitorFromPoint(cursorPos, NativeMethods.MONITOR_DEFAULTTONEAREST);
        //    NativeMethods.MONITORINFO monitorInfo = new NativeMethods.MONITORINFO();
        //    monitorInfo.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
        //    if (!NativeMethods.GetMonitorInfo(monitor, ref monitorInfo)) {
        //        return (100, 100); // Fallback
        //    }

        //    // Calculate position (simplified from your PositionWindowAboveTaskbar logic)
        //    float dpiScale = NativeMethods.GetDpiScaleForMonitor(monitor);
        //    int spacing = 6;
        //    bool isTaskbarAutoHide = NativeMethods.IsTaskbarAutoHide();

        //    if (isTaskbarAutoHide) {
        //        spacing += (int)(5 * dpiScale);
        //    }

        //    // Center horizontally on cursor
        //    int x = cursorPos.X - (windowWidth / 2);

        //    // Position above taskbar
        //    int y = monitorInfo.rcWork.bottom - windowHeight - spacing;

        //    // Clamp horizontally
        //    if (x < monitorInfo.rcWork.left)
        //        x = monitorInfo.rcWork.left;
        //    if (x + windowWidth > monitorInfo.rcWork.right)
        //        x = monitorInfo.rcWork.right - windowWidth;

        //    return (x, y);
        //}
        private void BringWindowToFront(IntPtr hWnd) {
            try {
                if (hWnd != IntPtr.Zero) {




                    NativeMethods.PositionWindowOffScreenBelow(hWnd);
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
                    //Task.Delay(50).Wait();

                    NativeMethods.ForceForegroundWindow(hWnd);
                    NativeMethods.PositionWindowAboveTaskbar(hWnd);


                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to bring window to front: {ex.Message}");
            }
        }

        private void BringEditWindowToFront(IntPtr hWnd) {
            try {
                if (hWnd != IntPtr.Zero) {
                    NativeMethods.SetForegroundWindow(hWnd);
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to bring window to front: {ex.Message}");
            }
        }

        private void CreateAllWindows() {
            try {
                editWindow = new EditGroupWindow(-1);
                editWindow.InitializeComponent();

                // Create MainWindow
                m_window = new MainWindow();
                m_window.InitializeComponent();

                // Create PopupWindow (hidden)
                popupWindow = new PopupWindow("Popup Window");
                popupWindow.InitializeComponent();
                IntPtr popupHWnd = WindowNative.GetWindowHandle(popupWindow);
                if (popupHWnd != IntPtr.Zero) {
                    NativeMethods.PositionWindowAboveTaskbar(popupHWnd);
                    NativeMethods.ShowWindow(popupHWnd, NativeMethods.SW_HIDE);
                }

                IntPtr editHWnd = WindowNative.GetWindowHandle(editWindow);
                if (editHWnd != IntPtr.Zero) {
                        NativeMethods.PositionWindowAboveTaskbar(popupHWnd);
                    NativeMethods.ShowWindow(editHWnd, NativeMethods.SW_HIDE);
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to create windows: {ex.Message}");
                throw;
            }
        }

        private void ShowMainWindow() {
            try {
                m_window?.Activate();
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to show main window: {ex.Message}");
            }
        }


       
        private void ShowPopupWindow() {
            try {
                if (popupWindow != null) {
                    IntPtr popupHWnd = WindowNative.GetWindowHandle(popupWindow);

                    if (popupHWnd != IntPtr.Zero) {
                        NativeMethods.PositionWindowAboveTaskbar(popupHWnd);
                        NativeMethods.ShowWindow(popupHWnd, NativeMethods.SW_SHOW);
                        NativeMethods.ForceForegroundWindow(popupHWnd);
                    }
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to show popup window: {ex.Message}");
            }
        }
       

        private void ShowEditWindow() {
            try {
                if (editWindow != null) {
                    IntPtr editHWnd = WindowNative.GetWindowHandle(editWindow);
                    if (editHWnd != IntPtr.Zero) {
                        NativeMethods.SetForegroundWindow(editHWnd);
                        NativeMethods.ShowWindow(editHWnd, NativeMethods.SW_RESTORE);
                        editWindow.Activate();
                    }
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to show edit window: {ex.Message}");
            }
        }

        private void HideMainWindow() {
            try {
                if (m_window != null) {
                    IntPtr mainHWnd = WindowNative.GetWindowHandle(m_window);
                    if (mainHWnd != IntPtr.Zero) {
                        NativeMethods.ShowWindow(mainHWnd, NativeMethods.SW_HIDE);
                    }
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to hide main window: {ex.Message}");
            }
        }

        private void HidePopupWindow() {
            try {
                if (popupWindow != null) {
                    IntPtr popupHWnd = WindowNative.GetWindowHandle(popupWindow);

                    if (popupHWnd != IntPtr.Zero) {
                        NativeMethods.ShowWindow(popupHWnd, NativeMethods.SW_HIDE);
                    }
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to hide popup window: {ex.Message}");
            }
        }

        private void HideEditWindow() {
            try {
                if (editWindow != null) {
                    IntPtr editHWnd = WindowNative.GetWindowHandle(editWindow);
                    if (editHWnd != IntPtr.Zero) {
                        NativeMethods.ShowWindow(editHWnd, NativeMethods.SW_HIDE);
                    }
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to hide edit window: {ex.Message}");
            }
        }

        private void InitializeSystemTray() {
            try {
                SystemTrayManager.Initialize(
                    showCallback: () => {
                        ShowAppGroup();
                    },
                    exitCallback: () => {
                        KillAppGroup();
                    }
                );
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize system tray: {ex.Message}");
            }
        }

        public void ShowSystemTray() {
            try {
                SystemTrayManager.ShowSystemTray();
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to show system tray: {ex.Message}");
            }
        }

        public void HideSystemTray() {
            try {
                SystemTrayManager.HideSystemTray();
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to hide system tray: {ex.Message}");
            }
        }

        private void ShowAppGroup() {
            try {
                IntPtr appGroupWindow = NativeMethods.FindWindow(null, "App Group");
                if (appGroupWindow != IntPtr.Zero) {

                    Debug.WriteLine("AppGroup.exe window found, bringing to front");
                    NativeMethods.ShowWindow(appGroupWindow, NativeMethods.SW_RESTORE);
                    NativeMethods.SetForegroundWindow(appGroupWindow);
                    return;
                }

                Process[] existingProcesses = Process.GetProcessesByName("App Group");
                if (existingProcesses.Length > 0) {
                    Debug.WriteLine("AppGroup.exe process found, attempting to show window");
                    foreach (var process in existingProcesses) {
                        if (process.MainWindowHandle != IntPtr.Zero) {
                            NativeMethods.ShowWindow(process.MainWindowHandle, NativeMethods.SW_RESTORE);
                            NativeMethods.SetForegroundWindow(process.MainWindowHandle);
                            return;
                        }
                    }

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

                if (m_window == null) {
                    m_window = new MainWindow();
                    m_window.InitializeComponent();
                }
                m_window.Activate();
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error showing AppGroup: {ex.Message}");
            }
        }

        private static void KillAppGroup() {
            try {
                var startInfo = new ProcessStartInfo {
                    FileName = "taskkill",
                    Arguments = "/f /t /im AppGroup.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(startInfo)) {
                    if (process != null) {
                        process.WaitForExit();

                        string output = process.StandardOutput.ReadToEnd();
                        string error = process.StandardError.ReadToEnd();

                        if (process.ExitCode == 0) {
                            Debug.WriteLine("Successfully killed all AppGroup.exe processes");
                            Debug.WriteLine(output);
                        }
                        else {
                            Debug.WriteLine($"taskkill exit code: {process.ExitCode}");
                            if (!string.IsNullOrEmpty(error)) {
                                Debug.WriteLine($"Error: {error}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error running taskkill: {ex.Message}");
            }
            finally {
                Application.Current?.Exit();
            }
        }

        private bool HasSilentFlag(string[] args) {
            try {
                foreach (string arg in args) {
                    if (arg.Equals("--silent", StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error checking silent flag: {ex.Message}");
                return false;
            }
        }

        private void SaveGroupNameToFile(string groupName) {
            try {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string filePath = Path.Combine(appDataPath, "AppGroup", "lastOpen");
                Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? "");
                File.WriteAllText(filePath, groupName);
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to save group name: {ex.Message}");
            }
        }

        private void SaveGroupIdToFile(string groupId) {
            try {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string filePath = Path.Combine(appDataPath, "AppGroup", "lastEdit");
                Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? "");
                File.WriteAllText(filePath, groupId);
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to save group ID: {ex.Message}");
            }
        }

        private string ExtractGroupNameFromCommandLine(string[] args) {
            try {
                foreach (string arg in args) {
                    if (arg.StartsWith("--groupName=")) {
                        return arg.Substring(12).Trim('"');
                    }
                    else if (arg.StartsWith("--groupId=")) {
                        if (int.TryParse(arg.Substring(10), out int groupId)) {
                            return JsonConfigHelper.FindGroupNameByKey(groupId);
                        }
                    }
                }
                return string.Empty;
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error extracting group name: {ex.Message}");
                return string.Empty;
            }
        }

        private int ExtractIdFromCommandLine(string[] args) {
            try {
                foreach (string arg in args) {
                    if (arg.StartsWith("--id=")) {
                        if (int.TryParse(arg.Substring(5), out int id)) {
                            return id;
                        }
                    }
                }
                return JsonConfigHelper.GetNextGroupId();
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error extracting ID: {ex.Message}");
                return JsonConfigHelper.GetNextGroupId();
            }
        }
    }
}