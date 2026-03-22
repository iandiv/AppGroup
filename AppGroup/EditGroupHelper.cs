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
            
        }

        public bool IsExist() {
            IntPtr hWnd = NativeMethods.FindWindow(null, windowTitle);
            return hWnd != IntPtr.Zero;
        }

        public void Activate() {
            IntPtr hWnd = NativeMethods.FindWindow(null, windowTitle);
            if (hWnd != IntPtr.Zero) {
                // Write to file FIRST so EditGroupWindow_Activated reads correct id
                SaveGroupIdToFile(groupId.ToString());
                NativeMethods.SendString(hWnd, $"__SHOW_EDIT__|{groupId}");
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
                Debug.WriteLine($"Failed to save group ID: {ex.Message}");
            }
        }



    }
}