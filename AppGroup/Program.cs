using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.StartScreen;
namespace AppGroup {
    public class Program {
        [STAThread]
        //static int Main(string[] args) {
        //    WinRT.ComWrappersSupport.InitializeComWrappers();
        //    bool isRedirect = DecideRedirection();

        //    if (!isRedirect) {
        //        Application.Start((p) => {
        //            var context = new DispatcherQueueSynchronizationContext(
        //                DispatcherQueue.GetForCurrentThread());
        //            SynchronizationContext.SetSynchronizationContext(context);
        //            _ = new App();
        //        });
        //    }

        //    return 0;
        //}

        static void Main(string[] args) {
            // Register the same message as your receiver
            int msgId = NativeMethods.WM_UPDATE_GROUP;
            string[] cmdArgs = Environment.GetCommandLineArgs();
            bool isSilent = HasSilentFlag(cmdArgs);

            // Find the target window by title (adjust this to match your window)
            //IntPtr targetWindow = NativeMethods.FindWindow(null, "Popup Window");
            //string[] cmdArgs = Environment.GetCommandLineArgs();

            //if (targetWindow != IntPtr.Zero) {
            //    // FIRST: Position and show the window immediately

            //    // THEN: Send the message to update content (async, non-blocking)
            //    if (cmdArgs.Length > 1) {
            //        string command = cmdArgs[1];
            //        NativeMethods.ShowWindow(targetWindow, NativeMethods.SW_SHOW);



            //        NativeMethods.SendString(targetWindow, command);
            //        NativeMethods.ForceForegroundWindow(targetWindow);

            //        NativeMethods.PositionWindowAboveTaskbar(targetWindow);

            //    }


            //    //return;

            //}



            // Kill if --silent is used with a group name (invalid combination)
            //if (isSilent && cmdArgs.Length > 2) {

            //    return;
            //}

            // Check if running without arguments and another instance is already running
            if (cmdArgs.Length <= 1 && !isSilent) {
                // No arguments provided - check for existing main window instance
                IntPtr existingMainHWnd = NativeMethods.FindWindow(null, "App Group");
                if (existingMainHWnd != IntPtr.Zero) {
                    // Bring existing instance to foreground and exit
                    NativeMethods.SetForegroundWindow(existingMainHWnd);
                    NativeMethods.ShowWindow(existingMainHWnd, NativeMethods.SW_RESTORE);

                    return;
                }
            }

            //if (cmdArgs.Length > 1 && !isSilent) {
            //    string groupName = cmdArgs[1];

            //    if (groupName != "EditGroupWindow" && groupName != "LaunchAll") {
            //        // Quick JSON check
            //        if (!JsonConfigHelper.GroupExistsInJson(groupName)) {

            //        }
            //    }
            //}

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
                        //InitializeJumpListSync();   


                        return;
                    }
                    else if (existingMainHWnd != IntPtr.Zero || existingPopupHWnd != IntPtr.Zero) {

                        return;
                    }
                }
                if (command == "LaunchAll") {
                    string targetGroupName = ExtractGroupNameFromCommandLine(cmdArgs);
                    Task.Run(async () => {
                        await JsonConfigHelper.LaunchAll(targetGroupName);
                    });

                    InitializeJumpListSync();  // If you want to update jump list
                    return;  // Exit immediately after launching apps
                }

                try {
                    int groupId = JsonConfigHelper.FindKeyByGroupName(command);
                    SaveGroupIdToFile(groupId.ToString());
                }
                catch (Exception ex) {
                    System.Diagnostics.Debug.WriteLine($"Failed to find group ID for '{command}': {ex.Message}");
                }


                if (existingPopupHWnd != IntPtr.Zero) {

                    NativeMethods.ShowWindow(existingPopupHWnd, NativeMethods.SW_SHOW);



                    NativeMethods.SendString(existingPopupHWnd, command);
                    NativeMethods.ForceForegroundWindow(existingPopupHWnd);

                    NativeMethods.PositionWindowAboveTaskbar(existingPopupHWnd);
                    InitializeJumpListSync();


                    return;
                }
              


            }

            //InitializeJumpListSync();
            WinRT.ComWrappersSupport.InitializeComWrappers();


            if (cmdArgs.Length <= 1 && !isSilent) {
                // No arguments provided - check for existing main window instance
                IntPtr existingMainHWnd = NativeMethods.FindWindow(null, "App Group");
                if (existingMainHWnd != IntPtr.Zero) {
                    // Bring existing instance to foreground and exit
                    NativeMethods.SetForegroundWindow(existingMainHWnd);
                    NativeMethods.ShowWindow(existingMainHWnd, NativeMethods.SW_RESTORE);

                }
            }



            Application.Start((p) => {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });


        }

        private static void InitializeJumpListSync() {
            try {
                Task.Run(async () => await InitializeJumpListAsync()).Wait();
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Sync jump list initialization failed: {ex.Message}");
            }
        }



        private static async Task InitializeJumpListAsync() {
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



        private static JumpListItem CreateJumpListItemTask() {
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

        private static JumpListItem CreateLaunchAllJumpListItem() {
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

        private static string ExtractGroupNameFromCommandLine(string[] args) {
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
        private static void SaveGroupIdToFile(string groupId) {
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
        private static int ExtractIdFromCommandLine(string[] args) {
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
        private static bool HasSilentFlag(string[] args) {
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

        private static bool DecideRedirection() {
            bool isRedirect = false;
            AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
            ExtendedActivationKind kind = args.Kind;
            AppInstance keyInstance = AppInstance.FindOrRegisterForKey("MySingleInstanceApp");

            if (keyInstance.IsCurrent) {
                keyInstance.Activated += OnActivated;
            }
            else {
                isRedirect = true;
            }

            return isRedirect;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateEvent(
    IntPtr lpEventAttributes, bool bManualReset,
    bool bInitialState, string lpName);

        [DllImport("kernel32.dll")]
        private static extern bool SetEvent(IntPtr hEvent);

        [DllImport("ole32.dll")]
        private static extern uint CoWaitForMultipleObjects(
            uint dwFlags, uint dwMilliseconds, ulong nHandles,
            IntPtr[] pHandles, out uint dwIndex);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        private static IntPtr redirectEventHandle = IntPtr.Zero;

        // Do the redirection on another thread, and use a non-blocking
        // wait method to wait for the redirection to complete.
        public static void RedirectActivationTo(AppActivationArguments args,
                                                AppInstance keyInstance) {
            redirectEventHandle = CreateEvent(IntPtr.Zero, true, false, null);
            Task.Run(() => {
                keyInstance.RedirectActivationToAsync(args).AsTask().Wait();
                SetEvent(redirectEventHandle);
            });

            uint CWMO_DEFAULT = 0;
            uint INFINITE = 0xFFFFFFFF;
            _ = CoWaitForMultipleObjects(
               CWMO_DEFAULT, INFINITE, 1,
               [redirectEventHandle], out uint handleIndex);

            // Bring the window to the foreground
            Process process = Process.GetProcessById((int)keyInstance.ProcessId);

            NativeMethods.ShowWindow(process.MainWindowHandle, NativeMethods.SW_SHOW);
            NativeMethods.ShowWindow(process.MainWindowHandle, NativeMethods.SW_RESTORE);
            SetForegroundWindow(process.MainWindowHandle);

            NativeMethods.ForceForegroundWindow(process.MainWindowHandle);
        }

        private static void OnActivated(object sender, AppActivationArguments args) {
            ExtendedActivationKind kind = args.Kind;
        }
    }

}
