using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

namespace AppGroup {
    public static class GroupTrayManager {
        private static int WM_TASKBARCREATED;
        private static IntPtr _hwnd = IntPtr.Zero;
        private static NativeMethods.WndProcDelegate _wndProcDelegate;
        private static IntPtr _hMenu = IntPtr.Zero;
        private static int _menuActiveGroupId = -1;

        // groupId → (hIcon, groupName)
        private static readonly Dictionary<int, (IntPtr hIcon, string groupName)> _icons
            = new Dictionary<int, (IntPtr, string)>();

        private const uint WM_GROUPTRAY = NativeMethods.WM_TRAYICON + 1;
        private const string WndClassName = "AppGroupGroupTrayWndClass";
        private static uint GroupIdToUid(int groupId) => (uint)(0x1000 + groupId);
        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Call once after LoadGroupsAsync completes.</summary>
        /// 
        public static void SyncFromJson() {
            try {
                string jsonFilePath = JsonConfigHelper.GetDefaultConfigPath();
                if (!File.Exists(jsonFilePath)) return;

                string jsonContent = File.ReadAllText(jsonFilePath);
                JsonNode jsonObject = JsonNode.Parse(jsonContent ?? "{}") ?? new JsonObject();
                var groupDictionary = jsonObject.AsObject();

                EnsureWindow();
                var incoming = new HashSet<int>();

                foreach (var property in groupDictionary) {
                    if (!int.TryParse(property.Key, out int groupId)) continue;
                    string groupName = property.Value?["groupName"]?.GetValue<string>();
                    string groupIcon = property.Value?["groupIcon"]?.GetValue<string>();
                    bool showOnTray = property.Value?["showOnTray"]?.GetValue<bool>() ?? false;

                    if (string.IsNullOrWhiteSpace(groupName)) continue;
                    if (!showOnTray) continue;

                    incoming.Add(groupId);
                    var g = new GroupItem {
                        GroupId = groupId,
                        GroupName = groupName,
                        GroupIcon = groupIcon,
                        PathIcons = new System.Collections.Generic.List<string>()
                    };
                    AddGroup(g);
                }

                foreach (var id in new System.Collections.Generic.List<int>(_icons.Keys))
                    if (!incoming.Contains(id))
                        RemoveGroup(id);
            }
            catch (Exception ex) {
                Debug.WriteLine($"GroupTrayManager.SyncFromJson failed: {ex.Message}");
            }
        }
       

        public static void Cleanup() {
            RemoveAll();
            if (_hMenu != IntPtr.Zero) {
                NativeMethods.DestroyMenu(_hMenu);
                _hMenu = IntPtr.Zero;
            }
        }

        // ── Window ────────────────────────────────────────────────────────────

        private static void EnsureWindow() {
            if (_hwnd != IntPtr.Zero) return;

            _wndProcDelegate = WndProc;

            var wc = new NativeMethods.WNDCLASSEX {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                hInstance = NativeMethods.GetModuleHandle(null),
                hCursor = NativeMethods.LoadCursor(IntPtr.Zero, 32512u),
                lpszClassName = WndClassName
            };
            NativeMethods.RegisterClassEx(ref wc);
            WM_TASKBARCREATED = NativeMethods.RegisterWindowMessage("TaskbarCreated");
            _hwnd = NativeMethods.CreateWindowEx(0, WndClassName, "AppGroup GroupTray",
                0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero,
                NativeMethods.GetModuleHandle(null), IntPtr.Zero);
        }

        // ── Per-group icon ────────────────────────────────────────────────────

        private static void AddGroup(GroupItem g) {
            if (string.IsNullOrWhiteSpace(g.GroupName)) return;

            string iconPath = !string.IsNullOrWhiteSpace(g.GroupIcon)
                ? g.GroupIcon
                : g.PathIcons?.Count > 0 ? g.PathIcons[0] : null;

            bool isExisting = _icons.ContainsKey(g.GroupId);

            // Destroy old icon handle before re-adding
            if (_icons.TryGetValue(g.GroupId, out var old)) {
                if (old.hIcon != IntPtr.Zero)
                    NativeMethods.DestroyIcon(old.hIcon);
                _icons.Remove(g.GroupId);
            }

            IntPtr hIcon = LoadGroupIcon(iconPath);
            var nid = BuildNid(GroupIdToUid(g.GroupId), hIcon, g.GroupName);

            bool ok = NativeMethods.Shell_NotifyIcon(isExisting ? NativeMethods.NIM_MODIFY : NativeMethods.NIM_ADD, ref nid);

            // Fallback: if NIM_MODIFY fails try NIM_ADD, if NIM_ADD fails try NIM_MODIFY
            if (!ok) {
                ok = NativeMethods.Shell_NotifyIcon(isExisting ? NativeMethods.NIM_ADD : NativeMethods.NIM_MODIFY, ref nid);
            }

            if (!ok) {
                Debug.WriteLine($"GroupTrayManager: failed to add/modify icon for group {g.GroupId}");
                if (hIcon != IntPtr.Zero) NativeMethods.DestroyIcon(hIcon);
                return;
            }

            _icons[g.GroupId] = (hIcon, g.GroupName);
        }
        private static void RemoveGroup(int groupId) {
            var nid = new NativeMethods.NOTIFYICONDATA {
                cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = GroupIdToUid(groupId)
            };
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref nid);

