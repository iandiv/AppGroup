using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Image = Microsoft.UI.Xaml.Controls.Image;

namespace AppGroup {
    public class IconHelper {



        public static string FindOrigIcon(string icoFilePath) {
            string[] possibleExtensions = { ".png", ".jpg", ".jpeg" };

            string directory = Path.GetDirectoryName(icoFilePath);
            string filenameWithoutExtension = Path.GetFileNameWithoutExtension(icoFilePath);

            foreach (string ext in possibleExtensions) {
                string potentialPath = Path.Combine(directory, filenameWithoutExtension + ext);
                if (System.IO.File.Exists(potentialPath)) {
                    return potentialPath;
                }
            }

            return icoFilePath;
        }





        public static async Task<string> ExtractIconAndSaveAsync(string filePath, string outputDirectory, TimeSpan? timeout = null) {
            timeout ??= TimeSpan.FromSeconds(3);
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) {
                Debug.WriteLine($"File does not exist: {filePath}");
                return null;
            }
            try {
                using var cancellationTokenSource = new CancellationTokenSource(timeout.Value);
                return await Task.Run(async () => {
                    try {
                        Bitmap iconBitmap = null;
                        if (Path.GetExtension(filePath).ToLower() == ".lnk") {
                            dynamic shell = Microsoft.VisualBasic.Interaction.CreateObject("WScript.Shell");
                            dynamic shortcut = shell.CreateShortcut(filePath);
                            string iconPath = shortcut.IconLocation;
                            string targetPath = shortcut.TargetPath;
                            if (!string.IsNullOrEmpty(iconPath) && iconPath != ",") {
                                string[] iconInfo = iconPath.Split(',');
                                string actualIconPath = iconInfo[0].Trim();
                                int iconIndex = iconInfo.Length > 1 ? int.Parse(iconInfo[1].Trim()) : 0;
                                if (File.Exists(actualIconPath)) {
                                    iconBitmap = ExtractSpecificIcon(actualIconPath, iconIndex);
                                }
                            }
                            if (iconBitmap == null && !string.IsNullOrEmpty(targetPath) && File.Exists(targetPath)) {
                                iconBitmap = ExtractIconWithoutArrow(targetPath);
                            }
                        }
                        else {
                            iconBitmap = ExtractIconWithoutArrow(filePath);
                        }
                        if (iconBitmap == null) {
                            Debug.WriteLine($"No icon found for file: {filePath}");
                            return null;
                        }
                        Directory.CreateDirectory(outputDirectory);

                        string iconFileName = GenerateUniqueIconFileName(filePath, iconBitmap);
                        string iconFilePath = Path.Combine(outputDirectory, iconFileName);

                        if (File.Exists(iconFilePath)) {
                            return iconFilePath;
                        }

                        using (var stream = new FileStream(iconFilePath, FileMode.Create)) {
                            cancellationTokenSource.Token.ThrowIfCancellationRequested();
                            iconBitmap.Save(stream, ImageFormat.Png);
                        }
                        Debug.WriteLine($"Icon saved to: {iconFilePath}");
                        return iconFilePath;
                    }
                    catch (OperationCanceledException) {
                        Debug.WriteLine($"Icon extraction timed out for: {filePath}");
                        return null;
                    }
                }, cancellationTokenSource.Token);
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error extracting icon: {ex.Message}");
                return null;
            }
        }

        private static string GenerateUniqueIconFileName(string filePath, Bitmap iconBitmap) {
            using (var md5 = System.Security.Cryptography.MD5.Create()) {
                byte[] filePathBytes = System.Text.Encoding.UTF8.GetBytes(filePath);

                using (var ms = new MemoryStream()) {
                    iconBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    byte[] bitmapBytes = ms.ToArray();

                    byte[] combinedBytes = new byte[filePathBytes.Length + bitmapBytes.Length];
                    filePathBytes.CopyTo(combinedBytes, 0);
                    bitmapBytes.CopyTo(combinedBytes, filePathBytes.Length);

                    byte[] hashBytes = md5.ComputeHash(combinedBytes);

                    string hash = BitConverter.ToString(hashBytes)
                        .Replace("-", "")
                        .Substring(0, 16)
                        .ToLower();

                    return $"{Path.GetFileNameWithoutExtension(filePath)}_{hash}.png";
                }
            }
        }




