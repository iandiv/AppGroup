using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace AppGroup {
    public class SettingsHelper {
        private const string STARTUP_REGISTRY_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "AppGroup";
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AppGroup",
            "settings.json"
        );

        public class AppSettings {
            public bool ShowSystemTrayIcon { get; set; } = true;
            public bool RunAtStartup { get; set; } = true;
            public bool UseGrayscaleIcon { get; set; } = false;
        }

        private static AppSettings _currentSettings;

        public static async Task<AppSettings> LoadSettingsAsync() {
            try {
                if (File.Exists(SettingsPath)) {
                    string jsonContent = await File.ReadAllTextAsync(SettingsPath);
                    _currentSettings = JsonSerializer.Deserialize<AppSettings>(jsonContent) ?? new AppSettings();
                }
                else {
                    _currentSettings = new AppSettings();
                    await SaveSettingsAsync(_currentSettings);
                }

                // Apply startup setting if this is the first time loading
                await EnsureStartupSettingIsApplied();
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                _currentSettings = new AppSettings();
                // Even if loading failed, try to apply default startup setting
                await EnsureStartupSettingIsApplied();
            }

            return _currentSettings;
        }

        /// <summary>
        /// Ensures that the startup registry setting matches the configured setting
        /// </summary>
        private static async Task EnsureStartupSettingIsApplied() {
            try {
                bool isInRegistry = IsInStartupRegistry();

                if (_currentSettings.RunAtStartup && !isInRegistry) {
                    // Setting says run at startup but it's not in registry - add it
                    AddToStartup();
                    System.Diagnostics.Debug.WriteLine("Applied default startup setting: Added to startup");
                }
                else if (!_currentSettings.RunAtStartup && isInRegistry) {
                    // Setting says don't run at startup but it's in registry - remove it
                    RemoveFromStartup();
                    System.Diagnostics.Debug.WriteLine("Applied startup setting: Removed from startup");
                }
                // If they match, no action needed
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error ensuring startup setting is applied: {ex.Message}");
            }
        }

        public static void AddToStartup() {
            try {
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                string startupCommand = $"\"{exePath}\" --silent";

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(STARTUP_REGISTRY_KEY, true)) {
                    if (key != null) {
                        key.SetValue(APP_NAME, startupCommand, RegistryValueKind.String);
                        System.Diagnostics.Debug.WriteLine($"Added to startup: {startupCommand}");
                    }
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error adding to startup: {ex.Message}");
                throw;
            }
        }

        public static void RemoveFromStartup() {
            try {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(STARTUP_REGISTRY_KEY, true)) {
                    if (key != null) {
                        if (key.GetValue(APP_NAME) != null) {
                            key.DeleteValue(APP_NAME, false);
                            System.Diagnostics.Debug.WriteLine("Removed from startup");
                        }
                    }
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error removing from startup: {ex.Message}");
                throw;
            }
        }

        public static bool IsInStartupRegistry() {
            try {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(STARTUP_REGISTRY_KEY, false)) {
                    if (key != null) {
                        object value = key.GetValue(APP_NAME);
                        return value != null;
                    }
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error checking startup registry: {ex.Message}");
            }
            return false;
        }

        public static async Task SaveSettingsAsync(AppSettings settings) {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));

                var options = new JsonSerializerOptions {
                    WriteIndented = true
                };

                string jsonContent = JsonSerializer.Serialize(settings, options);
                await File.WriteAllTextAsync(SettingsPath, jsonContent);

                _currentSettings = settings;
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        public static AppSettings GetCurrentSettings() {
            return _currentSettings ?? new AppSettings();
        }
    }
}