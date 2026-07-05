#nullable enable
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace SihyuPOSPayroll.Views.Admin.Employees
{
    public partial class AddEditEmployee : UserControl
    {
        private readonly EmployeeService _employeeService = new();
        private readonly UserService _userService = new();
        private readonly PositionSalaryService _positionSalaryService = new();
        private readonly WorkScheduleService _workScheduleService = new();   // Work schedule source

        private readonly bool _isEditMode;
        private readonly EmployeeModel? _editingEmployee;

        // Holds the chosen local image path before Save
        private string? _selectedImagePath;

        // Notify parent when saved
        public delegate void EmployeeSavedHandler();
        public event EmployeeSavedHandler? OnEmployeeSaved;

        public AddEditEmployee(EmployeeModel? employee = null)
        {
            InitializeComponent();

            // Populate dropdowns
            PopulatePositions();
            PopulateWorkSchedules();

            if (employee != null)
            {
                _isEditMode = true;
                _editingEmployee = employee;

                TitleText.Text = "Edit Employee";

                // Populate fields
                FullNameTextBox.Text = employee.FullName ?? string.Empty;
                AgeTextBox.Text = employee.Age?.ToString() ?? string.Empty;
                SexComboBox.SelectedItem = GetComboBoxItemByContent(SexComboBox, employee.Sex);
                AddressTextBox.Text = employee.Address ?? string.Empty;
                BirthdayDatePicker.SelectedDate = employee.Birthday;
                ContactNumberTextBox.Text = employee.ContactNumber ?? string.Empty;

                // Position (combo is editable)
                PositionComboBox.Text = employee.Position ?? string.Empty;

                // Salary: keep existing; if empty and manual override is off, try auto-fill from preset
                if (employee.SalaryPerDay.HasValue)
                    SalaryPerDayTextBox.Text = employee.SalaryPerDay.Value.ToString(CultureInfo.InvariantCulture);
                else
                    TryAutoFillSalaryFromPosition();

                // Shift: try to select the existing one; if nothing matches, pick a safe default
                ShiftComboBox.SelectedItem = GetComboBoxItemByContent(ShiftComboBox, employee.Shift);
                if (ShiftComboBox.SelectedItem == null)
                    ShiftComboBox.SelectedIndex = 0; // default "Morning"

                // Work Schedule (if any)
                if (employee.WorkScheduleId.HasValue)
                    WorkScheduleComboBox.SelectedValue = employee.WorkScheduleId.Value;

                SssNumberTextBox.Text = employee.SssNumber ?? string.Empty;
                PhilhealthNumberTextBox.Text = employee.PhilhealthNumber ?? string.Empty;
                PagibigNumberTextBox.Text = employee.PagibigNumber ?? string.Empty;

                // Image preview for existing photo
                if (!string.IsNullOrWhiteSpace(employee.ImageUrl))
                {
                    TryShowImagePreview(employee.ImageUrl!);
                    if (SelectedImageLabel != null)
                        SelectedImageLabel.Text = System.IO.Path.GetFileName(employee.ImageUrl);
                }

                EmergencyContactTextBox.Text = employee.EmergencyContact ?? string.Empty;
                DateHiredDatePicker.SelectedDate = employee.DateHired;

                // =========================
                // ACCOUNT (edit mode)
                // =========================
                if (employee.UserAccount != null)
                {
                    CreateUserAccountCheckBox.IsChecked = true;
                    EmailTextBox.Text = employee.UserAccount.Email ?? string.Empty;
                    // Don't set password, since we don't store plaintext
                    // Select the correct role
                    foreach (var item in RoleComboBox.Items)
                    {
                        if (item is ComboBoxItem cbi && 
                            string.Equals(cbi.Content?.ToString(), employee.UserAccount.Role, StringComparison.OrdinalIgnoreCase))
                        {
                            RoleComboBox.SelectedItem = cbi;
                            break;
                        }
                    }
                }
                else
                {
                    CreateUserAccountCheckBox.IsChecked = false;
                }
                
                var isActive = employee.UserAccount?.IsActive ?? true;
                if (StatusComboBox != null)
                    StatusComboBox.SelectedValue = isActive ? "1" : "0";
            }
            else
            {
                _isEditMode = false;
                TitleText.Text = "Add New Employee";
                SexComboBox.SelectedIndex = 0;
                ShiftComboBox.SelectedIndex = 0; // default "Morning"

                // =========================
                // ACCOUNT STATUS (new)
                // Default to Active
                // =========================
                if (StatusComboBox != null)
                    StatusComboBox.SelectedValue = "1";
            }

            // Salary field starts read-only; toggle via manual override
            SetSalaryReadOnlyState();
        }

        // =======================
        // UI Event Handlers
        // =======================

        private void PositionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Auto-fill salary only if manual override is OFF
            if (ManualOverrideCheckBox.IsChecked == true) return;
            TryAutoFillSalaryFromPosition();
        }

        private void ManualOverrideCheckBox_Checked(object sender, RoutedEventArgs e) => SetSalaryReadOnlyState();

        private void ManualOverrideCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            SetSalaryReadOnlyState();
            // When turning override OFF, refresh to preset
            TryAutoFillSalaryFromPosition();
        }

        private void UploadImage_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Title = "Select Profile Photo",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.webp;*.bmp",
                Multiselect = false
            };

            if (ofd.ShowDialog() == true)
            {
                _selectedImagePath = ofd.FileName;
                if (SelectedImageLabel != null)
                    SelectedImageLabel.Text = System.IO.Path.GetFileName(_selectedImagePath);
                TryShowImagePreview(_selectedImagePath);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (Parent is Panel parent)
                parent.Children.Remove(this);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Validate required fields (Full Name and Position)
            var posText = (PositionComboBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(FullNameTextBox.Text) || string.IsNullOrWhiteSpace(posText))
            {
                MessageBox.Show("Please fill at least Full Name and Position.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Parse numeric fields safely
            int? age = null;
            if (int.TryParse((AgeTextBox.Text ?? string.Empty).Trim(), out var parsedAge))
                age = parsedAge;

            decimal? salaryPerDay = null;
            if (decimal.TryParse((SalaryPerDayTextBox.Text ?? string.Empty).Trim(),
                                 NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedSalary))
                salaryPerDay = parsedSalary;

            // WorkScheduleId (SelectedValuePath="Id")
            int? workScheduleId = (WorkScheduleComboBox.SelectedValue is int id) ? id : (int?)null;

            // Shift: read from SelectedItem if possible; else from Text; if still blank, default to "Morning"
            string? shiftText =
                (ShiftComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
                ?? (ShiftComboBox.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(shiftText))
                shiftText = "Morning";

            // Save uploaded photo (if any). If none, keep existing on edit; otherwise null.
            var newImageUrl = SaveProfilePhotoIfAny();
            var effectiveImageUrl = newImageUrl
                ?? (_isEditMode ? _editingEmployee?.ImageUrl : null);

            // Prepare employee object
            var employee = new EmployeeModel
            {
                FullName = (FullNameTextBox.Text ?? string.Empty).Trim(),
                Age = age,
                Sex = (SexComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString(),
                Address = (AddressTextBox.Text ?? string.Empty).Trim(),
                Birthday = BirthdayDatePicker.SelectedDate,
                ContactNumber = (ContactNumberTextBox.Text ?? string.Empty).Trim(),
                Position = posText,
                SalaryPerDay = salaryPerDay,
                Shift = shiftText,                           // guaranteed non-empty
                WorkScheduleId = workScheduleId,            // may be null if not chosen
                SssNumber = (SssNumberTextBox.Text ?? string.Empty).Trim(),
                PhilhealthNumber = (PhilhealthNumberTextBox.Text ?? string.Empty).Trim(),
                PagibigNumber = (PagibigNumberTextBox.Text ?? string.Empty).Trim(),
                ImageUrl = effectiveImageUrl ?? string.Empty,
                EmergencyContact = (EmergencyContactTextBox.Text ?? string.Empty).Trim(),
                DateHired = DateHiredDatePicker.SelectedDate
            };

            var isActiveSelected = GetSelectedIsActiveOrDefault(); // read from StatusComboBox
            var createAccount = CreateUserAccountCheckBox.IsChecked == true;
            
            // Validate user account fields if creating account
            if (createAccount)
            {
                if (string.IsNullOrWhiteSpace(EmailTextBox.Text))
                {
                    MessageBox.Show("Please enter an email address for the user account.", "Validation",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(PasswordBox.Password))
                {
                    MessageBox.Show("Please enter a password for the user account.", "Validation",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            if (_isEditMode && _editingEmployee != null)
            {
                employee.Id = _editingEmployee.Id;
                employee.CreatedAt = _editingEmployee.CreatedAt;

                var success = _employeeService.UpdateEmployee(employee);
                if (success)
                {
                    // Handle user account
                    if (createAccount)
                    {
                        var role = (RoleComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Employee";
                        
                        if (_editingEmployee.UserAccount != null)
                        {
                            // Update existing user
                            var user = new UserModel
                            {
                                Id = _editingEmployee.UserAccount.Id,
                                Email = EmailTextBox.Text,
                                Password = PasswordBox.Password, // Will be hashed in UserService
                                Role = role,
                                EmployeeId = employee.Id
                            };
                            _userService.UpdateUser(user);
                        }
                        else
                        {
                            // Create new user
                            var user = new UserModel
                            {
                                Email = EmailTextBox.Text,
                                Password = PasswordBox.Password, // Will be hashed in UserService
                                Role = role,
                                EmployeeId = employee.Id
                            };
                            _userService.AddUser(user);
                        }
                        
                        // Apply account status
                        TrySetActiveStatusByEmployeeId(employee.Id, isActiveSelected);
                    }
                    else
                    {
                        // Optionally delete user account if checkbox is unchecked
                        // For now, just skip - maybe add later
                    }

                    MessageBox.Show("Employee updated successfully.", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    OnEmployeeSaved?.Invoke();
                    Cancel_Click(sender, e);
                }
                else
                {
                    MessageBox.Show("Failed to update employee.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                employee.CreatedAt = DateTime.Now;
                var success = _employeeService.AddEmployee(employee);
                if (success)
                {
                    // Handle user account
                    if (createAccount)
                    {
                        var role = (RoleComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Employee";
                        var user = new UserModel
                        {
                            Email = EmailTextBox.Text,
                            Password = PasswordBox.Password, // Will be hashed in UserService
                            Role = role,
                            EmployeeId = employee.Id
                        };
                        _userService.AddUser(user);
                        
                        // Apply account status
                        TrySetActiveStatusByEmployeeId(employee.Id, isActiveSelected);
                    }

                    MessageBox.Show("Employee added successfully.", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    OnEmployeeSaved?.Invoke();
                    Cancel_Click(sender, e);
                }
                else
                {
                    MessageBox.Show("Failed to add employee.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // =======================
        // Helpers
        // =======================

        private void PopulatePositions()
        {
            try
            {
                var list = _positionSalaryService.Load()
                                                 .Where(p => p.IsActive)
                                                 .OrderBy(p => p.Position, StringComparer.OrdinalIgnoreCase)
                                                 .Select(p => p.Position)
                                                 .Distinct(StringComparer.OrdinalIgnoreCase)
                                                 .ToList();

                PositionComboBox.ItemsSource = list;
            }
            catch (Exception ex)
            {
                // Non-fatal; just log
                Console.Error.WriteLine("Failed to load position presets: " + ex.Message);
            }
        }

        // Load work schedules into the combo
        private void PopulateWorkSchedules()
        {
            try
            {
                var list = _workScheduleService.Load()
                                               .Where(s => s.IsActive)
                                               .OrderBy(s => s.Label, StringComparer.OrdinalIgnoreCase)
                                               .ToList();

                WorkScheduleComboBox.DisplayMemberPath = "Label";
                WorkScheduleComboBox.SelectedValuePath = "Id";
                WorkScheduleComboBox.ItemsSource = list;

                // Optional auto-select one schedule:
                // if (list.Count == 1) WorkScheduleComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Failed to load work schedules: " + ex.Message);
            }
        }

        private void TryAutoFillSalaryFromPosition()
        {
            var pos = (PositionComboBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(pos)) return;
            if (ManualOverrideCheckBox.IsChecked == true) return;

            if (_positionSalaryService.TryGetRate(pos, out var rate))
            {
                SalaryPerDayTextBox.Text = rate.ToString("0.00", CultureInfo.InvariantCulture);
            }
            // else: leave as is if no preset found
        }

        private void SetSalaryReadOnlyState()
        {
            var manual = ManualOverrideCheckBox.IsChecked == true;
            SalaryPerDayTextBox.IsReadOnly = !manual;
            SalaryPerDayTextBox.ToolTip = manual
                ? "Manual override enabled. You can edit this value."
                : "Auto-filled from position. Enable Manual Override to edit.";
        }

        private ComboBoxItem? GetComboBoxItemByContent(ComboBox comboBox, string? content)
        {
            if (string.IsNullOrEmpty(content)) return null;

            foreach (var item in comboBox.Items)
            {
                if (item is ComboBoxItem cbi && string.Equals(cbi.Content?.ToString(), content, StringComparison.Ordinal))
                    return cbi;
            }
            return null;
        }

        private string? SaveProfilePhotoIfAny()
        {
            if (string.IsNullOrWhiteSpace(_selectedImagePath) || !File.Exists(_selectedImagePath))
                return null;

            // Save to app-local folder: <app>/Images/Employees/<guid>.<ext>
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var targetDir = System.IO.Path.Combine(appDir, "Images", "Employees");
            Directory.CreateDirectory(targetDir);

            var ext = System.IO.Path.GetExtension(_selectedImagePath);
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var destPath = System.IO.Path.Combine(targetDir, fileName);

            File.Copy(_selectedImagePath, destPath, overwrite: false);

            // Return a relative path (easier to move the app)
            var relative = System.IO.Path.Combine("Images", "Employees", fileName)
                            .Replace('\\', '/');
            return relative;
        }

        private void TryShowImagePreview(string path)
        {
            try
            {
                // Convert relative app path to absolute if needed
                Uri uri;
                if (Uri.IsWellFormedUriString(path, UriKind.Absolute))
                {
                    uri = new Uri(path, UriKind.Absolute);
                }
                else
                {
                    var abs = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
                    uri = new Uri(abs, UriKind.Absolute);
                }

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = uri;
                bmp.EndInit();

                if (ImagePreview != null)
                {
                    ImagePreview.Source = bmp;
                    ImagePreview.Visibility = Visibility.Visible;
                }
            }
            catch
            {
                if (ImagePreview != null)
                {
                    ImagePreview.Source = null;
                    ImagePreview.Visibility = Visibility.Collapsed;
                }
            }
        }

        private bool GetSelectedIsActiveOrDefault()
        {
            // StatusComboBox.SelectedValue comes from ComboBoxItem.Tag ("1" or "0")
            var sv = StatusComboBox?.SelectedValue?.ToString();
            if (string.IsNullOrWhiteSpace(sv)) return true; // default Active
            return sv == "1";
        }

        private void TrySetActiveStatusByEmployeeId(int employeeId, bool isActive)
        {
            try
            {
                // This will affect 0 rows if there is no linked user yet (which is fine).
                _employeeService.SetUserActiveStatusByEmployeeId(employeeId, isActive);
            }
            catch (Exception ex)
            {
                // Non-fatal; keep the main save successful even if status update failed.
                Console.Error.WriteLine("Failed to set user active status: " + ex.Message);
            }
        }
    }
}
