using IWshRuntimeLibrary;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.UI.ViewManagement;
using WinRT.Interop;
using WinUIEx;
using File = System.IO.File;

namespace AppGroup {
    public class PathData {
        public string Tooltip { get; set; }
        public string Args { get; set; }
        public string Icon { get; set; }
    }
    public class GroupData {
        public required string GroupIcon { get; set; }
        public required string GroupName { get; set; }
        public bool GroupHeader { get; set; }
        public int GroupCol { get; set; }
        public int GroupId { get; set; }
        public bool ShowLabels { get; set; } = false;
        public int LabelSize { get; set; } = 12;
        public string LabelPosition { get; set; } = "Bottom";
        public string HeaderPosition { get; set; } = "Top";
        public string Layout { get; set; } = "Default";
        public Dictionary<string, PathData> Path { get; set; }
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

        public bool IsSubgroup { get; set; }
        public string SubgroupName { get; set; }
    }
    public sealed partial class PopupWindow : Window {

        // Constants for UI elements
        private const int BUTTON_SIZE = 40;
        private const int BUTTON_SIZE_WITH_LABEL = 56;
        private const int BUTTON_HEIGHT_HORIZONTAL_LABEL = 40;  // Same as BUTTON_SIZE for consistent height
        private const int BUTTON_WIDTH_HORIZONTAL_LABEL = 180;
        private const int ICON_SIZE = 24;
        private const int BUTTON_MARGIN = 4;
        private const int DEFAULT_LABEL_SIZE = 12;
        private const string DEFAULT_LABEL_POSITION = "Bottom";
        private bool _hasBeenLoaded = false;

        // Add these constants to PopupWindow class

        private IntPtr _hwnd;
        private IntPtr _oldWndProc;
        private NativeMethods.WndProcDelegate _newWndProc; // Keep reference to prevent GC


        // Static JSON options to prevent redundant creation
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
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

        // Label settings for current group
        private bool _showLabels = false;
        private int _labelSize = DEFAULT_LABEL_SIZE;
        private string _labelPosition = DEFAULT_LABEL_POSITION;
        private int _currentColumns = 1;


        private string _originalIconPath;
        private string _iconWithBackgroundPath;
        private string iconGroup;
        private static string _cachedAppFolderPath;
        private static string _cachedLastOpenPath;
        private UISettings _uiSettings;
        private bool _isUISettingsSubscribed = false;
        private readonly List<Task> _backgroundTasks = new List<Task>();
        private readonly List<Task> _iconLoadingTasks = new List<Task>();

        private bool useFileMode = false;

        private NativeMethods.SubclassProc _subclassProc;
        private const int SUBCLASS_ID = 1;
        private readonly Dictionary<string, PopupWindow> _openSubPopups = new Dictionary<string, PopupWindow>();
        private PopupWindow _parentPopup = null;
        private Storyboard _entranceStoryboard;

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
            _windowHelper.IsResizable = false;
            _windowHelper.HasBorder = true;
            _windowHelper.HasTitleBar = false;
            _windowHelper.IsAlwaysOnTop = true;

            int screenHeight = (int)(DisplayArea.Primary.WorkArea.Height) * 2;
            int screenWidth = (int)(DisplayArea.Primary.WorkArea.Width) * 2;

            NativeMethods.PositionWindowOffScreen(this.GetWindowHandle());
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(screenWidth, screenHeight));
            this.Hide();


            InitializeTemplates();

            SetWindowIcon();
            if (!useFileMode) {
                _hwnd = WindowNative.GetWindowHandle(this);
                SubclassWindow();

            }


