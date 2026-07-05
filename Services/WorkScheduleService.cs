#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SihyuPOSPayroll.Helpers;
using SihyuPOSPayroll.Models;
using MySql.Data.MySqlClient;

namespace SihyuPOSPayroll.Services
{
    public class WorkScheduleService
    {
        private readonly string _cs;
        public string? LastError { get; private set; }

        public WorkScheduleService(string? connectionString = null)
        {
            _cs = string.IsNullOrWhiteSpace(connectionString)
                ? ConfigurationHelper.GetConnectionString()
                : connectionString!;
        }

        // =====================================================================
        // SCHEMA
        // =====================================================================

        public bool TryEnsureSchema()
        {
            LastError = null;
            try
            {
                using var conn = new MySqlConnection(_cs);
                conn.Open();

                const string ddl = @"
                    CREATE TABLE IF NOT EXISTS `work_schedule` (
                        `id`         BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
                        `label`      VARCHAR(100)     NOT NULL,
                        `days_mask`  TINYINT UNSIGNED NOT NULL DEFAULT 0, -- bit0=Mon ... bit6=Sun
                        `is_active`  TINYINT(1)       NOT NULL DEFAULT 1,
                        `updated_at` TIMESTAMP        NOT NULL DEFAULT CURRENT_TIMESTAMP
                                                       ON UPDATE CURRENT_TIMESTAMP,
                        PRIMARY KEY (`id`),
                        UNIQUE KEY `uq_label` (`label`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                using var cmd = new MySqlCommand(ddl, conn);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        public bool IsDbReady()
        {
            try
            {
                using var conn = new MySqlConnection(_cs);
                conn.Open();
                using var cmd = new MySqlCommand("SELECT COUNT(*) FROM `work_schedule`;", conn);
                _ = cmd.ExecuteScalar();
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        /// <summary>Optional: seed common presets if table is empty.</summary>
        public bool EnsureSeedDefaults()
        {
            try
            {
                using var conn = new MySqlConnection(_cs);
                conn.Open();

                var count = 0;
                using (var c = new MySqlCommand("SELECT COUNT(*) FROM work_schedule;", conn))
                    count = Convert.ToInt32(c.ExecuteScalar());

                if (count > 0) return true;

                var now = DateTime.Now;

                // Mon–Fri : 1111100 (bits 0..6 = Mon..Sun)
                InsertRaw(conn, "Mon–Fri", mask: (byte)((1 << 0) | (1 << 1) | (1 << 2) | (1 << 3) | (1 << 4)), isActive: true, now);
                // MWF     : 1010100
                InsertRaw(conn, "MWF", mask: (byte)((1 << 0) | (1 << 2) | (1 << 4)), isActive: true, now);
                // TTh     : 0101000
                InsertRaw(conn, "TTh", mask: (byte)((1 << 1) | (1 << 3)), isActive: true, now);

                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }

            static void InsertRaw(MySqlConnection conn, string label, byte mask, bool isActive, DateTime updatedAt)
            {
                const string sql = @"INSERT INTO work_schedule (label, days_mask, is_active, updated_at)
                                     VALUES (@l, @m, @a, @u)
                                     ON DUPLICATE KEY UPDATE days_mask=VALUES(days_mask), is_active=VALUES(is_active), updated_at=VALUES(updated_at);";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@l", label);
                cmd.Parameters.AddWithValue("@m", mask);
                cmd.Parameters.AddWithValue("@a", isActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@u", updatedAt);
                cmd.ExecuteNonQuery();
            }
        }

        // =====================================================================
        // LOADERS
        // =====================================================================

        public ObservableCollection<WorkScheduleModel> Load()
        {
            var list = new ObservableCollection<WorkScheduleModel>();
            LastError = null;

            try
            {
                using var conn = new MySqlConnection(_cs);
                conn.Open();
                const string sql = @"
                    SELECT id, label, days_mask, is_active, updated_at
                    FROM work_schedule
                    ORDER BY label ASC;";
                using var cmd = new MySqlCommand(sql, conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var m = new WorkScheduleModel
                    {
                        Id = Convert.ToInt32(r["id"]),
                        Label = r["label"]?.ToString() ?? string.Empty,
                        IsActive = Convert.ToInt32(r["is_active"]) != 0,
                        UpdatedAt = r.GetDateTime("updated_at")
                    };
                    m.FromDaysMask(Convert.ToByte(r["days_mask"]));
                    list.Add(m);
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }

            return list;
        }

        public List<WorkScheduleModel> GetAll(bool onlyActive = false)
        {
            var list = new List<WorkScheduleModel>();
            LastError = null;

            try
            {
                using var conn = new MySqlConnection(_cs);
                conn.Open();

                var sql = @"SELECT id, label, days_mask, is_active, updated_at
                            FROM work_schedule";
                if (onlyActive) sql += " WHERE is_active = 1";
                sql += " ORDER BY label ASC;";

                using var cmd = new MySqlCommand(sql, conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var m = new WorkScheduleModel
                    {
                        Id = Convert.ToInt32(r["id"]),
                        Label = r["label"]?.ToString() ?? string.Empty,
                        IsActive = Convert.ToInt32(r["is_active"]) != 0,
                        UpdatedAt = r.GetDateTime("updated_at")
                    };
                    m.FromDaysMask(Convert.ToByte(r["days_mask"]));
                    list.Add(m);
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }

            return list;
        }

        public List<string> GetLabels(bool onlyActive = true)
        {
            var labels = new List<string>();
            try
            {
                using var conn = new MySqlConnection(_cs);
                conn.Open();

                var sql = "SELECT label FROM work_schedule";
                if (onlyActive) sql += " WHERE is_active = 1";
                sql += " ORDER BY label ASC";

                using var cmd = new MySqlCommand(sql, conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    labels.Add(r["label"]?.ToString() ?? string.Empty);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
            return labels;
        }

        public WorkScheduleModel? GetById(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_cs);
                conn.Open();
                const string sql = @"SELECT id, label, days_mask, is_active, updated_at
                                     FROM work_schedule WHERE id=@id LIMIT 1;";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);
                using var r = cmd.ExecuteReader();
                if (!r.Read()) return null;

                var m = new WorkScheduleModel
                {
                    Id = Convert.ToInt32(r["id"]),
                    Label = r["label"]?.ToString() ?? string.Empty,
                    IsActive = Convert.ToInt32(r["is_active"]) != 0,
                    UpdatedAt = r.GetDateTime("updated_at")
                };
                m.FromDaysMask(Convert.ToByte(r["days_mask"]));
                return m;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return null;
            }
        }

        public bool TryGetByLabel(string label, out WorkScheduleModel? model)
        {
            model = null;
            if (string.IsNullOrWhiteSpace(label)) return false;

            try
            {
                using var conn = new MySqlConnection(_cs);
                conn.Open();

                // Case-insensitive match (uses default collation, typically case-insensitive)
                const string sql = @"SELECT id, label, days_mask, is_active, updated_at
                                     FROM work_schedule
                                     WHERE label = @l
                                     LIMIT 1;";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@l", label.Trim());
                using var r = cmd.ExecuteReader();
                if (!r.Read()) return false;

                var m = new WorkScheduleModel
                {
                    Id = Convert.ToInt32(r["id"]),
                    Label = r["label"]?.ToString() ?? string.Empty,
                    IsActive = Convert.ToInt32(r["is_active"]) != 0,
                    UpdatedAt = r.GetDateTime("updated_at")
                };
                m.FromDaysMask(Convert.ToByte(r["days_mask"]));
                model = m;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        public int? GetIdByLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label)) return null;
            try
            {
                using var conn = new MySqlConnection(_cs);
                conn.Open();
                const string sql = @"SELECT id FROM work_schedule WHERE label = @l LIMIT 1;";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@l", label.Trim());
                var obj = cmd.ExecuteScalar();
                if (obj == null || obj == DBNull.Value) return null;
                return Convert.ToInt32(obj);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return null;
            }
        }

        // =====================================================================
        // SAVE / UPSERT
        // =====================================================================

        /// <summary>
        /// Saves the list transactionally:
        /// 1) Upsert all items by label
        /// 2) Delete any rows not in provided labels (case-insensitive)
        /// </summary>
        public void Save(IEnumerable<WorkScheduleModel> items)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));

            using var conn = new MySqlConnection(_cs);
            conn.Open();
            using var tx = conn.BeginTransaction();

            const string upsert = @"
                INSERT INTO work_schedule (label, days_mask, is_active, updated_at)
                VALUES (@label, @mask, @active, @updated)
                ON DUPLICATE KEY UPDATE
                    days_mask = VALUES(days_mask),
                    is_active = VALUES(is_active),
                    updated_at = VALUES(updated_at);";

            using var upCmd = new MySqlCommand(upsert, conn, tx);
            upCmd.Parameters.Add("@label", MySqlDbType.VarChar, 100);
            upCmd.Parameters.Add("@mask", MySqlDbType.UByte);
            upCmd.Parameters.Add("@active", MySqlDbType.Byte);
            upCmd.Parameters.Add("@updated", MySqlDbType.DateTime);

            try
            {
                var normalized = items
                    .Where(x => !string.IsNullOrWhiteSpace(x.Label))
                    .Select(x =>
                    {
                        var label = x.Label.Trim();
                        if (label.Length > 100) label = label[..100];
                        return new WorkScheduleModel
                        {
                            Label = label,
                            UpdatedAt = x.UpdatedAt == default ? DateTime.Now : x.UpdatedAt,
                            IsActive = x.IsActive,
                            Mon = x.Mon,
                            Tue = x.Tue,
                            Wed = x.Wed,
                            Thu = x.Thu,
                            Fri = x.Fri,
                            Sat = x.Sat,
                            Sun = x.Sun
                        };
                    })
                    .GroupBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderByDescending(i => i.UpdatedAt).First())
                    .ToList();

                foreach (var it in normalized)
                {
                    upCmd.Parameters["@label"].Value = it.Label;
                    upCmd.Parameters["@mask"].Value = it.ToDaysMask();
                    upCmd.Parameters["@active"].Value = (byte)(it.IsActive ? 1 : 0);
                    upCmd.Parameters["@updated"].Value = it.UpdatedAt == default ? DateTime.Now : it.UpdatedAt;
                    upCmd.ExecuteNonQuery();
                }

                // delete rows not present
                var keep = new HashSet<string>(normalized.Select(n => n.Label), StringComparer.OrdinalIgnoreCase);
                var existing = new List<string>();
                using (var sel = new MySqlCommand("SELECT label FROM work_schedule;", conn, tx))
                using (var rr = sel.ExecuteReader())
                {
                    while (rr.Read()) existing.Add(rr["label"]?.ToString() ?? "");
                }

                var toDelete = existing.Where(x => !string.IsNullOrWhiteSpace(x) && !keep.Contains(x)).ToList();
                if (toDelete.Count > 0)
                {
                    using var del = new MySqlCommand("DELETE FROM work_schedule WHERE label=@l;", conn, tx);
                    del.Parameters.Add("@l", MySqlDbType.VarChar, 100);
                    foreach (var l in toDelete)
                    {
                        del.Parameters["@l"].Value = l;
                        del.ExecuteNonQuery();
                    }
                }

                tx.Commit();
            }
            catch
            {
                try { tx.Rollback(); } catch { /* ignore */ }
                throw;
            }
        }

        /// <summary>
        /// Upserts a single schedule by <see cref="WorkScheduleModel.Label"/> and returns its Id.
        /// </summary>
        public int UpsertByLabel(WorkScheduleModel item)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));
            var label = (item.Label ?? string.Empty).Trim();
            if (label.Length == 0) throw new ArgumentException("Label is required.", nameof(item));

            if (label.Length > 100) label = label[..100];

            using var conn = new MySqlConnection(_cs);
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                const string up = @"
                    INSERT INTO work_schedule (label, days_mask, is_active, updated_at)
                    VALUES (@l, @m, @a, @u)
                    ON DUPLICATE KEY UPDATE days_mask=VALUES(days_mask), is_active=VALUES(is_active), updated_at=VALUES(updated_at);";
                using (var cmd = new MySqlCommand(up, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@l", label);
                    cmd.Parameters.AddWithValue("@m", item.ToDaysMask());
                    cmd.Parameters.AddWithValue("@a", item.IsActive ? 1 : 0);
                    cmd.Parameters.AddWithValue("@u", item.UpdatedAt == default ? DateTime.Now : item.UpdatedAt);
                    cmd.ExecuteNonQuery();
                }

                // fetch id
                int id;
                using (var idCmd = new MySqlCommand("SELECT id FROM work_schedule WHERE label=@l LIMIT 1;", conn, tx))
                {
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
    }
}
