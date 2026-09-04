#nullable enable
using Microsoft.Data.Sqlite;
using SihyuPOSPayroll.Data;
using System;
using System.Diagnostics;

namespace SihyuPOSPayroll.Services
{
    /// <summary>
    /// Manages the <c>app_settings</c> table in SQLite.
    ///
    /// Schema (created by <see cref="AuthSchemaInitializer"/>):
    ///   id          INTEGER PRIMARY KEY AUTOINCREMENT
    ///   user        TEXT  — username or "global" for shared settings
    ///   page        TEXT  — e.g. "inventory", "pos", "sales"
    ///   column_name TEXT  — e.g. "qty", "category", "expiry_date"
    ///   is_show     INTEGER — 1 = visible, 0 = hidden
    ///   UNIQUE (user, page, column_name)
    /// </summary>
    public static class AppSettingsService
    {
        private const int Timeout = 10;

        // ── Schema ────────────────────────────────────────────────────────────

        /// <summary>
        /// No-op — table is created by <see cref="AuthSchemaInitializer.EnsureSchemaAtStartup"/>.
        /// Kept for API compatibility with any callers.
        /// </summary>
        public static void EnsureTable() { }

        // ── Read ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns whether a column should be visible for a given user + page.
        /// Falls back to <paramref name="defaultValue"/> if no row exists yet.
        /// </summary>
        public static bool GetColumnVisibility(
            string page,
            string columnName,
            bool   defaultValue = true,
            string user         = "global")
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT is_show
                    FROM   app_settings
                    WHERE  user        = @user
                      AND  page        = @page
                      AND  column_name = @col
                    LIMIT 1;";
                cmd.Parameters.AddWithValue("@user", user);
                cmd.Parameters.AddWithValue("@page", page);
                cmd.Parameters.AddWithValue("@col",  columnName);

                var result = cmd.ExecuteScalar();
                if (result is null || result == DBNull.Value)
                    return defaultValue;

                return Convert.ToInt32(result) == 1;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppSettingsService] GetColumnVisibility: {ex.Message}");
                return defaultValue;
            }
        }

        // ── Write ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Inserts or updates the visibility flag for a column on a given page/user.
        /// Fire-and-forget safe — errors are only logged, never thrown.
        /// </summary>
        public static void SetColumnVisibility(
            string page,
            string columnName,
            bool   isShow,
            string user = "global")
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                // INSERT OR REPLACE respects the UNIQUE(user, page, column_name) constraint.
                cmd.CommandText = @"
                    INSERT INTO app_settings (user, page, column_name, is_show)
                    VALUES (@user, @page, @col, @show)
                    ON CONFLICT(user, page, column_name) DO UPDATE SET is_show = excluded.is_show;";
                cmd.Parameters.AddWithValue("@user", user);
                cmd.Parameters.AddWithValue("@page", page);
                cmd.Parameters.AddWithValue("@col",  columnName);
                cmd.Parameters.AddWithValue("@show", isShow ? 1 : 0);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppSettingsService] SetColumnVisibility: {ex.Message}");
            }
        }

        // ── Int read/write (reuses is_show column for arbitrary small integers) ─

        /// <summary>
        /// Returns an integer setting, or <paramref name="defaultValue"/> if not set.
        /// Stored in the same app_settings table using the is_show INTEGER column.
        /// </summary>
        public static int GetIntSetting(
            string page,
            string columnName,
            int    defaultValue = 0,
            string user         = "global")
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT is_show
                    FROM   app_settings
                    WHERE  user        = @user
                      AND  page        = @page
                      AND  column_name = @col
                    LIMIT 1;";
                cmd.Parameters.AddWithValue("@user", user);
                cmd.Parameters.AddWithValue("@page", page);
                cmd.Parameters.AddWithValue("@col",  columnName);
                var result = cmd.ExecuteScalar();
                if (result is null || result == DBNull.Value) return defaultValue;
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppSettingsService] GetIntSetting: {ex.Message}");
                return defaultValue;
            }
        }

        /// <summary>Upserts an integer setting.</summary>
        public static void SetIntSetting(
            string page,
            string columnName,
            int    value,
            string user = "global")
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO app_settings (user, page, column_name, is_show)
                    VALUES (@user, @page, @col, @val)
                    ON CONFLICT(user, page, column_name) DO UPDATE SET is_show = excluded.is_show;";
                cmd.Parameters.AddWithValue("@user", user);
                cmd.Parameters.AddWithValue("@page", page);
                cmd.Parameters.AddWithValue("@col",  columnName);
                cmd.Parameters.AddWithValue("@val",  value);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppSettingsService] SetIntSetting: {ex.Message}");
            }
        }
    }
}
