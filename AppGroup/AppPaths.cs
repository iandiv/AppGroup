using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppGroup {
    public static class AppPaths {
        public static bool UsePortableMode = false;

        public static string BaseDataPath {
            get {
                string path;
                if (UsePortableMode) {
                    string exeDir = Path.GetDirectoryName(Environment.ProcessPath);
                    path = Path.Combine(exeDir, "config");
                }
                else {
                    path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppGroup");
                }

                Directory.CreateDirectory(path);
                return path;
            }
        }
    }
}
