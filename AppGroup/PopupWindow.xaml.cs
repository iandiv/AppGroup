using IWshRuntimeLibrary;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
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

     public   bool ShowOnTray { get; set; } = false;
        public Dictionary<string, PathData> Path { get; set; }
    }

    public class PopupItem : INotifyPropertyChanged {
        public string Path { get; set; }
        public string Name { get; set; }
        public string ToolTip { get; set; }
        public string Args { get; set; }
        public string IconPath { get; set; }
        public string CustomIconPath { get; set; }

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
        private const int BUTTON_SIZE = 40;
        private const int BUTTON_SIZE_WITH_LABEL = 56;
        private const int BUTTON_HEIGHT_HORIZONTAL_LABEL = 40;
        private const int BUTTON_WIDTH_HORIZONTAL_LABEL = 180;
        private const int ICON_SIZE = 24;
        private const int BUTTON_MARGIN = 4;
        private const int DEFAULT_LABEL_SIZE = 12;
        private const string DEFAULT_LABEL_POSITION = "Bottom";
        private bool _hasBeenLoaded = false;

        private IntPtr _hwnd;
        private IntPtr _oldWndProc;
        private NativeMethods.WndProcDelegate _newWndProc;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly Dictionary<int, EditGroupWindow> _openEditWindows = new Dictionary<int, EditGroupWindow>();
        private readonly WindowHelper _windowHelper;    
        private ObservableCollection<PopupItem> PopupItems = new ObservableCollection<PopupItem>();
        private Dictionary<string, GroupData> _groups;
        private GridView _gridView;
        private PopupItem _clickedItem;
        private int _groupId;
        internal string _groupFilter = null;
        private NativeMethods.POINT? _receivedCursorPos;
        private string _json = "";
        private bool _anyGroupDisplayed;
        private DataTemplate _itemTemplate;
        private DataTemplate _itemTemplateWithLabel;
        private DataTemplate _itemTemplateHorizontalLabel;
        private ItemsPanelTemplate _panelTemplate;
        private ItemsPanelTemplate _panelTemplateWithLabel;
        private ItemsPanelTemplate _panelTemplateHorizontalLabel;

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

        
        private CancellationTokenSource _iconLoadCts = new CancellationTokenSource();
        private readonly List<Task> _backgroundTasks = new List<Task>();

        private bool useFileMode = false;
        private NativeMethods.SubclassProc _subclassProc;
        private const int SUBCLASS_ID = 1;
        private readonly Dictionary<string, PopupWindow> _openSubPopups = new Dictionary<string, PopupWindow>();
        private PopupWindow _parentPopup = null;
        private Storyboard _entranceStoryboard;
        private bool _entranceStarted = false;  
        private bool _wasLaunchedFromTaskbar = false;

        private int _isLoadingConfig = 0;
        private bool _isFlyoutOpen = false;
        private bool _isClosing = false;
        private readonly CancellationTokenSource _windowCts = new CancellationTokenSource();
        private static readonly SemaphoreSlim _iconLoadSemaphore = new SemaphoreSlim(6, 6);
        private static BitmapImage _placeholderIcon;

        private static NativeMethods.POINT _lastClickPos;
        private static IntPtr _mouseHookHandle;
        private static NativeMethods.LowLevelMouseProc _mouseHookProc;
        public PopupWindow(string groupFilter = null) {
            InitializeComponent();

            _mouseHookProc = MouseHookCallback;
            _mouseHookHandle = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_MOUSE_LL,
                _mouseHookProc,
                NativeMethods.GetModuleHandle(null),
                0);
            _groupFilter = groupFilter;
            this.Title = "Popup Window";

            _windowHelper = new WindowHelper(this);
            _windowHelper.SetSystemBackdrop(WindowHelper.BackdropType.AcrylicBase);
            _windowHelper.IsMaximizable = false;
            _windowHelper.IsMinimizable = false;
            _windowHelper.IsResizable = true;
            _windowHelper.HasBorder = true;
            _windowHelper.HasTitleBar = false;
            _windowHelper.IsAlwaysOnTop = true;

            
            NativeMethods.PositionWindowOffScreen(this.GetWindowHandle());
            var workArea = DisplayArea.Primary.WorkArea;
            this.AppWindow.Resize(new SizeInt32(workArea.Width * 2, workArea.Height * 2));
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
        internal async Task PreloadSubgroupsAsync(CancellationToken token = default) {
            try {
                if (_groups == null || string.IsNullOrEmpty(_groupFilter)) return;

                // Find current group's items
                var match = _groups.FirstOrDefault(g =>
                    g.Value.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));
                if (match.Key == null) return;

                // Collect subgroup names from path entries
                var subgroupNames = new List<string>();
                foreach (var pathEntry in match.Value.Path) {
                    string path = pathEntry.Key;
                    if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) {
                        try {
                            IWshShell shell = new WshShell();
                            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(path);
                            string comment = shortcut.Description;
                            if (!string.IsNullOrEmpty(comment) &&
                                comment.EndsWith("- AppGroup Shortcut", StringComparison.OrdinalIgnoreCase)) {
                                string subgroupName = comment.Replace("- AppGroup Shortcut", "").Trim();
                                subgroupNames.Add(subgroupName);
                            }
                        }
                        catch (Exception ex) {
                            Debug.WriteLine($"PreloadSubgroups shortcut read error: {ex.Message}");
                        }
                    }
                }

                // Warm icon cache for each subgroup (background only, no UI)
                foreach (string subgroupName in subgroupNames) {
                    if (token.IsCancellationRequested) return;

                    var subMatch = _groups.FirstOrDefault(g =>
                        g.Value.GroupName.Equals(subgroupName, StringComparison.OrdinalIgnoreCase));
                    if (subMatch.Key == null) continue;

                    foreach (var path in subMatch.Value.Path.Keys) {
                        if (token.IsCancellationRequested) return;
                        string p = path;
                        _ = Task.Run(() => IconCache.GetIconPathAsync(p), token);
                    }
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"PreloadSubgroupsAsync error: {ex.Message}");
            }
        }
        internal async Task PreloadLastGroupAsync() {
            try {
                string configPath = JsonConfigHelper.GetDefaultConfigPath();
                _groups = await Task.Run(() => {
                    string json = JsonConfigHelper.ReadJsonFromFile(configPath);
                    return JsonSerializer.Deserialize<Dictionary<string, GroupData>>(json, JsonOptions);
                });
                if (_groups == null || string.IsNullOrEmpty(_groupFilter)) return;

                var match = _groups.FirstOrDefault(g =>
                    g.Value.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));
                if (match.Key == null) return;

                // Existing: warm main group icons
                foreach (var path in match.Value.Path.Keys) {
                    string p = path;
                    _ = Task.Run(() => IconCache.GetIconPathAsync(p));
                }

                // New: warm subgroup icons
                await PreloadSubgroupsAsync();
            }
            catch (Exception ex) {
                Debug.WriteLine($"PreloadLastGroupAsync error: {ex.Message}");
            }
        }
        private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam) {
            if (nCode >= 0 && wParam == (IntPtr)NativeMethods.WM_LBUTTONDOWN) {
                var hook = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                _lastClickPos = hook.pt;
            }
            return NativeMethods.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }
        private SizeInt32 GetOffscreenSize() {
            var workArea = DisplayArea.Primary.WorkArea;
            return new SizeInt32(workArea.Width * 2, workArea.Height * 2);
        }

        private void AnimateWindowSlideUp(IntPtr hWnd, bool isSubPopup = false, Action onComplete = null, NativeMethods.POINT? cursorOverride = null) {
            NativeMethods.GetWindowRect(hWnd, out NativeMethods.RECT rect);
            int finalX = rect.left;
            int finalY = rect.top;
            int windowWidth = rect.right - rect.left;
            int windowHeight = rect.bottom - rect.top;

            NativeMethods.POINT cursor = cursorOverride.HasValue
                ? cursorOverride.Value
                : (NativeMethods.GetCursorPos(out var p) ? p : default);

            IntPtr monitor = NativeMethods.MonitorFromPoint(cursor, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var mi = new NativeMethods.MONITORINFO {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFO>()
            };
            NativeMethods.GetMonitorInfo(monitor, ref mi);

            bool workEqualsMonitor =
                mi.rcWork.top == mi.rcMonitor.top && mi.rcWork.bottom == mi.rcMonitor.bottom &&
                mi.rcWork.left == mi.rcMonitor.left && mi.rcWork.right == mi.rcMonitor.right;

            int startX = finalX;
            int startY = finalY;

            if (!workEqualsMonitor) {
                if (mi.rcWork.left > mi.rcMonitor.left) startX = finalX - windowWidth;
                else if (mi.rcWork.right < mi.rcMonitor.right) startX = finalX + windowWidth;
                else if (mi.rcWork.top > mi.rcMonitor.top) startY = finalY - windowHeight;
                else startY = finalY + windowHeight;
            }
            else {
                startY = finalY + windowHeight;
            }

            if (!isSubPopup)
                NativeMethods.SetWindowPos(hWnd, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

            NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, startX, startY, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOWNOACTIVATE);

            
            int intervalMs = GetRefreshIntervalMs(hWnd);
            int durationMs = 200;
            int steps = Math.Max(1, durationMs / intervalMs);
            int currentStep = 0;
            int lastX = startX;
            int lastY = startY;

            var cts = _windowCts;
            _ = Task.Run(() => {
                Thread.CurrentThread.Priority = ThreadPriority.Highest;
                bool earlyTriggered = false;

                while (true) {
                    if (cts.IsCancellationRequested) break;

                    currentStep++;
                    double t = (double)currentStep / steps;
                    double ease = currentStep >= steps ? 1.0 : 1 - Math.Pow(2, -10 * t);

                    int currentX = (int)Math.Round(startX + (finalX - startX) * ease);
                    int currentY = (int)Math.Round(startY + (finalY - startY) * ease);

                    if (currentStep >= steps) {
                        currentX = finalX;
                        currentY = finalY;
                    }

                    if (currentX != lastX || currentY != lastY) {
                        NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, currentX, currentY, 0, 0,
                            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
                        NativeMethods.DwmFlush();
                        lastX = currentX;
                        lastY = currentY;
                    }
                    else {
                        NativeMethods.DwmFlush();
                    }
                    double remainingDistance = Math.Sqrt(
                        Math.Pow(finalX - currentX, 2) +
                        Math.Pow(finalY - currentY, 2)
                    );

                    double totalDistance = Math.Sqrt(
      Math.Pow(finalX - startX, 2) +
      Math.Pow(finalY - startY, 2)
  );
                    double triggerDistancePx = totalDistance * 0.1;

                    if (!earlyTriggered && remainingDistance <= triggerDistancePx) {
                        earlyTriggered = true;
                        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () => {
                            onComplete?.Invoke();
                        });
                    }

                    if (currentStep >= steps) {

                        if (!isSubPopup)
                            NativeMethods.SetWindowPos(hWnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
                        break;
                    }
                }
            });
        }
        private static BitmapImage GetOrCreatePlaceholder() {
            if (_placeholderIcon != null) return _placeholderIcon;
            
            _placeholderIcon = new BitmapImage();
            return _placeholderIcon;
        }

        private void AnimateWindowSlideDown(IntPtr hWnd, Action onComplete) {
            NativeMethods.GetWindowRect(hWnd, out NativeMethods.RECT rect);
            int startX = rect.left;
            int startY = rect.top;
            int windowWidth = rect.right - rect.left;
            int windowHeight = rect.bottom - rect.top;

            NativeMethods.GetCursorPos(out NativeMethods.POINT cursor);
            IntPtr monitor = NativeMethods.MonitorFromPoint(cursor, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var mi = new NativeMethods.MONITORINFO {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFO>()
            };
            NativeMethods.GetMonitorInfo(monitor, ref mi);

            bool workEqualsMonitor =
                mi.rcWork.top == mi.rcMonitor.top && mi.rcWork.bottom == mi.rcMonitor.bottom &&
                mi.rcWork.left == mi.rcMonitor.left && mi.rcWork.right == mi.rcMonitor.right;

            int endX = startX, endY = startY;
            if (!workEqualsMonitor) {
                if (mi.rcWork.left > mi.rcMonitor.left) endX = startX - windowWidth;
                else if (mi.rcWork.right < mi.rcMonitor.right) endX = startX + windowWidth;
                else if (mi.rcWork.top > mi.rcMonitor.top) endY = startY - windowHeight;
                else endY = startY + windowHeight;
            }
            else {
                endY = startY + windowHeight;
            }

            NativeMethods.SetWindowPos(hWnd, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

            int intervalMs = GetRefreshIntervalMs(hWnd);
            int durationMs = 150;
            int steps = Math.Max(1, durationMs / intervalMs);
            int currentStep = 0;
            var cts = _windowCts;

            System.Threading.Timer timer = null;
            timer = new System.Threading.Timer(_ => {
                if (cts.IsCancellationRequested) { onComplete?.Invoke(); timer?.Dispose(); return; }
                currentStep++;
                double t = (double)currentStep / steps;
                double ease = 1 - Math.Sqrt(1 - Math.Pow(t, 2));
                int currentX = (int)(startX + (endX - startX) * ease);
                int currentY = (int)(startY + (endY - startY) * ease);
                NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, currentX, currentY, 0, 0,
                    NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

                if (currentStep >= steps) {
                    onComplete?.Invoke();
                    timer?.Dispose();
                }
            }, null, intervalMs, intervalMs);
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
            return 16;
        }

        private void UiSettings_ColorValuesChanged(UISettings sender, object args) {
            DispatcherQueue.TryEnqueue(() => UpdateMainGridBackground(sender));
        }

        private bool IsAlreadyOpenInChain(string groupName) {
            foreach (var sub in _openSubPopups.Values) {
                if (sub._groupFilter?.Equals(groupName, StringComparison.OrdinalIgnoreCase) == true) return true;
                if (sub.IsAlreadyOpenInChain(groupName)) return true;
            }
            return false;
        }

        private void SubclassWindow() {
            try {
                _subclassProc = new NativeMethods.SubclassProc(SubclassProc);
                bool success = NativeMethods.SetWindowSubclass(_hwnd, _subclassProc, SUBCLASS_ID, IntPtr.Zero);
                Debug.WriteLine(success ? "Window subclassed successfully" : $"Failed to subclass window. Error: {Marshal.GetLastWin32Error()}");
            }
            catch (Exception ex) {
                Debug.WriteLine($"Failed to subclass window: {ex.Message}");
            }
        }

        private IntPtr SubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData) {
            if (msg == NativeMethods.WM_COPYDATA) {
                try {
                    NativeMethods.COPYDATASTRUCT cds = (NativeMethods.COPYDATASTRUCT)Marshal.PtrToStructure(
                        lParam, typeof(NativeMethods.COPYDATASTRUCT));
                    if (cds.dwData == (IntPtr)100) {
                        string raw = Marshal.PtrToStringUni(cds.lpData);
                        string groupName = raw;
                        NativeMethods.POINT? parsedClickPos = null;

                        int sep = raw.IndexOf('|');
                        if (sep >= 0) {
                            groupName = raw.Substring(0, sep);
                            string posStr = raw.Substring(sep + 1);
                            var parts = posStr.Split(',');
                            if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                                parsedClickPos = new NativeMethods.POINT { X = x, Y = y };
                        }

                        this.DispatcherQueue.TryEnqueue(async () => {
                            _groupFilter = groupName;
                            _receivedCursorPos = parsedClickPos ?? _lastClickPos;
                            await LoadConfiguration();
                        });
                    }
                }
                catch (Exception ex) {
                    Debug.WriteLine($"Error in WM_COPYDATA handler: {ex.Message}");
                }
            }
            return NativeMethods.DefSubclassProc(hWnd, msg, wParam, lParam);
        }

        private void UpdateMainGridBackground(UISettings uiSettings) {
            var settings = SettingsHelper.GetCurrentSettings();
            string popupTheme = settings?.PopupTheme ?? "WindowsMode";

            // Accent color only applies to WindowsMode (it's a Windows/taskbar-level setting)
            if (popupTheme == "WindowsMode"
       && settings.PopupAccentBackground
       && IsAccentColorOnStartTaskbarEnabled()) {
                if (Content is FrameworkElement rootElement)
                    rootElement.RequestedTheme = ElementTheme.Dark;

                string accentResourceKey = "SystemAccentColorDark2";
                if (Application.Current.Resources.TryGetValue(accentResourceKey, out object accentColor)) {
                    MainGrid.Background = new Microsoft.UI.Xaml.Media.AcrylicBrush {
                        TintColor = (Windows.UI.Color)accentColor,
                        TintOpacity = 0.8,
                        FallbackColor = (Windows.UI.Color)accentColor
                    };
                }
                return;
            }

            MainGrid.Background = null;

            ElementTheme resolvedTheme = popupTheme switch {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                "AppMode" => ResolveAppModeTheme(),
                _ => ResolveWindowsModeTheme()
            };

            if (Content is FrameworkElement root)
                root.RequestedTheme = resolvedTheme;
        }

        private ElementTheme ResolveWindowsModeTheme() {
            // Follows Windows mode (taskbar/start menu) — SystemUsesLightTheme
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key != null) {
                object value = key.GetValue("SystemUsesLightTheme");
                bool isLight = value != null && (int)value == 1;
                return isLight ? ElementTheme.Light : ElementTheme.Dark;
            }
            return ElementTheme.Default;
        }

        private ElementTheme ResolveAppModeTheme() {
            // Follows app mode (programs) — AppsUseLightTheme
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key != null) {
                object value = key.GetValue("AppsUseLightTheme");
                bool isLight = value != null && (int)value == 1;
                return isLight ? ElementTheme.Light : ElementTheme.Dark;
            }
            return ElementTheme.Default;
        }

        private bool IsAccentColorOnStartTaskbarEnabled() {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key != null) {
                object value = key.GetValue("ColorPrevalence");
                if (value != null && (int)value == 1) return true;
            }
            return false;
        }

        private void InitializeTemplates() {
            _itemTemplate = (DataTemplate)XamlReader.Load(
                $@"<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <Grid VerticalAlignment=""Center"" HorizontalAlignment=""Center"" UseLayoutRounding=""True""
          Width=""{BUTTON_SIZE}"" Height=""{BUTTON_SIZE}""
          ToolTipService.ToolTip=""{{Binding ToolTip}}"">
        <Image Source=""{{Binding Icon}}"" Width=""{ICON_SIZE}"" Height=""{ICON_SIZE}""
               Stretch=""Uniform"" VerticalAlignment=""Center"" HorizontalAlignment=""Center"" Margin=""8"" />
    </Grid>
</DataTemplate>");

            const int EFFECTIVE_BUTTON_WIDTH = BUTTON_SIZE + (BUTTON_MARGIN * 2);
            _panelTemplate = (ItemsPanelTemplate)XamlReader.Load(
                $@"<ItemsPanelTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
                <ItemsWrapGrid Orientation=""Horizontal""
                              ItemWidth=""{EFFECTIVE_BUTTON_WIDTH}"" ItemHeight=""{EFFECTIVE_BUTTON_WIDTH}""
                              HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
            </ItemsPanelTemplate>");
        }

        private void CreateLabelTemplates(int fontSize) {
            const int EFFECTIVE_BUTTON_WIDTH_WITH_LABEL = BUTTON_SIZE_WITH_LABEL + (BUTTON_MARGIN * 2);
            _itemTemplateWithLabel = (DataTemplate)XamlReader.Load(
                $@"<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <StackPanel VerticalAlignment=""Center"" HorizontalAlignment=""Center""
          Width=""{BUTTON_SIZE_WITH_LABEL}"" Height=""{BUTTON_SIZE_WITH_LABEL}""
          ToolTipService.ToolTip=""{{Binding ToolTip}}"">
        <Image Source=""{{Binding Icon}}"" Width=""{ICON_SIZE}"" Height=""{ICON_SIZE}""
               Stretch=""Uniform"" HorizontalAlignment=""Center"" Margin=""4,6,4,2"" />
        <TextBlock Text=""{{Binding ToolTip}}"" FontSize=""{fontSize}""
                   TextTrimming=""CharacterEllipsis"" TextAlignment=""Center""
                   HorizontalAlignment=""Center"" MaxWidth=""{BUTTON_SIZE_WITH_LABEL - 4}"" Opacity=""0.9"" />
    </StackPanel>
</DataTemplate>");

            _panelTemplateWithLabel = (ItemsPanelTemplate)XamlReader.Load(
                $@"<ItemsPanelTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
                <ItemsWrapGrid Orientation=""Horizontal""
                              ItemWidth=""{EFFECTIVE_BUTTON_WIDTH_WITH_LABEL}""
                              ItemHeight=""{EFFECTIVE_BUTTON_WIDTH_WITH_LABEL}""
                              HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
            </ItemsPanelTemplate>");

            const int EFFECTIVE_BUTTON_HEIGHT_HORIZONTAL = BUTTON_HEIGHT_HORIZONTAL_LABEL + (BUTTON_MARGIN * 2);
            _itemTemplateHorizontalLabel = (DataTemplate)XamlReader.Load(
                $@"<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <Grid Width=""{BUTTON_WIDTH_HORIZONTAL_LABEL}"" Height=""{BUTTON_HEIGHT_HORIZONTAL_LABEL}""
          ToolTipService.ToolTip=""{{Binding ToolTip}}"">
        <StackPanel Orientation=""Horizontal"" VerticalAlignment=""Center"" HorizontalAlignment=""Left"">
            <Image Source=""{{Binding Icon}}"" Width=""{ICON_SIZE}"" Height=""{ICON_SIZE}""
                   Stretch=""Uniform"" VerticalAlignment=""Center"" Margin=""8,0,8,0"" />
            <TextBlock Text=""{{Binding ToolTip}}"" FontSize=""{fontSize}""
                       TextTrimming=""CharacterEllipsis"" VerticalAlignment=""Center""
                       MaxWidth=""{BUTTON_WIDTH_HORIZONTAL_LABEL - ICON_SIZE - 12}"" Opacity=""0.9"" />
        </StackPanel>
    </Grid>
</DataTemplate>");

            _panelTemplateHorizontalLabel = (ItemsPanelTemplate)XamlReader.Load(
                $@"<ItemsPanelTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
                <ItemsWrapGrid Orientation=""Horizontal""
                              ItemWidth=""{BUTTON_WIDTH_HORIZONTAL_LABEL + (BUTTON_MARGIN * 2)}""
                              ItemHeight=""{EFFECTIVE_BUTTON_HEIGHT_HORIZONTAL}""
                              HorizontalAlignment=""Left"" VerticalAlignment=""Center""/>
            </ItemsPanelTemplate>");
        }

        private DateTime _lastConfigLoad = DateTime.MinValue;

        private async Task LoadConfiguration() {
          
            if (Interlocked.CompareExchange(ref _isLoadingConfig, 1, 0) != 0) return;
            try {
                string configPath = JsonConfigHelper.GetDefaultConfigPath();
                
                _groups = await Task.Run(() => {
                    string json = JsonConfigHelper.ReadJsonFromFile(configPath);
                    return JsonSerializer.Deserialize<Dictionary<string, GroupData>>(json, JsonOptions);
                });
                _lastConfigLoad = File.GetLastWriteTime(configPath);

                if (_groups != null) {
                    InitializeWindow();
                    await CreateDynamicContent();

                    if (!string.IsNullOrEmpty(_groupFilter)) {
                        var filteredGroup = _groups.FirstOrDefault(g =>
                            g.Value.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));
                        if (filteredGroup.Key != null) {
                            string headerPosition = filteredGroup.Value.HeaderPosition ?? "Top";
                            string layout = filteredGroup.Value.Layout ?? "Default";
                            _isCardLayout = (layout == "Card");
                            bool groupHeader = filteredGroup.Value.GroupHeader;
                            ApplyGroupLayout(headerPosition, layout, groupHeader);
                        }
                    }
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error loading configuration: {ex.Message}");
            }
            finally {
                Interlocked.Exchange(ref _isLoadingConfig, 0);
            }
        }

        private async Task CreateDynamicContent() {
            UnsubscribeGridViewHandlers();
            PopupItems.Clear();
            GridPanel.Children.Clear();
            HeaderText.Text = "";
            _anyGroupDisplayed = false;
            GridPanel.Opacity = 0;
            _iconLoadCts.Cancel();
            _iconLoadCts = new CancellationTokenSource();

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

                var currentGridView = _gridView;
                await LoadGridItems(group.Value.Path);
                if (currentGridView == null) return;

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

        
        private void UnsubscribeGridViewHandlers() {
            if (_gridView != null) {
                _gridView.RightTapped -= GridView_RightTapped;
                _gridView.DragItemsCompleted -= GridView_DragItemsCompleted;
                _gridView.ItemClick -= GridView_ItemClick;
            }
        }

        private async Task InitializeWindow() {
            int maxPathItems = 1;
            int maxColumns = 1;
            bool groupHeader = false;
            string headerPosition = "top";
            string layout = "Default";
            _showLabels = false;
            _labelSize = DEFAULT_LABEL_SIZE;
            _labelPosition = DEFAULT_LABEL_POSITION;
            _currentColumns = 1;

            if (!string.IsNullOrEmpty(_groupFilter) && _groups.Values.Any(g =>
                g.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase))) {
                var filteredGroup = _groups.FirstOrDefault(g =>
                    g.Value.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));

                maxPathItems = filteredGroup.Value.Path.Count;
                maxColumns = filteredGroup.Value.GroupCol;
                groupHeader = filteredGroup.Value.GroupHeader;
                iconGroup = filteredGroup.Value.GroupIcon;
                _showLabels = filteredGroup.Value.ShowLabels;
                _labelSize = filteredGroup.Value.LabelSize > 0 ? filteredGroup.Value.LabelSize : DEFAULT_LABEL_SIZE;
                _labelPosition = filteredGroup.Value.LabelPosition ?? DEFAULT_LABEL_POSITION;
                _currentColumns = maxColumns;
                headerPosition = filteredGroup.Value.HeaderPosition ?? "Top";
                layout = filteredGroup.Value.Layout ?? "Default";

                if (_showLabels) CreateLabelTemplates(_labelSize);
                if (!int.TryParse(filteredGroup.Key, out _groupId))
                    Debug.WriteLine($"Error: Group key '{filteredGroup.Key}' is not a valid integer ID");
            }
            else {
                foreach (var group in _groups.Values) {
                    maxPathItems = Math.Max(maxPathItems, group.Path.Count);
                    maxColumns = Math.Max(maxColumns, group.GroupCol);
                }
                _currentColumns = maxColumns;
            }

            DispatcherQueue.TryEnqueue(() => ApplyGroupLayout(headerPosition, layout, groupHeader));

            bool useHorizontalLabels = _showLabels && _labelPosition == "Right";
            int buttonWidth = useHorizontalLabels ? BUTTON_WIDTH_HORIZONTAL_LABEL
                : _showLabels ? BUTTON_SIZE_WITH_LABEL : BUTTON_SIZE;
            int buttonHeight = useHorizontalLabels ? BUTTON_HEIGHT_HORIZONTAL_LABEL
                : _showLabels ? BUTTON_SIZE_WITH_LABEL : BUTTON_SIZE;

            int numberOfRows = (int)Math.Ceiling((double)maxPathItems / maxColumns);
            int dynamicWidth = maxColumns * (buttonWidth + BUTTON_MARGIN * 2);
            if (groupHeader && maxColumns < 2 && !useHorizontalLabels)
                dynamicWidth = 2 * (buttonWidth + BUTTON_MARGIN * 2);
            if (useHorizontalLabels)
                dynamicWidth = Math.Max(dynamicWidth, BUTTON_WIDTH_HORIZONTAL_LABEL + (BUTTON_MARGIN * 2));

            int dynamicHeight = numberOfRows * (buttonHeight + BUTTON_MARGIN * 2);
            var displayInfo = GetDisplayInformation();
            float scaleFactor = displayInfo.Item1;

            int scaledWidth = (int)(dynamicWidth * scaleFactor);
            int scaledHeight = (int)(dynamicHeight * scaleFactor);
            if (groupHeader) scaledHeight += 40;

            int finalWidth = scaledWidth + 30;
            int finalHeight = scaledHeight + 20;
            if (layout == "Card" && groupHeader) finalHeight += 15;

            int screenHeight = (int)DisplayArea.Primary.WorkArea.Height;
            int maxAllowedHeight = screenHeight - 30;
            if (finalHeight > maxAllowedHeight) finalHeight = maxAllowedHeight;

            NativeMethods.PositionWindowOffScreen(this.GetWindowHandle());
            _windowHelper.SetSize(finalWidth, finalHeight);
            _windowHelper.IsAlwaysOnTop = true;
            if (_parentPopup != null) {
                PositionSubPopupNearParent();
                NativeMethods.ShowWindow(this.GetWindowHandle(), NativeMethods.SW_SHOWNOACTIVATE);
          
            }
            else if (IsLaunchedFromTaskbar(_receivedCursorPos)) {
                _wasLaunchedFromTaskbar = true;
                NativeMethods.PositionWindowAboveTaskbar(this.GetWindowHandle(), show: false, cursorOverride: _receivedCursorPos);

                var settings = await SettingsHelper.LoadSettingsAsync();
                if (settings.EnableWindowSlideAnimation) {
                    AnimateWindowSlideUp(this.GetWindowHandle(), isSubPopup: false, onComplete: null, cursorOverride: _receivedCursorPos);
                }
                else {
                    NativeMethods.ShowWindow(this.GetWindowHandle(), NativeMethods.SW_SHOWNOACTIVATE);
                }
                _receivedCursorPos = null;
                return;
            }
            else {
                _wasLaunchedFromTaskbar = false;
                NativeMethods.PositionWindowAboveTaskbar(this.GetWindowHandle(), show: false, cursorOverride: _receivedCursorPos);
                NativeMethods.ShowWindow(this.GetWindowHandle(), NativeMethods.SW_SHOWNOACTIVATE);
                _receivedCursorPos = null;
            }



            //DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, TriggerContentAnimation);


        }
        private void TriggerContentAnimation() {
            GridPanel.Opacity = 1;
            NativeMethods.GetCursorPos(out NativeMethods.POINT cursor);
            IntPtr monitor = NativeMethods.MonitorFromPoint(cursor, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var mi = new NativeMethods.MONITORINFO {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFO>()
            };
            NativeMethods.GetMonitorInfo(monitor, ref mi);

            bool workEqualsMonitor =
                mi.rcWork.top == mi.rcMonitor.top && mi.rcWork.bottom == mi.rcMonitor.bottom &&
                mi.rcWork.left == mi.rcMonitor.left && mi.rcWork.right == mi.rcMonitor.right;

            bool slideOnX = false;

            double slideFrom = 40;

            if (_parentPopup != null) {
                if (!workEqualsMonitor) {
                    if (mi.rcWork.left > mi.rcMonitor.left) { slideOnX = true; slideFrom = -40; }
                    else if (mi.rcWork.right < mi.rcMonitor.right) { slideOnX = true; slideFrom = 40; }
                    else if (mi.rcWork.top > mi.rcMonitor.top) { slideOnX = false; slideFrom = -40; }
                    else { slideOnX = false; slideFrom = 40; }
                }
                else { slideOnX = false; slideFrom = 40; }
            }
            else {
                bool fromTaskbar = IsLaunchedFromTaskbar();
                if (!fromTaskbar) { slideOnX = false; slideFrom = 40; }
                else if (!workEqualsMonitor) {
                    if (mi.rcWork.left > mi.rcMonitor.left) { slideOnX = true; slideFrom = -40; }
                    else if (mi.rcWork.right < mi.rcMonitor.right) { slideOnX = true; slideFrom = 40; }
                    else if (mi.rcWork.top > mi.rcMonitor.top) { slideOnX = false; slideFrom = -40; }
                    else { slideOnX = false; slideFrom = 40; }
                }
            }

            var transform = new Microsoft.UI.Xaml.Media.TranslateTransform {
                X = slideOnX ? slideFrom : 0,
                Y = slideOnX ? 0 : slideFrom
            };
            GridPanel.RenderTransform = transform;

            var sb = new Storyboard();
            var slideAnim = new DoubleAnimation {
                From = slideFrom,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(350)),
                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(slideAnim, transform);
            Storyboard.SetTargetProperty(slideAnim, slideOnX ? "X" : "Y");

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
            _entranceStarted = true;
            sb.Begin();
        }
        private void StopEntranceStoryboard() {
            
            if (_entranceStarted && _entranceStoryboard != null) {
                try { _entranceStoryboard.Stop(); } catch { }
                _entranceStoryboard = null;
                _entranceStarted = false;
            }
        }

        private void PositionSubPopupNearParent() {
            try {
                if (!NativeMethods.GetWindowRect(this.GetWindowHandle(), out NativeMethods.RECT wr)) return;
                int subWidth = wr.right - wr.left;
                int subHeight = wr.bottom - wr.top;

                // Use last click position instead of live cursor
                NativeMethods.POINT cursor = _lastClickPos;

                IntPtr monitor = NativeMethods.MonitorFromPoint(cursor, NativeMethods.MONITOR_DEFAULTTONEAREST);
                var mi = new NativeMethods.MONITORINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFO>() };
                NativeMethods.GetMonitorInfo(monitor, ref mi);

                bool workEqualsMonitor =
                    mi.rcWork.top == mi.rcMonitor.top && mi.rcWork.bottom == mi.rcMonitor.bottom &&
                    mi.rcWork.left == mi.rcMonitor.left && mi.rcWork.right == mi.rcMonitor.right;

                bool taskbarOnTop = !workEqualsMonitor && mi.rcWork.top > mi.rcMonitor.top;
                bool taskbarOnLeft = !workEqualsMonitor && mi.rcWork.left > mi.rcMonitor.left;
                bool taskbarOnRight = !workEqualsMonitor && mi.rcWork.right < mi.rcMonitor.right;

                int x, y;
                if (taskbarOnLeft) {
                    x = cursor.X + 10;
                    if (x + subWidth > mi.rcWork.right) x = cursor.X - subWidth - 10;
                    y = cursor.Y - subHeight / 2;
                }
                else if (taskbarOnRight) {
                    x = cursor.X - subWidth - 10;
                    if (x < mi.rcWork.left) x = cursor.X + 10;
                    y = cursor.Y - subHeight / 2;
                }
                else if (taskbarOnTop) {
                    x = cursor.X - subWidth / 2;
                    y = cursor.Y + 10;
                    if (y + subHeight > mi.rcWork.bottom) y = mi.rcWork.bottom - subHeight;
                }
                else {
                    x = cursor.X - subWidth / 2;
                    y = cursor.Y - subHeight - 10;
                    if (y < mi.rcWork.top) y = mi.rcWork.top;
                }

                x = Math.Max(mi.rcWork.left, Math.Min(x, mi.rcWork.right - subWidth));
                y = Math.Max(mi.rcWork.top, Math.Min(y, mi.rcWork.bottom - subHeight));

                NativeMethods.SetWindowPos(this.GetWindowHandle(), IntPtr.Zero, x, y, 0, 0,
                    NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
            }
            catch (Exception ex) {
                Debug.WriteLine($"PositionSubPopupNearParent error: {ex.Message}");
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

        private string _currentGridIconPath;

        private async Task CreateGridIconFromReorder() {
            try {
                if (PopupItems == null || !PopupItems.Any()) return;

                var filteredGroup = _groups.FirstOrDefault(g =>
                    g.Value.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));
                if (filteredGroup.Key == null) return;

                string currentIcon = filteredGroup.Value.GroupIcon;
                if (!currentIcon.Contains("grid")) return;

                int gridSize = currentIcon.Contains("grid3") ? 3 : 2;
                var gridItems = PopupItems.Take(gridSize * gridSize).Select(item => new ExeFileModel {
                    FileName = item.Name,
                    FilePath = item.Path,
                    Icon = item.Icon?.UriSource?.LocalPath ?? "",
                    Tooltip = item.ToolTip,
                    Args = item.Args,
                    IconPath = item.CustomIconPath
                }).ToList();

                IconHelper iconHelper = new IconHelper();
                string newGridIconPath = await iconHelper.CreateGridIconForPopupAsync(gridItems, gridSize, _groupFilter);

                if (!string.IsNullOrEmpty(newGridIconPath)) {
                    _currentGridIconPath = newGridIconPath;
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
                string groupsFolder = Path.Combine(localAppDataPath, "AppGroup", "Groups");
                string groupFolder = Path.Combine(groupsFolder, _groupFilter);
                if (!Directory.Exists(groupFolder)) return;

                string shortcutPath = Path.Combine(groupFolder, $"{_groupFilter}.lnk");
                if (File.Exists(shortcutPath)) {
                    IWshShell wshShell = new WshShell();
                    IWshShortcut shortcut = (IWshShortcut)wshShell.CreateShortcut(shortcutPath);
                    shortcut.IconLocation = newIconPath;
                    shortcut.Save();
                }

                await UpdateJsonConfiguration(newIconPath, gridSize);

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
                    ScrollView.RequestedTheme = (Content as FrameworkElement)?.RequestedTheme ?? ElementTheme.Default;
                    bool isDark = ScrollView.ActualTheme == ElementTheme.Dark;

                    // CardBackgroundFillColorDefault: Light=#B3FFFFFF, Dark=#0DFFFFFF
                    ScrollView.Background = new SolidColorBrush(isDark
                        ? Windows.UI.Color.FromArgb(13, 255, 255, 255)
                        : Windows.UI.Color.FromArgb(179, 255, 255, 255));

                    // CardStrokeColorDefault: Light=#0F000000, Dark=#19000000
                    ScrollView.BorderBrush = new SolidColorBrush(isDark
                        ? Windows.UI.Color.FromArgb(25, 0, 0, 0)
                        : Windows.UI.Color.FromArgb(15, 0, 0, 0));
                ScrollView.BorderThickness = new Thickness(0,0,0,1);
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
                //Header.Background = new SolidColorBrush(Microsoft.UI.Colors.Red);
                MainGrid.RowDefinitions[0].Height = GridLength.Auto;
                MainGrid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
                MainGrid.Margin = groupHeader ? new Thickness(0, 0, -1, -5) : new Thickness(0, -10, -1, -10);
                Header.Margin = layout == "Card" ? new Thickness(15, 5, 5, 5) : new Thickness(10, 5, 5, -5);
                HeaderEditButton.Padding = layout == "Card" ? new Thickness(10) : new Thickness(7);
                GridPanel.Padding = groupHeader ? new Thickness(0) : new Thickness(0, 10, 0, 15);
                GridPanel.Margin = layout == "Card" ? new Thickness(0, 0, -5, -15) : new Thickness(0, -5, -5, -25);
            }
            else {
                Grid.SetRow(ScrollView, 0);
                //Header.Background = new SolidColorBrush(Microsoft.UI.Colors.Blue);
                if (headerParent != null) Grid.SetRow(headerParent, 1);
                MainGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                MainGrid.RowDefinitions[1].Height = GridLength.Auto;
                MainGrid.Margin = groupHeader ? new Thickness(0, 0, -1, 0) : new Thickness(0, -10, -1, -10);
                Header.Margin = layout == "Card" ? new Thickness(15, 1, 5, 5) : new Thickness(10, -5, 5, 5);
                HeaderEditButton.Padding = layout == "Card" ? new Thickness(10) : new Thickness(7);
                GridPanel.Padding = new Thickness(0);
                GridPanel.Margin = layout == "Card" ? new Thickness(0, 0, -5, -15) : new Thickness(0, 0, -5, -15);
            }
        }

        private async Task UpdateJsonConfiguration(string newIconPath, int gridSize) {
            try {
                var filteredGroup = _groups.FirstOrDefault(g =>
                    g.Value.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));
                if (filteredGroup.Key == null) return;
                if (!int.TryParse(filteredGroup.Key, out int groupId)) return;

                Dictionary<string, (string tooltip, string args, string icon)> reorderedPaths =
                    PopupItems.ToDictionary(
                        item => item.Path,
                        item => (item.ToolTip, item.Args, item.CustomIconPath ?? ""));

                JsonConfigHelper.AddGroupToJson(
                    JsonConfigHelper.GetDefaultConfigPath(),
                    groupId, filteredGroup.Value.GroupName, filteredGroup.Value.GroupHeader,
                    newIconPath, filteredGroup.Value.GroupCol,
                    filteredGroup.Value.ShowLabels,
                    filteredGroup.Value.LabelSize > 0 ? filteredGroup.Value.LabelSize : DEFAULT_LABEL_SIZE,
                    filteredGroup.Value.LabelPosition ?? DEFAULT_LABEL_POSITION,
                    filteredGroup.Value.HeaderPosition ?? "Top",
                    filteredGroup.Value.Layout ?? "Default",
                    filteredGroup.Value.ShowOnTray,
                    reorderedPaths);

                string configPath = JsonConfigHelper.GetDefaultConfigPath();
                _json = JsonConfigHelper.ReadJsonFromFile(configPath);
                _groups = JsonSerializer.Deserialize<Dictionary<string, GroupData>>(_json, JsonOptions);
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error updating JSON configuration: {ex.Message}");
            }
        }

        private async void GridView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) {
            try {
                if (_groups == null || string.IsNullOrEmpty(_groupFilter)) return;

                var filteredGroup = _groups.FirstOrDefault(g =>
                    g.Value.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));
                if (filteredGroup.Key == null) return;

                Dictionary<string, (string tooltip, string args, string icon)> newPathOrder =
                    PopupItems.ToDictionary(
                        item => item.Path,
                        item => (item.ToolTip, item.Args, item.CustomIconPath ?? ""));

                if (!int.TryParse(filteredGroup.Key, out int groupId)) return;

                string currentIcon = filteredGroup.Value.GroupIcon;
                if (currentIcon.Contains("grid")) {
                    await CreateGridIconFromReorder();
                }
                else {
                    JsonConfigHelper.AddGroupToJson(
                        JsonConfigHelper.GetDefaultConfigPath(),
                        groupId, filteredGroup.Value.GroupName, filteredGroup.Value.GroupHeader,
                        filteredGroup.Value.GroupIcon, filteredGroup.Value.GroupCol,
                        filteredGroup.Value.ShowLabels,
                        filteredGroup.Value.LabelSize > 0 ? filteredGroup.Value.LabelSize : DEFAULT_LABEL_SIZE,
                        filteredGroup.Value.LabelPosition ?? DEFAULT_LABEL_POSITION,
                        filteredGroup.Value.HeaderPosition ?? "Top",
                        filteredGroup.Value.Layout ?? "Default",
                          filteredGroup.Value.ShowOnTray,
                        newPathOrder);
                }

                _json = File.ReadAllText(JsonConfigHelper.GetDefaultConfigPath());
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error in GridView_DragItemsCompleted: {ex.Message}");
                ShowErrorDialog($"Failed to save new item order: {ex.Message}");
            }
        }

        private string GetDisplayNameBackground(string filePath) {
            if (string.IsNullOrEmpty(filePath)) return "Unknown";
            if (Path.GetExtension(filePath).ToLower() == ".exe") {
                try {
                    var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(filePath);
                    if (!string.IsNullOrEmpty(versionInfo.FileDescription)) return versionInfo.FileDescription;
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
                        ? properties.Tooltip : GetDisplayNameBackground(path);
                    string customIconPath = !string.IsNullOrEmpty(properties.Icon) ? properties.Icon : null;

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

            var loadToken = _iconLoadCts.Token;
            var placeholder = GetOrCreatePlaceholder();

            foreach (var item in items) {
                if (loadToken.IsCancellationRequested) break;
                item.Icon = placeholder;
                PopupItems.Add(item);
            }

            int total = items.Count;
            if (total == 0) {
                GridPanel.Opacity = 1;
                return;
            }

            
            int revealThreshold = Math.Max(1, Math.Min(total, (int)Math.Ceiling(total * 0.6)));
            int loadedCount = 0;
            bool revealed = false;
            var revealLock = new object();

            void OnIconLoaded() {

                int count = Interlocked.Increment(ref loadedCount);
                if (!revealed && count >= revealThreshold) {
                    bool shouldReveal = false;
                    lock (revealLock) {
                        if (!revealed) { revealed = true; shouldReveal = true; }
                    }
                    if (shouldReveal) {
                        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, async () => {
                            if (loadToken.IsCancellationRequested) return;
                            var settings = await SettingsHelper.LoadSettingsAsync();
                            if (settings.EnableContentSlideAnimation)
                                TriggerContentAnimation();
                            else
                                GridPanel.Opacity = 1;

                        });
                    }
                }
            }

            var iconTasks = items
                .Where(_ => !loadToken.IsCancellationRequested)
                .Select(async item => {
                    await LoadIconAsync(item, item.Path, loadToken);
                    OnIconLoaded();
                })
                .ToList();

            _ = Task.WhenAll(iconTasks);
        }
        
        private async Task LoadIconAsync(PopupItem item, string path, CancellationToken token) {
            await _iconLoadSemaphore.WaitAsync(token).ConfigureAwait(false);
            try {
                if (token.IsCancellationRequested) return;

                // Resolve icon file path (background thread work)
                string iconPath;
                string customIcon = string.IsNullOrWhiteSpace(item.CustomIconPath) ? null : item.CustomIconPath;
                if (customIcon != null && File.Exists(customIcon)) {
                    iconPath = customIcon;
                }
               
                else {
                    iconPath = Path.GetExtension(path).Equals(".url", StringComparison.OrdinalIgnoreCase)
                        ? await IconHelper.GetUrlFileIconAsync(path).ConfigureAwait(false)
                        : await IconCache.GetIconPathAsync(path).ConfigureAwait(false);
                }

                if (token.IsCancellationRequested) return;
                if (string.IsNullOrEmpty(iconPath) || !File.Exists(iconPath)) return;

                // Decode image bytes on background thread
                byte[] imageBytes = await Task.Run(() => File.ReadAllBytes(iconPath), token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;

                // BitmapImage must be created on UI thread, but we minimise work there
                DispatcherQueue.TryEnqueue(async () => {
                    if (token.IsCancellationRequested) return;
                    try {
                        using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                        using var writer = new Windows.Storage.Streams.DataWriter(stream);
                        writer.WriteBytes(imageBytes);
                        await writer.StoreAsync();
                        await writer.FlushAsync();
                        stream.Seek(0);

                        var bmp = new BitmapImage();
                        bmp.DecodePixelWidth = ICON_SIZE;   // decode at display size — saves memory
                        bmp.DecodePixelHeight = ICON_SIZE;
                        await bmp.SetSourceAsync(stream);

                        if (!token.IsCancellationRequested)
                            item.Icon = bmp;
                    }
                    catch (Exception ex) {
                        Debug.WriteLine($"BitmapImage decode error for {path}: {ex.Message}");
                    }
                });
            }
            catch (OperationCanceledException) { /* expected on close */ }
            catch (Exception ex) {
                Debug.WriteLine($"Error loading icon for {path}: {ex.Message}");
            }
            finally {
                _iconLoadSemaphore.Release();
            }
        }
       

        private System.Threading.Timer _focusTimer = null;
        private DateTime _lastSubPopupOpenTime = DateTime.MinValue;

        //private void OpenSubPopup(string groupName) {
        //    var clickPos = _lastClickPos;
        //    if (_openSubPopups.TryGetValue(groupName, out var existing)) {
        //        NativeMethods.PositionWindowOffScreen(existing.GetWindowHandle());
        //        existing.Hide();
        //        existing._openSubPopups.Clear();
        //        existing._isLoaded = false;
        //        existing._parentPopup = null;
        //        _openSubPopups.Remove(groupName);
        //    }

        //    var subPopup = new PopupWindow(groupName);

        //    subPopup._parentPopup = this;
        //    subPopup._receivedCursorPos = clickPos;
        //    _openSubPopups[groupName] = subPopup;

        //    int disableTransitions = 1;
        //    NativeMethods.DwmSetWindowAttribute(subPopup.GetWindowHandle(),
        //        NativeMethods.DWMWA_TRANSITIONS_FORCEDISABLED,
        //        ref disableTransitions, sizeof(int));

        //    UpdateLastOpenTime();

        //    var root = this;
        //    while (root._parentPopup != null) root = root._parentPopup;
        //    root._focusTimer?.Dispose();
        //    root._focusTimer = null;
        //    root.StartFocusTimer();

        //    subPopup.Activate();
        //}
        private async void OpenSubPopup(string groupName) {
            var clickPos = _lastClickPos;

            if (_openSubPopups.TryGetValue(groupName, out var existing)) {
                NativeMethods.PositionWindowOffScreen(existing.GetWindowHandle());
                existing.Hide();
                existing._openSubPopups.Clear();
                existing._isLoaded = false;
                existing._parentPopup = null;
                _openSubPopups.Remove(groupName);
            }

            var subPopup = new PopupWindow(groupName);
            subPopup._parentPopup = this;
            subPopup._receivedCursorPos = clickPos;
            _openSubPopups[groupName] = subPopup;

            NativeMethods.PositionWindowOffScreen(subPopup.GetWindowHandle());



            var settings = await SettingsHelper.LoadSettingsAsync();
            if (!settings.EnableWindowSlideAnimation || !_wasLaunchedFromTaskbar) {
                int disableTransitions = 1;
                NativeMethods.DwmSetWindowAttribute(subPopup.GetWindowHandle(),
                    NativeMethods.DWMWA_TRANSITIONS_FORCEDISABLED,
                    ref disableTransitions, sizeof(int));
            }




            UpdateLastOpenTime();

            var root = this;
            while (root._parentPopup != null) root = root._parentPopup;
            root._focusTimer?.Dispose();
            root._focusTimer = null;
            root.StartFocusTimer();

            // Pre-load config, then activate
            subPopup.DispatcherQueue.TryEnqueue(async () => {
                await subPopup.LoadConfigurationForSubPopup();
                subPopup.Activate();
            });
        }
        internal async Task LoadConfigurationForSubPopup() {
            _isLoaded = true;
            _hasBeenLoaded = true;
            await LoadConfiguration();
        }
        private void UpdateLastOpenTime() {
            _lastSubPopupOpenTime = DateTime.Now;
            _parentPopup?.UpdateLastOpenTime();
        }

        private bool IsLaunchedFromTaskbar(NativeMethods.POINT? cursorOverride = null) {
            NativeMethods.POINT cursor;
            if (cursorOverride.HasValue)
                cursor = cursorOverride.Value;
            else if (!NativeMethods.GetCursorPos(out cursor))
                return false;

            IntPtr monitor = NativeMethods.MonitorFromPoint(cursor, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var mi = new NativeMethods.MONITORINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFO>() };
            NativeMethods.GetMonitorInfo(monitor, ref mi);

            bool workEqualsMonitor =
                mi.rcWork.top == mi.rcMonitor.top && mi.rcWork.bottom == mi.rcMonitor.bottom &&
                mi.rcWork.left == mi.rcMonitor.left && mi.rcWork.right == mi.rcMonitor.right;

            if (workEqualsMonitor) return NativeMethods.IsTaskbarAutoHide();

            int taskbarThickness = 60;

            if (mi.rcWork.bottom < mi.rcMonitor.bottom) {
                int taskbarTop = mi.rcWork.bottom;
                int taskbarBottom = mi.rcMonitor.bottom;
                return cursor.Y >= taskbarTop && cursor.Y <= taskbarBottom;
            }

            if (mi.rcWork.top > mi.rcMonitor.top) return cursor.Y < mi.rcWork.top + taskbarThickness;
            if (mi.rcWork.left > mi.rcMonitor.left) return cursor.X < mi.rcWork.left + taskbarThickness;
            if (mi.rcWork.right < mi.rcMonitor.right) return cursor.X > mi.rcWork.right - taskbarThickness;
            return false;
        }

        private void StartFocusTimer() {
            if (_parentPopup != null) { _parentPopup.StartFocusTimer(); return; }

            var old = Interlocked.Exchange(ref _focusTimer, null);
            old?.Dispose();

            var windowCts = _windowCts; // capture so callback doesn't touch disposed object
            System.Threading.Timer newTimer = null;
            newTimer = new System.Threading.Timer(_ => {
                if (windowCts.IsCancellationRequested) { newTimer?.Dispose(); return; }
                if ((DateTime.Now - _lastSubPopupOpenTime).TotalMilliseconds < 400) return;

                IntPtr foreground = NativeMethods.GetForegroundWindow();
                bool isInChain = IsAnywhereInChain(foreground);
                if (!isInChain) {
                    DispatcherQueue.TryEnqueue(() => {
                        if (windowCts.IsCancellationRequested) return;
                        StopFocusTimer();
                        CloseAllChildrenRecursive(_openSubPopups);
                        _openSubPopups.Clear();
                        _isLoaded = false;

                        var settings = SettingsHelper.GetCurrentSettings();

                        if (_wasLaunchedFromTaskbar && settings.EnableWindowSlideAnimation) {
                            AnimateWindowSlideDown(this.GetWindowHandle(), () => {
                                DispatcherQueue.TryEnqueue(() => {
                                    this.Hide();
                                    NativeMethods.PositionWindowOffScreen(this.GetWindowHandle());
                                });
                            });
                        }
                        else {
                            StopEntranceStoryboard();
                            this.Hide();
                            var offscreen = GetOffscreenSize();
                            _windowHelper.SetSize(offscreen.Width, offscreen.Height);
                            NativeMethods.PositionWindowOffScreen(this.GetWindowHandle());
                        }
                    });
                }
            }, null, 50, 50);

            Interlocked.Exchange(ref _focusTimer, newTimer);
        }

        private bool IsAnywhereInChain(IntPtr hwnd) {
            if (this.GetWindowHandle() == hwnd) return true;
            foreach (var sub in _openSubPopups.Values)
                if (sub.IsAnywhereInChain(hwnd)) return true;
            return false;
        }

        private void CloseAllChildrenRecursive(Dictionary<string, PopupWindow> subs) {
            foreach (var sub in subs.Values.ToList()) {
                CloseAllChildrenRecursive(sub._openSubPopups);
                sub._openSubPopups.Clear();
                sub._parentPopup = null;
                sub._isLoaded = false;
                NativeMethods.PositionWindowOffScreen(sub.GetWindowHandle());
                var offscreen = sub.GetOffscreenSize();
                sub.AppWindow.Resize(new SizeInt32(offscreen.Width, offscreen.Height));
                sub.Hide();
            }
        }

        private void StopFocusTimer() {
            if (_parentPopup != null) { _parentPopup.StopFocusTimer(); return; }
            var t = Interlocked.Exchange(ref _focusTimer, null);
            t?.Dispose();
        }
        private void GridView_ItemClick(object sender, ItemClickEventArgs e) {
            if (e.ClickedItem is PopupItem popupItem) {
                if (popupItem.IsSubgroup) {
                    OpenSubPopup(popupItem.SubgroupName);
                }
              
                else {
                    var root = this;
                    while (root._parentPopup != null) root = root._parentPopup;

                    string capturedPath = popupItem.Path;
                    string capturedArgs = popupItem.Args;

                    root.DispatcherQueue.TryEnqueue(async () => {
                        root.StopFocusTimer();
                        CloseAllChildrenRecursive(root._openSubPopups);
                        root._openSubPopups.Clear();
                        root._isLoaded = false;

                        var settings = await SettingsHelper.LoadSettingsAsync();
                        if (root._wasLaunchedFromTaskbar && settings.EnableWindowSlideAnimation) {
                            root.AnimateWindowSlideDown(root.GetWindowHandle(), () => {
                                root.DispatcherQueue.TryEnqueue(() => {
                                    root.Hide();
                                    NativeMethods.PositionWindowOffScreen(root.GetWindowHandle());
                                });
                            });
                        }
                        else {
                            root.Hide();
                            NativeMethods.PositionWindowOffScreen(root.GetWindowHandle());
                        }

                        TryLaunchApp(capturedPath, capturedArgs);
                    });

                    if (_parentPopup != null) this.Hide();
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
        private bool _isCardLayout;

        private async void Window_Activated(object sender, WindowActivatedEventArgs e) {
            if (e.WindowActivationState == WindowActivationState.Deactivated) {
                if (_parentPopup != null) return;
                if (_focusTimer != null) return;
                if (_isClosing) return;

                _isClosing = true;

                bool wasLoaded = _isLoaded;
                if (!_isFlyoutOpen)
                    _isLoaded = false;
                CleanupUISettings();

                if (_hasBeenLoaded && !string.IsNullOrEmpty(_groupFilter) && wasLoaded) {
                    var settings = await SettingsHelper.LoadSettingsAsync();

                    void DoCleanup() {
                        StopEntranceStoryboard();
                        _wasLaunchedFromTaskbar = false;
                        this.Hide();
                        var offscreen = GetOffscreenSize();
                        _windowHelper.SetSize(offscreen.Width, offscreen.Height);
                        NativeMethods.PositionWindowOffScreen(this.GetWindowHandle());

                        try {
                            UnsubscribeGridViewHandlers();
                            _iconLoadCts.Cancel();

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

                            foreach (var task in _backgroundTasks.ToList())
                                if (task.IsCompleted) { task.Dispose(); _backgroundTasks.Remove(task); }

                            _groups = null;
                            _json = "";
                            _clickedItem = null;
                            _gridView = null;
                        }
                        catch (Exception ex) {
                            Debug.WriteLine($"UI cleanup error: {ex.Message}");
                        }
                        finally {
                            _isClosing = false;
                        }
                    }

                    this.DispatcherQueue.TryEnqueue(() => {
                        if (_wasLaunchedFromTaskbar && settings.EnableWindowSlideAnimation) {
                            AnimateWindowSlideDown(this.GetWindowHandle(), () => {
                                DispatcherQueue.TryEnqueue(DoCleanup);
                            });
                        }
                        else {
                            DoCleanup();
                        }
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

                    _ = Task.Run(() => GC.Collect(0, GCCollectionMode.Optimized));
                }
                else {
                    _isClosing = false;
                }
            }
            else if (e.WindowActivationState == WindowActivationState.CodeActivated ||
                     e.WindowActivationState == WindowActivationState.PointerActivated) {
            
                if (_isClosing) return;

                if (!_isLoaded && !_isFlyoutOpen) {
                    await LoadConfiguration();
                    _isLoaded = true;
                    _hasBeenLoaded = true;
                }

                if (useFileMode) {
                    if (_cachedAppFolderPath == null) {
                        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        _cachedAppFolderPath = Path.Combine(appDataPath, "AppGroup");
                        _cachedLastOpenPath = Path.Combine(_cachedAppFolderPath, "lastOpen");
                    }
                    try {
                        if (File.Exists(_cachedLastOpenPath)) {
                            string fileGroupFilter = File.ReadAllText(_cachedLastOpenPath).Trim();
                            if (!string.IsNullOrEmpty(fileGroupFilter))
                                _groupFilter = fileGroupFilter;
                        }
                    }
                    catch (Exception ex) {
                        Debug.WriteLine($"Error reading group name from file: {ex.Message}");
                    }
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
                    _ = Task.Run(async () => {
                        try { await UpdateTaskbarIcon(_groupFilter); }
                        catch (Exception ex) { Debug.WriteLine($"Background taskbar update error: {ex.Message}"); }
                    });
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
                string groupIcon = IconHelper.FindOrigIcon(iconGroup);
                _originalIconPath = groupIcon;

                if (!string.IsNullOrEmpty(_originalIconPath) && File.Exists(_originalIconPath) && settings.UseGrayscaleIcon) {
                    _iconWithBackgroundPath = await IconHelper.CreateBlackWhiteIconAsync(_originalIconPath);
                    await TaskbarManager.UpdateTaskbarShortcutIcon(groupName, _iconWithBackgroundPath);
                }
            }
            catch (Exception ex) {
                Console.WriteLine($"Error updating taskbar icon with background: {ex.Message}");
            }
        }

        private void TryLaunchApp(string path, string args) {
            try {
                var psi = new System.Diagnostics.ProcessStartInfo {
                    FileName = "cmd.exe",
                    Arguments = $"/c start \"\" \"{path}\" {args}",
                    UseShellExecute = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };
                System.Diagnostics.Process.Start(psi)?.Close();
            }
            catch (Exception ex) {
                Debug.WriteLine($"Failed to launch {path}: {ex.Message}");
                ShowErrorDialog($"Failed to launch {path}: {ex.Message}");
            }
        }

        private void TryRunAsAdmin(string path, string args) {
            try {
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) throw new InvalidOperationException("Shell.Application COM object not found.");
                dynamic shell = Activator.CreateInstance(shellType);
                shell.ShellExecute(path, args, "", "runas", 1);
            }
            catch (Exception ex) {
                Debug.WriteLine($"Failed to run as admin {path}: {ex.Message}");
                ShowErrorDialog($"Failed to run as admin {path}: {ex.Message}");
            }
        }

        private void OpenFileLocation(string path) {
            try {
                string directory = Path.GetDirectoryName(path);
                if (Directory.Exists(directory))
                    System.Diagnostics.Process.Start("explorer.exe", directory);
                else
                    throw new Exception("Directory does not exist.");
            }
            catch (Exception ex) {
                Debug.WriteLine($"Failed to open file location {path}: {ex.Message}");
                ShowErrorDialog($"Failed to open file location {path}: {ex.Message}");
            }
        }

        private void ShowErrorDialog(string message) {
            var dialog = new DialogWindow("Error", message);
            _ = dialog.ShowDialogAsync();
        }

        private MenuFlyout CreateItemFlyout() {
            MenuFlyout flyout = new MenuFlyout();

            var openItem = new MenuFlyoutItem { Text = "Open", Icon = new FontIcon { Glyph = "\ue8a7" } };
            openItem.Click += OpenItem_Click;
            flyout.Items.Add(openItem);

            var runAsAdminItem = new MenuFlyoutItem { Text = "Run as Administrator", Icon = new FontIcon { Glyph = "\uE7EF" } };
            runAsAdminItem.Click += RunAsAdminItem_Click;
            flyout.Items.Add(runAsAdminItem);

            var fileLocationItem = new MenuFlyoutItem { Text = "Open File Location", Icon = new FontIcon { Glyph = "\ued43" } };
            fileLocationItem.Click += OpenFileLocation_Click;
            flyout.Items.Add(fileLocationItem);

            flyout.Items.Add(new MenuFlyoutSeparator());

            var editItem = new MenuFlyoutItem { Text = "Edit this Group", Icon = new FontIcon { Glyph = "\ue70f" } };
            editItem.Click += EditGroup_Click;
            flyout.Items.Add(editItem);

            var launchAll = new MenuFlyoutItem { Text = "Launch All", Icon = new FontIcon { Glyph = "\ue8a9" } };
            launchAll.Click += launchAllGroup_Click;
            flyout.Items.Add(launchAll);

            return flyout;
        }

        private async void launchAllGroup_Click(object sender, RoutedEventArgs e) {
            if (!string.IsNullOrEmpty(_groupFilter)) {
                var matchingGroup = _groups?.Values.FirstOrDefault(g =>
                    g.GroupName.Equals(_groupFilter, StringComparison.OrdinalIgnoreCase));
                if (matchingGroup != null)
                    await JsonConfigHelper.LaunchAll(matchingGroup.GroupName);
            }
        }

        private void EditGroup_Click(object sender, RoutedEventArgs e) {
            EditGroupHelper editGroup = new EditGroupHelper("Edit Group", _groupId);
            editGroup.Activate();
        }

        private void OpenItem_Click(object sender, RoutedEventArgs e) {
            if (_clickedItem != null) TryLaunchApp(_clickedItem.Path, _clickedItem.Args);
        }

        private void RunAsAdminItem_Click(object sender, RoutedEventArgs e) {
            if (_clickedItem != null) TryRunAsAdmin(_clickedItem.Path, _clickedItem.Args);
        }

        private void OpenFileLocation_Click(object sender, RoutedEventArgs e) {
            if (_clickedItem != null) OpenFileLocation(_clickedItem.Path);
        }

        private string GetDisplayName(string filePath) {
            if (string.IsNullOrEmpty(filePath)) return "Unknown";
            string extension = Path.GetExtension(filePath).ToLower();
            if (string.IsNullOrEmpty(extension)) return Path.GetFileName(filePath);
            if (extension == ".exe") {
                try {
                    var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(filePath);
                    if (!string.IsNullOrEmpty(versionInfo.FileDescription)) return versionInfo.FileDescription;
                }
                catch { }
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