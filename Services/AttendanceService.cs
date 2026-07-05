#nullable enable
using SihyuPOSPayroll.Helpers;
using SihyuPOSPayroll.Models;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.CompilerServices;

namespace SihyuPOSPayroll.Services
{
    /// <summary>
    /// Exception that includes a precise reason + diagnostics + source file/line.
    /// </summary>
    public sealed class AttendanceValidationException : InvalidOperationException
    {
        public string Diagnostics { get; }
        public string CallerMember { get; }
        public string CallerFile { get; }
        public int CallerLine { get; }

        public AttendanceValidationException(
            string message,
            string diagnostics,
            string callerMember,
            string callerFile,
            int callerLine) : base(message)
        {
            Diagnostics = diagnostics;
            CallerMember = callerMember;
            CallerFile = callerFile;
            CallerLine = callerLine;
        }

        public override string Message =>
            base.Message +
            Environment.NewLine +
            Diagnostics +
            Environment.NewLine +
            $"@ {CallerMember} ({System.IO.Path.GetFileName(CallerFile)}:{CallerLine})";
    }

    public class AttendanceService
    {
        private readonly string _connectionString;

        public AttendanceService(string? connectionString = null)
        {
            _connectionString = string.IsNullOrWhiteSpace(connectionString)
                ? ConfigurationHelper.GetConnectionString()
                : connectionString!;
        }

        // =========================
        // REQUIRED: Worked-days counter (single source of truth for payroll!)
        // =========================

        /// <summary>
        /// Counts DISTINCT attendance dates for an employee in [start, end] where BOTH time_in AND time_out are NOT NULL.
        /// Time components are ignored (date-only comparison).
        /// </summary>
        public int GetWorkedDaysCount(int employeeId, DateTime start, DateTime end)
        {
            var s = start.Date;
            var e = end.Date;
            if (e < s) (s, e) = (e, s);

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                const string sql = @"
                    SELECT COUNT(DISTINCT a.date)
                    FROM attendance a
                    WHERE a.employee_id = @empId
                      AND a.date BETWEEN @start AND @end
                      AND a.time_in IS NOT NULL
                      AND a.time_out IS NOT NULL;";

                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@empId", employeeId);
                cmd.Parameters.Add("@start", MySqlDbType.Date).Value = s;
                cmd.Parameters.Add("@end", MySqlDbType.Date).Value = e;

                var obj = cmd.ExecuteScalar();
                return (obj == null || obj == DBNull.Value) ? 0 : Convert.ToInt32(obj);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("GetWorkedDaysCount error: " + ex.Message);
                return 0;
            }
        }

        /// <summary>Back-compat alias.</summary>
        public int CountWorkedDays(int employeeId, DateTime start, DateTime end)
            => GetWorkedDaysCount(employeeId, start, end);

        // =========================
        // NEW: Scheduling helpers
        // =========================

        /// <summary>
        /// Returns true if the employee is scheduled to work on the given calendar date,
        /// based on work_schedule.days_mask. If the employee has no schedule, returns false.
        /// </summary>
        public bool IsScheduledWorkDay(int employeeId, DateTime date)
        {
            var mask = GetDaysMaskForEmployee(employeeId);
            if (mask is null) return false;
            int bit = DayOfWeekToBit(date.DayOfWeek);
            return ((mask.Value & (1 << bit)) != 0);
        }

        /// <summary>
        /// Returns a dense per-day calendar between [from..to] (inclusive) for the employee,
        /// with computed display status per day:
        ///   - "Present"  ? BOTH time_in and time_out exist for that date
        ///   - "No Record"? scheduled workday but missing one/both logs
        ///   - "Day Off"  ? not a scheduled workday
        /// TimeIn is earliest log for the day, TimeOut is latest log for the day.
        /// </summary>
        public List<AttendanceModel> GetDailyAttendanceCalendar(int employeeId, DateTime from, DateTime to)
        {
            var s = from.Date;
            var e = to.Date;
            if (e < s) (s, e) = (e, s);

            // Preload all raw logs in the window
            var perDay = new Dictionary<DateTime, (TimeSpan? firstIn, TimeSpan? lastOut)>();

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                const string sql = @"
                    SELECT date, time_in, time_out
                    FROM attendance
                    WHERE employee_id = @empId
                      AND date BETWEEN @from AND @to
                    ORDER BY date ASC, time_in ASC, time_out ASC;";

                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@empId", employeeId);
                cmd.Parameters.Add("@from", MySqlDbType.Date).Value = s;
                cmd.Parameters.Add("@to", MySqlDbType.Date).Value = e;

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var d = r.GetDateTime("date").Date;
                    var ti = r.IsDBNull(r.GetOrdinal("time_in")) ? (TimeSpan?)null : r.GetTimeSpan("time_in");
                    var toT = r.IsDBNull(r.GetOrdinal("time_out")) ? (TimeSpan?)null : r.GetTimeSpan("time_out");

                    if (!perDay.TryGetValue(d, out var agg))
                        agg = (null, null);

                    // earliest time_in
                    if (ti.HasValue)
                        agg.firstIn = (agg.firstIn.HasValue && agg.firstIn.Value <= ti.Value) ? agg.firstIn : ti;

                    // latest time_out
                    if (toT.HasValue)
                        agg.lastOut = (agg.lastOut.HasValue && agg.lastOut.Value >= toT.Value) ? agg.lastOut : toT;

                    perDay[d] = agg;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("GetDailyAttendanceCalendar load error: " + ex.Message);
            }

