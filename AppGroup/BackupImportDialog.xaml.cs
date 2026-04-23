    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;

    namespace AppGroup {
        public class BackupGroupPreviewItem : INotifyPropertyChanged {
            public string GroupName { get; set; }
            public int ShortcutCount { get; set; }
            public string GroupIcon { get; set; }
            public List<string> PathIcons { get; set; } = new List<string>();      // ADD
            public int AdditionalIconsCount { get; set; }                          // ADD
            public string AdditionalIconsText =>                                   // ADD
                AdditionalIconsCount > 0 ? $"+{AdditionalIconsCount}" : string.Empty;

            private bool _isSelected = true;
            public bool IsSelected {
                get => _isSelected;
                set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
            }
            public event PropertyChangedEventHandler PropertyChanged;
        }

        public sealed partial class BackupImportDialog : ContentDialog {
            private readonly ObservableCollection<BackupGroupPreviewItem> _items;
            private bool _updatingSelectAll = false;
            private bool _importConfirmed = false;

            public bool ImportConfirmed => _importConfirmed;

            /// <param name="items">
            /// Pre-built preview items from the parsed backup JSON.
            /// GroupIcon can be null if no icon was found in the zip.
            /// </param>
            public BackupImportDialog(List<BackupGroupPreviewItem> items) {
                InitializeComponent();

                _items = new ObservableCollection<BackupGroupPreviewItem>(items);

                foreach (var item in _items) {
                    item.PropertyChanged += (s, e) => {
                        if (e.PropertyName == nameof(BackupGroupPreviewItem.IsSelected))
                            SyncSelectAllState();
                    };
                }

                GroupListView.ItemsSource = _items;
                CloseButton.Click += (s, e) => Hide();
                ImportButton.Click += (s, e) => { _importConfirmed = true; Hide(); };
            }

            /// <summary>Returns only the group names the user left checked.</summary>
            public HashSet<string> GetSelectedNames() =>
                _items.Where(i => i.IsSelected)
                      .Select(i => i.GroupName)
                      .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

            // ── SelectAll sync ────────────────────────────────────────────────
            private void SelectAllCheckBox_Checked(object sender, RoutedEventArgs e) {
                if (_updatingSelectAll || _items == null) return;
                foreach (var item in _items) item.IsSelected = true;
            }

            private void SelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e) {
                if (_updatingSelectAll || _items == null) return;
                foreach (var item in _items) item.IsSelected = false;
            }

            private void SyncSelectAllState() {
                _updatingSelectAll = true;
                int checkedCount = _items.Count(i => i.IsSelected);
                if (checkedCount == _items.Count)
                    SelectAllCheckBox.IsChecked = true;
                else if (checkedCount == 0)
                    SelectAllCheckBox.IsChecked = false;
                else
                    SelectAllCheckBox.IsChecked = null; // indeterminate
                _updatingSelectAll = false;
            }
        }
    }