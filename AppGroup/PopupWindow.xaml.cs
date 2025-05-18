using IWshRuntimeLibrary;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI.ViewManagement;
using WinRT.Interop;
using WinUIEx;
using File = System.IO.File;

namespace AppGroup {

    public class GroupData {
        public Dictionary<string, Dictionary<string, string>> path { get; set; }
        public string groupIcon { get; set; }
        public string groupName { get; set; }
        public bool groupHeader { get; set; }
        public int groupCol { get; set; }
        public int groupId { get; set; }
    }

    public class PopupItem : INotifyPropertyChanged {
        public string Path { get; set; }
        public string Name { get; set; }
        public string ToolTip { get; set; }
        public string Args { get; set; }
        private BitmapImage _icon;
        public BitmapImage Icon {
            get => _icon;
            set {
                if (_icon != value) {
                    _icon = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
                }
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    public sealed partial class PopupWindow : Window {
        // Constants for UI elements
        private const int BUTTON_SIZE = 40;
        private const int ICON_SIZE = 24;
        private const int BUTTON_MARGIN = 4;

        // Static JSON options to prevent redundant creation
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true
        };

        // Member variables
        private readonly Dictionary<int, EditGroupWindow> _openEditWindows = new Dictionary<int, EditGroupWindow>();
        private readonly WindowHelper _windowHelper;
        private ObservableCollection<PopupItem> PopupItems = new ObservableCollection<PopupItem>();
        private Dictionary<string, GroupData> _groups;
        private GridView _gridView;
        private PopupItem _clickedItem;
        private int _groupId;
        private string _groupFilter = null;
        private string _json = "";
        private bool _anyGroupDisplayed;
        private DataTemplate _itemTemplate;
        private ItemsPanelTemplate _panelTemplate;
        private nint hWnd;

        // Constructor
        public PopupWindow(string groupFilter = null) {
            InitializeComponent();

            _groupFilter = groupFilter;
            this.Title = groupFilter;

            // Setup window
            _windowHelper = new WindowHelper(this);
            _windowHelper.SetSystemBackdrop(WindowHelper.BackdropType.AcrylicBase);
            _windowHelper.IsMaximizable = false;
            _windowHelper.IsMinimizable = false;
            _windowHelper.IsResizable = true;
            _windowHelper.HasBorder = true;
            _windowHelper.HasTitleBar = false;
            _windowHelper.IsAlwaysOnTop = true;

            // Initialize templates
            InitializeTemplates();



            // Load on activation
            this.Activated += Window_Activated;
        }
        private void UiSettings_ColorValuesChanged(UISettings sender, object args) {
            // Update the MainGrid background color based on the current settings
            UpdateMainGridBackground(sender);
        }

        private void UpdateMainGridBackground(UISettings uiSettings) {
            // Check if the accent color is being shown on Start and taskbar

            if (IsAccentColorOnStartTaskbarEnabled()) {
                MainGrid.Background = Application.Current.Resources["AccentAcrylicInAppFillColorBaseBrush"] as Microsoft.UI.Xaml.Media.AcrylicBrush;
            }
            else {
                MainGrid.Background = null;
            }
        }

        private bool IsAccentColorOnStartTaskbarEnabled() {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) {
                if (key != null) {
                    object value = key.GetValue("ColorPrevalence");
                    if (value != null && (int)value == 1) {
                        return true;
                    }
                }
            }
            return false;
        }

        private void InitializeTemplates() {
            // Create item template once
            _itemTemplate = (DataTemplate)XamlReader.Load(
     $@"<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <Grid VerticalAlignment=""Center""
          HorizontalAlignment=""Center""
          Width=""{BUTTON_SIZE}""
          Height=""{BUTTON_SIZE}""
          ToolTipService.ToolTip=""{{Binding ToolTip}}"">
        <Image Source=""{{Binding Icon}}""
               Width=""{ICON_SIZE}""
               Height=""{ICON_SIZE}""
               Stretch=""Uniform""
               VerticalAlignment=""Center""
               HorizontalAlignment=""Center""
               Margin=""8"" />
    </Grid>
</DataTemplate>");

            // Create panel template once
            const int EFFECTIVE_BUTTON_WIDTH = BUTTON_SIZE + (BUTTON_MARGIN * 2);
            _panelTemplate = (ItemsPanelTemplate)XamlReader.Load(
                $@"<ItemsPanelTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
                <ItemsWrapGrid Orientation=""Horizontal""
                              ItemWidth=""{EFFECTIVE_BUTTON_WIDTH}""
                              ItemHeight=""{EFFECTIVE_BUTTON_WIDTH}""
                              HorizontalAlignment=""Center""
                              VerticalAlignment=""Center""/>
            </ItemsPanelTemplate>");
        }

        // Load configuration with better error handling and caching
        private void LoadConfiguration() {
            try {
                string configPath = JsonConfigHelper.GetDefaultConfigPath();
                _json = JsonConfigHelper.ReadJsonFromFile(configPath);
                _groups = JsonSerializer.Deserialize<Dictionary<string, GroupData>>(_json, JsonOptions);

                // Only initialize the window and create dynamic content if groups are loaded successfully
                if (_groups != null) {
                    InitializeWindow();
                    CreateDynamicContent();
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error loading configuration: {ex.Message}");
                _json = GetDefaultJsonConfiguration();
                _groups = JsonSerializer.Deserialize<Dictionary<string, GroupData>>(_json, JsonOptions);
                InitializeWindow();
                CreateDynamicContent();
            }
        }

        private string GetDefaultJsonConfiguration() {
            return @"{
            ""Group1NameHere"": {
                ""groupCol"": 3,
                ""groupIcon"": ""test.png"",
                ""path"": [""C:\\Windows\\System32\\notepad.exe"", ""C:\\Windows\\System32\\calc.exe"", ""C:\\Windows\\System32\\mspaint.exe""]
            }
        }";
        }

        // Non-async window initialization for faster loading
        private void InitializeWindow() {
            int maxPathItems = 1;
            int maxColumns = 1;
            string groupIcon = "AppGroup.ico";
            bool groupHeader = false;

            // If we have a group filter, only consider that group
            if (!string.IsNullOrEmpty(_groupFilter) && _groups.Values.Any(g => g.groupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase))) {
                var filteredGroup = _groups.FirstOrDefault(g => g.Value.groupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));

                maxPathItems = filteredGroup.Value.path.Count;
                maxColumns = filteredGroup.Value.groupCol;
                groupHeader = filteredGroup.Value.groupHeader;
                groupIcon = filteredGroup.Value.groupIcon;

                if (!int.TryParse(filteredGroup.Key, out _groupId)) {
                    Debug.WriteLine($"Error: Group key '{filteredGroup.Key}' is not a valid integer ID");
                    return;
                }
            }
            else {
                foreach (var group in _groups.Values) {
                    maxPathItems = Math.Max(maxPathItems, group.path.Count);
                    maxColumns = Math.Max(maxColumns, group.groupCol);
                }
            }

            int numberOfRows = (int)Math.Ceiling((double)maxPathItems / maxColumns);
            int dynamicWidth = maxColumns * (BUTTON_SIZE + BUTTON_MARGIN * 2);
            if (groupHeader == true && maxColumns < 2) {
                dynamicWidth = 2 * (BUTTON_SIZE + BUTTON_MARGIN * 2);
            }

            int dynamicHeight = numberOfRows * (BUTTON_SIZE + BUTTON_MARGIN * 2);
            var displayInfo = GetDisplayInformation();
            float scaleFactor = displayInfo.Item1;

            int scaledWidth = (int)(dynamicWidth * scaleFactor);
            int scaledHeight = (int)(dynamicHeight * scaleFactor);
            if (groupHeader) {
                scaledHeight += 40;
            }

            // Set window icon - simplified from previous version
            this.AppWindow.SetIcon(groupIcon);
            MainGrid.Margin = new Thickness(0, 0, -5, -15);

            int finalWidth = scaledWidth + 30;
            int finalHeight = scaledHeight + 20;
            this.AppWindow.Resize(new SizeInt32(finalWidth, finalHeight));
            hWnd = WindowNative.GetWindowHandle(this);
            NativeMethods.PositionWindowAboveTaskbar(hWnd);

        }

        private void CreateDynamicContent() {
            // Clear existing items
            PopupItems.Clear();
            GridPanel.Children.Clear();

            _anyGroupDisplayed = false;

            foreach (var group in _groups) {
                // Skip this group if filtering is active and this isn't the requested group
                if (_groupFilter != null && !group.Value.groupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                _anyGroupDisplayed = true;

                // Set header visibility
                if (group.Value.groupHeader) {
                    Header.Visibility = Visibility.Visible;
                    HeaderText.Text = group.Value.groupName;
                    ScrollView.Margin = new Thickness(0, 0, 0, 5);
                }
                else {
                    Header.Visibility = Visibility.Collapsed;
                    ScrollView.Margin = new Thickness(0, 5, 0, 5);
                }

                // Configure GridView
                _gridView = new GridView {
                    SelectionMode = ListViewSelectionMode.Extended,
                    IsItemClickEnabled = true,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    CanDragItems = true,
                    CanReorderItems = true,
                    AllowDrop = true,
                    ItemTemplate = _itemTemplate,
                    ItemsPanel = _panelTemplate
                };


                // Set up events
                _gridView.RightTapped += GridView_RightTapped;
                _gridView.DragItemsCompleted += GridView_DragItemsCompleted;
                _gridView.ItemClick += GridView_ItemClick;

                // Load items
                LoadGridItems(group.Value.path);

                _gridView.ItemsSource = PopupItems;
                GridPanel.Children.Add(_gridView);
            }

            // Handle case where no groups match filter
            if (!_anyGroupDisplayed) {
                TextBlock noGroupsText = new TextBlock {
                    Text = $"No group found matching '{_groupFilter}'",
                    Margin = new Thickness(10),
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };
                GridPanel.Children.Add(noGroupsText);

                this.AppWindow.Resize(new SizeInt32(250, 120));
            }
        }

        // Load grid items efficiently
        //private void LoadGridItems(List<string> paths) {
        //    foreach (string path in paths) {
        //        string displayName = GetDisplayName(path);
        //        var popupItem = new PopupItem {
        //            Path = path,
        //            Name = Path.GetFileNameWithoutExtension(path),
        //            ToolTip = displayName,
        //            Icon = null
        //        };
        //        PopupItems.Add(popupItem);

        //        _ = LoadIconAsync(popupItem, path);
        //    }
        //}
        //private void LoadGridItems(Dictionary<string, Dictionary<string, string>> pathsWithProperties) {
        //    foreach (var pathEntry in pathsWithProperties) {
        //        string path = pathEntry.Key;
        //        var properties = pathEntry.Value;

        //        // Get displayName from properties if available, otherwise use default method
        //        string tooltip = properties.ContainsKey("tooltip")
        //            ? properties["tooltip"]
        //            : GetDisplayName(path);

        //        var popupItem = new PopupItem {
        //            Path = path,
        //            Name = Path.GetFileNameWithoutExtension(path), // Use the custom displayName instead of extracting from path
        //            ToolTip = tooltip, // You might want to keep the full path as tooltip or use a different property
        //            Icon = null,
        //            Args = properties.ContainsKey("Args") ? properties["Args"] : "" // Add the Args property
        //        };

        //        PopupItems.Add(popupItem);
        //        _ = LoadIconAsync(popupItem, path);
        //    }
        //}

        private void LoadGridItems(Dictionary<string, Dictionary<string, string>> pathsWithProperties) {
            foreach (var pathEntry in pathsWithProperties) {
                string path = pathEntry.Key;
                var properties = pathEntry.Value;

                // Get displayName from properties if available, otherwise use default method
                string tooltip = properties.ContainsKey("tooltip") && !string.IsNullOrEmpty(properties["tooltip"])
                    ? properties["tooltip"]
                    : GetDisplayName(path);

                var popupItem = new PopupItem {
                    Path = path,
                    Name = Path.GetFileNameWithoutExtension(path),
                    ToolTip = tooltip,
                    Icon = null,
                    Args = properties.ContainsKey("args") ? properties["args"] : "" // Add the Args property
                };

                PopupItems.Add(popupItem);
                _ = LoadIconAsync(popupItem, path);
            }
        }


        private async Task LoadIconAsync(PopupItem item, string path) {
            try {
                string iconPath = await IconCache.GetIconPathAsync(path);
                BitmapImage icon = await IconCache.LoadImageFromPathAsync(iconPath);

                // Update on UI thread
                DispatcherQueue.TryEnqueue(() => {
                    item.Icon = icon;
                });
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error loading icon for {path}: {ex.Message}");
                DispatcherQueue.TryEnqueue(() => {
                    item.Icon = null;
                });
            }
        }

        private void GridView_ItemClick(object sender, ItemClickEventArgs e) {
            if (e.ClickedItem is PopupItem popupItem) {
                TryLaunchApp(popupItem.Path, popupItem.Args);
            }
        }

        private void GridView_RightTapped(object sender, RightTappedRoutedEventArgs e) {
            var item = (e.OriginalSource as FrameworkElement)?.DataContext as PopupItem;

            if (item != null) {
                MenuFlyout flyout = CreateItemFlyout();
                flyout.ShowAt(_gridView, e.GetPosition(_gridView));
                _clickedItem = item;
            }
        }

       
        private void GridView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) {
            try {
                if (_groups == null || string.IsNullOrEmpty(_groupFilter)) {
                    Debug.WriteLine("Error: Unable to deserialize groups or group filter is not set");
                    return;
                }

                var filteredGroup = _groups.FirstOrDefault(g => g.Value.groupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));

                if (filteredGroup.Key == null) {
                    Debug.WriteLine($"Error: Group '{_groupFilter}' not found in configuration");
                    return;
                }

                // Create a new dictionary to hold the reordered paths with their properties
                Dictionary<string, (string tooltip, string args)> newPathOrder = new Dictionary<string, (string tooltip, string args)>();

                foreach (var item in PopupItems) {
                    // Add each path with its properties to the new dictionary
                    newPathOrder[item.Path] = (item.ToolTip, item.Args);
                }

                if (!int.TryParse(filteredGroup.Key, out int groupId)) {
                    Debug.WriteLine($"Error: Group key '{filteredGroup.Key}' is not a valid integer ID");
                    return;
                }

                JsonConfigHelper.AddGroupToJson(
                    JsonConfigHelper.GetDefaultConfigPath(),
                    groupId,
                    filteredGroup.Value.groupName,
                    filteredGroup.Value.groupHeader,
                    filteredGroup.Value.groupIcon,
                    filteredGroup.Value.groupCol,
                    newPathOrder
                );

                _json = File.ReadAllText(JsonConfigHelper.GetDefaultConfigPath());
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error in GridView_DragItemsCompleted: {ex.Message}");
                ShowErrorDialog($"Failed to save new item order: {ex.Message}");
            }
        }

