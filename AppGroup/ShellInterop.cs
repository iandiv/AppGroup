using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
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

        /// <summary>
        /// Reads a Windows shortcut (.lnk) file using native IShellLinkW and IPersistFile COM interfaces.
        /// Fully handles Unicode and Arabic file paths without the failures of legacy WScript.Shell.
        /// </summary>
        public static bool TryReadShortcut(
            string lnkPath,
            out string targetPath,
            out string arguments,
            out string iconPath,
            out int iconIndex,
            out string description) 
        {
            targetPath = string.Empty;
            arguments = string.Empty;
            iconPath = string.Empty;
            iconIndex = 0;
            description = string.Empty;

            if (string.IsNullOrWhiteSpace(lnkPath) || !System.IO.File.Exists(lnkPath))
                return false;

            try {
                var shellLink = (IShellLinkW)new CShellLink();
                var persistFile = (IPersistFile)shellLink;
                persistFile.Load(lnkPath, 0); // 0 = STGM_READ

                var targetSb = new StringBuilder(1024);
                shellLink.GetPath(targetSb, targetSb.Capacity, IntPtr.Zero, 0);
                targetPath = Environment.ExpandEnvironmentVariables(targetSb.ToString().Trim());

                var argsSb = new StringBuilder(2048);
                shellLink.GetArguments(argsSb, argsSb.Capacity);
                arguments = argsSb.ToString().Trim();

                var iconSb = new StringBuilder(1024);
                shellLink.GetIconLocation(iconSb, iconSb.Capacity, out iconIndex);
                iconPath = Environment.ExpandEnvironmentVariables(iconSb.ToString().Trim());

                var descSb = new StringBuilder(1024);
                shellLink.GetDescription(descSb, descSb.Capacity);
                description = descSb.ToString().Trim();

                return true;
            }
            catch (Exception ex) {
                Debug.WriteLine($"TryReadShortcut error for {lnkPath}: {ex.Message}");
                return false;
            }
        }

        public static bool TryReadShortcut(
            string lnkPath,
            out string targetPath,
            out string arguments,
            out string iconPath,
            out int iconIndex) 
        {
            return TryReadShortcut(lnkPath, out targetPath, out arguments, out iconPath, out iconIndex, out _);
        }

        /// <summary>
        /// Creates or updates a shortcut (.lnk) using native IShellLinkW and IPersistFile COM interfaces.
        /// Safe for Unicode/Arabic target and destination paths.
        /// </summary>
        public static bool SaveShortcut(
            string shortcutPath,
            string targetPath,
            string? arguments = null,
            string? description = null,
            string? iconLocation = null,
            string? workingDirectory = null) 
        {
            try {
                string? folder = Path.GetDirectoryName(shortcutPath);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

                var shellLink = (IShellLinkW)new CShellLink();
                shellLink.SetPath(targetPath);
                if (!string.IsNullOrEmpty(arguments)) shellLink.SetArguments(arguments);
                if (!string.IsNullOrEmpty(description)) shellLink.SetDescription(description);
                if (!string.IsNullOrEmpty(iconLocation)) shellLink.SetIconLocation(iconLocation, 0);
                if (!string.IsNullOrEmpty(workingDirectory)) shellLink.SetWorkingDirectory(workingDirectory);

                var persistFile = (IPersistFile)shellLink;
                persistFile.Save(shortcutPath, true);
                return true;
            }
            catch (Exception ex) {
                Debug.WriteLine($"SaveShortcut error for {shortcutPath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Updates the icon location of an existing .lnk shortcut safely.
        /// </summary>
        public static bool UpdateShortcutIcon(string lnkPath, string iconPath, int iconIndex = 0) {
            if (string.IsNullOrWhiteSpace(lnkPath) || !File.Exists(lnkPath)) return false;
            try {
                var shellLink = (IShellLinkW)new CShellLink();
                var persistFile = (IPersistFile)shellLink;
                persistFile.Load(lnkPath, 0);
                shellLink.SetIconLocation(iconPath, iconIndex);
                persistFile.Save(lnkPath, true);
                return true;
            }
            catch (Exception ex) {
                Debug.WriteLine($"UpdateShortcutIcon error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reads the description/comment of a shortcut safely.
        /// </summary>
        public static string? ReadShortcutDescription(string lnkPath) {
            if (string.IsNullOrWhiteSpace(lnkPath) || !File.Exists(lnkPath)) return null;
            try {
                var shellLink = (IShellLinkW)new CShellLink();
                var persistFile = (IPersistFile)shellLink;
                persistFile.Load(lnkPath, 0);
                var sb = new StringBuilder(1024);
                shellLink.GetDescription(sb, sb.Capacity);
                return sb.ToString().Trim();
            }
            catch {
                return null;
            }
        }

        // ---- IShellItem / IShellItem2 ----
        public enum SIGDN : uint {
            SIGDN_NORMALDISPLAY = 0x00000000,
            SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000,
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

        // PKEY_Link_TargetParsingPath (System.Link.TargetParsingPath)
        public static readonly PROPERTYKEY PKEY_Link_TargetParsingPath = new PROPERTYKEY {
            fmtid = new Guid("B9B4B3FC-2B23-45B9-AA27-39D04A680FBE"),
            pid = 2
        };

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IShellItem {
            void BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            [PreserveSig]
            int GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        [ComImport, Guid("7E9FB0D3-919F-4307-AB2E-9B1860310C93"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IShellItem2 : IShellItem {
            new void BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);
            new void GetParent(out IShellItem ppsi);
            [PreserveSig]
            new int GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
            new void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            new void Compare(IShellItem psi, uint hint, out int piOrder);

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
        public static extern int SHCreateItemFromIDList(IntPtr pidl, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);

        [DllImport("shell32.dll")]
        public static extern IntPtr ILCombine(IntPtr pidl1, IntPtr pidl2);

        [DllImport("ole32.dll")]
        public static extern void CoTaskMemFree(IntPtr pv);

        public static readonly Guid IID_IShellItem = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");
        public static readonly Guid IID_IShellItem2 = new Guid("7E9FB0D3-919F-4307-AB2E-9B1860310C93");

        [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW")]
        public static extern IntPtr SHGetFileInfoPidl(
            IntPtr pszPath, uint dwFileAttributes,
            ref NativeMethods.SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        /// <summary>Extracts the high-resolution shell icon (up to 256x256 JUMBO) for any PIDL and saves it as a PNG.</summary>
        public static string ExtractIconFromPidl(IntPtr pidl, string baseName) {
            try {
                var shfi = new NativeMethods.SHFILEINFO();
                // 0x00000008 = SHGFI_PIDL, 0x00004000 = SHGFI_SYSICONINDEX
                IntPtr ret = SHGetFileInfoPidl(pidl, 0, ref shfi, (uint)Marshal.SizeOf(shfi), 0x00000008 | 0x00004000);
                if (ret == IntPtr.Zero || shfi.iIcon < 0) return null;

                Guid iid = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"); // IID_IImageList
                NativeMethods.IImageList imageList = null;
                int hr = NativeMethods.SHGetImageList(NativeMethods.SHIL_JUMBO, ref iid, out imageList);
                if (hr != 0 || imageList == null) {
                    hr = NativeMethods.SHGetImageList(NativeMethods.SHIL_EXTRALARGE, ref iid, out imageList);
                }
                if (hr != 0 || imageList == null) return null;

                IntPtr hIcon = IntPtr.Zero;
                imageList.GetIcon(shfi.iIcon, 1, ref hIcon); // 1 = ILD_TRANSPARENT
                if (hIcon != IntPtr.Zero) {
                    try {
                        using (var icon = System.Drawing.Icon.FromHandle(hIcon)) {
                            using (var bmp = icon.ToBitmap()) {
                                string outputDir = System.IO.Path.Combine(AppPaths.BaseDataPath, "Icons");
                                System.IO.Directory.CreateDirectory(outputDir);
                                string safeName = string.Join("_", (baseName ?? "app").Split(System.IO.Path.GetInvalidFileNameChars()));
                                string iconFileName = $"{safeName}_{Math.Abs(baseName.GetHashCode())}.png";
                                string iconPath = System.IO.Path.Combine(outputDir, iconFileName);
                                bmp.Save(iconPath, System.Drawing.Imaging.ImageFormat.Png);
                                return iconPath;
                            }
                        }
                    }
                    finally {
                        NativeMethods.DestroyIcon(hIcon);
                    }
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"ExtractIconFromPidl error: {ex.Message}");
            }
            return null;
        }

        /// <summary>Result of resolving one item from a CFSTR_SHELLIDLIST payload.</summary>
        public class ResolvedShellItem {
            public bool IsFileSystem;
            public string Path;      // real path, if IsFileSystem
            public string Aumid;     // AppUserModelID, if packaged app
            public string DisplayName; // Friendly display name from shell
            public string ExtractedIconPath; // High-resolution shell icon extracted directly from PIDL
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
                        // First try IShellItem2, fallback to IShellItem
                        Guid iid2 = IID_IShellItem2;
                        int hr = SHCreateItemFromIDList(absolutePidl, ref iid2, out object objItem);
                        if (hr != 0 || objItem == null) {
                            Guid iid1 = IID_IShellItem;
                            hr = SHCreateItemFromIDList(absolutePidl, ref iid1, out objItem);
                        }

                        if (hr != 0 || objItem == null) continue;

                        var shellItem = objItem as IShellItem;
                        var shellItem2 = objItem as IShellItem2;
                        var resolved = new ResolvedShellItem();

                        // 1. Try reading the friendly display name
                        if (shellItem != null) {
                            int hrName = shellItem.GetDisplayName(SIGDN.SIGDN_NORMALDISPLAY, out IntPtr namePtr);
                            if (hrName == 0 && namePtr != IntPtr.Zero) {
                                resolved.DisplayName = NormalizeAppName(Marshal.PtrToStringUni(namePtr));
                                Marshal.FreeCoTaskMem(namePtr);
                            }
                        }

                        // 2. Try reading AUMID property via IShellItem2
                        if (shellItem2 != null) {
                            var key = PKEY_AppUserModel_ID;
                            int shr = shellItem2.GetString(ref key, out IntPtr aumidPtr);
                            if (shr == 0 && aumidPtr != IntPtr.Zero) {
                                resolved.Aumid = Marshal.PtrToStringUni(aumidPtr);
                                Marshal.FreeCoTaskMem(aumidPtr);
                            }
                        }

                        // 3. Try reading desktop parsing path (e.g. shell:AppsFolder\... or CLSID for AppsFolder)
                        if (shellItem != null) {
                            int hrParsing = shellItem.GetDisplayName(SIGDN.SIGDN_DESKTOPABSOLUTEPARSING, out IntPtr parsingPtr);
                            if (hrParsing == 0 && parsingPtr != IntPtr.Zero) {
                                string parsingPath = Marshal.PtrToStringUni(parsingPtr);
                                Marshal.FreeCoTaskMem(parsingPtr);
                                if (!string.IsNullOrEmpty(parsingPath)) {
                                    if (parsingPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && (System.IO.File.Exists(parsingPath) || System.IO.Directory.Exists(parsingPath))) {
                                        resolved.IsFileSystem = true;
                                        resolved.Path = parsingPath;
                                    }
                                    else if (string.IsNullOrEmpty(resolved.Aumid)) {
                                        if (parsingPath.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase)) {
                                            resolved.Aumid = parsingPath.Substring("shell:AppsFolder\\".Length);
                                        }
                                        else if (parsingPath.IndexOf("4234d49b-0245-4df3-b780-3893943456e1", StringComparison.OrdinalIgnoreCase) >= 0 || parsingPath.Contains("!")) {
                                            int lastSlash = parsingPath.LastIndexOf('\\');
                                            resolved.Aumid = lastSlash >= 0 ? parsingPath.Substring(lastSlash + 1) : parsingPath;
                                        }
                                    }
                                }
                            }
                        }

                        // 4. Try reading target parsing path via IShellItem2
                        string? linkTarget = null;
                        if (shellItem2 != null) {
                            var keyTarget = PKEY_Link_TargetParsingPath;
                            int hrTarget = shellItem2.GetString(ref keyTarget, out IntPtr targetPtr);
                            if (hrTarget == 0 && targetPtr != IntPtr.Zero) {
                                linkTarget = Marshal.PtrToStringUni(targetPtr);
                                Marshal.FreeCoTaskMem(targetPtr);
                            }
                        }

                        // 5. PRIORITY: Check if this matches a real .lnk in Start Menu (Brave Apps, Chrome Apps, Edge Apps, Programs)
                        string? foundLnk = FindShortcutInStartMenu(resolved.DisplayName, resolved.Aumid);
                        if (!string.IsNullOrEmpty(foundLnk) && System.IO.File.Exists(foundLnk)) {
                            resolved.IsFileSystem = true;
                            resolved.Path = foundLnk;
                            resolved.Aumid = null;
                            resolved.ExtractedIconPath = null;
                        }
                        else if (!string.IsNullOrEmpty(linkTarget) && (System.IO.File.Exists(linkTarget) || System.IO.Directory.Exists(linkTarget))) {
                            bool isBrowserExe = linkTarget.EndsWith("chrome.exe", StringComparison.OrdinalIgnoreCase) ||
                                               linkTarget.EndsWith("brave.exe", StringComparison.OrdinalIgnoreCase) ||
                                               linkTarget.EndsWith("msedge.exe", StringComparison.OrdinalIgnoreCase);

                            // Only use direct linkTarget if it is a real .lnk or a standalone non-browser executable
                            if (linkTarget.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || !isBrowserExe) {
                                resolved.IsFileSystem = true;
                                resolved.Path = linkTarget;
                            }
                        }

                        // 6. Try standard file system path if not yet resolved
                        if (string.IsNullOrEmpty(resolved.Path) && shellItem != null) {
                            int hrPath = shellItem.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out IntPtr pathPtr);
                            if (hrPath == 0 && pathPtr != IntPtr.Zero) {
                                string p = Marshal.PtrToStringUni(pathPtr);
                                Marshal.FreeCoTaskMem(pathPtr);
                                if (!string.IsNullOrEmpty(p) && (System.IO.File.Exists(p) || System.IO.Directory.Exists(p))) {
                                    resolved.IsFileSystem = true;
                                    resolved.Path = p;
                                }
                            }
                        }

                        // 7. If still not a filesystem path, try retrieving original 256x256 PWA icon from browser user data
                        if (!resolved.IsFileSystem && !string.IsNullOrEmpty(resolved.Aumid)) {
                            string? appId = ExtractAppId(resolved.Aumid);
                            if (!string.IsNullOrEmpty(appId)) {
                                string? pwaIcon = FindPwaIconPath(appId);
                                if (!string.IsNullOrEmpty(pwaIcon) && System.IO.File.Exists(pwaIcon)) {
                                    resolved.ExtractedIconPath = pwaIcon;
                                }
                            }
                        }

                        if (resolved.IsFileSystem || !string.IsNullOrEmpty(resolved.Aumid))
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

        /// <summary>
        /// Cleans app names by removing directional Unicode marks (LRM, RLM, etc.),
        /// zero-width formatting characters, and .lnk extension.
        /// </summary>
        public static string NormalizeAppName(string? name) {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;

            if (name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) {
                name = System.IO.Path.GetFileNameWithoutExtension(name);
            }

            var sb = new StringBuilder(name.Length);
            foreach (char c in name) {
                if (!char.IsControl(c) &&
                    char.GetUnicodeCategory(c) != UnicodeCategory.Format &&
                    c != '\uFEFF' && c != '\u200B' && c != '\u200C' && c != '\u200D') {
                    sb.Append(c);
                }
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Breadth-first search for .lnk files that safely handles UnauthorizedAccessException
        /// and skips reparse points/junctions.
        /// </summary>
        public static IEnumerable<string> SafeEnumerateLnkFiles(string rootDirectory) {
            if (string.IsNullOrWhiteSpace(rootDirectory) || !System.IO.Directory.Exists(rootDirectory))
                yield break;

            var queue = new Queue<string>();
            queue.Enqueue(rootDirectory);

            while (queue.Count > 0) {
                string currentDir = queue.Dequeue();

                string[] files = null;
                try {
                    files = System.IO.Directory.GetFiles(currentDir, "*.lnk");
                }
                catch { }

                if (files != null) {
                    foreach (var file in files) {
                        yield return file;
                    }
                }

                string[] subDirs = null;
                try {
                    subDirs = System.IO.Directory.GetDirectories(currentDir);
                }
                catch { }

                if (subDirs != null) {
                    foreach (var subDir in subDirs) {
                        try {
                            var dirInfo = new System.IO.DirectoryInfo(subDir);
                            if ((dirInfo.Attributes & System.IO.FileAttributes.ReparsePoint) != 0)
                                continue;
                            queue.Enqueue(subDir);
                        }
                        catch { }
                    }
                }
            }
        }

        /// <summary>
        /// Extracts chromium extension / app ID (24 to 32 characters) from AUMID, argument string, or file path.
        /// </summary>
        public static string? ExtractAppId(string? input) {
            if (string.IsNullOrWhiteSpace(input)) return null;

            // Chromium app IDs are 24 to 32 hexadecimal letters from 'a' to 'p'
            var match = Regex.Match(input, @"([a-pA-P]{24,32})");
            if (match.Success) return match.Groups[1].Value.ToLowerInvariant();

            string clean = input;
            if (clean.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
                clean = clean.Substring("shell:AppsFolder\\".Length);

            if (clean.StartsWith("_crx_", StringComparison.OrdinalIgnoreCase))
                clean = clean.Substring(5);

            int lastDelim = Math.Max(clean.LastIndexOf('\\'), clean.LastIndexOf('.'));
            if (lastDelim >= 0 && lastDelim < clean.Length - 1)
                clean = clean.Substring(lastDelim + 1);

            if (clean.StartsWith("_crx_", StringComparison.OrdinalIgnoreCase))
                clean = clean.Substring(5);

            return clean.Length >= 8 ? clean.ToLowerInvariant() : null;
        }

        /// <summary>
        /// Looks for the original 256x256 PWA icon in Brave, Chrome, and Edge local profile directories.
        /// Supports both full 32-char AppId and shortened 26-char Windows 11 AUMID via prefix matching.
        /// </summary>
        public static string? FindPwaIconPath(string appId) {
            if (string.IsNullOrWhiteSpace(appId)) return null;

            string cleanAppId = ExtractAppId(appId) ?? appId.ToLowerInvariant();
            string prefix = cleanAppId.Length >= 10 ? cleanAppId.Substring(0, 10) : cleanAppId;

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var browserDirs = new[] {
                System.IO.Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data"),
                System.IO.Path.Combine(localAppData, @"Google\Chrome\User Data"),
                System.IO.Path.Combine(localAppData, @"Google\Chrome Beta\User Data"),
                System.IO.Path.Combine(localAppData, @"Microsoft\Edge\User Data"),
                System.IO.Path.Combine(localAppData, @"Vivaldi\User Data"),
                System.IO.Path.Combine(localAppData, @"Opera Software\Opera Stable")
            };

            foreach (var bDir in browserDirs) {
                if (!System.IO.Directory.Exists(bDir)) continue;

                var profileDirs = new List<string>();
                string defProfile = System.IO.Path.Combine(bDir, "Default");
                if (System.IO.Directory.Exists(defProfile)) profileDirs.Add(defProfile);

                try {
                    var additional = System.IO.Directory.EnumerateDirectories(bDir, "Profile *", System.IO.SearchOption.TopDirectoryOnly);
                    profileDirs.AddRange(additional);
                }
                catch { }

                foreach (var pDir in profileDirs) {
                    string webAppsDir = System.IO.Path.Combine(pDir, "Web Applications");
                    if (!System.IO.Directory.Exists(webAppsDir)) continue;

                    var candidateDirs = new List<string>();
                    string direct1 = System.IO.Path.Combine(webAppsDir, $"_crx_{cleanAppId}");
                    string direct2 = System.IO.Path.Combine(webAppsDir, cleanAppId);
                    if (System.IO.Directory.Exists(direct1)) candidateDirs.Add(direct1);
                    if (System.IO.Directory.Exists(direct2)) candidateDirs.Add(direct2);

                    if (candidateDirs.Count == 0 && !string.IsNullOrEmpty(prefix)) {
                        try {
                            foreach (var subDir in System.IO.Directory.EnumerateDirectories(webAppsDir, $"*{prefix}*", System.IO.SearchOption.TopDirectoryOnly)) {
                                candidateDirs.Add(subDir);
                            }
                        }
                        catch { }
                    }

                    foreach (var candDir in candidateDirs) {
                        if (!System.IO.Directory.Exists(candDir)) continue;

                        try {
                            // 1. Check for any .ico file (multi-resolution including 256x256)
                            var icoFiles = System.IO.Directory.GetFiles(candDir, "*.ico");
                            if (icoFiles.Length > 0) {
                                return icoFiles[0];
                            }

                            // 2. Check for largest .png file (e.g. 256x256 or 512x512)
                            var pngFiles = System.IO.Directory.GetFiles(candDir, "*.png");
                            if (pngFiles.Length > 0) {
                                return pngFiles.OrderByDescending(f => {
                                    try { return new System.IO.FileInfo(f).Length; } catch { return 0L; }
                                }).First();
                            }
                        }
                        catch { }
                    }
                }
            }
            return null;
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string?> _startMenuShortcutCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Searches known Start Menu locations (Brave Apps, Chrome Apps, Edge Apps, User Programs, Common Programs)
        /// to find an existing .lnk matching the displayName or AUMID / AppId.
        /// Uses memory cache to prevent expensive repetitive disk scans.
        /// </summary>
        public static string? FindShortcutInStartMenu(string? displayName, string? aumid = null) {
            string cacheKey = $"{displayName}|{aumid}";
            if (_startMenuShortcutCache.TryGetValue(cacheKey, out var cached)) {
                if (cached == null || System.IO.File.Exists(cached))
                    return cached;
            }

            string? result = FindShortcutInStartMenuCore(displayName, aumid);
            _startMenuShortcutCache[cacheKey] = result;
            return result;
        }

        private static string? FindShortcutInStartMenuCore(string? displayName, string? aumid = null) {
            try {
                string normTargetName = NormalizeAppName(displayName);
                string? appId = ExtractAppId(aumid);
                if (string.IsNullOrEmpty(appId) && !string.IsNullOrEmpty(displayName)) {
                    appId = ExtractAppId(displayName);
                }

                var searchDirs = new List<string>();
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
                string commonPrograms = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
                string commonStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

                // Priority 1: Browser PWA folders
                if (!string.IsNullOrEmpty(appData)) {
                    searchDirs.Add(System.IO.Path.Combine(appData, @"Microsoft\Windows\Start Menu\Programs\Brave Apps"));
                    searchDirs.Add(System.IO.Path.Combine(appData, @"Microsoft\Windows\Start Menu\Programs\Chrome Apps"));
                    searchDirs.Add(System.IO.Path.Combine(appData, @"Microsoft\Windows\Start Menu\Programs\Edge Apps"));
                    searchDirs.Add(System.IO.Path.Combine(appData, @"Microsoft\Windows\Start Menu\Brave Apps"));
                    searchDirs.Add(System.IO.Path.Combine(appData, @"Microsoft\Windows\Start Menu\Chrome Apps"));
                    searchDirs.Add(System.IO.Path.Combine(appData, @"Microsoft\Windows\Start Menu\Edge Apps"));
                }

                // Priority 2: General user programs & start menu
                if (!string.IsNullOrEmpty(programs) && !searchDirs.Contains(programs, StringComparer.OrdinalIgnoreCase))
                    searchDirs.Add(programs);
                if (!string.IsNullOrEmpty(startMenu) && !searchDirs.Contains(startMenu, StringComparer.OrdinalIgnoreCase))
                    searchDirs.Add(startMenu);

                // Priority 3: Common machine programs & start menu
                if (!string.IsNullOrEmpty(commonPrograms) && !searchDirs.Contains(commonPrograms, StringComparer.OrdinalIgnoreCase))
                    searchDirs.Add(commonPrograms);
                if (!string.IsNullOrEmpty(commonStartMenu) && !searchDirs.Contains(commonStartMenu, StringComparer.OrdinalIgnoreCase))
                    searchDirs.Add(commonStartMenu);

                // Priority 4: Desktops
                if (!string.IsNullOrEmpty(desktop) && !searchDirs.Contains(desktop, StringComparer.OrdinalIgnoreCase))
                    searchDirs.Add(desktop);
                if (!string.IsNullOrEmpty(commonDesktop) && !searchDirs.Contains(commonDesktop, StringComparer.OrdinalIgnoreCase))
                    searchDirs.Add(commonDesktop);

                // Discover any additional "*Apps*" folders
                try {
                    if (!string.IsNullOrEmpty(programs) && System.IO.Directory.Exists(programs)) {
                        foreach (var d in System.IO.Directory.EnumerateDirectories(programs, "*Apps*", System.IO.SearchOption.TopDirectoryOnly)) {
                            if (!searchDirs.Contains(d, StringComparer.OrdinalIgnoreCase))
                                searchDirs.Insert(0, d);
                        }
                    }
                    if (!string.IsNullOrEmpty(startMenu) && System.IO.Directory.Exists(startMenu)) {
                        foreach (var d in System.IO.Directory.EnumerateDirectories(startMenu, "*Apps*", System.IO.SearchOption.TopDirectoryOnly)) {
                            if (!searchDirs.Contains(d, StringComparer.OrdinalIgnoreCase))
                                searchDirs.Insert(0, d);
                        }
                    }
                }
                catch { }

                // 1. Direct file check by exact normalized name in known roots
                if (!string.IsNullOrEmpty(normTargetName)) {
                    foreach (var dir in searchDirs) {
                        if (!System.IO.Directory.Exists(dir)) continue;

                        string directPath = System.IO.Path.Combine(dir, $"{normTargetName}.lnk");
                        if (System.IO.File.Exists(directPath)) return directPath;
                    }
                }

                // 2. Exact PWA match by AppId inside .lnk files (highest precision for Chromium/Brave/Edge PWAs)
                if (!string.IsNullOrEmpty(appId)) {
                    string prefix = appId.Length >= 10 ? appId.Substring(0, 10) : appId;
                    string? suffix = appId.Length >= 20 ? appId.Substring(appId.Length - 10) : null;

                    foreach (var dir in searchDirs) {
                        if (!System.IO.Directory.Exists(dir)) continue;

                        foreach (var lnk in SafeEnumerateLnkFiles(dir)) {
                            try {
                                byte[] fileBytes = System.IO.File.ReadAllBytes(lnk);
                                string ascii = System.Text.Encoding.ASCII.GetString(fileBytes);
                                bool matched = ascii.IndexOf(appId, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               (ascii.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0 &&
                                                (suffix == null || ascii.IndexOf(suffix, StringComparison.OrdinalIgnoreCase) >= 0));

                                if (!matched) {
                                    string unicode = System.Text.Encoding.Unicode.GetString(fileBytes);
                                    matched = unicode.IndexOf(appId, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                              (unicode.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0 &&
                                               (suffix == null || unicode.IndexOf(suffix, StringComparison.OrdinalIgnoreCase) >= 0));
                                }

                                if (matched) {
                                    return lnk;
                                }
                            }
                            catch { }
                        }
                    }
                }

                // 3. Scan directories safely for matching .lnk name (exact match first, then prefix/fuzzy)
                string? prefixMatch = null;
                string? fuzzyMatch = null;
                if (!string.IsNullOrEmpty(normTargetName)) {
                    foreach (var dir in searchDirs) {
                        if (!System.IO.Directory.Exists(dir)) continue;

                        foreach (var lnk in SafeEnumerateLnkFiles(dir)) {
                            string fileNorm = NormalizeAppName(System.IO.Path.GetFileNameWithoutExtension(lnk));

                            if (string.Equals(fileNorm, normTargetName, StringComparison.OrdinalIgnoreCase)) {
                                return lnk; // Exact match found!
                            }

                            if (prefixMatch == null && (fileNorm.StartsWith(normTargetName, StringComparison.OrdinalIgnoreCase) ||
                                                       normTargetName.StartsWith(fileNorm, StringComparison.OrdinalIgnoreCase))) {
                                prefixMatch = lnk;
                            }

                            if (fuzzyMatch == null && (fileNorm.Contains(normTargetName, StringComparison.OrdinalIgnoreCase) ||
                                                       normTargetName.Contains(fileNorm, StringComparison.OrdinalIgnoreCase))) {
                                fuzzyMatch = lnk;
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(prefixMatch) && System.IO.File.Exists(prefixMatch)) {
                    return prefixMatch;
                }

                if (!string.IsNullOrEmpty(fuzzyMatch) && System.IO.File.Exists(fuzzyMatch)) {
                    return fuzzyMatch;
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"FindShortcutInStartMenu exception: {ex.Message}");
            }

            return null;
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _runnablePathCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Resolves a stored path to an executable target, recovering real shortcuts
        /// for virtual StartAppShortcuts links (e.g. Brave/Chrome PWAs or desktop apps).
        /// Cached in memory for instant subsequent lookups.
        /// </summary>
        public static string ResolveRunnablePath(string path) {
            if (string.IsNullOrWhiteSpace(path)) return path;

            return _runnablePathCache.GetOrAdd(path, p => {
                try {
                    if (p.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) {
                        string startAppShortcuts = System.IO.Path.Combine(AppPaths.BaseDataPath, "StartAppShortcuts");
                        if (p.StartsWith(startAppShortcuts, StringComparison.OrdinalIgnoreCase)) {
                            string appName = System.IO.Path.GetFileNameWithoutExtension(p);
                            string? realLnk = FindShortcutInStartMenu(appName);
                            if (!string.IsNullOrEmpty(realLnk) && System.IO.File.Exists(realLnk)) {
                                return realLnk;
                            }
                        }
                    }
                }
                catch (Exception ex) {
                    Debug.WriteLine($"ResolveRunnablePath error: {ex.Message}");
                }
                return p;
            });
        }

        /// <summary>Creates (or reuses) a .lnk targeting a real file path, returns its path.</summary>
        public static string CreateFileShortcut(string targetPath, string displayName) {
            // Guard: If a real shortcut in Start Menu exists for this displayName (e.g. Brave/Chrome PWA), prefer it!
            string? realLnk = FindShortcutInStartMenu(displayName);
            if (!string.IsNullOrEmpty(realLnk) && System.IO.File.Exists(realLnk)) {
                return realLnk;
            }

            string folder = System.IO.Path.Combine(AppPaths.BaseDataPath, "StartAppShortcuts");
            System.IO.Directory.CreateDirectory(folder);

            string safeName = string.Join("_", NormalizeAppName(displayName).Split(System.IO.Path.GetInvalidFileNameChars()));
            if (string.IsNullOrWhiteSpace(safeName)) safeName = "App";
            string lnkPath = System.IO.Path.Combine(folder, $"{safeName}.lnk");

            var shellLink = (IShellLinkW)new CShellLink();
            shellLink.SetPath(targetPath);
            shellLink.SetDescription(displayName);

            var persistFile = (IPersistFile)shellLink;
            persistFile.Save(lnkPath, true);

            return lnkPath;
        }

        /// <summary>Creates (or reuses) a .lnk targeting shell:AppsFolder\{aumid}, returns its path.</summary>
        public static string CreateAppsFolderShortcut(string aumid, string displayName, string iconPath = null) {
            // Guard: If there is a real Start Menu / PWA shortcut, always prefer that over a shell:AppsFolder link!
            string? realLnk = FindShortcutInStartMenu(displayName, aumid);
            if (!string.IsNullOrEmpty(realLnk) && System.IO.File.Exists(realLnk)) {
                return realLnk;
            }

            string folder = System.IO.Path.Combine(AppPaths.BaseDataPath, "StartAppShortcuts");
            System.IO.Directory.CreateDirectory(folder);

            string safeName = string.Join("_", NormalizeAppName(displayName).Split(System.IO.Path.GetInvalidFileNameChars()));
            if (string.IsNullOrWhiteSpace(safeName)) safeName = "App";
            string lnkPath = System.IO.Path.Combine(folder, $"{safeName}.lnk");

            // If iconPath is not provided or invalid, check if we can get it from browser PWA User Data
            if (string.IsNullOrEmpty(iconPath) || !System.IO.File.Exists(iconPath)) {
                string? appId = ExtractAppId(aumid);
                if (!string.IsNullOrEmpty(appId)) {
                    string? pwaIco = FindPwaIconPath(appId);
                    if (!string.IsNullOrEmpty(pwaIco) && System.IO.File.Exists(pwaIco)) {
                        iconPath = pwaIco;
                    }
                }
            }

            var shellLink = (IShellLinkW)new CShellLink();
            shellLink.SetPath($"shell:AppsFolder\\{aumid}");
            shellLink.SetDescription(displayName);
            if (!string.IsNullOrEmpty(iconPath) && System.IO.File.Exists(iconPath)) {
                shellLink.SetIconLocation(iconPath, 0);
            }

            var persistFile = (IPersistFile)shellLink;
            persistFile.Save(lnkPath, true);

            return lnkPath;
        }
    }
}
