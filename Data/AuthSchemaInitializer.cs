#nullable enable
using Microsoft.Data.Sqlite;
using System;
using System.Diagnostics;

namespace SihyuPOSPayroll.Data
{
    /// <summary>
    /// Creates the complete SQLite schema for all migrated modules and seeds
    /// default data on first run. Call <see cref="EnsureSchemaAtStartup"/> once
    /// from <c>App.xaml.cs</c> before any view is constructed.
    ///
    /// Modules covered:
    ///   Auth      — roles, users, login_sessions
    ///   Employees — employees, work_schedule, position_salary
    ///   Inventory — inventory_items, inventory_categories
    ///   Menu      — menu
    ///   POS       — cafe_tables, orders, order_items, receipts
    ///
    /// SQLite type mapping (MySQL → SQLite):
    ///   AUTO_INCREMENT / BIGINT UNSIGNED  → INTEGER PRIMARY KEY AUTOINCREMENT
    ///   TINYINT(1)                        → INTEGER  (0 = false, 1 = true)
    ///   DATETIME / TIMESTAMP              → TEXT     (ISO-8601: "YYYY-MM-DD HH:MM:SS")
    ///   DATE                              → TEXT     ("YYYY-MM-DD")
    ///   TIME                              → TEXT     ("HH:MM:SS")
    ///   DECIMAL(p,s)                      → REAL
    ///   VARCHAR(n) / TEXT                 → TEXT
    /// </summary>
    public static class AuthSchemaInitializer
    {
        private static bool _initialized = false;

        /// <summary>Idempotent — safe to call more than once.</summary>
        public static void EnsureSchemaAtStartup()
        {
            if (_initialized) return;

            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                CreateTables(conn);
                SeedRoles(conn);
                SeedDefaultAdmin(conn);
                SeedSettings(conn);
                _initialized = true;
                Debug.WriteLine("[Schema] All tables ensured.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Schema] Initialization error: {ex.Message}");
                throw;
            }
        }

        // ── DDL ───────────────────────────────────────────────────────────────

        private static void CreateTables(SqliteConnection conn)
        {
            // Execute each group separately so a failure in one block is easy to trace.
            ExecuteSql(conn, AuthTables,      "Auth");
            ExecuteSql(conn, EmployeeTables,  "Employees");
            ExecuteSql(conn, InventoryTables, "Inventory");
            ExecuteSql(conn, MenuTables,      "Menu");
            ExecuteSql(conn, POSTables,       "POS");
            ExecuteSql(conn, SettingsTables,  "Settings");
            MigrateOrderTypeColumn(conn);
        }

