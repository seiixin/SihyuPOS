using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;

namespace SihyuPOSPayroll.ViewModels
{
    public enum ChartMode { Monthly, Yearly }

    public class DashboardViewModel : INotifyPropertyChanged
    {
        // ── Services ───────────────────────────────────────────────────────────
        private readonly IEmployeeService _employeeService;
        private readonly InventoryService _inventoryService;
        private readonly OrderService     _orderService = new();

        // ── Stat card backing fields ───────────────────────────────────────────
        private string _totalSalesToday    = "₱0.00";
        private string _totalSalesThisYear = "₱0.00";
        private int    _activeEmployees;
        private int    _lowStockCount;

        private string _salesTrend    = "";
        private string _employeeTrend = "";
        private string _lowStockTrend = "";

        private SolidColorBrush _salesTrendBrush    = new(Color.FromRgb(0x6B, 0x72, 0x80));
        private SolidColorBrush _employeeTrendBrush = new(Color.FromRgb(0x6B, 0x72, 0x80));
        private SolidColorBrush _lowStockTrendBrush = new(Color.FromRgb(0x6B, 0x72, 0x80));

        // ── Chart backing fields ───────────────────────────────────────────────
        private PlotModel?  _salesChartModel;
        private int         _selectedMonth;
        private ChartMode   _selectedChartMode  = ChartMode.Monthly;
        private string      _chartTitle         = "Monthly Sales";
        private Visibility  _chartVisibility    = Visibility.Collapsed;
        private Visibility  _noDataVisibility   = Visibility.Visible;
        private Visibility  _monthPickerVisible = Visibility.Visible;

        // ── Accent brushes (reused) ────────────────────────────────────────────
        private static readonly SolidColorBrush EmeraldBrush = new(Color.FromRgb(0x12, 0x88, 0x54));
        private static readonly SolidColorBrush RedBrush     = new(Color.FromRgb(0xEF, 0x44, 0x44));
        private static readonly SolidColorBrush MutedBrush   = new(Color.FromRgb(0x6B, 0x72, 0x80));

        // ── Public properties ──────────────────────────────────────────────────
        public string TotalSalesToday
        {
            get => _totalSalesToday;
            private set { _totalSalesToday = value; OnPropertyChanged(); }
        }

        public string TotalSalesThisYear
        {
            get => _totalSalesThisYear;
            private set { _totalSalesThisYear = value; OnPropertyChanged(); }
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

        public string ChartTitle
        {
            get => _chartTitle;
            private set { _chartTitle = value; OnPropertyChanged(); }
        }

        // ── Chart mode toggle ─────────────────────────────────────────────────
        public ChartMode SelectedChartMode
        {
            get => _selectedChartMode;
            set
            {
                if (_selectedChartMode == value) return;
                _selectedChartMode  = value;
                MonthPickerVisible  = value == ChartMode.Monthly
                                      ? Visibility.Visible : Visibility.Collapsed;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsMonthlyMode));
                OnPropertyChanged(nameof(IsYearlyMode));
                _ = LoadDataAsync();
            }
        }

        public bool IsMonthlyMode
        {
            get => _selectedChartMode == ChartMode.Monthly;
            set { if (value) SelectedChartMode = ChartMode.Monthly; }
        }

        public bool IsYearlyMode
        {
            get => _selectedChartMode == ChartMode.Yearly;
            set { if (value) SelectedChartMode = ChartMode.Yearly; }
        }

        public Visibility MonthPickerVisible
        {
            get => _monthPickerVisible;
            private set { _monthPickerVisible = value; OnPropertyChanged(); }
        }

        // ── Month picker ──────────────────────────────────────────────────────
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

        public List<string> MonthNames { get; } = new()
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

        // ── Recent Orders ──────────────────────────────────────────────────────
        public ObservableCollection<OrderModel> RecentOrders { get; } = new();

        private int _todayOrderCount;
        public int TodayOrderCount
        {
            get => _todayOrderCount;
            private set { _todayOrderCount = value; OnPropertyChanged(); }
        }

        // ── Constructor ────────────────────────────────────────────────────────
        public DashboardViewModel(IEmployeeService? employeeService = null,
                                  InventoryService? inventoryService = null)
        {
            _employeeService    = employeeService  ?? new EmployeeService();
            _inventoryService   = inventoryService ?? new InventoryService();
            _selectedMonth      = DateTime.Now.Month - 1;
            _selectedChartMode  = ChartMode.Monthly;
            _monthPickerVisible = Visibility.Visible;
        }

