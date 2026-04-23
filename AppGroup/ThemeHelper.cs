using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;


namespace AppGroup {

    public static class ThemeHelper {
        public static void UpdateTitleBarColors(Window window) {
            if (window.Content is FrameworkElement root) {
                root.ActualThemeChanged += (sender, args) => {
                    ApplyTitleBarColors(window);
                };
                ApplyTitleBarColors(window);
            }
        }

        public static void ApplyTitleBarColors(Window window) {
            var titleBar = window.AppWindow.TitleBar;
            var isDark = (window.Content as FrameworkElement)?.ActualTheme == ElementTheme.Dark;

            titleBar.ButtonForegroundColor = isDark ? Colors.White : Colors.Black;
            titleBar.ButtonHoverForegroundColor = isDark ? Colors.White : Colors.Black;
            titleBar.ButtonHoverBackgroundColor = isDark
                ? Windows.UI.Color.FromArgb(30, 255, 255, 255)
                : Windows.UI.Color.FromArgb(30, 0, 0, 0);
            titleBar.ButtonPressedForegroundColor = isDark ? Colors.White : Colors.Black;
            titleBar.ButtonPressedBackgroundColor = isDark
                ? Windows.UI.Color.FromArgb(50, 255, 255, 255)
                : Windows.UI.Color.FromArgb(50, 0, 0, 0);
        }
    }
}