        private async void Window_Activated(object sender, WindowActivatedEventArgs e) {
            if (e.WindowActivationState == WindowActivationState.Deactivated) {



                this.DispatcherQueue.TryEnqueue(() => {
                    if (_gridView != null) {
                        _gridView.RightTapped -= GridView_RightTapped;
                        _gridView.DragItemsCompleted -= GridView_DragItemsCompleted;
                        _gridView.ItemClick -= GridView_ItemClick;
                    }

                    foreach (var item in PopupItems) {
                        item.Icon = null;
                    }

                    PopupItems.Clear();
                    GridPanel.Children.Clear();

                    _groups = null;
                    _json = "";
                    _clickedItem = null;
                    _gridView = null;

                });
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                NativeMethods.EmptyWorkingSet(Process.GetCurrentProcess().Handle);
                this.DispatcherQueue.TryEnqueue(() => {
                    if (!_anyGroupDisplayed) {
                        this.Close();

                    }
                    else {
                        this.Hide();

                    }
                });

            }
            else if (e.WindowActivationState == WindowActivationState.CodeActivated) {
                // Get the UISettings instance
                var uiSettings = new UISettings();

                // Subscribe to the ColorValuesChanged event
                uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;

                // Initial update to set the background color based on the current settings
                UpdateMainGridBackground(uiSettings);
                LoadConfiguration();
            }
        }