            this.AppWindow.IsShownInSwitchers = false;
            this.Activated += Window_Activated;
        }

        private void AnimateWindowSlideUp(IntPtr hWnd, bool isSubPopup = false) {
            NativeMethods.GetWindowRect(hWnd, out NativeMethods.RECT rect);
            int finalX = rect.left;
            int finalY = rect.top;
            int startY = finalY + 60;

            if (!isSubPopup) {
                NativeMethods.SetWindowPos(hWnd, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            }

            NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, finalX, startY, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOWNOACTIVATE);

            int intervalMs = GetRefreshIntervalMs(hWnd);
            int durationMs = 200;
            int steps = durationMs / intervalMs;
            int currentStep = 0;

            var timer = new System.Threading.Timer(_ => {
                currentStep++;
                double t = (double)currentStep / steps;
                double ease = Math.Sqrt(1 - Math.Pow(t - 1, 2));
                int currentY = (int)(startY + (finalY - startY) * ease);

                NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, finalX, currentY, 0, 0,
                    NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

                if (currentStep >= steps) {
                    NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, finalX, finalY, 0, 0,
                        NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

                    if (!isSubPopup) {
                        NativeMethods.SetWindowPos(hWnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
                    }
                }
            }, null, intervalMs, intervalMs);

            Task.Delay(durationMs + intervalMs * 2).ContinueWith(_ => timer.Dispose());
        }

        private void AnimateWindowSlideDown(IntPtr hWnd, Action onComplete) {
            NativeMethods.GetWindowRect(hWnd, out NativeMethods.RECT rect);
            int startX = rect.left;
            int startY = rect.top;
            int endY = startY + 60;

            // Remove always-on-top so it slides behind taskbar
            NativeMethods.SetWindowPos(hWnd, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

            int intervalMs = GetRefreshIntervalMs(hWnd);
            int durationMs = 150;
            int steps = durationMs / intervalMs;
            int currentStep = 0;

            var timer = new System.Threading.Timer(_ => {
                currentStep++;
                double t = (double)currentStep / steps;
                double ease = 1 - Math.Sqrt(1 - Math.Pow(t, 2));
                int currentY = (int)(startY + (endY - startY) * ease);

                NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, startX, currentY, 0, 0,
                    NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

                if (currentStep >= steps) {
                    onComplete?.Invoke();
                }
            }, null, intervalMs, intervalMs);

            Task.Delay(durationMs + intervalMs * 2).ContinueWith(_ => timer.Dispose());
        }
        private int GetRefreshIntervalMs(IntPtr hWnd) {
            try {
                IntPtr monitor = NativeMethods.MonitorFromWindow(hWnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
                NativeMethods.MONITORINFOEX monitorInfo = new NativeMethods.MONITORINFOEX();
                monitorInfo.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFOEX));
                NativeMethods.GetMonitorInfo(monitor, ref monitorInfo);

                NativeMethods.DEVMODE devMode = new NativeMethods.DEVMODE();
                devMode.dmSize = (short)Marshal.SizeOf(typeof(NativeMethods.DEVMODE));
                if (NativeMethods.EnumDisplaySettings(monitorInfo.szDevice, -1, ref devMode)) {
                    int refreshRate = (int)devMode.dmDisplayFrequency;
                    if (refreshRate > 0) return 1000 / refreshRate;
                }
            }
            catch { }
            return 16; // fallback 60hz
        }

        private void UiSettings_ColorValuesChanged(UISettings sender, object args) {
            // Update the MainGrid background color based on the current settings
            DispatcherQueue.TryEnqueue(() => {
                UpdateMainGridBackground(sender);
            });
        }
        private bool IsAlreadyOpenInChain(string groupName) {
            foreach (var sub in _openSubPopups.Values) {
                if (sub._groupFilter?.Equals(groupName, StringComparison.OrdinalIgnoreCase) == true)
                    return true;
                if (sub.IsAlreadyOpenInChain(groupName))
                    return true;
            }
            return false;
        }
        private void SubclassWindow() {
            try {
                // Create delegate and keep reference to prevent GC
                _subclassProc = new NativeMethods.SubclassProc(SubclassProc);

                // Use SetWindowSubclass instead of SetWindowLongPtr
                bool success = NativeMethods.SetWindowSubclass(
                    _hwnd,
                    _subclassProc,
                    SUBCLASS_ID,
                    IntPtr.Zero);

                if (success) {
                    Debug.WriteLine("Window subclassed successfully");
                }
                else {
                    Debug.WriteLine($"Failed to subclass window. Error: {Marshal.GetLastWin32Error()}");
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Failed to subclass window: {ex.Message}");
            }
        }

        // Subclass procedure
        private IntPtr SubclassProc(
     IntPtr hWnd,
     uint msg,
     IntPtr wParam,
     IntPtr lParam,
     IntPtr uIdSubclass,
     IntPtr dwRefData) {

            // Handle WM_COPYDATA for string messages
            if (msg == NativeMethods.WM_COPYDATA) {
                try {
                    NativeMethods.COPYDATASTRUCT cds = (NativeMethods.COPYDATASTRUCT)Marshal.PtrToStructure(
                        lParam, typeof(NativeMethods.COPYDATASTRUCT));

                    // Check the dwData to identify message type
                    if (cds.dwData == (IntPtr)100) {
                        string groupName = Marshal.PtrToStringUni(cds.lpData);
                        Debug.WriteLine($"Received WM_COPYDATA message with groupName: {groupName}");

                        this.DispatcherQueue.TryEnqueue(async () => {
                            try {
                                // ← ADD: ignore if already open as sub-popup in chain
                                if (IsAlreadyOpenInChain(groupName)) {
                                    Debug.WriteLine($"Ignoring WM_COPYDATA for '{groupName}' - already open as sub-popup");
                                    return;
                                }

                                _groupFilter = groupName;
                                Debug.WriteLine($"Updated group filter to: {_groupFilter}");
                                await LoadConfiguration();
                            }
                            catch (Exception ex) {
                                Debug.WriteLine($"Error updating group: {ex.Message}");
                            }
                        });

                        return (IntPtr)1;
                    }
                }
                catch (Exception ex) {
                    Debug.WriteLine($"Error in WM_COPYDATA handler: {ex.Message}");
                }
            }

            

            return NativeMethods.DefSubclassProc(hWnd, msg, wParam, lParam);
        }
        private void UpdateMainGridBackground(UISettings uiSettings) {
            // Check if the accent color is being shown on Start and taskbar
            if (IsAccentColorOnStartTaskbarEnabled()) {

                if (Content is FrameworkElement rootElement) {
                    rootElement.RequestedTheme = ElementTheme.Dark;
                }
                // Get current app theme
                var appTheme = Application.Current.RequestedTheme;

                // Use SystemAccentColorDark2 for Light mode, Dark3 for Dark mode
                string accentResourceKey = appTheme == ApplicationTheme.Light
                    ? "SystemAccentColorDark2"
                    : "SystemAccentColorDark2";


                if (Application.Current.Resources.TryGetValue(accentResourceKey, out object accentColor)) {
                    var acrylicBrush = new Microsoft.UI.Xaml.Media.AcrylicBrush {
                        TintColor = (Windows.UI.Color)accentColor,
                        TintOpacity = 0.8,
                        FallbackColor = (Windows.UI.Color)accentColor
                    };
                    MainGrid.Background = acrylicBrush;
                }
            }
            else {
                MainGrid.Background = null;

                // Follow window mode theme (not app mode)
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) {
                    if (key != null) {
                        object value = key.GetValue("SystemUsesLightTheme"); // window mode registry key
                        bool isWindowLightTheme = value != null && (int)value == 1;

                        if (Content is FrameworkElement rootElement) {
                            rootElement.RequestedTheme = isWindowLightTheme
                                ? ElementTheme.Light
                                : ElementTheme.Dark;
                        }
                    }
                }
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
UseLayoutRounding=""True""
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

            // Label templates will be created dynamically in CreateLabelTemplates() with the actual font size
        }

        // Create label templates with the specified font size
        private void CreateLabelTemplates(int fontSize) {
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
                   FontSize=""{fontSize}""
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
                   Margin=""8,0,8,0"" />
            <TextBlock Text=""{{Binding ToolTip}}""
                       FontSize=""{fontSize}""
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
                              VerticalAlignment=""Center""/>
            </ItemsPanelTemplate>");
        }

        // Load configuration with better error handling and caching
        private DateTime _lastConfigLoad = DateTime.MinValue;

        // LoadConfiguration
        private bool _isLoadingConfig = false;
        private async Task LoadConfiguration() {
            if (_isLoadingConfig) return; // prevent concurrent loads
            _isLoadingConfig = true;
            try {
                string configPath = JsonConfigHelper.GetDefaultConfigPath();

                var lastWrite = File.GetLastWriteTime(configPath);
                if (_groups == null || lastWrite > _lastConfigLoad) {
                    _groups = await Task.Run(() => {
                        string json = JsonConfigHelper.ReadJsonFromFile(configPath);
                        return JsonSerializer.Deserialize<Dictionary<string, GroupData>>(json, JsonOptions);
                    });
                    _lastConfigLoad = lastWrite;
                }

                if (_groups != null) {
                    InitializeWindow();
                    await CreateDynamicContent();

                    if (!string.IsNullOrEmpty(_groupFilter)) {
                        var filteredGroup = _groups.FirstOrDefault(g =>
                            g.Value.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));
                        if (filteredGroup.Key != null) {
                            string headerPosition = filteredGroup.Value.HeaderPosition ?? "Top";
                            string layout = filteredGroup.Value.Layout ?? "Default";
                            bool groupHeader = filteredGroup.Value.GroupHeader ;
                            ApplyGroupLayout(headerPosition, layout, groupHeader);
                        }
                    }
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error loading configuration: {ex.Message}");
            }
            finally {
                _isLoadingConfig = false;
            }
        }
        // CreateDynamicContent
        private async Task CreateDynamicContent() {
            PopupItems.Clear();
            GridPanel.Children.Clear();
            GridPanel.Opacity = 0;
            HeaderText.Text = "";
            _anyGroupDisplayed = false;

            foreach (var group in _groups) {
                if (_groupFilter != null && !group.Value.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                _anyGroupDisplayed = true;

                if (group.Value.GroupHeader) {
                    Header.Visibility = Visibility.Visible;
                    HeaderText.Text = group.Value.GroupName;
                    ScrollView.Margin = new Thickness(0, 0, 0, 5);
                }
                else {
                    Header.Visibility = Visibility.Collapsed;
                    ScrollView.Margin = new Thickness(0, 5, 0, 5);
                }

                bool useHorizontalLabels = _showLabels && _labelPosition == "Right";
                DataTemplate selectedItemTemplate;
                ItemsPanelTemplate selectedPanelTemplate;

                if (useHorizontalLabels) {
                    selectedItemTemplate = _itemTemplateHorizontalLabel;
                    selectedPanelTemplate = _panelTemplateHorizontalLabel;
                }
                else if (_showLabels) {
                    selectedItemTemplate = _itemTemplateWithLabel;
                    selectedPanelTemplate = _panelTemplateWithLabel;
                }
                else {
                    selectedItemTemplate = _itemTemplate;
                    selectedPanelTemplate = _panelTemplate;
                }

                _gridView = new GridView {
                    SelectionMode = ListViewSelectionMode.None,
                    IsItemClickEnabled = true,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    CanDragItems = true,
                    CanReorderItems = true,
                    AllowDrop = true,
                    ItemTemplate = selectedItemTemplate,
                    ItemsPanel = selectedPanelTemplate,
                };
                _gridView.ItemContainerTransitions = null;
                _gridView.Transitions = null;

                _gridView.RightTapped += GridView_RightTapped;
                _gridView.DragItemsCompleted += GridView_DragItemsCompleted;
                _gridView.ItemClick += GridView_ItemClick;


                var currentGridView = _gridView; // ← capture before await

                await LoadGridItems(group.Value.Path);

                if (currentGridView == null) return; // ← guard after await

                currentGridView.ItemsSource = PopupItems;
                GridPanel.Children.Add(currentGridView);
                _gridView = currentGridView;


                if (!_anyGroupDisplayed) {
                    GridPanel.Children.Add(new TextBlock {
                        Text = $"No group found matching '{_groupFilter}'",
                        Margin = new Thickness(10),
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextAlignment = TextAlignment.Center
                    });
                    this.AppWindow.Resize(new SizeInt32(250, 120));
                }
            }
        }
        private string GetDefaultJsonConfiguration() {
            return @"{
        ""1"": {
            ""groupCol"": 3,
            ""groupIcon"": ""AppGroup.ico"",
            ""groupName"": ""Group1NameHere"",
            ""groupHeader"": false,
            ""showLabels"": false,
            ""labelSize"": 12,
            ""labelPosition"": ""Bottom"",
            ""path"": {
                ""C:\\Windows\\System32\\notepad.exe"": { ""tooltip"": """", ""args"": """", ""icon"": """" },
                ""C:\\Windows\\System32\\calc.exe"": { ""tooltip"": """", ""args"": """", ""icon"": """" }
            }
        }
    }";
        }

        // Non-async window initialization for faster loading
        private void InitializeWindow() {

            int maxPathItems = 1;
            int maxColumns = 1;
            string groupIcon = "AppGroup.ico";
            bool groupHeader = false;
            string headerPosition = "top";
            string layout = "Default";
            // Reset label settings
            _showLabels = false;
            _labelSize = DEFAULT_LABEL_SIZE;
            _labelPosition = DEFAULT_LABEL_POSITION;
            _currentColumns = 1;
          
            // If we have a group filter, only consider that group
            if (!string.IsNullOrEmpty(_groupFilter) && _groups.Values.Any(g => g.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase))) {
                var filteredGroup = _groups.FirstOrDefault(g => g.Value.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));

                maxPathItems = filteredGroup.Value.Path.Count;
                maxColumns = filteredGroup.Value.GroupCol;
                groupHeader = filteredGroup.Value.GroupHeader;
                //groupIcon = filteredGroup.Value.groupIcon;
                iconGroup = filteredGroup.Value.GroupIcon;

                // Get label settings
                _showLabels = filteredGroup.Value.ShowLabels;
                _labelSize = filteredGroup.Value.LabelSize > 0 ? filteredGroup.Value.LabelSize : DEFAULT_LABEL_SIZE;
                _labelPosition = filteredGroup.Value.LabelPosition != null ? filteredGroup.Value.LabelPosition : DEFAULT_LABEL_POSITION;

                _currentColumns = maxColumns;
                 headerPosition = filteredGroup.Value.HeaderPosition ?? "Top";
                 layout = filteredGroup.Value.Layout ?? "Default";
               
                // Create label templates with the actual font size from config
                if (_showLabels) {
                    CreateLabelTemplates(_labelSize);
                }

                if (!int.TryParse(filteredGroup.Key, out _groupId)) {
                    Debug.WriteLine($"Error: Group key '{filteredGroup.Key}' is not a valid integer ID");
                    return;
                }
            }
            else {
                foreach (var group in _groups.Values) {
                    maxPathItems = Math.Max(maxPathItems, group.Path.Count);
                    maxColumns = Math.Max(maxColumns, group.GroupCol);
                }
                _currentColumns = maxColumns;
            }
            DispatcherQueue.TryEnqueue(() => {
                ApplyGroupLayout(headerPosition, layout, groupHeader);
            });

            // Determine if using horizontal labels (single column with labels)
            bool useHorizontalLabels = _showLabels && _labelPosition == "Right";
            int buttonWidth, buttonHeight;
            if (useHorizontalLabels) {
                buttonWidth = BUTTON_WIDTH_HORIZONTAL_LABEL;
                buttonHeight = BUTTON_HEIGHT_HORIZONTAL_LABEL;
            }
            else if (_showLabels) {
                buttonWidth = BUTTON_SIZE_WITH_LABEL;
                buttonHeight = BUTTON_SIZE_WITH_LABEL;
            }
            else {
                buttonWidth = BUTTON_SIZE;
                buttonHeight = BUTTON_SIZE;
            }

            int numberOfRows = (int)Math.Ceiling((double)maxPathItems / maxColumns);
            int dynamicWidth = maxColumns * (buttonWidth + BUTTON_MARGIN * 2);
            if (groupHeader == true && maxColumns < 2 && !useHorizontalLabels) {
                dynamicWidth = 2 * (buttonWidth + BUTTON_MARGIN * 2);
            }
            // Ensure minimum width for horizontal labels
            if (useHorizontalLabels) {
                dynamicWidth = Math.Max(dynamicWidth, BUTTON_WIDTH_HORIZONTAL_LABEL + (BUTTON_MARGIN * 2));
            }

            int dynamicHeight = numberOfRows * (buttonHeight + BUTTON_MARGIN * 2);
            var displayInfo = GetDisplayInformation();
            float scaleFactor = displayInfo.Item1;

            int scaledWidth = (int)(dynamicWidth * scaleFactor);
            int scaledHeight = (int)(dynamicHeight * scaleFactor);
            if (groupHeader) {
                scaledHeight += 40;
            }



            //MainGrid.Margin = new Thickness(0, 0, -5, -15);

            int finalWidth = scaledWidth + 30;
            int finalHeight = scaledHeight + 20;

            finalHeight = layout == "Card"&& groupHeader==true ? finalHeight + 15 : finalHeight;


            int screenHeight = (int)(DisplayArea.Primary.WorkArea.Height);
            int maxAllowedHeight = screenHeight - 30; // Reserve space for taskbar and window chrome
            if (finalHeight > maxAllowedHeight) {
                finalHeight = maxAllowedHeight;
            }

            NativeMethods.PositionWindowOffScreen(this.GetWindowHandle());


            _windowHelper.SetSize(finalWidth, finalHeight);



            NativeMethods.PositionWindowAboveTaskbar(this.GetWindowHandle(), show: false);
            AnimateWindowSlideUp(this.GetWindowHandle(), isSubPopup: _parentPopup != null);

            // Start animation immediately as window becomes visible
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => {
                var transform = new Microsoft.UI.Xaml.Media.TranslateTransform { Y = 40 };
                GridPanel.RenderTransform = transform;
                GridPanel.Opacity = 0;

                var sb = new Storyboard();

                var slideAnim = new DoubleAnimation {
                    From = 40,
                    To = 0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                    EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(slideAnim, transform);
                Storyboard.SetTargetProperty(slideAnim, "Y");

                var fadeAnim = new DoubleAnimation {
                    From = 0,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromMilliseconds(150))
                };
                Storyboard.SetTarget(fadeAnim, GridPanel);
                Storyboard.SetTargetProperty(fadeAnim, "Opacity");

                sb.Children.Add(slideAnim);
                sb.Children.Add(fadeAnim);
                _entranceStoryboard = sb;
                sb.Begin();

            });


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

        private string _currentGridIconPath;

        // Add this method to PopupWindow class
        private async Task CreateGridIconFromReorder() {
            try {
                if (PopupItems == null || !PopupItems.Any()) {
                    Debug.WriteLine("No items available for grid icon creation");
                    return;
                }

                // Get the group information
                var filteredGroup = _groups.FirstOrDefault(g => g.Value.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));
                if (filteredGroup.Key == null) {
                    Debug.WriteLine($"Error: Group '{_groupFilter}' not found");
                    return;
                }

                // Determine if this is a grid icon based on the current icon path
                string currentIcon = filteredGroup.Value.GroupIcon;
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

        private void ApplyGroupLayout(string headerPosition, string layout, bool groupHeader = false) {
            if (!groupHeader) layout = "Default";

            if (layout == "Card") {
                ScrollView.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
                ScrollView.BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
                ScrollView.BorderThickness = new Thickness(0);
            }
            else {
                ScrollView.Background = null;
                ScrollView.BorderBrush = null;
                ScrollView.BorderThickness = new Thickness(0);
            }

            var headerParent = Header.Parent as FrameworkElement;

            if (headerPosition == "Top") {
                Grid.SetRow(ScrollView, 1);
                if (headerParent != null) Grid.SetRow(headerParent, 0);
                MainGrid.RowDefinitions[0].Height = GridLength.Auto;
                MainGrid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);

                MainGrid.Margin = groupHeader
                    ? new Thickness(0, 0, -1, -5)
                    : new Thickness(0, -10, -1, -10);
                Header.Margin = layout == "Card" ? new Thickness(15, 5, 5, 5): new Thickness(10, 5, 5, 5);

                HeaderEditButton.Padding = layout == "Card" ? new Thickness(10): new Thickness(7);
                GridPanel.Padding = groupHeader ? new Thickness(0,0,0,0) : new Thickness(0, 10, 0, 15);
                GridPanel.Margin = layout == "Card"
                    ? new Thickness(0, 0, -5, -15)
                    : new Thickness(0, -5, -5, -15);
            }
            else {
                Grid.SetRow(ScrollView, 0);
                if (headerParent != null) Grid.SetRow(headerParent, 1);
                MainGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                MainGrid.RowDefinitions[1].Height = GridLength.Auto;

                MainGrid.Margin = groupHeader
                    ? new Thickness(0,0,-1,0)
                    : new Thickness(0, -10, -1, -10);
                Header.Margin = layout == "Card" ? new Thickness(15, 0, 5, 5) : new Thickness(10, 5, 5, 5);
                HeaderEditButton.Padding = layout == "Card" ? new Thickness(10) : new Thickness(7);
                GridPanel.Padding = groupHeader ? new Thickness(0,0,0,0) : new Thickness(0, 10, 0, 15);
                GridPanel.Margin = layout == "Card"
                    ? new Thickness(0, 0, -5, -15)
                    : new Thickness(0, 0, -5, -20);
            }
        }
        private async Task UpdateJsonConfiguration(string newIconPath, int gridSize) {
            try {
                var filteredGroup = _groups.FirstOrDefault(g => g.Value.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));
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
                    filteredGroup.Value.GroupName,
                    filteredGroup.Value.GroupHeader,
                    newIconPath, // Use the new grid icon path
                    filteredGroup.Value.GroupCol,
                    filteredGroup.Value.ShowLabels,
                    filteredGroup.Value.LabelSize > 0 ? filteredGroup.Value.LabelSize : DEFAULT_LABEL_SIZE,
                        filteredGroup.Value.LabelPosition != null ? filteredGroup.Value.LabelPosition : DEFAULT_LABEL_POSITION,
                           filteredGroup.Value.HeaderPosition ?? "Top",
    filteredGroup.Value.Layout ?? "Default",
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

                var filteredGroup = _groups.FirstOrDefault(g => g.Value.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));

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
                string currentIcon = filteredGroup.Value.GroupIcon;
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
                        filteredGroup.Value.GroupName,
                        filteredGroup.Value.GroupHeader,
                        filteredGroup.Value.GroupIcon,
                        filteredGroup.Value.GroupCol,
                        filteredGroup.Value.ShowLabels,
                        filteredGroup.Value.LabelSize > 0 ? filteredGroup.Value.LabelSize : DEFAULT_LABEL_SIZE,
                        filteredGroup.Value.LabelPosition != null ? filteredGroup.Value.LabelPosition : DEFAULT_LABEL_POSITION,
                        filteredGroup.Value.HeaderPosition ?? "Top",
    filteredGroup.Value.Layout ?? "Default",
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

      
        private string GetDisplayNameBackground(string filePath) {
            if (string.IsNullOrEmpty(filePath)) return "Unknown";
            string extension = Path.GetExtension(filePath).ToLower();
            if (extension == ".exe") {
                try {
                    var versionInfo = FileVersionInfo.GetVersionInfo(filePath);
                    if (!string.IsNullOrEmpty(versionInfo.FileDescription))
                        return versionInfo.FileDescription;
                }
                catch { }
            }
            return Path.GetFileNameWithoutExtension(filePath);
        }
        private async Task LoadGridItems(Dictionary<string, PathData> pathsWithProperties) {
            var items = await Task.Run(() => {
                var result = new List<PopupItem>();
                foreach (var pathEntry in pathsWithProperties) {
                    string path = pathEntry.Key;
                    PathData properties = pathEntry.Value;

                    string tooltip = !string.IsNullOrEmpty(properties.Tooltip)
                        ? properties.Tooltip
                        : GetDisplayNameBackground(path);

                    string customIconPath = !string.IsNullOrEmpty(properties.Icon)
                        ? properties.Icon : null;

                    var popupItem = new PopupItem {
                        Path = path,
                        Name = Path.GetFileNameWithoutExtension(path),
                        ToolTip = tooltip,
                        Icon = null,
                        Args = properties.Args ?? "",
                        IconPath = customIconPath,
                        CustomIconPath = customIconPath
                    };

                    if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) {
                        try {
                            IWshShell shell = new WshShell();
                            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(path);
                            string comment = shortcut.Description;
                            if (!string.IsNullOrEmpty(comment) &&
                                comment.EndsWith("- AppGroup Shortcut", StringComparison.OrdinalIgnoreCase)) {
                                popupItem.IsSubgroup = true;
                                popupItem.SubgroupName = comment.Replace("- AppGroup Shortcut", "").Trim();
                            }
                        }
                        catch (Exception ex) {
                            Debug.WriteLine($"Failed to read shortcut comment: {ex.Message}");
                        }
                    }

                    result.Add(popupItem);
                }
                return result;
            });

            foreach (var item in items) {
                PopupItems.Add(item);
                _ = LoadIconAsync(item, item.Path);
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
                    if (Path.GetExtension(path).Equals(".url", StringComparison.OrdinalIgnoreCase)) {
                        iconPath = await IconHelper.GetUrlFileIconAsync(path);
                    }
                    else {
                        iconPath = await IconCache.GetIconPathAsync(path);
                    }

                }

                BitmapImage icon = await IconCache.LoadImageFromPathAsync(iconPath);

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

        private System.Threading.Timer _focusTimer = null;

        private DateTime _lastSubPopupOpenTime = DateTime.MinValue;

        private void OpenSubPopup(string groupName) {
            if (_openSubPopups.TryGetValue(groupName, out var existing)) {
                // Close and remove existing before recreating
                NativeMethods.PositionWindowOffScreen(existing.GetWindowHandle());
                existing.Hide();
                existing._isLoaded = false;
                existing._openSubPopups.Clear();
                existing._parentPopup = null;
                _openSubPopups.Remove(groupName);
            }

            var subPopup = new PopupWindow(groupName);
            subPopup._parentPopup = this;
            _openSubPopups[groupName] = subPopup;

            int disableTransitions = 1;
            NativeMethods.DwmSetWindowAttribute(
                subPopup.GetWindowHandle(),
                NativeMethods.DWMWA_TRANSITIONS_FORCEDISABLED,
                ref disableTransitions,
                sizeof(int));
            UpdateLastOpenTime();

            var root = this;
            while (root._parentPopup != null) root = root._parentPopup;
            root._focusTimer?.Dispose();
            root._focusTimer = null;
            root.StartFocusTimer();

            subPopup.Activate();
        }
        private void UpdateLastOpenTime() {
            _lastSubPopupOpenTime = DateTime.Now;
            Debug.WriteLine($"UpdateLastOpenTime on {this.GetWindowHandle()}, parent={_parentPopup?.GetWindowHandle()}");
            _parentPopup?.UpdateLastOpenTime();
        }

        private void StartFocusTimer() {
            if (_parentPopup != null) {
                _parentPopup.StartFocusTimer();
                return;
            }

            _focusTimer?.Dispose();
            _focusTimer = new System.Threading.Timer(_ => {
                if ((DateTime.Now - _lastSubPopupOpenTime).TotalMilliseconds < 400)
                    return;

                IntPtr foreground = NativeMethods.GetForegroundWindow();
                bool isInChain = IsAnywhereInChain(foreground);

                if (!isInChain) {
                    DispatcherQueue.TryEnqueue(() => {
                        StopFocusTimer();
                        CloseAllChildrenRecursive(_openSubPopups);
                        _openSubPopups.Clear();
                        _isLoaded = false;
                        AnimateWindowSlideDown(this.GetWindowHandle(), () => {
                            DispatcherQueue.TryEnqueue(() => {
                                this.Hide();
                                NativeMethods.PositionWindowOffScreen(this.GetWindowHandle());
                            });
                        });
                    });
                }
                // Do NOT close children when clicking a popup in chain
                // Let Window_Activated handle that via natural focus
            }, null, 50, 50);
        }


        private bool IsAnywhereInChain(IntPtr hwnd) {
            if (this.GetWindowHandle() == hwnd) return true;
            foreach (var sub in _openSubPopups.Values) {
                if (sub.IsAnywhereInChain(hwnd)) return true;
            }
            return false;
        }
        int screenHeight = (int)(DisplayArea.Primary.WorkArea.Height) * 2;
        int screenWidth = (int)(DisplayArea.Primary.WorkArea.Width) * 2;
        private void CloseAllChildrenRecursive(Dictionary<string, PopupWindow> subs) {
            foreach (var sub in subs.Values.ToList()) {
                CloseAllChildrenRecursive(sub._openSubPopups);
                sub._openSubPopups.Clear();
                sub._parentPopup = null; // ← clear parent reference
                sub._isLoaded = false;
                NativeMethods.PositionWindowOffScreen(sub.GetWindowHandle());

                sub.AppWindow.Resize(new Windows.Graphics.SizeInt32(screenWidth, screenHeight));

                sub.Hide();
            }
        }

        private void StopFocusTimer() {
            if (_parentPopup != null) {
                _parentPopup.StopFocusTimer();
                return;
            }
            _focusTimer?.Dispose();
            _focusTimer = null;
        }





        private void GridView_ItemClick(object sender, ItemClickEventArgs e) {
            if (e.ClickedItem is PopupItem popupItem) {
                Debug.WriteLine($"ItemClick: path={popupItem.Path} IsSubgroup={popupItem.IsSubgroup} SubgroupName={popupItem.SubgroupName}");
                if (popupItem.IsSubgroup) {
                    OpenSubPopup(popupItem.SubgroupName);
                }
                else {
                    // Walk up to root popup
                    var root = this;
                    while (root._parentPopup != null) root = root._parentPopup;

                    // Close entire chain from root
                    root.DispatcherQueue.TryEnqueue(() => {
                        root.StopFocusTimer();
                        CloseAllChildrenRecursive(root._openSubPopups);
                        root._openSubPopups.Clear();
                        root._isLoaded = false;
                        root.Hide();
                    });

                    // Hide self if this is a sub-popup
                    if (_parentPopup != null) { this.Hide(); }

                    // Launch after closing
                    TryLaunchApp(popupItem.Path, popupItem.Args);
                }
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


        private bool _isLoaded = false;
        private async void Window_Activated(object sender, WindowActivatedEventArgs e) {
            if (e.WindowActivationState == WindowActivationState.Deactivated) {
                int screenHeight = (int)(DisplayArea.Primary.WorkArea.Height) * 2;
                int screenWidth = (int)(DisplayArea.Primary.WorkArea.Width) * 2;

                // Sub-popups never self-cleanup
                if (_parentPopup != null) return;

                // Root: don't cleanup if timer is running (sub-popups are open)
                if (_focusTimer != null) return;

                _isLoaded = false;

                var settings = await SettingsHelper.LoadSettingsAsync();

                CleanupUISettings();

                if (_hasBeenLoaded && !string.IsNullOrEmpty(_groupFilter)) {
                    this.DispatcherQueue.TryEnqueue(() => {

                        AnimateWindowSlideDown(this.GetWindowHandle(), () => {
                            DispatcherQueue.TryEnqueue(() => {
                                _entranceStoryboard?.Stop();
                                _entranceStoryboard = null;

                                this.Hide();
                                _windowHelper.SetSize(screenWidth, screenHeight);
                                NativeMethods.PositionWindowOffScreen(this.GetWindowHandle());

                                try {
                                    if (_gridView != null) {
                                        _gridView.RightTapped -= GridView_RightTapped;
                                        _gridView.DragItemsCompleted -= GridView_DragItemsCompleted;
                                        _gridView.ItemClick -= GridView_ItemClick;
                                    }

                                    foreach (var item in PopupItems) {
                                        if (item.Icon != null) {
                                            item.Icon.UriSource = null;
                                            item.Icon = null;
                                        }
                                    }
                                    PopupItems.Clear();
                                    GridPanel.Children.Clear();
                                    Header.Visibility = Visibility.Collapsed;
                                    HeaderText.Text = "";
                                    _anyGroupDisplayed = false;

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
                        });

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

                    _ = Task.Run(() => {
                        GC.Collect(0, GCCollectionMode.Optimized);
                    });
                }
            }
            else if (e.WindowActivationState == WindowActivationState.CodeActivated || e.WindowActivationState == WindowActivationState.PointerActivated) {

                if (!_isLoaded) {
                    GridPanel.Opacity = 0;
                    await LoadConfiguration();
                    _isLoaded = true;
                    _hasBeenLoaded = true;
                }

                Debug.WriteLine($"Activated: _isLoaded={_isLoaded}");

                if (useFileMode) {
                    Debug.WriteLine("FILE MODE");
                    if (_cachedAppFolderPath == null) {
                        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        _cachedAppFolderPath = Path.Combine(appDataPath, "AppGroup");
                        _cachedLastOpenPath = Path.Combine(_cachedAppFolderPath, "lastOpen");
                    }

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
                }
                else {
                    Debug.WriteLine("MESSAGE MODE");
                }

                if (!_isUISettingsSubscribed) {
                    _uiSettings ??= new UISettings();
                    _uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;
                    _isUISettingsSubscribed = true;
                }

                UpdateMainGridBackground(_uiSettings);

                if (_openSubPopups.Count > 0 &&
                    (DateTime.Now - _lastSubPopupOpenTime).TotalMilliseconds > 400) {
                    CloseAllChildrenRecursive(_openSubPopups);
                    _openSubPopups.Clear();
                }

                _ = this.DispatcherQueue.TryEnqueue(() => {
                    try {
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


        // Use CMD Wrapper 
        private void TryLaunchApp(string path, string args) {
            try {
                ProcessStartInfo psi = new ProcessStartInfo {
                    FileName = "cmd.exe",
                    Arguments = $"/c start \"\" \"{path}\" {args}",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process process = Process.Start(psi);
                process?.Close();
            }
            catch (Exception ex) {
                Debug.WriteLine($"Failed to launch {path}: {ex.Message}");
                ShowErrorDialog($"Failed to launch {path}: {ex.Message}");
            }
        }


        // Use  COM Elavation
        private void TryRunAsAdmin(string path, string args) {
            try {
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null)
                    throw new InvalidOperationException("Shell.Application COM object not found.");

                dynamic shell = Activator.CreateInstance(shellType);
                if (shell == null)
                    throw new InvalidOperationException("Failed to create Shell.Application instance.");

                shell.ShellExecute(path, args, "", "runas", 1);
            }
            catch (UnauthorizedAccessException) {
                Debug.WriteLine("Access denied.Try launching as administrator.");

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
            var dialog = new DialogWindow("Error", message);
            _ = dialog.ShowDialogAsync();
            //ContentDialog dialog = new ContentDialog {
            //    Title = "Error",
            //    Content = message,
            //    CloseButtonText = "OK",
            //    XamlRoot = this.Content.XamlRoot
            //};
            //_ = dialog.ShowAsync();

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
                    g.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));

                // Check if we found a matching group
                if (matchingGroup != null) {
                    // Call the LaunchAll function with the matching group name
                    await JsonConfigHelper.LaunchAll(matchingGroup.GroupName);
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