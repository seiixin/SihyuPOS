#nullable enable
using Microsoft.Data.Sqlite;
using SihyuPOSPayroll.Data;
using SihyuPOSPayroll.Models;
using System;
using System.Collections.Generic;
using System.Data;

namespace SihyuPOSPayroll.Services
{
    public interface IEmployeeService
    {
        List<EmployeeModel> GetAllEmployees();
        EmployeeModel? GetEmployeeById(int id);
        bool AddEmployee(EmployeeModel employee);
        bool UpdateEmployee(EmployeeModel employee);
        bool UpdateEmployeeImage(int employeeId, string imageUrl);
        bool DeleteEmployee(int employeeId);

        int? GetEmployeeIdByUserId(int userId);
        string? GetEmployeeFullName(int employeeId);
        int? GetUserIdByEmployeeId(int employeeId);
        int? GetWorkScheduleDaysMask(int employeeId);

        bool SetUserActiveStatusByEmployeeId(int employeeId, bool isActive);
        bool SetUserActiveStatusByUserId(int userId, bool isActive);
    }

    public sealed class EmployeeService : IEmployeeService
    {
        // =====================================================================
        // READ
        // =====================================================================

        public List<EmployeeModel> GetAllEmployees()
        {
            var employees = new List<EmployeeModel>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT
                        e.*,
                        u.id        AS user_id,
                        u.email     AS user_email,
                        u.role      AS user_role,
                        u.is_active AS user_is_active
                    FROM employees e
                    LEFT JOIN users u ON u.employee_id = e.id
                    ORDER BY e.id DESC;";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    employees.Add(MapEmployee(reader));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[EmployeeService] GetAllEmployees: {ex.Message}");
            }
            return employees;
        }

        public EmployeeModel? GetEmployeeById(int id)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT
                        e.*,
                        u.id        AS user_id,
                        u.email     AS user_email,
                        u.role      AS user_role,
                        u.is_active AS user_is_active
                    FROM employees e
                    LEFT JOIN users u ON u.employee_id = e.id
                    WHERE e.id = @id
                    LIMIT 1;";
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = cmd.ExecuteReader();
                return reader.Read() ? MapEmployee(reader) : null;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[EmployeeService] GetEmployeeById: {ex.Message}");
                return null;
            }
        }

        // =====================================================================
        // CREATE
        // =====================================================================

        public bool AddEmployee(EmployeeModel employee)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var tx   = conn.BeginTransaction();
                using var cmd  = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO employees
                        (full_name, age, sex, address, birthday, contact_number, position,
                         salary_per_day, work_schedule_id, shift, sss_number, philhealth_number,
                         pagibig_number, image_url, emergency_contact, date_hired, created_at)
                    VALUES
                        (@fullName, @age, @sex, @address, @birthday, @contactNumber, @position,
                         @salaryPerDay, @workScheduleId, @shift, @sssNumber, @philhealthNumber,
                         @pagibigNumber, @imageUrl, @emergencyContact, @dateHired, datetime('now'));
                    SELECT last_insert_rowid();";

                BindEmployeeParams(cmd, employee);

                var newId = Convert.ToInt32(cmd.ExecuteScalar());
                tx.Commit();
                employee.Id = newId;
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[EmployeeService] AddEmployee: {ex.Message}");
                return false;
            }
        }

        // =====================================================================
        // UPDATE
        // =====================================================================

        public bool UpdateEmployee(EmployeeModel employee)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE employees SET
                        full_name         = @fullName,
                        age               = @age,
                        sex               = @sex,
                        address           = @address,
                        birthday          = @birthday,
                        contact_number    = @contactNumber,
                        position          = @position,
                        salary_per_day    = @salaryPerDay,
                        work_schedule_id  = @workScheduleId,
                        shift             = @shift,
                        sss_number        = @sssNumber,
                        philhealth_number = @philhealthNumber,
                        pagibig_number    = @pagibigNumber,
                        image_url         = @imageUrl,
                        emergency_contact = @emergencyContact,
                        date_hired        = @dateHired
                    WHERE id = @id;";

                BindEmployeeParams(cmd, employee);
                cmd.Parameters.AddWithValue("@id", employee.Id);

                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[EmployeeService] UpdateEmployee: {ex.Message}");
                return false;
            }
        }

        public bool UpdateEmployeeImage(int employeeId, string imageUrl)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "UPDATE employees SET image_url = @url WHERE id = @id;";
                cmd.Parameters.AddWithValue("@url", (object?)imageUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@id",  employeeId);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[EmployeeService] UpdateEmployeeImage: {ex.Message}");
                return false;
            }
        }

        // =====================================================================
        // DELETE
        // =====================================================================

        public bool DeleteEmployee(int employeeId)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var tx   = conn.BeginTransaction();

                // FK ON DELETE CASCADE handles login_sessions; users FK is ON DELETE SET NULL.
                // Explicitly nullify the user's employee_id link first.
                using (var unlinkCmd = conn.CreateCommand())
                {
                    unlinkCmd.Transaction  = tx;
                    unlinkCmd.CommandText  = "UPDATE users SET employee_id = NULL WHERE employee_id = @eid;";
                    unlinkCmd.Parameters.AddWithValue("@eid", employeeId);
                    unlinkCmd.ExecuteNonQuery();
                }

                int rows;
                using (var delCmd = conn.CreateCommand())
                {
                    delCmd.Transaction  = tx;
                    delCmd.CommandText  = "DELETE FROM employees WHERE id = @id;";
                    delCmd.Parameters.AddWithValue("@id", employeeId);
                    rows = delCmd.ExecuteNonQuery();
                }

                tx.Commit();
                return rows > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[EmployeeService] DeleteEmployee: {ex.Message}");
                return false;
            }
        }

        // =====================================================================
        // USER ↔ EMPLOYEE MAPPING
        // =====================================================================

        public int? GetEmployeeIdByUserId(int userId)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "SELECT employee_id FROM users WHERE id = @uid LIMIT 1;";
                cmd.Parameters.AddWithValue("@uid", userId);
                var res = cmd.ExecuteScalar();
                if (res == null || res == DBNull.Value) return null;
                var val = Convert.ToInt32(res);
                return val == 0 ? null : val;
            }
            catch { return null; }
        }

        public int? GetUserIdByEmployeeId(int employeeId)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "SELECT id FROM users WHERE employee_id = @eid LIMIT 1;";
                cmd.Parameters.AddWithValue("@eid", employeeId);
                var res = cmd.ExecuteScalar();
                if (res == null || res == DBNull.Value) return null;
                var val = Convert.ToInt32(res);
                return val == 0 ? null : val;
            }
            catch { return null; }
        }

        public string? GetEmployeeFullName(int employeeId)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "SELECT full_name FROM employees WHERE id = @id LIMIT 1;";
                cmd.Parameters.AddWithValue("@id", employeeId);
                var res = cmd.ExecuteScalar();
                return res == null || res == DBNull.Value ? null : res.ToString();
            }
            catch { return null; }
        }

        public int? GetWorkScheduleDaysMask(int employeeId)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT ws.days_mask
                    FROM   employees e
                    LEFT JOIN work_schedule ws ON ws.id = e.work_schedule_id
                    WHERE  e.id = @eid
                    LIMIT  1;";
                cmd.Parameters.AddWithValue("@eid", employeeId);
                var res = cmd.ExecuteScalar();
                if (res == null || res == DBNull.Value) return null;
                try { return Convert.ToInt32(res); } catch { return null; }
            }
            catch { return null; }
        }

        // =====================================================================
        // ACTIVE / INACTIVE STATUS
        // =====================================================================

        public bool SetUserActiveStatusByEmployeeId(int employeeId, bool isActive)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "UPDATE users SET is_active = @active WHERE employee_id = @eid;";
                cmd.Parameters.AddWithValue("@active", isActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@eid",    employeeId);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[EmployeeService] SetUserActiveStatusByEmployeeId: {ex.Message}");
                return false;
            }
        }

        public bool SetUserActiveStatusByUserId(int userId, bool isActive)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "UPDATE users SET is_active = @active WHERE id = @uid;";
                cmd.Parameters.AddWithValue("@active", isActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@uid",    userId);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[EmployeeService] SetUserActiveStatusByUserId: {ex.Message}");
                return false;
            }
        }

        // =====================================================================
        // PRIVATE HELPERS
        // =====================================================================

        private static EmployeeModel MapEmployee(SqliteDataReader r)
        {
            var emp = new EmployeeModel
            {
                Id               = r.GetInt32(r.GetOrdinal("id")),
                FullName         = SafeString(r, "full_name"),
                Age              = SafeInt(r,    "age"),
                Sex              = SafeString(r, "sex"),
                Address          = SafeString(r, "address"),
                Birthday         = SafeDate(r,   "birthday"),
                ContactNumber    = SafeString(r, "contact_number"),
                Position         = SafeString(r, "position"),
                SalaryPerDay     = SafeDecimal(r,"salary_per_day"),
                WorkScheduleId   = SafeInt(r,    "work_schedule_id"),
                Shift            = SafeString(r, "shift"),
                SssNumber        = SafeString(r, "sss_number"),
                PhilhealthNumber = SafeString(r, "philhealth_number"),
                PagibigNumber    = SafeString(r, "pagibig_number"),
                ImageUrl         = SafeString(r, "image_url"),
                EmergencyContact = SafeString(r, "emergency_contact"),
                DateHired        = SafeDate(r,   "date_hired"),
                CreatedAt        = SafeDateTime(r,"created_at") ?? DateTime.Now,
            };

            // Attach linked user account when the JOIN returned one
            if (HasColumn(r, "user_id") && !r.IsDBNull(r.GetOrdinal("user_id")))
            {
                emp.UserAccount = new UserModel
                {
                    Id         = r.GetInt32(r.GetOrdinal("user_id")),
                    Email      = SafeString(r, "user_email"),
                    Role       = SafeString(r, "user_role"),
                    EmployeeId = emp.Id,
                    IsActive   = r.GetInt32(r.GetOrdinal("user_is_active")) == 1,
                };
            }

            return emp;
        }

        /// <summary>Bind INSERT/UPDATE parameters shared between Add and Update.</summary>
        private static void BindEmployeeParams(SqliteCommand cmd, EmployeeModel e)
        {
            cmd.Parameters.AddWithValue("@fullName",         (object?)e.FullName         ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@age",              (object?)e.Age               ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sex",              (object?)e.Sex               ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@address",          (object?)e.Address           ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@birthday",         e.Birthday.HasValue
                                                                 ? e.Birthday.Value.ToString("yyyy-MM-dd")
                                                                 : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@contactNumber",    (object?)e.ContactNumber     ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@position",         (object?)e.Position          ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@salaryPerDay",     (object?)e.SalaryPerDay      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@workScheduleId",   (object?)e.WorkScheduleId    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@shift",            (object?)e.Shift             ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sssNumber",        (object?)e.SssNumber         ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@philhealthNumber", (object?)e.PhilhealthNumber  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pagibigNumber",    (object?)e.PagibigNumber      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@imageUrl",         (object?)e.ImageUrl          ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@emergencyContact", (object?)e.EmergencyContact  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@dateHired",        e.DateHired.HasValue
                                                                 ? e.DateHired.Value.ToString("yyyy-MM-dd")
                                                                 : (object)DBNull.Value);
        }

        // ── Type-safe nullable readers ──────────────────────────────────────

        private static string? SafeString(IDataRecord r, string col)
        {
            int ord = r.GetOrdinal(col);
            return r.IsDBNull(ord) ? null : r.GetString(ord);
        }

        private static int? SafeInt(IDataRecord r, string col)
        {
            int ord = r.GetOrdinal(col);
            return r.IsDBNull(ord) ? null : r.GetInt32(ord);
        }

        private static decimal? SafeDecimal(IDataRecord r, string col)
        {
            int ord = r.GetOrdinal(col);
            if (r.IsDBNull(ord)) return null;
            return Convert.ToDecimal(r.GetValue(ord));
        }

        private static DateTime? SafeDate(IDataRecord r, string col)
        {
            int ord = r.GetOrdinal(col);
            if (r.IsDBNull(ord)) return null;
            var raw = r.GetString(ord);
            return DateTime.TryParse(raw, out var dt) ? dt : null;
        }

        private static DateTime? SafeDateTime(IDataRecord r, string col)
        {
            int ord;
            try { ord = r.GetOrdinal(col); } catch { return null; }
            if (r.IsDBNull(ord)) return null;
            var raw = r.GetString(ord);
            return DateTime.TryParse(raw, out var dt) ? dt : null;
        }

        private static bool HasColumn(IDataRecord r, string name)
        {
            for (int i = 0; i < r.FieldCount; i++)
                if (string.Equals(r.GetName(i), name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
