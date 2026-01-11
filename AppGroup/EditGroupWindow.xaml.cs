using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinUIEx;
using IWshRuntimeLibrary;
using Windows.ApplicationModel.DataTransfer;
using File = System.IO.File;
using System.Text.RegularExpressions;
using WinUIEx.Messaging;
using System.Drawing;
using Microsoft.UI.Xaml.Media;

namespace AppGroup {
    public class ExeFileModel {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string Icon { get; set; }
        public string Tooltip { get; set; }
        public string Args { get; set; }
        public string IconPath { get; set; } // Add this property for custom icon path

        //// Add this property for display
        //public ImageSource DisplayIcon => !string.IsNullOrEmpty(IconPath) ?
        //    new BitmapImage(new Uri(IconPath)) :
        //    new BitmapImage(new Uri(Icon));
    }



    public sealed partial class EditGroupWindow : WinUIEx.WindowEx {
        public int GroupId { get; private set; }
        private string selectedIconPath = string.Empty;
        private string selectedFilePath = string.Empty;
        private ObservableCollection<ExeFileModel> ExeFiles = new ObservableCollection<ExeFileModel>();
        private bool regularIcon = true;
        private string? lastSelectedItem;
        private string? copiedImagePath;
        private string tempIcon;
        private string? groupName;
        private FileSystemWatcher fileWatcher;
        private string groupIdFilePath;
        private int? lastGroupId = null;
        private ExeFileModel CurrentItem { get; set; }
        private string originalItemIconPath = null;
        private bool _isDialogRepositioning = false;

        private const int DEFAULT_LABEL_SIZE = 12;
        private const string DEFAULT_LABEL_POSITION = "Bottom";

        public EditGroupWindow(int groupId) {
   
            this.InitializeComponent();

            GroupId = groupId;

            this.CenterOnScreen();

            var iconPath = Path.Combine(AppContext.BaseDirectory, "EditGroup.ico");
            this.AppWindow.SetIcon(iconPath);
            string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appDataPath = Path.Combine(localAppDataPath, "AppGroup");

            if (!Directory.Exists(appDataPath)) {
                Directory.CreateDirectory(appDataPath);
            }

            //groupIdFilePath = Path.Combine(appDataPath, "gid");

            //if (!File.Exists(groupIdFilePath)) {
            //    File.WriteAllText(groupIdFilePath, groupId.ToString());
            //}
            //else {
            //    string existingGroupIdText = File.ReadAllText(groupIdFilePath);
            //    if (int.TryParse(existingGroupIdText, out int existingGroupId)) {
            //        GroupId = existingGroupId;
            //    }
            //}

            //fileWatcher = new FileSystemWatcher(Path.GetDirectoryName(groupIdFilePath));
            //fileWatcher.Filter = Path.GetFileName(groupIdFilePath);
            //fileWatcher.Changed += OnGroupIdFileChanged;
            //fileWatcher.EnableRaisingEvents = true;


            ExeListView.ItemsSource = ExeFiles;

            MinHeight = 600;
            MinWidth = 530;
            ExtendsContentIntoTitleBar = true;
           
           
            ThemeHelper.UpdateTitleBarColors(this);
            _ = LoadGroupDataAsync(GroupId);
            Closed += MainWindow_Closed;
            this.AppWindow.Closing += AppWindow_Closing;

            ApplicationCount.Text = "Item";
            NativeMethods.SetCurrentProcessExplicitAppUserModelID("AppGroup.EditGroup");
            Activated += EditGroupWindow_Activated;
            //this.AppWindow.Changed += AppWindow_Changed;

            this.SizeChanged += EditGroupWindow_SizeChanged;
        }


        private async void EditGroupWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args) {

            await HideAllDialogsAsync();
            //if (EditItemDialog.Visibility == Visibility.Visible && !_isDialogRepositioning) {
            //    _isDialogRepositioning = true;
            //    System.Diagnostics.Debug.WriteLine("Dialog was visible, hiding and reshowing...");

            //    try {
            //        EditItemDialog.Hide();
            //        EditItemDialog.XamlRoot = this.Content.XamlRoot;
            //        //await Task.Delay(100);
            //        //_= EditItemDialog.ShowAsync();
            //    }
            //    finally {
            //        _isDialogRepositioning = false;
            //    }
            //}
        }

        private async void EditGroupWindow_Activated(object sender, WindowActivatedEventArgs e) {

            if (e.WindowActivationState == WindowActivationState.Deactivated) {
               
                _ = Task.Run(() => {
                    GC.Collect(0, GCCollectionMode.Optimized);
                });
                //NativeMethods.EmptyWorkingSet(Process.GetCurrentProcess().Handle);
            }

            if (e.WindowActivationState == WindowActivationState.CodeActivated) {
                // Store the current GroupId to compare against
                int previousGroupId = GroupId;
                int newGroupId = -1; // Default value

                // Read group filter from file each time window is activated

                try {
                    string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string appFolderPath = Path.Combine(appDataPath, "AppGroup");
                    string filePath = Path.Combine(appFolderPath, "lastEdit");

                    if (File.Exists(filePath)) {
                        string fileGroupIdText = File.ReadAllText(filePath).Trim();
                        if (!string.IsNullOrEmpty(fileGroupIdText) && int.TryParse(fileGroupIdText, out int fileGroupId)) {
                            newGroupId = fileGroupId;
                        }
                    }
                }
                catch (Exception ex) {
                    Debug.WriteLine($"Error reading group name from file: {ex.Message}");
                }

                // Update GroupId and only load data if it changed
                GroupId = newGroupId;
                if (GroupId != previousGroupId) {
                    await LoadGroupDataAsync(-1);
                    await LoadGroupDataAsync(GroupId);
                    Debug.WriteLine($"GroupId changed from {previousGroupId} to {GroupId}, data reloaded");
                }
                else {
                    Debug.WriteLine($"GroupId unchanged ({GroupId}), skipping data reload");
                }
            }
        }

