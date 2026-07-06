using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Win32;

using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using SihyuPOSPayroll.Helpers; // RelayCommand

// Alias the *Services* interface/implementation to avoid namespace ambiguity
using IEmployeeService = SihyuPOSPayroll.Services.IEmployeeService;
using EmployeeService = SihyuPOSPayroll.Services.EmployeeService;

namespace SihyuPOSPayroll.ViewModels
{
    // NOTE: Removed the local IEmployeeService from ViewModels to avoid conflicts.
    // Use the Services-layer interface via the aliases above.

    public sealed class EmployeeProfileViewModel : INotifyPropertyChanged
    {
        private readonly IEmployeeService _service;

        // -----------------------------
        // Ctors (choose one at creation time)
        // -----------------------------

        // 1) Preferred: pass the known employeeId + DI for service
        public EmployeeProfileViewModel(int employeeId, IEmployeeService? service = null)
        {
            _service = service ?? new EmployeeService();
            InitializeCommands();
            if (employeeId > 0)
            {
                _employeeId = employeeId;
                LoadEmployee(_employeeId);
            }
        }

        // 2) Alternative: pass the logged-in UserModel (like Sidebar does), we’ll extract employeeId
        public EmployeeProfileViewModel(UserModel user, IEmployeeService? service = null)
            : this(user?.Employee?.Id ?? 0, service) { }

        // 3) Last resort: you can still new-up with no args (e.g., designer),
        // but nothing will load until EmployeeId is set manually.
        public EmployeeProfileViewModel() : this(0, null) { }

        // -----------------------------
        // Bindable state
        // -----------------------------
        private int _employeeId;
        public int EmployeeId
        {
            get => _employeeId;
            set
            {
                if (Set(ref _employeeId, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                    if (_employeeId > 0)
                        LoadEmployee(_employeeId);
                }
            }
        }

        private EmployeeModel? _employee;
        public EmployeeModel? Employee
        {
            get => _employee;
            set
            {
                if (Set(ref _employee, value))
                {
                    OnPropertyChanged(nameof(DisplayName));
                    OnPropertyChanged(nameof(DisplayPosition));
                }
            }
        }

        public string DisplayName => Employee?.FullName ?? "—";
        public string DisplayPosition => string.IsNullOrWhiteSpace(Employee?.Position) ? "—" : Employee!.Position;

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (Set(ref _isBusy, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => Set(ref _statusMessage, value);
        }

        // -----------------------------
        // Commands
        // -----------------------------
        public ICommand LoadCommand { get; private set; } = null!;
        public ICommand SaveCommand { get; private set; } = null!;
        public ICommand ChangePhotoCommand { get; private set; } = null!;
        public ICommand RefreshCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            LoadCommand = new RelayCommand(_ => LoadEmployee(EmployeeId),
                                           _ => !IsBusy && EmployeeId > 0);

            SaveCommand = new RelayCommand(_ => SaveEmployee(),
                                           _ => !IsBusy && Employee is { Id: > 0 });

            ChangePhotoCommand = new RelayCommand(_ => ChangePhoto(),
                                                  _ => !IsBusy && Employee is { Id: > 0 });

            RefreshCommand = new RelayCommand(_ => LoadEmployee(EmployeeId),
                                              _ => !IsBusy && EmployeeId > 0);
        }

        // Convenience (if code-behind wants to call)
        public void Load() => LoadEmployee(EmployeeId);
        public void Save() => SaveEmployee();

        // -----------------------------
        // Ops
        // -----------------------------
        private void LoadEmployee(int id)
        {
            if (id <= 0) { StatusMessage = "Invalid employee id."; return; }

            try
            {
                IsBusy = true;

                var one = _service.GetEmployeeById(id);
                if (one == null)
                {
                    // Fallback if service shape differs
                    var list = _service.GetAllEmployees();
                    one = list.Find(e => e.Id == id);
                }

                Employee = one;
                StatusMessage = one == null ? $"Employee #{id} not found." : $"Loaded employee #{id}.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SaveEmployee()
        {
            if (Employee is not { Id: > 0 })
            {
                StatusMessage = "Nothing to save.";
                return;
            }

            try
            {
                IsBusy = true;

                var ok = _service.UpdateEmployee(Employee);
                StatusMessage = ok ? "Profile updated." : "No changes were saved.";

                if (ok) LoadEmployee(Employee.Id);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Save failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ChangePhoto()
        {
            if (Employee is not { Id: > 0 })
            {
                StatusMessage = "Load an employee first.";
                return;
            }

            try
            {
                var ofd = new OpenFileDialog
                {
                    Title = "Choose profile photo",
                    Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
                    CheckFileExists = true,
                    Multiselect = false
                };
                if (ofd.ShowDialog() != true) return;

                IsBusy = true;

                var path = ofd.FileName;

                // Prefer dedicated endpoint, fall back to full update
                var ok = _service.UpdateEmployeeImage(Employee.Id, path);
                if (!ok)
                {
                    Employee.ImageUrl = path;
                    ok = _service.UpdateEmployee(Employee);
                }

                if (ok)
                {
                    Employee.ImageUrl = path;
                    // raise for bindings that listen to Employee.* directly
                    OnPropertyChanged(nameof(Employee));
                    StatusMessage = "Photo updated.";
                }
                else
                {
                    StatusMessage = "Failed to update photo.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Photo update failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // -----------------------------
        // INotifyPropertyChanged
        // -----------------------------
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }
}
