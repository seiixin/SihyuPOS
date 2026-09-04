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
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace SihyuPOSPayroll.Views.Admin.Employees
{
    public partial class AddEditEmployee : UserControl
    {
        private readonly EmployeeService       _employeeService       = new();
        private readonly PositionSalaryService _positionSalaryService = new();
        private readonly WorkScheduleService   _workScheduleService   = new();

        private readonly bool           _isEditMode;
        private readonly EmployeeModel? _editingEmployee;

        // Chosen local image path before Save
        private string? _selectedImagePath;

        /// <summary>Raised when the dialog should close. bool = saved successfully.</summary>
        public event EventHandler<bool>? DialogClosed;

        // ── Constructor ───────────────────────────────────────────────────────

        public AddEditEmployee(EmployeeModel? employee = null)
        {
            InitializeComponent();

            PopulatePositions();
            PopulateWorkSchedules();

            // Esc → cancel
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    Close(saved: false);
                }
            };

            if (employee != null)
            {
                _isEditMode      = true;
                _editingEmployee = employee;

                TitleText.Text    = "Edit Employee";
                SubtitleText.Text = "Update the employee's profile and settings.";

                FullNameTextBox.Text        = employee.FullName        ?? string.Empty;
                AgeTextBox.Text             = employee.Age?.ToString() ?? string.Empty;
                AddressTextBox.Text         = employee.Address         ?? string.Empty;
                ContactNumberTextBox.Text   = employee.ContactNumber   ?? string.Empty;
                EmergencyContactTextBox.Text= employee.EmergencyContact?? string.Empty;

                BirthdayDatePicker.SelectedDate  = employee.Birthday;
                DateHiredDatePicker.SelectedDate = employee.DateHired;

                SexComboBox.SelectedItem = GetComboBoxItemByContent(SexComboBox, employee.Sex);
                if (SexComboBox.SelectedItem == null) SexComboBox.SelectedIndex = 0;

                PositionComboBox.Text = employee.Position ?? string.Empty;

                if (employee.SalaryPerDay.HasValue)
                    SalaryPerDayTextBox.Text = employee.SalaryPerDay.Value
                                                  .ToString("0.00", CultureInfo.InvariantCulture);
                else
                    TryAutoFillSalaryFromPosition();

                ShiftComboBox.SelectedItem = GetComboBoxItemByContent(ShiftComboBox, employee.Shift);
                if (ShiftComboBox.SelectedItem == null) ShiftComboBox.SelectedIndex = 0;

                if (employee.WorkScheduleId.HasValue)
                    WorkScheduleComboBox.SelectedValue = employee.WorkScheduleId.Value;

                SssNumberTextBox.Text        = employee.SssNumber        ?? string.Empty;
                PhilhealthNumberTextBox.Text = employee.PhilhealthNumber ?? string.Empty;
                PagibigNumberTextBox.Text    = employee.PagibigNumber    ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(employee.ImageUrl))
                {
                    TryShowImagePreview(employee.ImageUrl);
                    SelectedImageLabel.Text = Path.GetFileName(employee.ImageUrl);
                }

                var isActive = employee.UserAccount?.IsActive ?? true;
                StatusComboBox.SelectedValue = isActive ? "1" : "0";
            }
            else
            {
                _isEditMode       = false;
                TitleText.Text    = "Add New Employee";
                SubtitleText.Text = "Fill in the employee details below.";

                SexComboBox.SelectedIndex   = 0;
                ShiftComboBox.SelectedIndex = 0;
                StatusComboBox.SelectedValue = "1"; // Active by default
            }

            SetSalaryReadOnlyState();
        }

        // ── Close helper ──────────────────────────────────────────────────────

        private void Close(bool saved)
        {
            DialogClosed?.Invoke(this, saved);

            // Legacy fallback: remove from parent panel if used outside the overlay
            if (Parent is Panel parent)
                parent.Children.Remove(this);
        }

        // ── UI event handlers ─────────────────────────────────────────────────

        private void PositionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ManualOverrideCheckBox.IsChecked != true)
                TryAutoFillSalaryFromPosition();
        }

        private void ManualOverrideCheckBox_Checked(object sender, RoutedEventArgs e)
            => SetSalaryReadOnlyState();

        private void ManualOverrideCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            SetSalaryReadOnlyState();
            TryAutoFillSalaryFromPosition();
        }

        private void UploadImage_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Title  = "Select Profile Photo",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.webp;*.bmp",
                Multiselect = false,
            };

            if (ofd.ShowDialog() == true)
            {
                _selectedImagePath     = ofd.FileName;
                SelectedImageLabel.Text = Path.GetFileName(_selectedImagePath);
                TryShowImagePreview(_selectedImagePath);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
            => Close(saved: false);

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // ── Validation ────────────────────────────────────────────────────
            var fullName = (FullNameTextBox.Text ?? string.Empty).Trim();
            var posText  = (PositionComboBox.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                ToastService.Warning("Full Name is required.");
                FullNameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(posText))
            {
                ToastService.Warning("Position is required.");
                PositionComboBox.Focus();
                return;
            }

            // ── Parse numeric fields ──────────────────────────────────────────
            int? age = null;
            if (int.TryParse((AgeTextBox.Text ?? string.Empty).Trim(), out var parsedAge))
                age = parsedAge;

            decimal? salaryPerDay = null;
            if (decimal.TryParse((SalaryPerDayTextBox.Text ?? string.Empty).Trim(),
                                  NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedSalary))
                salaryPerDay = parsedSalary;

            int? workScheduleId =
                WorkScheduleComboBox.SelectedValue is int sid ? sid : (int?)null;

            string shiftText =
                (ShiftComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
                ?? (ShiftComboBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(shiftText)) shiftText = "Morning";

            // ── Image ─────────────────────────────────────────────────────────
            var newImageUrl     = SaveProfilePhotoIfAny();
            var effectiveImage  = newImageUrl ?? (_isEditMode ? _editingEmployee?.ImageUrl : null);

            // ── Build model ───────────────────────────────────────────────────
            var employee = new EmployeeModel
            {
                FullName         = fullName,
                Age              = age,
                Sex              = (SexComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString(),
                Address          = (AddressTextBox.Text         ?? string.Empty).Trim(),
                Birthday         = BirthdayDatePicker.SelectedDate,
                ContactNumber    = (ContactNumberTextBox.Text   ?? string.Empty).Trim(),
                Position         = posText,
                SalaryPerDay     = salaryPerDay,
                Shift            = shiftText,
                WorkScheduleId   = workScheduleId,
                SssNumber        = (SssNumberTextBox.Text        ?? string.Empty).Trim(),
                PhilhealthNumber = (PhilhealthNumberTextBox.Text ?? string.Empty).Trim(),
                PagibigNumber    = (PagibigNumberTextBox.Text    ?? string.Empty).Trim(),
                ImageUrl         = effectiveImage ?? string.Empty,
                EmergencyContact = (EmergencyContactTextBox.Text ?? string.Empty).Trim(),
                DateHired        = DateHiredDatePicker.SelectedDate,
            };

            bool isActiveSelected = GetSelectedIsActive();

            // ── Persist ───────────────────────────────────────────────────────
            if (_isEditMode && _editingEmployee != null)
            {
                employee.Id        = _editingEmployee.Id;
                employee.CreatedAt = _editingEmployee.CreatedAt;

                if (_employeeService.UpdateEmployee(employee))
                {
                    TrySetActiveStatus(employee.Id, isActiveSelected);
                    Close(saved: true);
                    ToastService.Success($"'{employee.FullName}' updated.");
                }
                else
                {
                    ToastService.Error("Failed to update employee.");
                }
            }
            else
            {
                employee.CreatedAt = DateTime.Now;

                if (_employeeService.AddEmployee(employee))
                {
                    TrySetActiveStatus(employee.Id, isActiveSelected);
                    Close(saved: true);
                    ToastService.Success($"'{employee.FullName}' added.");
                }
                else
                {
                    ToastService.Error("Failed to add employee.");
                }
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

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
                Console.Error.WriteLine("Failed to load positions: " + ex.Message);
            }
        }

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
                WorkScheduleComboBox.ItemsSource       = list;
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
                SalaryPerDayTextBox.Text = rate.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private void SetSalaryReadOnlyState()
        {
            bool manual = ManualOverrideCheckBox.IsChecked == true;
            SalaryPerDayTextBox.IsReadOnly = !manual;
            SalaryPerDayTextBox.ToolTip    = manual
                ? "Manual override enabled — you can edit this value."
                : "Auto-filled from position. Enable Manual Override to edit.";
        }

        private ComboBoxItem? GetComboBoxItemByContent(ComboBox cb, string? content)
        {
            if (string.IsNullOrEmpty(content)) return null;
            foreach (var item in cb.Items)
                if (item is ComboBoxItem cbi &&
                    string.Equals(cbi.Content?.ToString(), content, StringComparison.OrdinalIgnoreCase))
                    return cbi;
            return null;
        }

        private string? SaveProfilePhotoIfAny()
        {
            if (string.IsNullOrWhiteSpace(_selectedImagePath) || !File.Exists(_selectedImagePath))
                return null;

            var appDir    = AppDomain.CurrentDomain.BaseDirectory;
            var targetDir = Path.Combine(appDir, "Images", "Employees");
            Directory.CreateDirectory(targetDir);

            var ext      = Path.GetExtension(_selectedImagePath);
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var destPath = Path.Combine(targetDir, fileName);

            File.Copy(_selectedImagePath, destPath, overwrite: false);

            return Path.Combine("Images", "Employees", fileName).Replace('\\', '/');
        }

        private void TryShowImagePreview(string path)
        {
            try
            {
                Uri uri = Uri.IsWellFormedUriString(path, UriKind.Absolute)
                    ? new Uri(path, UriKind.Absolute)
                    : new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path),
                              UriKind.Absolute);

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource   = uri;
                bmp.EndInit();

                ImagePreview.Source     = bmp;
                ImagePreview.Visibility = Visibility.Visible;
            }
            catch
            {
                ImagePreview.Source     = null;
                ImagePreview.Visibility = Visibility.Collapsed;
            }
        }

        private bool GetSelectedIsActive()
        {
            var sv = StatusComboBox?.SelectedValue?.ToString();
            return string.IsNullOrWhiteSpace(sv) || sv == "1";
        }

        private void TrySetActiveStatus(int employeeId, bool isActive)
        {
            try
            {
                _employeeService.SetUserActiveStatusByEmployeeId(employeeId, isActive);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Failed to set active status: " + ex.Message);
            }
        }
    }
}
