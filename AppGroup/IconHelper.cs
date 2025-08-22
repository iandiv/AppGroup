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
using System.Xml;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Image = Microsoft.UI.Xaml.Controls.Image;

namespace AppGroup {
    public class IconHelper {

        public static string FindOrigIcon(string icoFilePath) {
            if (string.IsNullOrEmpty(icoFilePath)) {
                return icoFilePath;
            }

            string[] possibleExtensions = { ".png", ".jpg", ".jpeg" };
            string directory = Path.GetDirectoryName(icoFilePath);
            string filenameWithoutExtension = Path.GetFileNameWithoutExtension(icoFilePath);

            foreach (string ext in possibleExtensions) {
                string potentialPath = Path.Combine(directory, filenameWithoutExtension + ext);
                if (File.Exists(potentialPath)) {
                    return potentialPath;
                }
            }

            return icoFilePath;
        }

        /// <summary>
        /// Creates a copy of the icon file with _bg suffix for backup purposes
        /// The actual background addition will be handled by a modified version of your CreateGridIconAsync
        /// </summary>
        public static async Task<string> PrepareIconWithBackgroundAsync(string originalIconPath) {
            try {
                if (string.IsNullOrEmpty(originalIconPath) || !File.Exists(originalIconPath)) {
                    return originalIconPath;
                }

                string directory = Path.GetDirectoryName(originalIconPath);
                string filenameWithoutExtension = Path.GetFileNameWithoutExtension(originalIconPath);
                string extension = Path.GetExtension(originalIconPath);

                // Create new filename with _bg suffix
                string newIconPath = Path.Combine(directory, $"{filenameWithoutExtension}_bg{extension}");

                // For now, just copy the original file - we'll add background in CreateSingleIconWithBackground
                File.Copy(originalIconPath, newIconPath, true);

                return newIconPath;
            }
            catch (Exception ex) {
                Console.WriteLine($"Error preparing icon with background: {ex.Message}");
                return originalIconPath;
            }
        }

