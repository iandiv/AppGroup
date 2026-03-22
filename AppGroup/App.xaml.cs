

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.UI.StartScreen;
using Windows.UI.WindowManagement;
using WinRT.Interop;
using WinUIEx;

namespace AppGroup {

    public partial class App : Application {
        private MainWindow? m_window;
        private PopupWindow? popupWindow;
        private EditGroupWindow? editWindow;

        private nint hWnd;
        private bool useFileMode = false;

        public App() {
            try {

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
        protected async override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args) {
            try {
                string[] cmdArgs = Environment.GetCommandLineArgs();
                bool isSilent = HasSilentFlag(cmdArgs);
                IntPtr existingPopupHWnd = NativeMethods.FindWindow(null, "Popup Window");

                if (isSilent) {
                    if (existingPopupHWnd != IntPtr.Zero) {
                        Environment.Exit(0);
                        return;
                    }
                    CreateAllWindows();
                    InitializeSystemTray();
                    return;
                }

                if (cmdArgs.Length > 1) {
                    //await InitializeJumpListAsync();
                }

                CreateAllWindows();
                InitializeSystemTray();

                if (cmdArgs.Length > 1) {
                    string command = cmdArgs[1];

                    if (command == "EditGroupWindow") {
                        ShowEditWindow();
                        HideMainWindow();
                        HidePopupWindow();
                    }
                    else if (command != "LaunchAll") {
                        HideMainWindow();
                        HideEditWindow();

                        // Wait for window to be fully initialized before showing
                        await Task.Delay(300);

                        // Send group name first, then show
                        IntPtr popupHWnd = popupWindow?.GetWindowHandle() ?? IntPtr.Zero;
                        if (popupHWnd != IntPtr.Zero) {
                            NativeMethods.SendString(popupHWnd, command);
                            NativeMethods.ForceForegroundWindow(popupHWnd);
                        }
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

        //protected async override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args) {
        //    try {
        //        string[] cmdArgs = Environment.GetCommandLineArgs();
        //        bool isSilent = HasSilentFlag(cmdArgs);
        //        IntPtr existingPopupHWnd = NativeMethods.FindWindow(null, "Popup Window");

        //        // Handle --silent flag (special case)
        //        if (isSilent) {
        //            if (existingPopupHWnd != IntPtr.Zero) {
        //                Environment.Exit(0);
        //                return;
        //            }
        //            CreateAllWindows();
        //            //await InitializeJumpListAsync();
        //            InitializeSystemTray();
        //            return;
        //        }

        //        // ALWAYS update jump list when we have arguments
        //        if (cmdArgs.Length > 1) {
        //            //await InitializeJumpListAsync();
        //        }

        //        // Create all windows for first launch
        //        CreateAllWindows();

        //        // Initialize system tray after windows are created
        //        InitializeSystemTray();

        //        // Show the appropriate window based on arguments
        //        if (cmdArgs.Length > 1) {
        //            string command = cmdArgs[1];

        //            if (command == "EditGroupWindow") {
        //                ShowEditWindow();
        //                HideMainWindow();
        //                HidePopupWindow();
        //            }
        //            else if (command != "LaunchAll") {
        //                // Show PopupWindow with group name
        //                ShowPopupWindow();
        //                //HidePopupWindow();
        //                HideMainWindow();
        //                HideEditWindow();


        //            }
        //        }
        //        else {
        //            HidePopupWindow();
        //            HideEditWindow();
        //            ShowMainWindow();
        //        }
        //    }
        //    catch (Exception ex) {
        //        System.Diagnostics.Debug.WriteLine($"OnLaunched failed: {ex.Message}");
        //        Environment.Exit(1);
        //    }
        //}


        private void BringWindowToFront(IntPtr hWnd) {
            if (useFileMode) {
                try {
                    if (hWnd != IntPtr.Zero) {



                        NativeMethods.PositionWindowOffScreen(hWnd);
                        NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
                        NativeMethods.ForceForegroundWindow(hWnd);

                        Task.Delay(5).Wait();


                        NativeMethods.PositionWindowAboveTaskbar(hWnd);

                    }
                }
                catch (Exception ex) {
                    System.Diagnostics.Debug.WriteLine($"Failed to bring window to front: {ex.Message}");
                }
            }
            else { 
                try {
                    if (hWnd != IntPtr.Zero) {
                        // FIRST: Position and show the window immediately
                        NativeMethods.PositionWindowOffScreen(hWnd);

                        // THEN: Send the message to update content (async, non-blocking)
                        string[] cmdArgs = Environment.GetCommandLineArgs();
                        if (cmdArgs.Length > 1) {
                            string command = cmdArgs[1];
                            NativeMethods.SendString(hWnd, command);
                        }
                        NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);

                        NativeMethods.ForceForegroundWindow(hWnd);

                        NativeMethods.PositionWindowAboveTaskbar(hWnd);
                    }
                }
                catch (Exception ex) {
                    System.Diagnostics.Debug.WriteLine($"Failed to bring window to front: {ex.Message}");
                }
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
                int screenHeight = (int)(DisplayArea.Primary.WorkArea.Height) * 2;
                int screenWidth = (int)(DisplayArea.Primary.WorkArea.Width) * 2;

                popupWindow = new PopupWindow("Popup Window");
                popupWindow.AppWindow.Resize(new SizeInt32(screenWidth, screenHeight));

                popupWindow.InitializeComponent();

                NativeMethods.PositionWindowOffScreen(popupWindow.GetWindowHandle());



                //NativeMethods.ShowWindow(popupWindow.GetWindowHandle(), NativeMethods.SW_HIDE);

                IntPtr editHWnd = WindowNative.GetWindowHandle(editWindow);
                if (editHWnd != IntPtr.Zero) {
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



                    IntPtr popupHWnd = NativeMethods.FindWindow(null, "Popup Window");

                    Task.Delay(200).Wait();
                    BringWindowToFront(popupWindow.GetWindowHandle());


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
                IntPtr hwnd = NativeMethods.FindWindow(null, "App Group");
                if (hwnd != IntPtr.Zero) {
                    NativeMethods.SendString(hwnd, "__SHOW_MAIN__");
                }
               
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

     
    }
}