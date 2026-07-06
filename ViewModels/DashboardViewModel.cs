using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;

namespace SihyuPOSPayroll.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged
    {
        // ── Services ───────────────────────────────────────────────────────────
        private readonly IEmployeeService  _employeeService;
        private readonly InventoryService  _inventoryService;

        // ── Stat card backing fields ───────────────────────────────────────────
        private string  _totalSalesToday  = "₱0.00";
        private int     _activeEmployees;
        private int     _lowStockCount;

        private string  _salesTrend       = "";
        private string  _employeeTrend    = "";
        private string  _lowStockTrend    = "";

        private SolidColorBrush _salesTrendBrush     = new(Color.FromRgb(0x6B, 0x72, 0x80));
        private SolidColorBrush _employeeTrendBrush  = new(Color.FromRgb(0x6B, 0x72, 0x80));
        private SolidColorBrush _lowStockTrendBrush  = new(Color.FromRgb(0x6B, 0x72, 0x80));

        // ── Chart backing fields ───────────────────────────────────────────────
        private PlotModel?   _salesChartModel;
        private int          _selectedMonth;
        private Visibility   _chartVisibility   = Visibility.Collapsed;
        private Visibility   _noDataVisibility  = Visibility.Visible;

        // ── Accent / red brushes (reused) ──────────────────────────────────────
        private static readonly SolidColorBrush EmeraldBrush = new(Color.FromRgb(0x12, 0x88, 0x54));
        private static readonly SolidColorBrush RedBrush     = new(Color.FromRgb(0xEF, 0x44, 0x44));
        private static readonly SolidColorBrush MutedBrush   = new(Color.FromRgb(0x6B, 0x72, 0x80));

        // ── Public properties ──────────────────────────────────────────────────
        public string TotalSalesToday
        {
            get => _totalSalesToday;
            private set { _totalSalesToday = value; OnPropertyChanged(); }
        }

        public int ActiveEmployees
        {
            get => _activeEmployees;
            private set { _activeEmployees = value; OnPropertyChanged(); }
        }

        public int LowStockCount
        {
            get => _lowStockCount;
            private set { _lowStockCount = value; OnPropertyChanged(); }
        }

        public string SalesTrend
        {
            get => _salesTrend;
            private set { _salesTrend = value; OnPropertyChanged(); }
        }

        public string EmployeeTrend
        {
            get => _employeeTrend;
            private set { _employeeTrend = value; OnPropertyChanged(); }
        }

        public string LowStockTrend
        {
            get => _lowStockTrend;
            private set { _lowStockTrend = value; OnPropertyChanged(); }
        }

        public SolidColorBrush SalesTrendBrush
        {
            get => _salesTrendBrush;
            private set { _salesTrendBrush = value; OnPropertyChanged(); }
        }

        public SolidColorBrush EmployeeTrendBrush
        {
            get => _employeeTrendBrush;
            private set { _employeeTrendBrush = value; OnPropertyChanged(); }
        }

        public SolidColorBrush LowStockTrendBrush
        {
            get => _lowStockTrendBrush;
            private set { _lowStockTrendBrush = value; OnPropertyChanged(); }
        }

        public PlotModel? SalesChartModel
        {
            get => _salesChartModel;
            private set { _salesChartModel = value; OnPropertyChanged(); }
        }

        public int SelectedMonth
        {
            get => _selectedMonth;
            set
            {
                if (_selectedMonth == value) return;
                _selectedMonth = value;
                OnPropertyChanged();
                _ = LoadDataAsync();
            }
        }

        public List<string> MonthNames { get; } = new List<string>
        {
            "January","February","March","April","May","June",
            "July","August","September","October","November","December"
        };

        public Visibility ChartVisibility
        {
            get => _chartVisibility;
            private set { _chartVisibility = value; OnPropertyChanged(); }
        }

        public Visibility NoDataVisibility
        {
            get => _noDataVisibility;
            private set { _noDataVisibility = value; OnPropertyChanged(); }
        }

        // ── Constructor ────────────────────────────────────────────────────────
        public DashboardViewModel(IEmployeeService? employeeService = null,
                                  InventoryService? inventoryService = null)
        {
            _employeeService  = employeeService  ?? new EmployeeService();
            _inventoryService = inventoryService ?? new InventoryService();

            // Default to current month (0-indexed)
            _selectedMonth = DateTime.Now.Month - 1;
        }

        // ── Data loading ───────────────────────────────────────────────────────
        public async Task LoadDataAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    LoadStats();
                    LoadChart();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DashboardViewModel.LoadDataAsync: {ex.Message}");
                }
            });
        }

        private void LoadStats()
        {
            // ── Sales today ────────────────────────────────────────────────────
            var dailySales = SalesService.GetSales(ReportPeriod.Daily);
            var today      = DateTime.Today;
            var todayRow   = dailySales.FirstOrDefault(r => r.StartDate.Date == today);
            decimal todayTotal    = todayRow?.TotalAmount ?? 0m;
            TotalSalesToday = $"₱{todayTotal:N2}";

            // Trend: compare today vs yesterday
            var yesterdayRow  = dailySales.FirstOrDefault(r => r.StartDate.Date == today.AddDays(-1));
            decimal yesterday = yesterdayRow?.TotalAmount ?? 0m;
            SalesTrend        = ComputeTrend(todayTotal, yesterday, out var salesBrush);
            SalesTrendBrush   = salesBrush;

            // ── Active employees ───────────────────────────────────────────────
            var employees    = _employeeService.GetAllEmployees();
            ActiveEmployees  = employees.Count(e => e.IsActive == true);
            EmployeeTrend    = "";
            EmployeeTrendBrush = MutedBrush;

            // ── Low-stock items ────────────────────────────────────────────────
            var lowStock    = _inventoryService.GetLowStockItems();
            LowStockCount   = lowStock.Count;
            LowStockTrend   = "";
            LowStockTrendBrush = LowStockCount > 0 ? RedBrush : MutedBrush;
        }

        private void LoadChart()
        {
            // Build daily data for the selected month in the current year
            var allDaily = SalesService.GetSales(ReportPeriod.Daily);
            int year     = DateTime.Now.Year;
            int month    = _selectedMonth + 1; // convert 0-index → 1-index

            var monthData = allDaily
                .Where(r => r.StartDate.Year == year && r.StartDate.Month == month)
                .OrderBy(r => r.StartDate)
                .ToList();

            if (monthData.Count == 0)
            {
                ChartVisibility  = Visibility.Collapsed;
                NoDataVisibility = Visibility.Visible;
                SalesChartModel  = null;
                return;
            }

            ChartVisibility  = Visibility.Visible;
            NoDataVisibility = Visibility.Collapsed;
            SalesChartModel  = BuildChartModel(monthData);
        }

        private PlotModel BuildChartModel(List<SalesRow> data)
        {
            var model = new PlotModel
            {
                PlotAreaBackground  = OxyColors.Transparent,
                Background          = OxyColors.Transparent,
                TextColor           = OxyColor.Parse("#9CA3AF"),
                PlotAreaBorderColor = OxyColor.Parse("#1A1D2E"),
            };

            var xAxis = new LinearAxis
            {
                Position           = AxisPosition.Bottom,
                AxislineColor      = OxyColor.Parse("#1A1D2E"),
                MajorGridlineStyle = LineStyle.None,
                TextColor          = OxyColor.Parse("#9CA3AF"),
                Title              = "Day",
            };
            var yAxis = new LinearAxis
            {
                Position           = AxisPosition.Left,
                AxislineColor      = OxyColor.Parse("#1A1D2E"),
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColor.Parse("#0F1220"),
                TextColor          = OxyColor.Parse("#9CA3AF"),
                Title              = "PHP",
                StringFormat       = "N0",
            };
            model.Axes.Add(xAxis);
            model.Axes.Add(yAxis);

            var line = new LineSeries
            {
                Color           = OxyColor.Parse("#128854"),
                StrokeThickness = 2,
            };
            var area = new AreaSeries
            {
                Fill            = OxyColor.FromArgb(32, 18, 136, 84),
                Color           = OxyColors.Transparent,
                StrokeThickness = 0,
            };

            foreach (var row in data)
            {
                double x = row.StartDate.Day;
                double y = (double)row.TotalAmount;
                line.Points.Add(new DataPoint(x, y));
                area.Points.Add(new DataPoint(x, y));
                area.Points2.Add(new DataPoint(x, 0));
            }

            model.Series.Add(area);
            model.Series.Add(line);
            return model;
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private static string ComputeTrend(decimal current, decimal previous,
                                           out SolidColorBrush brush)
        {
            if (previous == 0)
            {
                brush = MutedBrush;
                return "";
            }

            double pct = (double)((current - previous) / previous * 100);

            if (pct > 0)
            {
                brush = EmeraldBrush;
                return $"↑ {pct:F1}% vs yesterday";
            }
            else if (pct < 0)
            {
                brush = RedBrush;
                return $"↓ {Math.Abs(pct):F1}% vs yesterday";
            }
            else
            {
                brush = MutedBrush;
                return "No change vs yesterday";
            }
        }

        // ── INotifyPropertyChanged ─────────────────────────────────────────────
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
