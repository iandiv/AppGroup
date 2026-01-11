using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
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

            // Find the target window by title (adjust this to match your window)
            IntPtr targetWindow = NativeMethods.FindWindow(null, "Popup Window");
            string[] cmdArgs = Environment.GetCommandLineArgs();

            if (targetWindow != IntPtr.Zero) {
                // FIRST: Position and show the window immediately

                // THEN: Send the message to update content (async, non-blocking)
                if (cmdArgs.Length > 1) {
                    string command = cmdArgs[1];
                    NativeMethods.ShowWindow(targetWindow, NativeMethods.SW_SHOW);
                  


                    NativeMethods.SendString(targetWindow, command);
                    NativeMethods.ForceForegroundWindow(targetWindow);

                    NativeMethods.PositionWindowAboveTaskbar(targetWindow);
                }



            }
           
                WinRT.ComWrappersSupport.InitializeComWrappers();

            bool isSilent = HasSilentFlag(cmdArgs);

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
        private  static bool HasSilentFlag(string[] args) {
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
            Task.Run(() =>
            {
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
