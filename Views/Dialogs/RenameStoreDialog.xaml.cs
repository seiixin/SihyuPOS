using System.Windows;
using System.Windows.Input;

namespace SihyuPOSPayroll.Views.Dialogs
{
    public partial class RenameStoreDialog : Window
    {
        /// <summary>The trimmed new store name entered by the user. Only valid when DialogResult == true.</summary>
        public string NewName { get; private set; } = string.Empty;

        public RenameStoreDialog(string currentName)
        {
            InitializeComponent();
            NameBox.Text = currentName ?? string.Empty;
            Loaded += (_, _) =>
            {
                NameBox.Focus();
                NameBox.SelectAll();
            };
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => DragMove();

        private void NameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)  { e.Handled = true; TrySave(); }
            if (e.Key == Key.Escape) { e.Handled = true; DialogResult = false; }
        }

        private void Save_Click(object sender, RoutedEventArgs e) => TrySave();

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void TrySave()
        {
            var name = NameBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                NameBox.Focus();
                return;
            }
            NewName = name;
            DialogResult = true;
        }
    }
}
