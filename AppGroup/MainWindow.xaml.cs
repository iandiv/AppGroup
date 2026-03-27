using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinRT.Interop;
using WinUIEx;

namespace AppGroup {
    public class GroupItem : INotifyPropertyChanged {
        public int GroupId { get; set; }
        private string groupName;
        public string GroupName {
            get => groupName;
            set { if (groupName != value) { groupName = value; OnPropertyChanged(nameof(GroupName)); } }
        }
        private string groupIcon;
        public string GroupIcon {
            get => groupIcon;
            set { if (groupIcon != value) { groupIcon = value; OnPropertyChanged(nameof(GroupIcon)); } }
        }
        private List<string> pathIcons;
        public List<string> PathIcons {
            get => pathIcons;
            set { if (pathIcons != value) { pathIcons = value; OnPropertyChanged(nameof(PathIcons)); } }
        }
        public string AdditionalIconsText => AdditionalIconsCount > 0 ? $"+{AdditionalIconsCount}" : string.Empty;
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
        public Dictionary<string, string> Tooltips { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> Args { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> CustomIcons { get; set; } = new Dictionary<string, string>();
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed partial class MainWindow : WinUIEx.WindowEx {
        private readonly Dictionary<int, EditGroupWindow> _openEditWindows = new Dictionary<int, EditGroupWindow>();
        private BackupHelper _backupHelper;
        private ObservableCollection<GroupItem> GroupItems;
        private FileSystemWatcher _fileWatcher;
        private readonly object _loadLock = new object();
        private bool _isLoading = false;
        private string tempIcon;
        private readonly IconHelper _iconHelper;
        private DispatcherTimer debounceTimer;

        // Fix: file-watcher debounce timer to prevent reload storms
        private DispatcherTimer _watcherDebounceTimer;

        private SupportDialogHelper _supportDialogHelper;
        private bool _isReordering = false;
        private readonly Dictionary<int, string> _tempDragFiles = new Dictionary<int, string>();
        private IntPtr _hwnd;
        private NativeMethods.SubclassProc _subclassProc;
        private const int SUBCLASS_ID = 2;
        private bool _wasHidden = false;
        private CancellationTokenSource _dragCleanupCts;

        // Fix: owned CTS so we can cancel on window close
        private readonly CancellationTokenSource _windowCloseCts = new CancellationTokenSource();

        public MainWindow() {
            InitializeComponent();
            _hwnd = WindowNative.GetWindowHandle(this);
            SubclassWindow();
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

            // Fix: create config file if it doesn't exist before first load
            EnsureConfigFileExists();

            _ = LoadGroupsAsync();
            SetupFileWatcher();
            ThemeHelper.UpdateTitleBarColors(this);

            debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            debounceTimer.Tick += FilterGroups;

            // Fix: watcher debounce — 500 ms quiet period before reloading
            _watcherDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _watcherDebounceTimer.Tick += async (s, e) => {
                _watcherDebounceTimer.Stop();
                string jsonFilePath = JsonConfigHelper.GetDefaultConfigPath();
                if (!IsFileInUse(jsonFilePath))
                    await UpdateGroupItemAsync(jsonFilePath);
            };

            _supportDialogHelper = new SupportDialogHelper(this);
            NativeMethods.SetCurrentProcessExplicitAppUserModelID("AppGroup.Main");
            this.Activated += Window_Activated;
            this.AppWindow.Closing += AppWindow_Closing;
            SetWindowIcon();
            _ = CheckForUpdatesOnStartupAsync();
        }

        // Fix: ensure the config JSON exists so ReadAllTextAsync never throws FileNotFoundException
        private void EnsureConfigFileExists() {
            string path = JsonConfigHelper.GetDefaultConfigPath();
            if (!File.Exists(path))
                File.WriteAllText(path, "{}");
        }

        private void PlayContentScaleUp() {
            var content = this.Content as FrameworkElement;
            if (content == null) return;

            content.Opacity = 0;
            content.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            content.RenderTransform = new Microsoft.UI.Xaml.Media.ScaleTransform { ScaleX = 0.95, ScaleY = 0.95 };

            var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();

            var fadeAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(100)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase {
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut
                }
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeAnim, content);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeAnim, "Opacity");

            var scaleXAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation {
                From = 0.95,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(250)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase {
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut
                }
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleXAnim, content.RenderTransform);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleXAnim, "ScaleX");

            var scaleYAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation {
                From = 0.95,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(250)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase {
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut
                }
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleYAnim, content.RenderTransform);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleYAnim, "ScaleY");

