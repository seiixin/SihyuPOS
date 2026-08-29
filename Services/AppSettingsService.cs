using System;
using System.Diagnostics;
using MySql.Data.MySqlClient;

namespace SihyuPOSPayroll.Services
{
    /// <summary>
    /// Manages the <c>app_settings</c> table.
    ///
    /// Schema:
    ///   id          INT AUTO_INCREMENT PRIMARY KEY
    ///   user        VARCHAR(100)  — username or "global" for shared settings
    ///   page        VARCHAR(100)  — e.g. "inventory", "pos", "sales"
    ///   column_name VARCHAR(100)  — e.g. "qty", "category", "expiry_date"
    ///   is_show     TINYINT(1)    — 1 = visible, 0 = hidden
    ///   UNIQUE KEY  uq_app_settings (user, page, column_name)
    /// </summary>
    public static class AppSettingsService
    {
        private const string Cs =
            "server=localhost;user=root;password=;database=sihyu_pos;";
        private const int Timeout = 10;

        // ── Schema ────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates the app_settings table if it does not yet exist.
        /// Safe to call multiple times — idempotent.
        /// </summary>
        public static void EnsureTable()
        {
            try
            {
                using var conn = new MySqlConnection(Cs);
                conn.Open();
                const string sql = @"
                    CREATE TABLE IF NOT EXISTS app_settings (
                        id          INT          NOT NULL AUTO_INCREMENT PRIMARY KEY,
                        user        VARCHAR(100) NOT NULL DEFAULT 'global',
                        page        VARCHAR(100) NOT NULL,
                        column_name VARCHAR(100) NOT NULL,
                        is_show     TINYINT(1)   NOT NULL DEFAULT 1,
                        UNIQUE KEY uq_app_settings (user, page, column_name)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.CommandTimeout = Timeout;
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppSettingsService] EnsureTable: {ex.Message}");
            }
        }

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
                using var conn = new MySqlConnection(Cs);
                conn.Open();
                const string sql = @"
                    SELECT is_show
                    FROM   app_settings
                    WHERE  user        = @user
                      AND  page        = @page
                      AND  column_name = @col
                    LIMIT 1;";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.CommandTimeout = Timeout;
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
                using var conn = new MySqlConnection(Cs);
                conn.Open();
                const string sql = @"
                    INSERT INTO app_settings (user, page, column_name, is_show)
                    VALUES (@user, @page, @col, @show)
                    ON DUPLICATE KEY UPDATE is_show = @show;";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.CommandTimeout = Timeout;
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
    }
}
