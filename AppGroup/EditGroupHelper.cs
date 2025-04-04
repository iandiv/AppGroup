using Microsoft.UI.Windowing;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Text;
using Microsoft.UI;
using Windows.UI.WindowManagement;

namespace AppGroup {
    public class EditGroupHelper {
        private readonly string windowTitle;
        private readonly int groupId;
        private readonly string groupIdFilePath;
        private readonly string logFilePath;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public EditGroupHelper(string windowTitle, int groupId) {
            this.windowTitle = windowTitle;
            this.groupId = groupId;
            // Define the local application data path
            string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appDataPath = Path.Combine(localAppDataPath, "AppGroup");

            // Ensure the directory exists
            if (!Directory.Exists(appDataPath)) {
                Directory.CreateDirectory(appDataPath);
            }

            groupIdFilePath = Path.Combine(appDataPath, "gid");

          
        }

        public bool IsExist() {
            IntPtr hWnd = FindWindow(null, windowTitle);
            return hWnd != IntPtr.Zero;
        }

        public void Activate() {
            IntPtr hWnd = FindWindow(null, windowTitle);
            if (hWnd != IntPtr.Zero) {
                SetForegroundWindow(hWnd);
              UpdateFile();
                  
            }
            else {
              UpdateFile();

                EditGroupWindow editGroupWindow = new EditGroupWindow(groupId);

              
                editGroupWindow.Activate();
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