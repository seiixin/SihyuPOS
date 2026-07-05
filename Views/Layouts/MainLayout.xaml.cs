using System.Windows;
using System.Windows.Controls;

namespace SihyuPOSPayroll.Views.Layouts
{
    /// <summary>
    /// Interaction logic for MainLayout.xaml
    /// </summary>
    public partial class MainLayout : UserControl
    {
        // Tracks the sidebar visibility state
        private bool isSidebarVisible = true;

        /// <summary>
        /// Initializes the MainLayout component.
        /// </summary>
        public MainLayout()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Handles the sidebar toggle button click.
        /// Shows or hides the sidebar and adjusts the column width.
        /// </summary>
        private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            // Toggle the visibility state
            isSidebarVisible = !isSidebarVisible;

            // Ensure SidebarControl and SidebarColumn exist before attempting changes
            if (SidebarControl != null && SidebarColumn != null)
            {
                SidebarControl.Visibility = isSidebarVisible ? Visibility.Visible : Visibility.Collapsed;
                SidebarColumn.Width = isSidebarVisible ? new GridLength(250) : new GridLength(0);
            }
        }
    }
}