        private static void ExecuteSql(SqliteConnection conn, string sql, string label)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
            Debug.WriteLine($"[Schema] {label} tables ensured.");
        }

        // ── Auth ──────────────────────────────────────────────────────────────

        private const string AuthTables = @"
            -- ── roles ────────────────────────────────────────────────────────
            CREATE TABLE IF NOT EXISTS roles (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                name        TEXT    NOT NULL UNIQUE,
                description TEXT    NOT NULL DEFAULT ''
            );

            -- ── employees (full schema — also satisfies AuthService LEFT JOIN) ──
            CREATE TABLE IF NOT EXISTS employees (
                id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                full_name           TEXT    NOT NULL,
                age                 INTEGER,
                sex                 TEXT,
                address             TEXT,
                birthday            TEXT,                -- DATE as TEXT 'YYYY-MM-DD'
                contact_number      TEXT,
                position            TEXT,
                salary_per_day      REAL,
                work_schedule_id    INTEGER,
                shift               TEXT,
                sss_number          TEXT,
                philhealth_number   TEXT,
                pagibig_number      TEXT,
                image_url           TEXT,
                emergency_contact   TEXT,
                date_hired          TEXT,                -- DATE as TEXT 'YYYY-MM-DD'
                is_active           INTEGER NOT NULL DEFAULT 1,
                created_at          TEXT    NOT NULL DEFAULT (datetime('now'))
            );

            CREATE INDEX IF NOT EXISTS idx_employees_position ON employees(position);
            CREATE INDEX IF NOT EXISTS idx_employees_schedule ON employees(work_schedule_id);

            -- ── users ──────────────────────────────────────────────────────────
            CREATE TABLE IF NOT EXISTS users (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                email       TEXT    NOT NULL UNIQUE COLLATE NOCASE,
                password    TEXT    NOT NULL,
                role        TEXT    NOT NULL DEFAULT 'Employee',
                employee_id INTEGER,
                is_active   INTEGER NOT NULL DEFAULT 1,
                created_at  TEXT    NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (employee_id) REFERENCES employees(id) ON DELETE SET NULL
            );

            CREATE INDEX IF NOT EXISTS idx_users_email     ON users(email);
            CREATE INDEX IF NOT EXISTS idx_users_role      ON users(role);
            CREATE INDEX IF NOT EXISTS idx_users_is_active ON users(is_active);

            -- ── login_sessions ─────────────────────────────────────────────────
            CREATE TABLE IF NOT EXISTS login_sessions (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id       INTEGER NOT NULL,
                token         TEXT    NOT NULL UNIQUE,
                logged_in_at  TEXT    NOT NULL DEFAULT (datetime('now')),
                logged_out_at TEXT,
                FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_sessions_user_id ON login_sessions(user_id);
            CREATE INDEX IF NOT EXISTS idx_sessions_token   ON login_sessions(token);
        ";

        // ── Employees ─────────────────────────────────────────────────────────

        private const string EmployeeTables = @"
            -- ── work_schedule ─────────────────────────────────────────────────
            CREATE TABLE IF NOT EXISTS work_schedule (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                label      TEXT    NOT NULL UNIQUE,
                days_mask  INTEGER NOT NULL DEFAULT 0,   -- bitmask: Sun=1,Mon=2,Tue=4,...
                is_active  INTEGER NOT NULL DEFAULT 1,
                updated_at TEXT    NOT NULL DEFAULT (datetime('now'))
            );

            CREATE INDEX IF NOT EXISTS idx_ws_label  ON work_schedule(label);
            CREATE INDEX IF NOT EXISTS idx_ws_active ON work_schedule(is_active);

            -- ── position_salary ────────────────────────────────────────────────
            CREATE TABLE IF NOT EXISTS position_salary (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                position   TEXT NOT NULL UNIQUE,
                daily_rate REAL NOT NULL DEFAULT 0,
                is_active  INTEGER NOT NULL DEFAULT 1,
                updated_at TEXT    NOT NULL DEFAULT (datetime('now'))
            );

            CREATE INDEX IF NOT EXISTS idx_ps_position ON position_salary(position);
            CREATE INDEX IF NOT EXISTS idx_ps_active   ON position_salary(is_active);
        ";

        // ── Inventory ─────────────────────────────────────────────────────────

        private const string InventoryTables = @"
            -- ── inventory_categories ──────────────────────────────────────────
            CREATE TABLE IF NOT EXISTS inventory_categories (
                Id   INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT    NOT NULL UNIQUE
            );

            -- ── inventory_items ────────────────────────────────────────────────
            -- Column names are PascalCase to match the existing InventoryService queries.
            CREATE TABLE IF NOT EXISTS inventory_items (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                Barcode      TEXT,
                ProductName  TEXT    NOT NULL,
                CategoryName TEXT,
                Quantity     INTEGER NOT NULL DEFAULT 0,
                Price        REAL    NOT NULL DEFAULT 0,
                ExpiryDate   TEXT,                       -- DATE as TEXT 'YYYY-MM-DD'
                ImagePath    TEXT,
                CreatedAt    TEXT    NOT NULL DEFAULT (datetime('now')),
                UpdatedAt    TEXT    NOT NULL DEFAULT (datetime('now'))
            );

            CREATE INDEX IF NOT EXISTS idx_inv_barcode  ON inventory_items(Barcode);
            CREATE INDEX IF NOT EXISTS idx_inv_product  ON inventory_items(ProductName);
            CREATE INDEX IF NOT EXISTS idx_inv_category ON inventory_items(CategoryName);
            CREATE INDEX IF NOT EXISTS idx_inv_expiry   ON inventory_items(ExpiryDate);
        ";

        // ── Menu ──────────────────────────────────────────────────────────────

        private const string MenuTables = @"
            -- ── menu ──────────────────────────────────────────────────────────
            -- Column names match the existing MenuService queries exactly.
            CREATE TABLE IF NOT EXISTS menu (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Name        TEXT    NOT NULL,
                Category    TEXT,
                Price       REAL,
                image_url   TEXT,
                Description TEXT,
                Created_At  TEXT    NOT NULL DEFAULT (datetime('now'))
            );

            CREATE INDEX IF NOT EXISTS idx_menu_name     ON menu(Name);
            CREATE INDEX IF NOT EXISTS idx_menu_category ON menu(Category);
        ";

        // ── Settings ─────────────────────────────────────────────────────────

        private const string SettingsTables = @"
            -- ── SystemSettings ────────────────────────────────────────────────
            CREATE TABLE IF NOT EXISTS SystemSettings (
                SettingKey   TEXT NOT NULL PRIMARY KEY,
                SettingValue TEXT NOT NULL
            );

            -- ── ModuleVisibility ───────────────────────────────────────────────
            CREATE TABLE IF NOT EXISTS ModuleVisibility (
                ModuleName TEXT    NOT NULL PRIMARY KEY,
                IsEnabled  INTEGER NOT NULL DEFAULT 1   -- 1 = visible, 0 = hidden
            );

            -- ── IngredientRecipes ──────────────────────────────────────────────
            CREATE TABLE IF NOT EXISTS IngredientRecipes (
                Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
                MenuItemId         INTEGER NOT NULL,
                IngredientId       INTEGER NOT NULL,
                QuantityPerServing REAL    NOT NULL,
                UNIQUE (MenuItemId, IngredientId),
                FOREIGN KEY (MenuItemId)   REFERENCES menu(Id)             ON DELETE CASCADE,
                FOREIGN KEY (IngredientId) REFERENCES inventory_items(Id)  ON DELETE CASCADE
            );

            -- ── IngredientBatches ──────────────────────────────────────────────
            CREATE TABLE IF NOT EXISTS IngredientBatches (
                Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                IngredientId  INTEGER NOT NULL,
                BatchQuantity REAL    NOT NULL,
                YieldPerBatch REAL    NOT NULL,
                CreatedAt     TEXT    NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (IngredientId) REFERENCES inventory_items(Id) ON DELETE CASCADE
            );

            -- ── PackagingMaterials ─────────────────────────────────────────────
            CREATE TABLE IF NOT EXISTS PackagingMaterials (
                PackagingMaterialId INTEGER PRIMARY KEY AUTOINCREMENT,
                MenuItemId          INTEGER NOT NULL,
                InventoryItemId     INTEGER NOT NULL,
                QuantityPerOrder    INTEGER NOT NULL DEFAULT 1,
                FOREIGN KEY (MenuItemId)      REFERENCES menu(Id)             ON DELETE CASCADE,
                FOREIGN KEY (InventoryItemId) REFERENCES inventory_items(Id)  ON DELETE RESTRICT
            );

            -- ── app_settings ───────────────────────────────────────────────────
            CREATE TABLE IF NOT EXISTS app_settings (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                user        TEXT    NOT NULL DEFAULT 'global',
                page        TEXT    NOT NULL,
                column_name TEXT    NOT NULL,
                is_show     INTEGER NOT NULL DEFAULT 1,
                UNIQUE (user, page, column_name)
            );
        ";

        // ── POS ───────────────────────────────────────────────────────────────

        private const string POSTables = @"
            -- ── cafe_tables ───────────────────────────────────────────────────
            CREATE TABLE IF NOT EXISTS cafe_tables (
                id             INTEGER PRIMARY KEY AUTOINCREMENT,
                table_number   TEXT    NOT NULL UNIQUE,
                is_available   INTEGER NOT NULL DEFAULT 1
            );

            CREATE INDEX IF NOT EXISTS idx_ct_number    ON cafe_tables(table_number);
            CREATE INDEX IF NOT EXISTS idx_ct_available ON cafe_tables(is_available);

            -- ── orders ────────────────────────────────────────────────────────
            CREATE TABLE IF NOT EXISTS orders (
                id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                customer_id         INTEGER,
                table_number        TEXT,
                total_amount        REAL    NOT NULL DEFAULT 0,
                payment_status      TEXT    NOT NULL DEFAULT 'Unpaid',
                order_status        TEXT    NOT NULL DEFAULT 'Pending',
                cash_register_id    INTEGER,
                ordered_by_user_id  INTEGER,
                created_at          TEXT    NOT NULL DEFAULT (datetime('now'))
            );

            CREATE INDEX IF NOT EXISTS idx_ord_table    ON orders(table_number);
            CREATE INDEX IF NOT EXISTS idx_ord_payment  ON orders(payment_status);
            CREATE INDEX IF NOT EXISTS idx_ord_status   ON orders(order_status);
            CREATE INDEX IF NOT EXISTS idx_ord_created  ON orders(created_at);

            -- ── order_items ────────────────────────────────────────────────────
            CREATE TABLE IF NOT EXISTS order_items (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                order_id   INTEGER NOT NULL,
                product_id INTEGER NOT NULL,
                quantity   INTEGER NOT NULL DEFAULT 1,
                unit_price REAL    NOT NULL,
                FOREIGN KEY (order_id) REFERENCES orders(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_oi_order   ON order_items(order_id);
            CREATE INDEX IF NOT EXISTS idx_oi_product ON order_items(product_id);

            -- ── receipts ──────────────────────────────────────────────────────
            CREATE TABLE IF NOT EXISTS receipts (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                order_id    INTEGER NOT NULL,
                amount_paid REAL    NOT NULL,
                issued_at   TEXT    NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (order_id) REFERENCES orders(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_rec_order   ON receipts(order_id);
            CREATE INDEX IF NOT EXISTS idx_rec_issued  ON receipts(issued_at);
        ";

        /// <summary>
        /// Adds the order_type column to orders if it doesn't exist yet.
        /// SQLite has no ALTER TABLE ... ADD COLUMN IF NOT EXISTS, so we attempt
        /// the ALTER and swallow the "duplicate column" error silently.
        /// </summary>
        private static void MigrateOrderTypeColumn(SqliteConnection conn)
        {
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "ALTER TABLE orders ADD COLUMN order_type TEXT NOT NULL DEFAULT 'NotApplicable';";
                cmd.ExecuteNonQuery();
                Debug.WriteLine("[Schema] orders.order_type column added.");
            }
            catch
            {
                // Column already exists — safe to ignore.
            }
        }

        // ── Seed data ─────────────────────────────────────────────────────────

        private static void SeedRoles(SqliteConnection conn)
        {
            const string sql = @"
                INSERT OR IGNORE INTO roles (name, description) VALUES
                    ('Admin',    'Full access to all modules'),
                    ('Cashier',  'POS, Orders, and Inventory read access'),
                    ('Employee', 'Self-service: profile, attendance, payslip');
            ";
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var affected = cmd.ExecuteNonQuery();
            if (affected > 0)
                Debug.WriteLine($"[Schema] Seeded {affected} role(s).");
        }

        /// <summary>
        /// Seeds a default Admin account only when the users table is empty.
        /// Credentials: admin@sihyupos.com / admin123
        /// </summary>
        private static void SeedDefaultAdmin(SqliteConnection conn)
        {
            using var countCmd = conn.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM users;";
            var count = Convert.ToInt64(countCmd.ExecuteScalar() ?? 0L);
            if (count > 0) return;

            var hash = BCrypt.Net.BCrypt.HashPassword("admin123", workFactor: 12);

            using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO users (email, password, role, is_active, created_at)
                VALUES (@email, @password, 'Admin', 1, datetime('now'));";
            insertCmd.Parameters.AddWithValue("@email",    "admin@sihyupos.com");
            insertCmd.Parameters.AddWithValue("@password", hash);
            insertCmd.ExecuteNonQuery();

            Debug.WriteLine("[Schema] Default admin seeded: admin@sihyupos.com");
        }

        /// <summary>Seeds SystemMode and all 13 default module visibility rows.</summary>
        private static void SeedSettings(SqliteConnection conn)
        {
            // SystemMode default
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT OR IGNORE INTO SystemSettings (SettingKey, SettingValue)
                    VALUES ('SystemMode', 'RestaurantMode');";
                cmd.ExecuteNonQuery();
            }

            // Module visibility defaults
            string[] modules =
            {
                "Attendance", "Dashboard", "Employees", "Inventory",
                "Menu", "Orders", "Payroll", "PayslipRequests",
                "Receipts", "Sales", "Tables", "Users", "Settings"
            };

            using var insert = conn.CreateCommand();
            insert.CommandText = "INSERT OR IGNORE INTO ModuleVisibility (ModuleName, IsEnabled) VALUES (@name, 1);";
            var param = insert.Parameters.Add("@name", Microsoft.Data.Sqlite.SqliteType.Text);
            foreach (var m in modules)
            {
                param.Value = m;
                insert.ExecuteNonQuery();
            }

            Debug.WriteLine("[Schema] Settings and ModuleVisibility seeded.");
        }
    }
}
