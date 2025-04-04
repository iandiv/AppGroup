using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AppGroup {

    public partial class App : Application {

        public App() {
            this.InitializeComponent();

        }
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args) {
            string[] cmdArgs = Environment.GetCommandLineArgs();

            if (cmdArgs.Length > 1) {
                string groupName = cmdArgs[1];
                Debug.WriteLine($"Launching with group filter: {groupName}");

                m_window = new PopupWindow(groupName);
                m_window.Activate();

            }
            else {
                m_window = new MainWindow();
                m_window.Activate();
            }

        }

        private Window? m_window;
    }
}
