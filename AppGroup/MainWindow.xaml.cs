
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using WinRT.Interop;
using WinUIEx;

namespace AppGroup {
    public class GroupItem : INotifyPropertyChanged {
        public int GroupId { get; set; }
        private string groupName;
        public string GroupName {
            get => groupName;
            set {
                if (groupName != value) {
                    groupName = value;
                    OnPropertyChanged(nameof(GroupName));
                }
            }
        }
        private string groupIcon;
        public string GroupIcon {
            get => groupIcon;
            set {
                if (groupIcon != value) {
                    groupIcon = value;
                    OnPropertyChanged(nameof(GroupIcon));
                }
            }
        }
        private List<string> pathIcons;
        public List<string> PathIcons {
            get => pathIcons;
            set {
                if (pathIcons != value) {
                    pathIcons = value;
                    OnPropertyChanged(nameof(PathIcons));
                }
            }
        }

        public string AdditionalIconsText {
            get {
                return AdditionalIconsCount > 0 ? $"+{AdditionalIconsCount}" : string.Empty;
            }
        }
        private int additionalIconsCount;

        public int AdditionalIconsCount {
            get => additionalIconsCount;
            set {
                if (additionalIconsCount != value) {
                    additionalIconsCount = value;
                    OnPropertyChanged(nameof(AdditionalIconsCount));
                    OnPropertyChanged(nameof(AdditionalIconsText));
                }
            }
        }