        public static async Task<BitmapImage> ExtractIconFastAsync(string filePath, DispatcherQueue dispatcher) {
            if (!File.Exists(filePath)) return null;

            if (Path.GetExtension(filePath).ToLower() == ".lnk") {
                return await ExtractLnkIconWithoutArrowAsync(filePath, dispatcher);
            }

            return await Task.Run(() => {
                try {
                    using (var icon = Icon.ExtractAssociatedIcon(filePath)) {
                        if (icon == null) return null;

                        using (var stream = new MemoryStream()) {
                            icon.ToBitmap().Save(stream, ImageFormat.Png);
                            stream.Position = 0;

                            BitmapImage bitmapImage = null;
                            var resetEvent = new ManualResetEvent(false);

                            dispatcher.TryEnqueue(() => {
                                try {
                                    bitmapImage = new BitmapImage();
                                    bitmapImage.SetSource(stream.AsRandomAccessStream());
                                    resetEvent.Set();
                                }
                                catch (Exception ex) {
                                    Debug.WriteLine($"Error setting bitmap source: {ex.Message}");
                                    resetEvent.Set();
                                }
                            });

                            resetEvent.WaitOne();
                            return bitmapImage;
                        }
                    }
                }
                catch (Exception ex) {
                    Debug.WriteLine($"Error extracting icon: {ex.Message}");
                    return null;
                }
            });
        }
        public static async Task<BitmapImage> ExtractLnkIconWithoutArrowAsync(string lnkPath, DispatcherQueue dispatcher) {
            return await Task.Run(() => {
                try {
                    dynamic shell = Microsoft.VisualBasic.Interaction.CreateObject("WScript.Shell");
                    dynamic shortcut = shell.CreateShortcut(lnkPath);

                    string iconPath = shortcut.IconLocation;
                    string targetPath = shortcut.TargetPath;

                    if (!string.IsNullOrEmpty(iconPath) && iconPath != ",") {
                        // Split the icon path and index
                        string[] iconInfo = iconPath.Split(',');
                        string actualIconPath = iconInfo[0].Trim();
                        int iconIndex = iconInfo.Length > 1 ? int.Parse(iconInfo[1].Trim()) : 0;

                        if (File.Exists(actualIconPath)) {
                            using (var extractedIcon = ExtractSpecificIcon(actualIconPath, iconIndex)) {
                                if (extractedIcon != null) {
                                    return CreateBitmapImageFromBitmap(extractedIcon, dispatcher);
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath)) {
                        using (var targetIcon = ExtractIconWithoutArrow(targetPath)) {
                            if (targetIcon != null) {
                                return CreateBitmapImageFromBitmap(targetIcon, dispatcher);
                            }
                        }
                    }

                    return null;
                }
                catch (Exception ex) {
                    Debug.WriteLine($"Error extracting .lnk icon: {ex.Message}");
                    return null;
                }
            });
        }

        private static Bitmap ExtractSpecificIcon(string iconPath, int iconIndex) {
            try {
                IntPtr[] hIcons = new IntPtr[1];
                uint iconCount = NativeMethods.ExtractIconEx(iconPath, iconIndex, hIcons, null, 1);

                if (iconCount > 0 && hIcons[0] != IntPtr.Zero) {
                    using (var icon = Icon.FromHandle(hIcons[0])) {
                        var bitmap = new Bitmap(icon.ToBitmap());
                        NativeMethods.DestroyIcon(hIcons[0]);
                        return bitmap;
                    }
                }

                return null;
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error extracting specific icon: {ex.Message}");
                return null;
            }
        }

        private static Bitmap ExtractIconWithoutArrow(string targetPath) {
            try {
                IntPtr[] hIcons = new IntPtr[1];
                uint iconCount = NativeMethods.ExtractIconEx(targetPath, 0, hIcons, null, 1);

                if (iconCount > 0 && hIcons[0] != IntPtr.Zero) {
                    using (var icon = Icon.FromHandle(hIcons[0])) {
                        var bitmap = new Bitmap(icon.ToBitmap());
                        NativeMethods.DestroyIcon(hIcons[0]);
                        return bitmap;
                    }
                }

                return Icon.ExtractAssociatedIcon(targetPath)?.ToBitmap();
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error extracting icon without arrow: {ex.Message}");
                return null;
            }
        }


        private static BitmapImage CreateBitmapImageFromBitmap(Bitmap bitmap, DispatcherQueue dispatcher) {
            if (bitmap == null) return null;

            using (var stream = new MemoryStream()) {
                bitmap.Save(stream, ImageFormat.Png);
                stream.Position = 0;

                BitmapImage bitmapImage = null;
                var resetEvent = new ManualResetEvent(false);

                dispatcher.TryEnqueue(() => {
                    try {
                        bitmapImage = new BitmapImage();
                        bitmapImage.SetSource(stream.AsRandomAccessStream());
                        resetEvent.Set();
                    }
                    catch (Exception ex) {
                        Debug.WriteLine($"Error setting bitmap source: {ex.Message}");
                        resetEvent.Set();
                    }
                });

                resetEvent.WaitOne();
                return bitmapImage;
            }
        }

        public static async Task<BitmapImage> ExtractIconFromFileAsync(string filePath, DispatcherQueue dispatcher) {
            try {
                if (!System.IO.File.Exists(filePath)) {
                    Debug.WriteLine($"File not found: {filePath}");
                    return null;
                }

                return await Task.Run(() => {
                    try {
                        SHFILEINFO shfi = new SHFILEINFO();
                        uint flags = NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON;

                        IntPtr result = NativeMethods.SHGetFileInfo(filePath, 0, ref shfi, (uint)Marshal.SizeOf(shfi), flags);

                        if (result == IntPtr.Zero || shfi.hIcon == IntPtr.Zero) {
                            Debug.WriteLine($"SHGetFileInfo failed for: {filePath}");
                            return null;
                        }

                        Debug.WriteLine($"Successfully extracted icon for: {filePath}");

                        using (var icon = System.Drawing.Icon.FromHandle(shfi.hIcon))
                        using (var bitmap = icon.ToBitmap())
                        using (var stream = new MemoryStream()) {
                            bitmap.Save(stream, ImageFormat.Png);
                            stream.Position = 0;

                            BitmapImage bitmapImage = null;

                            var resetEvent = new ManualResetEvent(false);

                            dispatcher.TryEnqueue(() => {
                                try {
                                    bitmapImage = new BitmapImage();
                                    bitmapImage.SetSource(stream.AsRandomAccessStream());
                                    resetEvent.Set();
                                }
                                catch (Exception ex) {
                                    Debug.WriteLine($"Error setting bitmap source: {ex.Message}");
                                    resetEvent.Set();
                                }
                            });

                            resetEvent.WaitOne();

                            NativeMethods.DestroyIcon(shfi.hIcon);

                            return bitmapImage;
                        }
                    }
                    catch (Exception ex) {
                        Debug.WriteLine($"Icon extraction error: {ex.Message}");
                        return null;
                    }
                });
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error extracting icon: {ex.Message}");
                return null;
            }
        }


         public static bool ConvertToIco(string sourcePath, string icoFilePath) {
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(icoFilePath)) {
                Debug.WriteLine("Invalid source or destination path.");
                return false;
            }

            if (!File.Exists(sourcePath)) {
                Debug.WriteLine($"Source file not found: {sourcePath}");
                return false;
            }

            try {
                using (System.Drawing.Image originalImage = System.Drawing.Image.FromFile(sourcePath)) {
                    Size[] sizes = new Size[] { new Size(256, 256), new Size(128, 128), new Size(64, 64), new Size(32, 32), new Size(16, 16) };

                    using (FileStream fs = new FileStream(icoFilePath, FileMode.Create)) {
                        BinaryWriter bw = new BinaryWriter(fs);
                        bw.Write((short)0);
                        bw.Write((short)1);
                        bw.Write((short)sizes.Length);

                        int headerSize = 6 + (16 * sizes.Length);
                        int dataOffset = headerSize;
                        List<byte[]> imageDataList = new List<byte[]>();

                        foreach (Size size in sizes) {
                            using (Bitmap bitmap = new Bitmap(originalImage, size)) {
                                using (MemoryStream ms = new MemoryStream()) {
                                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                    byte[] imageData = ms.ToArray();
                                    imageDataList.Add(imageData);
                                }
                            }
                        }

                        for (int i = 0; i < sizes.Length; i++) {
                            Size size = sizes[i];
                            byte[] imageData = imageDataList[i];

                            bw.Write((byte)size.Width);
                            bw.Write((byte)size.Height);
                            bw.Write((byte)0);
                            bw.Write((byte)0);
                            bw.Write((short)1);
                            bw.Write((short)32);
                            bw.Write((int)imageData.Length);
                            bw.Write((int)dataOffset);

                            dataOffset += imageData.Length;
                        }

                        foreach (byte[] imageData in imageDataList) {
                            bw.Write(imageData);
                        }

                        bw.Flush();
                    }
                }
                return true;
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error converting to ICO: {ex.Message}");
                return false;
            }
        }

        public async Task<string> CreateGridIconAsync(
  List<ExeFileModel> selectedItems,
  int selectedSize,
  Image iconPreviewImage,
  Border iconPreviewBorder) {
            try {
                if (selectedItems == null || selectedSize <= 0) {
                    throw new ArgumentException("Invalid selected items or grid size.");
                }

                selectedItems = selectedItems.Take(selectedSize * selectedSize).ToList();

                int finalSize = 256;
                int gridSize;
                int cellSize;

                if (selectedItems.Count == 2) {
                    gridSize = 2;
                    cellSize = finalSize / 2;
                }
                else {
                    gridSize = (int)Math.Ceiling(Math.Sqrt(selectedItems.Count));
                    cellSize = finalSize / gridSize;
                }

                string tempFolder = Path.Combine(Path.GetTempPath(), "GridIconTemp");
                Directory.CreateDirectory(tempFolder);
                string outputPath = Path.Combine(tempFolder, "grid_icon.png");

                using (var bitmap = new System.Drawing.Bitmap(finalSize, finalSize)) {
                    using (var graphics = System.Drawing.Graphics.FromImage(bitmap)) {
                        graphics.Clear(System.Drawing.Color.Transparent);

                        for (int i = 0; i < selectedItems.Count; i++) {
                            string filePath = selectedItems[i].FilePath;
                            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath)) {
                                Debug.WriteLine($"File not found: {filePath}");
                                continue;
                            }

                            int x, y;
                            if (selectedItems.Count == 2) {
                                if (i == 0) {
                                    x = 0;
                                    y = cellSize;
                                }
                                else {
                                    x = cellSize;
                                    y = 0;
                                }
                            }
                            else {
                                int row = i / gridSize;
                                int col = i % gridSize;
                                x = col * cellSize;
                                y = row * cellSize;
                            }

                            System.Drawing.Bitmap iconBitmap = null;

                            if (Path.GetExtension(filePath).ToLower() == ".lnk") {
                                dynamic shell = Microsoft.VisualBasic.Interaction.CreateObject("WScript.Shell");
                                dynamic shortcut = shell.CreateShortcut(filePath);

                                string iconPath = shortcut.IconLocation;
                                string targetPath = shortcut.TargetPath;

                                if (!string.IsNullOrEmpty(iconPath) && iconPath != ",") {
                                    string[] iconInfo = iconPath.Split(',');
                                    string actualIconPath = iconInfo[0].Trim();
                                    int iconIndex = iconInfo.Length > 1 ? int.Parse(iconInfo[1].Trim()) : 0;

                                    if (File.Exists(actualIconPath)) {
                                        iconBitmap = ExtractSpecificIcon(actualIconPath, iconIndex);
                                    }
                                }

                                if (iconBitmap == null && !string.IsNullOrEmpty(targetPath) && File.Exists(targetPath)) {
                                    iconBitmap = ExtractIconWithoutArrow(targetPath);
                                }
                            }
                            else {
                                iconBitmap = ExtractIconWithoutArrow(filePath);
                            }

                            if (iconBitmap != null) {
                                try {
                                    int padding = 5;
                                    int drawSize = cellSize - (padding * 2);
                                    graphics.DrawImage(iconBitmap, new System.Drawing.Rectangle(
                                        x + padding, y + padding, drawSize, drawSize));
                                }
                                catch (Exception ex) {
                                    Debug.WriteLine($"Error processing icon {i}: {ex.Message}");
                                }
                                finally {
                                    iconBitmap.Dispose();
                                }
                            }
                            else {
                                Debug.WriteLine($"Failed to get icon for file: {filePath}");
                            }
                        }

                        bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
                    }
                }

                StorageFile iconFile = await StorageFile.GetFileFromPathAsync(outputPath);
                BitmapImage gridIcon = new BitmapImage();

                using (var stream = await iconFile.OpenReadAsync()) {
                    await gridIcon.SetSourceAsync(stream);
                }

                iconPreviewImage.Source = gridIcon;
                iconPreviewBorder.Visibility = Visibility.Visible;
                return outputPath;
            }
            catch (Exception ex) {
                Debug.WriteLine($"Grid icon creation error: {ex.Message}");
                return null;
            }
        }



    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SHFILEINFO {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    public static class NativeMethods {
        public const uint SHGFI_ICON = 0x000000100;
        public const uint SHGFI_LARGEICON = 0x000000000;
        public const uint SHGFI_SMALLICON = 0x000000001;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr ExtractAssociatedIcon(IntPtr hInst, StringBuilder lpIconPath, out ushort lpiIcon);
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern uint ExtractIconEx(string szFileName, int nIconIndex,
       IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);


        [DllImport("shell32.dll")]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr handle);
    }
}
