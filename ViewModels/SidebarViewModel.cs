using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using SihyuPOSPayroll.Views.Admin.Attendance;
using SihyuPOSPayroll.Views.Admin.Categories;
using SihyuPOSPayroll.Views.Admin.Dashboard;
using SihyuPOSPayroll.Views.Admin.Employees;
using SihyuPOSPayroll.Views.Admin.Inventory;
using SihyuPOSPayroll.Views.Admin.Menu;
using SihyuPOSPayroll.Views.Admin.Orders;
using SihyuPOSPayroll.Views.Admin.Payroll;
using SihyuPOSPayroll.Views.Admin.Payslip_Requests;
using SihyuPOSPayroll.Views.Admin.Receipts;
using SihyuPOSPayroll.Views.Admin.Sales;
using SihyuPOSPayroll.Views.Admin.Settings;
using SihyuPOSPayroll.Views.Admin.Tables;
using SihyuPOSPayroll.Views.Admin.Users;
using SihyuPOSPayroll.Views.Cashier.Inventory;
using SihyuPOSPayroll.Views.Cashier.Orders;
using SihyuPOSPayroll.Views.Cashier.POS;
using SihyuPOSPayroll.Views.Cashier.Receipts;
using SihyuPOSPayroll.Views.Cashier.Tables;
using SihyuPOSPayroll.Views.Employee.Attendance;
using SihyuPOSPayroll.Views.Employee.Payslip;
using SihyuPOSPayroll.Views.Employee.Profile;

// alias the service types (same technique as your EmployeeProfileViewModel)
using IEmployeeService = SihyuPOSPayroll.Services.IEmployeeService;
using EmployeeService = SihyuPOSPayroll.Services.EmployeeService;

namespace SihyuPOSPayroll.ViewModels
{
    // ── Sidebar menu data models ───────────────────────────────────────────────
    public class SidebarMenuGroup
    {
        public string Header { get; set; } = string.Empty;        // e.g. "INFRASTRUCTURE"
        public List<SidebarMenuItem> Items { get; set; } = new();
    }

    public class SidebarMenuItem
    {
        public string Label   { get; set; } = string.Empty;       // e.g. "Dashboard"
        public string Icon    { get; set; } = string.Empty;       // Segoe MDL2 glyph e.g. "\uE80F"
        public ICommand? Command { get; set; }                    // Navigate(Label)
    }

    // ──────────────────────────────────────────────────────────────────────────
    public class SidebarViewModel : INotifyPropertyChanged
    {
        // -----------------------------
        // Dependencies / identity
        // -----------------------------
        private readonly IEmployeeService _service;
        private int _employeeId;             // authoritative id used for DB fetch
        private string _role = string.Empty; // normalized role from DB

        // -----------------------------
        // Header state (bound in XAML)
        // -----------------------------
        private string _userRole = string.Empty;
        private string _userName = string.Empty;
        private ImageSource? _userAvatarImage;

        // -----------------------------
        // UI state
        // -----------------------------
        private string _selectedMenuItem = string.Empty;
        private UserControl _currentView = new UserControl();

        public SidebarViewModel(int employeeId, IEmployeeService? service = null)
        {
            _service = service ?? new EmployeeService();
            MenuItems = new ObservableCollection<string>();
            MenuGroups = new ObservableCollection<SidebarMenuGroup>();
            NavigateCommand = new RelayCommand<string?>(Navigate);
            ReloadProfileCommand = new RelayCommand<object?>(_ => LoadEmployee(_employeeId));
            RefreshAvatarCommand = new RelayCommand<object?>(_ => RefreshAvatar());

            // Subscribe to settings changes so the sidebar refreshes when an admin saves settings
            SettingsService.Instance.SettingsChanged += InitializeMenuItems;

            // Seed _role from the active session before LoadEmployee runs.
            // This ensures the fast-path (id <= 0) uses the correct role rather
            // than falling back to "EMPLOYEE" when the admin has no employee record.
            if (!string.IsNullOrWhiteSpace(Helpers.Session.CurrentUserRole))
                _role = Helpers.Session.CurrentUserRole.Trim().ToUpperInvariant();

            _employeeId = employeeId;
            LoadEmployee(_employeeId); // pulls name/role/avatar from DB and sets menu + default view
        }

        // Convenience overload: allow constructing with a UserModel too
        public SidebarViewModel(UserModel user, IEmployeeService? service = null)
            : this(user?.Employee?.Id ?? 0, service)
        { }

        // -----------------------------
        // Bindable properties
        // -----------------------------
        public string UserRole
        {
            get => _userRole;
            private set { _userRole = value; OnPropertyChanged(); }
        }

        public string UserName
        {
            get => _userName;
            private set { _userName = value; OnPropertyChanged(); }
        }

