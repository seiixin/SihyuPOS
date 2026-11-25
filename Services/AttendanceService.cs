#nullable enable
using Microsoft.Data.Sqlite;
using SihyuPOSPayroll.Data;
using SihyuPOSPayroll.Models;
using System;
using System.Collections.Generic;
using System.Data;

namespace SihyuPOSPayroll.Services
{
    public interface IAttendanceService
    {
        List<AttendanceModel> GetAllAttendances();
        List<AttendanceModel> GetAttendances();
        List<AttendanceModel> GetAttendances(DateTime? filterDate, int? employeeId);
        List<AttendanceModel> GetAttendancesByEmployeeId(int employeeId);
        List<AttendanceModel> GetAttendancesForEmployee(int employeeId);
        List<AttendanceModel> GetAttendancesForEmployee(int employeeId, DateTime from, DateTime to);
        List<AttendanceModel> GetAttendancesByDateRange(DateTime start, DateTime end);
        AttendanceModel? GetAttendanceById(int id);
        bool AddAttendance(AttendanceModel attendance);
        bool UpdateAttendance(AttendanceModel attendance);
        bool DeleteAttendance(int id);
        bool ClockIn(int employeeId);
        bool ClockIn(int employeeId, DateTime date, TimeSpan timeIn);
        bool ClockIn(int employeeId, DateTime dateTime);
        bool ClockOut(int employeeId);
        bool ClockOut(int employeeId, DateTime date, TimeSpan timeOut);
        bool ClockOut(int employeeId, DateTime dateTime);
        int GetWorkedDaysCount(int employeeId, DateTime startDate, DateTime endDate);
    }

    public sealed class AttendanceService : IAttendanceService
    {
        // Constructor kept for interface compatibility; connection is always
        // obtained from SqliteConnectionFactory so the parameter is ignored.
        public AttendanceService(string? connectionString = null) { }

        // ── Base query fragment ───────────────────────────────────────────────
        private const string SelectBase = @"
            SELECT
                a.id, a.employee_id, a.date, a.time_in, a.time_out,
                a.status, a.created_at,
                e.full_name AS employee_name,
                u.id        AS user_id
            FROM attendance a
            LEFT JOIN employees e ON a.employee_id = e.id
            LEFT JOIN users     u ON u.employee_id = e.id";

        // ── Read helpers ──────────────────────────────────────────────────────

        public List<AttendanceModel> GetAllAttendances()
        {
            var list = new List<AttendanceModel>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = SelectBase + " ORDER BY a.date DESC;";
                using var r = cmd.ExecuteReader();
                while (r.Read()) list.Add(MapAttendance(r));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error loading all attendances: " + ex.Message);
            }
            return list;
        }

        public List<AttendanceModel> GetAttendances() => GetAllAttendances();