        // Updated properties for Tooltip, Args, and Custom Icons
        public Dictionary<string, string> Tooltips { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> Args { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> CustomIcons { get; set; } = new Dictionary<string, string>(); // New property

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


    public sealed partial class MainWindow : WinUIEx.WindowEx {
        // Private fields
        private readonly Dictionary<int, EditGroupWindow> _openEditWindows = new Dictionary<int, EditGroupWindow>();
        private BackupHelper _backupHelper;
        private ObservableCollection<GroupItem> GroupItems;
        private FileSystemWatcher _fileWatcher;
        private readonly object _loadLock = new object();
        private bool _isLoading = false;
        private string tempIcon;
        private readonly IconHelper _iconHelper;
        private DispatcherTimer debounceTimer;
        private SupportDialogHelper _supportDialogHelper;
        public MainWindow() {
            InitializeComponent();

            _backupHelper = new BackupHelper(this);

            GroupItems = new ObservableCollection<GroupItem>();
            GroupListView.ItemsSource = GroupItems;
            _iconHelper = new IconHelper();

            this.CenterOnScreen();
            this.MinHeight = 600;
            this.MinWidth = 530;

            this.ExtendsContentIntoTitleBar = true;
            var iconPath = Path.Combine(AppContext.BaseDirectory, "AppGroup.ico");

            this.AppWindow.SetIcon(iconPath);

            _ = LoadGroupsAsync();

            SetupFileWatcher();

            ThemeHelper.UpdateTitleBarColors(this);
            debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            debounceTimer.Tick += FilterGroups;

            _supportDialogHelper = new SupportDialogHelper(this);
            NativeMethods.SetCurrentProcessExplicitAppUserModelID("AppGroup.Main");
            // Load on activation
            //this.Activated += Window_Activated;
            this.AppWindow.Closing += AppWindow_Closing;
            SetWindowIcon();

            // Check for updates on startup if enabled
            _ = CheckForUpdatesOnStartupAsync();
        }

        private async Task CheckForUpdatesOnStartupAsync() {
            try {
                // Wait for window to be fully loaded and settings to be available
                await Task.Delay(2000);

                // Load settings properly (not just get cached which may be null)
                var settings = await SettingsHelper.LoadSettingsAsync();
                if (!settings.CheckForUpdatesOnStartup) {
                    return;
                }

                var updateInfo = await UpdateChecker.CheckForUpdatesAsync();

                if (updateInfo.UpdateAvailable && this.Content?.XamlRoot != null) {
                    // Show update notification
                    await ShowUpdateDialogAsync(updateInfo);
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error checking for updates on startup: {ex.Message}");
            }
        }

        private async Task ShowUpdateDialogAsync(UpdateChecker.UpdateInfo updateInfo) {
            try {
                if (this.Content?.XamlRoot == null) {
                    return;
                }

                var dialog = new ContentDialog {
                    Title = "Update Available",
                    Content = $"A new version of AppGroup is available!\n\n" +
                              $"Current version: {updateInfo.CurrentVersion}\n" +
                              $"Latest version: {updateInfo.LatestVersion}\n\n" +
                              "Would you like to download the update?",
                    PrimaryButtonText = "Download",
                    CloseButtonText = "Later",
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary) {
                    UpdateChecker.OpenReleasesPage(updateInfo.ReleaseUrl);
                }
            }
            catch (Exception ex) {
                // Dialog may fail if another dialog is already open - this is expected
                Debug.WriteLine($"Error showing update dialog: {ex.Message}");
            }
        }


        private void SetWindowIcon() {
            try {
                // Get the window handle
                IntPtr hWnd = WindowNative.GetWindowHandle(this);

                // Try to load icon from embedded resource first
                var iconPath = Path.Combine(AppContext.BaseDirectory, "AppGroup.ico");

                if (File.Exists(iconPath)) {
                    // Load and set the icon using Win32 APIs
                    IntPtr hIcon = NativeMethods.LoadIcon(iconPath);
                    if (hIcon != IntPtr.Zero) {
                        NativeMethods.SendMessage(hWnd, NativeMethods.WM_SETICON, NativeMethods.ICON_SMALL, hIcon);
                        NativeMethods.SendMessage(hWnd, NativeMethods.WM_SETICON, NativeMethods.ICON_BIG, hIcon);
                    }
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to set window icon: {ex.Message}");
            }
        }

        private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args) {
            args.Cancel = true;
            try {
                var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(this.Content.XamlRoot);
                foreach (var popup in popups) {
                    if (popup.Child is ContentDialog dialog) {
                        dialog.Hide();
                    }
                }
            }
            catch {
                // Fallback - some dialogs might not be in popups
            }
            this.Hide();        // Just hide the window
        }
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            debounceTimer.Stop();
            debounceTimer.Start();
        }

        private void FilterGroups(object sender, object e) {
            debounceTimer.Stop();
            string searchQuery = SearchTextBox.Text.ToLower();
            var filteredGroups = GroupItems.Where(group => group.GroupName.ToLower().Contains(searchQuery)).ToList();
            GroupListView.ItemsSource = filteredGroups.Count > 0 ? filteredGroups : GroupItems;
            GroupsCount.Text = GroupListView.Items.Count > 1
                                          ? GroupListView.Items.Count.ToString() + " Groups"
                                          : GroupListView.Items.Count == 1
                                          ? "1 Group"
                                          : "";
        }




        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        public async Task UpdateGroupItemAsync(string jsonFilePath) {
            await _semaphore.WaitAsync();
            try {
                string jsonContent = await File.ReadAllTextAsync(jsonFilePath);
                JsonNode jsonObject = JsonNode.Parse(jsonContent ?? "{}") ?? new JsonObject();
                var groupDictionary = jsonObject.AsObject();

                var tasks = groupDictionary.Select(async property => {
                    if (int.TryParse(property.Key, out int groupId)) {
                        var existingItem = GroupItems.FirstOrDefault(item => item.GroupId == groupId);
                        if (existingItem != null) {
                            string newGroupName = property.Value?["groupName"]?.GetValue<string>();
                            string newGroupIcon = property.Value?["groupIcon"]?.GetValue<string>();

                            existingItem.GroupName = newGroupName;
                            existingItem.GroupIcon = null;
                            existingItem.GroupIcon = IconHelper.FindOrigIcon(newGroupIcon);

                            var paths = property.Value?["path"]?.AsObject();
                            if (paths?.Count > 0) {
                                var iconTasks = paths
                                    .Where(p => p.Value != null)
                                    .Select(async path => {
                                        string filePath = path.Key;
                                        string tooltip = path.Value["tooltip"]?.GetValue<string>();
                                        string args = path.Value["args"]?.GetValue<string>();
                                        string customIcon = path.Value["icon"]?.GetValue<string>(); // Get custom icon

                                        existingItem.Tooltips[filePath] = tooltip;
                                        existingItem.Args[filePath] = args;
                                        existingItem.CustomIcons[filePath] = customIcon; // Store custom icon

                                        // Use custom icon if available, otherwise get from IconCache
                                        if (!string.IsNullOrEmpty(customIcon) && File.Exists(customIcon)) {
                                            return customIcon;
                                        }
                                        else {
                                            return await IconCache.GetIconPathAsync(filePath);
                                        }
                                    })
                                    .ToList();

                                var iconPaths = await Task.WhenAll(iconTasks);
                                var validIconPaths = iconPaths.Where(p => !string.IsNullOrEmpty(p)).ToList();

                                // Limit to 7 icons
                                int maxIconsToShow = 7;
                                existingItem.PathIcons = validIconPaths.Take(maxIconsToShow).ToList();
                                existingItem.AdditionalIconsCount = Math.Max(0, validIconPaths.Count - maxIconsToShow);
                            }
                        }
                        else {
                            var newItem = await CreateGroupItemAsync(groupId, property.Value);
                            GroupItems.Add(newItem);
                        }
                    }
                }).ToList();

                await Task.WhenAll(tasks);

                // Update UI elements outside the loop to avoid redundant updates
                GroupsCount.Text = GroupListView.Items.Count > 1
                                    ? GroupListView.Items.Count.ToString() + " Groups"
                                    : GroupListView.Items.Count == 1
                                    ? "1 Group"
                                    : "";
                EmptyView.Visibility = GroupListView.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            finally {
                _semaphore.Release();
            }
        }
        private bool _isReordering = false;

        // Add this field to your MainWindow class
        private readonly Dictionary<int, string> _tempDragFiles = new Dictionary<int, string>();

        private async void GroupListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e) {
            // Set flag to prevent file watcher from interfering during reorder
            _isReordering = true;

            // Store the dragged item for reference
            if (e.Items.Count > 0 && e.Items[0] is GroupItem draggedItem) {
                e.Data.Properties.Add("DraggedGroupId", draggedItem.GroupId);

                // Always set basic data for internal reordering
                e.Data.RequestedOperation = DataPackageOperation.Copy | DataPackageOperation.Move | DataPackageOperation.Link;

                string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appDataPath = Path.Combine(localAppDataPath, "AppGroup");
                // Prepare file data for external drops
                //string shortcutPath = Path.Combine("Groups", draggedItem.GroupName, $"{draggedItem.GroupName}.lnk");
                //string fullShortcutPath = Path.GetFullPath(shortcutPath);
                string shortcutPath = Path.Combine(appDataPath, "Groups", draggedItem.GroupName, $"{draggedItem.GroupName}.lnk");
                string fullShortcutPath = Path.GetFullPath(shortcutPath);
                if (File.Exists(fullShortcutPath)) {
                    try {
                        // Copy to temp location
                        string tempDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppGroup", "DragTemp");
                        Directory.CreateDirectory(tempDir);
                        string tempShortcutPath = Path.Combine(tempDir, $"{draggedItem.GroupName}.lnk");

                        if (File.Exists(tempShortcutPath)) {
                            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                            tempShortcutPath = Path.Combine(tempDir, $"{draggedItem.GroupName}_{timestamp}.lnk");
                        }

                        File.Copy(fullShortcutPath, tempShortcutPath, true);
                        _tempDragFiles[draggedItem.GroupId] = tempShortcutPath;

                        // Set text data immediately (this won't break reordering)
                        e.Data.SetText(fullShortcutPath);

                        // Use SetDataProvider for StorageItems - only provides data when external target requests it
                        e.Data.SetDataProvider(StandardDataFormats.StorageItems, async (request) => {
                            var deferral = request.GetDeferral();
                            try {
                                var tempFolder = await StorageFolder.GetFolderFromPathAsync(tempDir);
                                var tempFile = await tempFolder.GetFileAsync(Path.GetFileName(tempShortcutPath));
                                request.SetData(new List<IStorageItem> { tempFile });
                                System.Diagnostics.Debug.WriteLine($"Provided storage items for external drop: {tempShortcutPath}");
                            }
                            catch (Exception ex) {
                                System.Diagnostics.Debug.WriteLine($"Error providing storage items: {ex.Message}");
                            }
                            finally {
                                deferral.Complete();
                            }
                        });

                        System.Diagnostics.Debug.WriteLine($"Prepared conditional drag data for: {tempShortcutPath}");
                    }
                    catch (Exception ex) {
                        System.Diagnostics.Debug.WriteLine($"Error preparing drag data: {ex.Message}");
                    }
                }
            }
        }

        private async void GroupListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) {
            try {
                // Clean up temp files for all dragged items
                foreach (var item in args.Items) {
                    if (item is GroupItem groupItem && _tempDragFiles.ContainsKey(groupItem.GroupId)) {
                        string tempFilePath = _tempDragFiles[groupItem.GroupId];
                        if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath)) {
                            try {
                                File.Delete(tempFilePath);
                                System.Diagnostics.Debug.WriteLine($"Cleaned up temp file: {tempFilePath}");
                            }
                            catch (Exception cleanupEx) {
                                System.Diagnostics.Debug.WriteLine($"Error cleaning up temp file: {cleanupEx.Message}");
                            }
                        }
                        _tempDragFiles.Remove(groupItem.GroupId);
                    }
                }

                // Reset the reordering flag
                _isReordering = false;

                if (args.DropResult == Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move) {
                    // Get the current order of items in the ListView
                    var reorderedItems = new List<GroupItem>();
                    for (int i = 0; i < GroupListView.Items.Count; i++) {
                        if (GroupListView.Items[i] is GroupItem item) {
                            reorderedItems.Add(item);
                        }
                    }

                    // Update the JSON file with the new order
                    await UpdateJsonWithNewOrderAsync(reorderedItems);
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error during drag completion: {ex.Message}");
                // Reload groups to restore correct state
                _ = LoadGroupsAsync();
            }
        }

        private async Task UpdateJsonWithNewOrderAsync(List<GroupItem> reorderedItems) {
            try {
                string jsonFilePath = JsonConfigHelper.GetDefaultConfigPath();

                // Temporarily disable file watcher to prevent recursive updates
                _fileWatcher.EnableRaisingEvents = false;

                // Read the current JSON content
                string jsonContent = await File.ReadAllTextAsync(jsonFilePath);
                JsonNode jsonObject = JsonNode.Parse(jsonContent ?? "{}") ?? new JsonObject();
                var groupDictionary = jsonObject.AsObject();

                // Create a new ordered JSON object
                var newJsonObject = new JsonObject();

                // Create a mapping of new sequential IDs to preserve the order
                var orderMapping = new Dictionary<int, int>();
                for (int i = 0; i < reorderedItems.Count; i++) {
                    int newId = i + 1; // Start from 1
                    int oldId = reorderedItems[i].GroupId;
                    orderMapping[oldId] = newId;
                }

                // Rebuild the JSON with new order and IDs
                for (int i = 0; i < reorderedItems.Count; i++) {
                    var item = reorderedItems[i];
                    int newId = i + 1;
                    string oldKey = item.GroupId.ToString();
                    string newKey = newId.ToString();

                    if (groupDictionary.ContainsKey(oldKey)) {
                        var groupData = groupDictionary[oldKey];
                        newJsonObject[newKey] = groupData?.DeepClone();
                    }
                }

                // Write the updated JSON back to file
                string updatedJsonContent = newJsonObject.ToJsonString(new JsonSerializerOptions {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(jsonFilePath, updatedJsonContent);

                // Update the GroupId properties in the ObservableCollection to match new IDs
                for (int i = 0; i < reorderedItems.Count; i++) {
                    reorderedItems[i].GroupId = i + 1;
                }

                // Small delay to ensure file write completes
                await Task.Delay(100);

                // Re-enable file watcher
                _fileWatcher.EnableRaisingEvents = true;

                Debug.WriteLine("JSON file updated with new group order");
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error updating JSON with new order: {ex.Message}");
                // Re-enable file watcher in case of error
                _fileWatcher.EnableRaisingEvents = true;
                throw;
            }
        }

        //private void SetupFileWatcher() {
        //    string jsonFilePath = JsonConfigHelper.GetDefaultConfigPath();
        //    string directoryPath = Path.GetDirectoryName(jsonFilePath);
        //    string fileName = Path.GetFileName(jsonFilePath);

        //    _fileWatcher = new FileSystemWatcher(directoryPath, fileName) {
        //        NotifyFilter = NotifyFilters.LastWrite
        //    };

        //    _fileWatcher.Changed +=  (s, e) =>
        //    {
        //         DispatcherQueue.TryEnqueue(async () =>
        //        {
        //            if (!IsFileInUse(jsonFilePath)) {
        //                await UpdateGroupItemAsync(jsonFilePath);
        //            }
        //        });
        //    };

        //    _fileWatcher.EnableRaisingEvents = true;
        //}


        // Modify your existing SetupFileWatcher method to handle reordering
        private void SetupFileWatcher() {
            string jsonFilePath = JsonConfigHelper.GetDefaultConfigPath();
            string directoryPath = Path.GetDirectoryName(jsonFilePath);
            string fileName = Path.GetFileName(jsonFilePath);

            _fileWatcher = new FileSystemWatcher(directoryPath, fileName) {
                NotifyFilter = NotifyFilters.LastWrite
            };

            _fileWatcher.Changed += (s, e) =>
            {
                // Skip file watcher updates during reordering to prevent conflicts
                if (!_isReordering) {
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        if (!IsFileInUse(jsonFilePath)) {
                            await UpdateGroupItemAsync(jsonFilePath);
                        }
                    });
                }
            };

            _fileWatcher.EnableRaisingEvents = true;
        }

        // Optional: Add a method to manually save the current order
        private async void SaveCurrentOrder() {
            var currentItems = GroupItems.ToList();
            await UpdateJsonWithNewOrderAsync(currentItems);
        }


        private bool IsFileInUse(string filePath) {
            try {
                using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
                    fs.Close();
                }
                return false;
            }
            catch (IOException) {
                return true;
            }
        }

