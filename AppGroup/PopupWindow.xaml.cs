using IWshRuntimeLibrary;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using WinRT.Interop;
using WinUIEx;

namespace AppGroup {
    public class GroupData {
        public List<string> path { get; set; }
        public string groupIcon { get; set; }
        public string groupName { get; set; }
        public bool groupHeader { get; set; }
        public int groupCol { get; set; }
        public int groupId { get; set; }
    }
    public class PopupItem {

        public string Path { get; set; }
        public string Name { get; set; }
        public string ToolTip { get; set; }
        public BitmapImage Icon { get; set; }




       
    }
    public sealed partial class PopupWindow : Window {
        private readonly Dictionary<int, EditGroupWindow> _openEditWindows = new Dictionary<int, EditGroupWindow>();

        private const int BUTTON_SIZE = 40; // Reduced from 50
        private const int ICON_SIZE = 25;   // Reduced from 25
        private const int BUTTON_MARGIN = 4; // Reduced from 8

        private string groupFilter = null;
        private string json = "";


        private WindowHelper _windowHelper;

        private readonly Window _window;

        private SystemBackdropConfiguration _configurationSource;
        private MicaBackdrop _micaBackdrop;
        private bool _micaEnabled;
        ObservableCollection<PopupItem> PopupItems = new ObservableCollection<PopupItem>();
        private PopupItem clickedItem;

        private GridView gridView;
        private int groupId;
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true
        };

        private Dictionary<string, GroupData> groups;