        // ── Data loading ───────────────────────────────────────────────────────
        public async Task LoadDataAsync()
        {
            string          totalSalesToday = "₱0.00";
            string          totalSalesYear  = "₱0.00";
            string          salesTrend      = "";
            SolidColorBrush salesBrush      = MutedBrush;
            int             activeEmployees = 0;
            int             lowStockCount   = 0;
            SolidColorBrush lowStockBrush   = MutedBrush;
            PlotModel?      chartModel      = null;
            Visibility      chartVis        = Visibility.Collapsed;
            Visibility      noDataVis       = Visibility.Visible;
            List<OrderModel> recentOrders   = new();
            int             todayOrderCount = 0;
            string          chartTitle      = "";
            ChartMode       mode            = _selectedChartMode;
            int             selMonth        = _selectedMonth;

            await Task.Run(() =>
            {
                try
                {
                    decimal todayTotal = SalesService.GetTodayTotal();
                    decimal yesterday  = SalesService.GetYesterdayTotal();
                    decimal yearTotal  = SalesService.GetYearTotal();
                    totalSalesToday    = $"₱{todayTotal:N2}";
                    totalSalesYear     = $"₱{yearTotal:N2}";
                    salesTrend         = ComputeTrend(todayTotal, yesterday, out salesBrush);

                    var employees   = _employeeService.GetAllEmployees();
                    activeEmployees = employees.Count(e => e.IsActive == true);

                    var lowStock  = _inventoryService.GetLowStockItems();
                    lowStockCount = lowStock.Count;
                    lowStockBrush = lowStockCount > 0 ? RedBrush : MutedBrush;

                    int year = DateTime.Now.Year;

                    if (mode == ChartMode.Monthly)
                    {
                        int month  = selMonth + 1;
                        chartTitle = $"Monthly Sales — {new System.Globalization.DateTimeFormatInfo().GetMonthName(month)} {year}";
                        var data   = SalesService.GetDailySalesForMonth(year, month);
                        if (data.Count > 0)
                        {
                            chartModel = BuildDailyChartModel(data);
                            chartVis   = Visibility.Visible;
                            noDataVis  = Visibility.Collapsed;
                        }
                    }
                    else
                    {
                        chartTitle = $"Yearly Sales — {year}";
                        var data   = SalesService.GetMonthlySalesForYear(year);
                        if (data.Count > 0)
                        {
                            chartModel = BuildMonthlyChartModel(data);
                            chartVis   = Visibility.Visible;
                            noDataVis  = Visibility.Collapsed;
                        }
                    }

                    var allOrders   = _orderService.GetAllOrders();
                    todayOrderCount = allOrders.Count(o => o.CreatedAt.Date == DateTime.Today);
                    recentOrders    = allOrders
                        .OrderByDescending(o => o.CreatedAt)
                        .Take(10)
                        .ToList();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DashboardViewModel.LoadDataAsync: {ex.Message}");
                }
            });

            Application.Current?.Dispatcher.Invoke(() =>
            {
                TotalSalesToday    = totalSalesToday;
                TotalSalesThisYear = totalSalesYear;
                SalesTrend         = salesTrend;
                SalesTrendBrush    = salesBrush;
                ActiveEmployees    = activeEmployees;
                TodayOrderCount    = todayOrderCount;
                LowStockCount      = lowStockCount;
                LowStockTrendBrush = lowStockBrush;
                ChartTitle         = chartTitle;
                SalesChartModel    = chartModel;
                ChartVisibility    = chartVis;
                NoDataVisibility   = noDataVis;

                RecentOrders.Clear();
                foreach (var o in recentOrders)
                    RecentOrders.Add(o);
            });
        }

        // ── Chart builders ────────────────────────────────────────────────────

        private static PlotModel BuildDailyChartModel(List<SalesRow> data)
            => BuildChartModel(data, x => (double)x.StartDate.Day, "Day {2:0}\n₱{4:N2}");