        private async void Reload(object sender, RoutedEventArgs e) {
            _ = LoadGroupsAsync();


        }


        private readonly SemaphoreSlim _loadingSemaphore = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _loadCancellationSource = new CancellationTokenSource();

      


        private async Task<List<GroupItem>> ProcessGroupsInParallelAsync(
            JsonObject groupDictionary,
            CancellationToken cancellationToken) {
            var options = new ParallelOptions {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            };

            var newGroupItems = new ConcurrentBag<GroupItem>();

            await Parallel.ForEachAsync(
                groupDictionary,
                options,
                async (property, token) => {
                    if (int.TryParse(property.Key, out int groupId)) {
                        try {
                            var groupItem = await CreateGroupItemAsync(groupId, property.Value);
                            newGroupItems.Add(groupItem);
                        }
                        catch (Exception ex) {
                            Debug.WriteLine($"Error processing group {groupId}: {ex.Message}");
                        }
                    }
                });

            return newGroupItems
                 .OrderBy(g => g.GroupId)
                .ToList();
        }

        private async Task<List<GroupItem>> ProcessGroupsSequentiallyAsync(
            JsonObject groupDictionary,
            CancellationToken cancellationToken) {
            var newGroupItems = new List<GroupItem>();

            foreach (var property in groupDictionary) {
                cancellationToken.ThrowIfCancellationRequested();

                if (int.TryParse(property.Key, out int groupId)) {
                    try {
                        var groupItem = await CreateGroupItemAsync(groupId, property.Value);

                        newGroupItems.Add(groupItem);
                    }
                    catch (Exception ex) {
                        Debug.WriteLine($"Error processing group {groupId}: {ex.Message}");
                    }
                }
            }

            return newGroupItems
        .OrderBy(g => g.GroupId)
        .ToList();
        }

