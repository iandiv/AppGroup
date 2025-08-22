using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using IWshRuntimeLibrary;

namespace AppGroup {
    public class TaskbarManager {
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        
        public static async Task<bool> IsShortcutPinnedToTaskbar(string groupName) {
            return await Task.Run(() => {
                try {
                    string taskbarPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar"
                    );

                    if (!Directory.Exists(taskbarPath))
                        return false;

                    var taskbarShortcuts = Directory.GetFiles(taskbarPath, "*.lnk");
                    IWshShell wshShell = new WshShell();

                    foreach (string shortcutFile in taskbarShortcuts) {
                        try {
                            IWshShortcut shortcut = (IWshShortcut)wshShell.CreateShortcut(shortcutFile);
                            if (shortcut.Arguments.Contains($"\"{groupName}\"")) {
                                return true;
                            }
                        }
                        catch { }
                    }
                    return false;
                }
                catch {
                    return false;
                }
            });
        }

        // Fire-and-forget version - returns immediately
        public static void TryRefreshTaskbarWithoutRestartAsync() {
            _ = Task.Run(async () => {
                try {
                    // Quick shell notification only
                    NativeMethods.SHChangeNotify(NativeMethods.SHCNE_ASSOCCHANGED, NativeMethods.SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
                    
                    // Minimal delay
                    await Task.Delay(100);
                    
                    // Force taskbar redraw
                    IntPtr taskbarWindow = NativeMethods.FindWindow("Shell_TrayWnd", null);
                    if (taskbarWindow != IntPtr.Zero) {
                        NativeMethods.InvalidateRect(taskbarWindow, IntPtr.Zero, true);
                    }
                }
                catch (Exception ex) {
                    Console.WriteLine($"Error in refresh attempt: {ex.Message}");
                }
            });
        }

        // Synchronous version that returns immediately
        public static async Task TryRefreshTaskbarWithoutRestart() {
            await Task.Run(() => {
                try {
                    // Just the essential refresh operations
                    NativeMethods.SHChangeNotify(NativeMethods.SHCNE_ASSOCCHANGED, NativeMethods.SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
                    
                    IntPtr taskbarWindow = NativeMethods.FindWindow("Shell_TrayWnd", null);
                    if (taskbarWindow != IntPtr.Zero) {
                        NativeMethods.InvalidateRect(taskbarWindow, IntPtr.Zero, true);
                    }
                }
                catch (Exception ex) {
                    Console.WriteLine($"Error in refresh attempt: {ex.Message}");
                }
            });
        }

        // Fire-and-forget version for heavy operations
        public static void ForceTaskbarUpdateAsync() {
            _ = Task.Run(async () => {
                if (!await _semaphore.WaitAsync(100)) // Quick timeout
                    return;
                
                try {
                    var killProcess = new Process {
                        StartInfo = new ProcessStartInfo {
                            FileName = "taskkill",
                            Arguments = "/f /im explorer.exe",
                            WindowStyle = ProcessWindowStyle.Hidden,
                            CreateNoWindow = true,
                            UseShellExecute = false
                        }
                    };

                    killProcess.Start();
                    await killProcess.WaitForExitAsync();

                    await Task.Delay(300); // Reduced delay

                    Process.Start("explorer.exe");
                }
                catch (Exception ex) {
                    Console.WriteLine($"Error restarting explorer: {ex.Message}");
                }
                finally {
                    _semaphore.Release();
                }
            });
        }

        private static string _cachedTaskbarPath;
        private static string _cachedExePath;
        private static readonly object _lockObject = new object(); // Thread safety for caching

        // Helper method to extract group name from quoted arguments
        private static string ExtractGroupNameFromArguments(string arguments) {
            if (string.IsNullOrEmpty(arguments))
                return null;

            // Find the last quoted string in the arguments
            int lastQuoteStart = arguments.LastIndexOf('"');
            if (lastQuoteStart == -1)
                return null;

            int secondLastQuoteStart = arguments.LastIndexOf('"', lastQuoteStart - 1);
            if (secondLastQuoteStart == -1)
                return null;

            // Extract the text between the last pair of quotes
            string groupName = arguments.Substring(secondLastQuoteStart + 1, lastQuoteStart - secondLastQuoteStart - 1);
            return groupName;
        }

        public static async Task UpdateTaskbarShortcutIcon(string oldGroupName, string newGroupName, string iconPath) {
            await Task.Run(async () => {
                try {
                    // Cache paths to avoid repeated lookups with thread safety
                    if (_cachedTaskbarPath == null) {
                        lock (_lockObject) {
                            _cachedTaskbarPath ??= Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar"
                            );
                        }
                    }
                    if (!Directory.Exists(_cachedTaskbarPath))
                        return;

                    if (_cachedExePath == null) {
                        lock (_lockObject) {
                            _cachedExePath ??= Process.GetCurrentProcess().MainModule?.FileName ??
                                              Path.Combine(Path.GetDirectoryName(Environment.ProcessPath), "AppGroup.exe");
                        }
                    }

                    // Use EnumerateFiles instead of GetFiles for better memory efficiency
                    var taskbarShortcuts = Directory.EnumerateFiles(_cachedTaskbarPath, "*.lnk");

                    bool groupNameChanged = oldGroupName != newGroupName;

                    // Create COM object once and reuse
                    IWshShell wshShell = new WshShell();
                    try {
                        foreach (string shortcutFile in taskbarShortcuts) {
                            try {
                                IWshShortcut shortcut = (IWshShortcut)wshShell.CreateShortcut(shortcutFile);
                                try {
                                    // Pre-fetch properties once to avoid multiple COM calls
                                    string targetPath = shortcut.TargetPath;
                                    string arguments = shortcut.Arguments;
                                    string description = shortcut.Description;

                                    // Check if this is our executable
                                    if (!targetPath.Equals(_cachedExePath, StringComparison.OrdinalIgnoreCase))
                                        continue;

                                    // Extract group name from quoted arguments
                                    string extractedGroupName = ExtractGroupNameFromArguments(arguments);

                                    // Check for exact match with the old group name
                                    bool isOurShortcut = false;

                                    if (!string.IsNullOrEmpty(extractedGroupName)) {
                                        // Exact match with extracted group name
                                        isOurShortcut = extractedGroupName.Equals(oldGroupName, StringComparison.OrdinalIgnoreCase);
                                    }
                                    else {
                                        // Fallback: check description for exact match
                                        isOurShortcut = description.Equals($"{oldGroupName} - AppGroup Shortcut", StringComparison.OrdinalIgnoreCase);
                                    }

                                    if (isOurShortcut) {
                                        string shortcutFileName = Path.GetFileNameWithoutExtension(shortcutFile);
                                        Console.WriteLine($"Updating taskbar shortcut: {shortcutFileName}");
                                        Console.WriteLine($"Extracted group name: '{extractedGroupName}'");
                                        Console.WriteLine($"Target group name: '{oldGroupName}'");

                                        // Update icon location
                                        shortcut.IconLocation = iconPath;

                                        // Update arguments if group name changed
                                        if (groupNameChanged && !string.IsNullOrEmpty(extractedGroupName)) {
                                            // Replace the exact quoted group name
                                            string oldQuotedName = $"\"{extractedGroupName}\"";
                                            string newQuotedName = $"\"{newGroupName}\"";
                                            shortcut.Arguments = arguments.Replace(oldQuotedName, newQuotedName);

                                            // Update description
                                            shortcut.Description = $"{newGroupName} - AppGroup Shortcut";
                                        }

                                        shortcut.Save();

                                        // Rename the shortcut file to match the new group name
                                        if (groupNameChanged) {
                                            try {
                                                string newShortcutPath = Path.Combine(_cachedTaskbarPath, $"{newGroupName}.lnk");
                                                // Only rename if the new filename doesn't already exist
                                                if (!System.IO.File.Exists(newShortcutPath)) {
                                                    System.IO.File.Move(shortcutFile, newShortcutPath);
                                                    Console.WriteLine($"Renamed shortcut file to: {newGroupName}.lnk");
                                                }
                                                else {
                                                    Console.WriteLine($"Cannot rename shortcut: {newGroupName}.lnk already exists");
                                                }
                                            }
                                            catch (Exception ex) {
                                                Console.WriteLine($"Error renaming shortcut file: {ex.Message}");
                                            }
                                        }

                                        // Quick refresh without delays
                                        //RefreshDesktop();
                                        //await Task.Delay(200);

                                        TryRefreshTaskbarWithoutRestartAsync();
                                        return; // Exit early once we find and update our shortcut
                                    }
                                }
                                finally {
                                    // Release COM object immediately after use
                                    System.Runtime.InteropServices.Marshal.ReleaseComObject(shortcut);
                                }
                            }
                            catch (Exception ex) {
                                Console.WriteLine($"Error processing shortcut {shortcutFile}: {ex.Message}");
                            }
                        }
                    }
                    finally {
                        // Release COM shell object
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(wshShell);
                    }
                }
                catch (Exception ex) {
                    Console.WriteLine($"Error updating taskbar shortcut: {ex.Message}");
                }
            });
        }

        public static async Task UpdateTaskbarShortcutIcon(string groupName, string iconPath) {
            await UpdateTaskbarShortcutIcon(groupName, groupName, iconPath);
        }

       
        public static void UpdateTaskbarShortcutIconAsync(string groupName, string iconPath) {
            _ = Task.Run(async () => {
                await UpdateTaskbarShortcutIcon(groupName, iconPath);
                
                // Optional delayed refresh
                await Task.Delay(200);
                TryRefreshTaskbarWithoutRestartAsync();
            });
        }

        private static void RefreshDesktop() {
            NativeMethods.SHChangeNotify(NativeMethods.SHCNE_ASSOCCHANGED, NativeMethods.SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
        }
    }
}