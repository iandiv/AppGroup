using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AppGroup.Extension.Folders;

// Protocol v1, called by AppGroup:
//   list --path <folder> [--max <n>]
// Writes one JSON object to stdout:
//   { "ok": true, "items": [ { "name", "path", "isFolder", "icon" } ] }
//   { "ok": false, "error": "..." }
internal static class Program {
    private const int DefaultMax = 100;

    [STAThread]
    private static int Main(string[] args) {
        try {
            string command = args.Length > 0 ? args[0] : "";
            return command switch {
                "list" => List(GetOption(args, "--path"), ParseMax(GetOption(args, "--max"))),
                _ => Fail($"Unknown command '{command}'.")
            };
        }
        catch (Exception ex) {
            return Fail(ex.Message);
        }
    }

    private static int List(string? folder, int max) {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return Fail("Folder not found.");

        var dir = new DirectoryInfo(folder);
        var entries = dir.EnumerateFileSystemInfos()
            .Where(e => (e.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
            .OrderBy(e => e is FileInfo)
            .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(max);

        string iconDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AppGroup", "Extensions", "cache", "folders");
        Directory.CreateDirectory(iconDir);

        var items = new List<object>();
        foreach (var entry in entries) {
            bool isFolder = entry is DirectoryInfo;
            string name = isFolder || !IsShortcutLike(entry.Extension)
                ? entry.Name
                : Path.GetFileNameWithoutExtension(entry.Name);
            items.Add(new {
                name,
                path = entry.FullName,
                isFolder,
                icon = ExtractIcon(entry.FullName, iconDir)
            });
        }

        Write(new { ok = true, items });
        return 0;
    }

    private static bool IsShortcutLike(string extension) =>
        extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".url", StringComparison.OrdinalIgnoreCase);

    private static string? ExtractIcon(string path, string iconDir) {
        try {
            string key = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(
                path.ToLowerInvariant() + "|" + File.GetLastWriteTimeUtc(path).Ticks)));
            string pngPath = Path.Combine(iconDir, key + ".png");
            if (File.Exists(pngPath)) return pngPath;

            var info = new SHFILEINFO();
            IntPtr result = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf(info), SHGFI_ICON | SHGFI_LARGEICON);
            if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero) return null;
            try {
                using var icon = Icon.FromHandle(info.hIcon);
                using Bitmap bitmap = icon.ToBitmap();
                bitmap.Save(pngPath, ImageFormat.Png);
            }
            finally {
                DestroyIcon(info.hIcon);
            }
            return pngPath;
        }
        catch {
            return null;
        }
    }

    private static string? GetOption(string[] args, string name) {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static int ParseMax(string? value) =>
        int.TryParse(value, out int max) && max > 0 ? max : DefaultMax;

    private static int Fail(string error) {
        Write(new { ok = false, error });
        return 1;
    }

    private static void Write(object payload) {
        using var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
        stdout.Write(JsonSerializer.Serialize(payload));
    }

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
