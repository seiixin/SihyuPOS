#nullable enable
using SihyuPOSPayroll.Services;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SihyuPOSPayroll.Views.Admin.Positions
{
    public partial class AddEditPosition : UserControl
    {
        private readonly PositionSalaryService _service = new();
        private readonly bool                  _isEdit;
        private readonly PositionSalaryService.PositionSalary? _editing;

        public event EventHandler<bool>? DialogClosed;

        public AddEditPosition(PositionSalaryService.PositionSalary? item = null)
        {
            InitializeComponent();

            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) { e.Handled = true; Close(false); }
            };

            if (item != null)
            {
                _isEdit  = true;
                _editing = item;

                TitleText.Text    = "Edit Position";
                SubtitleText.Text = "Update the position name or daily rate.";
                PositionTextBox.Text  = item.Position;
                DailyRateTextBox.Text = item.DailyRate.ToString("0.00", CultureInfo.InvariantCulture);
                IsActiveCheckBox.IsChecked = item.IsActive;
            }
            else
            {
                TitleText.Text    = "Add Position";
                SubtitleText.Text = "Define a job position and its daily rate.";
                DailyRateTextBox.Text = "0.00";
            }
        }

        private void Close(bool saved) => DialogClosed?.Invoke(this, saved);

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close(false);

        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var tb        = (TextBox)sender;
            string candidate = tb.Text.Remove(tb.SelectionStart, tb.SelectionLength)
                                      .Insert(tb.SelectionStart, e.Text);
            e.Handled = !Regex.IsMatch(candidate, @"^\d{0,10}(\.\d{0,2})?$");
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var posName = PositionTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(posName))
            {
                ToastService.Warning("Position Name is required.");
                PositionTextBox.Focus();
                return;
            }

            decimal rate = 0m;
            if (!string.IsNullOrWhiteSpace(DailyRateTextBox.Text))
                decimal.TryParse(DailyRateTextBox.Text, NumberStyles.Any,
                                 CultureInfo.InvariantCulture, out rate);

            var item = new PositionSalaryService.PositionSalary
            {
                Id        = _editing?.Id ?? 0,
                Position  = posName,
                DailyRate = rate,
                IsActive  = IsActiveCheckBox.IsChecked == true,
                UpdatedAt = DateTime.Now,
            };

            bool ok = _isEdit && _editing != null
                ? _service.UpdatePosition(item)
                : _service.AddPosition(item);

            if (ok)
            {
                Close(true);
                ToastService.Success(_isEdit
                    ? $"Position '{item.Position}' updated."
                    : $"Position '{item.Position}' added.");
            }
            else
            {
                ToastService.Error(_isEdit
                    ? "Failed to update position."
                    : "Failed to add position. Name may already exist.");
            }
        }
    }
}
