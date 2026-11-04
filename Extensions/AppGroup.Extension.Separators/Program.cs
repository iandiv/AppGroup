using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.Json;

namespace AppGroup.Extension.Separators;

// Protocol v1, called by AppGroup:
//   create [--style line|blank]
// Writes one JSON object to stdout:
//   { "ok": true, "path": "<separator .lnk>" }
//   { "ok": false, "error": "..." }
// A pinned separator launches this exe with --taskbar-separator, which exits at once.
internal static class Program {
    private const string SeparatorArgument = "--taskbar-separator";
    private const string AppIdPrefix = "IanDiv.AppGroup.Separator.";

    [STAThread]
    private static int Main(string[] args) {
        if (args.Contains(SeparatorArgument, StringComparer.OrdinalIgnoreCase)) return 0;
        try {
            string command = args.Length > 0 ? args[0] : "";
            return command switch {
                "create" => Create(GetOption(args, "--style") ?? "line"),
                _ => Fail($"Unknown command '{command}'.")
            };
        }
        catch (Exception ex) {
            return Fail(ex.Message);
        }
    }

    private static int Create(string style) {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AppGroup", "Separators");
        Directory.CreateDirectory(folder);

        int number = 1;
        while (File.Exists(Path.Combine(folder, $"Separator {number}.lnk"))) number++;
        string name = $"Separator {number}";

        string iconPath = Path.Combine(folder, $"{name}.ico");
        WriteSeparatorIcon(iconPath, drawLine: !style.Equals("blank", StringComparison.OrdinalIgnoreCase));

        string exePath = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot resolve the extension path.");
        string shortcutPath = Path.Combine(folder, $"{name}.lnk");
        CreateShortcut(shortcutPath, exePath, SeparatorArgument, iconPath, AppIdPrefix + number);

        Write(new { ok = true, path = shortcutPath });
        return 0;
    }

    private static void WriteSeparatorIcon(string icoPath, bool drawLine) {
        const int size = 256;
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap)) {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (drawLine) {
                using var shadow = new Pen(Color.FromArgb(190, 72, 72, 72), 8) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                using var highlight = new Pen(Color.FromArgb(210, 224, 224, 224), 4) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(shadow, 124, 48, 124, 208);
                g.DrawLine(highlight, 132, 48, 132, 208);
            }
        }

        using var png = new MemoryStream();
        bitmap.Save(png, ImageFormat.Png);
        byte[] pngBytes = png.ToArray();

        using var ico = new BinaryWriter(File.Create(icoPath));
        ico.Write((ushort)0);
        ico.Write((ushort)1);
        ico.Write((ushort)1);
        ico.Write((byte)0);
        ico.Write((byte)0);
        ico.Write((byte)0);
        ico.Write((byte)0);
        ico.Write((ushort)1);
        ico.Write((ushort)32);
        ico.Write(pngBytes.Length);
        ico.Write(6 + 16);
        ico.Write(pngBytes);
    }

    private static void CreateShortcut(string shortcutPath, string target, string arguments, string iconPath, string appUserModelId) {
        var link = (IShellLinkW)new CShellLink();
        link.SetPath(target);
        link.SetArguments(arguments);
        link.SetWorkingDirectory(Path.GetDirectoryName(target) ?? "");
        link.SetIconLocation(iconPath, 0);
        link.SetDescription("AppGroup taskbar separator");

        var store = (IPropertyStore)link;
        var key = new PROPERTYKEY { fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), pid = 5 };
        var value = new PROPVARIANT { vt = 31, pointerValue = Marshal.StringToCoTaskMemUni(appUserModelId) };
        try {
            store.SetValue(ref key, ref value);
            store.Commit();
        }
        finally {
            PropVariantClear(ref value);
        }

        ((IPersistFile)link).Save(shortcutPath, true);
    }

    private static string? GetOption(string[] args, string name) {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static int Fail(string error) {
        Write(new { ok = false, error });
        return 1;
    }

    private static void Write(object payload) {
        using var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
        stdout.Write(JsonSerializer.Serialize(payload));
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class CShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    private interface IPropertyStore {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PROPERTYKEY pkey);
        void GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        void SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        void Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PROPERTYKEY {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PROPVARIANT {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr pointerValue;
        [FieldOffset(8)] public long padding;
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PROPVARIANT pvar);
}