        private void TryLaunchApp(string path,string args) {
            try {
                ProcessStartInfo psi = new ProcessStartInfo {
                    FileName = path,
                    Arguments = args,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex) {
                Debug.WriteLine($"Failed to launch {path}: {ex.Message}");
                ShowErrorDialog($"Failed to launch {path}: {ex.Message}");
            }
        }


        private void TryRunAsAdmin(string path, string args) {
            try {
                ProcessStartInfo psi = new ProcessStartInfo {
                    FileName = path,
                    Arguments = args,
                    Verb = "runas",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex) {
                Debug.WriteLine($"Failed to run as admin {path}: {ex.Message}");
                ShowErrorDialog($"Failed to run as admin {path}: {ex.Message}");
            }
        }

        private void OpenFileLocation(string path) {
            try {
                string directory = Path.GetDirectoryName(path);

                if (Directory.Exists(directory)) {
                    Process.Start("explorer.exe", directory);
                }
                else {
                    throw new Exception("Directory does not exist.");
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Failed to open file location {path}: {ex.Message}");
                ShowErrorDialog($"Failed to open file location {path}: {ex.Message}");
            }
        }

        // UI Helpers
        private void ShowErrorDialog(string message) {
            ContentDialog dialog = new ContentDialog {
                Title = "Error",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            _ = dialog.ShowAsync();
        }

        private MenuFlyout CreateItemFlyout() {
            MenuFlyout flyout = new MenuFlyout();

            MenuFlyoutItem openItem = new MenuFlyoutItem {
                Text = "Open",
                Icon = new FontIcon { Glyph = "\ue8a7" }
            };
            openItem.Click += OpenItem_Click;
            flyout.Items.Add(openItem);

            MenuFlyoutItem runAsAdminItem = new MenuFlyoutItem {
                Text = "Run as Administrator",
                Icon = new FontIcon { Glyph = "\uE7EF" }
            };
            runAsAdminItem.Click += RunAsAdminItem_Click;
            flyout.Items.Add(runAsAdminItem);

            MenuFlyoutItem fileLocationItem = new MenuFlyoutItem {
                Text = "Open File Location",
                Icon = new FontIcon { Glyph = "\ued43" }
            };
            fileLocationItem.Click += OpenFileLocation_Click;
            flyout.Items.Add(fileLocationItem);

            flyout.Items.Add(new MenuFlyoutSeparator());

            MenuFlyoutItem editItem = new MenuFlyoutItem {
                Text = "Edit this Group",
                Icon = new FontIcon { Glyph = "\ue70f" }
            };
            editItem.Click += EditGroup_Click;
            flyout.Items.Add(editItem);

            MenuFlyoutItem launchAll = new MenuFlyoutItem {
                Text = "Launch All",
                Icon = new FontIcon { Glyph = "\ue8a9" }
            };
            launchAll.Click += launchAllGroup_Click;



            flyout.Items.Add(launchAll);
            return flyout;
        }

        private async void launchAllGroup_Click(object sender, RoutedEventArgs e) {
            if (!string.IsNullOrEmpty(_groupFilter)) {
                // Find the group with the matching name
                var matchingGroup = _groups.Values.FirstOrDefault(g =>
                    g.groupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));

                // Check if we found a matching group
                if (matchingGroup != null) {
                    // Call the LaunchAll function with the matching group name
                    await JsonConfigHelper.LaunchAll(matchingGroup.groupName);
                }
            }
        }



        private void EditGroup_Click(object sender, RoutedEventArgs e) {
            EditGroupHelper editGroup = new EditGroupHelper("Edit Group", _groupId);
            editGroup.Activate();
        }

        private void OpenItem_Click(object sender, RoutedEventArgs e) {
            if (_clickedItem != null) {
                TryLaunchApp(_clickedItem.Path, _clickedItem.Args);
            }
        }

        private void RunAsAdminItem_Click(object sender, RoutedEventArgs e) {
            if (_clickedItem != null) {
                TryRunAsAdmin(_clickedItem.Path, _clickedItem.Args);
            }
        }

        private void OpenFileLocation_Click(object sender, RoutedEventArgs e) {
            if (_clickedItem != null) {
                OpenFileLocation(_clickedItem.Path);
            }
        }

        private string GetDisplayName(string filePath) {
            if (string.IsNullOrEmpty(filePath)) {
                return "Unknown";
            }

            string extension = Path.GetExtension(filePath).ToLower();

            if (string.IsNullOrEmpty(extension)) {
                return Path.GetFileName(filePath);
            }

            if (extension == ".exe") {
                try {
                    FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(filePath);
                    if (!string.IsNullOrEmpty(versionInfo.FileDescription)) {
                        return versionInfo.FileDescription;
                    }
                }
                catch (Exception) {
                    // Fall through to default case
                }
            }
            else if (extension == ".lnk") {
                try {
                    dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                    dynamic shortcut = shell.CreateShortcut(filePath);
                    string targetPath = shortcut.TargetPath;
                    if (!string.IsNullOrEmpty(targetPath)) {
                        return Path.GetFileNameWithoutExtension(targetPath);
                    }
                }
                catch (Exception) {
                }
            }

            return Path.GetFileNameWithoutExtension(filePath);
        }

        private Tuple<float, int, int> GetDisplayInformation() {
            var hwnd = WindowNative.GetWindowHandle(this);

            uint dpi = NativeMethods.GetDpiForWindow(hwnd);
            float scaleFactor = (float)dpi / 96.0f;

            IntPtr monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);

            NativeMethods.MONITORINFOEX monitorInfo = new NativeMethods.MONITORINFOEX();
            monitorInfo.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFOEX));
            NativeMethods.GetMonitorInfo(monitor, ref monitorInfo);

            int screenWidth = monitorInfo.rcMonitor.right - monitorInfo.rcMonitor.left;
            int screenHeight = monitorInfo.rcMonitor.bottom - monitorInfo.rcMonitor.top;

            return new Tuple<float, int, int>(scaleFactor, screenWidth, screenHeight);
        }
    }

}