using Microsoft.UI.Windowing;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Text;
using Microsoft.UI;
using Windows.UI.WindowManagement;
using Microsoft.UI.Xaml;

namespace AppGroup {
    public class EditGroupHelper {
        private readonly string windowTitle;
        private readonly int groupId;
        private readonly string groupIdFilePath;
        private readonly string logFilePath;

        

        public EditGroupHelper(string windowTitle, int groupId) {
            this.windowTitle = windowTitle;
            this.groupId = groupId;
            // Define the local application data path
            //string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            //string appDataPath = Path.Combine(localAppDataPath, "AppGroup");

            //// Ensure the directory exists
            //if (!Directory.Exists(appDataPath)) {
            //    Directory.CreateDirectory(appDataPath);
            //}

            //groupIdFilePath = Path.Combine(appDataPath, "gid");


        }

        public bool IsExist() {
            IntPtr hWnd = NativeMethods.FindWindow(null, windowTitle);
            return hWnd != IntPtr.Zero;
        }

        public void Activate() {
            IntPtr hWnd = NativeMethods.FindWindow(null, windowTitle);
            if (hWnd != IntPtr.Zero) {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
                NativeMethods.SetForegroundWindow(hWnd);
                //UpdateFile();

            }
            else {
                 //editWindow = new EditGroupWindow(groupId);
                //editWindow.InitializeComponent();
                //string executablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppGroup.exe");

                //using (Process process = new Process()) {
                //    process.StartInfo = new ProcessStartInfo {
                //        FileName = executablePath,
                //        Arguments = "EditGroupWindow",
                //        UseShellExecute = false,
                //        RedirectStandardOutput = true,
                //        RedirectStandardError = true,
                //        CreateNoWindow = true
                //    };

                //    process.Start();
                //}


                //UpdateFile();

            }


        }

        private bool UpdateFile() {

            try {
                File.WriteAllText(groupIdFilePath, groupId.ToString());

                return true;
            }
            catch (Exception ex) {
                Debug.WriteLine($"Direct file update failed: {ex.Message}");

                return false;
            }
        }



    }
}