        private void HandleLoadingError(Exception ex) {
            Debug.WriteLine($"Critical error loading groups: {ex.Message}");

            DispatcherQueue.TryEnqueue(() => {
            });
        }
        public async Task LoadGroupsAsync() {
            if (!await _loadingSemaphore.WaitAsync(0)) {
                return;
            }

            try {
                using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_loadCancellationSource.Token);
                cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(5));

                DispatcherQueue.TryEnqueue(() =>
                {
                    GroupItems.Clear();
                });

                string jsonFilePath = JsonConfigHelper.GetDefaultConfigPath();
                string jsonContent = await File.ReadAllTextAsync(jsonFilePath, cancellationTokenSource.Token)
                    .ConfigureAwait(false);

                JsonNode jsonObject = JsonNode.Parse(jsonContent ?? "{}") ?? new JsonObject();
                var groupDictionary = jsonObject.AsObject();

                var processingStrategy = groupDictionary.Count >= 5
                    ? ProcessGroupsInParallelAsync(groupDictionary, cancellationTokenSource.Token)
                    : ProcessGroupsSequentiallyAsync(groupDictionary, cancellationTokenSource.Token);

                var updatedGroupItems = await processingStrategy
                    .ConfigureAwait(false);

                DispatcherQueue.TryEnqueue(async () =>
                {
                    GroupItems.Clear();
                    foreach (var item in updatedGroupItems) {
                        // Check if the item already exists in GroupItems
                        if (!GroupItems.Any(existingItem => existingItem.GroupId == item.GroupId)) {
                            GroupItems.Add(item);
                            if (GroupListView.Items.Count == 0) {
                                EmptyView.Visibility = Visibility.Visible;
                            }
                            else {
                                EmptyView.Visibility = Visibility.Collapsed;
                            }
                        }
                       

                    }
                    GroupsCount.Text = GroupListView.Items.Count > 1
                        ? GroupListView.Items.Count.ToString() + " Groups"
                        : GroupListView.Items.Count == 1
                            ? "1 Group"
                            : "";
                   

                });
            }
            catch (OperationCanceledException) {
                Debug.WriteLine("Group loading timed out.");
            }
            catch (Exception ex) {
                HandleLoadingError(ex);
            }
            finally {
                _loadingSemaphore.Release();
            }
        }



        private async Task<GroupItem> CreateGroupItemAsync(int groupId, JsonNode groupNode) {
            string groupName = groupNode?["groupName"]?.GetValue<string>();
            string groupIcon = IconHelper.FindOrigIcon(groupNode?["groupIcon"]?.GetValue<string>());

            var groupItem = new GroupItem {
                GroupId = groupId,
                GroupName = groupName,
                GroupIcon = groupIcon,
                PathIcons = new List<string>(),
                Tooltips = new Dictionary<string, string>(),
                Args = new Dictionary<string, string>(),
                CustomIcons = new Dictionary<string, string>() // Initialize custom icons
            };

            var paths = groupNode?["path"]?.AsObject();
            if (paths?.Count > 0) {
                string outputDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AppGroup",
                    "Icons"
                );
                Directory.CreateDirectory(outputDirectory);

                var iconTasks = paths
                    .Where(p => p.Value != null)
                    .Select(async path => {
                        string filePath = path.Key;
                        string tooltip = path.Value["tooltip"]?.GetValue<string>();
                        string args = path.Value["args"]?.GetValue<string>();
                        string customIcon = path.Value["icon"]?.GetValue<string>(); // Get custom icon from JSON

                        groupItem.Tooltips[filePath] = tooltip;
                        groupItem.Args[filePath] = args;
                        groupItem.CustomIcons[filePath] = customIcon; // Store custom icon

                        // Use custom icon if available and exists, otherwise use cached icon
                        if (!string.IsNullOrEmpty(customIcon) && File.Exists(customIcon)) {
                            return customIcon;
                        }
                        else {
                            // Force icon regeneration if not exists
                            string cachedIconPath = await IconCache.GetIconPathAsync(filePath);

                            // Additional verification to ensure icon is actually generated
                            if (string.IsNullOrEmpty(cachedIconPath) || !File.Exists(cachedIconPath)) {
                                cachedIconPath = await ReGenerateIconAsync(filePath, outputDirectory);
                            }

                            return cachedIconPath;
                        }
                    })
                    .ToList();

                var iconPaths = await Task.WhenAll(iconTasks);
                var validIconPaths = iconPaths.Where(p => !string.IsNullOrEmpty(p) && File.Exists(p)).ToList();

                // Limit to 7 icons
                int maxIconsToShow = 7;
                groupItem.PathIcons.AddRange(validIconPaths.Take(maxIconsToShow));
                groupItem.AdditionalIconsCount = Math.Max(0, validIconPaths.Count - maxIconsToShow);
            }

            return groupItem;
        }
        private async Task<string> ReGenerateIconAsync(string filePath, string outputDirectory) {
            try {
                // Force regeneration of icon
                var regeneratedIconPath = await IconHelper.ExtractIconAndSaveAsync(filePath, outputDirectory, TimeSpan.FromSeconds(2));

                if (regeneratedIconPath != null && File.Exists(regeneratedIconPath)) {
                    // Compute cache key and update cache
                    string cacheKey = IconCache.ComputeFileCacheKey(filePath);
                    IconCache._iconCache[cacheKey] = regeneratedIconPath;
                    IconCache.SaveIconCache();

                    return regeneratedIconPath;
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Icon regeneration failed for {filePath}: {ex.Message}");
            }

            return null;
        }

      
        
        private async void ExportBackupButton_Click(object sender, RoutedEventArgs e) {
            await _backupHelper.ExportBackupAsync();
        }

        private async void ImportBackupButton_Click(object sender, RoutedEventArgs e) {
            await _backupHelper.ImportBackupAsync();
        }

        private void ForceTaskbarUpdate_Click(object sender, RoutedEventArgs e) {

             TaskbarManager.ForceTaskbarUpdateAsync();

        }

        private void AddGroup(object sender, RoutedEventArgs e) {
            int groupId = JsonConfigHelper.GetNextGroupId();
            SaveGroupIdToFile(groupId.ToString());
            EditGroupHelper editGroup = new EditGroupHelper("Edit Group", groupId);
            editGroup.Activate();
        }
        private async void GitHubButton_Click(object sender, RoutedEventArgs e) {
            var uri = new Uri("https://github.com/iandiv");
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }
        private async void CoffeeButton_Click(object sender, RoutedEventArgs e) {
            var uri = new Uri("https://ko-fi.com/iandiv/tip");
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }
        private void EditButton_Click(object sender, RoutedEventArgs e) {
            if (sender is Button button && button.DataContext is GroupItem selectedGroup) {
                SaveGroupIdToFile(selectedGroup.GroupId.ToString());
                EditGroupHelper editGroup = new EditGroupHelper("Edit Group", selectedGroup.GroupId);

                editGroup.Activate();
                //IntPtr existingEditHWnd = NativeMethods.FindWindow(null, "Edit Group");

                //BringEditWindowToFront(existingEditHWnd);

            }
        }
        private void BringEditWindowToFront(IntPtr hWnd) {
            try {
                if (hWnd != IntPtr.Zero) {
                    NativeMethods.SetForegroundWindow(hWnd);
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Failed to bring window to front: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Failed to save group ID: {ex.Message}");
                /* Fail silently - don't block startup */
            }
        }
        private async void DeleteButton_Click(object sender, RoutedEventArgs e) {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is GroupItem selectedGroup) {
                ContentDialog deleteDialog = new ContentDialog {
                    Title = "Delete",
                    Content = $"Are you sure you want to delete the group \"{selectedGroup.GroupName}\"?",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await deleteDialog.ShowAsync();
                if (result == ContentDialogResult.Primary) {
                    string filePath = JsonConfigHelper.GetDefaultConfigPath();
                    JsonConfigHelper.DeleteGroupFromJson(filePath, selectedGroup.GroupId);
                    await LoadGroupsAsync();
                }
            }
        }

        // Add this method to your MainWindow class in MainWindow.xaml.cs

        private async void SettingsButton_Click(object sender, RoutedEventArgs e) {
            try {
                SettingsDialog settingsDialog = new SettingsDialog {
                    XamlRoot = this.Content.XamlRoot
                };

                await settingsDialog.ShowAsync();
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error showing settings dialog: {ex.Message}");

                // Optional: Show an error message to the user
                ContentDialog errorDialog = new ContentDialog {
                    Title = "Error",
                    Content = "Failed to open settings dialog.",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };

                await errorDialog.ShowAsync();
            }
        }
        private async void DuplicateButton_Click(object sender, RoutedEventArgs e) {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is GroupItem selectedGroup) {
                string filePath = JsonConfigHelper.GetDefaultConfigPath();
                JsonConfigHelper.DuplicateGroupInJson(filePath, selectedGroup.GroupId);
                await LoadGroupsAsync();
            }
        }
        private void OpenLocationButton_Click(object sender, RoutedEventArgs e) {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is GroupItem selectedGroup) {
                JsonConfigHelper.OpenGroupFolder(selectedGroup.GroupId);
            }
        }


      

        private void GroupListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (GroupListView.SelectedItem is GroupItem selectedGroup) {
                EditGroupWindow editGroupWindow = new EditGroupWindow(selectedGroup.GroupId);
                editGroupWindow.Activate();
            }
        }
    }
}