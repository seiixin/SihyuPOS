using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using SihyuPOSPayroll.Services;
using SihyuPOSPayroll.ViewModels;
using SihyuPOSPayroll.Models;

namespace SihyuPOSPayroll.Views.Admin.Attendance
{
    public partial class WorkScheduleEdit : UserControl
    {
        public event Action? CloseRequested;
        public WorkScheduleViewModel VM { get; }
        public bool InitFailed { get; private set; }

        public WorkScheduleEdit() : this(service: null) { }

        public WorkScheduleEdit(WorkScheduleService? service)
        {
            try { InitializeComponent(); }
            catch (XamlParseException xpe)
            {
                var root = xpe.InnerException ?? xpe;
                MessageBox.Show($"UI load failed (XAML).\n\n{root.GetType().Name}: {root.Message}\n\n{root.StackTrace}",
                    "Work Schedules", MessageBoxButton.OK, MessageBoxImage.Error);
                InitFailed = true; return;
            }
            catch (Exception ex)
            {
                var root = ex.InnerException ?? ex;
                MessageBox.Show($"UI load failed.\n\n{root.GetType().Name}: {root.Message}\n\n{root.StackTrace}",
                    "Work Schedules", MessageBoxButton.OK, MessageBoxImage.Error);
                InitFailed = true; return;
            }

            VM = new WorkScheduleViewModel(service);
            VM.RequestClose += OnVmRequestClose;
            DataContext = VM;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public WorkScheduleEdit(WorkScheduleViewModel viewModel)
        {
            try { InitializeComponent(); }
            catch (Exception ex)
            {
                var root = ex.InnerException ?? ex;
                MessageBox.Show($"UI load failed.\n\n{root.GetType().Name}: {root.Message}\n\n{root.StackTrace}",
                    "Work Schedules", MessageBoxButton.OK, MessageBoxImage.Error);
                InitFailed = true; return;
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
            try { Keyboard.Focus(this); VM.Load(); }
            catch (Exception ex)
            {
                var root = ex.InnerException ?? ex;
                MessageBox.Show($"Failed to open Work Schedules.\n\n{root.GetType().Name}: {root.Message}\n\n{root.StackTrace}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            if (!InitFailed) VM.RequestClose -= OnVmRequestClose;
            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;
        }

        private void EditRow_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not WorkScheduleModel rowItem) return;
            VM.Selected = rowItem;
            if (this.FindName("SchedulesGrid") is not DataGrid grid) return;

            grid.UpdateLayout();
            grid.ScrollIntoView(rowItem, grid.Columns.Count > 0 ? grid.Columns[0] : null);

            if (grid.Columns.Count > 0)
            {
                grid.CurrentCell = new DataGridCellInfo(rowItem, grid.Columns[0]);
                grid.BeginEdit();
                var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(rowItem);
                (grid.Columns[0].GetCellContent(row) as FrameworkElement)?.Focus();
            }
        }

        private void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is WorkScheduleModel rowItem)
            {
                if (VM.RemoveCommand.CanExecute(rowItem))
                    VM.RemoveCommand.Execute(rowItem);
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { e.Handled = true; CloseRequested?.Invoke(); return; }
            base.OnPreviewKeyDown(e);
        }
    }
}