        public ImageSource? UserAvatarImage
        {
            get => _userAvatarImage;
            private set { _userAvatarImage = value; OnPropertyChanged(); }
        }

        public string SelectedMenuItem
        {
            get => _selectedMenuItem;
            set
            {
                if (_selectedMenuItem == value) return;
                _selectedMenuItem = value ?? string.Empty;
                OnPropertyChanged();
                if (!string.IsNullOrWhiteSpace(_selectedMenuItem))
                    Navigate(_selectedMenuItem);
            }
        }

        public UserControl CurrentView
        {
            get => _currentView;
            set { _currentView = value ?? throw new ArgumentNullException(nameof(CurrentView)); OnPropertyChanged(); }
        }

        public ObservableCollection<string> MenuItems { get; }
        public ObservableCollection<SidebarMenuGroup> MenuGroups { get; }
        public ICommand NavigateCommand { get; }
        public ICommand ReloadProfileCommand { get; }
        public ICommand RefreshAvatarCommand { get; }

        // -----------------------------
        // Data fetch (same approach as EmployeeProfileViewModel)
        // -----------------------------
        private void LoadEmployee(int id)
        {
            if (id <= 0)
            {
                // fallback visuals (e.g., in designer or if no employee mapped)
                ApplyHeader(null);
                InitializeMenuItems(); // with whatever _role currently is (may be empty)
                SetDefaultView();
                return;
            }

            try
            {
                var one = _service.GetEmployeeById(id);
                if (one == null)
                {
                    // gentle fallback if service shape differs
                    var list = _service.GetAllEmployees();
                    one = list.Find(e => e.Id == id);
                }

                ApplyHeader(one);
                InitializeMenuItems();
                SetDefaultView();
            }
            catch (Exception ex)
            {
                // minimal error surfacing in sidebar; you can expand if desired
                ApplyHeader(null);
                InitializeMenuItems();
                SetDefaultView();
                System.Diagnostics.Debug.WriteLine($"Sidebar LoadEmployee failed: {ex.Message}");
            }
        }

        private void ApplyHeader(EmployeeModel? emp)
        {
            // Derive name: employee full name → session email (strip @domain) → role
            if (!string.IsNullOrWhiteSpace(emp?.FullName))
                UserName = emp!.FullName;
            else if (!string.IsNullOrWhiteSpace(Helpers.Session.CurrentUser?.Email))
                UserName = Helpers.Session.CurrentUser.Email.Split('@')[0];
            else
                UserName = Helpers.Session.CurrentUserRole is { Length: > 0 } r ? r : "User";

            // Derive role: employee's linked user account → in-memory Session → previous _role → "EMPLOYEE"
            var roleRaw = emp?.UserAccount?.Role
                       ?? Helpers.Session.CurrentUserRole
                       ?? _role;
            _role = (roleRaw ?? "EMPLOYEE").Trim().ToUpperInvariant();
            UserRole = _role;

            // Derive avatar
            var path = emp?.ImageUrl;
            UserAvatarImage = LoadImageOrFallback(path);
        }

