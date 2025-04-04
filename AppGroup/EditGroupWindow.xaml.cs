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

namespace AppGroup {
    public class ExeFileModel {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string Icon { get; set; }
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
     
        public EditGroupWindow(int groupId) {
   
            InitializeComponent();

            GroupId = groupId;

            this.CenterOnScreen();

            var iconPath = Path.Combine(AppContext.BaseDirectory, "EditGroup.ico");
            this.AppWindow.SetIcon(iconPath);
            string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appDataPath = Path.Combine(localAppDataPath, "AppGroup");

            if (!Directory.Exists(appDataPath)) {
                Directory.CreateDirectory(appDataPath);
            }

            groupIdFilePath = Path.Combine(appDataPath, "gid");

            if (!File.Exists(groupIdFilePath)) {
                File.WriteAllText(groupIdFilePath, groupId.ToString());
            }
            else {
                string existingGroupIdText = File.ReadAllText(groupIdFilePath);
                if (int.TryParse(existingGroupIdText, out int existingGroupId)) {
                    GroupId = existingGroupId;
                }
            }

            fileWatcher = new FileSystemWatcher(Path.GetDirectoryName(groupIdFilePath));
            fileWatcher.Filter = Path.GetFileName(groupIdFilePath);
            fileWatcher.Changed += OnGroupIdFileChanged;
            fileWatcher.EnableRaisingEvents = true;

          
            ExeListView.ItemsSource = ExeFiles;

            MinHeight = 600;
            MinWidth = 530;
            ExtendsContentIntoTitleBar = true;
           
           
            ThemeHelper.UpdateTitleBarColors(this);
            _ = LoadGroupDataAsync(GroupId);
            Closed += MainWindow_Closed;
            ApplicationCount.Text = "Item";
            SetCurrentProcessExplicitAppUserModelID("AppGroup.EditGroup");
        }
        [DllImport("shell32.dll", SetLastError = true)]
        static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

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
                             file.FileType.Equals(".lnk", StringComparison.OrdinalIgnoreCase))) {
                            var icon = await IconCache.GetIconPathAsync(file.Path);

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
            }
            else {
                GroupHeader.IsEnabled = true;
            }
        }

        private async Task LoadGroupDataAsync(int groupId) {
            await Task.Run(async () => {
                string jsonFilePath = JsonConfigHelper.GetDefaultConfigPath();
                if (File.Exists(jsonFilePath)) {
                    string jsonContent = await File.ReadAllTextAsync(jsonFilePath);
                    JsonNode jsonObject = JsonNode.Parse(jsonContent) ?? new JsonObject();

                    if (jsonObject.AsObject().TryGetPropertyValue(groupId.ToString(), out JsonNode groupNode)) {
                        // Existing code to handle the case when groupId is found
                        groupName = groupNode["groupName"]?.GetValue<string>();
                        int groupCol = groupNode["groupCol"]?.GetValue<int>() ?? 0;
                        string groupIcon = IconHelper.FindOrigIcon(groupNode["groupIcon"]?.GetValue<string>());
                        bool groupHeader = groupNode["groupHeader"]?.GetValue<bool>() ?? false;

                        JsonArray paths = groupNode["path"]?.AsArray();

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

                        DispatcherQueue.TryEnqueue(() => {
                            GroupHeader.IsOn = groupHeader;
                            if (!string.IsNullOrEmpty(groupName)) {
                                GroupNameTextBox.Text = groupName;
                            }
                        });

                        if (groupCol > 0) {
                            if (paths != null) {
                                DispatcherQueue.TryEnqueue(() => {
                                    for (int i = 1; i <= paths.Count; i++) {
                                        GroupColComboBox.Items.Add(i.ToString());
                                    }
                                    GroupColComboBox.SelectedItem = groupCol.ToString();
                                });
                            }
                        }

                        if (!string.IsNullOrEmpty(groupIcon)) {
                            DispatcherQueue.TryEnqueue(() => {
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
                                string filePath = path?.GetValue<string>();
                                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath)) {
                                    Debug.WriteLine($"Icon : {filePath}");
                                    var icon = await IconCache.GetIconPathAsync(filePath);
                                    await Task.Delay(5);

                                    DispatcherQueue.TryEnqueue(() => {
                                        ExeFiles.Add(new ExeFileModel {
                                            FileName = Path.GetFileName(filePath),
                                            Icon = icon,
                                            FilePath = filePath
                                        });
                                    });
                                }
                            }

                            DispatcherQueue.TryEnqueue(() => {
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
                                    CreateGridIcon();
                                    IconGridComboBox.Visibility = Visibility.Visible;
                                }
                            });
                        }
                    }
                    else {
                        DispatcherQueue.TryEnqueue(() => {
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
                        });
                    }
                }
            });
        }

        private async void CreateGridIcon() {
            var selectedItem = IconGridComboBox.SelectedItem;
            int selectedGridSize = 2;
            if (selectedItem != null && int.TryParse(selectedItem.ToString(), out int selectedSize)) {
                var selectedItems = ExeListView.SelectedItems.Cast<ExeFileModel>().ToList();
                selectedItems = ExeFiles.Take(selectedSize * selectedSize).ToList();

                try {
                    IconHelper iconHelper = new IconHelper();
                    selectedIconPath = await iconHelper.CreateGridIconAsync(selectedItems, selectedSize, IconPreviewImage, IconPreviewBorder);
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
                openPicker.FileTypeFilter.Add(".png");
                openPicker.FileTypeFilter.Add(".ico");
                StorageFile file = await openPicker.PickSingleFileAsync();

                if (file != null) {
                    selectedIconPath = file.Path;
                    BitmapImage bitmapImage = new BitmapImage();
                    using (var stream = await file.OpenReadAsync()) {
                        await bitmapImage.SetSourceAsync(stream);
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
            openPicker.FileTypeFilter.Add(".lnk");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(openPicker, hwnd);

            var files = await openPicker.PickMultipleFilesAsync();
            if (files == null || files.Count == 0) return;

            foreach (var file in files) {
                var icon = await IconCache.GetIconPathAsync(file.Path);
                ExeFiles.Add(new ExeFileModel { FileName = file.Name, Icon = icon, FilePath = file.Path });
            }

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

        private void ExeListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {


        }

        private void ExeListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) {
            if (args.DropResult == DataPackageOperation.Move && IconGridComboBox.SelectedItem != null && !regularIcon) {
                CreateGridIcon();
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

            if (ExeFiles.Count >= 9) {
                IconGridComboBox.Items.Add("2");
                IconGridComboBox.Items.Add("3");

                IconGridComboBox.SelectedItem = "3";
            }
            else {
                IconGridComboBox.Items.Add("2");
                IconGridComboBox.SelectedItem = "2";
            }

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
                string groupsFolder = Path.Combine(exeDirectory, "Groups");
                Directory.CreateDirectory(groupsFolder);

                string oldGroupName = GetOldGroupName(); // Implement this method to get the old group name
                string oldGroupFolder = Path.Combine(groupsFolder, oldGroupName);

                if (!string.IsNullOrEmpty(oldGroupName) && Directory.Exists(oldGroupFolder) && oldGroupName != newGroupName) {
                    Directory.Delete(oldGroupFolder, true);
                    await ShowDialog("Important", "Renaming a group requires re-pinning to the taskbar.");
                }


                string groupFolder = Path.Combine(groupsFolder, newGroupName);
                Directory.CreateDirectory(groupFolder);

                string uniqueFolderName = Path.GetRandomFileName();
                string uniqueFolderPath = Path.Combine(groupFolder, uniqueFolderName);
                Directory.CreateDirectory(uniqueFolderPath);

                // Set the folder attributes to hidden
                File.SetAttributes(uniqueFolderPath, File.GetAttributes(uniqueFolderPath) | System.IO.FileAttributes.Hidden);
                string shortcutPath = Path.Combine(groupFolder, $"{newGroupName}.lnk");
                string targetPath = Process.GetCurrentProcess().MainModule?.FileName ?? Path.Combine(exeDirectory, "AppGroup.exe");

                string iconBaseName = $"{newGroupName}_{(regularIcon ? "regular" : (IconGridComboBox.SelectedItem?.ToString() == "3" ? "grid3" : "grid"))}";
                string icoFilePath = Path.Combine(uniqueFolderPath, $"{iconBaseName}.ico");

                bool iconSuccess = true;

                if (Path.GetExtension(selectedIconPath).ToLower() == ".ico") {
                    File.Copy(selectedIconPath, icoFilePath, true);
                }
                else {
                    iconSuccess = IconHelper.ConvertToIco(selectedIconPath, icoFilePath);
                }

                if (!iconSuccess) {
                    await ShowDialog("Error", "Failed to convert image to ICO format.");
                    return;
                }

                string originalImageExtension = Path.GetExtension(selectedIconPath);
                copiedImagePath = Path.Combine(uniqueFolderPath, $"{iconBaseName}{originalImageExtension}");
                File.Copy(selectedIconPath, copiedImagePath, true);

                //if (!File.Exists(shortcutPath) || await ConfirmOverwrite(shortcutPath) */) {
                    IWshShell wshShell = new WshShell();
                    IWshShortcut shortcut = (IWshShortcut)wshShell.CreateShortcut(shortcutPath);

                    shortcut.TargetPath = targetPath;
                    shortcut.Arguments = $"\"{newGroupName}\"";
                    shortcut.Description = $"{newGroupName} - AppGroup Shortcut";
                    shortcut.IconLocation = icoFilePath;
                    shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                    shortcut.Save();





                    bool groupHeader = GroupHeader.IsEnabled ? GroupHeader.IsOn : false;
                    if (GroupColComboBox.SelectedItem != null && int.TryParse(GroupColComboBox.SelectedItem.ToString(), out int groupCol) && groupCol > 0) {
                        string[] paths = ExeFiles.Select(file => file.FilePath).ToArray();

                        JsonConfigHelper.AddGroupToJson(JsonConfigHelper.GetDefaultConfigPath(), GroupId, newGroupName, groupHeader, icoFilePath, groupCol, paths);

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

                        //var dialog = new ContentDialog {
                        //    Title = "Success",
                        //    Content = "Shortcut created successfully.",
                        //    CloseButtonText = "OK",
                        //    PrimaryButtonText = "Open File Location",
                        //    XamlRoot = this.Content.XamlRoot // Ensure this is set in a UI context
                        //};

                        //dialog.PrimaryButtonClick += (s, e) => JsonConfigHelper.OpenGroupFolder(GroupId);

                        //await dialog.ShowAsync();
                        Close();
                    }
                    else {
                        await ShowDialog("Error", "Please select a valid group column value.");
                    }
                //}
            }
            catch (Exception ex) {
                await ShowDialog("Error", $"An error occurred: {ex.Message}");
            }
        }

        // Implement this method to get the old group name
        private string GetOldGroupName() {
            return groupName ?? "";
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
