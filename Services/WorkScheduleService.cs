#nullable enable
using Microsoft.Data.Sqlite;
using SihyuPOSPayroll.Data;
using SihyuPOSPayroll.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SihyuPOSPayroll.Services
{
    public class WorkScheduleService
    {
        // Constructor kept for interface compatibility — connection string ignored,
        // all data comes from SQLite via SqliteConnectionFactory.
        public WorkScheduleService(string? connectionString = null) { }

        public string? LastError { get; private set; }

        // ── Schema ────────────────────────────────────────────────────────────
        // The work_schedule table is created by AuthSchemaInitializer (EmployeeTables).

        public bool TryEnsureSchema()
        {
            LastError = null;
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS work_schedule (
                        id         INTEGER PRIMARY KEY AUTOINCREMENT,
                        label      TEXT    NOT NULL UNIQUE,
                        days_mask  INTEGER NOT NULL DEFAULT 0,
                        is_active  INTEGER NOT NULL DEFAULT 1,
                        updated_at TEXT    NOT NULL DEFAULT (datetime('now'))
                    );";
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex) { LastError = ex.Message; return false; }
        }

        public bool IsDbReady()
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM work_schedule;";
                cmd.ExecuteScalar();
                return true;
            }
            catch (Exception ex) { LastError = ex.Message; return false; }
        }

        public bool EnsureSeedDefaults()
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();

                using (var c = conn.CreateCommand())
                {
                    c.CommandText = "SELECT COUNT(*) FROM work_schedule;";
                    if (Convert.ToInt32(c.ExecuteScalar()) > 0) return true;
                }

                var presets = new[]
                {
                    ("Mon-Fri", (byte)((1<<0)|(1<<1)|(1<<2)|(1<<3)|(1<<4))),
                    ("MWF",     (byte)((1<<0)|(1<<2)|(1<<4))),
                    ("TTh",     (byte)((1<<1)|(1<<3))),
                };

                foreach (var (label, mask) in presets)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        INSERT OR IGNORE INTO work_schedule (label, days_mask, is_active, updated_at)
                        VALUES (@l, @m, 1, datetime('now'));";
                    cmd.Parameters.AddWithValue("@l", label);
                    cmd.Parameters.AddWithValue("@m", mask);
                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (Exception ex) { LastError = ex.Message; return false; }
        }

        // ── Read ──────────────────────────────────────────────────────────────

        public ObservableCollection<WorkScheduleModel> Load()
        {
            var list = new ObservableCollection<WorkScheduleModel>();
            LastError = null;
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT id, label, days_mask, is_active, updated_at
                    FROM   work_schedule ORDER BY label ASC;";
                using var r = cmd.ExecuteReader();
                while (r.Read()) list.Add(Map(r));
            }
            catch (Exception ex) { LastError = ex.Message; }
            return list;
        }

        public List<WorkScheduleModel> GetAll(bool onlyActive = false)
        {
            var list = new List<WorkScheduleModel>();
            LastError = null;
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "SELECT id, label, days_mask, is_active, updated_at FROM work_schedule"
                                + (onlyActive ? " WHERE is_active = 1" : "")
                                + " ORDER BY label ASC;";
                using var r = cmd.ExecuteReader();
                while (r.Read()) list.Add(Map(r));
            }
            catch (Exception ex) { LastError = ex.Message; }
            return list;
        }

        public List<string> GetLabels(bool onlyActive = true)
        {
            var labels = new List<string>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "SELECT label FROM work_schedule"
                                + (onlyActive ? " WHERE is_active = 1" : "")
                                + " ORDER BY label ASC;";
                using var r = cmd.ExecuteReader();
                while (r.Read()) labels.Add(r.GetString(0));
            }
            catch (Exception ex) { LastError = ex.Message; }
            return labels;
        }

        public WorkScheduleModel? GetById(int id)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT id, label, days_mask, is_active, updated_at
                    FROM   work_schedule WHERE id = @id LIMIT 1;";
                cmd.Parameters.AddWithValue("@id", id);
                using var r = cmd.ExecuteReader();
                return r.Read() ? Map(r) : null;
            }
            catch (Exception ex) { LastError = ex.Message; return null; }
        }

        public bool TryGetByLabel(string label, out WorkScheduleModel? model)
        {
            model = null;
            if (string.IsNullOrWhiteSpace(label)) return false;
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT id, label, days_mask, is_active, updated_at
                    FROM   work_schedule WHERE label = @l COLLATE NOCASE LIMIT 1;";
                cmd.Parameters.AddWithValue("@l", label.Trim());
                using var r = cmd.ExecuteReader();
                if (!r.Read()) return false;
                model = Map(r);
                return true;
            }
            catch (Exception ex) { LastError = ex.Message; return false; }
        }

        public int? GetIdByLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label)) return null;
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "SELECT id FROM work_schedule WHERE label = @l COLLATE NOCASE LIMIT 1;";
                cmd.Parameters.AddWithValue("@l", label.Trim());
                var obj = cmd.ExecuteScalar();
                return (obj == null || obj == DBNull.Value) ? null : Convert.ToInt32(obj);
            }
            catch (Exception ex) { LastError = ex.Message; return null; }
        }

        // ── Write ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Upserts all items by label and deletes any rows not in the provided list.
        /// </summary>
        public void Save(IEnumerable<WorkScheduleModel> items)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));

            var normalized = items
                .Where(x => !string.IsNullOrWhiteSpace(x.Label))
                .Select(x =>
                {
                    var lbl = x.Label.Trim();
                    if (lbl.Length > 100) lbl = lbl[..100];
                    return new WorkScheduleModel
                    {
                        Label     = lbl,
                        UpdatedAt = x.UpdatedAt == default ? DateTime.Now : x.UpdatedAt,
                        IsActive  = x.IsActive,
                        Mon = x.Mon, Tue = x.Tue, Wed = x.Wed,
                        Thu = x.Thu, Fri = x.Fri, Sat = x.Sat, Sun = x.Sun
                    };
                })
                .GroupBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(i => i.UpdatedAt).First())
                .ToList();

            using var conn = SqliteConnectionFactory.CreateOpenConnection();
            using var tx   = conn.BeginTransaction();
            try
            {
                // Upsert each item
                foreach (var it in normalized)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    // INSERT OR REPLACE keeps the UNIQUE(label) constraint satisfied
                    cmd.CommandText = @"
                        INSERT INTO work_schedule (label, days_mask, is_active, updated_at)
                        VALUES (@l, @m, @a, @u)
                        ON CONFLICT(label) DO UPDATE SET
                            days_mask  = excluded.days_mask,
                            is_active  = excluded.is_active,
                            updated_at = excluded.updated_at;";
                    cmd.Parameters.AddWithValue("@l", it.Label);
                    cmd.Parameters.AddWithValue("@m", it.ToDaysMask());
                    cmd.Parameters.AddWithValue("@a", it.IsActive ? 1 : 0);
                    cmd.Parameters.AddWithValue("@u", (it.UpdatedAt == default ? DateTime.Now : it.UpdatedAt).ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.ExecuteNonQuery();
                }

                // Delete rows whose labels are no longer in the list
                var keep = new HashSet<string>(normalized.Select(n => n.Label), StringComparer.OrdinalIgnoreCase);
                var existing = new List<string>();
                using (var sel = conn.CreateCommand())
                {
                    sel.Transaction  = tx;
                    sel.CommandText  = "SELECT label FROM work_schedule;";
                    using var rr = sel.ExecuteReader();
                    while (rr.Read()) existing.Add(rr.GetString(0));
                }

                foreach (var lbl in existing.Where(x => !string.IsNullOrWhiteSpace(x) && !keep.Contains(x)))
                {
                    using var del = conn.CreateCommand();
                    del.Transaction  = tx;
                    del.CommandText  = "DELETE FROM work_schedule WHERE label = @l;";
                    del.Parameters.AddWithValue("@l", lbl);
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

        /// <summary>Upserts a single schedule by label and returns its Id.</summary>
        public int UpsertByLabel(WorkScheduleModel item)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));
            var label = (item.Label ?? string.Empty).Trim();
            if (label.Length == 0) throw new ArgumentException("Label is required.", nameof(item));
            if (label.Length > 100) label = label[..100];

            using var conn = SqliteConnectionFactory.CreateOpenConnection();
            using var tx   = conn.BeginTransaction();
            try
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction  = tx;
                    cmd.CommandText  = @"
                        INSERT INTO work_schedule (label, days_mask, is_active, updated_at)
                        VALUES (@l, @m, @a, @u)
                        ON CONFLICT(label) DO UPDATE SET
                            days_mask  = excluded.days_mask,
                            is_active  = excluded.is_active,
                            updated_at = excluded.updated_at;";
                    cmd.Parameters.AddWithValue("@l", label);
                    cmd.Parameters.AddWithValue("@m", item.ToDaysMask());
                    cmd.Parameters.AddWithValue("@a", item.IsActive ? 1 : 0);
                    cmd.Parameters.AddWithValue("@u", (item.UpdatedAt == default ? DateTime.Now : item.UpdatedAt).ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.ExecuteNonQuery();
                }

                int id;
                using (var idCmd = conn.CreateCommand())
                {
                    idCmd.Transaction  = tx;
                    idCmd.CommandText  = "SELECT id FROM work_schedule WHERE label = @l LIMIT 1;";
                    idCmd.Parameters.AddWithValue("@l", label);
                    id = Convert.ToInt32(idCmd.ExecuteScalar());
                }

                tx.Commit();
                return id;
            }
            catch
            {
                try { tx.Rollback(); } catch { }
                throw;
            }
        }

        // ── Mapper ────────────────────────────────────────────────────────────

        private static WorkScheduleModel Map(SqliteDataReader r)
        {
            var m = new WorkScheduleModel
            {
                Id        = r.GetInt32(r.GetOrdinal("id")),
                Label     = r.IsDBNull(r.GetOrdinal("label")) ? string.Empty : r.GetString(r.GetOrdinal("label")),
                IsActive  = r.GetInt32(r.GetOrdinal("is_active")) != 0,
                UpdatedAt = r.IsDBNull(r.GetOrdinal("updated_at"))
                                ? DateTime.Now
                                : DateTime.TryParse(r.GetString(r.GetOrdinal("updated_at")), out var d) ? d : DateTime.Now,
            };
            m.FromDaysMask(Convert.ToByte(r.GetInt32(r.GetOrdinal("days_mask"))));
            return m;
        }
    }
}