            sb.Children.Add(fadeAnim);
            sb.Children.Add(scaleXAnim);
            sb.Children.Add(scaleYAnim);
            sb.Begin();
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs e) {
            if (e.WindowActivationState == WindowActivationState.CodeActivated) {
                if (_wasHidden) {
                    PlayContentScaleUp();
                    _wasHidden = false;
                }
            }
        }

        private void SubclassWindow() {
            _subclassProc = new NativeMethods.SubclassProc(SubclassProc);
            NativeMethods.SetWindowSubclass(_hwnd, _subclassProc, SUBCLASS_ID, IntPtr.Zero);
        }

        private IntPtr SubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData) {
            if (msg == NativeMethods.WM_COPYDATA) {
                try {
                    NativeMethods.COPYDATASTRUCT cds = (NativeMethods.COPYDATASTRUCT)Marshal.PtrToStructure(
                        lParam, typeof(NativeMethods.COPYDATASTRUCT));
                    if (cds.dwData == (IntPtr)100) {
                        string command = Marshal.PtrToStringUni(cds.lpData);
                        if (command == "__SHOW_MAIN__") {
                            DispatcherQueue.TryEnqueue(() => {
                                NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_RESTORE);
                                NativeMethods.SetForegroundWindow(_hwnd);
                                this.AppWindow.IsShownInSwitchers = true;
                            });
                            return (IntPtr)1;
                        }
                    }
                }
                catch (Exception ex) {
                    Debug.WriteLine($"MainWindow SubclassProc error: {ex.Message}");
                }
            }
            return NativeMethods.DefSubclassProc(hWnd, msg, wParam, lParam);
        }

        private async Task CheckForUpdatesOnStartupAsync() {
            try {
                await Task.Delay(2000);
                var settings = await SettingsHelper.LoadSettingsAsync();
                if (!settings.CheckForUpdatesOnStartup) return;

                var updateInfo = await UpdateChecker.CheckForUpdatesAsync();
                if (updateInfo.UpdateAvailable && this.Content?.XamlRoot != null)
                    await ShowUpdateDialogAsync(updateInfo);
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error checking for updates on startup: {ex.Message}");
            }
        }

        private async Task ShowUpdateDialogAsync(UpdateChecker.UpdateInfo updateInfo) {
            try {
                if (this.Content?.XamlRoot == null) return;
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
                if (result == ContentDialogResult.Primary)
                    UpdateChecker.OpenReleasesPage(updateInfo.ReleaseUrl);
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error showing update dialog: {ex.Message}");
            }
        }

        private void SetWindowIcon() {
            try {
                IntPtr hWnd = WindowNative.GetWindowHandle(this);
                var iconPath = Path.Combine(AppContext.BaseDirectory, "AppGroup.ico");
                if (File.Exists(iconPath)) {
                    IntPtr hIcon = NativeMethods.LoadIcon(iconPath);
                    if (hIcon != IntPtr.Zero) {
                        NativeMethods.SendMessage(hWnd, NativeMethods.WM_SETICON, NativeMethods.ICON_SMALL, hIcon);
                        NativeMethods.SendMessage(hWnd, NativeMethods.WM_SETICON, NativeMethods.ICON_BIG, hIcon);
                    }
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Failed to set window icon: {ex.Message}");
            }
        }

        private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args) {
            args.Cancel = true;
            try {
                var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(this.Content.XamlRoot);
                foreach (var popup in popups) {
                    if (popup.Child is ContentDialog dialog) dialog.Hide();
                }
            }
            catch { }

            // Fix: cancel any in-flight load tasks
            _windowCloseCts.Cancel();

            NativeMethods.GetCursorPos(out NativeMethods.POINT cursorPos);
            int screenWidth = NativeMethods.GetSystemMetrics(0);
            NativeMethods.SetCursorPos(screenWidth - 1, cursorPos.Y);

            this.Hide();
            _wasHidden = true;
            await Task.Delay(10);

            NativeMethods.SetCursorPos(cursorPos.X, cursorPos.Y);
            this.AppWindow.IsShownInSwitchers = false;
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            debounceTimer.Stop();
            debounceTimer.Start();
        }

        private void FilterGroups(object sender, object e) {
            debounceTimer.Stop();
            string searchQuery = SearchTextBox.Text.ToLower();
            var filteredGroups = GroupItems.Where(g => g.GroupName.ToLower().Contains(searchQuery)).ToList();
            GroupListView.ItemsSource = filteredGroups.Count > 0 ? filteredGroups : GroupItems;
            GroupsCount.Text = GroupListView.Items.Count > 1
                ? GroupListView.Items.Count + " Groups"
                : GroupListView.Items.Count == 1 ? "1 Group" : "";
        }

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public async Task UpdateGroupItemAsync(string jsonFilePath) {
            // Fix: use a timeout on the semaphore so a hung icon task can't deadlock forever
            if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(10))) {
                Debug.WriteLine("UpdateGroupItemAsync: semaphore wait timed out");
                return;
            }
            try {
                string jsonContent = await File.ReadAllTextAsync(jsonFilePath);
                JsonNode jsonObject = JsonNode.Parse(jsonContent ?? "{}") ?? new JsonObject();
                var groupDictionary = jsonObject.AsObject();

                // Fix: build icon data off the UI thread, then apply to UI thread in one batch
                var updates = new List<(int groupId, string newName, string newIcon, List<string> icons, int extra,
                    Dictionary<string, string> tooltips, Dictionary<string, string> args, Dictionary<string, string> customIcons)>();

                foreach (var property in groupDictionary) {
                    if (!int.TryParse(property.Key, out int groupId)) continue;
                    try {
                        string newGroupName = property.Value?["groupName"]?.GetValue<string>();
                        string newGroupIcon = IconHelper.FindOrigIcon(property.Value?["groupIcon"]?.GetValue<string>());
                        var paths = property.Value?["path"]?.AsObject();

                        var tooltips = new Dictionary<string, string>();
                        var args = new Dictionary<string, string>();
                        var customIcons = new Dictionary<string, string>();
                        var iconPaths = new List<string>();

                        if (paths?.Count > 0) {
                            // Fix: run icon tasks sequentially with individual timeouts rather than WhenAll inside semaphore
                            foreach (var path in paths.Where(p => p.Value != null)) {
                                string filePath = path.Key;
                                string tooltip = path.Value["tooltip"]?.GetValue<string>();
                                string argVal = path.Value["args"]?.GetValue<string>();
                                string customIcon = path.Value["icon"]?.GetValue<string>();

                                tooltips[filePath] = tooltip;
                                args[filePath] = argVal;
                                customIcons[filePath] = customIcon;

                                string iconResult;
                                // Fix: treat "" same as null — JSON writes "" for no custom icon
                                string effectiveCustom = string.IsNullOrWhiteSpace(customIcon) ? null : customIcon;
                                if (effectiveCustom != null && File.Exists(effectiveCustom)) {
                                    iconResult = effectiveCustom;
                                }
                                else {
                                    try {
                                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_windowCloseCts.Token);
                                        cts.CancelAfter(TimeSpan.FromSeconds(3));
                                        iconResult = Path.GetExtension(filePath).Equals(".url", StringComparison.OrdinalIgnoreCase)
                                            ? await IconHelper.GetUrlFileIconAsync(filePath)
                                            : await IconCache.GetIconPathAsync(filePath);
                                    }
                                    catch (OperationCanceledException) {
                                        iconResult = null;
                                    }
                                }
                                if (!string.IsNullOrEmpty(iconResult))
                                    iconPaths.Add(iconResult);
                            }
                        }

                        int maxIcons = 7;
                        updates.Add((groupId, newGroupName, newGroupIcon,
                            iconPaths.Take(maxIcons).ToList(),
                            Math.Max(0, iconPaths.Count - maxIcons),
                            tooltips, args, customIcons));
                    }
                    catch (Exception ex) {
                        Debug.WriteLine($"UpdateGroupItemAsync: error processing group {groupId}: {ex.Message}");
                    }
                }

                // Fix: all UI mutations on the dispatcher thread
                DispatcherQueue.TryEnqueue(() => {
                    foreach (var (groupId, newName, newIcon, icons, extra, tooltips, args, customIcons) in updates) {
                        var existingItem = GroupItems.FirstOrDefault(item => item.GroupId == groupId);
                        if (existingItem != null) {
                            existingItem.GroupName = newName;
                            existingItem.GroupIcon = null;
                            existingItem.GroupIcon = newIcon;
                            existingItem.PathIcons = icons;
                            existingItem.AdditionalIconsCount = extra;
                            existingItem.Tooltips = tooltips;
                            existingItem.Args = args;
                            existingItem.CustomIcons = customIcons;
                        }
                        else {
                            GroupItems.Add(new GroupItem {
                                GroupId = groupId,
                                GroupName = newName,
                                GroupIcon = newIcon,
                                PathIcons = icons,
                                AdditionalIconsCount = extra,
                                Tooltips = tooltips,
                                Args = args,
                                CustomIcons = customIcons
                            });
                        }
                    }

                    // Remove items that no longer exist in the JSON
                    var activeIds = updates.Select(u => u.groupId).ToHashSet();
                    var toRemove = GroupItems.Where(g => !activeIds.Contains(g.GroupId)).ToList();
                    foreach (var item in toRemove)
                        GroupItems.Remove(item);

                    GroupsCount.Text = GroupListView.Items.Count > 1
                        ? GroupListView.Items.Count + " Groups"
                        : GroupListView.Items.Count == 1 ? "1 Group" : "";
                    EmptyView.Visibility = GroupListView.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                });
            }
            finally {
                _semaphore.Release();
            }
        }

        private void TouchFile(string path) {
            var now = DateTime.Now;
            File.SetCreationTime(path, now);
            File.SetLastWriteTime(path, now);
            File.SetLastAccessTime(path, now);
        }

        private async void GroupListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e) {
            _isReordering = true;

            if (e.Items.Count > 0 && e.Items[0] is GroupItem draggedItem) {
                if (string.IsNullOrWhiteSpace(draggedItem.GroupName)) {
                    e.Data.RequestedOperation = DataPackageOperation.Move;
                    return;
                }

                e.Data.Properties.Add("DraggedGroupId", draggedItem.GroupId);
                e.Data.RequestedOperation = DataPackageOperation.Copy | DataPackageOperation.Link;

                string appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppGroup");
                string fullShortcutPath = Path.GetFullPath(
                    Path.Combine(appDataPath, "Groups", draggedItem.GroupName, $"{draggedItem.GroupName}.lnk"));

                if (File.Exists(fullShortcutPath)) {
                    try {
                        string tempDir = Path.Combine(appDataPath, "DragTemp");
                        Directory.CreateDirectory(tempDir);

                        _dragCleanupCts?.Cancel();
                        _dragCleanupCts = new CancellationTokenSource();

                        string tempShortcutPath = Path.Combine(tempDir, $"{draggedItem.GroupName}.lnk");
                        File.Copy(fullShortcutPath, tempShortcutPath, true);
                        TouchFile(tempShortcutPath);
                        _tempDragFiles[draggedItem.GroupId] = tempShortcutPath;

                        e.Data.SetText(fullShortcutPath);
                        var storageFile = await StorageFile.GetFileFromPathAsync(tempShortcutPath);
                        e.Data.SetStorageItems(new List<IStorageItem> { storageFile });
                    }
                    catch (Exception ex) {
                        Debug.WriteLine($"Error preparing drag data: {ex.Message}");
                        throw;
                    }
                }
            }
        }

        private async void GroupListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) {
            try {
                var itemsToClean = new List<string>();

                foreach (var item in args.Items) {
                    if (item is GroupItem groupItem && _tempDragFiles.TryGetValue(groupItem.GroupId, out var tempFilePath)) {
                        itemsToClean.Add(tempFilePath);
                        _tempDragFiles.Remove(groupItem.GroupId);
                    }
                }

                _isReordering = false;

                if (args.DropResult == DataPackageOperation.Move) {
                    var reorderedItems = GroupListView.Items.OfType<GroupItem>().ToList();
                    await UpdateJsonWithNewOrderAsync(reorderedItems);
                }

                // Fix: always clean up temp files regardless of drop result
                _ = Task.Run(() => {
                    foreach (var tempPath in itemsToClean) {
                        if (string.IsNullOrEmpty(tempPath)) continue;
                        // Give the shell a moment to finish reading the file
                        Task.Delay(2000).Wait();
                        try {
                            if (File.Exists(tempPath))
                                File.Delete(tempPath);
                        }
                        catch (IOException ex) {
                            Debug.WriteLine($"Could not delete temp drag file: {ex.Message}");
                        }
                    }
                });
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error during drag completion: {ex.Message}");
                _ = LoadGroupsAsync();
            }
        }

        private async Task UpdateJsonWithNewOrderAsync(List<GroupItem> reorderedItems) {
            try {
                string jsonFilePath = JsonConfigHelper.GetDefaultConfigPath();
                _fileWatcher.EnableRaisingEvents = false;

                string jsonContent = await File.ReadAllTextAsync(jsonFilePath);
                JsonNode jsonObject = JsonNode.Parse(jsonContent ?? "{}") ?? new JsonObject();
                var groupDictionary = jsonObject.AsObject();

                var newJsonObject = new JsonObject();
                for (int i = 0; i < reorderedItems.Count; i++) {
                    var item = reorderedItems[i];
                    string oldKey = item.GroupId.ToString();
                    string newKey = (i + 1).ToString();
                    if (groupDictionary.ContainsKey(oldKey))
                        newJsonObject[newKey] = groupDictionary[oldKey]?.DeepClone();
                }

                string updatedJsonContent = newJsonObject.ToJsonString(
                    new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(jsonFilePath, updatedJsonContent);

                for (int i = 0; i < reorderedItems.Count; i++)
                    reorderedItems[i].GroupId = i + 1;

                await Task.Delay(100);
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error updating JSON with new order: {ex.Message}");
                throw;
            }
            finally {
                // Fix: always re-enable watcher, even on error
                _fileWatcher.EnableRaisingEvents = true;
            }
        }

        private void SetupFileWatcher() {
            string jsonFilePath = JsonConfigHelper.GetDefaultConfigPath();
            string directoryPath = Path.GetDirectoryName(jsonFilePath);
            string fileName = Path.GetFileName(jsonFilePath);

            _fileWatcher = new FileSystemWatcher(directoryPath, fileName) {
                NotifyFilter = NotifyFilters.LastWrite
            };

            // Fix: use debounce timer instead of firing reload directly
            _fileWatcher.Changed += (s, e) => {
                if (_isReordering) return;
                DispatcherQueue.TryEnqueue(() => {
                    _watcherDebounceTimer.Stop();
                    _watcherDebounceTimer.Start();
                });
            };

            _fileWatcher.EnableRaisingEvents = true;
        }

        private bool IsFileInUse(string filePath) {
            try {
                using var fs = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return false;
            }
            catch (IOException) {
                return true;
            }
        }

        private void Reload(object sender, RoutedEventArgs e) => _ = LoadGroupsAsync();

        private readonly SemaphoreSlim _loadingSemaphore = new SemaphoreSlim(1, 1);

        private async Task<List<GroupItem>> ProcessGroupsInParallelAsync(
            JsonObject groupDictionary, CancellationToken cancellationToken) {
            var options = new ParallelOptions {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            };
            var newGroupItems = new ConcurrentBag<GroupItem>();

            await Parallel.ForEachAsync(groupDictionary, options, async (property, token) => {
                if (!int.TryParse(property.Key, out int groupId)) return;
                try {
                    var groupItem = await CreateGroupItemAsync(groupId, property.Value);
                    newGroupItems.Add(groupItem);
                }
                catch (Exception ex) {
                    Debug.WriteLine($"Error processing group {groupId}: {ex.Message}");
                }
            });

            return newGroupItems.OrderBy(g => g.GroupId).ToList();
        }

        private async Task<List<GroupItem>> ProcessGroupsSequentiallyAsync(
            JsonObject groupDictionary, CancellationToken cancellationToken) {
            var newGroupItems = new List<GroupItem>();
            foreach (var property in groupDictionary) {
                cancellationToken.ThrowIfCancellationRequested();
                if (!int.TryParse(property.Key, out int groupId)) continue;
                try {
                    var groupItem = await CreateGroupItemAsync(groupId, property.Value);
                    newGroupItems.Add(groupItem);
                }
                catch (Exception ex) {
                    Debug.WriteLine($"Error processing group {groupId}: {ex.Message}");
                }
            }
            return newGroupItems.OrderBy(g => g.GroupId).ToList();
        }

        private void HandleLoadingError(Exception ex) {
            Debug.WriteLine($"Critical error loading groups: {ex.Message}");
        }

        public async Task LoadGroupsAsync() {
            if (!await _loadingSemaphore.WaitAsync(0)) return;

            try {
                // Fix: link to window-close token so loads abort when window closes
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(_windowCloseCts.Token);
                cts.CancelAfter(TimeSpan.FromSeconds(10));

                string jsonFilePath = JsonConfigHelper.GetDefaultConfigPath();

                // Fix: guard against missing file instead of crashing
                if (!File.Exists(jsonFilePath)) {
                    File.WriteAllText(jsonFilePath, "{}");
                }

                string jsonContent = await File.ReadAllTextAsync(jsonFilePath, cts.Token)
                    .ConfigureAwait(false);

                JsonNode jsonObject = JsonNode.Parse(jsonContent ?? "{}") ?? new JsonObject();
                var groupDictionary = jsonObject.AsObject();

                var processingTask = groupDictionary.Count >= 5
                    ? ProcessGroupsInParallelAsync(groupDictionary, cts.Token)
                    : ProcessGroupsSequentiallyAsync(groupDictionary, cts.Token);

                var updatedGroupItems = await processingTask.ConfigureAwait(false);

                // Fix: swap atomically — clear and re-add in one dispatcher call to avoid flash
                DispatcherQueue.TryEnqueue(() => {
                    // Remove items no longer in JSON
                    var activeIds = updatedGroupItems.Select(g => g.GroupId).ToHashSet();
                    var toRemove = GroupItems.Where(g => !activeIds.Contains(g.GroupId)).ToList();
                    foreach (var r in toRemove)
                        GroupItems.Remove(r);

                    // Add or update
                    foreach (var item in updatedGroupItems) {
                        var existing = GroupItems.FirstOrDefault(g => g.GroupId == item.GroupId);
                        if (existing == null)
                            GroupItems.Add(item);
                        else {
                            existing.GroupName = item.GroupName;
                            existing.GroupIcon = item.GroupIcon;
                            existing.PathIcons = item.PathIcons;
                            existing.AdditionalIconsCount = item.AdditionalIconsCount;
                        }
                    }

                    GroupsCount.Text = GroupListView.Items.Count > 1
                        ? GroupListView.Items.Count + " Groups"
                        : GroupListView.Items.Count == 1 ? "1 Group" : "";
                    EmptyView.Visibility = GroupListView.Items.Count == 0
                        ? Visibility.Visible : Visibility.Collapsed;
                });
            }
            catch (OperationCanceledException) {
                Debug.WriteLine("Group loading cancelled or timed out.");
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
                CustomIcons = new Dictionary<string, string>()
            };

            var paths = groupNode?["path"]?.AsObject();
            if (paths?.Count > 0) {
                string outputDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AppGroup", "Icons");
                Directory.CreateDirectory(outputDirectory);

                var iconPaths = new List<string>();
                foreach (var path in paths.Where(p => p.Value != null)) {
                    // Fix: respect window-close cancellation in per-item icon loads
                    if (_windowCloseCts.IsCancellationRequested) break;

                    string filePath = path.Key;
                    string tooltip = path.Value["tooltip"]?.GetValue<string>();
                    string argVal = path.Value["args"]?.GetValue<string>();
                    string customIcon = path.Value["icon"]?.GetValue<string>();

                    groupItem.Tooltips[filePath] = tooltip;
                    groupItem.Args[filePath] = argVal;
                    groupItem.CustomIcons[filePath] = customIcon;

                    string iconResult = null;
                    // Fix: treat "" the same as null — AddGroupToJson writes "" for no custom icon
                    string effectiveCustomIcon = string.IsNullOrWhiteSpace(customIcon) ? null : customIcon;
                    if (effectiveCustomIcon != null && File.Exists(effectiveCustomIcon)) {
                        iconResult = effectiveCustomIcon;
                    }
                    else {
                        string cachedIconPath;
                        try {
                            cachedIconPath = Path.GetExtension(filePath).Equals(".url", StringComparison.OrdinalIgnoreCase)
                                ? await IconHelper.GetUrlFileIconAsync(filePath)
                                : await IconCache.GetIconPathAsync(filePath);
                        }
                        catch (Exception ex) {
                            Debug.WriteLine($"Icon load failed for {filePath}: {ex.Message}");
                            cachedIconPath = null;
                        }

                        if (string.IsNullOrEmpty(cachedIconPath) || !File.Exists(cachedIconPath))
                            cachedIconPath = await ReGenerateIconAsync(filePath, outputDirectory);

                        iconResult = cachedIconPath;
                    }

                    if (!string.IsNullOrEmpty(iconResult)) iconPaths.Add(iconResult);
                }

                var validIconPaths = iconPaths.Where(File.Exists).ToList();
                int maxIconsToShow = 7;
                groupItem.PathIcons.AddRange(validIconPaths.Take(maxIconsToShow));
                groupItem.AdditionalIconsCount = Math.Max(0, validIconPaths.Count - maxIconsToShow);
            }

            return groupItem;
        }

        private async Task<string> ReGenerateIconAsync(string filePath, string outputDirectory) {
            try {
                // Only evict if the cached PNG is missing from disk; InvalidateEntry handles the rest
                if (IconCache.TryGetCachedPath(filePath, out _)) {
                    // Entry exists and PNG is on disk — no eviction needed
                }
                else {
                    // No valid entry — InvalidateEntry is a no-op here but safe to call
                    IconCache.InvalidateEntry(filePath);
                }

                string regeneratedIconPath = await IconCache.GetIconPathAsync(filePath);
                if (!string.IsNullOrEmpty(regeneratedIconPath) && File.Exists(regeneratedIconPath))
                    return regeneratedIconPath;
            }
            catch (Exception ex) {
                Debug.WriteLine($"Icon regeneration failed for {filePath}: {ex.Message}");
            }
            return null;
        }

        private async void ExportBackupButton_Click(object sender, RoutedEventArgs e) =>
            await _backupHelper.ExportBackupAsync();

        private async void ImportBackupButton_Click(object sender, RoutedEventArgs e) =>
            await _backupHelper.ImportBackupAsync();

        private void ForceTaskbarUpdate_Click(object sender, RoutedEventArgs e) =>
            TaskbarManager.ForceTaskbarUpdateAsync();

        private void AddGroup(object sender, RoutedEventArgs e) {
            int groupId = JsonConfigHelper.GetNextGroupId();
            SaveGroupIdToFile(groupId.ToString());
            EditGroupHelper editGroup = new EditGroupHelper("Edit Group", groupId);
            editGroup.Activate();
        }

        private async void GitHubButton_Click(object sender, RoutedEventArgs e) =>
            await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/iandiv"));

        private async void CoffeeButton_Click(object sender, RoutedEventArgs e) =>
            await Windows.System.Launcher.LaunchUriAsync(new Uri("https://ko-fi.com/iandiv/tip"));

        private void EditButton_Click(object sender, RoutedEventArgs e) {
            if (sender is Button button && button.DataContext is GroupItem selectedGroup) {
                SaveGroupIdToFile(selectedGroup.GroupId.ToString());
                EditGroupHelper editGroup = new EditGroupHelper("Edit Group", selectedGroup.GroupId);
                editGroup.Activate();
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
                Debug.WriteLine($"Failed to bring window to front: {ex.Message}");
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

        private async void SettingsButton_Click(object sender, RoutedEventArgs e) {
            try {
                SettingsDialog settingsDialog = new SettingsDialog {
                    XamlRoot = this.Content.XamlRoot
                };
                await settingsDialog.ShowAsync();
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error showing settings dialog: {ex.Message}");
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
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is GroupItem selectedGroup)
                JsonConfigHelper.OpenGroupFolder(selectedGroup.GroupId);
        }

        private void GroupListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (GroupListView.SelectedItem is GroupItem selectedGroup) {
                EditGroupWindow editGroupWindow = new EditGroupWindow(selectedGroup.GroupId);
                editGroupWindow.Activate();
            }
        }
    }
}