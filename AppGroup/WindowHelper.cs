using System;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;
using WinRT;
using System.Diagnostics;
using System.Drawing;
namespace AppGroup {
    public class WindowHelper {
        private readonly Window _window;
        private AppWindow _appWindow;
        private IntPtr _hWnd;
        private SystemBackdropConfiguration _configurationSource;
        private MicaBackdrop _micaBackdrop;
        private DesktopAcrylicController _acrylicController;
        private bool _micaEnabled;
        private bool _extendContent;
        private bool _canMaximize;
        private bool _centerWindow;
        private int _minWidth = 0;
        private int _minHeight = 0;

        public enum BackdropType {
            None,
            Mica,
            AcrylicBase,
            AcrylicThin
        }
        private BackdropType _currentBackdropType = BackdropType.None;

        public delegate int SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, uint dwRefData);

        [DllImport("Comctl32.dll", SetLastError = true)]
        public static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, uint uIdSubclass, uint dwRefData);

        [DllImport("Comctl32.dll", SetLastError = true)]
        public static extern int DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        private const int WM_GETMINMAXINFO = 0x0024;

        private struct MINMAXINFO {
            public System.Drawing.Point ptReserved;
            public System.Drawing.Point ptMaxSize;
            public System.Drawing.Point ptMaxPosition;
            public System.Drawing.Point ptMinTrackSize;
            public System.Drawing.Point ptMaxTrackSize;
        }
   




        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

        private readonly SUBCLASSPROC _subClassDelegate;

        public WindowHelper(Window window) {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _subClassDelegate = new SUBCLASSPROC(WindowSubClass);
            InitializeWindow();
        }

        public BackdropType CurrentBackdropType => _currentBackdropType;

        public bool IsMaximizable {
            get => _appWindow.Presenter is OverlappedPresenter presenter && presenter.IsMaximizable;
            set {
                if (_appWindow.Presenter is OverlappedPresenter presenter) {
                    presenter.IsMaximizable = value;
                }
            }
        }
        public bool IsAlwaysOnTop {
            get => _appWindow.Presenter is OverlappedPresenter presenter && presenter.IsMaximizable;
            set {
                if (_appWindow.Presenter is OverlappedPresenter presenter) {
                    presenter.IsAlwaysOnTop = value;
                }
            }
        }
        public bool IsResizable {
            get => _appWindow.Presenter is OverlappedPresenter presenter && presenter.IsResizable;
            set {
                if (_appWindow.Presenter is OverlappedPresenter presenter) {
                    presenter.IsResizable = value;
                }
            }
        }

        public bool HasBorder {
            get => _appWindow.Presenter is OverlappedPresenter presenter && presenter.HasBorder;
            set {
                if (_appWindow.Presenter is OverlappedPresenter presenter) {
                    presenter.SetBorderAndTitleBar(value, HasTitleBar);
                }
            }
        }

        public bool HasTitleBar {
            get => _appWindow.Presenter is OverlappedPresenter presenter && presenter.HasTitleBar;
            set {
                if (_appWindow.Presenter is OverlappedPresenter presenter) {
                    presenter.SetBorderAndTitleBar(HasBorder, value);
                }
            }
        }

        public bool IsMinimizable {
            get => _appWindow.Presenter is OverlappedPresenter presenter && presenter.IsMinimizable;
            set {
                if (_appWindow.Presenter is OverlappedPresenter presenter) {
                    presenter.IsMinimizable = value;
                }
            }
        }

        public AppWindow AppWindow => _appWindow;

        public IntPtr WindowHandle => _hWnd;

     

        public bool CenterWindow {
            get => _centerWindow;
            set {
                _centerWindow = value;
                CenterOnScreen();
            }
        }

        public (int Width, int Height) MinimumSize {
            get => (_minWidth, _minHeight);
            set {
                _minWidth = value.Width;
                _minHeight = value.Height;
            }
        }

        public (int Width, int Height) WindowSize {
            get => (_appWindow.Size.Width, _appWindow.Size.Height);
            set => SetSize(value.Width, value.Height);
        }

        private void InitializeWindow() {
            _hWnd = WindowNative.GetWindowHandle(_window);
            var windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            SetWindowSubclass(_hWnd, _subClassDelegate, 0, 0);


            if (_window.Content is FrameworkElement root) {
                root.ActualThemeChanged += (sender, args) => {
                    UpdateTheme(root.ActualTheme);
                };
            }
        }

        private void UpdateTheme(ElementTheme newTheme) {
            if (_configurationSource != null) {
                _configurationSource.Theme = newTheme == ElementTheme.Dark
                    ? SystemBackdropTheme.Dark
                    : SystemBackdropTheme.Light;
            }

            //TrySetMicaBackdrop();
            UpdateTitleBarColors();
        }


        public static float GetDpiScaleForMonitor(IntPtr hMonitor) {
            try {
                if (Environment.OSVersion.Version.Major > 6 ||
                    (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor >= 3)) {

                    uint dpiX, dpiY;

                    // Try to get DPI for the monitor
                    if (NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out dpiX, out dpiY) == 0) {
                        return dpiX / 96.0f;
                    }
                }

                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) {
                    return g.DpiX / 96.0f;
                }
            }
            catch {
                return 1.0f;
            }
        }
     
        private void RefreshThemeResources() {
            if (_window.Content is FrameworkElement root) {
                root.Resources.MergedDictionaries.Clear();
                root.RequestedTheme = root.ActualTheme;
            }
        }

        public void SetSize(int width, int height) {
            _appWindow.Resize(new SizeInt32(width, height));
        }

     
        public BackdropType SetSystemBackdrop(BackdropType backdropType) {
            CleanupSystemBackdrop();

            switch (backdropType) {
                case BackdropType.Mica:
                    return TrySetMicaBackdrop();
                case BackdropType.AcrylicBase:
                    return TrySetAcrylicBackdrop(false);
                case BackdropType.AcrylicThin:
                    return TrySetAcrylicBackdrop(true);
                default:
                    _window.SystemBackdrop = null;
                    _currentBackdropType = BackdropType.None;
                    return BackdropType.None;
            }
        }

        private BackdropType TrySetMicaBackdrop() {
            if (!MicaController.IsSupported() || !_micaEnabled) {
                _window.SystemBackdrop = null;
                return BackdropType.None;
            }

            if (_micaBackdrop == null) {
                _micaBackdrop = new MicaBackdrop();
            }

            if (_configurationSource == null) {
                _configurationSource = new SystemBackdropConfiguration();
            }

            _configurationSource.Theme = (_window.Content as FrameworkElement)?.ActualTheme == ElementTheme.Dark
                ? SystemBackdropTheme.Dark
                : SystemBackdropTheme.Light;

            _window.SystemBackdrop = _micaBackdrop;
            _currentBackdropType = BackdropType.Mica;
            return BackdropType.Mica;
        }

        private BackdropType TrySetAcrylicBackdrop(bool useThin) {
            if (!DesktopAcrylicController.IsSupported()) {
                _window.SystemBackdrop = null;
                _currentBackdropType = BackdropType.None;
                return BackdropType.None;
            }

            var dispatcherQueueHelper = new WindowsSystemDispatcherQueueHelper();
            dispatcherQueueHelper.EnsureWindowsSystemDispatcherQueueController();

            if (_configurationSource == null) {
                _configurationSource = new SystemBackdropConfiguration();
            }

            _configurationSource.Theme = (_window.Content as FrameworkElement)?.ActualTheme == ElementTheme.Dark
                ? SystemBackdropTheme.Dark
                : SystemBackdropTheme.Light;

            _acrylicController = new DesktopAcrylicController();
            _acrylicController.Kind = useThin
                ? DesktopAcrylicKind.Thin
                : DesktopAcrylicKind.Base;

            _acrylicController.AddSystemBackdropTarget(
                _window.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>()
            );
            _acrylicController.SetSystemBackdropConfiguration(_configurationSource);

            _currentBackdropType = useThin ? BackdropType.AcrylicThin : BackdropType.AcrylicBase;
            return _currentBackdropType;
        }

        private void CleanupSystemBackdrop() {
            if (_acrylicController != null) {
                _acrylicController.Dispose();
                _acrylicController = null;
            }

            _window.SystemBackdrop = null;
        }

        private void CenterOnScreen() {
            var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            var centerX = (displayArea.WorkArea.Width - _appWindow.Size.Width) / 2;
            var centerY = (displayArea.WorkArea.Height - _appWindow.Size.Height) / 2;
            _appWindow.Move(new PointInt32(centerX, centerY));
        }

        private void ExtendContentIntoTitleBar() {
            if (_appWindow == null) return;

            var titleBar = _appWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = true;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            _window.SetTitleBar(null);
        }

        private void UpdateTitleBarColors() {
            var titleBar = _appWindow.TitleBar;
            var isDarkMode = (_window.Content as FrameworkElement)?.ActualTheme == ElementTheme.Dark;

            titleBar.ButtonForegroundColor = isDarkMode ? Colors.White : Colors.Black;
        }

        private int WindowSubClass(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, uint dwRefData) {
            if (uMsg == WM_GETMINMAXINFO) {
                MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));
                mmi.ptMinTrackSize.X = _minWidth;
                mmi.ptMinTrackSize.Y = _minHeight;

                Marshal.StructureToPtr(mmi, lParam, false);
                return 0;
            }
            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        private class WindowsSystemDispatcherQueueHelper {
            [DllImport("CoreMessaging.dll")]
            private static extern int CreateDispatcherQueueController(
                DispatcherQueueOptions options,
                out IntPtr dispatcherQueueController
            );

            private IntPtr m_dispatcherQueueController = IntPtr.Zero;

            public void EnsureWindowsSystemDispatcherQueueController() {
                if (m_dispatcherQueueController == IntPtr.Zero) {
                    DispatcherQueueOptions options;
                    options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
                    options.threadType = 2;    
                    options.apartmentType = 0;

                    CreateDispatcherQueueController(options, out m_dispatcherQueueController);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DispatcherQueueOptions {
            public int dwSize;
            public int threadType;
            public int apartmentType;
        }
    }
}