        private static PlotModel BuildMonthlyChartModel(List<SalesRow> data)
        {
            var model = CreateBaseModel();
            var xAxis = CreateBaseXAxis();
            xAxis.Minimum         = 1;
            xAxis.Maximum         = 12;
            xAxis.AbsoluteMinimum = 1;
            xAxis.AbsoluteMaximum = 12;
            xAxis.MajorStep       = 1;
            xAxis.LabelFormatter  = v =>
            {
                int m = (int)Math.Round(v);
                return m >= 1 && m <= 12
                    ? new[] { "Jan","Feb","Mar","Apr","May","Jun",
                               "Jul","Aug","Sep","Oct","Nov","Dec" }[m - 1]
                    : "";
            };
            model.Axes.Add(xAxis);
            model.Axes.Add(CreateBaseYAxis());
            AddLineSeries(model, data, x => (double)x.StartDate.Month, "{2}\n₱{4:N2}");
            return model;
        }

        private static PlotModel BuildChartModel(List<SalesRow> data,
            Func<SalesRow, double> xSelector, string trackerFormat)
        {
            var model = CreateBaseModel();
            model.Axes.Add(CreateBaseXAxis());
            model.Axes.Add(CreateBaseYAxis());
            AddLineSeries(model, data, xSelector, trackerFormat);
            return model;
        }

        private static PlotModel CreateBaseModel() => new()
        {
            PlotAreaBackground  = OxyColors.Transparent,
            Background          = OxyColors.Transparent,
            TextColor           = OxyColor.Parse("#9CA3AF"),
            PlotAreaBorderColor = OxyColors.Transparent,
            Padding             = new OxyThickness(8, 8, 8, 8),
        };

        private static LinearAxis CreateBaseXAxis() => new()
        {
            Position           = AxisPosition.Bottom,
            AxislineColor      = OxyColor.Parse("#1A1D2E"),
            AxislineStyle      = LineStyle.Solid,
            AxislineThickness  = 1,
            MajorGridlineStyle = LineStyle.None,
            MinorGridlineStyle = LineStyle.None,
            TicklineColor      = OxyColor.Parse("#1A1D2E"),
            TextColor          = OxyColor.Parse("#6B7280"),
            FontSize           = 11,
            Minimum            = 1,
            AbsoluteMinimum    = 1,
        };

        private static LinearAxis CreateBaseYAxis() => new()
        {
            Position           = AxisPosition.Left,
            AxislineColor      = OxyColor.Parse("#1A1D2E"),
            AxislineStyle      = LineStyle.Solid,
            AxislineThickness  = 1,
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColor.Parse("#1A1D2E"),
            MinorGridlineStyle = LineStyle.None,
            TicklineColor      = OxyColors.Transparent,
            TextColor          = OxyColor.Parse("#6B7280"),
            FontSize           = 11,
            StringFormat       = "₱#,0",
            AbsoluteMinimum    = 0,
        };

        private static void AddLineSeries(PlotModel model, List<SalesRow> data,
            Func<SalesRow, double> xSelector, string trackerFormat)
        {
            var area = new AreaSeries
            {
                Fill            = OxyColor.FromArgb(50, 18, 136, 84),
                Color           = OxyColors.Transparent,
                Color2          = OxyColors.Transparent,
                StrokeThickness = 0,
                RenderInLegend  = false,
            };
            var line = new LineSeries
            {
                Color                 = OxyColor.Parse("#10B981"),
                StrokeThickness       = 2.5,
                MarkerType            = MarkerType.Circle,
                MarkerSize            = 4.5,
                MarkerFill            = OxyColor.Parse("#10B981"),
                MarkerStroke          = OxyColor.Parse("#000000"),
                MarkerStrokeThickness = 1.5,
                LineStyle             = LineStyle.Solid,
                RenderInLegend        = false,
                TrackerFormatString   = trackerFormat,
            };
            foreach (var row in data)
            {
                double x = xSelector(row);
                double y = (double)row.TotalAmount;
                line.Points.Add(new DataPoint(x, y));
                area.Points.Add(new DataPoint(x, y));
                area.Points2.Add(new DataPoint(x, 0));
            }
            model.Series.Add(area);
            model.Series.Add(line);
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private static string ComputeTrend(decimal current, decimal previous,
                                           out SolidColorBrush brush)
        {
            if (previous == 0)
            {
                brush = MutedBrush;
                return current > 0 ? "First sale today!" : "";
            }
            double pct = (double)((current - previous) / previous * 100);
            if (pct > 0)      { brush = EmeraldBrush; return $"↑ {pct:F1}% vs yesterday"; }
            else if (pct < 0) { brush = RedBrush;     return $"↓ {Math.Abs(pct):F1}% vs yesterday"; }
            else              { brush = MutedBrush;   return "No change vs yesterday"; }
        }

        // ── INotifyPropertyChanged ─────────────────────────────────────────────
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
