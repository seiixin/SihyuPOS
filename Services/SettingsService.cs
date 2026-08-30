#nullable enable
using Microsoft.Data.Sqlite;
using SihyuPOSPayroll.Data;
using SihyuPOSPayroll.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SihyuPOSPayroll.Services
{
    /// <summary>
    /// Singleton service that persists and retrieves SystemMode and ModuleVisibility
    /// from the SQLite database. Schema is created by
    /// <see cref="AuthSchemaInitializer.EnsureSchemaAtStartup"/>.
    ///
    /// Call <see cref="EnsureSchemaAtStartup"/> once from App.xaml.cs (now a no-op
    /// kept for API compatibility), then call <see cref="Instance"/>.Load().
    /// </summary>
    public class SettingsService
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        public static readonly SettingsService Instance = new SettingsService();
        private SettingsService() { }

        // ── In-memory state ───────────────────────────────────────────────────
        public SystemMode CurrentMode { get; private set; } = SystemMode.StoreMode;

        private Dictionary<string, bool> _visibility =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, bool> ModuleVisibility => _visibility;

        // ── Events ────────────────────────────────────────────────────────────
        /// <summary>
        /// Raised after a successful <see cref="Save"/>; subscribers (e.g. SidebarViewModel)
        /// should call InitializeMenuItems() to refresh their MenuGroups.
        /// </summary>
        public event Action? SettingsChanged;

        // ── Schema (no-op — handled by AuthSchemaInitializer) ─────────────────
        /// <summary>
        /// No-op. Schema is created by <see cref="AuthSchemaInitializer.EnsureSchemaAtStartup"/>.
        /// Kept for API compatibility with App.xaml.cs.
        /// </summary>
        public static void EnsureSchemaAtStartup()
        {
            // Tables are created + seeded by AuthSchemaInitializer.
            // Nothing to do here.
            Debug.WriteLine("[SettingsService] EnsureSchemaAtStartup — delegated to AuthSchemaInitializer.");
        }

        // ── Load ──────────────────────────────────────────────────────────────
        /// <summary>
        /// Reads SystemMode and ModuleVisibility from SQLite into the in-memory cache.
        /// On any DB error falls back to defaults (RestaurantMode, all modules enabled).
        /// </summary>
        public void Load()
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();

                // Read SystemMode
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "SELECT SettingValue FROM SystemSettings WHERE SettingKey = 'SystemMode' LIMIT 1;";
                    var raw = cmd.ExecuteScalar() as string;
                    CurrentMode = ParseSystemMode(raw);
                }

                // Read ModuleVisibility
                var visibility = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT ModuleName, IsEnabled FROM ModuleVisibility;";
                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        string name    = rdr.GetString(0);
                        bool   enabled = rdr.GetInt32(1) == 1;
                        visibility[name] = enabled;
                    }
                }

                EnsureDefaultModulesInCache(visibility);
                _visibility = visibility;

                Debug.WriteLine($"[SettingsService] Loaded. Mode={CurrentMode}, Modules={_visibility.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsService] Load error — using defaults: {ex.Message}");
                CurrentMode  = SystemMode.StoreMode;
                _visibility  = BuildDefaultVisibility();
            }
        }

        // overload kept for API compat (App.xaml.cs passes no arg now, but just in case)
        public void Load(string _ ) => Load();

        // ── Save ──────────────────────────────────────────────────────────────
        /// <summary>
        /// Persists mode + module states in a single SQLite transaction.
        /// Updates the in-memory cache and raises <see cref="SettingsChanged"/> on success.
        /// </summary>
        public void Save(SystemMode mode, IEnumerable<ModuleConfig> modules)
        {
            using var conn = SqliteConnectionFactory.CreateOpenConnection();
            using var tx   = conn.BeginTransaction();

            try
            {
                // Upsert SystemMode
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        INSERT INTO SystemSettings (SettingKey, SettingValue)
                        VALUES ('SystemMode', @val)
                        ON CONFLICT(SettingKey) DO UPDATE SET SettingValue = excluded.SettingValue;";
                    cmd.Parameters.AddWithValue("@val", mode.ToString());
                    cmd.ExecuteNonQuery();
                }

                // Upsert each module
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        INSERT INTO ModuleVisibility (ModuleName, IsEnabled)
                        VALUES (@name, @enabled)
                        ON CONFLICT(ModuleName) DO UPDATE SET IsEnabled = excluded.IsEnabled;";
                    var pName    = cmd.Parameters.Add("@name",    SqliteType.Text);
                    var pEnabled = cmd.Parameters.Add("@enabled", SqliteType.Integer);

                    foreach (var m in modules)
                    {
                        pName.Value    = m.ModuleName;
                        pEnabled.Value = m.IsEnabled ? 1 : 0;
                        cmd.ExecuteNonQuery();
                    }
                }

                tx.Commit();

                // Update in-memory cache
                CurrentMode = mode;
                var newVis = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in modules)
                    newVis[m.ModuleName] = m.IsEnabled;
                EnsureDefaultModulesInCache(newVis);
                _visibility = newVis;

                NotifyChanged();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // overload for callers that still pass a connection string (ignored)
        public void Save(SystemMode mode, IEnumerable<ModuleConfig> modules, string _)
            => Save(mode, modules);

        // ── NotifyChanged ─────────────────────────────────────────────────────
        internal void NotifyChanged() => SettingsChanged?.Invoke();

        // ── Private helpers ───────────────────────────────────────────────────
        private static SystemMode ParseSystemMode(string? raw)
        {
            if (Enum.TryParse<SystemMode>(raw, ignoreCase: true, out var parsed))
                return parsed;
            return SystemMode.StoreMode;
        }

        private static readonly string[] AllDefaultModules =
        {
            "Attendance", "Dashboard", "Employees", "Inventory",
            "Menu", "Orders", "Payroll", "PayslipRequests",
            "Receipts", "Sales", "Tables", "Users", "Settings"
        };

        private static Dictionary<string, bool> BuildDefaultVisibility()
        {
            var d = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in AllDefaultModules) d[m] = true;
            return d;
        }

        private static void EnsureDefaultModulesInCache(Dictionary<string, bool> vis)
        {
            foreach (var m in AllDefaultModules)
                if (!vis.ContainsKey(m)) vis[m] = true;
        }
    }
}
