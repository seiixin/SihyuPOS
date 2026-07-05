#nullable enable
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.ViewModels;

namespace SihyuPOSPayroll.Views.Admin.Attendance
{
    /// <summary>
    /// Interaction logic for AttendanceAdminView.xaml
    /// </summary>
    public partial class AttendanceAdminView : UserControl
    {
        // Guards to prevent re-entrancy during commit/update
        private bool _savingRow;
        private bool _committingCell;

        // Default ctor
        public AttendanceAdminView() : this(new AttendanceAdminViewModel())
        {
        }

        // DI/test-friendly ctor
        public AttendanceAdminView(AttendanceAdminViewModel viewModel)
        {
            InitializeComponent();

            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not AttendanceAdminViewModel vm) return;

            try
            {
                if (vm.FilterCommand?.CanExecute(null) == true)
                    vm.FilterCommand.Execute(null);

                if (vm.LeaveFilterCommand?.CanExecute(null) == true)
                    vm.LeaveFilterCommand.Execute(null);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load attendance/leave data.\n\n" + ex.Message,
                    "Attendance Admin", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;
        }

        // Keep VM.SelectedLeave in sync with the grid’s selection
        private void LeaveGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_savingRow) return;
            if (DataContext is not AttendanceAdminViewModel vm) return;
            if (sender is not DataGrid grid) return;

            if (grid.SelectedItem is LeaveRequestModel row && !ReferenceEquals(vm.SelectedLeave, row))
                vm.SelectedLeave = row;
        }

        // Commit any pending cell edit when the current cell moves
        private void LeaveGrid_CurrentCellChanged(object? sender, EventArgs e)
        {
            if (_committingCell) return;
            if (sender is not DataGrid grid) return;

            try
            {
                _committingCell = true;
                grid.CommitEdit(DataGridEditingUnit.Cell, true);
            }
            catch
            {
                // best-effort; ignore commit errors here
            }
            finally
            {
                _committingCell = false;
            }
        }

        // After a row edit is committed, persist changes via VM command
        private void LeaveGrid_RowEditEnding(object? sender, DataGridRowEditEndingEventArgs e)
        {
            if (_savingRow) return;
            if (e.EditAction != DataGridEditAction.Commit) return;
            if (sender is not DataGrid grid) return;
            if (DataContext is not AttendanceAdminViewModel vm) return;

            // Defer until WPF finishes committing the row to avoid re-entrancy
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_savingRow) return;

                try
                {
                    _savingRow = true;

                    // Ensure all pending edits are committed
                    grid.CommitEdit(DataGridEditingUnit.Cell, true);
                    grid.CommitEdit(DataGridEditingUnit.Row, true);

                    // Sync VM selection with the edited row
                    if (e.Row?.Item is LeaveRequestModel row && !ReferenceEquals(vm.SelectedLeave, row))
                        vm.SelectedLeave = row;

                    // Trigger VM update command
                    if (vm.LeaveUpdateCommand?.CanExecute(null) == true)
                        vm.LeaveUpdateCommand.Execute(null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to save edited leave row.\n\n" + ex.Message,
                        "Attendance Admin", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    _savingRow = false;
                }
            }), DispatcherPriority.Background);
        }

        /// <summary>
        /// Opens the Work Schedule editor in a modal window.
        /// (Wired from XAML: Click="OpenWorkSchedules_Click")
        /// </summary>
        private void OpenWorkSchedules_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var host = Window.GetWindow(this);

                var win = new Window
                {
                    Title = "Work Schedules",
                    Owner = host,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    SizeToContent = SizeToContent.WidthAndHeight,
                    MinWidth = 720,
                    MinHeight = 440,
                    ResizeMode = ResizeMode.CanResize,
                    Background = Brushes.White,
                    Content = new WorkScheduleEdit() // uses its own VM/service inside
                };

                win.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to open Work Schedules.\n\n" + ex.Message,
                    "Attendance Admin", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
