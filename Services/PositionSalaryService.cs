#nullable enable
// PositionSalaryService — SQLite implementation
// Ported from the MySQL version; now uses SqliteConnectionFactory to match
// the rest of the app (UserService, EmployeeService, etc.).

using Microsoft.Data.Sqlite;
using SihyuPOSPayroll.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SihyuPOSPayroll.Services
{
    /// <summary>
    /// DB-backed service for managing Position → Daily Rate presets.
    /// Table: <c>position_salary</c> (SQLite — created by AuthSchemaInitializer).
    /// </summary>
    public sealed class PositionSalaryService
    {
        // ── Nested data model ─────────────────────────────────────────────────

        public class PositionSalary : INotifyPropertyChanged
        {
            private int     _id;
            private string  _position  = string.Empty;
            private decimal _dailyRate;
            private bool    _isActive  = true;
            private DateTime _updatedAt = DateTime.Now;

            public int Id
            {
                get => _id;
                set { if (_id != value) { _id = value; OnPropertyChanged(); } }
            }
            public string Position
            {
                get => _position;
                set { if (_position != value) { _position = value; OnPropertyChanged(); } }
            }
            public decimal DailyRate
            {
                get => _dailyRate;
                set { if (_dailyRate != value) { _dailyRate = value; OnPropertyChanged(); } }
            }
            public bool IsActive
            {
                get => _isActive;
                set { if (_isActive != value) { _isActive = value; OnPropertyChanged(); } }
            }
            public DateTime UpdatedAt
            {
                get => _updatedAt;
                set { if (_updatedAt != value) { _updatedAt = value; OnPropertyChanged(); } }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string? n = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        }

        // ── State ──────────────────────────────────────────────────────────────
        public string? LastError { get; private set; }

        // ── Schema probe ──────────────────────────────────────────────────────

        /// <summary>
        /// No-op — schema is already created by AuthSchemaInitializer at startup.
        /// Kept for API compatibility with PositionSalaryViewModel.
        /// </summary>
        public bool TryEnsureSchema()
        {
            LastError = null;
            return true;
        }

        /// <summary>Quick accessibility probe — never throws.</summary>
        public bool IsDbReady()
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM position_salary;";
                cmd.ExecuteScalar();
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        // ── READ ──────────────────────────────────────────────────────────────

        /// <summary>Loads all rows. Never throws; sets LastError on failure.</summary>
        public List<PositionSalary> Load()
        {
            var list = new List<PositionSalary>();
            LastError = null;
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT id, position, daily_rate, is_active, updated_at
                    FROM position_salary
                    ORDER BY position ASC;";

                using var r = cmd.ExecuteReader();
                while (r.Read())
                    list.Add(MapRow(r));
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
            return list;
        }

        // ── CREATE ────────────────────────────────────────────────────────────

        public bool AddPosition(PositionSalary item)
        {
            if (string.IsNullOrWhiteSpace(item.Position)) return false;
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO position_salary (position, daily_rate, is_active, updated_at)
                    VALUES (@pos, @rate, @active, @upd);
                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@pos",    item.Position.Trim());
                cmd.Parameters.AddWithValue("@rate",   (double)item.DailyRate);
                cmd.Parameters.AddWithValue("@active", item.IsActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@upd",    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                var id = cmd.ExecuteScalar();
                if (id != null && id != DBNull.Value)
                    item.Id = Convert.ToInt32(id);
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.Error.WriteLine($"[PositionSalaryService] AddPosition: {ex.Message}");
                return false;
            }
        }

        // ── UPDATE ────────────────────────────────────────────────────────────

        public bool UpdatePosition(PositionSalary item)
        {
            if (item.Id <= 0 || string.IsNullOrWhiteSpace(item.Position)) return false;
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE position_salary
                    SET position   = @pos,
                        daily_rate = @rate,
                        is_active  = @active,
                        updated_at = @upd
                    WHERE id = @id;";
                cmd.Parameters.AddWithValue("@pos",    item.Position.Trim());
                cmd.Parameters.AddWithValue("@rate",   (double)item.DailyRate);
                cmd.Parameters.AddWithValue("@active", item.IsActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@upd",    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@id",     item.Id);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.Error.WriteLine($"[PositionSalaryService] UpdatePosition: {ex.Message}");
                return false;
            }
        }

        // ── DELETE ────────────────────────────────────────────────────────────

        public bool DeletePosition(int id)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM position_salary WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.Error.WriteLine($"[PositionSalaryService] DeletePosition: {ex.Message}");
                return false;
            }
        }

        /// <summary>Explicit delete by position name (kept for API compatibility).</summary>
        public void DeletePosition(string position)
        {
            if (string.IsNullOrWhiteSpace(position)) return;
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM position_salary WHERE position = @p;";
                cmd.Parameters.AddWithValue("@p", position.Trim());
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[PositionSalaryService] DeletePosition(name): {ex.Message}");
            }
        }

        // ── RATE LOOKUP ───────────────────────────────────────────────────────

        public bool TryGetRate(string? position, out decimal rate)
        {
            rate = 0m;
            if (string.IsNullOrWhiteSpace(position)) return false;
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT daily_rate
                    FROM position_salary
                    WHERE is_active = 1
                      AND LOWER(position) = LOWER(@p)
                    LIMIT 1;";
                cmd.Parameters.AddWithValue("@p", position.Trim());
                var obj = cmd.ExecuteScalar();
                if (obj == null || obj == DBNull.Value) return false;
                rate = Convert.ToDecimal(obj, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[PositionSalaryService] TryGetRate: {ex.Message}");
                return false;
            }
        }

        // ── LEGACY BULK SAVE (kept for PositionSalaryViewModel compatibility) ──

        /// <summary>
        /// Bulk upsert + cleanup: inserts/updates all provided items and
        /// deletes DB rows not present in the list. Throws on failure.
        /// </summary>
        public void Save(IEnumerable<PositionSalary> items)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));

            var normalized = items
                .Where(x => !string.IsNullOrWhiteSpace(x.Position))
                .Select(x => new PositionSalary
                {
                    Id        = x.Id,
                    Position  = x.Position.Trim().Length > 100 ? x.Position.Trim()[..100] : x.Position.Trim(),
                    DailyRate = x.DailyRate < 0 ? 0 : Math.Round(x.DailyRate, 2, MidpointRounding.AwayFromZero),
                    IsActive  = x.IsActive,
                    UpdatedAt = x.UpdatedAt == default ? DateTime.Now : x.UpdatedAt,
                })
                .GroupBy(x => x.Position, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(i => i.UpdatedAt).First())
                .ToList();

            using var conn = SqliteConnectionFactory.CreateOpenConnection();
            using var tx   = conn.BeginTransaction();

            try
            {
                foreach (var it in normalized)
                {
                    it.UpdatedAt = DateTime.Now;
                    using var upsert = conn.CreateCommand();
                    upsert.Transaction = tx;
                    upsert.CommandText = @"
                        INSERT INTO position_salary (position, daily_rate, is_active, updated_at)
                        VALUES (@pos, @rate, @active, @upd)
                        ON CONFLICT(position) DO UPDATE SET
                            daily_rate = excluded.daily_rate,
                            is_active  = excluded.is_active,
                            updated_at = excluded.updated_at;";
                    upsert.Parameters.AddWithValue("@pos",    it.Position);
                    upsert.Parameters.AddWithValue("@rate",   (double)it.DailyRate);
                    upsert.Parameters.AddWithValue("@active", it.IsActive ? 1 : 0);
                    upsert.Parameters.AddWithValue("@upd",    it.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                    upsert.ExecuteNonQuery();
                }

                // Delete rows not in the provided list
                var keepNames = new HashSet<string>(
                    normalized.Select(n => n.Position), StringComparer.OrdinalIgnoreCase);

                var existing = new List<string>();
                using (var selCmd = conn.CreateCommand())
                {
                    selCmd.Transaction  = tx;
                    selCmd.CommandText  = "SELECT position FROM position_salary;";
                    using var r = selCmd.ExecuteReader();
                    while (r.Read())
                    {
                        var p = r.IsDBNull(0) ? null : r.GetString(0);
                        if (!string.IsNullOrWhiteSpace(p)) existing.Add(p);
                    }
                }

                foreach (var dead in existing.Where(p => !keepNames.Contains(p)))
                {
                    using var del = conn.CreateCommand();
                    del.Transaction = tx;
                    del.CommandText = "DELETE FROM position_salary WHERE position = @p;";
                    del.Parameters.AddWithValue("@p", dead);
                    del.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch
            {
                try { tx.Rollback(); } catch { /* ignore */ }
                throw;
            }
        }

        // ── PRIVATE MAPPER ────────────────────────────────────────────────────

        private static PositionSalary MapRow(SqliteDataReader r)
        {
            DateTime updatedAt = DateTime.Now;
            if (!r.IsDBNull(r.GetOrdinal("updated_at")))
            {
                var raw = r.GetString(r.GetOrdinal("updated_at"));
                DateTime.TryParse(raw, out updatedAt);
            }

            return new PositionSalary
            {
                Id        = r.GetInt32(r.GetOrdinal("id")),
                Position  = r.IsDBNull(r.GetOrdinal("position"))   ? string.Empty : r.GetString(r.GetOrdinal("position")),
                DailyRate = r.IsDBNull(r.GetOrdinal("daily_rate"))  ? 0m : Convert.ToDecimal(r.GetValue(r.GetOrdinal("daily_rate")), CultureInfo.InvariantCulture),
                IsActive  = !r.IsDBNull(r.GetOrdinal("is_active")) && r.GetInt32(r.GetOrdinal("is_active")) == 1,
                UpdatedAt = updatedAt,
            };
        }
    }
}
