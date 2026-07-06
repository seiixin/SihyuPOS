using System;
using System.Windows;
using System.Windows.Controls;
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;

namespace SihyuPOSPayroll.Views.Admin.Categories
{
    public partial class CategoriesView : UserControl
    {
        private int _editingId = 0; // 0 = new

        public CategoriesView()
        {
            InitializeComponent();
            Loaded += (_, _) => Refresh();
        }

        private void Refresh()
        {
            try
            {
                CategoriesGrid.ItemsSource = CategoryService.GetAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load categories.\n{ex.Message}", "Categories",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Add ───────────────────────────────────────────────────────────────
        private void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            _editingId          = 0;
            EditorTitle.Text    = "Add Category";
            CategoryNameBox.Text = string.Empty;
            EditorPanel.Visibility = Visibility.Visible;
            CategoryNameBox.Focus();
        }

        // ── Edit ──────────────────────────────────────────────────────────────
        private void EditCategory_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not CategoryModel cat) return;
            _editingId           = cat.Id;
            EditorTitle.Text     = "Edit Category";
            CategoryNameBox.Text = cat.Name;
            EditorPanel.Visibility = Visibility.Visible;
            CategoryNameBox.Focus();
        }

        // ── Save ──────────────────────────────────────────────────────────────
        private void SaveEditor_Click(object sender, RoutedEventArgs e)
        {
            var name = CategoryNameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Category name is required.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                CategoryNameBox.Focus();
                return;
            }

            try
            {
                if (_editingId == 0)
                    CategoryService.Add(name);
                else
                    CategoryService.Update(_editingId, name);

                EditorPanel.Visibility = Visibility.Collapsed;
                Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save category.\n{ex.Message}", "Categories",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Cancel ────────────────────────────────────────────────────────────
        private void CancelEditor_Click(object sender, RoutedEventArgs e)
            => EditorPanel.Visibility = Visibility.Collapsed;

        // ── Delete ────────────────────────────────────────────────────────────
        private void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not CategoryModel cat) return;

            var confirm = MessageBox.Show(
                $"Delete category \"{cat.Name}\"?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                CategoryService.Delete(cat.Id);
                Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete category.\n{ex.Message}", "Categories",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