        private async void ShowActivationDialog(string id) {
            try {
                var dialog = new ContentDialog() {
                    Title = "Window Activated",
                    Content = "The group id is " +id,
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot // Assuming 'this' is your MainWindow
                };

                await dialog.ShowAsync();
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error showing dialog: {ex.Message}");
            }
        }

        private async void OnGroupIdFileChanged(object sender, FileSystemEventArgs e) {
            try {
                if (File.Exists(groupIdFilePath)) {
                    using (FileStream stream = new FileStream(groupIdFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader reader = new StreamReader(stream)) {
                        string newGroupIdText = await reader.ReadToEndAsync();

                        if (int.TryParse(newGroupIdText, out int newGroupId)) {
                            if (lastGroupId != newGroupId)
                            {
                                lastGroupId = newGroupId;
                                GroupId = newGroupId;

                                DispatcherQueue.TryEnqueue(() => {
                                    ExeFiles.Clear();
                                     LoadGroupDataAsync(GroupId);
                           
                                });

                               
                            }
                        }
                    }
                }
            }
            catch (IOException ex) {
                Debug.WriteLine("File read error: " + ex.Message);
            }
            catch (Exception ex) {
                Debug.WriteLine("Unexpected error: " + ex.Message);
            }
        }

        private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args) {
            args.Cancel = true;
            GroupId = -1;
            await HideAllDialogsAsync();
            this.Hide();        // Just hide the window
        }

