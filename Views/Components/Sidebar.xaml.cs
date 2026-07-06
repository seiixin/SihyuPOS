using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SihyuPOSPayroll.ViewModels;

namespace SihyuPOSPayroll.Views.Components
{
    public partial class Sidebar : UserControl
    {
        public Sidebar()
        {
            InitializeComponent();
        }

        // Fired when a ListBoxItem inside a MenuGroup is clicked.
        // Clears selection on all OTHER ListBoxes so only one item is highlighted globally.
        private void MenuItem_Clicked(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item && item.DataContext is SidebarMenuItem menuItem)
            {
                // Find the parent ListBox of the clicked item
                var clickedListBox = FindParent<ListBox>(item);

                // Deselect every ListBox in the sidebar except the one that was just clicked
                ClearOtherListBoxes(this, clickedListBox);

                // Execute navigation
                menuItem.Command?.Execute(null);

                // Sync SelectedMenuItem so the VM knows what's active
                if (DataContext is SidebarViewModel vm)
                    vm.SelectedMenuItem = menuItem.Label;
            }
        }

        // Walk the visual tree and clear selection on all ListBoxes except the excluded one
        private static void ClearOtherListBoxes(DependencyObject parent, ListBox? exclude)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ListBox lb && lb != exclude)
                {
                    lb.SelectedItem = null;
                }
                ClearOtherListBoxes(child, exclude);
            }
        }

        // Find the nearest ancestor of type T in the visual tree
        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T typed) return typed;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        // Smooth wheel scroll while keeping the scrollbar hidden
        private void SidebarScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (SidebarScroll != null)
            {
                SidebarScroll.ScrollToVerticalOffset(SidebarScroll.VerticalOffset - e.Delta);
                e.Handled = true;
            }
        }
    }
}