        public PopupWindow(string groupFilter = null) {


            this.InitializeComponent();

            this.groupFilter = groupFilter;
            this.Title = groupFilter;


            _windowHelper = new WindowHelper(this);

            _windowHelper.SetSystemBackdrop(WindowHelper.BackdropType.AcrylicBase);
            _windowHelper.IsMaximizable = false;
            _windowHelper.IsMinimizable = false;
            _windowHelper.IsResizable = true;
            _windowHelper.HasBorder = true;
            _windowHelper.HasTitleBar = false;




            _ = LoadConfigurationAsync();
        }
        private async Task LoadConfigurationAsync() {
            json = await LoadConfigurationFromFileAsync();
            groups = DeserializeGroups(json);
            await InitializeWindowAsync();
        }
        private Dictionary<string, GroupData> DeserializeGroups(string json) {
            return JsonSerializer.Deserialize<Dictionary<string, GroupData>>(json, JsonOptions);
        }
        private async Task<string> LoadConfigurationFromFileAsync() {
            try {
                string configPath = JsonConfigHelper.GetDefaultConfigPath();
                return JsonConfigHelper.ReadJsonFromFile(configPath);
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error loading configuration: {ex.Message}");
                return GetDefaultJsonConfiguration();
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
        private async Task InitializeWindowAsync() {
      
            int maxPathItems = 1;
            int maxColumns = 1;
            string groupIcon = "AppGroup.ico";
            bool groupHeader = false;

           
            // If we have a group filter, only consider that group
            if (!string.IsNullOrEmpty(groupFilter) && groups.Values.Any(g => g.groupName.Equals(groupFilter, StringComparison.OrdinalIgnoreCase))) {
                var filteredGroup = groups.FirstOrDefault(g => g.Value.groupName.Equals(groupFilter, StringComparison.OrdinalIgnoreCase));

                maxPathItems = filteredGroup.Value.path.Count;
                maxColumns = filteredGroup.Value.groupCol;

                groupHeader = filteredGroup.Value.groupHeader;

                groupIcon = filteredGroup.Value.groupIcon;

                if (!int.TryParse(filteredGroup.Key, out groupId)) {
                    Debug.WriteLine($"Error: Group key '{filteredGroup.Key}' is not a valid integer ID");
                    return;
                }
            }
            else {
                foreach (var group in groups.Values) {
                    maxPathItems = Math.Max(maxPathItems, group.path.Count);
                    maxColumns = Math.Max(maxColumns, group.groupCol);
                }
            }

            int numberOfRows = (int)Math.Ceiling((double)maxPathItems / maxColumns);
            int dynamicWidth = maxColumns * (BUTTON_SIZE + BUTTON_MARGIN * 2);
            if (groupHeader == true) {
                if (maxColumns < 2) {
                    dynamicWidth = 2 * (BUTTON_SIZE + BUTTON_MARGIN * 2);

                }
            }

            int dynamicHeight = numberOfRows * (BUTTON_SIZE + BUTTON_MARGIN * 2);
            var displayInfo = GetDisplayInformation();
            float scaleFactor = displayInfo.Item1;

            Debug.WriteLine($"Display scale factor: {scaleFactor}");

            int scaledWidth = (int)(dynamicWidth * scaleFactor);
            int scaledHeight = (int)(dynamicHeight * scaleFactor);
            if (groupHeader == true) {
                scaledHeight += 40;
            }

            this.AppWindow.SetIcon(groupIcon);




            MainGrid.Margin = new Thickness(0, 0, -5, -15);

            int finalWidth = scaledWidth + 30;
            int finalHeight = scaledHeight + 20;
            this.AppWindow.Resize(new SizeInt32(finalWidth, finalHeight));

            PositionAboveCursor(this.AppWindow, finalHeight);
            await CreateDynamicContentAsync(json);

            this.Activated += Window_Activated;

          
        }


        private Tuple<float, int, int> GetDisplayInformation() {
            var hwnd = WindowNative.GetWindowHandle(this);

            uint dpi = GetDpiForWindow(hwnd);
            float scaleFactor = (float)dpi / 96.0f;
            RECT rect;

            GetWindowRect(hwnd, out rect);

            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            MONITORINFOEX monitorInfo = new MONITORINFOEX();
            monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
            GetMonitorInfo(monitor, ref monitorInfo);

            int screenWidth = monitorInfo.rcMonitor.right - monitorInfo.rcMonitor.left;
            int screenHeight = monitorInfo.rcMonitor.bottom - monitorInfo.rcMonitor.top;

            Debug.WriteLine($"DPI: {dpi}, Scale Factor: {scaleFactor}, Screen: {screenWidth}x{screenHeight}");

            return new Tuple<float, int, int>(scaleFactor, screenWidth, screenHeight);
        }


        private void GridView_RightTapped(object sender, RightTappedRoutedEventArgs e) {
            var gridView = sender as GridView;
            var item = (e.OriginalSource as FrameworkElement)?.DataContext as PopupItem;

            if (item != null) {
                MenuFlyout flyout = CreateItemFlyout();
                flyout.ShowAt(gridView, e.GetPosition(gridView));

                clickedItem = item;
            }
        }



        private void EditGroup_Click(object sender, RoutedEventArgs e) {

            EditGroupHelper editGroup = new EditGroupHelper("Edit Group", groupId);
            editGroup.Activate();

        }

        private void OpenItem_Click(object sender, RoutedEventArgs e) {
            if (clickedItem != null) {
                TryLaunchApp(clickedItem.Path);
            }
        }

        private void RunAsAdminItem_Click(object sender, RoutedEventArgs e) {
            if (clickedItem != null) {
                TryRunAsAdmin(clickedItem.Path);
            }
        }

        private void OpenFileLocation_Click(object sender, RoutedEventArgs e) {
            if (clickedItem != null) {
                OpenFileLocation(clickedItem.Path);
            }
        }

        private void TryLaunchApp(string path) {
            try {
                ProcessStartInfo psi = new ProcessStartInfo {
                    FileName = path,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex) {
                Debug.WriteLine($"Failed to launch {path}: {ex.Message}");
                ShowErrorDialog($"Failed to launch {path}: {ex.Message}");
            }
        }

        private void TryRunAsAdmin(string path) {
            try {
                ProcessStartInfo psi = new ProcessStartInfo {
                    FileName = path,
                    Verb = "runas",
                    UseShellExecute = true // Ensure this is set to true
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
                string directory = System.IO.Path.GetDirectoryName(path);

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




        private async void ShowErrorDialog(string message) {
            ContentDialog dialog = new ContentDialog {
                Title = "Error",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
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

            MenuFlyoutItem deleteItem = new MenuFlyoutItem {
                Text = "Open File Location",
                Icon = new FontIcon { Glyph = "\ued43" }
            }; ;
            deleteItem.Click += OpenFileLocation_Click;
            flyout.Items.Add(deleteItem);
            MenuFlyoutSeparator separator = new MenuFlyoutSeparator();
            flyout.Items.Add(separator);
            MenuFlyoutItem editItem = new MenuFlyoutItem {
                Text = "Edit this Group",
                Icon = new FontIcon { Glyph = "\ue70f" }
            }; ;
            editItem.Click += EditGroup_Click;
            flyout.Items.Add(editItem);

            return flyout;
        }


        private async Task CreateDynamicContentAsync(string json) {
            // Increase button size and add margin between buttons

            const int EFFECTIVE_BUTTON_WIDTH = BUTTON_SIZE + (BUTTON_MARGIN * 2);

            var options = new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true
            };

            
            bool anyGroupDisplayed = false;

            foreach (var group in groups) {
                // Skip this group if filtering is active and this isn't the requested group
                if (groupFilter != null && !group.Value.groupName.Equals(groupFilter, StringComparison.OrdinalIgnoreCase)) {
                    Debug.WriteLine($"Skipping group {group.Value.groupName} due to filter");
                    continue;
                }

                anyGroupDisplayed = true;
                Debug.WriteLine($"Displaying group: {group.Value.groupName}");

                if (group.Value.groupHeader == true) {
                    Header.Visibility = Visibility.Visible;
                    HeaderText.Text = group.Value.groupName;
                    ScrollView.Margin = new Thickness(0, 0, 0, 5);

                }
                else {

                    ScrollView.Margin = new Thickness(0, 5, 0, 5);

                }



                int totalItems = group.Value.path.Count;
                int groupColumns = group.Value.groupCol;

                gridView = new GridView();
                gridView.SelectionMode = ListViewSelectionMode.Extended;
                gridView.IsItemClickEnabled = true;
                gridView.HorizontalAlignment = HorizontalAlignment.Left;
                gridView.CanDragItems = true;
                gridView.CanReorderItems = true;
                gridView.AllowDrop = true;
                gridView.DragItemsCompleted += GridView_DragItemsCompleted;

                int gridWidth = groupColumns * EFFECTIVE_BUTTON_WIDTH + BUTTON_MARGIN * 2;

                gridView.RightTapped += GridView_RightTapped;
                gridView.ItemsPanel = (ItemsPanelTemplate)XamlReader.Load(
                    $@"<ItemsPanelTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                     xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
                        <ItemsWrapGrid  
                                      Orientation=""Horizontal"" 
                                      ItemWidth=""{EFFECTIVE_BUTTON_WIDTH}"" 
                                      ItemHeight=""{EFFECTIVE_BUTTON_WIDTH}""
                                      HorizontalAlignment=""Center""
                                      VerticalAlignment=""Center""/>
                    </ItemsPanelTemplate>");




                for (int i = 0; i < totalItems; i++) {
                    string path = group.Value.path[i];
                    string displayName = GetDisplayName(path);
                    var PopupItem = new PopupItem {
                        Path = path,
                        Name = System.IO.Path.GetFileNameWithoutExtension(path),
                        ToolTip = $"{displayName}"
                    };
                    PopupItem.Icon = await IconCache.LoadImageFromPathAsync(await IconCache.GetIconPathAsync(path));

                    PopupItems.Add(PopupItem);
                }

                gridView.ItemsSource = PopupItems;

                gridView.ItemTemplate = (DataTemplate)XamlReader.Load(
      $@"<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
       xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
        <Grid Width=""" + BUTTON_SIZE + @""" Height=""" + BUTTON_SIZE + @""" VerticalAlignment=""Center"" HorizontalAlignment=""Center""
              ToolTipService.ToolTip=""{Binding ToolTip}"">
            <Grid VerticalAlignment=""Center"" HorizontalAlignment=""Center"">
                <Image Source=""{Binding Icon}""
                       Width=""" + ICON_SIZE + @"""
                       Height=""" + ICON_SIZE + @"""
                       Stretch=""Uniform""
                       VerticalAlignment=""Center""
                       HorizontalAlignment=""Center""
                       Margin=""0"" />
            </Grid>
        </Grid>
    </DataTemplate>");





                gridView.ItemClick += (sender, e) => {
                    if (e.ClickedItem is PopupItem PopupItem) {
                        TryLaunchApp(PopupItem.Path);

                    }
                };

                GridPanel.Children.Add(gridView);
            }

            if (!anyGroupDisplayed) {
                TextBlock noGroupsText = new TextBlock {
                    Text = $"No group found matching '{groupFilter}'",
                    Margin = new Thickness(10),
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                GridPanel.Children.Add(noGroupsText);

                this.AppWindow.Resize(new SizeInt32(250, 120));
                PositionAboveCursor(this.AppWindow, 120);


            }
        }


        private string GetDisplayName(string filePath) {
            string extension = Path.GetExtension(filePath).ToLower();

            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(extension)) {
                Debug.WriteLine("File path or extension is null or empty.");
                return "Unknown";
            }

            if (extension == ".exe") {
                try {
                    FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(filePath);
                    if (!string.IsNullOrEmpty(versionInfo.FileDescription)) {
                        return versionInfo.FileDescription;
                    }
                    else {
                        Debug.WriteLine("File description is empty.");
                    }
                }
                catch (Exception ex) {
                    Debug.WriteLine($"Exception retrieving file description: {ex.Message}");
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
                    else {
                        Debug.WriteLine("Shortcut target path is empty.");
                    }
                }
                catch (Exception ex) {
                    Debug.WriteLine($"Exception retrieving shortcut target path: {ex.Message}");
                }
            }

            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrEmpty(fileNameWithoutExtension)) {
                Debug.WriteLine("File name without extension is empty.");
                return "Unknown";
            }

            return fileNameWithoutExtension;
        }

        private void GridView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) {
            try {
               

                if (groups == null || string.IsNullOrEmpty(groupFilter)) {
                    Debug.WriteLine("Error: Unable to deserialize groups or group filter is not set");
                    return;
                }

                var filteredGroup = groups.FirstOrDefault(g => g.Value.groupName.Equals(groupFilter, StringComparison.OrdinalIgnoreCase));

                if (filteredGroup.Key == null) {
                    Debug.WriteLine($"Error: Group '{groupFilter}' not found in configuration");
                    return;
                }

                string[] newPathOrder = PopupItems.Select(file => file.Path).ToArray();

                int groupId;
                if (!int.TryParse(filteredGroup.Key, out groupId)) {
                    Debug.WriteLine($"Error: Group key '{filteredGroup.Key}' is not a valid integer ID");
                    return;
                }

                string groupName = filteredGroup.Value.groupName;
                bool groupHeader = filteredGroup.Value.groupHeader;
                string groupIcon = filteredGroup.Value.groupIcon;
                int groupCol = filteredGroup.Value.groupCol;

                JsonConfigHelper.AddGroupToJson(
                    JsonConfigHelper.GetDefaultConfigPath(),
                    groupId,
                    groupName,
                    groupHeader,
                    groupIcon,
                    groupCol,
                    newPathOrder
                );

                json = System.IO.File.ReadAllText(JsonConfigHelper.GetDefaultConfigPath());

                Debug.WriteLine($"Successfully updated order for group '{groupFilter}' with ID {groupId}");
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error in GridView_DragItemsCompleted: {ex.Message}");
                ContentDialog dialog = new ContentDialog {
                    Title = "Error",
                    Content = $"Failed to save new item order: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                _ = dialog.ShowAsync();
            }
        }



        private OverlappedPresenter GetAppWindowAndPresenter() {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId myWndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var _apw = AppWindow.GetFromWindowId(myWndId);
            return _apw.Presenter as OverlappedPresenter;
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs e) {
            if (e.WindowActivationState == WindowActivationState.Deactivated) {
                this.Close();

            }

        }
        public static class NativeMethods {
            [DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            public const int SW_HIDE = 0;
            public const int SW_SHOW = 5;
        }
        private void AppButton_Click(object sender, RoutedEventArgs e, string appPath) {
            try {
                ProcessStartInfo psi = new ProcessStartInfo(appPath);
                Process.Start(psi);
                this.Close();
            }
            catch (Exception ex) {
                Debug.WriteLine($"Failed to launch {appPath}: {ex.Message}");
                ContentDialog dialog = new ContentDialog {
                    Title = "Error",
                    Content = $"Failed to launch {appPath}: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                _ = dialog.ShowAsync();
            }
        }

        private void PositionAboveCursor(AppWindow appWindow, int windowHeight) {
            try {
                // Get current cursor position
                POINT cursorPos = GetCursorPos();
                Debug.WriteLine($"Cursor position: {cursorPos.X}, {cursorPos.Y}");

                int windowWidth = appWindow.Size.Width;
                Debug.WriteLine($"Window size: {windowWidth}x{windowHeight}");

                int x = cursorPos.X - (windowWidth / 2);
                int y = cursorPos.Y - windowHeight - 20;
                var hwnd = WindowNative.GetWindowHandle(this);
                IntPtr monitor = MonitorFromPoint(cursorPos, MONITOR_DEFAULTTONEAREST);

                MONITORINFO monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO));
                GetMonitorInfo(monitor, ref monitorInfo);
                if (x < monitorInfo.rcWork.left)
                    x = monitorInfo.rcWork.left;
                if (x + windowWidth > monitorInfo.rcWork.right)
                    x = monitorInfo.rcWork.right - windowWidth;
                if (y < monitorInfo.rcWork.top)
                    y = monitorInfo.rcWork.top;

                Debug.WriteLine($"Moving window to: {x}, {y}");

                appWindow.Move(new Windows.Graphics.PointInt32(x, y));
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error positioning window: {ex.Message}");
                appWindow.Move(new Windows.Graphics.PointInt32(50, 50));
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }
        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);


        [StructLayout(LayoutKind.Sequential)]
        private struct RECT {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFOEX {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        // SHFILEINFO structure
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private static POINT GetCursorPos() {
            GetCursorPos(out POINT point);
            return point;
        }
    }

  
}