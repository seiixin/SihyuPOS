#nullable enable
using SihyuPOSPayroll.Helpers;
using SihyuPOSPayroll.Models;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;

namespace SihyuPOSPayroll.Services
{
    public interface IEmployeeService
    {
        List<EmployeeModel> GetAllEmployees();
        EmployeeModel? GetEmployeeById(int id);
        bool UpdateEmployee(EmployeeModel employee);
        bool UpdateEmployeeImage(int employeeId, string imageUrl);
        bool AddEmployee(EmployeeModel employee);
        bool DeleteEmployee(int employeeId);

        // Existing: map logged-in user (users.id) -> employees.id
        int? GetEmployeeIdByUserId(int userId);

        // NEW: header & schedule helpers (employeeId → info)
        string? GetEmployeeFullName(int employeeId);
        int? GetUserIdByEmployeeId(int employeeId);
        int? GetWorkScheduleDaysMask(int employeeId);

        // NEW: status controls
        bool SetUserActiveStatusByEmployeeId(int employeeId, bool isActive);
        bool SetUserActiveStatusByUserId(int userId, bool isActive);
    }

    public sealed class EmployeeService : IEmployeeService
    {
        private readonly string _connectionString;

        public EmployeeService(string? connectionString = null)
        {
            _connectionString = string.IsNullOrWhiteSpace(connectionString)
                ? ConfigurationHelper.GetConnectionString()
                : connectionString!;
        }

        // =====================================================================
        // READ
        // =====================================================================

        public List<EmployeeModel> GetAllEmployees()
        {
            var employees = new List<EmployeeModel>();

            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                const string sql = @"
                    SELECT 
                        e.*,
                        u.id         AS user_id,
                        u.email      AS email,
                        u.role       AS role,
                        u.is_active  AS user_is_active
                    FROM employees e
                    LEFT JOIN users u ON u.employee_id = e.id
                    ORDER BY e.id DESC;";

                using var cmd = new MySqlCommand(sql, connection);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var emp = MapEmployee(reader);

                    if (HasColumn(reader, "user_id") && !reader.IsDBNull(reader.GetOrdinal("user_id")))
                    {
                        emp.UserAccount = new UserModel
                        {
                            Id = reader.GetInt32("user_id"),
                            Email = reader["email"]?.ToString(),
                            Role = reader["role"]?.ToString(),
                            EmployeeId = emp.Id,
                            IsActive = ReadTinyIntBool(reader, "user_is_active", defaultValue: true)
                        };
                    }

                    employees.Add(emp);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error loading employees: " + ex.Message);
            }

            return employees;
        }

