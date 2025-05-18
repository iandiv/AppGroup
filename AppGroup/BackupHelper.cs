using IWshRuntimeLibrary;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinUIEx;
using File = System.IO.File;

namespace AppGroup {
    public class BackupHelper {
        private readonly Window _parentWindow;

        public BackupHelper(Window parentWindow) {
            _parentWindow = parentWindow;
        }

        public async Task ExportBackupAsync() {
            var savePicker = new FileSavePicker {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = $"AppGroup_Backup_{DateTime.Now:yyyyMMdd_HHmmss}"
            };
            savePicker.FileTypeChoices.Add("Backup File", new List<string> { ".agz" });

            // Initialize COM for WinRT file picker
            InitializeWithWindow.Initialize(savePicker, _parentWindow.GetWindowHandle());

            StorageFile backupFile = await savePicker.PickSaveFileAsync();
            if (backupFile == null) return;

            // Show progress dialog
            var progressDialog = new ContentDialog {
                Title = "Exporting Backup",
                Content = new ProgressBar {
                    IsIndeterminate = true
                },
                XamlRoot = _parentWindow.Content.XamlRoot
            };

            try {
                // Show progress dialog
                var progressTask = progressDialog.ShowAsync();

                // Paths
                string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string configPath = Path.Combine(localAppDataPath, "AppGroup", "appgroups.json");
                string groupsPath = Path.Combine(AppContext.BaseDirectory, "Groups");
                string tempZipPath = Path.Combine(Path.GetTempPath(), $"AppGroup_Backup_{Guid.NewGuid()}.zip");

                // Create temporary zip file
                using (var archive = ZipFile.Open(tempZipPath, ZipArchiveMode.Create)) {
                    // Add config file
                    if (File.Exists(configPath)) {
                        archive.CreateEntryFromFile(configPath, "appgroups.json");
                    }

                    // Add Groups directory if it exists
                    if (Directory.Exists(groupsPath)) {
                        AddDirectoryToZip(archive, groupsPath, "Groups");
                    }
                }

                // Copy temp zip to user-selected location
                File.Copy(tempZipPath, backupFile.Path, true);

                // Clean up temp file
                File.Delete(tempZipPath);

                // Close progress dialog
                progressDialog.Hide();

                // Show success message
                await ShowMessageDialogAsync("Backup Successful", "Your AppGroup configuration has been exported.");
            }
            catch (Exception ex) {
                // Close progress dialog
                progressDialog.Hide();

                // Show error message
                await ShowMessageDialogAsync("Export Failed", $"An error occurred: {ex.Message}");
                Debug.WriteLine($"Backup export error: {ex}");
            }
        }
        public async Task ImportBackupAsync() {
            var filePicker = new FileOpenPicker {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            filePicker.FileTypeFilter.Add(".agz");

            // Initialize COM for WinRT file picker
            InitializeWithWindow.Initialize(filePicker, _parentWindow.GetWindowHandle());

            StorageFile backupFile = await filePicker.PickSingleFileAsync();
            if (backupFile == null) return;

            // Show progress dialog
            var progressDialog = new ContentDialog {
                Title = "Importing Backup",
                Content = new ProgressBar {
                    IsIndeterminate = true
                },
                XamlRoot = _parentWindow.Content.XamlRoot
            };

            // Flag to track if we've stopped the background process
            bool backgroundProcessStopped = false;

            try {
                // Show progress dialog
                var progressTask = progressDialog.ShowAsync();

                // Paths
                string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appGroupLocalPath = Path.Combine(localAppDataPath, "AppGroup");
                string configPath = Path.Combine(appGroupLocalPath, "appgroups.json");
                string groupsPath = Path.Combine(AppContext.BaseDirectory, "Groups");
                string iconsPath = Path.Combine(appGroupLocalPath, "Icons");

                // Temporary variables to store configuration and path validation
                Dictionary<string, GroupConfig> config = null;
                var groupsWithInvalidPaths = new Dictionary<string, List<string>>();

                // First, examine the zip file contents without extracting
                using (var archive = ZipFile.OpenRead(backupFile.Path)) {
                    // Find the configuration file entry
                    var configEntry = archive.Entries.FirstOrDefault(e => e.FullName == "appgroups.json");

                    if (configEntry != null) {
                        // Read the configuration from the zip entry
                        using (var stream = configEntry.Open())
                        using (var reader = new StreamReader(stream)) {
                            string configContent = await reader.ReadToEndAsync();

                            var serializerOptions = new System.Text.Json.JsonSerializerOptions {
                                PropertyNameCaseInsensitive = true
                            };
                            config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, GroupConfig>>(configContent, serializerOptions);

                            // Null check after deserialization
                            if (config == null) {
                                progressDialog.Hide();
                                await ShowMessageDialogAsync("Import Error", "Configuration file is empty or invalid.");
                                return;
                            }

                            // Check for invalid paths BEFORE proceeding with extraction
                            foreach (var groupKey in config.Keys) {
                                var group = config[groupKey];

                                // Check paths in the configuration
                                if (group.path == null) {
                                    group.path = new Dictionary<string, PathConfig>();
                                    continue;
                                }

                                var pathsToRemove = group.path
                                    .Where(path => path.Key != null && !File.Exists(path.Key))
                                    .Select(path => path.Key)
                                    .ToList();

                                foreach (var invalidPath in pathsToRemove) {
                                    // Record the invalid path for this group
                                    if (!groupsWithInvalidPaths.ContainsKey(group.groupName)) {
                                        groupsWithInvalidPaths[group.groupName] = new List<string>();
                                    }
                                    groupsWithInvalidPaths[group.groupName].Add(invalidPath);
                                }
                            }
                        }
                    }
                    else {
                        progressDialog.Hide();
                        await ShowMessageDialogAsync("Import Error", "No configuration file found in the backup.");
                        return;
                    }

                    // Hide progress dialog while showing the invalid paths dialog
                    progressDialog.Hide();

                    // If there are no invalid paths, stop the background process immediately
                    if (!groupsWithInvalidPaths.Any()) {
                        await StopBackgroundProcessesAsync();
                        backgroundProcessStopped = true;
                        Debug.WriteLine("Background processes stopped before import (no invalid paths)");

                        // Show progress dialog again
                        progressTask = progressDialog.ShowAsync();
                    }

                    // If invalid paths exist, ask user how to proceed BEFORE extraction
                    if (groupsWithInvalidPaths.Any()) {
                        var invalidPathsMessage = "The following invalid paths were detected in the backup:\n\n";
                        foreach (var groupEntry in groupsWithInvalidPaths) {
                            invalidPathsMessage += $"{groupEntry.Key}:\n";
                            foreach (var path in groupEntry.Value) {
                                invalidPathsMessage += $"- {path}\n";
                            }
                            invalidPathsMessage += "\n";
                        }
                        invalidPathsMessage += "Do you want to continue importing and remove these paths?";

                        var confirmDialog = new ContentDialog {
                            Title = "Invalid Paths Detected",
                            Content = new ScrollViewer {
                                MaxHeight = 400, // Limit height for better UI
                                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                                Content = new TextBlock {
                                    Text = invalidPathsMessage,
                                    TextWrapping = TextWrapping.Wrap
                                }
                            },
                            PrimaryButtonText = "Continue",
                            CloseButtonText = "Cancel",
                            DefaultButton = ContentDialogButton.Primary,
                            XamlRoot = _parentWindow.Content.XamlRoot
                        };

                        var result = await confirmDialog.ShowAsync();

                        // If user chooses to cancel, return without proceeding
                        if (result != ContentDialogResult.Primary) {
                            Debug.WriteLine("User canceled import due to invalid paths");
                            return;
                        }
                        else {
                            Debug.WriteLine("User chose to continue import despite invalid paths");

                            // Stop background process when user confirms to continue
                            await StopBackgroundProcessesAsync();
                            backgroundProcessStopped = true;
                            Debug.WriteLine("Background processes stopped after user confirmed import");

                            // Now remove the invalid paths from the configuration
                            foreach (var groupKey in config.Keys) {
                                var group = config[groupKey];
                                if (group.path != null && groupsWithInvalidPaths.ContainsKey(group.groupName)) {
                                    foreach (var invalidPath in groupsWithInvalidPaths[group.groupName]) {
                                        group.path.Remove(invalidPath);
                                    }
                                }
                            }
                        }

                        // Show progress dialog again for extraction
                        progressTask = progressDialog.ShowAsync();
                    }

                    // Ensure directories exist
                    Directory.CreateDirectory(appGroupLocalPath);

                    // Clean up the Groups directory - remove all existing folders to ensure clean import
                    if (Directory.Exists(groupsPath)) {
                        try {
                            // Get all directories inside the Groups folder
                            string[] groupDirectories = Directory.GetDirectories(groupsPath);

                            // Delete each directory
                            foreach (string directory in groupDirectories) {
                                Directory.Delete(directory, true); // true means recursive delete
                            }
                            Debug.WriteLine("Removed all existing group folders before import");
                        }
                        catch (Exception ex) {
                            Debug.WriteLine($"Error cleaning up Groups directory: {ex.Message}");
                            // Continue with import even if cleanup fails
                        }
                    }

                    // Ensure Groups directory exists
                    Directory.CreateDirectory(groupsPath);

                    // Handle Icons folder more carefully
                    string tempIconsPath = Path.Combine(Path.GetTempPath(), $"AppGroup_Icons_{Guid.NewGuid()}");
                    try {
                        if (Directory.Exists(iconsPath)) {
                            // Create a temporary backup of the current icons
                            Directory.CreateDirectory(tempIconsPath);
                            foreach (string file in Directory.GetFiles(iconsPath)) {
                                string destFile = Path.Combine(tempIconsPath, Path.GetFileName(file));
                                File.Copy(file, destFile, true);
                            }
                        }
                    }
                    catch (Exception ex) {
                        Debug.WriteLine($"Could not backup Icons folder: {ex.Message}");
                        // Continue with import even if we can't backup the Icons folder
                    }

                    // Ensure Icons directory exists
                    Directory.CreateDirectory(iconsPath);

                    // Extract all entries
                    foreach (var entry in archive.Entries) {
                        try {
                            // Skip directory entries
                            if (string.IsNullOrEmpty(entry.Name))
                                continue;

                            // Skip .lnk files - we'll recreate them later
                            if (entry.Name.EndsWith(".lnk"))
                                continue;

                            // Determine destination based on entry name
                            string destinationPath;
                            if (entry.FullName == "appgroups.json") {
                                // Config file goes to local app data
                                destinationPath = configPath;
                            }
                            else if (entry.FullName.StartsWith("Groups/")) {
                                // Groups files go to executable directory
                                destinationPath = Path.Combine(AppContext.BaseDirectory, entry.FullName);

                                // Ensure directory exists for this entry
                                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                            }
                            else if (entry.FullName.StartsWith("Icons/")) {
                                // Icons files go to the icons directory
                                destinationPath = Path.Combine(iconsPath, Path.GetFileName(entry.FullName));

                                // Check if the file already exists in the temp backup
                                string tempIconPath = Path.Combine(tempIconsPath, Path.GetFileName(entry.FullName));
                                if (File.Exists(tempIconPath)) {
                                    // Skip extracting if the icon already exists in the backup
                                    continue;
                                }
                            }
                            else {
                                // Unexpected entry, skip
                                continue;
                            }

                            // Ensure parent directory exists for the file
                            string parentDir = Path.GetDirectoryName(destinationPath);
                            if (!string.IsNullOrEmpty(parentDir)) {
                                Directory.CreateDirectory(parentDir);
                            }

                            // Extract file with overwrite
                            entry.ExtractToFile(destinationPath, true);
                        }
                        catch (Exception ex) {
                            Debug.WriteLine($"Error extracting {entry.FullName}: {ex.Message}");
                            // Continue with next entry
                        }
                    }

                    // Restore any icons from the temporary backup that weren't in the archive
                    try {
                        if (Directory.Exists(tempIconsPath)) {
                            foreach (string file in Directory.GetFiles(tempIconsPath)) {
                                string destFile = Path.Combine(iconsPath, Path.GetFileName(file));
                                if (!File.Exists(destFile)) {
                                    File.Copy(file, destFile, true);
                                }
                            }
                            Directory.Delete(tempIconsPath, true);
                        }
                    }
                    catch (Exception ex) {
                        Debug.WriteLine($"Error restoring icons from backup: {ex.Message}");
                    }

                    // Now process the configuration and create shortcuts safely
                    if (config != null) {
                        try {
                            foreach (var groupKey in config.Keys) {
                                var group = config[groupKey];
                                string groupName = group.groupName;

                                // Skip groups with no name
                                if (string.IsNullOrEmpty(groupName))
                                    continue;

                                // Create the group directory if it doesn't exist
                                string groupDirPath = Path.Combine(groupsPath, groupName);
                                Directory.CreateDirectory(groupDirPath);

                                // Update icon path to use the current base directory
                                if (!string.IsNullOrEmpty(group.groupIcon)) {
                                    // Extract filename from the old path
                                    string iconFileName = Path.GetFileName(group.groupIcon);

                                    // Check if the icon file exists in the new location
                                    string newIconDir = Path.Combine(groupsPath, groupName, groupName);
                                    string newIconPath = Path.Combine(newIconDir, iconFileName);

                                    if (File.Exists(newIconPath)) {
                                        // Update the group icon path in the config
                                        group.groupIcon = newIconPath;

                                        // Create the shortcut with the new paths
                                        string shortcutPath = Path.Combine(groupDirPath, $"{groupName}.lnk");

                                        try {
                                            SafeCreateShortcut(shortcutPath, groupName, newIconPath);
                                            Debug.WriteLine($"Created shortcut for {groupName} at {shortcutPath}");
                                        }
                                        catch (Exception ex) {
                                            Debug.WriteLine($"Error creating shortcut for {groupName}: {ex.Message}");
                                        }
                                    }
                                    else {
                                        Debug.WriteLine($"Icon file not found: {newIconPath}");
                                    }
                                }
                            }

                            // Update the configuration file after path validation and removal
                            string updatedConfigContent = System.Text.Json.JsonSerializer.Serialize(config,
                                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                            await File.WriteAllTextAsync(configPath, updatedConfigContent);
                        }
                        catch (Exception ex) {
                            Debug.WriteLine($"Error processing configuration: {ex.Message}");
                        }
                    }

                    // Close progress dialog
                    progressDialog.Hide();

                    // Create a scrollable dialog for the success message
                    var successDialog = new ContentDialog {
                        Title = "Import Successful",
                        XamlRoot = _parentWindow.Content.XamlRoot,
                        PrimaryButtonText = "OK"
                    };

                    // Create a ScrollViewer to contain the message
                    var scrollViewer = new ScrollViewer {
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                        Padding = new Thickness(10)
                    };

                    // Create a TextBlock to display the message
                    var messageTextBlock = new TextBlock {
                        TextWrapping = TextWrapping.Wrap,
                        FontFamily = new FontFamily("Segoe UI")
                    };

                    // Build the success message
                    string message = $"Backup imported successfully.\n";

                    if (groupsWithInvalidPaths.Any()) {
                        message += $"\n\nRemoved {groupsWithInvalidPaths.Sum(g => g.Value.Count)} invalid paths:";
                        foreach (var groupEntry in groupsWithInvalidPaths) {
                            message += $"\n{groupEntry.Key}\n";
                            foreach (var path in groupEntry.Value) {
                                message += $"- {path}\n";
                            }
                        }
                    }

                    messageTextBlock.Text = message;

                    // Set the ScrollViewer content
                    scrollViewer.Content = messageTextBlock;

                    // Set the dialog content
                    successDialog.Content = scrollViewer;

                    try {
                        if (_parentWindow is MainWindow mainWindow) {
                            await mainWindow.UpdateGroupItemAsync(JsonConfigHelper.GetDefaultConfigPath());
                            await mainWindow.LoadGroupsAsync();
                            Debug.WriteLine("Groups reloaded after import");
                        }
                    }
                    catch (Exception ex) {
                        Debug.WriteLine($"Error reloading groups after import: {ex.Message}");
                    }

                    // Start the background process again before showing the success dialog
                    await StartBackgroundProcessAsync();
                    backgroundProcessStopped = false;
                    Debug.WriteLine("Background process started after import completed");

                    // Show the dialog
                    await successDialog.ShowAsync();
                }
            }
            catch (Exception ex) {
                // Close progress dialog
                progressDialog.Hide();

                // Show error message with detailed exception information
                await ShowMessageDialogAsync("Import Failed",
                    $"An error occurred: {ex.Message}\n\nDetails: {ex.GetType().Name}\n{ex.StackTrace}");
                Debug.WriteLine($"Backup import error: {ex}");
            }
            finally {
                // Make sure we restart the background process if it was stopped and not already restarted
                if (backgroundProcessStopped) {
                    await StartBackgroundProcessAsync();
                    Debug.WriteLine("Background process started in finally block");
                }
            }
        }

        // Helper method to stop all other AppGroup.exe instances and all AppGroupBackground.exe instances
        private async Task StopBackgroundProcessesAsync() {
            try {
                // Get the current process ID
                int currentProcessId = Process.GetCurrentProcess().Id;

                // Kill all other AppGroup.exe instances (except the current one)
                var appGroupProcesses = Process.GetProcessesByName("AppGroup");
                Debug.WriteLine($"Found {appGroupProcesses.Length} AppGroup.exe processes");

                foreach (var process in appGroupProcesses) {
                    // Skip the current process
                    if (process.Id == currentProcessId) {
                        Debug.WriteLine($"Skipping current AppGroup.exe process with ID {process.Id}");
                        continue;
                    }

                    // Kill the process
                    process.Kill();
                    Debug.WriteLine($"Stopped AppGroup.exe process with ID {process.Id}");
                }

                // Kill all AppGroupBackground.exe instances
                var backgroundProcesses = Process.GetProcessesByName("AppGroupBackground");
                Debug.WriteLine($"Found {backgroundProcesses.Length} AppGroupBackground.exe processes");

                foreach (var process in backgroundProcesses) {
                    process.Kill();
                    Debug.WriteLine($"Stopped AppGroupBackground.exe process with ID {process.Id}");
                }

                // Small delay to ensure processes are completely terminated
                await Task.Delay(500);
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error stopping processes: {ex.Message}");
            }
        }

        // Helper method to start only the AppGroupBackground process
        private async Task StartBackgroundProcessAsync() {
            try {
                // Get the path to the AppGroupBackground.exe
                string backgroundExePath = Path.Combine(AppContext.BaseDirectory, "AppGroupBackground.exe");

                // Check if the file exists
                if (File.Exists(backgroundExePath)) {
                    // Start the process
                    Process.Start(backgroundExePath);
                    Debug.WriteLine("Started AppGroupBackground.exe process");

                    // Small delay to ensure process is fully started
                    await Task.Delay(500);
                }
                else {
                    Debug.WriteLine("AppGroupBackground.exe not found at: " + backgroundExePath);
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error starting background process: {ex.Message}");
            }
        }
        // Safer method to create shortcuts with error handling
        private void SafeCreateShortcut(string shortcutPath, string groupName, string iconPath) {
            try {
                string baseDir = AppContext.BaseDirectory;

                // Delete the old shortcut if it exists
                if (File.Exists(shortcutPath)) {
                    File.Delete(shortcutPath);
                }

                // Create a new shortcut using the IWshShortcut COM object
                dynamic wshShell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                dynamic shortcut = wshShell.CreateShortcut(shortcutPath);

                // Set the properties of the shortcut
                shortcut.TargetPath = Path.Combine(baseDir, "AppGroup.exe");
                shortcut.Arguments = $"\"{groupName}\"";
                shortcut.Description = $"{groupName} - AppGroup Shortcut";
                shortcut.IconLocation = iconPath;

                // Set working directory to the base directory
                shortcut.WorkingDirectory = baseDir;

                // Save the shortcut
                shortcut.Save();

                Debug.WriteLine($"Shortcut created successfully at {shortcutPath}");
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error creating shortcut: {ex.Message}");
                // Don't throw the exception - just log it and continue
            }
        }

        private void AddDirectoryToZip(ZipArchive archive, string sourceDirectoryPath, string entryPrefix) {
            // Get all files in the directory
            string[] files = Directory.GetFiles(sourceDirectoryPath, "*", SearchOption.AllDirectories);

            foreach (string filePath in files) {
                // Calculate relative path
                string relativePath = Path.GetRelativePath(sourceDirectoryPath, filePath);
                string zipEntryName = Path.Combine(entryPrefix, relativePath).Replace('\\', '/');

                // Create zip entry and write file content
                archive.CreateEntryFromFile(filePath, zipEntryName);
            }
        }

        private async Task ShowMessageDialogAsync(string title, string content) {
            var messageDialog = new ContentDialog {
                Title = title,
                Content = content,
                CloseButtonText = "OK",
                XamlRoot = _parentWindow.Content.XamlRoot
            };

            await messageDialog.ShowAsync();
        }

        // Helper class to deserialize configuration
        public class GroupConfig {
            public string groupName { get; set; }
            public bool groupHeader { get; set; }
            public int groupCol { get; set; }
            public string groupIcon { get; set; }
            public Dictionary<string, PathConfig> path { get; set; }
        }

        public class PathConfig {
            public string tooltip { get; set; }
            public string args { get; set; }
        }
    }
}