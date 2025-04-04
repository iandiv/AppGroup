using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace AppGroup {
    /// <summary>
    /// Helper class to handle "Support Us" dialogs that show after multiple uses of the app
    /// </summary>
    public class SupportDialogHelper {
        private const string USAGE_COUNT_FILENAME = "usage_count.dat";
        private const int DEFAULT_DIALOG_THRESHOLD =3;

        private readonly int _dialogThreshold;
        private readonly Window _ownerWindow;
        private readonly string _donationUrl;
        private bool _checkPerformed = false;

            public SupportDialogHelper(Window ownerWindow, int dialogThreshold = DEFAULT_DIALOG_THRESHOLD, string donationUrl = "https://ko-fi.com/iandiv/tip") {
            _ownerWindow = ownerWindow ?? throw new ArgumentNullException(nameof(ownerWindow));
            _dialogThreshold = dialogThreshold > 0 ? dialogThreshold : DEFAULT_DIALOG_THRESHOLD;
            _donationUrl = donationUrl;

            ScheduleUsageCheck();
        }
  public void ScheduleUsageCheck() {
            if (_checkPerformed) return;

            DispatcherTimer startupTimer = new DispatcherTimer();
            startupTimer.Interval = TimeSpan.FromMilliseconds(1000);
            startupTimer.Tick += (s, e) => {
                startupTimer.Stop();
                CheckUsageAndShowDialog();
                _checkPerformed = true;
            };
            startupTimer.Start();
        }

       
        public void CheckUsageAndShowDialog() {
            try {
                int usageCount = GetCurrentUsageCount();
                usageCount++;
                SaveUsageCount(usageCount);

                Debug.WriteLine($"App usage count: {usageCount}");

                if (usageCount % _dialogThreshold == 0) {
                    _ownerWindow.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () => {
                        await ShowSupportDialogAsync();
                    });
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error in CheckUsageAndShowDialog: {ex.Message}");
            }
        }

       
        private int GetCurrentUsageCount() {
            string filePath = GetUsageFilePath();

            try {
                if (File.Exists(filePath)) {
                    string countText = File.ReadAllText(filePath).Trim();
                    if (int.TryParse(countText, out int count)) {
                        return count;
                    }
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error reading usage count: {ex.Message}");
            }

            return 0; 
        }

     
        private void SaveUsageCount(int count) {
            string filePath = GetUsageFilePath();

            try {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, count.ToString());
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error saving usage count: {ex.Message}");
            }
        }

        private string GetUsageFilePath() {
            string appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AppGroup"
            );

            return Path.Combine(appDataFolder, USAGE_COUNT_FILENAME);
        }

      
        private async Task ShowSupportDialogAsync() {
            try {
                if (_ownerWindow == null || _ownerWindow.Content == null || _ownerWindow.Content.XamlRoot == null) {
                    Debug.WriteLine("Cannot show dialog: Window or XamlRoot is not available");
                    return;
                }

                ContentDialog supportDialog = new ContentDialog {
                    Title = "❤️ Support Us ",
                    Content = new TextBlock {
                        Text = "Thanks for using AppGroup!\nIf you find it useful, your support is greatly appreciated.",
                        TextWrapping = TextWrapping.Wrap
                    },

                    SecondaryButtonText = "Support Us",
                    PrimaryButtonText = "Later",
                    DefaultButton = ContentDialogButton.Secondary,
                    XamlRoot = _ownerWindow.Content.XamlRoot
                };

                var result = await supportDialog.ShowAsync();

                if (result == ContentDialogResult.Secondary) {
                    var uri = new Uri(_donationUrl);
                    await Windows.System.Launcher.LaunchUriAsync(uri);
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error showing support dialog: {ex.Message}");
            }
        }

       
        public void ResetUsageCount() {
            SaveUsageCount(0);
        }
    }
}