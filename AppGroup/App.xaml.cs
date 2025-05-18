using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Windows.UI.StartScreen;
using WinRT.Interop;
using WinUIEx;

namespace AppGroup {

    public partial class App : Application {

        public App() {

            string[] cmdArgs = Environment.GetCommandLineArgs();

            _ = Task.Run(() => EnsureBackgroundClientRunning());
            if (cmdArgs.Length > 1) {
                string groupName = cmdArgs[1];
               
                if (groupName != "EditGroupWindow" && groupName != "LaunchAll") {
                    // Check if window for this group already exists
                    IntPtr hWnd = NativeMethods.FindWindow(null, groupName);
                    if (hWnd != IntPtr.Zero) {
                       
                        NativeMethods.SetForegroundWindow(hWnd);
                        NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
                        NativeMethods.PositionWindowAboveTaskbar(hWnd);
                        Environment.Exit(0);
                    }
                    // Check if Group Name exist in JSON 
                    if (!JsonConfigHelper.GroupExistsInJson(groupName)) {
                        Environment.Exit(0);
                    }
                }
            }

            InitializeJumpListAsync();
            this.InitializeComponent();
        }

        // Method to create a Jump List Item
       


        private async Task InitializeJumpListAsync() {
            var jumpListItem = CreateJumpListItemTask();
            var launchAllItem = CreateLaunchAllJumpListItem();

            JumpList jumpList = await JumpList.LoadCurrentAsync();
            jumpList.Items.Clear();
            jumpList.Items.Add(jumpListItem);
            jumpList.Items.Add(launchAllItem);

            await jumpList.SaveAsync();
        }

        // Method to create a Jump List Item for launching all paths in a group
        private JumpListItem CreateLaunchAllJumpListItem() {
            string groupName = Environment.GetCommandLineArgs()[1];
            var taskItem = JumpListItem.CreateWithArguments($"LaunchAll --groupName=\"{groupName}\"", "Launch All");
            return taskItem;
        }
        private JumpListItem CreateJumpListItemTask() {
            int groupId = JsonConfigHelper.FindKeyByGroupName(Environment.GetCommandLineArgs()[1]);
            var taskItem = JumpListItem.CreateWithArguments("EditGroupWindow --id=" + groupId, "Edit this Group ");
            return taskItem;
        }
        protected async override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args) {
            try {
                string[] cmdArgs = Environment.GetCommandLineArgs();
                if (cmdArgs.Length > 1) {
                    string groupName = cmdArgs[1];
                    bool isSilent = cmdArgs.Contains("--silent");

                    if (groupName == "EditGroupWindow") {
                        int id = ExtractIdFromCommandLine(cmdArgs);
                      
                        EditGroupWindow editGroupWindow = new EditGroupWindow(id);
                        editGroupWindow.Activate();
                    }
                    else if (groupName == "LaunchAll") {
                        string targetGroupName = ExtractGroupNameFromCommandLine(cmdArgs);
                        await JsonConfigHelper.LaunchAll(targetGroupName);
                        Environment.Exit(0);
                    }
                    else {
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
                }
                else {
                    m_window = new MainWindow();
                    m_window.Activate();
                }
            }
            catch (Exception ex) {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
        private string ExtractGroupNameFromCommandLine(string[] args) {
            foreach (string arg in args) {
                if (arg.StartsWith("--groupName=")) {
                    return arg.Substring(12);
                }
            }
            return string.Empty;
        }
        private int ExtractIdFromCommandLine(string[] args) {
            foreach (string arg in args) {
                if (arg.StartsWith("--id=")) {
                    string idStr = arg.Substring(5);
                    if (int.TryParse(idStr, out int id)) {
                        return id;
                    }
                }
            }
            // Return default value if ID not found or invalid
            return JsonConfigHelper.GetNextGroupId();
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
       
        private Window? m_window;
        private PopupWindow? popupWindow;
    }
}