        public List<AttendanceModel> GetAttendances(DateTime? filterDate, int? employeeId)
        {
            var list = new List<AttendanceModel>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();

                var sql = SelectBase + " WHERE 1=1";
                if (filterDate.HasValue)
                {
                    sql += " AND a.date = @FilterDate";
                    cmd.Parameters.AddWithValue("@FilterDate", filterDate.Value.Date.ToString("yyyy-MM-dd"));
                }
                if (employeeId.HasValue)
                {
                    sql += " AND a.employee_id = @EmployeeId";
                    cmd.Parameters.AddWithValue("@EmployeeId", employeeId.Value);
                }
                sql += " ORDER BY a.date DESC;";
                cmd.CommandText = sql;

                using var r = cmd.ExecuteReader();
                while (r.Read()) list.Add(MapAttendance(r));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error loading filtered attendances: " + ex.Message);
            }
            return list;
        }

        public List<AttendanceModel> GetAttendancesForEmployee(int employeeId)
            => GetAttendancesByEmployeeId(employeeId);

        public List<AttendanceModel> GetAttendancesForEmployee(int employeeId, DateTime from, DateTime to)
        {
            var list = new List<AttendanceModel>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = SelectBase + @"
                    WHERE a.employee_id = @EmployeeId
                      AND a.date BETWEEN @From AND @To
                    ORDER BY a.date DESC;";
                cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                cmd.Parameters.AddWithValue("@From",       from.Date.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@To",         to.Date.ToString("yyyy-MM-dd"));

                using var r = cmd.ExecuteReader();
                while (r.Read()) list.Add(MapAttendance(r));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error loading attendances for employee {employeeId} in date range: " + ex.Message);
            }
            return list;
        }

        // ── Clock helpers ─────────────────────────────────────────────────────

        public bool ClockIn(int employeeId)
            => ClockIn(employeeId, DateTime.Today, DateTime.Now.TimeOfDay);

        public bool ClockIn(int employeeId, DateTime dateTime)
            => ClockIn(employeeId, dateTime.Date, dateTime.TimeOfDay);

        public bool ClockOut(int employeeId)
            => ClockOut(employeeId, DateTime.Today, DateTime.Now.TimeOfDay);

        public bool ClockOut(int employeeId, DateTime dateTime)
            => ClockOut(employeeId, dateTime.Date, dateTime.TimeOfDay);

        public int GetWorkedDaysCount(int employeeId, DateTime startDate, DateTime endDate)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT COUNT(*)
                    FROM   attendance
                    WHERE  employee_id = @EmployeeId
                      AND  date BETWEEN @StartDate AND @EndDate
                      AND  time_in  IS NOT NULL
                      AND  time_out IS NOT NULL;";
                cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                cmd.Parameters.AddWithValue("@StartDate",  startDate.Date.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@EndDate",    endDate.Date.ToString("yyyy-MM-dd"));
                return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error getting worked days count: " + ex.Message);
                return 0;
            }
        }

        public List<AttendanceModel> GetAttendancesByEmployeeId(int employeeId)
        {
            var list = new List<AttendanceModel>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = SelectBase + @"
                    WHERE a.employee_id = @EmployeeId
                    ORDER BY a.date DESC;";
                cmd.Parameters.AddWithValue("@EmployeeId", employeeId);

                using var r = cmd.ExecuteReader();
                while (r.Read()) list.Add(MapAttendance(r));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error loading attendances for employee {employeeId}: " + ex.Message);
            }
            return list;
        }

        public List<AttendanceModel> GetAttendancesByDateRange(DateTime start, DateTime end)
        {
            var list = new List<AttendanceModel>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = SelectBase + @"
                    WHERE a.date BETWEEN @Start AND @End
                    ORDER BY a.date DESC;";
                cmd.Parameters.AddWithValue("@Start", start.Date.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@End",   end.Date.ToString("yyyy-MM-dd"));

                using var r = cmd.ExecuteReader();
                while (r.Read()) list.Add(MapAttendance(r));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error loading attendances by date range: " + ex.Message);
            }
            return list;
        }

        public AttendanceModel? GetAttendanceById(int id)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = SelectBase + " WHERE a.id = @Id LIMIT 1;";
                cmd.Parameters.AddWithValue("@Id", id);

                using var r = cmd.ExecuteReader();
                if (r.Read()) return MapAttendance(r);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error loading attendance {id}: " + ex.Message);
            }
            return null;
        }

        public bool AddAttendance(AttendanceModel attendance)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO attendance
                        (employee_id, date, time_in, time_out, status, created_at)
                    VALUES
                        (@EmployeeId, @Date, @TimeIn, @TimeOut, @Status, @CreatedAt);
                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@EmployeeId", attendance.EmployeeId);
                cmd.Parameters.AddWithValue("@Date",       attendance.Date.Date.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@TimeIn",     ParamOrDbNull(attendance.TimeIn?.ToString()));
                cmd.Parameters.AddWithValue("@TimeOut",    ParamOrDbNull(attendance.TimeOut?.ToString()));
                cmd.Parameters.AddWithValue("@Status",     ParamOrDbNull(attendance.Status));
                cmd.Parameters.AddWithValue("@CreatedAt",  (attendance.CreatedAt == default ? DateTime.Now : attendance.CreatedAt ?? DateTime.Now).ToString("yyyy-MM-dd HH:mm:ss"));

                var newId = cmd.ExecuteScalar();
                attendance.Id = Convert.ToInt32(newId);
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error adding attendance: " + ex.Message);
                return false;
            }
        }

        public bool UpdateAttendance(AttendanceModel attendance)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE attendance SET
                        employee_id = @EmployeeId,
                        date        = @Date,
                        time_in     = @TimeIn,
                        time_out    = @TimeOut,
                        status      = @Status
                    WHERE id = @Id;";
                cmd.Parameters.AddWithValue("@EmployeeId", attendance.EmployeeId);
                cmd.Parameters.AddWithValue("@Date",       attendance.Date.Date.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@TimeIn",     ParamOrDbNull(attendance.TimeIn?.ToString()));
                cmd.Parameters.AddWithValue("@TimeOut",    ParamOrDbNull(attendance.TimeOut?.ToString()));
                cmd.Parameters.AddWithValue("@Status",     ParamOrDbNull(attendance.Status));
                cmd.Parameters.AddWithValue("@Id",         attendance.Id);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error updating attendance: " + ex.Message);
                return false;
            }
        }

        public bool DeleteAttendance(int id)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM attendance WHERE id = @Id;";
                cmd.Parameters.AddWithValue("@Id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error deleting attendance {id}: " + ex.Message);
                return false;
            }
        }

        // BUG FIX: allow multiple clock-in entries per day (no upsert, plain INSERT).
        public bool ClockIn(int employeeId, DateTime date, TimeSpan timeIn)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO attendance
                        (employee_id, date, time_in, status, created_at)
                    VALUES
                        (@EmployeeId, @Date, @TimeIn, 'Clocked In', @Now);";
                cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                cmd.Parameters.AddWithValue("@Date",       date.Date.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@TimeIn",     timeIn.ToString());
                cmd.Parameters.AddWithValue("@Now",        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error clocking in: " + ex.Message);
                return false;
            }
        }

        public bool ClockOut(int employeeId, DateTime date, TimeSpan timeOut)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                // Update the most recent 'Clocked In' row for this employee/date that has no time_out yet.
                cmd.CommandText = @"
                    UPDATE attendance
                    SET    time_out = @TimeOut,
                           status   = 'Present'
                    WHERE  id = (
                        SELECT id FROM attendance
                        WHERE  employee_id = @EmployeeId
                          AND  date        = @Date
                          AND  time_out    IS NULL
                        ORDER  BY created_at DESC
                        LIMIT  1
                    );";
                cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                cmd.Parameters.AddWithValue("@Date",       date.Date.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@TimeOut",    timeOut.ToString());
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error clocking out: " + ex.Message);
                return false;
            }
        }

        // ── Mapper ────────────────────────────────────────────────────────────

        private static AttendanceModel MapAttendance(SqliteDataReader r)
        {
            return new AttendanceModel
            {
                Id         = r.GetInt32(r.GetOrdinal("id")),
                EmployeeId = r.GetInt32(r.GetOrdinal("employee_id")),
                Date       = DateTime.Parse(r.GetString(r.GetOrdinal("date"))),
                TimeIn     = HasColumn(r, "time_in") && !r.IsDBNull(r.GetOrdinal("time_in"))
                                 ? TimeSpan.TryParse(r.GetString(r.GetOrdinal("time_in")), out var ti) ? ti : (TimeSpan?)null
                                 : (TimeSpan?)null,
                TimeOut    = HasColumn(r, "time_out") && !r.IsDBNull(r.GetOrdinal("time_out"))
                                 ? TimeSpan.TryParse(r.GetString(r.GetOrdinal("time_out")), out var tout) ? tout : (TimeSpan?)null
                                 : (TimeSpan?)null,
                Status     = HasColumn(r, "status") && !r.IsDBNull(r.GetOrdinal("status"))
                                 ? r.GetString(r.GetOrdinal("status"))
                                 : null,
                CreatedAt  = HasColumn(r, "created_at") && !r.IsDBNull(r.GetOrdinal("created_at"))
                                 ? DateTime.TryParse(r.GetString(r.GetOrdinal("created_at")), out var ca) ? ca : (DateTime?)null
                                 : (DateTime?)null,
                EmployeeName = HasColumn(r, "employee_name") && !r.IsDBNull(r.GetOrdinal("employee_name"))
                                 ? r.GetString(r.GetOrdinal("employee_name"))
                                 : null,
                UserId     = HasColumn(r, "user_id") && !r.IsDBNull(r.GetOrdinal("user_id"))
                                 ? r.GetInt32(r.GetOrdinal("user_id"))
                                 : (int?)null
            };
        }

        private static object ParamOrDbNull(object? value) => value ?? DBNull.Value;

        private static bool HasColumn(IDataRecord r, string name)
        {
            for (int i = 0; i < r.FieldCount; i++)
                if (string.Equals(r.GetName(i), name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
