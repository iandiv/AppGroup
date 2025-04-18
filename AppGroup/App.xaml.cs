using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using WinUIEx;

namespace AppGroup {

    public partial class App : Application {

        public App() {

            string[] cmdArgs = Environment.GetCommandLineArgs();
            _ = Task.Run(() => EnsureBackgroundClientRunning());
            if (cmdArgs.Length > 1) {
                string groupName = cmdArgs[1];

                if (groupName != "EditGroupWindow") {
                    // Check if window for this group already exists
                    IntPtr hWnd = NativeMethods.FindWindow(null, groupName);
                    if (hWnd != IntPtr.Zero) {
                       
                        NativeMethods.SetForegroundWindow(hWnd);
                        NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
                        NativeMethods.PositionWindowAboveTaskbar(hWnd);
                        Environment.Exit(0);
                    }
                    // Check if Group Name exist in JSON 
                    if (!GroupExistsInJson(groupName)) {
                        Environment.Exit(0);
                    }
                }
            }


            // Initialize components only if the application does not exit early
            this.InitializeComponent();
        }


        protected async override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args) {
            string[] cmdArgs = Environment.GetCommandLineArgs();

            if (cmdArgs.Length > 1) {
                string groupName = cmdArgs[1];
                bool isSilent = cmdArgs.Contains("--silent");

                if (groupName != "EditGroupWindow") {
                    // Create a new window if one doesn't exist
                    popupWindow = new PopupWindow(groupName);
                    IntPtr hWnd = WindowNative.GetWindowHandle(popupWindow);
                    popupWindow.InitializeComponent();
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                    NativeMethods.EmptyWorkingSet(Process.GetCurrentProcess().Handle);


                    if (!isSilent) {
                        NativeMethods.SetForegroundWindow(hWnd);
                        NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
                        NativeMethods.PositionWindowAboveTaskbar(hWnd);
                        popupWindow.Activate();
                    }
                }
                else {
                    EditGroupWindow editGroupWindow = new EditGroupWindow(JsonConfigHelper.GetNextGroupId());
                    editGroupWindow.Activate();
                }
            }
            else {
                m_window = new MainWindow();
                m_window.Activate();
            }

       
        }




        private void EnsureBackgroundClientRunning() {
            try {
                // Check if the mutex exists (indicating the background client is running)
                bool mutexExists = false;
                using (Mutex mutex = new Mutex(false, "AppGroupBackgroundClientMutex", out mutexExists)) {
                    if (mutexExists) {
                        string backgroundClientPath = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "AppGroupBackground.exe");
                        //string backgroundClientPath = "C:\\Users\\Ian Divinagracia\\source\\repos\\AppGroup\\AppGroupBackground\\bin\\Debug\\net8.0\\AppGroupBackground.exe";
                        if (File.Exists(backgroundClientPath)) {
                            // Start the background client process
                            ProcessStartInfo startInfo = new ProcessStartInfo {
                                FileName = backgroundClientPath,
                                UseShellExecute = true,
                                CreateNoWindow = true,
                                WindowStyle = ProcessWindowStyle.Hidden
                            };

                            Process.Start(startInfo);
                            Debug.WriteLine("Started BackgroundClient process");
                        }
                        else {
                            Debug.WriteLine($"BackgroundClient executable not found at: {backgroundClientPath}");
                        }
                    }
                    else {
                        Debug.WriteLine("BackgroundClient is already running");
                    }
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error checking/starting BackgroundClient: {ex.Message}");
            }
        }
        private bool GroupExistsInJson(string groupName) {
            string jsonPath = JsonConfigHelper.GetDefaultConfigPath(); 
            if (File.Exists(jsonPath)) {
                string jsonContent = File.ReadAllText(jsonPath);
                using (JsonDocument document = JsonDocument.Parse(jsonContent)) {
                    JsonElement root = document.RootElement;

                    foreach (JsonProperty property in root.EnumerateObject()) {
                        if (property.Value.TryGetProperty("groupName", out JsonElement groupNameElement) &&
                            groupNameElement.GetString() == groupName) {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        private Window? m_window;
        private PopupWindow? popupWindow;

      
    }
}
