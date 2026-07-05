using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using SihyuPOSPayroll.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows;
using System.Threading.Tasks;

namespace SihyuPOSPayroll.ViewModels
{
    public class AdminPayslipViewModel : BaseViewModel
    {
        private ObservableCollection<PayslipRequestModel> _allRequests = new();
        private ObservableCollection<PayslipRequestModel> _payslipRequests = new();
        private readonly PayslipService _payslipService;
        private bool _isLoading;
        private string _statusMessage = string.Empty;
        private string _searchText = string.Empty;

        public ObservableCollection<PayslipRequestModel> PayslipRequests
        {
            get => _payslipRequests;
            set => SetProperty(ref _payslipRequests, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterPayslipRequests(value);
                }
            }
        }

        public ICommand ApproveCommand { get; }
        public ICommand DenyCommand { get; }
        public ICommand DoneCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand RefreshCommand { get; }

        public AdminPayslipViewModel()
        {
            _payslipService = new PayslipService();

            ApproveCommand = new RelayCommand<PayslipRequestModel>(async (request) => await ApproveRequestAsync(request), CanExecuteStatusCommand);
            DenyCommand = new RelayCommand<PayslipRequestModel>(async (request) => await DenyRequestAsync(request), CanExecuteStatusCommand);
            DoneCommand = new RelayCommand<PayslipRequestModel>(async (request) => await MarkRequestDoneAsync(request), CanExecuteStatusCommand);
            DeleteCommand = new RelayCommand<PayslipRequestModel>(async (request) => await DeleteRequestAsync(request), CanDeleteRequest);
            RefreshCommand = new RelayCommand(async _ => await LoadPayslipRequestsFromDatabaseAsync());
        }

        public async Task LoadPayslipRequestsFromDatabaseAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading payslip requests...";

                await Task.Run(() =>
                {
                    var data = _payslipService.GetAllPayslipRequests();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _allRequests = new ObservableCollection<PayslipRequestModel>(data);
                        PayslipRequests = new ObservableCollection<PayslipRequestModel>(_allRequests);
                    });
                });

                StatusMessage = $"Loaded {PayslipRequests.Count} payslip requests";
                await Task.Delay(2000); // Show message for 2 seconds
                StatusMessage = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading data: {ex.Message}";
                ShowErrorMessage($"Failed to load payslip requests: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Non-async version for compatibility
        public async void LoadPayslipRequestsFromDatabase()
        {
            await LoadPayslipRequestsFromDatabaseAsync();
        }

        public void FilterPayslipRequests(string filterText)
        {
            if (string.IsNullOrWhiteSpace(filterText))
            {
                PayslipRequests = new ObservableCollection<PayslipRequestModel>(_allRequests);
            }
            else
            {
                var filtered = _allRequests.Where(p =>
                    p.EmployeeId.ToString().Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(p.FullName) && p.FullName.Contains(filterText, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(p.Status) && p.Status.Contains(filterText, StringComparison.OrdinalIgnoreCase))).ToList();

                PayslipRequests = new ObservableCollection<PayslipRequestModel>(filtered);
            }

            StatusMessage = $"Showing {PayslipRequests.Count} of {_allRequests.Count} requests";
        }

        private bool CanExecuteStatusCommand(PayslipRequestModel request)
        {
            return request != null && !IsLoading;
        }

        private bool CanDeleteRequest(PayslipRequestModel request)
        {
            return request != null && !IsLoading &&
                   (string.Equals(request.Status, "Denied", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(request.Status, "Done", StringComparison.OrdinalIgnoreCase));
        }

        private async Task ApproveRequestAsync(PayslipRequestModel request)
        {
            if (request == null) return;

            try
            {
                if (string.Equals(request.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                {
                    ShowInfoMessage("Request is already approved.");
                    return;
                }

                var result = MessageBox.Show(
                    $"Are you sure you want to approve the payslip request for {request.FullName}?",
                    "Confirm Approval",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    IsLoading = true;
                    StatusMessage = "Approving request...";

                    await Task.Run(() =>
                    {
                        var success = _payslipService.UpdateRequestStatus(request.Id, "Approved");

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (success)
                            {
                                request.Status = "Approved";
                                StatusMessage = $"Request for {request.FullName} has been approved";
                            }
                            else
                            {
                                StatusMessage = "Failed to approve request";
                                ShowErrorMessage("Failed to approve the request. Please try again.");
                            }
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                ShowErrorMessage($"Error approving request: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task DenyRequestAsync(PayslipRequestModel request)
        {
            if (request == null) return;

            try
            {
                if (string.Equals(request.Status, "Denied", StringComparison.OrdinalIgnoreCase))
                {
                    ShowInfoMessage("Request is already denied.");
                    return;
                }

                var result = MessageBox.Show(
                    $"Are you sure you want to deny the payslip request for {request.FullName}?",
                    "Confirm Denial",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    IsLoading = true;
                    StatusMessage = "Denying request...";

                    await Task.Run(() =>
                    {
                        var success = _payslipService.UpdateRequestStatus(request.Id, "Denied");

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (success)
                            {
                                request.Status = "Denied";
                                StatusMessage = $"Request for {request.FullName} has been denied";
                            }
                            else
                            {
                                StatusMessage = "Failed to deny request";
                                ShowErrorMessage("Failed to deny the request. Please try again.");
                            }
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                ShowErrorMessage($"Error denying request: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task MarkRequestDoneAsync(PayslipRequestModel request)
        {
            if (request == null) return;

            try
            {
                if (string.Equals(request.Status, "Done", StringComparison.OrdinalIgnoreCase))
                {
                    ShowInfoMessage("Request is already marked as done.");
                    return;
                }

                if (!string.Equals(request.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                {
                    ShowInfoMessage("Only approved requests can be marked as done.");
                    return;
                }

                var result = MessageBox.Show(
                    $"Are you sure you want to mark the payslip request for {request.FullName} as done?",
                    "Confirm Done",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    IsLoading = true;
                    StatusMessage = "Marking request as done...";

                    await Task.Run(() =>
                    {
                        var success = _payslipService.UpdateRequestStatus(request.Id, "Done");

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (success)
                            {
                                request.Status = "Done";
                                StatusMessage = $"Request for {request.FullName} has been marked as done";
                            }
                            else
                            {
                                StatusMessage = "Failed to mark request as done";
                                ShowErrorMessage("Failed to mark the request as done. Please try again.");
                            }
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                ShowErrorMessage($"Error marking request as done: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task DeleteRequestAsync(PayslipRequestModel request)
        {
            if (request == null) return;

            try
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete the payslip request for {request.FullName}?\n\nThis action cannot be undone.",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    IsLoading = true;
                    StatusMessage = "Deleting request...";

                    await Task.Run(() =>
                    {
                        var success = _payslipService.DeletePayslipRequest(request.Id);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (success)
                            {
                                _allRequests.Remove(request);
                                PayslipRequests.Remove(request);
                                StatusMessage = $"Request for {request.FullName} has been deleted";
                            }
                            else
                            {
                                StatusMessage = "Failed to delete request";
                                ShowErrorMessage("Failed to delete the request. Please try again.");
                            }
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                ShowErrorMessage($"Error deleting request: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static void ShowErrorMessage(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static void ShowInfoMessage(string message)
        {
            MessageBox.Show(message, "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}