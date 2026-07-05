using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup; // XamlParseException
using SihyuPOSPayroll.Services;
using SihyuPOSPayroll.ViewModels;

namespace SihyuPOSPayroll.Views.Admin.Payroll
{
    /// <summary>
    /// Interaction logic for PositionSalaryEdit.xaml
    /// </summary>
    public partial class PositionSalaryEdit : UserControl
    {
        public event Action? CloseRequested;

        /// <summary>Strongly-typed access to the view model.</summary>
        public PositionSalaryViewModel VM { get; }

        /// <summary>True if InitializeComponent failed (XAML parse/binding issue). Host may skip showing this control.</summary>
        public bool InitFailed { get; private set; }

        // Default ctor (safe: no DB work here)
        public PositionSalaryEdit() : this(service: null) { }

        /// <summary>DI-friendly ctor.</summary>
        public PositionSalaryEdit(PositionSalaryService? service)
        {
            try
            {
                InitializeComponent(); // if XAML has issues, catch below
            }
            catch (XamlParseException xpe)
            {
                var root = xpe.InnerException ?? xpe;
                MessageBox.Show(
                    $"UI load failed (XAML).\n\n{root.GetType().Name}: {root.Message}\n\n{root.StackTrace}",
                    "Positions & Salaries", MessageBoxButton.OK, MessageBoxImage.Error);
                InitFailed = true;
                return;
            }
            catch (Exception ex)
            {
                var root = ex.InnerException ?? ex;
                MessageBox.Show(
                    $"UI load failed.\n\n{root.GetType().Name}: {root.Message}\n\n{root.StackTrace}",
                    "Positions & Salaries", MessageBoxButton.OK, MessageBoxImage.Error);
                InitFailed = true;
                return;
            }

            VM = new PositionSalaryViewModel(service);
            VM.RequestClose += OnVmRequestClose;

            DataContext = VM;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>Overload if you want to supply an existing VM.</summary>
        public PositionSalaryEdit(PositionSalaryViewModel viewModel)
        {
            try
            {
                InitializeComponent();
            }
            catch (XamlParseException xpe)
            {
                var root = xpe.InnerException ?? xpe;
                MessageBox.Show(
                    $"UI load failed (XAML).\n\n{root.GetType().Name}: {root.Message}\n\n{root.StackTrace}",
                    "Positions & Salaries", MessageBoxButton.OK, MessageBoxImage.Error);
                InitFailed = true;
                return;
            }
            catch (Exception ex)
            {
                var root = ex.InnerException ?? ex;
                MessageBox.Show(
                    $"UI load failed.\n\n{root.GetType().Name}: {root.Message}\n\n{root.StackTrace}",
                    "Positions & Salaries", MessageBoxButton.OK, MessageBoxImage.Error);
                InitFailed = true;
                return;
            }

            VM = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            VM.RequestClose += OnVmRequestClose;

            DataContext = VM;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnVmRequestClose() => CloseRequested?.Invoke();

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (InitFailed) return;

            try
            {
                // Prefer focusing the grid if present; otherwise the control itself.
                if (FindName("RatesGrid") is DataGrid grid)
                {
                    Keyboard.Focus(grid);
                }
                else
                {
                    Keyboard.Focus(this);
                }

                // Load AFTER visual tree is ready so any errors are shown via dialogs
                VM.Load();
            }
            catch (Exception ex)
            {
                var root = ex.InnerException ?? ex;
                MessageBox.Show(
                    $"Failed to open Positions & Salaries.\n\n{root.GetType().Name}: {root.Message}\n\n{root.StackTrace}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            if (!InitFailed)
                VM.RequestClose -= OnVmRequestClose;

            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;
        }

        /// <summary>
        /// Actions column: ?? Edit button click handler.
        /// Requires the DataGrid in XAML to be named 'RatesGrid' and the button wired with Click="EditRow_Click".
        /// </summary>
        private void EditRow_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not PositionSalaryService.PositionSalary rowItem)
                return;

            VM.Selected = rowItem;

            if (FindName("RatesGrid") is not DataGrid grid) return;

            grid.UpdateLayout();
            grid.ScrollIntoView(rowItem);

            // Try to realize the row container, then focus first editable column (Position)
            var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(rowItem);
            if (row == null)
            {
                grid.UpdateLayout();
                grid.ScrollIntoView(rowItem);
                row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(rowItem);
            }

            if (row != null && grid.Columns.Count > 0)
            {
                grid.CurrentCell = new DataGridCellInfo(rowItem, grid.Columns[0]);
                grid.BeginEdit();
                var content = grid.Columns[0].GetCellContent(row) as FrameworkElement;
                content?.Focus();
            }
        }

        /// <summary>
        /// Actions column: ?? Delete button click handler.
        /// Requires the button wired with Click="DeleteRow_Click".
        /// </summary>
        private void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is PositionSalaryService.PositionSalary rowItem)
            {
                if (VM.RemoveCommand.CanExecute(rowItem))
                    VM.RemoveCommand.Execute(rowItem);
            }
        }

        /// <summary>
        /// Numeric filter used by the TextBox style in XAML for DailyRate.
        /// Allows digits and at most one '.' with up to 2 decimals.
        /// </summary>
        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var tb = sender as TextBox;
            var text = tb?.Text ?? string.Empty;
            var selectionStart = tb?.SelectionStart ?? 0;
            var selectionLength = tb?.SelectionLength ?? 0;

            var proposed = text.Remove(selectionStart, selectionLength)
                               .Insert(selectionStart, e.Text);

            e.Handled = !Regex.IsMatch(proposed, @"^\d*([.]\d{0,2})?$");
        }

        /// <summary>Allow ESC key to close the panel (host should handle CloseRequested).</summary>
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CloseRequested?.Invoke();
                return;
            }
            base.OnPreviewKeyDown(e);
        }
    }
}