        private async Task HideAllDialogsAsync() {
            var dialogs = FindVisualChildren<ContentDialog>(this.Content);

            foreach (var dialog in dialogs) {
                if (dialog.Visibility == Visibility.Visible) {
                    dialog.Hide();
                }
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject {
            if (depObj != null) {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++) {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);

                    if (child != null && child is T) {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child)) {
                        yield return childOfChild;
                    }
                }
            }
        }
        private void MainWindow_Closed(object sender, WindowEventArgs args) {

        



            fileWatcher.Dispose();
            if (File.Exists(groupIdFilePath)) {
                File.Delete(groupIdFilePath);
            }
            if (!string.IsNullOrEmpty(tempIcon)) {
                string tempFolder = Path.GetDirectoryName(tempIcon);
                Directory.Delete(tempFolder, true);
            }
        }

     
        private async void ExeListView_DragEnter(object sender, DragEventArgs e) {
            try {
                if (e.DataView.Contains(StandardDataFormats.StorageItems)) {
                    e.AcceptedOperation = DataPackageOperation.Copy;
                }
                else {
                    e.AcceptedOperation = DataPackageOperation.None;
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Drag Enter Error: {ex.Message}");
            }
        }

        private async void ExeListView_Drop(object sender, DragEventArgs e) {
            try {
                if (e.DataView.Contains(StandardDataFormats.StorageItems)) {
                    var items = await e.DataView.GetStorageItemsAsync();

                    foreach (var item in items) {
                        if (item is StorageFile file &&
                            (file.FileType.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                             file.FileType.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
                             file.FileType.Equals(".url", StringComparison.OrdinalIgnoreCase))) {

                            string icon;

                            if (file.FileType.Equals(".url", StringComparison.OrdinalIgnoreCase)) {
                                icon = await IconHelper.GetUrlFileIconAsync(file.Path);
                            }
                            else {
                                icon = await IconCache.GetIconPathAsync(file.Path);
                            }

                            ExeFiles.Add(new ExeFileModel {
                                FileName = file.Name,
                                Icon = icon,
                                FilePath = file.Path
                            });
                        }
                    }

                    // Reuse the existing logic from BrowseFiles method
                    ExeListView.ItemsSource = ExeFiles;
                    lastSelectedItem = GroupColComboBox.SelectedItem as string;
                    ApplicationCount.Text = ExeListView.Items.Count > 1
                        ? ExeListView.Items.Count.ToString() + " Items"
                        : ExeListView.Items.Count == 1
                        ? "1 Item"
                        : "";
                    IconGridComboBox.Items.Clear();
                    if (ExeFiles.Count >= 9) {
                        IconGridComboBox.Items.Add("2");
                        IconGridComboBox.Items.Add("3");
                        IconGridComboBox.SelectedItem = "3";
                    }
                    else {
                        IconGridComboBox.Items.Add("2");
                        IconGridComboBox.SelectedItem = "2";
                    }

                    GroupColComboBox.Items.Clear();
                    for (int i = 1; i <= ExeFiles.Count; i++) {
                        GroupColComboBox.Items.Add(i.ToString());
                    }
                    if (ExeFiles.Count > 3) {
                        if (lastSelectedItem != null) {
                            GroupColComboBox.SelectedItem = lastSelectedItem;

                        }
                        else {
                            GroupColComboBox.SelectedItem = "3";

                        }
                    }
                    else {
                        GroupColComboBox.SelectedItem = ExeFiles.Count.ToString();
                    }

                    if (!regularIcon) {
                        IconGridComboBox.Visibility = Visibility.Visible;
                        if (CustomDialog.XamlRoot != null) {
                            CustomDialog.Hide();
                        }
                    }
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Drop Error: {ex.Message}");
            }
        }


        private void GroupColComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (GroupColComboBox.SelectedItem != null && GroupColComboBox.SelectedItem.ToString() == "1") {
                GroupHeader.IsEnabled = false;
                HeaderPanel.Opacity = 0.5;
            }
            else {
                GroupHeader.IsEnabled = true;
                HeaderPanel.Opacity = 1.0;

            }
        }

        private void ShowLabels_Toggled(object sender, RoutedEventArgs e) {
            if (ShowLabels.IsOn) {
                LabelSizePanel.Opacity = 1.0;
                LabelSizeComboBox.IsEnabled = true;

                LabelPositionPanel.Opacity = 1.0;
                LabelPositionComboBox.IsEnabled = true;
            }
            else {
                LabelSizePanel.Opacity = 0.5;
                LabelSizeComboBox.IsEnabled = false;
                LabelPositionPanel.Opacity = 0.5;
                LabelPositionComboBox.IsEnabled = false;
            }
        }

        private void InitializeLabelSizeComboBox() {
            LabelSizeComboBox.Items.Clear();
            int[] sizes = { 8, 9, 10, 11, 12, 14 };
            foreach (int size in sizes) {
                LabelSizeComboBox.Items.Add(size.ToString());
            }
            LabelSizeComboBox.SelectedItem = int.Parse(DEFAULT_LABEL_SIZE.ToString()); // Default
        }

        private void InitializeLabelPositionComboBox() {
            LabelPositionComboBox.Items.Clear();

            LabelPositionComboBox.Items.Add("Right");
            LabelPositionComboBox.Items.Add("Bottom");
            LabelPositionComboBox.SelectedItem = DEFAULT_LABEL_POSITION; 
        }

        private async Task LoadGroupDataAsync(int groupId) {
            await Task.Run(async () =>
            {
                string jsonFilePath = JsonConfigHelper.GetDefaultConfigPath();
                if (File.Exists(jsonFilePath)) {
                    string jsonContent = await File.ReadAllTextAsync(jsonFilePath);
                    JsonNode jsonObject = JsonNode.Parse(jsonContent) ?? new JsonObject();

                    if (jsonObject.AsObject().TryGetPropertyValue(groupId.ToString(), out JsonNode groupNode)) {
                        groupName = groupNode["groupName"]?.GetValue<string>();
                        int groupCol = groupNode["groupCol"]?.GetValue<int>() ?? 0;
                        string groupIcon = IconHelper.FindOrigIcon(groupNode["groupIcon"]?.GetValue<string>());
                        bool groupHeader = groupNode["groupHeader"]?.GetValue<bool>() ?? false;
                        bool showLabels = groupNode["showLabels"]?.GetValue<bool>() ?? false;
                        int labelSize = groupNode["labelSize"]?.GetValue<int>() ?? int.Parse(DEFAULT_LABEL_SIZE.ToString());
                        string labelPosition = groupNode["labelPosition"]?.GetValue<string>() ?? DEFAULT_LABEL_POSITION;
                        JsonObject paths = groupNode["path"]?.AsObject();

                        string tempSubfolderPath = Path.Combine(Path.GetTempPath(), "AppGroup");
                        if (!Directory.Exists(tempSubfolderPath)) {
                            Directory.CreateDirectory(tempSubfolderPath);
                        }
                        string uniqueFolderName = new DirectoryInfo(Path.GetDirectoryName(groupIcon)).Name;
                        string uniqueFolderPath = Path.Combine(tempSubfolderPath, uniqueFolderName);
                        if (!Directory.Exists(uniqueFolderPath)) {
                            Directory.CreateDirectory(uniqueFolderPath);
                        }
                        string tempIcon = Path.Combine(uniqueFolderPath, Path.GetFileName(groupIcon));

                        await Task.Run(() => File.Copy(groupIcon, tempIcon, overwrite: true));

                        Console.WriteLine("Temporary file path: " + tempIcon);

                        DispatcherQueue.TryEnqueue(() =>
                        {
                            GroupHeader.IsOn = groupHeader;
                            if (!string.IsNullOrEmpty(groupName)) {
                                GroupNameTextBox.Text = groupName;
                            }

                            // Initialize and set label settings
                            InitializeLabelSizeComboBox();
                            InitializeLabelPositionComboBox();
                            ShowLabels.IsOn = showLabels;
                            LabelSizeComboBox.SelectedItem = labelSize.ToString();
                            LabelPositionComboBox.SelectedItem = labelPosition.ToString();
                            // Update label size panel state
                        
                            if (showLabels) {
                                LabelSizePanel.Opacity = 1.0;
                                LabelSizeComboBox.IsEnabled = true;

                                LabelPositionPanel.Opacity = 1.0;
                                LabelPositionComboBox.IsEnabled = true;
                            }
                            else {
                                LabelSizePanel.Opacity = 0.5;
                                LabelSizeComboBox.IsEnabled = false;
                                LabelPositionPanel.Opacity = 0.5;
                                LabelPositionComboBox.IsEnabled = false;
                            }
                        });

                        if (groupCol > 0) {
                            if (paths != null) {
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    for (int i = 1; i <= paths.Count; i++) {
                                        GroupColComboBox.Items.Add(i.ToString());
                                    }
                                    GroupColComboBox.SelectedItem = groupCol.ToString();
                                });
                            }
                        }

                        if (!string.IsNullOrEmpty(groupIcon)) {
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                selectedIconPath = tempIcon;
                                BitmapImage bitmapImage = new BitmapImage(new Uri(tempIcon));
                                IconPreviewImage.Source = bitmapImage;
                                IconPreviewBorder.Visibility = Visibility.Visible;
                                ApplicationCount.Text = paths.Count > 1
                                    ? paths.Count.ToString() + " Items"
                                    : paths.Count == 1
                                    ? "1 Item"
                                    : "";
                            });
                        }

                        if (paths != null) {
                            foreach (var path in paths) {
                                string filePath = path.Key;
                                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath)) {
                                    Debug.WriteLine($"Icon : {filePath}");
                                    var icon = await IconCache.GetIconPathAsync(filePath);
                                    await Task.Delay(10);

                                    if (path.Value.AsObject().TryGetPropertyValue("icon", out JsonNode? iconNode)
                                          && iconNode is not null
                                          && !string.IsNullOrEmpty(iconNode.GetValue<string>())) {
                                        icon = iconNode.GetValue<string>();
                                    }

                                    DispatcherQueue.TryEnqueue(() =>
                                    {
                                        ExeFiles.Add(new ExeFileModel {
                                            FileName = Path.GetFileName(filePath),
                                            Icon = icon,
                                            FilePath = filePath,
                                            Tooltip = path.Value["tooltip"]?.GetValue<string>(),
                                            Args = path.Value["args"]?.GetValue<string>(),
                                            IconPath = icon
                                        });
                                    });
                                }
                            }

                            DispatcherQueue.TryEnqueue(() =>
                            {
                                IconGridComboBox.Items.Clear();
                                if (ExeFiles.Count >= 9) {
                                    IconGridComboBox.Items.Add("2");
                                    IconGridComboBox.Items.Add("3");
                                    IconGridComboBox.SelectedItem = "3";
                                }
                                else {
                                    IconGridComboBox.Items.Add("2");
                                    IconGridComboBox.SelectedItem = "2";
                                }
                                if (groupIcon.Contains("grid")) {
                                    IconGridComboBox.SelectedItem = groupIcon.Contains("grid3") ? "3" : "2";
                                    regularIcon = false;
                                    IconGridComboBox.Visibility = Visibility.Visible;
                                }
                            });
                        }
                    }
                    else {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            groupName = "";
                            GroupHeader.IsOn = false;
                            GroupNameTextBox.Text = string.Empty;
                            GroupColComboBox.Items.Clear();
                            selectedIconPath = string.Empty;
                            IconPreviewImage.Source = new BitmapImage(new Uri("ms-appx:///default_preview.png"));

                            ApplicationCount.Text = string.Empty;
                            ExeFiles.Clear();
                            IconGridComboBox.Items.Clear();
                            IconGridComboBox.Visibility = Visibility.Collapsed;

                            // Initialize label settings for new groups
                            InitializeLabelSizeComboBox();
                            InitializeLabelPositionComboBox();

                            ShowLabels.IsOn = false;
                            LabelSizePanel.Opacity = 0.5;
                            LabelSizeComboBox.IsEnabled = false;
                            LabelPositionPanel.Opacity = 0.5;
                            LabelPositionComboBox.IsEnabled = false;

                        });
                    }
                }
                else {
                    // Config file doesn't exist (fresh install) - initialize with defaults
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        groupName = "";
                        GroupHeader.IsOn = false;
                        GroupNameTextBox.Text = string.Empty;
                        GroupColComboBox.Items.Clear();
                        selectedIconPath = string.Empty;
                        IconPreviewImage.Source = new BitmapImage(new Uri("ms-appx:///default_preview.png"));

                        ApplicationCount.Text = string.Empty;
                        ExeFiles.Clear();
                        IconGridComboBox.Items.Clear();
                        IconGridComboBox.Visibility = Visibility.Collapsed;

                        // Initialize label settings for new groups on fresh install
                        InitializeLabelSizeComboBox();
                        InitializeLabelPositionComboBox();

                        ShowLabels.IsOn = false;
                        LabelSizePanel.Opacity = 0.5;
                        LabelSizeComboBox.IsEnabled = false;
                    });
                }

                await Task.Run(() => Task.Delay(10));

                DispatcherQueue.TryEnqueue(() =>
                {
                    // Final UI state setup
                    if (CustomDialog != null && CustomDialog.XamlRoot == null) {
                        CustomDialog.XamlRoot = this.Content.XamlRoot;
                    }
                });
            });
        }
        private async void CreateGridIcon() {
            var selectedItem = IconGridComboBox.SelectedItem;
            int selectedGridSize = 2;
            if (selectedItem != null && int.TryParse(selectedItem.ToString(), out int selectedSize)) {
                // Use all items up to the grid size, not just selected items
                var selectedItems = ExeFiles.Take(selectedSize * selectedSize).ToList();

                try {
                    IconHelper iconHelper = new IconHelper();
                    selectedIconPath = await iconHelper.CreateGridIconAsync(
                        selectedItems,
                        selectedSize,
                        IconPreviewImage,
                        IconPreviewBorder
                    );
                }
                catch (Exception ex) {
                    ShowErrorDialog("Error creating grid icon", ex.Message);
                    Debug.WriteLine($"Grid icon creation error: {ex.Message}");
                }
            }
            else {
                ShowErrorDialog("Invalid grid size", "Please select a valid grid size from the ComboBox.");
            }
        }

        private void IconGridComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (IconGridComboBox.SelectedItem != null && !regularIcon) {
                CreateGridIcon();
            }
        }

        private async void BrowseIconButton_Click(object sender, RoutedEventArgs e) {
            ContentDialogResult result = await CustomDialog.ShowAsync();
            
        }

        private void CloseDialog(object sender, RoutedEventArgs e) {
            CustomDialog.Hide();
        }
        private void CloseEditDialog(object sender, RoutedEventArgs e) {
            EditItemDialog.Hide();
        }

        private void CloseCustomizeDialog(object sender, RoutedEventArgs e) {
            CustomizeDialog.Hide();
        }
        private void GridClick(object sender, RoutedEventArgs e) {
            if (ExeListView.Items.Count == 0) {
                regularIcon = false;
                BrowseFiles();
            }
            else {
                regularIcon = false;
                CreateGridIcon();
                IconGridComboBox.Visibility = Visibility.Visible;
                CustomDialog.Hide();
            }
        }

        private void RegularClick(object sender, RoutedEventArgs e) {
            regularIcon = true;
            BrowseIcon();
        }

        private async void BrowseIcon() {
            try {
                FileOpenPicker openPicker = new FileOpenPicker();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                InitializeWithWindow.Initialize(openPicker, hwnd);
                openPicker.FileTypeFilter.Add(".jpeg");
                openPicker.FileTypeFilter.Add(".jpg");
                openPicker.FileTypeFilter.Add(".exe");
                openPicker.FileTypeFilter.Add(".url");
                openPicker.FileTypeFilter.Add(".png");
                openPicker.FileTypeFilter.Add(".ico");
                StorageFile file = await openPicker.PickSingleFileAsync();

                if (file != null) {
                    selectedIconPath = file.Path;
                    BitmapImage bitmapImage = new BitmapImage();
                    if (file.FileType == ".exe") {
                        var iconPath = await IconCache.GetIconPathAsync(file.Path);
                        if (!string.IsNullOrEmpty(iconPath)) {
                            using (var stream = File.OpenRead(iconPath)) {
                                await bitmapImage.SetSourceAsync(stream.AsRandomAccessStream());
                            }
                        }
                    }
                    else {
                        using (var stream = await file.OpenReadAsync()) {
                            await bitmapImage.SetSourceAsync(stream);
                        }
                    }

                    IconPreviewImage.Source = bitmapImage;
                    IconPreviewBorder.Visibility = Visibility.Visible;

                    if (CustomDialog.XamlRoot != null) {
                        CustomDialog.Hide();
                        IconGridComboBox.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex) {
                ShowErrorDialog("Error selecting icon", ex.Message);
            }
        }

        private async void BrowseFilePathButton_Click(object sender, RoutedEventArgs e) {
            BrowseFiles();
        }

        private async void BrowseFiles() {
            var openPicker = new FileOpenPicker();
            openPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            openPicker.FileTypeFilter.Add(".exe");
            openPicker.FileTypeFilter.Add(".url");
            openPicker.FileTypeFilter.Add(".lnk");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(openPicker, hwnd);

            var files = await openPicker.PickMultipleFilesAsync();
            if (files == null || files.Count == 0) return;
            string icon;
            foreach (var file in files) {
                if (file.FileType.Equals(".url", StringComparison.OrdinalIgnoreCase)) {
                    icon = await IconHelper.GetUrlFileIconAsync(file.Path);
                }else{ icon = await IconCache.GetIconPathAsync(file.Path);
                    }
                ExeFiles.Add(new ExeFileModel { FileName = file.Name, Icon = icon, FilePath = file.Path, Tooltip = "", Args = "" });
            }

            ExeListView.ItemsSource = ExeFiles;
            lastSelectedItem = GroupColComboBox.SelectedItem as string;
            ApplicationCount.Text = ExeListView.Items.Count > 1
                          ? ExeListView.Items.Count.ToString() + " Items"
                          : ExeListView.Items.Count == 1
                          ? "1 Item"
                          : "";


            IconGridComboBox.Items.Clear();
            //if (ExeFiles.Count >= 9) {
            //    IconGridComboBox.Items.Add("2");
            //    IconGridComboBox.Items.Add("3");

            //    IconGridComboBox.SelectedItem = "3";
            //}
            //else {
                IconGridComboBox.Items.Add("2");
                IconGridComboBox.SelectedItem = "2";
            //}

            GroupColComboBox.Items.Clear();
            for (int i = 1; i <= ExeFiles.Count; i++) {
                GroupColComboBox.Items.Add(i.ToString());
            }

            if (ExeFiles.Count > 3) {
                if (lastSelectedItem != null) {
                    GroupColComboBox.SelectedItem = lastSelectedItem;

                }
                else {
                    GroupColComboBox.SelectedItem = "3";

                }
            }
            else {
                GroupColComboBox.SelectedItem = ExeFiles.Count.ToString();
            }

            if (!regularIcon) {
                IconGridComboBox.Visibility = Visibility.Visible;
                if (CustomDialog.XamlRoot != null) {
                    CustomDialog.Hide();
                }
            }
        }
    
        private void ExeListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {


        }

        private void ExeListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) {
            if (args.DropResult == DataPackageOperation.Move && IconGridComboBox.SelectedItem != null && !regularIcon) {
                CreateGridIcon();
            }
        }


        private async void CustomizeDialog_Click(object sender, RoutedEventArgs e) {
            ContentDialogResult result = await CustomizeDialog.ShowAsync();

        }
        private async void EditItem_Click(object sender, RoutedEventArgs e) {
            if (sender is Button button && button.Tag is ExeFileModel item) {
                CurrentItem = item;

                EditTitle.Text = item.FileName;
                TooltipTextBox.Text = item.Tooltip;
                ArgsTextBox.Text = item.Args;

                // Set the current group path for icon saving
                string exeDirectory = Path.GetDirectoryName(Environment.ProcessPath);
                //string groupsFolder = Path.Combine(exeDirectory, "Groups");

                string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appDataPath = Path.Combine(localAppDataPath, "AppGroup");
                string groupsFolder = Path.Combine(appDataPath, "Groups");
                Directory.CreateDirectory(groupsFolder);

                string groupName = GroupNameTextBox.Text?.Trim();
                string groupFolder = Path.Combine(groupsFolder, groupName);
                string uniqueFolderName = groupName;
                currentGroupPath = Path.Combine(groupFolder, uniqueFolderName);

                // Store the original icon path (extracted from exe)
                originalItemIconPath = await IconCache.GetIconPathAsync(item.FilePath);
                // Load existing custom icon if available, otherwise show original
                if (!string.IsNullOrEmpty(item.IconPath) && item.IconPath != originalItemIconPath) {
                    selectedItemIconPath = item.IconPath;
                    ItemIconPreview.Source = new BitmapImage(new Uri(item.IconPath));
                }
                else {
                    selectedItemIconPath = originalItemIconPath;
                    if (!string.IsNullOrEmpty(originalItemIconPath)) {
                        ItemIconPreview.Source = new BitmapImage(new Uri(originalItemIconPath));
                    }
                }

                ContentDialogResult result = await EditItemDialog.ShowAsync();
            }
        }
        private void EditItemSave_Click(object sender, RoutedEventArgs e) {
            if (CurrentItem != null) {
                // Update the model properties
                CurrentItem.Tooltip = TooltipTextBox.Text;
                CurrentItem.Args = ArgsTextBox.Text;

                // Save icon path if provided and different from original
                if (!string.IsNullOrEmpty(selectedItemIconPath)) {
                    if (selectedItemIconPath == originalItemIconPath) {
                        // Reset to original - clear custom icon path
                        CurrentItem.IconPath = null;
                        CurrentItem.Icon = originalItemIconPath;
                    }
                    else {
                        // Custom icon selected
                        CurrentItem.IconPath = selectedItemIconPath;
                        CurrentItem.Icon = selectedItemIconPath;
                    }
                }

                // Force UI refresh by notifying the ListView that the item has changed
                int index = ExeFiles.IndexOf(CurrentItem);
                if (index >= 0) {
                    ExeFiles.RemoveAt(index);
                    ExeFiles.Insert(index, CurrentItem);
                }

                // If using grid icon and not regular icon, regenerate the grid icon
                if (!regularIcon && IconGridComboBox.SelectedItem != null) {
                    CreateGridIcon();
                }

                EditItemDialog.Hide();
            }
        }
        private async void ResetItemIcon_Click(object sender, RoutedEventArgs e) {
            try {
                if (!string.IsNullOrEmpty(originalItemIconPath)) {
                    selectedItemIconPath = originalItemIconPath;
                    ItemIconPreview.Source = new BitmapImage(new Uri(originalItemIconPath));
                }
                else if (CurrentItem != null) {
                    // Fallback: re-extract the icon from the executable
                    string originalIcon = await IconCache.GetIconPathAsync(CurrentItem.FilePath);
                    if (!string.IsNullOrEmpty(originalIcon)) {
                        selectedItemIconPath = originalIcon;
                        originalItemIconPath = originalIcon;
                        ItemIconPreview.Source = new BitmapImage(new Uri(originalIcon));
                    }
                }
            }
            catch (Exception ex) {
                var dialog = new ContentDialog() {
                    Title = "Error",
                    Content = $"Failed to reset icon: {ex.Message}",
                    CloseButtonText = "OK"
                };
                dialog.XamlRoot = this.Content.XamlRoot;
                await dialog.ShowAsync();
            }
        }
        private void RemoveItem_Click(object sender, RoutedEventArgs e) {
            if (sender is Button button && button.Tag is ExeFileModel item) {
                ExeFiles.Remove(item);
            }

            ExeListView.ItemsSource = ExeFiles;
            IconGridComboBox.Items.Clear();
            ApplicationCount.Text = ExeListView.Items.Count > 0
      ? ExeListView.Items.Count.ToString() + " Items"
      : "Item";

            //if (ExeFiles.Count >= 9) {
            //    IconGridComboBox.Items.Add("2");
            //    IconGridComboBox.Items.Add("3");

            //    IconGridComboBox.SelectedItem = "3";
            //}
            //else {
                IconGridComboBox.Items.Add("2");
                IconGridComboBox.SelectedItem = "2";
            //}

            lastSelectedItem = GroupColComboBox.SelectedItem as string;
            GroupColComboBox.Items.Clear();

            for (int i = 1; i <= ExeFiles.Count; i++) {
                GroupColComboBox.Items.Add(i.ToString());
            }

            if (lastSelectedItem != null && int.TryParse(lastSelectedItem, out int lastSelectedIndex)) {

                GroupColComboBox.SelectedItem = lastSelectedItem;
                if (lastSelectedIndex > ExeFiles.Count) {
                    GroupColComboBox.SelectedItem = ExeFiles.Count.ToString();
                }
            }



        }
        private void GroupNameTextBox_GotFocus(object sender, RoutedEventArgs e) {
            // Show the InfoBar when the text box is clicked

        }

        private void GroupNameTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (sender is TextBox textBox) {
                string newGroupName = textBox.Text;
                string oldGroupName = GetOldGroupName();
                Debug.WriteLine($"old: {oldGroupName}");
                Debug.WriteLine($"new: {newGroupName}");
                if (!string.IsNullOrEmpty(GroupNameTextBox.Text) &&
                    !string.IsNullOrEmpty(oldGroupName) &&
                    oldGroupName != newGroupName) {
                    RenameInfoBar.IsOpen = true;
                }
                else {
                    RenameInfoBar.IsOpen = false;
                }
            }
        }
        private async void CreateShortcut_Click(object sender, RoutedEventArgs e) {
            var button = sender as Button;
            if (button != null && !button.IsEnabled)
                return;

            if (button != null)
                button.IsEnabled = false;

            try {
                string newGroupName = GroupNameTextBox.Text?.Trim();
                if (string.IsNullOrEmpty(newGroupName)) {
                    await ShowDialog("Error", "Please enter a group name.");
                    return;
                }

                if (string.IsNullOrEmpty(selectedIconPath)) {
                    await ShowDialog("Error", "Please select an icon.");
                    return;
                }
                


                string exeDirectory = Path.GetDirectoryName(Environment.ProcessPath);
                string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appDataPath = Path.Combine(localAppDataPath, "AppGroup");
                string groupsFolder = Path.Combine(appDataPath, "Groups");
                Directory.CreateDirectory(groupsFolder);



                //string groupsFolder = Path.Combine(exeDirectory, "Groups");
                //Directory.CreateDirectory(groupsFolder);

                string oldGroupName = GetOldGroupName();
                string oldGroupFolder = Path.Combine(groupsFolder, oldGroupName);

                if (!string.IsNullOrEmpty(oldGroupName) && Directory.Exists(oldGroupFolder) && oldGroupName != newGroupName) {
                    Directory.Delete(oldGroupFolder, true);
                    await ShowDialog("Important", "Renaming a group requires \"Force Taskbar Update\" or re-pinning to the taskbar.");
                }

                string groupFolder = Path.Combine(groupsFolder, newGroupName);
                Directory.CreateDirectory(groupFolder);

                string uniqueFolderName = newGroupName;
                string uniqueFolderPath = Path.Combine(groupFolder, uniqueFolderName);
                Directory.CreateDirectory(uniqueFolderPath);

                File.SetAttributes(uniqueFolderPath, File.GetAttributes(uniqueFolderPath) | System.IO.FileAttributes.Hidden);
                string shortcutPath = Path.Combine(groupFolder, $"{newGroupName}.lnk");
                string targetPath = Process.GetCurrentProcess().MainModule?.FileName ?? Path.Combine(exeDirectory, "AppGroup.exe");

                string iconBaseName = $"{newGroupName}_{(regularIcon ? "regular" : (IconGridComboBox.SelectedItem?.ToString() == "3" ? "grid3" : "grid"))}";
                string icoFilePath = Path.Combine(uniqueFolderPath, $"{iconBaseName}.ico");
                string copiedImagePath; // Define the variable

                string originalImageExtension = Path.GetExtension(selectedIconPath);

                if (originalImageExtension.Equals(".ico", StringComparison.OrdinalIgnoreCase)) {
                    // If it's already an ICO, just copy it directly
                    File.Copy(selectedIconPath, icoFilePath, true);
                }
                else if (originalImageExtension.Equals(".exe", StringComparison.OrdinalIgnoreCase)) {
                    // For EXE files, use GetIconPathAsync to extract the icon (which returns a PNG)
                    string extractedPngPath = await IconCache.GetIconPathAsync(selectedIconPath);
                    if (!string.IsNullOrEmpty(extractedPngPath)) {
                        // Save the extracted PNG to the destination folder
                        string pngFilePath = Path.Combine(uniqueFolderPath, $"{iconBaseName}.png");
                        File.Copy(extractedPngPath, pngFilePath, true);

                        // Convert the PNG to ICO
                        bool iconSuccess = await IconHelper.ConvertToIco(pngFilePath, icoFilePath);
                        if (!iconSuccess) {
                            await ShowDialog("Error", "Failed to convert extracted PNG to ICO format.");
                            return;
                        }
                    }
                    else {
                        await ShowDialog("Error", "Failed to extract icon from EXE file.");
                        return;
                    }
                }
                else {
                    // For all other image types (PNG, JPG, etc.), convert to ICO
                    bool iconSuccess = await IconHelper.ConvertToIco(selectedIconPath, icoFilePath);
                    if (!iconSuccess) {
                        await ShowDialog("Error", "Failed to convert image to ICO format.");
                        return;
                    }
                }

                // Copy the original image for reference/future use (except for EXE files, which we've already handled)
                if (!originalImageExtension.Equals(".exe", StringComparison.OrdinalIgnoreCase)) {
                    copiedImagePath = Path.Combine(uniqueFolderPath, $"{iconBaseName}{originalImageExtension}");
                    File.Copy(selectedIconPath, copiedImagePath, true);
                }

                IWshShell wshShell = new WshShell();
                IWshShortcut shortcut = (IWshShortcut)wshShell.CreateShortcut(shortcutPath);

                shortcut.TargetPath = targetPath;
                shortcut.Arguments = $"\"{newGroupName}\"";
                shortcut.Description = $"{newGroupName} - AppGroup Shortcut";
                shortcut.IconLocation = icoFilePath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Save();



             
           bool isPinned = await TaskbarManager.IsShortcutPinnedToTaskbar(oldGroupName ?? newGroupName);

if (isPinned) {
    await TaskbarManager.UpdateTaskbarShortcutIcon(oldGroupName ?? newGroupName, newGroupName, icoFilePath);
   
    TaskbarManager.TryRefreshTaskbarWithoutRestartAsync();
}

                bool groupHeader = GroupHeader.IsEnabled ? GroupHeader.IsOn : false;
                if (GroupColComboBox.SelectedItem != null && int.TryParse(GroupColComboBox.SelectedItem.ToString(), out int groupCol) && groupCol > 0) {
                    // When saving to JSON
                    Dictionary<string, (string tooltip, string args, string icon)> paths = ExeFiles.ToDictionary(
         file => file.FilePath,
         file => (file.Tooltip, file.Args, file.IconPath)
     );

                    // Get label settings from UI
                    bool showLabels = ShowLabels.IsOn;
                    int labelSize = LabelSizeComboBox.SelectedItem != null ? int.Parse(LabelSizeComboBox.SelectedItem.ToString()) : int.Parse(DEFAULT_LABEL_SIZE.ToString());
                    string? labelPosition = LabelPositionComboBox.SelectedItem != null ? LabelPositionComboBox.SelectedItem.ToString() : DEFAULT_LABEL_POSITION;

                    // Update your AddGroupToJson method signature and implementation to handle icon
                    JsonConfigHelper.AddGroupToJson(JsonConfigHelper.GetDefaultConfigPath(), GroupId, newGroupName, groupHeader, icoFilePath, groupCol, showLabels, labelSize,labelPosition, paths);
                    ExpanderLabel.IsExpanded = false;


                    if (tempIcon != null) {
                        try {
                            File.Delete(tempIcon);
                            Console.WriteLine("TempIcon deleted successfully.");
                        }
                        catch (Exception ex) {
                            await ShowDialog("Error", $"An error occurred: {ex.Message}");
                        }
                    }

                    string[] oldFolders = Directory.GetDirectories(groupFolder);
                    foreach (string oldFolder in oldFolders) {
                        if (oldFolder != uniqueFolderPath) {
                            Directory.Delete(oldFolder, true);
                        }
                    }
                    IntPtr hWnd = NativeMethods.FindWindow(null, "App Group");
                    if (hWnd != IntPtr.Zero) {

                        NativeMethods.SetForegroundWindow(hWnd);
                    }
                    GroupId = -1;

                    this.Hide();
                }
                else {
                    await ShowDialog("Error", "Please select a valid group column value.");
                }
            }
            catch (Exception ex) {
                await ShowDialog("Error", $"An error occurred: {ex.Message}");
            }
            finally {
                if (button != null)
                    button.IsEnabled = true;
            }
        }

       
           private string GetOldGroupName() {
            return groupName ?? "";
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
        // Add these fields to your class
        private string selectedItemIconPath = null;
        private string currentGroupPath = null; // Store the group folder path

        // Add this method for icon browsing
        private async void BrowseItemIcon_Click(object sender, RoutedEventArgs e) {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".ico");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".exe");

            // Initialize the picker with the window handle
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null) {
                await ProcessSelectedIcon(file);
            }
        }

        private async Task ProcessSelectedIcon(StorageFile file) {
            try {
                // Ensure the group directory exists
                if (!string.IsNullOrEmpty(currentGroupPath) && !Directory.Exists(currentGroupPath)) {
                    Directory.CreateDirectory(currentGroupPath);
                }

                selectedItemIconPath = file.Path;
                BitmapImage bitmapImage = new BitmapImage();

                if (file.FileType == ".exe") {
                    var iconPath = await IconCache.GetIconPathAsync(file.Path);
                    if (!string.IsNullOrEmpty(iconPath)) {
                        using (var stream = File.OpenRead(iconPath)) {
                            await bitmapImage.SetSourceAsync(stream.AsRandomAccessStream());
                        }
                        selectedItemIconPath = iconPath;
                    }
                }
                else {
                    using (var stream = await file.OpenReadAsync()) {
                        await bitmapImage.SetSourceAsync(stream);
                    }
                }

                //IconPathTextBox.Text = Path.GetFileName(selectedItemIconPath);
                ItemIconPreview.Source = bitmapImage;
            }
            catch (Exception ex) {
                var dialog = new ContentDialog() {
                    Title = "Error",
                    Content = $"Failed to process icon: {ex.Message}",
                    CloseButtonText = "OK"
                };
                dialog.XamlRoot = this.Content.XamlRoot;
                await dialog.ShowAsync();
            }
        }
        private async Task<bool> ConfirmOverwrite(string path) {
            ContentDialog dialog = new ContentDialog {
                Title = "Overwrite",
                Content = $"A shortcut with this name already exists. Do you want to replace it?",
                PrimaryButtonText = "Yes",
                CloseButtonText = "No",
                XamlRoot = Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }
        private async Task ShowDialog(string title, string message) {
            ContentDialog dialog = new ContentDialog {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };

            await dialog.ShowAsync();
        }

        private async void ShowErrorDialog(string title, string message) {
            await ShowDialog(title, message);
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW {
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        }

        [ClassInterface(ClassInterfaceType.None)]
        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        private class CShellLink { }
    }
}
