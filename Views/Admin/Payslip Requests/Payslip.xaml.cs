using SihyuPOSPayroll.ViewModels;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SihyuPOSPayroll.Views.Admin.Payslip_Requests
{
    public partial class Payslip : UserControl
    {
        private AdminPayslipViewModel viewModel;

        public Payslip()
        {
            InitializeComponent();
            viewModel = new AdminPayslipViewModel();
            this.DataContext = viewModel;
            Loaded += Payslip_Loaded;
        }

        private void Payslip_Loaded(object sender, RoutedEventArgs e)
        {
            viewModel.LoadPayslipRequestsFromDatabase();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (viewModel != null && sender is TextBox tb)
            {
                viewModel.FilterPayslipRequests(tb.Text);
            }
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Handle selection changes if needed
        }
    }

    // Simple converter for string to visibility
    public class StringToVisibilityConverter : IValueConverter
    {
        public static readonly StringToVisibilityConverter Instance = new StringToVisibilityConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str && !string.IsNullOrEmpty(str))
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}