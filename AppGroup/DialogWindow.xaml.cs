using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using Windows.Graphics;

namespace AppGroup {
    public sealed partial class DialogWindow : Window {
        private TaskCompletionSource<bool> _tcs = new();
        private WindowHelper _windowHelper;

        public DialogWindow(string title, string message) {
            InitializeComponent();
            this.Title = title;

            _windowHelper = new WindowHelper(this);

            _windowHelper.SetSystemBackdrop(WindowHelper.BackdropType.AcrylicBase);
            _windowHelper.IsMaximizable = false;
            _windowHelper.IsMinimizable = false;
            _windowHelper.IsResizable = true;
            
            _windowHelper.HasBorder = true;
            _windowHelper.HasTitleBar = true;
            _windowHelper.IsAlwaysOnTop = true;

            TitleText.Text = title;
            MessageText.Text = message;

            SetupWindow();
        }

        private void SetupWindow() {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            // Remove default title bar
            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            appWindow.TitleBar.ButtonHoverBackgroundColor = Colors.Transparent;

            // Size
            appWindow.Resize(new SizeInt32(420, 220));

            // Center on screen
            var area = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
            appWindow.Move(new PointInt32(
                (area.WorkArea.Width - 420) / 2,
                (area.WorkArea.Height - 220) / 2
            ));
        }

        public Task<bool> ShowDialogAsync() {
            Activate();
            return _tcs.Task;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e) {
            _tcs.TrySetResult(true);
            Close();
        }
    }
}