        public EmployeeModel? GetEmployeeById(int id)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                const string sql = @"
                    SELECT 
                        e.*,
                        u.id         AS user_id,
                        u.email      AS email,
                        u.role       AS role,
                        u.is_active  AS user_is_active
                    FROM employees e
                    LEFT JOIN users u ON u.employee_id = e.id
                    WHERE e.id = @id
                    LIMIT 1;";

                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read()) return null;

                var emp = MapEmployee(reader);

                if (HasColumn(reader, "user_id") && !reader.IsDBNull(reader.GetOrdinal("user_id")))
                {
                    emp.UserAccount = new UserModel
                    {
                        Id = reader.GetInt32("user_id"),
                        Email = reader["email"]?.ToString(),
                        Role = reader["role"]?.ToString(),
                        EmployeeId = emp.Id,
                        IsActive = ReadTinyIntBool(reader, "user_is_active", defaultValue: true)
                    };
                }

                return emp;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error loading employee: " + ex.Message);
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
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                using var tx = connection.BeginTransaction();

                const string sql = @"
                    INSERT INTO employees 
                    (full_name, age, sex, address, birthday, contact_number, position, salary_per_day, work_schedule_id, shift,
                     sss_number, philhealth_number, pagibig_number, image_url, emergency_contact, date_hired, created_at)
                    VALUES
                    (@FullName, @Age, @Sex, @Address, @Birthday, @ContactNumber, @Position, @SalaryPerDay, @WorkScheduleId, @Shift,
                     @SssNumber, @PhilhealthNumber, @PagibigNumber, @ImageUrl, @EmergencyContact, @DateHired, @CreatedAt);
                    SELECT LAST_INSERT_ID();";

                using var cmd = new MySqlCommand(sql, connection, tx);
                cmd.Parameters.AddWithValue("@FullName", ParamOrDbNull(employee.FullName));
                cmd.Parameters.AddWithValue("@Age", ParamOrDbNull(employee.Age));
                cmd.Parameters.AddWithValue("@Sex", ParamOrDbNull(employee.Sex));
                cmd.Parameters.AddWithValue("@Address", ParamOrDbNull(employee.Address));
                cmd.Parameters.AddWithValue("@Birthday", ParamOrDbNull(employee.Birthday));
                cmd.Parameters.AddWithValue("@ContactNumber", ParamOrDbNull(employee.ContactNumber));
                cmd.Parameters.AddWithValue("@Position", ParamOrDbNull(employee.Position));
                cmd.Parameters.AddWithValue("@SalaryPerDay", ParamOrDbNull(employee.SalaryPerDay));
                cmd.Parameters.AddWithValue("@WorkScheduleId", ParamOrDbNull(employee.WorkScheduleId));
                cmd.Parameters.AddWithValue("@Shift", ParamOrDbNull(employee.Shift));
                cmd.Parameters.AddWithValue("@SssNumber", ParamOrDbNull(employee.SssNumber));
                cmd.Parameters.AddWithValue("@PhilhealthNumber", ParamOrDbNull(employee.PhilhealthNumber));
                cmd.Parameters.AddWithValue("@PagibigNumber", ParamOrDbNull(employee.PagibigNumber));
                cmd.Parameters.AddWithValue("@ImageUrl", ParamOrDbNull(employee.ImageUrl));
                cmd.Parameters.AddWithValue("@EmergencyContact", ParamOrDbNull(employee.EmergencyContact));
                cmd.Parameters.AddWithValue("@DateHired", ParamOrDbNull(employee.DateHired));
                cmd.Parameters.AddWithValue("@CreatedAt", employee.CreatedAt == default ? DateTime.Now : employee.CreatedAt);

                var newIdObj = cmd.ExecuteScalar();
                tx.Commit();

                employee.Id = Convert.ToInt32(newIdObj);
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error adding employee: " + ex.Message);
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
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                const string sql = @"
                    UPDATE employees SET
                        full_name         = @FullName,
                        age               = @Age,
                        sex               = @Sex,
                        address           = @Address,
                        birthday          = @Birthday,
                        contact_number    = @ContactNumber,
                        position          = @Position,
                        salary_per_day    = @SalaryPerDay,
                        work_schedule_id  = @WorkScheduleId,
                        shift             = @Shift,
                        sss_number        = @SssNumber,
                        philhealth_number = @PhilhealthNumber,
                        pagibig_number    = @PagibigNumber,
                        image_url         = @ImageUrl,
                        emergency_contact = @EmergencyContact,
                        date_hired        = @DateHired
                    WHERE id = @Id;";

                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@FullName", ParamOrDbNull(employee.FullName));
                cmd.Parameters.AddWithValue("@Age", ParamOrDbNull(employee.Age));
                cmd.Parameters.AddWithValue("@Sex", ParamOrDbNull(employee.Sex));
                cmd.Parameters.AddWithValue("@Address", ParamOrDbNull(employee.Address));
                cmd.Parameters.AddWithValue("@Birthday", ParamOrDbNull(employee.Birthday));
                cmd.Parameters.AddWithValue("@ContactNumber", ParamOrDbNull(employee.ContactNumber));
                cmd.Parameters.AddWithValue("@Position", ParamOrDbNull(employee.Position));
                cmd.Parameters.AddWithValue("@SalaryPerDay", ParamOrDbNull(employee.SalaryPerDay));
                cmd.Parameters.AddWithValue("@WorkScheduleId", ParamOrDbNull(employee.WorkScheduleId));
                cmd.Parameters.AddWithValue("@Shift", ParamOrDbNull(employee.Shift));
                cmd.Parameters.AddWithValue("@SssNumber", ParamOrDbNull(employee.SssNumber));
                cmd.Parameters.AddWithValue("@PhilhealthNumber", ParamOrDbNull(employee.PhilhealthNumber));
                cmd.Parameters.AddWithValue("@PagibigNumber", ParamOrDbNull(employee.PagibigNumber));
                cmd.Parameters.AddWithValue("@ImageUrl", ParamOrDbNull(employee.ImageUrl));
                cmd.Parameters.AddWithValue("@EmergencyContact", ParamOrDbNull(employee.EmergencyContact));
                cmd.Parameters.AddWithValue("@DateHired", ParamOrDbNull(employee.DateHired));
                cmd.Parameters.AddWithValue("@Id", employee.Id);

                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error updating employee: " + ex.Message);
                return false;
            }
        }

        public bool UpdateEmployeeImage(int employeeId, string imageUrl)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                const string sql = "UPDATE employees SET image_url = @ImageUrl WHERE id = @Id;";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@ImageUrl", ParamOrDbNull(imageUrl));
                cmd.Parameters.AddWithValue("@Id", employeeId);

                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error updating employee image: " + ex.Message);
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
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                using var tx = connection.BeginTransaction();

                using (var delUser = new MySqlCommand("DELETE FROM users WHERE employee_id = @eid;", connection, tx))
                {
                    delUser.Parameters.AddWithValue("@eid", employeeId);
                    delUser.ExecuteNonQuery();
                }

                int rows;
                using (var delEmp = new MySqlCommand("DELETE FROM employees WHERE id = @id;", connection, tx))
                {
                    delEmp.Parameters.AddWithValue("@id", employeeId);
                    rows = delEmp.ExecuteNonQuery();
                }

                tx.Commit();
                return rows > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error deleting employee: " + ex.Message);
                return false;
            }
        }

        // =====================================================================
        // USER ↔ EMPLOYEE MAPPING
        // =====================================================================

        public int? GetEmployeeIdByUserId(int userId)
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            const string sql = "SELECT employee_id FROM users WHERE id = @uid LIMIT 1;";
            using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@uid", userId);

            var res = cmd.ExecuteScalar();
            if (res == null || res == DBNull.Value) return null;

            var empId = Convert.ToInt32(res);
            return empId == 0 ? (int?)null : empId;
        }

        // NEW: employeeId → userId
        public int? GetUserIdByEmployeeId(int employeeId)
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            const string sql = "SELECT id FROM users WHERE employee_id = @eid LIMIT 1;";
            using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@eid", employeeId);

            var res = cmd.ExecuteScalar();
            if (res == null || res == DBNull.Value) return null;

            var uid = Convert.ToInt32(res);
            return uid == 0 ? (int?)null : uid;
        }

        // NEW: employee full name
        public string? GetEmployeeFullName(int employeeId)
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            const string sql = "SELECT full_name FROM employees WHERE id = @id LIMIT 1;";
            using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@id", employeeId);

            var res = cmd.ExecuteScalar();
            return res == null || res == DBNull.Value ? null : res.ToString();
        }

        // NEW: days_mask of assigned work schedule (nullable if none)
        public int? GetWorkScheduleDaysMask(int employeeId)
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            const string sql = @"
                SELECT ws.days_mask
                FROM employees e
                LEFT JOIN work_schedule ws ON ws.id = e.work_schedule_id
                WHERE e.id = @eid
                LIMIT 1;";
            using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@eid", employeeId);

            var res = cmd.ExecuteScalar();
            if (res == null || res == DBNull.Value) return null;

            try { return Convert.ToInt32(res); }
            catch { return null; }
        }

        // =====================================================================
        // NEW: Active/Inactive status setters
        // =====================================================================

        public bool SetUserActiveStatusByEmployeeId(int employeeId, bool isActive)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                const string sql = "UPDATE users SET is_active = @active WHERE employee_id = @eid;";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@active", isActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@eid", employeeId);

                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error updating user active status by employee_id: " + ex.Message);
                return false;
            }
        }

        public bool SetUserActiveStatusByUserId(int userId, bool isActive)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                const string sql = "UPDATE users SET is_active = @active WHERE id = @uid;";
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@active", isActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@uid", userId);

                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error updating user active status by user_id: " + ex.Message);
                return false;
            }
        }

        // =====================================================================
        // Mapper / helpers
        // =====================================================================

        private static EmployeeModel MapEmployee(MySqlDataReader r)
        {
            var model = new EmployeeModel
            {
                Id = r.GetInt32("id"),
                FullName = r["full_name"]?.ToString(),
                Age = HasColumn(r, "age") && !r.IsDBNull(r.GetOrdinal("age")) ? r.GetInt32("age") : (int?)null,
                Sex = r["sex"]?.ToString(),
                Address = r["address"]?.ToString(),
                Birthday = HasColumn(r, "birthday") && !r.IsDBNull(r.GetOrdinal("birthday")) ? r.GetDateTime("birthday") : (DateTime?)null,
                ContactNumber = r["contact_number"]?.ToString(),
                Position = r["position"]?.ToString(),
                SalaryPerDay = HasColumn(r, "salary_per_day") && !r.IsDBNull(r.GetOrdinal("salary_per_day")) ? r.GetDecimal("salary_per_day") : (decimal?)null,

                WorkScheduleId = HasColumn(r, "work_schedule_id") && !r.IsDBNull(r.GetOrdinal("work_schedule_id")) ? r.GetInt32("work_schedule_id") : (int?)null,
                Shift = r["shift"]?.ToString(),
                SssNumber = r["sss_number"]?.ToString(),
                PhilhealthNumber = r["philhealth_number"]?.ToString(),
                PagibigNumber = r["pagibig_number"]?.ToString(),
                ImageUrl = r["image_url"]?.ToString(),
                EmergencyContact = r["emergency_contact"]?.ToString(),
                DateHired = HasColumn(r, "date_hired") && !r.IsDBNull(r.GetOrdinal("date_hired")) ? r.GetDateTime("date_hired") : (DateTime?)null,
                CreatedAt = HasColumn(r, "created_at") && !r.IsDBNull(r.GetOrdinal("created_at")) ? r.GetDateTime("created_at") : DateTime.Now
            };

            return model;
        }

        private static bool ReadTinyIntBool(IDataRecord r, string col, bool defaultValue = false)
        {
            if (!HasColumn(r, col) || r.IsDBNull(r.GetOrdinal(col))) return defaultValue;
            try { return Convert.ToInt32(r[col]) == 1; }
            catch { return defaultValue; }
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