        private static ImageSource LoadImageOrFallback(string? path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    return MakePlaceholderAvatar();

                var uriKind = Uri.IsWellFormedUriString(path, UriKind.Absolute)
                    ? UriKind.Absolute
                    : UriKind.RelativeOrAbsolute;

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path, uriKind);
                bmp.CacheOption = BitmapCacheOption.OnLoad;           // avoid file locking
                bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return MakePlaceholderAvatar();
            }
        }

        private static ImageSource MakePlaceholderAvatar()
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
                dc.DrawRectangle(Brushes.Gray, null, new Rect(0, 0, 64, 64));

            var rtb = new RenderTargetBitmap(64, 64, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        // allow external callers to just refresh the picture without a full DB roundtrip
        public void RefreshAvatar(string? newImagePath = null)
        {
            UserAvatarImage = LoadImageOrFallback(newImagePath);
        }

        // -----------------------------
        // Menu / Navigation
        // -----------------------------

        /// <summary>
        /// All possible sidebar items, organised into their display groups.
        /// The module key in the tuple matches role_permissions.module_key exactly.
        /// Admin-only items (Permissions, Settings) are handled separately.
        /// </summary>
        private static readonly (string Label, string Icon, string Group, string ModuleKey)[] AllModuleItems =
        {
            // INFRASTRUCTURE
            ("Dashboard",        "\uE80F", "INFRASTRUCTURE", "Dashboard"),
            ("Inventory",        "\uE8B4", "INFRASTRUCTURE", "Inventory"),
            ("Categories",       "\uE8FD", "INFRASTRUCTURE", "Categories"),

            // ADMIN TOOLS
            ("Users",            "\uE716", "ADMIN TOOLS",    "Users"),
            ("Employees",        "\uE716", "ADMIN TOOLS",    "Employees"),
            ("Positions",        "\uE821", "ADMIN TOOLS",    "Positions"),

            // FINANCIALS
            ("POS",              "\uE8C9", "FINANCIALS",     "POS"),
            ("Orders",           "\uE8A5", "FINANCIALS",     "Orders"),
            ("Receipts",         "\uE9F9", "FINANCIALS",     "Receipts"),
        };

        private void InitializeMenuItems()
        {
            MenuItems.Clear();
            MenuGroups.Clear();

            // ── Determine which modules this role may see ──────────────────────
            // Admin always gets everything (including Permissions).
            // All other roles get only what was granted in role_permissions.
            HashSet<string> granted;
            if (IsAdmin)
            {
                granted = new HashSet<string>(
                    AllModuleItems.Select(m => m.ModuleKey),
                    StringComparer.OrdinalIgnoreCase);
                // Admin always keeps Permissions management
                granted.Add("Permissions");
            }
            else
            {
                var svc = new Services.RoleService();
                // Use the original role name (not uppercased) for the DB lookup
                var roleName = Helpers.Session.CurrentUser?.Role
                               ?? (_role.Length > 0
                                   ? char.ToUpper(_role[0]) + _role.Substring(1).ToLower()
                                   : string.Empty);
                granted = svc.GetModulesForRole(roleName);
            }

            // ── Build groups from AllModuleItems filtered by granted set ───────
            var groupMap = new Dictionary<string, SidebarMenuGroup>(StringComparer.Ordinal);

            foreach (var (label, icon, group, moduleKey) in AllModuleItems)
            {
                if (!granted.Contains(moduleKey)) continue;

                if (!groupMap.TryGetValue(group, out var g))
                {
                    g = new SidebarMenuGroup { Header = group };
                    groupMap[group] = g;
                }
                g.Items.Add(MakeItem(label, icon));
                MenuItems.Add(label);
            }

            // Insert groups in canonical order
            foreach (var header in new[] { "INFRASTRUCTURE", "ADMIN TOOLS", "FINANCIALS" })
            {
                if (groupMap.TryGetValue(header, out var g) && g.Items.Count > 0)
                    MenuGroups.Add(g);
            }

            // Admin gets the Permissions item in ADMIN TOOLS
            if (IsAdmin)
            {
                var adminGroup = MenuGroups.FirstOrDefault(g =>
                    g.Header == "ADMIN TOOLS");
                if (adminGroup != null)
                {
                    adminGroup.Items.Insert(0, MakeItem("Permissions", "\uE8D7"));
                    MenuItems.Add("Permissions");
                }
                else
                {
                    // Create the group if it was somehow empty
                    MenuGroups.Add(new SidebarMenuGroup
                    {
                        Header = "ADMIN TOOLS",
                        Items  = new List<SidebarMenuItem> { MakeItem("Permissions", "\uE8D7") }
                    });
                    MenuItems.Add("Permissions");
                }
            }

            // Always add ACCOUNT group with Logout
            MenuGroups.Add(new SidebarMenuGroup
            {
                Header = "ACCOUNT",
                Items  = new List<SidebarMenuItem> { MakeItem("Logout", "\uF3B1") }
            });
            MenuItems.Add("Logout");

            // ── Navigate away if current view is no longer accessible ──────────
            if (_currentView != null &&
                !(_currentView is UserControl uc && uc.Content == null) &&
                !(_currentView is Dashboard))
            {
                string currentKey = _currentView switch
                {
                    Dashboard                        => "Dashboard",
                    Views.Admin.Users.Users          => "Users",
                    Employees                        => "Employees",
                    Views.Admin.Positions.Positions  => "Positions",
                    Views.Admin.Permissions.Permissions => "Permissions",
                    Inventory                        => "Inventory",
                    Views.Admin.Categories.CategoriesView => "Categories",
                    Orders                           => "Orders",
                    Receipts                         => "Receipts",
                    Views.Cashier.POS.POSView        => "POS",
                    _                                => string.Empty,
                };
                if (!string.IsNullOrEmpty(currentKey) && !granted.Contains(currentKey))
                    SetDefaultView();
            }
        }

        /// <summary>Creates a SidebarMenuItem wired to Navigate(label).</summary>
        private SidebarMenuItem MakeItem(string label, string icon) => new SidebarMenuItem
        {
            Label   = label,
            Icon    = icon,
            Command = new RelayCommand<object?>(_ => Navigate(label)),
        };

        /// <summary>
        /// Maps a sidebar label to the corresponding module key used in role_permissions.
        /// Returns an empty string for items that are never permission-gated (Logout).
        /// </summary>
        private static string LabelToModuleKey(string label) => label.Trim() switch
        {
            "Dashboard"       => "Dashboard",
            "Users"           => "Users",
            "Employees"       => "Employees",
            "Permissions"     => "Permissions",
            "Positions"       => "Positions",
            "Inventory"       => "Inventory",
            "Categories"      => "Categories",
            "Orders"          => "Orders",
            "Receipts"        => "Receipts",
            "POS"             => "POS",
            _                 => string.Empty,
        };

        private bool IsCurrentViewExcluded(IReadOnlyDictionary<string, bool> visibility)
        {
            if (_currentView is Dashboard || _currentView is SettingsView)
                return false;

            string moduleKey = _currentView switch
            {
                Views.Admin.Users.Users          => "Users",
                Employees                        => "Employees",
                Payroll                          => "Payroll",
                Payslip                          => "PayslipRequests",
                AttendanceAdminView              => "Attendance",
                MenuView                         => "Menu",
                Inventory                        => "Inventory",
                Orders                           => "Orders",
                Receipts                         => "Receipts",
                Tables                           => "Tables",
                Sales                            => "Sales",
                _                                => string.Empty,
            };

            if (string.IsNullOrEmpty(moduleKey)) return false;
            return visibility.TryGetValue(moduleKey, out bool isEnabled) && !isEnabled;
        }

        private void SetDefaultView()
        {
            // First granted module wins; Dashboard > POS > first available > blank
            var svc      = new Services.RoleService();
            var roleName = Helpers.Session.CurrentUser?.Role
                           ?? (_role.Length > 0
                               ? char.ToUpper(_role[0]) + _role.Substring(1).ToLower()
                               : string.Empty);
            var granted  = IsAdmin
                ? new HashSet<string>(AllModuleItems.Select(m => m.ModuleKey), StringComparer.OrdinalIgnoreCase)
                : svc.GetModulesForRole(roleName);

            if (granted.Contains("Dashboard"))     { CurrentView = new Dashboard();  return; }
            if (granted.Contains("POS"))           { CurrentView = new Views.Cashier.POS.POSView(); return; }
            if (granted.Contains("Inventory"))     { CurrentView = new Inventory();   return; }
            if (granted.Contains("Orders"))        { CurrentView = new Orders();      return; }
            if (granted.Contains("Receipts"))      { CurrentView = new Receipts();    return; }
            if (granted.Contains("Users"))         { CurrentView = new Views.Admin.Users.Users(); return; }
            if (granted.Contains("Employees"))     { CurrentView = new Employees();   return; }

            CurrentView = new UserControl();  // fallback – no modules granted
        }

        public void Navigate(string? menuItem)
        {
            switch (menuItem?.Trim().ToLowerInvariant())
            {
                case "dashboard":
                    CurrentView = new Dashboard();
                    break;

                case "users":
                    CurrentView = new Views.Admin.Users.Users();
                    break;

                case "employees":
                    CurrentView = new Employees();
                    break;

                case "permissions":
                    if (IsAdmin) CurrentView = new Views.Admin.Permissions.Permissions();
                    break;

                case "positions":
                    CurrentView = new Views.Admin.Positions.Positions();
                    break;

                case "inventory":
                    CurrentView = new Inventory();
                    break;

                case "categories":
                    CurrentView = new Views.Admin.Categories.CategoriesView();
                    break;

                case "orders":
                    CurrentView = new Orders();
                    break;

                case "receipts":
                    CurrentView = new Receipts();
                    break;

                case "pos":
                    CurrentView = new Views.Cashier.POS.POSView();
                    break;

                case "logout":
                    LogoutToMainWindow();
                    break;
            }
        }

        private bool IsAdmin    => _role == "ADMIN";
        private bool IsCashier  => _role == "CASHIER";
        private bool IsEmployee => _role == "EMPLOYEE";

        private UserControl MakeEmployeeProfileViewOrFallback()
        {
            if (_employeeId > 0)
                return new ProfileView(_employeeId);

            var border = new Border
            {
                Padding = new Thickness(24),
                Child = new System.Windows.Controls.TextBlock
                {
                    Text = "No employee record was found for the current user.",
                    FontSize = 16,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            return new UserControl { Content = border };
        }

        private static void LogoutToMainWindow()
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();

            foreach (Window window in Application.Current.Windows)
            {
                if (window != mainWindow)
                {
                    window.Close();
                    break;
                }
            }
        }

        // -----------------------------
        // INotifyPropertyChanged
        // -----------------------------
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // your RelayCommand<T> stays the same
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute((T?)parameter);
        public void Execute(object? parameter) => _execute((T?)parameter);

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
