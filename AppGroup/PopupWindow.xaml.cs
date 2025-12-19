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
using Windows.ApplicationModel.Background;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI.ViewManagement;
using Windows.UI.WindowManagement;
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
        public bool showLabels { get; set; } = false;  // Default: labels off for backward compatibility
        public int labelSize { get; set; } = 10;       // Default font size
    }

    public class PopupItem : INotifyPropertyChanged {
        public string Path { get; set; }
        public string Name { get; set; }
        public string ToolTip { get; set; }
        public string Args { get; set; }
        public string IconPath { get; set; } // Custom icon path from JSON
        public string CustomIconPath { get; set; } // Additional property to distinguish between default and custom

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
        private const int BUTTON_SIZE_WITH_LABEL = 56;
        private const int BUTTON_HEIGHT_HORIZONTAL_LABEL = 32;
        private const int BUTTON_WIDTH_HORIZONTAL_LABEL = 150;
        private const int ICON_SIZE = 24;
        private const int BUTTON_MARGIN = 4;
        private const int DEFAULT_LABEL_SIZE = 10;

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
        private DataTemplate _itemTemplateWithLabel;
        private DataTemplate _itemTemplateHorizontalLabel;
        private ItemsPanelTemplate _panelTemplate;
        private ItemsPanelTemplate _panelTemplateWithLabel;
        private ItemsPanelTemplate _panelTemplateHorizontalLabel;
        private nint hWnd;

        // Label settings for current group
        private bool _showLabels = false;
        private int _labelSize = DEFAULT_LABEL_SIZE;
        private int _currentColumns = 1;


        private string _originalIconPath;
        private string _iconWithBackgroundPath;
        private string iconGroup;
        // Add these fields to your class
        private static string _cachedAppFolderPath;
        private static string _cachedLastOpenPath;
        private UISettings _uiSettings; // Cache UISettings instance
        private bool _isUISettingsSubscribed = false;
        private readonly List<Task> _backgroundTasks = new List<Task>();
        private readonly List<Task> _iconLoadingTasks = new List<Task>();
        // Constructor
        public PopupWindow(string groupFilter = null) {
            InitializeComponent();

            _groupFilter = groupFilter;
            this.Title = "Popup Window";

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

            SetWindowIcon();

            //InitializeSystemTray();
            this.AppWindow.IsShownInSwitchers = false;

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
            // Create item template once (without labels)
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

            // Create panel template once (without labels)
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

            // Create item template with labels
            const int EFFECTIVE_BUTTON_WIDTH_WITH_LABEL = BUTTON_SIZE_WITH_LABEL + (BUTTON_MARGIN * 2);
            _itemTemplateWithLabel = (DataTemplate)XamlReader.Load(
     $@"<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <StackPanel VerticalAlignment=""Center""
          HorizontalAlignment=""Center""
          Width=""{BUTTON_SIZE_WITH_LABEL}""
          Height=""{BUTTON_SIZE_WITH_LABEL}""
          ToolTipService.ToolTip=""{{Binding ToolTip}}"">
        <Image Source=""{{Binding Icon}}""
               Width=""{ICON_SIZE}""
               Height=""{ICON_SIZE}""
               Stretch=""Uniform""
               HorizontalAlignment=""Center""
               Margin=""4,6,4,2"" />
        <TextBlock Text=""{{Binding ToolTip}}""
                   FontSize=""{DEFAULT_LABEL_SIZE}""
                   TextTrimming=""CharacterEllipsis""
                   TextAlignment=""Center""
                   HorizontalAlignment=""Center""
                   MaxWidth=""{BUTTON_SIZE_WITH_LABEL - 4}""
                   Opacity=""0.9"" />
    </StackPanel>
</DataTemplate>");

            // Create panel template with labels
            _panelTemplateWithLabel = (ItemsPanelTemplate)XamlReader.Load(
                $@"<ItemsPanelTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
                <ItemsWrapGrid Orientation=""Horizontal""
                              ItemWidth=""{EFFECTIVE_BUTTON_WIDTH_WITH_LABEL}""
                              ItemHeight=""{EFFECTIVE_BUTTON_WIDTH_WITH_LABEL}""
                              HorizontalAlignment=""Center""
                              VerticalAlignment=""Center""/>
            </ItemsPanelTemplate>");

            // Create item template with horizontal labels (for single column layout)
            const int EFFECTIVE_BUTTON_HEIGHT_HORIZONTAL = BUTTON_HEIGHT_HORIZONTAL_LABEL + (BUTTON_MARGIN * 2);
            _itemTemplateHorizontalLabel = (DataTemplate)XamlReader.Load(
     $@"<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <Grid Width=""{BUTTON_WIDTH_HORIZONTAL_LABEL}""
          Height=""{BUTTON_HEIGHT_HORIZONTAL_LABEL}""
          ToolTipService.ToolTip=""{{Binding ToolTip}}"">
        <StackPanel Orientation=""Horizontal""
              VerticalAlignment=""Center""
              HorizontalAlignment=""Left"">
            <Image Source=""{{Binding Icon}}""
                   Width=""{ICON_SIZE}""
                   Height=""{ICON_SIZE}""
                   Stretch=""Uniform""
                   VerticalAlignment=""Center""
                   Margin=""0,0,8,0"" />
            <TextBlock Text=""{{Binding ToolTip}}""
                       FontSize=""{DEFAULT_LABEL_SIZE}""
                       TextTrimming=""CharacterEllipsis""
                       VerticalAlignment=""Center""
                       MaxWidth=""{BUTTON_WIDTH_HORIZONTAL_LABEL - ICON_SIZE - 12}""
                       Opacity=""0.9"" />
        </StackPanel>
    </Grid>
</DataTemplate>");

            // Create panel template with horizontal labels
            _panelTemplateHorizontalLabel = (ItemsPanelTemplate)XamlReader.Load(
                $@"<ItemsPanelTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
                <ItemsWrapGrid Orientation=""Horizontal""
                              ItemWidth=""{BUTTON_WIDTH_HORIZONTAL_LABEL + (BUTTON_MARGIN * 2)}""
                              ItemHeight=""{EFFECTIVE_BUTTON_HEIGHT_HORIZONTAL}""
                              HorizontalAlignment=""Left""
                              VerticalAlignment=""Top""/>
            </ItemsPanelTemplate>");
        }

        // Load configuration with better error handling and caching
        private async void LoadConfiguration() {
            // Update taskbar icon with white background when window shows
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

            // Reset label settings
            _showLabels = false;
            _labelSize = DEFAULT_LABEL_SIZE;
            _currentColumns = 1;

            // If we have a group filter, only consider that group
            if (!string.IsNullOrEmpty(_groupFilter) && _groups.Values.Any(g => g.groupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase))) {
                var filteredGroup = _groups.FirstOrDefault(g => g.Value.groupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));

                maxPathItems = filteredGroup.Value.path.Count;
                maxColumns = filteredGroup.Value.groupCol;
                groupHeader = filteredGroup.Value.groupHeader;
                //groupIcon = filteredGroup.Value.groupIcon;
                iconGroup = filteredGroup.Value.groupIcon;

                // Get label settings
                _showLabels = filteredGroup.Value.showLabels;
                _labelSize = filteredGroup.Value.labelSize > 0 ? filteredGroup.Value.labelSize : DEFAULT_LABEL_SIZE;
                _currentColumns = maxColumns;

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
                _currentColumns = maxColumns;
            }

            // Determine if using horizontal labels (single column with labels)
            bool useHorizontalLabels = _showLabels && _currentColumns == 1;

            // Use appropriate button size based on label setting and layout
            int buttonWidth, buttonHeight;
            if (useHorizontalLabels) {
                buttonWidth = BUTTON_WIDTH_HORIZONTAL_LABEL;
                buttonHeight = BUTTON_HEIGHT_HORIZONTAL_LABEL;
            } else if (_showLabels) {
                buttonWidth = BUTTON_SIZE_WITH_LABEL;
                buttonHeight = BUTTON_SIZE_WITH_LABEL;
            } else {
                buttonWidth = BUTTON_SIZE;
                buttonHeight = BUTTON_SIZE;
            }

            int numberOfRows = (int)Math.Ceiling((double)maxPathItems / maxColumns);
            int dynamicWidth = maxColumns * (buttonWidth + BUTTON_MARGIN * 2);
            if (groupHeader == true && maxColumns < 2 && !useHorizontalLabels) {
                dynamicWidth = 2 * (buttonWidth + BUTTON_MARGIN * 2);
            }

            int dynamicHeight = numberOfRows * (buttonHeight + BUTTON_MARGIN * 2);
            var displayInfo = GetDisplayInformation();
            float scaleFactor = displayInfo.Item1;

            int scaledWidth = (int)(dynamicWidth * scaleFactor);
            int scaledHeight = (int)(dynamicHeight * scaleFactor);
            if (groupHeader) {
                scaledHeight += 40;
            }

            //var iconPath = Path.Combine(AppContext.BaseDirectory, "AppGroup.ico");

            //this.AppWindow.SetIcon(iconPath);

            MainGrid.Margin = new Thickness(0, 0, -5, -15);

            int finalWidth = scaledWidth + 30;
            int finalHeight = scaledHeight + 20;
            this.AppWindow.Resize(new SizeInt32(finalWidth, finalHeight));
            hWnd = WindowNative.GetWindowHandle(this);
            NativeMethods.PositionWindowAboveTaskbar(hWnd);

        
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

                // Configure GridView - select template based on labels and column count
                // Auto: horizontal labels for 1 column, vertical labels for 2+
                bool useHorizontalLabels = _showLabels && _currentColumns == 1;

                DataTemplate selectedItemTemplate;
                ItemsPanelTemplate selectedPanelTemplate;

                if (useHorizontalLabels) {
                    selectedItemTemplate = _itemTemplateHorizontalLabel;
                    selectedPanelTemplate = _panelTemplateHorizontalLabel;
                } else if (_showLabels) {
                    selectedItemTemplate = _itemTemplateWithLabel;
                    selectedPanelTemplate = _panelTemplateWithLabel;
                } else {
                    selectedItemTemplate = _itemTemplate;
                    selectedPanelTemplate = _panelTemplate;
                }

                _gridView = new GridView {
                    SelectionMode = ListViewSelectionMode.Extended,
                    IsItemClickEnabled = true,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    CanDragItems = true,
                    CanReorderItems = true,
                    AllowDrop = true,
                    ItemTemplate = selectedItemTemplate,
                    ItemsPanel = selectedPanelTemplate
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

        // Add these fields to your PopupWindow class
        //private string _originalIconPath;
        private string _currentGridIconPath;
        private bool _isGridIcon = false;

        // Add this method to PopupWindow class
        private async Task CreateGridIconFromReorder() {
            try {
                if (PopupItems == null || !PopupItems.Any()) {
                    Debug.WriteLine("No items available for grid icon creation");
                    return;
                }

                // Get the group information
                var filteredGroup = _groups.FirstOrDefault(g => g.Value.groupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));
                if (filteredGroup.Key == null) {
                    Debug.WriteLine($"Error: Group '{_groupFilter}' not found");
                    return;
                }

                // Determine if this is a grid icon based on the current icon path
                string currentIcon = filteredGroup.Value.groupIcon;
                bool isCurrentlyGridIcon = currentIcon.Contains("grid");

                if (!isCurrentlyGridIcon) {
                    Debug.WriteLine("Current icon is not a grid icon, skipping grid recreation");
                    return;
                }

                // Determine grid size from current icon name
                int gridSize = currentIcon.Contains("grid3") ? 3 : 2;

                // Take items up to grid size limit
                var gridItems = PopupItems.Take(gridSize * gridSize).Select(item => new ExeFileModel {
                    FileName = item.Name,
                    FilePath = item.Path,
                    Icon = item.Icon?.UriSource?.LocalPath ?? "", // Get the actual icon path
                    Tooltip = item.ToolTip,
                    Args = item.Args,
                    IconPath = item.CustomIconPath
                }).ToList();

                // Create the grid icon
                IconHelper iconHelper = new IconHelper();
                string newGridIconPath = await iconHelper.CreateGridIconForPopupAsync(
                    gridItems,
                    gridSize,
                    _groupFilter
                );

                if (!string.IsNullOrEmpty(newGridIconPath)) {
                    _currentGridIconPath = newGridIconPath;

                    // Update the shortcut and JSON configuration
                    await UpdateShortcutAndConfig(newGridIconPath, gridSize);
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error creating grid icon: {ex.Message}");
            }
        }

        private async Task UpdateShortcutAndConfig(string newIconPath, int gridSize) {
            try {
                string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appDataPath = Path.Combine(localAppDataPath, "AppGroup");
                string groupsFolder = Path.Combine(appDataPath, "Groups");

                string groupFolder = Path.Combine(groupsFolder, _groupFilter);
                if (!Directory.Exists(groupFolder)) {
                    Debug.WriteLine($"Group folder not found: {groupFolder}");
                    return;
                }

                // Update the shortcut icon
                string shortcutPath = Path.Combine(groupFolder, $"{_groupFilter}.lnk");
                if (File.Exists(shortcutPath)) {
                    IWshShell wshShell = new WshShell();
                    IWshShortcut shortcut = (IWshShortcut)wshShell.CreateShortcut(shortcutPath);
                    shortcut.IconLocation = newIconPath;
                    shortcut.Save();

                    Debug.WriteLine($"Updated shortcut icon: {shortcutPath}");
                }

                // Update the JSON configuration with new icon path and reordered items
                await UpdateJsonConfiguration(newIconPath, gridSize);

                // Update taskbar if pinned
                bool isPinned = await TaskbarManager.IsShortcutPinnedToTaskbar(_groupFilter);
                if (isPinned) {
                    await TaskbarManager.UpdateTaskbarShortcutIcon(_groupFilter, newIconPath);
                    TaskbarManager.TryRefreshTaskbarWithoutRestartAsync();
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error updating shortcut and config: {ex.Message}");
            }
        }

        private async Task UpdateJsonConfiguration(string newIconPath, int gridSize) {
            try {
                var filteredGroup = _groups.FirstOrDefault(g => g.Value.groupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));
                if (filteredGroup.Key == null) return;

                if (!int.TryParse(filteredGroup.Key, out int groupId)) {
                    Debug.WriteLine($"Error: Group key '{filteredGroup.Key}' is not a valid integer ID");
                    return;
                }

                // Create the reordered paths dictionary with custom icons preserved
                Dictionary<string, (string tooltip, string args, string icon)> reorderedPaths =
                    new Dictionary<string, (string tooltip, string args, string icon)>();

                foreach (var item in PopupItems) {
                    string customIcon = !string.IsNullOrEmpty(item.CustomIconPath) ? item.CustomIconPath : "";
                    reorderedPaths[item.Path] = (item.ToolTip, item.Args, customIcon);
                }

                // Update JSON with new icon path and reordered items
                JsonConfigHelper.AddGroupToJson(
                    JsonConfigHelper.GetDefaultConfigPath(),
                    groupId,
                    filteredGroup.Value.groupName,
                    filteredGroup.Value.groupHeader,
                    newIconPath, // Use the new grid icon path
                    filteredGroup.Value.groupCol,
                    filteredGroup.Value.showLabels,
                    filteredGroup.Value.labelSize > 0 ? filteredGroup.Value.labelSize : 10,
                    reorderedPaths
                );

                // Reload the JSON to reflect changes
                string configPath = JsonConfigHelper.GetDefaultConfigPath();
                _json = JsonConfigHelper.ReadJsonFromFile(configPath);
                _groups = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, GroupData>>(_json, JsonOptions);

                Debug.WriteLine($"Updated JSON configuration with new icon: {newIconPath}");
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error updating JSON configuration: {ex.Message}");
            }
        }

        // Update the existing GridView_DragItemsCompleted method in PopupWindow
        private async void GridView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) {
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

                // Create a new dictionary to hold the reordered paths with their properties INCLUDING custom icons
                Dictionary<string, (string tooltip, string args, string icon)> newPathOrder = new Dictionary<string, (string tooltip, string args, string icon)>();
                foreach (var item in PopupItems) {
                    // Include the custom icon path when reordering
                    string customIcon = !string.IsNullOrEmpty(item.CustomIconPath) ? item.CustomIconPath : "";
                    newPathOrder[item.Path] = (item.ToolTip, item.Args, customIcon);
                }

                if (!int.TryParse(filteredGroup.Key, out int groupId)) {
                    Debug.WriteLine($"Error: Group key '{filteredGroup.Key}' is not a valid integer ID");
                    return;
                }

                // Check if current icon is a grid icon and regenerate if needed
                string currentIcon = filteredGroup.Value.groupIcon;
                bool isGridIcon = currentIcon.Contains("grid");

                if (isGridIcon) {
                    // Regenerate the grid icon with new order
                    await CreateGridIconFromReorder();
                }
                else {
                    // Just update the JSON with reordered items (no icon change needed)
                    JsonConfigHelper.AddGroupToJson(
                        JsonConfigHelper.GetDefaultConfigPath(),
                        groupId,
                        filteredGroup.Value.groupName,
                        filteredGroup.Value.groupHeader,
                        filteredGroup.Value.groupIcon,
                        filteredGroup.Value.groupCol,
                        filteredGroup.Value.showLabels,
                        filteredGroup.Value.labelSize > 0 ? filteredGroup.Value.labelSize : 10,
                        newPathOrder
                    );
                }

                _json = File.ReadAllText(JsonConfigHelper.GetDefaultConfigPath());
                Debug.WriteLine("Successfully updated configuration after drag reorder");
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error in GridView_DragItemsCompleted: {ex.Message}");
                ShowErrorDialog($"Failed to save new item order: {ex.Message}");
            }
        }

        private void LoadGridItems(Dictionary<string, Dictionary<string, string>> pathsWithProperties) {
            foreach (var pathEntry in pathsWithProperties) {
                string path = pathEntry.Key;
                var properties = pathEntry.Value;

                // Get displayName from properties if available, otherwise use default method
                string tooltip = properties.ContainsKey("tooltip") && !string.IsNullOrEmpty(properties["tooltip"])
                    ? properties["tooltip"]
                    : GetDisplayName(path);

                // Get custom icon path from JSON if available
                string customIconPath = properties.ContainsKey("icon") && !string.IsNullOrEmpty(properties["icon"])
                    ? properties["icon"]
                    : null;

                var popupItem = new PopupItem {
                    Path = path,
                    Name = Path.GetFileNameWithoutExtension(path),
                    ToolTip = tooltip,
                    Icon = null,
                    Args = properties.ContainsKey("args") ? properties["args"] : "",
                    IconPath = customIconPath, // Store the custom icon path
                    CustomIconPath = customIconPath // Additional tracking
                };

                PopupItems.Add(popupItem);
                _ = LoadIconAsync(popupItem, path);
            }
        }


        private async Task LoadIconAsync(PopupItem item, string path) {
            try {
                string iconPath;

                // Use custom icon if available and file exists
                if (!string.IsNullOrEmpty(item.CustomIconPath) && File.Exists(item.CustomIconPath)) {
                    iconPath = item.CustomIconPath;
                }
                else {
                    // Fall back to cached icon
                    iconPath = await IconCache.GetIconPathAsync(path);
                }

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


        //private void GridView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) {
        //    try {
        //        if (_groups == null || string.IsNullOrEmpty(_groupFilter)) {
        //            Debug.WriteLine("Error: Unable to deserialize groups or group filter is not set");
        //            return;
        //        }

        //        var filteredGroup = _groups.FirstOrDefault(g => g.Value.groupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));

        //        if (filteredGroup.Key == null) {
        //            Debug.WriteLine($"Error: Group '{_groupFilter}' not found in configuration");
        //            return;
        //        }

        //        // Create a new dictionary to hold the reordered paths with their properties INCLUDING custom icons
        //        Dictionary<string, (string tooltip, string args, string icon)> newPathOrder = new Dictionary<string, (string tooltip, string args, string icon)>();
        //        foreach (var item in PopupItems) {
        //            // Include the custom icon path when reordering
        //            string customIcon = !string.IsNullOrEmpty(item.CustomIconPath) ? item.CustomIconPath : "";
        //            newPathOrder[item.Path] = (item.ToolTip, item.Args, customIcon);
        //        }

        //        if (!int.TryParse(filteredGroup.Key, out int groupId)) {
        //            Debug.WriteLine($"Error: Group key '{filteredGroup.Key}' is not a valid integer ID");
        //            return;
        //        }

        //        JsonConfigHelper.AddGroupToJson(
        //            JsonConfigHelper.GetDefaultConfigPath(),
        //            groupId,
        //            filteredGroup.Value.groupName,
        //            filteredGroup.Value.groupHeader,
        //            filteredGroup.Value.groupIcon,
        //            filteredGroup.Value.groupCol,
        //            newPathOrder
        //        );

        //        _json = File.ReadAllText(JsonConfigHelper.GetDefaultConfigPath());
        //    }
        //    catch (Exception ex) {
        //        Debug.WriteLine($"Error in GridView_DragItemsCompleted: {ex.Message}");
        //        ShowErrorDialog($"Failed to save new item order: {ex.Message}");
        //    }
        //}


       
        private async void Window_Activated(object sender, WindowActivatedEventArgs e) {
            if (e.WindowActivationState == WindowActivationState.Deactivated) {
                var settings = await SettingsHelper.LoadSettingsAsync();

                // FIRST: Cleanup UISettings to prevent event handler accumulation
                CleanupUISettings();

                if (!string.IsNullOrEmpty(_originalIconPath) && !string.IsNullOrEmpty(_groupFilter)) {
                    // Hide window immediately
                    this.DispatcherQueue.TryEnqueue(() => {
                        this.Hide();
                    });

                    if (settings.UseGrayscaleIcon) {
                        var task = Task.Run(async () => {
                            try {
                                await TaskbarManager.UpdateTaskbarShortcutIcon(_groupFilter, iconGroup);
                                if (!string.IsNullOrEmpty(_iconWithBackgroundPath)) {
                                    IconHelper.RemoveBackgroundIcon(_iconWithBackgroundPath);
                                    _iconWithBackgroundPath = null;
                                }
                            }
                            catch (Exception ex) {
                                Debug.WriteLine($"Background cleanup error: {ex.Message}");
                            }
                        });
                        _backgroundTasks.Add(task);
                    }

                    // UI cleanup
                    this.DispatcherQueue.TryEnqueue(() => {
                        try {
                            if (_gridView != null) {
                                _gridView.RightTapped -= GridView_RightTapped;
                                _gridView.DragItemsCompleted -= GridView_DragItemsCompleted;
                                _gridView.ItemClick -= GridView_ItemClick;
                            }

                            // Improved image cleanup
                            foreach (var item in PopupItems) {
                                if (item.Icon != null) {
                                    item.Icon.UriSource = null;
                                    item.Icon = null;
                                }
                            }
                            PopupItems.Clear();
                            GridPanel.Children.Clear();

                            // Cleanup task lists
                            foreach (var task in _backgroundTasks.ToList()) {
                                if (task.IsCompleted) {
                                    task.Dispose();
                                    _backgroundTasks.Remove(task);
                                }
                            }
                            foreach (var task in _iconLoadingTasks.ToList()) {
                                if (task.IsCompleted) {
                                    task.Dispose();
                                    _iconLoadingTasks.Remove(task);
                                }
                            }

                            _groups = null;
                            _json = "";
                            _clickedItem = null;
                            _gridView = null;
                        }
                        catch (Exception ex) {
                            Debug.WriteLine($"UI cleanup error: {ex.Message}");
                        }
                    });

                    _ = Task.Run(() => {
                        GC.Collect(0, GCCollectionMode.Optimized); 
                    });
                }
            }
            else if (e.WindowActivationState == WindowActivationState.CodeActivated || e.WindowActivationState == WindowActivationState.PointerActivated) {
                // Cache file paths to avoid repeated Path.Combine operations

                if (_cachedAppFolderPath == null) {
                    string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    _cachedAppFolderPath = Path.Combine(appDataPath, "AppGroup");
                    _cachedLastOpenPath = Path.Combine(_cachedAppFolderPath, "lastOpen");
                }

                // Read group filter from file each time window is activated
                try {
                    if (File.Exists(_cachedLastOpenPath)) {
                        string fileGroupFilter = File.ReadAllText(_cachedLastOpenPath).Trim();
                        if (!string.IsNullOrEmpty(fileGroupFilter)) {
                            _groupFilter = fileGroupFilter;
                        }
                    }
                }
                catch (Exception ex) {
                    Debug.WriteLine($"Error reading group name from file: {ex.Message}");
                }

                // Subscribe to UISettings only once
                if (!_isUISettingsSubscribed) {
                    _uiSettings ??= new UISettings();
                    _uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;
                    _isUISettingsSubscribed = true;
                }

                // Initial update to set the background color based on the current settings
                UpdateMainGridBackground(_uiSettings);

                // Do heavy operations in background after window is shown
              

                // Load configuration asynchronously to not block UI
                _ = this.DispatcherQueue.TryEnqueue(() => {
                    try {
                        LoadConfiguration();
                          _ = Task.Run(async () => {
                                try {
                                    await UpdateTaskbarIcon(_groupFilter);
                                }
                                catch (Exception ex) {
                                    Debug.WriteLine($"Background taskbar update error: {ex.Message}");
                                }
                         });
                    }
                    catch (Exception ex) {
                        Debug.WriteLine($"Configuration loading error: {ex.Message}");
                    }
                });
            }
        }

        // Add this cleanup method to your class
        private void CleanupUISettings() {
            if (_isUISettingsSubscribed && _uiSettings != null) {
                _uiSettings.ColorValuesChanged -= UiSettings_ColorValuesChanged;
                _isUISettingsSubscribed = false;
            }
        }
        private async Task UpdateTaskbarIcon(string groupName) {
            var settings = await SettingsHelper.LoadSettingsAsync();

            try {
                // Determine the icon path based on your structure
                string basePath = Path.Combine("Groups", groupName, groupName);
                string iconPath;
                string groupIcon = IconHelper.FindOrigIcon(iconGroup);

                // Load settings to check grayscale preference
             

                _originalIconPath = groupIcon;

              

                if (!string.IsNullOrEmpty(_originalIconPath) && File.Exists(_originalIconPath)) {
                    if (settings.UseGrayscaleIcon) {
                    _iconWithBackgroundPath = await IconHelper.CreateBlackWhiteIconAsync(_originalIconPath);

                        await TaskbarManager.UpdateTaskbarShortcutIcon(groupName, _iconWithBackgroundPath);
                    }
                 
                }
            }
            catch (Exception ex) {
                Console.WriteLine($"Error updating taskbar icon with background: {ex.Message}");
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
                Path.GetFileNameWithoutExtension(filePath);
                //try {
                //    dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                //    dynamic shortcut = shell.CreateShortcut(filePath);
                //    string targetPath = shortcut.TargetPath;
                //    if (!string.IsNullOrEmpty(targetPath)) {
                //        return Path.GetFileNameWithoutExtension(targetPath);
                //    }
                //}
                //catch (Exception) {
                //}
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