
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

        // New properties for Tooltip and Args
        public Dictionary<string, string> Tooltips { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> Args { get; set; } = new Dictionary<string, string>();

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

            this.InitializeComponent();

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

                                        existingItem.Tooltips[filePath] = tooltip;
                                        existingItem.Args[filePath] = args;

                                        return await IconCache.GetIconPathAsync(filePath);
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

        private void SetupFileWatcher() {
            string jsonFilePath = JsonConfigHelper.GetDefaultConfigPath();
            string directoryPath = Path.GetDirectoryName(jsonFilePath);
            string fileName = Path.GetFileName(jsonFilePath);

            _fileWatcher = new FileSystemWatcher(directoryPath, fileName) {
                NotifyFilter = NotifyFilters.LastWrite
            };

            _fileWatcher.Changed +=  (s, e) =>
            {
                 DispatcherQueue.TryEnqueue(async () =>
                {
                    if (!IsFileInUse(jsonFilePath)) {
                        await UpdateGroupItemAsync(jsonFilePath);
                    }
                });
            };

            _fileWatcher.EnableRaisingEvents = true;
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
                Args = new Dictionary<string, string>()
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

                        groupItem.Tooltips[filePath] = tooltip;
                        groupItem.Args[filePath] = args;

                        // Force icon regeneration if not exists
                        string cachedIconPath = await IconCache.GetIconPathAsync(filePath);

                        // Additional verification to ensure icon is actually generated
                        if (string.IsNullOrEmpty(cachedIconPath) || !File.Exists(cachedIconPath)) {
                            cachedIconPath = await ReGenerateIconAsync(filePath, outputDirectory);
                        }

                        return cachedIconPath;
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



        private void AddGroup(object sender, RoutedEventArgs e) {
            int groupId = JsonConfigHelper.GetNextGroupId();

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

                EditGroupHelper editGroup = new EditGroupHelper("Edit Group", selectedGroup.GroupId);

                editGroup.Activate();
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