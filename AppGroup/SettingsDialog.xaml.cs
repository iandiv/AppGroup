using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Dispatching;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace AppGroup {
    public sealed partial class SettingsDialog : ContentDialog {
        private SettingsHelper.AppSettings _settings;
        private Button _checkUpdateButton;
        private readonly DispatcherQueue _dispatcherQueue;
        private bool _isLoading = true;

        public SettingsDialog() {
            this.InitializeComponent();

            // Get the dispatcher queue for UI thread operations
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            this.Loaded += SettingsDialog_Loaded;

          
        }

        private async void SettingsDialog_Loaded(object sender, RoutedEventArgs e) {
            try {
                await LoadCurrentSettingsAsync();

                // Wire up toggle events after loading to prevent firing during init
                SystemTrayToggle.Toggled += SystemTrayToggle_Toggled;
                StartupToggle.Toggled += StartupToggle_Toggled;
                GrayscaleIconToggle.Toggled += GrayScaleToggle_Toggled;

                _isLoading = false;
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error in SettingsDialog_Loaded: {ex.Message}");
                _isLoading = false;
            }
        }

      

    
        private async Task LoadCurrentSettingsAsync() {
            try {
                _settings = await SettingsHelper.LoadSettingsAsync();

                // Update UI with current settings
                SystemTrayToggle.IsOn = _settings.ShowSystemTrayIcon;
                StartupToggle.IsOn = _settings.RunAtStartup;
                GrayscaleIconToggle.IsOn = _settings.UseGrayscaleIcon;

                // Verify startup setting matches registry
                bool isInRegistry = SettingsHelper.IsInStartupRegistry();
                if (_settings.RunAtStartup != isInRegistry) {
                    // Sync the setting with actual registry state
                    _settings.RunAtStartup = isInRegistry;
                    StartupToggle.IsOn = isInRegistry;
                    await SettingsHelper.SaveSettingsAsync(_settings);
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error loading settings in dialog: {ex.Message}");
                // Fallback to defaults
                _settings = new SettingsHelper.AppSettings();
                SystemTrayToggle.IsOn = true;
                StartupToggle.IsOn = true;
                GrayscaleIconToggle.IsOn = false;
            }
        }

        private async void SystemTrayToggle_Toggled(object sender, RoutedEventArgs e) {
            if (_isLoading) return;

            try {
                await SaveSettingsAsync();
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error saving system tray settings: {ex.Message}");
                // Revert the toggle if saving failed
                _isLoading = true;
                SystemTrayToggle.IsOn = !SystemTrayToggle.IsOn;
                _isLoading = false;
            }
        }

        private async void StartupToggle_Toggled(object sender, RoutedEventArgs e) {
            if (_isLoading) return;

            try {
                await SaveSettingsAsync();
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error saving startup settings: {ex.Message}");
                // Revert the toggle if saving failed
                _isLoading = true;
                StartupToggle.IsOn = !StartupToggle.IsOn;
                _isLoading = false;
            }
        }

        private async void GrayScaleToggle_Toggled(object sender, RoutedEventArgs e) {
            if (_isLoading) return;

            try {
                await SaveSettingsAsync();
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error saving grayscale settings: {ex.Message}");
                // Revert the toggle if saving failed
                _isLoading = true;
                GrayscaleIconToggle.IsOn = !GrayscaleIconToggle.IsOn;
                _isLoading = false;
            }
        }

        private async Task SaveSettingsAsync() {
            try {
                if (_settings == null) {
                    _settings = new SettingsHelper.AppSettings();
                }

                // Update settings from UI
                _settings.ShowSystemTrayIcon = SystemTrayToggle.IsOn;
                _settings.RunAtStartup = StartupToggle.IsOn;
                _settings.UseGrayscaleIcon = GrayscaleIconToggle.IsOn;

                // Save to file
                await SettingsHelper.SaveSettingsAsync(_settings);

                // Apply settings immediately (but safely)
                await Task.Run(() => {
                    try {
                        ApplySystemTraySettings();
                        ApplyStartupSettings();
                    }
                    catch (Exception ex) {
                        Debug.WriteLine($"Error applying settings: {ex.Message}");
                    }
                });
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error saving settings: {ex.Message}");
                throw; // Re-throw to let the caller handle it
            }
        }

        private void CloseDialog(object sender, RoutedEventArgs e) {
            try {
                this.Hide();
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error closing dialog: {ex.Message}");
                // Try alternative approach if Hide() fails
                try {
                    if (this.XamlRoot?.Content is FrameworkElement rootElement) {
                        // Remove from visual tree if possible
                    }
                }
                catch (Exception ex2) {
                    Debug.WriteLine($"Error in alternative close method: {ex2.Message}");
                }
            }
        }

        private void ApplySystemTraySettings() {
            try {
                if (_settings.ShowSystemTrayIcon) {
                    // Initialize/show system tray if it's not already shown
                    if (App.Current is App app) {
                        app.ShowSystemTray();
                    }
                }
                else {
                    // Hide system tray
                    if (App.Current is App app) {
                        app.HideSystemTray();
                    }
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error applying system tray settings: {ex.Message}");
            }
        }

        private void ApplyStartupSettings() {
            try {
                if (_settings.RunAtStartup) {
                    SettingsHelper.AddToStartup();
                }
                else {
                    SettingsHelper.RemoveFromStartup();
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error applying startup settings: {ex.Message}");
            }
        }
    }
}