            if (_icons.TryGetValue(groupId, out var entry)) {
                if (entry.hIcon != IntPtr.Zero)
                    NativeMethods.DestroyIcon(entry.hIcon);
                _icons.Remove(groupId);
            }
        }

        private static void RemoveAll() {
            foreach (var id in new List<int>(_icons.Keys))
                RemoveGroup(id);
        }

        // ── Icon loading ──────────────────────────────────────────────────────

        private static IntPtr LoadGroupIcon(string iconPath) {
            if (!string.IsNullOrWhiteSpace(iconPath)) {
                // If FindOrigIcon returned a .png/.jpg, look for sibling .ico
                string ext = Path.GetExtension(iconPath).ToLowerInvariant();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg") {
                    string icoPath = Path.Combine(
                        Path.GetDirectoryName(iconPath),
                        Path.GetFileNameWithoutExtension(iconPath) + ".ico");
                    if (File.Exists(icoPath))
                        iconPath = icoPath;
                }

                if (File.Exists(iconPath)) {
                    IntPtr h = NativeMethods.LoadImage(IntPtr.Zero, iconPath,
                        NativeMethods.IMAGE_ICON, 16, 16, NativeMethods.LR_LOADFROMFILE);
                    if (h != IntPtr.Zero) return h;
                }
            }
            // Fallback: generic application icon
            return NativeMethods.LoadImage(IntPtr.Zero, "#32516",
                NativeMethods.IMAGE_ICON, 16, 16, 0);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static NativeMethods.NOTIFYICONDATA BuildNid(uint uid, IntPtr hIcon, string tip) =>
            new NativeMethods.NOTIFYICONDATA {
                cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = uid,
                uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
                uCallbackMessage = WM_GROUPTRAY,
                hIcon = hIcon,
                szTip = tip.Length > 127 ? tip[..127] : tip
            };

        // ── WndProc ───────────────────────────────────────────────────────────

        private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam) {
            if ((int)msg == WM_TASKBARCREATED) {
                // Force re-add all icons
                var snapshot = new Dictionary<int, (IntPtr hIcon, string groupName)>(_icons);
                _icons.Clear();
                foreach (var kvp in snapshot) {
                    if (kvp.Value.hIcon != IntPtr.Zero)
                        NativeMethods.DestroyIcon(kvp.Value.hIcon);
                }
                SyncFromJson(); // rebuilds everything via NIM_ADD
                return IntPtr.Zero;
            }
            if (msg == WM_GROUPTRAY) {
                int groupId = (int)(wParam.ToInt32() - 0x1000);
                int mouseMsg = lParam.ToInt32();

                // Left click / double-click → launch group
                if (mouseMsg == 0x0202 || mouseMsg == 0x0203) {
                    if (_icons.TryGetValue(groupId, out var entry))
                        LaunchGroup(entry.groupName);
                }

                // Right-click → context menu
                if (mouseMsg == 0x0205) {
                    _menuActiveGroupId = groupId;
                    ShowContextMenu();
                }

                return IntPtr.Zero;
            }

            return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        // ── Context menu ──────────────────────────────────────────────────────

        private static void ShowContextMenu() {
            if (_hMenu != IntPtr.Zero) {
                NativeMethods.DestroyMenu(_hMenu);
                _hMenu = IntPtr.Zero;
            }

            _hMenu = NativeMethods.CreatePopupMenu();
            NativeMethods.AppendMenu(_hMenu, 0, 1, "Edit this Group");
            NativeMethods.AppendMenu(_hMenu, 0, 2, "Launch All");

            NativeMethods.GetCursorPos(out NativeMethods.POINT pt);
            NativeMethods.SetForegroundWindow(_hwnd);

            uint result = NativeMethods.TrackPopupMenu(
                _hMenu,
                NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_RIGHTBUTTON,
                pt.X, pt.Y, 0, _hwnd, IntPtr.Zero);

            NativeMethods.PostMessage(_hwnd, NativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);

            if (!_icons.TryGetValue(_menuActiveGroupId, out var e)) return;

            if (result == 1) EditGroup(e.groupName);
            if (result == 2) LaunchAll(e.groupName);
        }

        // ── Actions ───────────────────────────────────────────────────────────

        private static void LaunchGroup(string groupName) { 
            Launch($"\"{groupName}\"");
        }

        private static void EditGroup(string groupName) {
            try {
                int groupId = JsonConfigHelper.FindKeyByGroupName(groupName);
                Launch($"EditGroupWindow --id={groupId}");
            }
            catch (Exception ex) {
                Debug.WriteLine($"GroupTrayManager: EditGroup failed: {ex.Message}");
            }
        }

        private static void LaunchAll(string groupName) =>
            Launch($"LaunchAll --groupName=\"{groupName}\"");

        private static void Launch(string args) {
            try {
                string exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppGroup.exe");
                Process.Start(new ProcessStartInfo {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false
                });
            }
            catch (Exception ex) {
                Debug.WriteLine($"GroupTrayManager: launch failed '{args}': {ex.Message}");
            }
        }
    }
}