        /// <summary>
        /// Removes the background version of an icon file (both PNG and ICO)
        /// </summary>
        /// <param name="iconWithBackgroundPath">Path to the icon with background</param>
        public static void RemoveBackgroundIcon(string iconWithBackgroundPath) {
            try {
                if (!string.IsNullOrEmpty(iconWithBackgroundPath) && iconWithBackgroundPath.Contains("_bg")) {
                    // Remove the ICO file
                    if (File.Exists(iconWithBackgroundPath)) {
                        File.Delete(iconWithBackgroundPath);
                    }

                    // Also remove the PNG file if it exists
                    string pngPath = Path.ChangeExtension(iconWithBackgroundPath, ".png");
                    if (File.Exists(pngPath)) {
                        File.Delete(pngPath);
                    }
                }
            }
            catch (Exception ex) {
                Console.WriteLine($"Error removing background icon: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the original icon path from a background version path
        /// </summary>
        /// <param name="iconWithBackgroundPath">Path to icon with background</param>
        /// <returns>Original icon path</returns>
        public static string GetOriginalIconPath(string iconWithBackgroundPath) {
            if (string.IsNullOrEmpty(iconWithBackgroundPath)) {
                return iconWithBackgroundPath;
            }

            if (iconWithBackgroundPath.Contains("_bg")) {
                return iconWithBackgroundPath.Replace("_bg", "");
            }

            return iconWithBackgroundPath;
        }

        /// <summary>
        /// Creates a single icon with white background using the same System.Drawing approach as CreateGridIconAsync
        /// Add this method to your existing class that contains CreateGridIconAsync
        /// </summary>
        /// <summary>
        /// Creates a single icon with white background using the same System.Drawing approach as CreateGridIconAsync
        /// Add this method to your existing class that contains CreateGridIconAsync
        /// </summary>
       // Option 1: Convert PNG icon to black and white (grayscale)
public static async Task<string> CreateBlackWhiteIconAsync(string originalIconPath) {
    try {
        if (string.IsNullOrEmpty(originalIconPath) || !File.Exists(originalIconPath)) {
            Console.WriteLine("Invalid path or file doesn't exist");
            return originalIconPath;
        }

        string directory = Path.GetDirectoryName(originalIconPath);
        string filenameWithoutExtension = Path.GetFileNameWithoutExtension(originalIconPath);

        Console.WriteLine($"Converting PNG to B&W: {originalIconPath}");

        // Load the PNG directly
        System.Drawing.Bitmap originalBitmap;
        try {
            originalBitmap = new System.Drawing.Bitmap(originalIconPath);
            Console.WriteLine($"✅ PNG loaded: {originalBitmap.Width}x{originalBitmap.Height}");
        }
        catch (Exception ex) {
            Console.WriteLine($"❌ Failed to load PNG: {ex.Message}");
            return originalIconPath;
        }

        string pngPath;
        using (var bwBitmap = new System.Drawing.Bitmap(originalBitmap.Width, originalBitmap.Height)) {
            // Convert to black and white pixel by pixel
            for (int x = 0; x < originalBitmap.Width; x++) {
                for (int y = 0; y < originalBitmap.Height; y++) {
                    System.Drawing.Color originalColor = originalBitmap.GetPixel(x, y);
                    
                    // Convert to grayscale using luminance formula
                    int grayValue = (int)(originalColor.R * 0.299 + originalColor.G * 0.587 + originalColor.B * 0.114);
                    
                    // Keep the original alpha channel for transparency
                    System.Drawing.Color grayColor = System.Drawing.Color.FromArgb(originalColor.A, grayValue, grayValue, grayValue);
                    bwBitmap.SetPixel(x, y, grayColor);
                }
            }

            // Save as PNG
            pngPath = Path.Combine(directory, $"{filenameWithoutExtension}_bw.png");
            bwBitmap.Save(pngPath, System.Drawing.Imaging.ImageFormat.Png);
            Console.WriteLine($"✅ B&W PNG saved to: {pngPath}");
        }

        originalBitmap.Dispose();

        // Convert PNG to ICO using your existing method
        string icoPath = Path.Combine(directory, $"{filenameWithoutExtension}_bw.ico");
        bool iconSuccess = await ConvertToIco(pngPath, icoPath);

        if (iconSuccess) {
            Console.WriteLine($"✅ B&W ICO created successfully: {icoPath}");
            return icoPath;
        }
        else {
            Console.WriteLine("⚠️  Failed to convert PNG to ICO, returning PNG path");
            return pngPath;
        }
    }
    catch (Exception ex) {
        Console.WriteLine($"❌ Error creating B&W icon: {ex.Message}");
        return originalIconPath;
    }
}

// Option 2: Add bottom border to PNG icon
public static async Task<string> CreateIconWithBottomBorderAsync(string originalIconPath, System.Drawing.Color borderColor = default, int borderHeight = 10) {
    try {
        if (string.IsNullOrEmpty(originalIconPath) || !File.Exists(originalIconPath)) {
            Console.WriteLine("Invalid path or file doesn't exist");
            return originalIconPath;
        }

        // Default to white border if no color specified
        if (borderColor == default) {
            borderColor = System.Drawing.Color.White;
        }

        string directory = Path.GetDirectoryName(originalIconPath);
        string filenameWithoutExtension = Path.GetFileNameWithoutExtension(originalIconPath);

        Console.WriteLine($"Adding bottom border to PNG: {originalIconPath}");
        Console.WriteLine($"Border color: {borderColor}, Height: {borderHeight}px");

        // Load the PNG directly
        System.Drawing.Bitmap originalBitmap;
        try {
            originalBitmap = new System.Drawing.Bitmap(originalIconPath);
            Console.WriteLine($"✅ PNG loaded: {originalBitmap.Width}x{originalBitmap.Height}");
        }
        catch (Exception ex) {
            Console.WriteLine($"❌ Failed to load PNG: {ex.Message}");
            return originalIconPath;
        }

        // Create new bitmap with extra height for bottom border
        int newWidth = originalBitmap.Width;
        int newHeight = originalBitmap.Height + borderHeight;

        string pngPath;
        using (var newBitmap = new System.Drawing.Bitmap(newWidth, newHeight)) {
            using (var graphics = System.Drawing.Graphics.FromImage(newBitmap)) {
                // Set high quality rendering
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;

                // Fill entire image with transparent background first
                graphics.Clear(System.Drawing.Color.Transparent);

                // Draw the original PNG at the top
                graphics.DrawImage(originalBitmap, 0, 0, originalBitmap.Width, originalBitmap.Height);
                Console.WriteLine("✓ Original PNG drawn at top");

                // Draw the bottom border
                using (var borderBrush = new System.Drawing.SolidBrush(borderColor)) {
                    graphics.FillRectangle(borderBrush, 0, originalBitmap.Height, newWidth, borderHeight);
                    Console.WriteLine($"✓ Bottom border added: {borderColor} ({borderHeight}px high)");
                }
            }

            // Save as PNG
            pngPath = Path.Combine(directory, $"{filenameWithoutExtension}_border.png");
            newBitmap.Save(pngPath, System.Drawing.Imaging.ImageFormat.Png);
            Console.WriteLine($"✅ Border PNG saved to: {pngPath}");
        }

        originalBitmap.Dispose();

        // Convert PNG to ICO using your existing method
        string icoPath = Path.Combine(directory, $"{filenameWithoutExtension}_border.ico");
        bool iconSuccess = await ConvertToIco(pngPath, icoPath);

        if (iconSuccess) {
            Console.WriteLine($"✅ Border ICO created successfully: {icoPath}");
            return icoPath;
        }
        else {
            Console.WriteLine("⚠️  Failed to convert PNG to ICO, returning PNG path");
            return pngPath;
        }
    }
    catch (Exception ex) {
        Console.WriteLine($"❌ Error creating icon with border: {ex.Message}");
        return originalIconPath;
    }
}

// Usage examples:
// 
// Option 1 - Convert PNG to black and white:
// string bwIconPath = await CreateBlackWhiteIconAsync("C:\\path\\to\\icon.png");
//
// Option 2 - Add white bottom border (10px default):
// string borderIconPath = await CreateIconWithBottomBorderAsync("C:\\path\\to\\icon.png");
//
// Option 2 - Add red bottom border (20px):
// string redBorderIconPath = await CreateIconWithBottomBorderAsync("C:\\path\\to\\icon.png", System.Drawing.Color.Red, 20);
//
// Option 2 - Add black bottom border (15px):
// string blackBorderIconPath = await CreateIconWithBottomBorderAsync("C:\\path\\to\\icon.png", System.Drawing.Color.Black, 15);
        private static async Task<Bitmap> ExtractWindowsAppIconAsync(string shortcutPath, string outputDirectory) {
            try {
                // Get the shortcut target using Shell COM objects
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return null;

                dynamic shell = Activator.CreateInstance(shellType);
                dynamic folder = shell.Namespace(Path.GetDirectoryName(shortcutPath));
                dynamic shortcutItem = folder.ParseName(Path.GetFileName(shortcutPath));

                // Find the "Link target" property
                string linkTarget = null;
                for (int i = 0; i < 500; i++) {
                    string propertyName = folder.GetDetailsOf(null, i);
                    if (propertyName == "Link target") {
                        linkTarget = folder.GetDetailsOf(shortcutItem, i);
                        break;
                    }
                }

                if (string.IsNullOrEmpty(linkTarget)) return null;

                // Extract the app name from the link target (remove everything after the first "_")
                string appName = System.Text.RegularExpressions.Regex.Replace(linkTarget, "_.*$", "");
                if (string.IsNullOrEmpty(appName)) return null;

                // Use Windows Runtime API to find the package
                Windows.Management.Deployment.PackageManager packageManager = new Windows.Management.Deployment.PackageManager();
                IEnumerable<Windows.ApplicationModel.Package> packages = packageManager.FindPackagesForUser("");

                // Find the package that matches the app name
                Windows.ApplicationModel.Package appPackage = packages.FirstOrDefault(p => p.Id.Name.StartsWith(appName, StringComparison.OrdinalIgnoreCase));
                if (appPackage == null) return null;

                string installPath = appPackage.InstalledLocation.Path;
                string manifestPath = Path.Combine(installPath, "AppxManifest.xml");

                if (!File.Exists(manifestPath)) return null;

                // Load and parse the manifest XML
                XmlDocument manifest = new XmlDocument();
                manifest.Load(manifestPath);

                // Create namespace manager
                XmlNamespaceManager nsManager = new XmlNamespaceManager(manifest.NameTable);
                nsManager.AddNamespace("ns", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");

                // Get logo path from manifest
                XmlNode logoNode = manifest.SelectSingleNode("/ns:Package/ns:Properties/ns:Logo", nsManager);
                if (logoNode == null) return null;

                string logoPath = logoNode.InnerText;
                string logoDir = Path.Combine(installPath, Path.GetDirectoryName(logoPath));

                if (!Directory.Exists(logoDir)) return null;

                string[] logoPatterns = new[] {

    "*StoreLogo*.png",

        };

                string highestResLogoPath = null;
                long highestSize = 0;

                foreach (string pattern in logoPatterns) {
                    foreach (string file in Directory.GetFiles(logoDir, pattern, SearchOption.AllDirectories)) {
                        FileInfo fileInfo = new FileInfo(file);
                        if (fileInfo.Length > highestSize) {
                            highestSize = fileInfo.Length;
                            highestResLogoPath = file;
                        }
                    }

                    if (highestResLogoPath != null) break;
                }

                if (string.IsNullOrEmpty(highestResLogoPath) || !File.Exists(highestResLogoPath)) return null;

                // Load the image and resize/crop it to 200x200
                using (FileStream stream = new FileStream(highestResLogoPath, FileMode.Open, FileAccess.Read)) {
                    using (var originalBitmap = new Bitmap(stream)) {
                        // Create a square bitmap of 200x200
                        var resizedIcon = ResizeAndCropImageToSquare(originalBitmap, 200);
                        return resizedIcon;
                    }
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error extracting Windows app icon: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Resizes and crops an image to a square with the specified size
        /// </summary>
        private static Bitmap ResizeAndCropImageToSquare(Bitmap originalImage, int size, float zoomFactor = 1.3f) {
            try {
                // Create a new square bitmap
                Bitmap resizedImage = new Bitmap(size, size);

                // Calculate dimensions for maintaining aspect ratio
                int sourceWidth = originalImage.Width;
                int sourceHeight = originalImage.Height;

                // Find the smallest dimension and calculate the crop area
                int cropSize = Math.Min(sourceWidth, sourceHeight);

                // Apply zoom factor (smaller crop size = more zoom)
                cropSize = (int)(cropSize / zoomFactor);

                // Center the cropping rectangle
                int cropX = (sourceWidth - cropSize) / 2;
                int cropY = (sourceHeight - cropSize) / 2;

                // Create a graphics object to perform the resize
                using (Graphics g = Graphics.FromImage(resizedImage)) {
                    // Set high quality mode for better results
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                    // Draw the centered and cropped image to maintain aspect ratio
                    g.DrawImage(originalImage,
                        new Rectangle(0, 0, size, size),
                        new Rectangle(cropX, cropY, cropSize, cropSize),
                        GraphicsUnit.Pixel);
                }

                return resizedImage;
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error resizing image: {ex.Message}");
                return null;
            }
        }

        // Also modify the main ExtractIconAndSaveAsync method to use this resize function for all icons
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
                        string appName = string.Empty;
                        if (Path.GetExtension(filePath).ToLower() == ".lnk") {
                            // Try to extract Windows app shortcut icon first
                            iconBitmap = await ExtractWindowsAppIconAsync(filePath, outputDirectory);


                            // If Windows app icon extraction failed, fall back to the existing method
                            if (iconBitmap == null) {
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

                                //Use the Icon with arrow as fallback
                                if (iconBitmap == null) {
                                    Icon icon = Icon.ExtractAssociatedIcon(filePath);
                                    iconBitmap = icon.ToBitmap();
                                }
                            }
                        }
                        else {
                            iconBitmap = ExtractIconWithoutArrow(filePath);
                           
                        }
                        if (iconBitmap == null) {
                            Debug.WriteLine($"No icon found for file: {filePath}");
                            return null;
                        }

                        //using (Bitmap resizedIcon = ResizeAndCropImageToSquare(iconBitmap, 200)) {
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
                        //}
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
                        NativeMethods.SHFILEINFO shfi = new NativeMethods.SHFILEINFO();
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

        public static async Task<bool> ConvertToIco(string sourcePath, string icoFilePath) {
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(icoFilePath)) {
                Debug.WriteLine("Invalid source or destination path.");
                return false;
            }

            if (!File.Exists(sourcePath)) {
                Debug.WriteLine($"Source file not found: {sourcePath}");
                return false;
            }

            try {
                string tempIconPath = null;

                // If the source file is an .exe, extract the icon first
                if (Path.GetExtension(sourcePath).Equals(".exe", StringComparison.OrdinalIgnoreCase)) {
                    tempIconPath = await IconCache.GetIconPathAsync(sourcePath);
                    if (string.IsNullOrEmpty(tempIconPath)) {
                        Debug.WriteLine("Failed to extract icon from .exe file.");
                        return false;
                    }
                    sourcePath = tempIconPath;
                }

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

                // Clean up the temporary icon file if it was created
                if (!string.IsNullOrEmpty(tempIconPath) && File.Exists(tempIconPath)) {
                    File.Delete(tempIconPath);
                }

                return true;
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error converting to ICO: {ex.Message}");
                return false;
            }
        }


        //public static bool ConvertToIco(string sourcePath, string icoFilePath) {
        //    if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(icoFilePath)) {
        //        Debug.WriteLine("Invalid source or destination path.");
        //        return false;
        //    }

        //    if (!File.Exists(sourcePath)) {
        //        Debug.WriteLine($"Source file not found: {sourcePath}");
        //        return false;
        //    }

        //    try {
        //        using (System.Drawing.Image originalImage = System.Drawing.Image.FromFile(sourcePath)) {
        //            Size[] sizes = new Size[] { new Size(256, 256), new Size(128, 128), new Size(64, 64), new Size(32, 32), new Size(16, 16) };

        //            using (FileStream fs = new FileStream(icoFilePath, FileMode.Create)) {
        //                BinaryWriter bw = new BinaryWriter(fs);
        //                bw.Write((short)0);
        //                bw.Write((short)1);
        //                bw.Write((short)sizes.Length);

        //                int headerSize = 6 + (16 * sizes.Length);
        //                int dataOffset = headerSize;
        //                List<byte[]> imageDataList = new List<byte[]>();

        //                foreach (Size size in sizes) {
        //                    using (Bitmap bitmap = new Bitmap(originalImage, size)) {
        //                        using (MemoryStream ms = new MemoryStream()) {
        //                            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        //                            byte[] imageData = ms.ToArray();
        //                            imageDataList.Add(imageData);
        //                        }
        //                    }
        //                }

        //                for (int i = 0; i < sizes.Length; i++) {
        //                    Size size = sizes[i];
        //                    byte[] imageData = imageDataList[i];

        //                    bw.Write((byte)size.Width);
        //                    bw.Write((byte)size.Height);
        //                    bw.Write((byte)0);
        //                    bw.Write((byte)0);
        //                    bw.Write((short)1);
        //                    bw.Write((short)32);
        //                    bw.Write((int)imageData.Length);
        //                    bw.Write((int)dataOffset);

        //                    dataOffset += imageData.Length;
        //                }

        //                foreach (byte[] imageData in imageDataList) {
        //                    bw.Write(imageData);
        //                }

        //                bw.Flush();
        //            }
        //        }
        //        return true;
        //    }
        //    catch (Exception ex) {
        //        Debug.WriteLine($"Error converting to ICO: {ex.Message}");
        //        return false;
        //    }
        //}

        //      public async Task<string> CreateGridIconAsync(
        //List<ExeFileModel> selectedItems,
        //int selectedSize,
        //Image iconPreviewImage,
        //Border iconPreviewBorder) {
        //          try {
        //              if (selectedItems == null || selectedSize <= 0) {
        //                  throw new ArgumentException("Invalid selected items or grid size.");
        //              }

        //              selectedItems = selectedItems.Take(selectedSize * selectedSize).ToList();

        //              int finalSize = 256;
        //              int gridSize;
        //              int cellSize;

        //              if (selectedItems.Count == 2) {
        //                  gridSize = 2;
        //                  cellSize = finalSize / 2;
        //              }
        //              else {
        //                  gridSize = (int)Math.Ceiling(Math.Sqrt(selectedItems.Count));
        //                  cellSize = finalSize / gridSize;
        //              }

        //              string tempFolder = Path.Combine(Path.GetTempPath(), "GridIconTemp");
        //              Directory.CreateDirectory(tempFolder);
        //              string outputPath = Path.Combine(tempFolder, "grid_icon.png");

        //              using (var bitmap = new System.Drawing.Bitmap(finalSize, finalSize)) {
        //                  using (var graphics = System.Drawing.Graphics.FromImage(bitmap)) {
        //                      graphics.Clear(System.Drawing.Color.Transparent);

        //                      for (int i = 0; i < selectedItems.Count; i++) {
        //                          string filePath = selectedItems[i].FilePath;
        //                          if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath)) {
        //                              Debug.WriteLine($"File not found: {filePath}");
        //                              continue;
        //                          }

        //                          int x, y;
        //                          if (selectedItems.Count == 2) {
        //                              if (i == 0) {
        //                                  x = 0;
        //                                  y = cellSize;
        //                              }
        //                              else {
        //                                  x = cellSize;
        //                                  y = 0;
        //                              }
        //                          }
        //                          else {
        //                              int row = i / gridSize;
        //                              int col = i % gridSize;
        //                              x = col * cellSize;
        //                              y = row * cellSize;
        //                          }

        //                          System.Drawing.Bitmap iconBitmap = null;

        //                          if (Path.GetExtension(filePath).ToLower() == ".lnk") {
        //                              iconBitmap = await ExtractWindowsAppIconAsync(filePath, tempFolder);



        //                              dynamic shell = Microsoft.VisualBasic.Interaction.CreateObject("WScript.Shell");
        //                              dynamic shortcut = shell.CreateShortcut(filePath);

        //                              string iconPath = shortcut.IconLocation;
        //                              string targetPath = shortcut.TargetPath;

        //                              if (!string.IsNullOrEmpty(iconPath) && iconPath != ",") {
        //                                  string[] iconInfo = iconPath.Split(',');
        //                                  string actualIconPath = iconInfo[0].Trim();
        //                                  int iconIndex = iconInfo.Length > 1 ? int.Parse(iconInfo[1].Trim()) : 0;

        //                                  if (File.Exists(actualIconPath)) {
        //                                      iconBitmap = ExtractSpecificIcon(actualIconPath, iconIndex);
        //                                  }
        //                              }

        //                              if (iconBitmap == null && !string.IsNullOrEmpty(targetPath) && File.Exists(targetPath)) {
        //                                  iconBitmap = ExtractIconWithoutArrow(targetPath);
        //                              }
        //                              //Use the Icon with arrow as fallback
        //                              if (iconBitmap == null) {
        //                                  Icon icon = Icon.ExtractAssociatedIcon(filePath);
        //                                  iconBitmap = icon.ToBitmap();
        //                              }
        //                          }
        //                          else {
        //                              iconBitmap = ExtractIconWithoutArrow(filePath);
        //                          }

        //                          if (iconBitmap != null) {
        //                              try {
        //                                  int padding = 5;
        //                                  int drawSize = cellSize - (padding * 2);
        //                                  graphics.DrawImage(iconBitmap, new System.Drawing.Rectangle(
        //                                      x + padding, y + padding, drawSize, drawSize));
        //                              }
        //                              catch (Exception ex) {
        //                                  Debug.WriteLine($"Error processing icon {i}: {ex.Message}");
        //                              }
        //                              finally {
        //                                  iconBitmap.Dispose();
        //                              }
        //                          }
        //                          else {
        //                              Debug.WriteLine($"Failed to get icon for file: {filePath}");
        //                          }
        //                      }

        //                      bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        //                  }
        //              }

        //              StorageFile iconFile = await StorageFile.GetFileFromPathAsync(outputPath);
        //              BitmapImage gridIcon = new BitmapImage();

        //              using (var stream = await iconFile.OpenReadAsync()) {
        //                  await gridIcon.SetSourceAsync(stream);
        //              }

        //              iconPreviewImage.Source = gridIcon;
        //              iconPreviewBorder.Visibility = Visibility.Visible;
        //              return outputPath;
        //          }
        //          catch (Exception ex) {
        //              Debug.WriteLine($"Grid icon creation error: {ex.Message}");
        //              return null;
        //          }
        //      }

        public async Task<string> CreateGridIconForPopupAsync(List<ExeFileModel> items, int gridSize, string groupName) {
            try {
                if (items == null || !items.Any()) {
                    throw new ArgumentException("No items provided for grid icon creation");
                }

                // Ensure we have the right number of items for the grid
                int maxItems = gridSize * gridSize;
                var gridItems = items.Take(maxItems).ToList();

                // Fill missing slots with empty items if needed
                //while (gridItems.Count < maxItems) {
                //    gridItems.Add(new ExeFileModel {
                //        FileName = "",
                //        FilePath = "",
                //        Icon = "",
                //        Tooltip = "",
                //        Args = "",
                //        IconPath = ""
                //    });
                //}

                // Create temporary UI elements (they won't be displayed)
                var tempImage = new Image();
                var tempBorder = new Border();

                // Reuse CreateGridIconAsync to ensure identical positioning
                string tempGridIconPath = await CreateGridIconAsync(
                    gridItems,
                    gridSize,
                    tempImage,
                    tempBorder
                );

                if (string.IsNullOrEmpty(tempGridIconPath)) {
                    throw new Exception("Failed to create grid icon using CreateGridIconAsync");
                }

                // Now copy the generated icon to the proper location for the group
                string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appDataPath = Path.Combine(localAppDataPath, "AppGroup");
                string groupsFolder = Path.Combine(appDataPath, "Groups");
                string groupFolder = Path.Combine(groupsFolder, groupName);
                string uniqueFolderName = groupName;
                string uniqueFolderPath = Path.Combine(groupFolder, uniqueFolderName);

                Directory.CreateDirectory(uniqueFolderPath);

                string iconBaseName = $"{groupName}_{(gridSize == 3 ? "grid3" : "grid")}";
                string finalIcoFilePath = Path.Combine(uniqueFolderPath, $"{iconBaseName}.ico");
                string finalPngFilePath = Path.Combine(uniqueFolderPath, $"{iconBaseName}.png");

                // Copy the temp PNG to final location
                File.Copy(tempGridIconPath, finalPngFilePath, true);

                // Convert to ICO
                bool success = await ConvertToIco(finalPngFilePath, finalIcoFilePath);

                // Clean up temp file
                try {
                    File.Delete(tempGridIconPath);
                    // Also clean up temp directory if it exists
                    string tempDir = Path.GetDirectoryName(tempGridIconPath);
                    if (Directory.Exists(tempDir) && Directory.GetFiles(tempDir).Length == 0) {
                        Directory.Delete(tempDir);
                    }
                }
                catch {
                    // Ignore cleanup errors
                }

                if (success) {
                    return finalIcoFilePath;
                }
                else {
                    throw new Exception("Failed to convert grid icon to ICO format");
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error creating grid icon for popup: {ex.Message}");
                throw;
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
                            var item = selectedItems[i];
                            string iconPath = !string.IsNullOrEmpty(item.IconPath) ? item.IconPath : item.Icon;

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
                            if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath)) {
                                // Use the custom icon path if available
                                iconBitmap = new System.Drawing.Bitmap(iconPath);
                            }
                            else {
                                // Fallback: Extract icon from the file path
                                string filePath = item.FilePath;
                                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) {
                                    Debug.WriteLine($"File not found: {filePath}");
                                    continue;
                                }

                                if (Path.GetExtension(filePath).ToLower() == ".lnk") {
                                    iconBitmap = await ExtractWindowsAppIconAsync(filePath, tempFolder);
                                    dynamic shell = Microsoft.VisualBasic.Interaction.CreateObject("WScript.Shell");
                                    dynamic shortcut = shell.CreateShortcut(filePath);
                                    string shortcutIconPath = shortcut.IconLocation;
                                    string targetPath = shortcut.TargetPath;
                                    if (!string.IsNullOrEmpty(shortcutIconPath) && shortcutIconPath != ",") {
                                        string[] iconInfo = shortcutIconPath.Split(',');
                                        string actualIconPath = iconInfo[0].Trim();
                                        int iconIndex = iconInfo.Length > 1 ? int.Parse(iconInfo[1].Trim()) : 0;
                                        if (File.Exists(actualIconPath)) {
                                            iconBitmap = ExtractSpecificIcon(actualIconPath, iconIndex);
                                        }
                                    }
                                    if (iconBitmap == null && !string.IsNullOrEmpty(targetPath) && File.Exists(targetPath)) {
                                        iconBitmap = ExtractIconWithoutArrow(targetPath);
                                    }
                                    // Use the Icon with arrow as fallback
                                    if (iconBitmap == null) {
                                        Icon icon = Icon.ExtractAssociatedIcon(filePath);
                                        iconBitmap = icon.ToBitmap();
                                    }
                                }
                                else {
                                    iconBitmap = ExtractIconWithoutArrow(filePath);
                                }
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
                                    iconBitmap?.Dispose();
                                }
                            }
                            else {
                                Debug.WriteLine($"Failed to get icon for file: {item.FilePath}");
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


}
