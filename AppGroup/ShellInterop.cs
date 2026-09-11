using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AppGroup {
    internal static class ShellInterop {

        // ---- Correctly-ordered IShellLinkW (18 methods, matches real vtable) ----
        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IShellLinkW {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cch, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        public class CShellLink { }

        [ComImport, Guid("0000010b-0000-0000-C000-000000000046"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IPersistFile {
            void GetClassID(out Guid pClassID);
            void IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }

        // ---- IShellItem / IShellItem2, only slots we use are real, rest are placeholders ----
        public enum SIGDN : uint {
            SIGDN_FILESYSPATH = 0x80058000
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROPERTYKEY {
            public Guid fmtid;
            public uint pid;
        }

        // PKEY_AppUserModel_ID
        public static readonly PROPERTYKEY PKEY_AppUserModel_ID = new PROPERTYKEY {
            fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
            pid = 5
        };

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IShellItem2 {
            void BindToHandler_Placeholder();
            void GetParent_Placeholder();
            void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
            void GetAttributes_Placeholder();
            void Compare_Placeholder();
            void GetPropertyStore_Placeholder();
            void GetPropertyStoreWithCreateObject_Placeholder();
            void GetPropertyStoreForKeys_Placeholder();
            void GetPropertyDescriptionList_Placeholder();
            void Update_Placeholder();
            void GetProperty_Placeholder();
            void GetCLSID_Placeholder();
            void GetFileTime_Placeholder();
            void GetInt32_Placeholder();
            [PreserveSig]
            int GetString(ref PROPERTYKEY key, out IntPtr ppsz);
        }

        [DllImport("shell32.dll")]
        public static extern int SHCreateItemFromIDList(IntPtr pidl, ref Guid riid, out IShellItem2 ppv);

        [DllImport("shell32.dll")]
        public static extern IntPtr ILCombine(IntPtr pidl1, IntPtr pidl2);

        [DllImport("ole32.dll")]
        public static extern void CoTaskMemFree(IntPtr pv);

        public static readonly Guid IID_IShellItem2 = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");

        /// <summary>Result of resolving one item from a CFSTR_SHELLIDLIST payload.</summary>
        public class ResolvedShellItem {
            public bool IsFileSystem;
            public string Path;      // real path, if IsFileSystem
            public string Aumid;     // AppUserModelID, if packaged app
        }

        /// <summary>Parses a raw CFSTR_SHELLIDLIST ("Shell IDList Array") buffer into resolved items.</summary>
        public static System.Collections.Generic.List<ResolvedShellItem> ParseShellIdList(byte[] raw) {
            var results = new System.Collections.Generic.List<ResolvedShellItem>();
            GCHandle handle = GCHandle.Alloc(raw, GCHandleType.Pinned);
            try {
                IntPtr basePtr = handle.AddrOfPinnedObject();
                uint cidl = (uint)Marshal.ReadInt32(basePtr, 0);
                Debug.WriteLine($"[ShellIDList] cidl = {cidl}");
                var offsets = new uint[cidl + 1];
                for (int i = 0; i <= cidl; i++)
                    offsets[i] = (uint)Marshal.ReadInt32(basePtr, 4 + i * 4);

                IntPtr parentPidl = IntPtr.Add(basePtr, (int)offsets[0]);

                for (int i = 1; i <= cidl; i++) {
                    IntPtr childPidl = IntPtr.Add(basePtr, (int)offsets[i]);
                    IntPtr absolutePidl = ILCombine(parentPidl, childPidl);
                    if (absolutePidl == IntPtr.Zero) continue;

                    try {
                        Guid iid = IID_IShellItem2;
                        int hr = SHCreateItemFromIDList(absolutePidl, ref iid, out IShellItem2 item);
                        Debug.WriteLine($"[ShellIDList] SHCreateItemFromIDList hr=0x{hr:X8}, item null={item == null}");
                        if (hr != 0 || item == null) continue;

                        var resolved = new ResolvedShellItem();

                        try {
                            item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out IntPtr pathPtr);
                            resolved.IsFileSystem = true;
                            resolved.Path = Marshal.PtrToStringUni(pathPtr);
                            Marshal.FreeCoTaskMem(pathPtr);
                        }
                        catch {
                            // Not filesystem-backed — try AUMID
                            var key = PKEY_AppUserModel_ID;
                            int shr = item.GetString(ref key, out IntPtr aumidPtr);
                            if (shr == 0 && aumidPtr != IntPtr.Zero) {
                                resolved.IsFileSystem = false;
                                resolved.Aumid = Marshal.PtrToStringUni(aumidPtr);
                                Marshal.FreeCoTaskMem(aumidPtr);
                            }
                        }

                        if (resolved.IsFileSystem || resolved.Aumid != null)
                            results.Add(resolved);
                    }
                    finally {
                        CoTaskMemFree(absolutePidl);
                    }
                }
            }
            finally {
                handle.Free();
            }
            return results;
        }

        /// <summary>Creates (or reuses) a .lnk targeting a real file path, returns its path.</summary>
        public static string CreateFileShortcut(string targetPath, string displayName) {
            string folder = System.IO.Path.Combine(AppPaths.BaseDataPath, "StartAppShortcuts");
            System.IO.Directory.CreateDirectory(folder);

            string safeName = string.Join("_", displayName.Split(System.IO.Path.GetInvalidFileNameChars()));
            string lnkPath = System.IO.Path.Combine(folder, $"{safeName}.lnk");

            var shellLink = (IShellLinkW)new CShellLink();
            shellLink.SetPath(targetPath);
            shellLink.SetDescription(displayName);

            var persistFile = (IPersistFile)shellLink;
            persistFile.Save(lnkPath, true);

            return lnkPath;
        }
        /// <summary>Creates (or reuses) a .lnk targeting shell:AppsFolder\{aumid}, returns its path.</summary>
        public static string CreateAppsFolderShortcut(string aumid, string displayName) {
            string folder = System.IO.Path.Combine(AppPaths.BaseDataPath, "StartAppShortcuts");
            System.IO.Directory.CreateDirectory(folder);

            string safeName = string.Join("_", displayName.Split(System.IO.Path.GetInvalidFileNameChars()));
            string lnkPath = System.IO.Path.Combine(folder, $"{safeName}.lnk");

            var shellLink = (IShellLinkW)new CShellLink();
            shellLink.SetPath($"shell:AppsFolder\\{aumid}");
            shellLink.SetDescription(displayName);

            var persistFile = (IPersistFile)shellLink;
            persistFile.Save(lnkPath, true);

            return lnkPath;
        }
    }
}
