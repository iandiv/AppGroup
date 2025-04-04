using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;


namespace AppGroup {

    public static class ThemeHelper {
        public static void UpdateTitleBarColors(Window window) {
            if (window.Content is FrameworkElement root) {
                root.ActualThemeChanged += (sender, args) => {
                    var titleBar = window.AppWindow.TitleBar;
                    var isDarkMode = (window.Content as FrameworkElement)?.ActualTheme == ElementTheme.Dark;

                    titleBar.ButtonForegroundColor = isDarkMode ? Colors.White : Colors.Black;
                };

                var initialIsDarkMode = (window.Content as FrameworkElement)?.ActualTheme == ElementTheme.Dark;
                var initialTitleBar = window.AppWindow.TitleBar;
                initialTitleBar.ButtonForegroundColor = initialIsDarkMode ? Colors.White : Colors.Black;
            }
        }
    }
}

