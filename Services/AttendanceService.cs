#nullable enable
using SihyuPOSPayroll.Models;
using MySql.Data.MySqlClient;
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
        private readonly string _connectionString;

        public AttendanceService(string? connectionString = null)
        {
            _connectionString = string.IsNullOrWhiteSpace(connectionString)
                ? "server=localhost;user=root;password=;database=sihyu_pos;"
                : connectionString!;
        }

        public List<AttendanceModel> GetAllAttendances()
        {
            var attendances = new List<AttendanceModel>();
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                const string sql = @"
                    SELECT 
                        a.*,
                        e.full_name AS employee_name,
                        u.id AS user_id
                    FROM attendance a
                    LEFT JOIN employees e ON a.employee_id = e.id
                    LEFT JOIN users u ON u.employee_id = e.id
                    ORDER BY a.date DESC;";
                using var cmd = new MySqlCommand(sql, connection);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    attendances.Add(MapAttendance(reader));
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error loading all attendances: " + ex.Message);
            }
            return attendances;
        }

        public List<AttendanceModel> GetAttendances()
        {
            return GetAllAttendances();
        }

        public List<AttendanceModel> GetAttendances(DateTime? filterDate, int? employeeId)
        {
            var attendances = new List<AttendanceModel>();
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                
                var sql = @"
                    SELECT 
                        a.*,
                        e.full_name AS employee_name,
                        u.id AS user_id
                    FROM attendance a
                    LEFT JOIN employees e ON a.employee_id = e.id
                    LEFT JOIN users u ON u.employee_id = e.id
                    WHERE 1=1";

                var parameters = new List<MySqlParameter>();

                if (filterDate.HasValue)
                {
                    sql += " AND a.date = @FilterDate";
                    parameters.Add(new MySqlParameter("@FilterDate", filterDate.Value.Date));
                }

                if (employeeId.HasValue)
                {
                    sql += " AND a.employee_id = @EmployeeId";
                    parameters.Add(new MySqlParameter("@EmployeeId", employeeId.Value));
                }

                sql += " ORDER BY a.date DESC;";

                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddRange(parameters.ToArray());
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    attendances.Add(MapAttendance(reader));
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error loading filtered attendances: " + ex.Message);
            }
            return attendances;
        }

        public List<AttendanceModel> GetAttendancesForEmployee(int employeeId)
        {
            return GetAttendancesByEmployeeId(employeeId);
        }

        public List<AttendanceModel> GetAttendancesForEmployee(int employeeId, DateTime from, DateTime to)
        {
            var attendances = new List<AttendanceModel>();
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                const string sql = @"
                    SELECT 
                        a.*,
                        e.full_name AS employee_name,
                        u.id AS user_id
                    FROM attendance a
                    LEFT JOIN employees e ON a.employee_id = e.id
                    LEFT JOIN users u ON u.employee_id = e.id
                    WHERE a.employee_id = @EmployeeId
                    AND a.date BETWEEN @From AND @To
                    ORDER BY a.date DESC;";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                cmd.Parameters.AddWithValue("@From", from.Date);
                cmd.Parameters.AddWithValue("@To", to.Date);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    attendances.Add(MapAttendance(reader));
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error loading attendances for employee {employeeId} in date range: " + ex.Message);
            }
            return attendances;
        }

        public bool ClockIn(int employeeId)
        {
            return ClockIn(employeeId, DateTime.Today, DateTime.Now.TimeOfDay);
        }

        public bool ClockIn(int employeeId, DateTime dateTime)
        {
            return ClockIn(employeeId, dateTime.Date, dateTime.TimeOfDay);
        }

        public bool ClockOut(int employeeId)
        {
            return ClockOut(employeeId, DateTime.Today, DateTime.Now.TimeOfDay);
        }

        public bool ClockOut(int employeeId, DateTime dateTime)
        {
            return ClockOut(employeeId, dateTime.Date, dateTime.TimeOfDay);
        }

        public int GetWorkedDaysCount(int employeeId, DateTime startDate, DateTime endDate)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                const string sql = @"
                    SELECT COUNT(*) 
                    FROM attendance 
                    WHERE employee_id = @EmployeeId 
                    AND date BETWEEN @StartDate AND @EndDate 
                    AND time_in IS NOT NULL 
                    AND time_out IS NOT NULL;";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                cmd.Parameters.AddWithValue("@StartDate", startDate.Date);
                cmd.Parameters.AddWithValue("@EndDate", endDate.Date);
                var result = cmd.ExecuteScalar();
                return Convert.ToInt32(result ?? 0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error getting worked days count: " + ex.Message);
                return 0;
            }
        }

        public List<AttendanceModel> GetAttendancesByEmployeeId(int employeeId)
        {
            var attendances = new List<AttendanceModel>();
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                const string sql = @"
                    SELECT 
                        a.*,
                        e.full_name AS employee_name,
                        u.id AS user_id
                    FROM attendance a
                    LEFT JOIN employees e ON a.employee_id = e.id
                    LEFT JOIN users u ON u.employee_id = e.id
                    WHERE a.employee_id = @EmployeeId
                    ORDER BY a.date DESC;";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    attendances.Add(MapAttendance(reader));
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error loading attendances for employee {employeeId}: " + ex.Message);
            }
            return attendances;
        }

        public List<AttendanceModel> GetAttendancesByDateRange(DateTime start, DateTime end)
        {
            var attendances = new List<AttendanceModel>();
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                const string sql = @"
                    SELECT 
                        a.*,
                        e.full_name AS employee_name,
                        u.id AS user_id
                    FROM attendance a
                    LEFT JOIN employees e ON a.employee_id = e.id
                    LEFT JOIN users u ON u.employee_id = e.id
                    WHERE a.date BETWEEN @Start AND @End
                    ORDER BY a.date DESC;";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@Start", start.Date);
                cmd.Parameters.AddWithValue("@End", end.Date);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    attendances.Add(MapAttendance(reader));
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error loading attendances by date range: " + ex.Message);
            }
            return attendances;
        }

        public AttendanceModel? GetAttendanceById(int id)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                const string sql = @"
                    SELECT 
                        a.*,
                        e.full_name AS employee_name,
                        u.id AS user_id
                    FROM attendance a
                    LEFT JOIN employees e ON a.employee_id = e.id
                    LEFT JOIN users u ON u.employee_id = e.id
                    WHERE a.id = @Id
                    LIMIT 1;";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@Id", id);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return MapAttendance(reader);
                }
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
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                const string sql = @"
                    INSERT INTO attendance 
                    (employee_id, date, time_in, time_out, status, created_at)
                    VALUES
                    (@EmployeeId, @Date, @TimeIn, @TimeOut, @Status, @CreatedAt);
                    SELECT LAST_INSERT_ID();";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@EmployeeId", attendance.EmployeeId);
                cmd.Parameters.AddWithValue("@Date", attendance.Date.Date);
                cmd.Parameters.AddWithValue("@TimeIn", ParamOrDbNull(attendance.TimeIn));
                cmd.Parameters.AddWithValue("@TimeOut", ParamOrDbNull(attendance.TimeOut));
                cmd.Parameters.AddWithValue("@Status", ParamOrDbNull(attendance.Status));
                cmd.Parameters.AddWithValue("@CreatedAt", attendance.CreatedAt == default ? DateTime.Now : attendance.CreatedAt);
                var newIdObj = cmd.ExecuteScalar();
                attendance.Id = Convert.ToInt32(newIdObj);
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
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                const string sql = @"
                    UPDATE attendance SET
                        employee_id = @EmployeeId,
                        date = @Date,
                        time_in = @TimeIn,
                        time_out = @TimeOut,
                        status = @Status
                    WHERE id = @Id;";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@EmployeeId", attendance.EmployeeId);
                cmd.Parameters.AddWithValue("@Date", attendance.Date.Date);
                cmd.Parameters.AddWithValue("@TimeIn", ParamOrDbNull(attendance.TimeIn));
                cmd.Parameters.AddWithValue("@TimeOut", ParamOrDbNull(attendance.TimeOut));
                cmd.Parameters.AddWithValue("@Status", ParamOrDbNull(attendance.Status));
                cmd.Parameters.AddWithValue("@Id", attendance.Id);
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
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                const string sql = "DELETE FROM attendance WHERE id = @Id;";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@Id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error deleting attendance {id}: " + ex.Message);
                return false;
            }
        }

        public bool ClockIn(int employeeId, DateTime date, TimeSpan timeIn)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                const string sql = @"
                    INSERT INTO attendance (employee_id, date, time_in, status, created_at)
                    VALUES (@EmployeeId, @Date, @TimeIn, 'Clocked In', NOW())
                    ON DUPLICATE KEY UPDATE time_in = @TimeIn, status = 'Clocked In';";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                cmd.Parameters.AddWithValue("@Date", date.Date);
                cmd.Parameters.AddWithValue("@TimeIn", timeIn);
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
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                const string sql = @"
                    UPDATE attendance 
                    SET time_out = @TimeOut, status = 'Present'
                    WHERE employee_id = @EmployeeId AND date = @Date;";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                cmd.Parameters.AddWithValue("@Date", date.Date);
                cmd.Parameters.AddWithValue("@TimeOut", timeOut);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error clocking out: " + ex.Message);
                return false;
            }
        }

        private static AttendanceModel MapAttendance(MySqlDataReader r)
        {
            var model = new AttendanceModel
            {
                Id = r.GetInt32("id"),
                EmployeeId = r.GetInt32("employee_id"),
                Date = r.GetDateTime("date"),
                TimeIn = HasColumn(r, "time_in") && !r.IsDBNull(r.GetOrdinal("time_in")) 
                    ? r.GetTimeSpan("time_in") 
                    : (TimeSpan?)null,
                TimeOut = HasColumn(r, "time_out") && !r.IsDBNull(r.GetOrdinal("time_out")) 
                    ? r.GetTimeSpan("time_out") 
                    : (TimeSpan?)null,
                Status = r["status"]?.ToString(),
                CreatedAt = HasColumn(r, "created_at") && !r.IsDBNull(r.GetOrdinal("created_at")) 
                    ? r.GetDateTime("created_at") 
                    : (DateTime?)null,
                EmployeeName = HasColumn(r, "employee_name") && !r.IsDBNull(r.GetOrdinal("employee_name")) 
                    ? r["employee_name"]?.ToString() 
                    : null,
                UserId = HasColumn(r, "user_id") && !r.IsDBNull(r.GetOrdinal("user_id")) 
                    ? r.GetInt32("user_id") 
                    : (int?)null
            };
            return model;
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
