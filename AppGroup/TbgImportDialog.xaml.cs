using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace AppGroup {
    public class TbgGroupPreviewItem : INotifyPropertyChanged {
        public TbgGroupPreview Preview { get; set; }
        public string GroupName => Preview.GroupName;
        public int ShortcutCount => Preview.ShortcutCount;
        public int GroupCol => Preview.GroupCol;
        public List<string> PathIcons { get; set; } = new List<string>();
        public int AdditionalIconsCount { get; set; }
        public string AdditionalIconsText => AdditionalIconsCount > 0 ? $"+{AdditionalIconsCount}" : string.Empty;

        private bool _isSelected = true;
        public bool IsSelected {
            get => _isSelected;
            set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public string GroupIcon { get; set; }

    }

    public sealed partial class TbgImportDialog : ContentDialog {
        private readonly ObservableCollection<TbgGroupPreviewItem> _items;
        private bool _updatingSelectAll = false;
        private bool _importConfirmed = false;
        public bool ImportConfirmed => _importConfirmed;
        public TbgImportDialog(List<TbgGroupPreview> previews) {
            InitializeComponent();
            _items = new ObservableCollection<TbgGroupPreviewItem>(
                previews.Select(p => {
                    var item = new TbgGroupPreviewItem {
                        Preview = p,
                        PathIcons = p.PathIcons,
                        AdditionalIconsCount = p.AdditionalIconsCount,
                        GroupIcon = p.GroupIconPath
                    };
                    item.PropertyChanged += (s, e) => {
                        if (e.PropertyName == nameof(TbgGroupPreviewItem.IsSelected))
                            SyncSelectAllState();
                    };
                    return item;
                }));
            GroupListView.ItemsSource = _items;

            CloseButton.Click += (s, e) => Hide();
            ImportButton.Click += (s, e) => { _importConfirmed = true; Hide(); };
        }

        public List<TbgGroupPreview> GetSelected() =>
            _items.Where(i => i.IsSelected).Select(i => i.Preview).ToList();

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