            // Resolve days_mask once
            var mask = GetDaysMaskForEmployee(employeeId);

            var outRows = new List<AttendanceModel>();
            for (var d = s; d <= e; d = d.AddDays(1))
            {
                bool isWorkday = false;
                if (mask is not null)
                {
                    int bit = DayOfWeekToBit(d.DayOfWeek);
                    isWorkday = ((mask.Value & (1 << bit)) != 0);
                }

                perDay.TryGetValue(d, out var aggTimes);
                var hasIn = aggTimes.firstIn.HasValue;
                var hasOut = aggTimes.lastOut.HasValue;

                string display;
                if (!isWorkday)
                    display = "Day Off";
                else if (hasIn && hasOut)
                    display = "Present";
                else
                    display = "No Record";

                outRows.Add(new AttendanceModel
                {
                    Id = 0, // calendar row (not necessarily a DB row)
                    EmployeeId = employeeId,
                    Date = d,
                    TimeIn = aggTimes.firstIn,
                    TimeOut = aggTimes.lastOut,
                    // Status here carries the DISPLAY status for the day grid
                    Status = display
                });
            }

            return outRows;
        }

        // =========================
        // Auto-NOW versions (no timestamp passed)
        // =========================

        /// <summary>
        /// Inserts a new attendance row with CURDATE()/CURTIME() as time_in.
        /// Blocks if outside employee's assigned work schedule/shift.
        /// NOTE: Explicitly marks status='Present' on clock in.
        /// </summary>
        public void ClockIn(int employeeId)
        {
            ValidateWithinScheduledShiftOrThrow(employeeId, DateTime.Now);
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            const string sql = @"
                INSERT INTO attendance (employee_id, date, time_in, status)
                VALUES (@id, CURDATE(), CURTIME(), 'Present');";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", employeeId);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Sets time_out = CURTIME() for the most recent open record (today, no time_out yet).
        /// Blocks if outside employee's assigned work schedule/shift.
        /// </summary>
        public void ClockOut(int employeeId)
        {
            ValidateWithinScheduledShiftOrThrow(employeeId, DateTime.Now);

            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            const string sql = @"
                UPDATE attendance a
                JOIN (
                    SELECT id
                    FROM attendance
                    WHERE employee_id = @id
                      AND date = CURDATE()
                      AND time_out IS NULL
                    ORDER BY time_in DESC
                    LIMIT 1
                ) t ON a.id = t.id
                SET a.time_out = CURTIME();";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", employeeId);
            cmd.ExecuteNonQuery();
        }

        // =========================
        // Manual timestamp overloads
        // =========================

        /// <summary>
        /// Inserts a new row using the provided timestamp.
        /// Blocks if outside employee's assigned work schedule/shift at that time.
        /// NOTE: Explicitly marks status='Present' on clock in.
        /// </summary>
        public void ClockIn(int employeeId, DateTime timestamp)
        {
            ValidateWithinScheduledShiftOrThrow(employeeId, timestamp);

            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            const string sql = @"
                INSERT INTO attendance (employee_id, date, time_in, status)
                VALUES (@id, @date, @timeIn, 'Present');";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", employeeId);
            cmd.Parameters.Add("@date", MySqlDbType.Date).Value = timestamp.Date;
            cmd.Parameters.Add("@timeIn", MySqlDbType.Time).Value = timestamp.TimeOfDay;
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Sets time_out for the most recent open record on the same DATE as the timestamp.
        /// Blocks if outside employee's assigned work schedule/shift at that time.
        /// </summary>
        public void ClockOut(int employeeId, DateTime timestamp)
        {
            ValidateWithinScheduledShiftOrThrow(employeeId, timestamp);

            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            const string sql = @"
                UPDATE attendance a
                JOIN (
                    SELECT id
                    FROM attendance
                    WHERE employee_id = @id
                      AND date = @date
                      AND time_out IS NULL
                    ORDER BY time_in DESC
                    LIMIT 1
                ) t ON a.id = t.id
                SET a.time_out = @timeOut;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", employeeId);
            cmd.Parameters.Add("@date", MySqlDbType.Date).Value = timestamp.Date;
            cmd.Parameters.Add("@timeOut", MySqlDbType.Time).Value = timestamp.TimeOfDay;
            cmd.ExecuteNonQuery();
        }

        // =========================
        // Queries & Admin ops
        // =========================

        public List<AttendanceModel> GetAttendances(DateTime? date = null, int? employeeId = null)
        {
            var results = new List<AttendanceModel>();
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            var sql = "SELECT id, employee_id, date, time_in, time_out, status FROM attendance WHERE 1=1";
            if (date.HasValue) sql += " AND date = @date";
            if (employeeId.HasValue) sql += " AND employee_id = @employeeId";
            sql += " ORDER BY date DESC, time_in DESC, id DESC";

            using var cmd = new MySqlCommand(sql, conn);
            if (date.HasValue)
                cmd.Parameters.Add("@date", MySqlDbType.Date).Value = date.Value.Date;
            if (employeeId.HasValue)
                cmd.Parameters.AddWithValue("@employeeId", employeeId.Value);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new AttendanceModel
                {
                    Id = reader.GetInt32("id"),
                    EmployeeId = reader.GetInt32("employee_id"),
                    Date = reader.GetDateTime("date"),
                    TimeIn = reader.IsDBNull(reader.GetOrdinal("time_in")) ? (TimeSpan?)null : reader.GetTimeSpan("time_in"),
                    TimeOut = reader.IsDBNull(reader.GetOrdinal("time_out")) ? (TimeSpan?)null : reader.GetTimeSpan("time_out"),
                    Status = reader.IsDBNull(reader.GetOrdinal("status")) ? null : reader.GetString("status")
                });
            }
            return results;
        }

        public void AddAttendance(AttendanceModel model)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            const string sql = @"
                INSERT INTO attendance (employee_id, date, time_in, time_out, status)
                VALUES (@employeeId, @date, @timeIn, @timeOut, @status);";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@employeeId", model.EmployeeId);
            cmd.Parameters.Add("@date", MySqlDbType.Date).Value = model.Date.Date;
            cmd.Parameters.Add("@timeIn", MySqlDbType.Time).Value = model.TimeIn is null ? DBNull.Value : model.TimeIn;
            cmd.Parameters.Add("@timeOut", MySqlDbType.Time).Value = model.TimeOut is null ? DBNull.Value : model.TimeOut;
            // Do NOT default to 'Present' here; leave null unless explicitly provided.
            if (string.IsNullOrWhiteSpace(model.Status))
                cmd.Parameters.Add("@status", MySqlDbType.VarChar).Value = DBNull.Value;
            else
                cmd.Parameters.Add("@status", MySqlDbType.VarChar).Value = model.Status;
            cmd.ExecuteNonQuery();
        }

        public void UpdateAttendance(AttendanceModel model)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            const string sql = @"
                UPDATE attendance
                SET employee_id = @employeeId,
                    date        = @date,
                    time_in     = @timeIn,
                    time_out    = @timeOut,
                    status      = @status
                WHERE id = @id;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", model.Id);
            cmd.Parameters.AddWithValue("@employeeId", model.EmployeeId);
            cmd.Parameters.Add("@date", MySqlDbType.Date).Value = model.Date.Date;
            cmd.Parameters.Add("@timeIn", MySqlDbType.Time).Value = model.TimeIn is null ? DBNull.Value : model.TimeIn;
            cmd.Parameters.Add("@timeOut", MySqlDbType.Time).Value = model.TimeOut is null ? DBNull.Value : model.TimeOut;
            // Do NOT default to 'Present' here; keep as-is or null.
            if (string.IsNullOrWhiteSpace(model.Status))
                cmd.Parameters.Add("@status", MySqlDbType.VarChar).Value = DBNull.Value;
            else
                cmd.Parameters.Add("@status", MySqlDbType.VarChar).Value = model.Status;
            cmd.ExecuteNonQuery();
        }

        public void DeleteAttendance(int id)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            const string sql = "DELETE FROM attendance WHERE id = @id;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public List<AttendanceModel> GetAttendancesForEmployee(int employeeId, DateTime? from = null, DateTime? to = null)
        {
            var results = new List<AttendanceModel>();
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            var sql = @"
                SELECT id, employee_id, date, time_in, time_out, status
                FROM attendance
                WHERE employee_id = @employeeId";

            if (from.HasValue && to.HasValue)
                sql += " AND date BETWEEN @from AND @to";
            else if (from.HasValue)
                sql += " AND date >= @from";
            else if (to.HasValue)
                sql += " AND date <= @to";

            sql += " ORDER BY date DESC, time_in DESC, id DESC";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@employeeId", employeeId);
            if (from.HasValue) cmd.Parameters.Add("@from", MySqlDbType.Date).Value = from.Value.Date;
            if (to.HasValue) cmd.Parameters.Add("@to", MySqlDbType.Date).Value = to.Value.Date;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new AttendanceModel
                {
                    Id = reader.GetInt32("id"),
                    EmployeeId = reader.GetInt32("employee_id"),
                    Date = reader.GetDateTime("date"),
                    TimeIn = reader.IsDBNull(reader.GetOrdinal("time_in")) ? (TimeSpan?)null : reader.GetTimeSpan("time_in"),
                    TimeOut = reader.IsDBNull(reader.GetOrdinal("time_out")) ? (TimeSpan?)null : reader.GetTimeSpan("time_out"),
                    Status = reader.IsDBNull(reader.GetOrdinal("status")) ? null : reader.GetString("status")
                });
            }
            return results;
        }

        // =========================
        // Work-schedule / Shift gate WITH DIAGNOSTICS
        // =========================

        private void ValidateWithinScheduledShiftOrThrow(
            int employeeId,
            DateTime when,
            [CallerMemberName] string callerMember = "",
            [CallerFilePath] string callerFile = "",
            [CallerLineNumber] int callerLine = 0)
        {
            // 1) Load employee’s work schedule + shift
            int? workScheduleId = null;
            string? shiftName = null;

            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                const string empSql = @"SELECT work_schedule_id, shift FROM employees WHERE id = @id LIMIT 1;";
                using var empCmd = new MySqlCommand(empSql, conn);
                empCmd.Parameters.AddWithValue("@id", employeeId);
                using var r = empCmd.ExecuteReader();
                if (!r.Read())
                {
                    ThrowDetailed("Employee not found.",
                        $"employee_id={employeeId}",
                        callerMember, callerFile, callerLine);
                }

                workScheduleId = r.IsDBNull(r.GetOrdinal("work_schedule_id"))
                    ? (int?)null
                    : Convert.ToInt32(r["work_schedule_id"]);
                shiftName = r["shift"]?.ToString();
            }

            if (workScheduleId is null)
            {
                ThrowDetailed("No work schedule assigned.",
                    $"employee_id={employeeId}, shift='{shiftName ?? "(null)"}'",
                    callerMember, callerFile, callerLine);
            }

            if (string.IsNullOrWhiteSpace(shiftName))
            {
                ThrowDetailed("No shift assigned.",
                    $"employee_id={employeeId}, work_schedule_id={workScheduleId}",
                    callerMember, callerFile, callerLine);
            }

            // 2) Load days_mask
            byte daysMask;
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                const string wsSql = @"SELECT days_mask FROM work_schedule WHERE id = @id LIMIT 1;";
                using var wsCmd = new MySqlCommand(wsSql, conn);
                wsCmd.Parameters.AddWithValue("@id", workScheduleId!.Value);
                var obj = wsCmd.ExecuteScalar();
                if (obj == null || obj == DBNull.Value)
                {
                    ThrowDetailed("Work schedule not found for employee.",
                        $"employee_id={employeeId}, work_schedule_id={workScheduleId}",
                        callerMember, callerFile, callerLine);
                }
                daysMask = Convert.ToByte(obj);
            }

            // 3) Determine shift window (DB overrides -> fallback to defaults)
            var (start, end, source) = ResolveShiftWindow(shiftName!);

            // 4) Check day + time
            var today = when.DayOfWeek;
            int bit = DayOfWeekToBit(today);
            bool dayAllowed = (daysMask & (1 << bit)) != 0;

            // If crossing midnight and current TOD < end, evaluate previous day instead.
            bool crossesMidnight = end <= start;
            var tod = when.TimeOfDay;
            if (crossesMidnight && tod < end)
            {
                var prev = when.AddDays(-1).DayOfWeek;
                bit = DayOfWeekToBit(prev);
                dayAllowed = (daysMask & (1 << bit)) != 0;
            }

            bool timeAllowed = IsWithinWindow(tod, start, end);

            if (!(dayAllowed && timeAllowed))
            {
                var diag =
                    $"now={when:yyyy-MM-dd HH:mm:ss}, tod={tod:hh\\:mm\\:ss}, " +
                    $"shift='{shiftName}', window={FormatWindow(start, end)} (source={source}, crossesMidnight={crossesMidnight}), " +
                    $"mask={FormatMask(daysMask)}, dayCheckedBit={bit}, dayAllowed={dayAllowed}, timeAllowed={timeAllowed}";

                ThrowDetailed("You can only time in/out during your scheduled shift.", diag,
                    callerMember, callerFile, callerLine);
            }
        }

        // =========================
        // Private helpers
        // =========================

        private int? GetDaysMaskForEmployee(int employeeId)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();
                const string sql = @"
                    SELECT ws.days_mask
                    FROM employees e
                    LEFT JOIN work_schedule ws ON ws.id = e.work_schedule_id
                    WHERE e.id = @eid
                    LIMIT 1;";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@eid", employeeId);
                var res = cmd.ExecuteScalar();
                if (res == null || res == DBNull.Value) return null;
                return Convert.ToInt32(res);
            }
            catch
            {
                return null;
            }
        }

        private static void ThrowDetailed(
            string message,
            string diagnostics,
            string callerMember,
            string callerFile,
            int callerLine)
        {
            throw new AttendanceValidationException(message, diagnostics, callerMember, callerFile, callerLine);
        }

        private static int DayOfWeekToBit(DayOfWeek d) => d switch
        {
            DayOfWeek.Monday => 0,
            DayOfWeek.Tuesday => 1,
            DayOfWeek.Wednesday => 2,
            DayOfWeek.Thursday => 3,
            DayOfWeek.Friday => 4,
            DayOfWeek.Saturday => 5,
            DayOfWeek.Sunday => 6,
            _ => 0
        };

        private static bool IsWithinWindow(TimeSpan t, TimeSpan start, TimeSpan end)
        {
            if (end <= start) // crosses midnight
                return t >= start || t < end;
            return t >= start && t <= end;
        }

        private static string FormatMask(byte mask)
        {
            string[] names = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            var parts = new List<string>();
            for (int i = 0; i < 7; i++)
                if ((mask & (1 << i)) != 0) parts.Add(names[i]);
            return parts.Count == 0 ? "(none)" : string.Join(" ", parts);
        }

        private static string FormatWindow(TimeSpan start, TimeSpan end)
            => $"{start:hh\\:mm}-{end:hh\\:mm}";

        /// <summary>
        /// Tries DB table shift_definitions(name,start_time,end_time) first, then common defaults.
        /// Returns (start,end,source) where source is 'db' or 'default'.
        /// </summary>
        private (TimeSpan start, TimeSpan end, string source) ResolveShiftWindow(string raw)
        {
            var key = (raw ?? "").Trim().ToLowerInvariant();

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();
                const string s = @"SELECT start_time, end_time
                                   FROM shift_definitions
                                   WHERE LOWER(name) = LOWER(@n)
                                   LIMIT 1;";
                using var cmd = new MySqlCommand(s, conn);
                cmd.Parameters.AddWithValue("@n", key);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    var start = r.GetTimeSpan("start_time");
                    var end = r.GetTimeSpan("end_time");
                    return (start, end, "db");
                }
            }
            catch
            {
                // table may not exist; ignore and fallback
            }

            if (key is "morning" or "am" or "day" or "day shift")
                return (TimeSpan.FromHours(8), TimeSpan.FromHours(17), "default");
            if (key is "afternoon" or "pm" or "swing")
                return (TimeSpan.FromHours(13), TimeSpan.FromHours(22), "default");
            if (key is "night" or "graveyard" or "evening" or "night shift")
                return (TimeSpan.FromHours(22), TimeSpan.FromHours(6), "default");

            return (TimeSpan.FromHours(8), TimeSpan.FromHours(17), "default(fallback)");
        }
    }
}
