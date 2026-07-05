using SihyuPOSPayroll.Helpers;
using SihyuPOSPayroll.Models;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;

namespace SihyuPOSPayroll.Services
{
    public class PayslipService
    {
        private readonly string _connectionString;

        public PayslipService(string? connectionString = null)
        {
            _connectionString = string.IsNullOrWhiteSpace(connectionString)
                ? ConfigurationHelper.GetConnectionString()
                : connectionString!;
        }
        private const int CommandTimeoutSeconds = 15;

        // ------------------------------------------------------------
        // OPTIONAL: call ONCE at app startup (App.xaml.cs OnStartup)
        // to create columns if they don't exist yet.
        // Or run the equivalent SQL once manually and NEVER call this.
        // ------------------------------------------------------------
        private static bool _schemaChecked = false;
        public static void EnsureSchemaAtStartup(string? connectionString = null)
        {
            if (_schemaChecked) return;

            try
            {
                string cs = string.IsNullOrWhiteSpace(connectionString)
                    ? ConfigurationHelper.GetConnectionString()
                    : connectionString!;
                using var conn = new MySqlConnection(cs);
                conn.Open();

                var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                const string colCheckSql = @"
                    SELECT COLUMN_NAME
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'PayslipRequests'
                      AND COLUMN_NAME IN ('PayrollId','Reason');";

                using (var checkCmd = new MySqlCommand(colCheckSql, conn))
                {
                    checkCmd.CommandTimeout = CommandTimeoutSeconds;
                    using var rdr = checkCmd.ExecuteReader();
                    while (rdr.Read()) existing.Add(rdr.GetString(0));
                }

                // Perform DDL only if really needed, and NEVER inside a user TX.
                if (!existing.Contains("PayrollId"))
                {
                    using var alter1 = new MySqlCommand("ALTER TABLE PayslipRequests ADD COLUMN PayrollId INT NULL AFTER EmployeeId;", conn);
                    alter1.CommandTimeout = CommandTimeoutSeconds;
                    alter1.ExecuteNonQuery();
                }
                if (!existing.Contains("Reason"))
                {
                    using var alter2 = new MySqlCommand("ALTER TABLE PayslipRequests ADD COLUMN Reason TEXT NULL AFTER Status;", conn);
                    alter2.CommandTimeout = CommandTimeoutSeconds;
                    alter2.ExecuteNonQuery();
                }

                _schemaChecked = true;
            }
            catch (Exception ex)
            {
                // Log only; do NOT rethrow — receipts/others must continue to work.
                Console.Error.WriteLine($"[PayslipService] EnsureSchemaAtStartup warning: {ex.Message}");
            }
        }

        #region ===== Helpers: readers =====

        private static T Get<T>(MySqlDataReader r, string name)
        {
            int o = r.GetOrdinal(name);
            return r.GetFieldValue<T>(o);
        }

        private static T? GetNullable<T>(MySqlDataReader r, string name) where T : struct
        {
            int o = r.GetOrdinal(name);
            return r.IsDBNull(o) ? (T?)null : r.GetFieldValue<T>(o);
        }

        private static string? GetNullableString(MySqlDataReader r, string name)
        {
            int o = r.GetOrdinal(name);
            return r.IsDBNull(o) ? null : r.GetString(o);
        }

        private static MySqlCommand Cmd(MySqlConnection conn, string sql, MySqlTransaction? tx = null)
        {
            var c = new MySqlCommand(sql, conn, tx);
            c.CommandTimeout = CommandTimeoutSeconds;
            return c;
        }

        #endregion

        // ------------------------------------------------------------
        // Create payslips from finalized payroll rows for a period
        // ------------------------------------------------------------
        public int CreatePayslipsFromPayrollPeriod(DateTime periodStart, DateTime periodEnd)
        {
            int created = 0;

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();
                using var tx = conn.BeginTransaction();

                const string selectPayrollSql = @"
                    SELECT
                        p.employee_id,
                        p.sss_deduction,
                        p.philhealth_deduction,
                        p.pagibig_deduction,
                        p.other_deductions,
                        p.net_salary
                    FROM payroll p
                    WHERE p.start_date = @start AND p.end_date = @end;";

                const string existsPayslipSql = @"
                    SELECT COUNT(*) FROM Payslips
                    WHERE EmployeeId = @empId AND PayDate = @payDate;";

                const string insertPayslipSql = @"
                    INSERT INTO Payslips
                        (EmployeeId, PayDate, HoursWorked, RatePerHour, Deductions, NetSalary, UpdatedDate)
                    VALUES
                        (@empId, @payDate, @hoursWorked, @ratePerHour, @deductions, @netSalary, NOW());";

                using var selectCmd = Cmd(conn, selectPayrollSql, tx);
                selectCmd.Parameters.AddWithValue("@start", periodStart.Date);
                selectCmd.Parameters.AddWithValue("@end", periodEnd.Date);

                using var reader = selectCmd.ExecuteReader();
                var rows = new List<(int EmpId, decimal Deductions, decimal Net)>();
                while (reader.Read())
                {
                    int empId = Get<int>(reader, "employee_id");
                    decimal sss = reader.IsDBNull(reader.GetOrdinal("sss_deduction")) ? 0m : reader.GetDecimal(reader.GetOrdinal("sss_deduction"));
                    decimal ph = reader.IsDBNull(reader.GetOrdinal("philhealth_deduction")) ? 0m : reader.GetDecimal(reader.GetOrdinal("philhealth_deduction"));
                    decimal pi = reader.IsDBNull(reader.GetOrdinal("pagibig_deduction")) ? 0m : reader.GetDecimal(reader.GetOrdinal("pagibig_deduction"));
                    decimal oth = reader.IsDBNull(reader.GetOrdinal("other_deductions")) ? 0m : reader.GetDecimal(reader.GetOrdinal("other_deductions"));
                    decimal net = reader.IsDBNull(reader.GetOrdinal("net_salary")) ? 0m : reader.GetDecimal(reader.GetOrdinal("net_salary"));
                    rows.Add((empId, sss + ph + pi + oth, net));
                }
                reader.Close();

                foreach (var row in rows)
                {
                    using var existsCmd = Cmd(conn, existsPayslipSql, tx);
                    existsCmd.Parameters.AddWithValue("@empId", row.EmpId);
                    existsCmd.Parameters.AddWithValue("@payDate", periodEnd.Date);
                    var exists = Convert.ToInt32(existsCmd.ExecuteScalar()) > 0;
                    if (exists) continue;

                    using var insertCmd = Cmd(conn, insertPayslipSql, tx);
                    insertCmd.Parameters.AddWithValue("@empId", row.EmpId);
                    insertCmd.Parameters.AddWithValue("@payDate", periodEnd.Date);
                    insertCmd.Parameters.AddWithValue("@hoursWorked", 0m);
                    insertCmd.Parameters.AddWithValue("@ratePerHour", 0m);
                    insertCmd.Parameters.AddWithValue("@deductions", row.Deductions);
                    insertCmd.Parameters.AddWithValue("@netSalary", row.Net);

                    if (insertCmd.ExecuteNonQuery() > 0) created++;
                }

                tx.Commit();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error creating payslips: {ex.Message}");
            }

            return created;
        }

        // ------------------------------------------------------------
        // Create a payslip from one payroll row (by payrollId)
        // ------------------------------------------------------------
        public int CreatePayslipFromPayrollId(int payrollId)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();
                using var tx = conn.BeginTransaction();

                const string selectOne = @"
                    SELECT employee_id, start_date, end_date,
                           sss_deduction, philhealth_deduction, pagibig_deduction, other_deductions,
                           net_salary
                    FROM payroll
                    WHERE id = @pid
                    LIMIT 1;";

                const string existsPayslipSql = @"
                    SELECT COUNT(*) FROM Payslips
                    WHERE EmployeeId = @empId AND PayDate = @payDate;";

                const string insertPayslipSql = @"
                    INSERT INTO Payslips
                        (EmployeeId, PayDate, HoursWorked, RatePerHour, Deductions, NetSalary, UpdatedDate)
                    VALUES
                        (@empId, @payDate, 0, 0, @deductions, @netSalary, NOW());";

                int affected = 0;

                int empId;
                DateTime endDate;
                decimal deductions = 0m;
                decimal net = 0m;

                using (var cmd = Cmd(conn, selectOne, tx))
                {
                    cmd.Parameters.AddWithValue("@pid", payrollId);
                    using var rdr = cmd.ExecuteReader();
                    if (!rdr.Read()) throw new Exception($"Payroll row not found: {payrollId}");

                    empId = Get<int>(rdr, "employee_id");
                    endDate = Get<DateTime>(rdr, "end_date");
                    decimal sss = rdr.IsDBNull(rdr.GetOrdinal("sss_deduction")) ? 0m : rdr.GetDecimal(rdr.GetOrdinal("sss_deduction"));
                    decimal ph = rdr.IsDBNull(rdr.GetOrdinal("philhealth_deduction")) ? 0m : rdr.GetDecimal(rdr.GetOrdinal("philhealth_deduction"));
                    decimal pi = rdr.IsDBNull(rdr.GetOrdinal("pagibig_deduction")) ? 0m : rdr.GetDecimal(rdr.GetOrdinal("pagibig_deduction"));
                    decimal oth = rdr.IsDBNull(rdr.GetOrdinal("other_deductions")) ? 0m : rdr.GetDecimal(rdr.GetOrdinal("other_deductions"));
                    net = rdr.IsDBNull(rdr.GetOrdinal("net_salary")) ? 0m : rdr.GetDecimal(rdr.GetOrdinal("net_salary"));
                    deductions = sss + ph + pi + oth;
                }

                using (var existsCmd = Cmd(conn, existsPayslipSql, tx))
                {
                    existsCmd.Parameters.AddWithValue("@empId", empId);
                    existsCmd.Parameters.AddWithValue("@payDate", endDate.Date);
                    var exists = Convert.ToInt32(existsCmd.ExecuteScalar()) > 0;
                    if (!exists)
                    {
                        using var ins = Cmd(conn, insertPayslipSql, tx);
                        ins.Parameters.AddWithValue("@empId", empId);
                        ins.Parameters.AddWithValue("@payDate", endDate.Date);
                        ins.Parameters.AddWithValue("@deductions", deductions);
                        ins.Parameters.AddWithValue("@netSalary", net);
                        affected = ins.ExecuteNonQuery();
                    }
                }

                tx.Commit();
                return affected;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating payslip from payroll: {ex.Message}");
            }
        }

        // ------------------------------------------------------------
        // Employee payslips
        // ------------------------------------------------------------
        public List<PayslipModel> GetEmployeePayslips(int employeeId)
        {
            var list = new List<PayslipModel>();
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();
                using var cmd = Cmd(conn, @"
                    SELECT PayslipId, EmployeeId, PayDate, HoursWorked, RatePerHour, Deductions, NetSalary, UpdatedDate
                    FROM Payslips 
                    WHERE EmployeeId = @EmployeeId
                    ORDER BY PayDate DESC");
                cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new PayslipModel
                    {
                        PayslipId = Get<int>(reader, "PayslipId"),
                        EmployeeId = Get<int>(reader, "EmployeeId"),
                        FullName = string.Empty,
                        PayDate = Get<DateTime>(reader, "PayDate"),
                        HoursWorked = reader.IsDBNull(reader.GetOrdinal("HoursWorked")) ? 0m : reader.GetDecimal(reader.GetOrdinal("HoursWorked")),
                        RatePerHour = reader.IsDBNull(reader.GetOrdinal("RatePerHour")) ? 0m : reader.GetDecimal(reader.GetOrdinal("RatePerHour")),
                        Deductions = reader.IsDBNull(reader.GetOrdinal("Deductions")) ? 0m : reader.GetDecimal(reader.GetOrdinal("Deductions")),
                        NetSalary = reader.IsDBNull(reader.GetOrdinal("NetSalary")) ? 0m : reader.GetDecimal(reader.GetOrdinal("NetSalary")),
                        UpdatedDate = GetNullable<DateTime>(reader, "UpdatedDate"),
                        Status = "Pending"
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving payslips: {ex.Message}");
            }
            return list;
        }

        // ------------------------------------------------------------
        // Payslip Requests
        // ------------------------------------------------------------
        public List<PayslipRequestModel> GetAllPayslipRequests()
        {
            var list = new List<PayslipRequestModel>();
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                using var cmd = Cmd(conn, @"
                    SELECT Id, EmployeeId, PayrollId, FullName, RequestDate, Status, Reason, UpdatedDate 
                    FROM PayslipRequests 
                    ORDER BY RequestDate DESC");

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new PayslipRequestModel
                    {
                        Id = Get<int>(reader, "Id"),
                        EmployeeId = Get<int>(reader, "EmployeeId"),
                        PayrollId = reader.IsDBNull(reader.GetOrdinal("PayrollId")) ? 0 : reader.GetInt32(reader.GetOrdinal("PayrollId")),
                        FullName = Get<string>(reader, "FullName"),
                        RequestDate = Get<DateTime>(reader, "RequestDate"),
                        Status = Get<string>(reader, "Status"),
                        Reason = GetNullableString(reader, "Reason"),
                        UpdatedDate = GetNullable<DateTime>(reader, "UpdatedDate")
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving payslip requests: {ex.Message}");
            }
            return list;
        }

        public PayslipRequestModel? GetPayslipRequestById(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                using var cmd = Cmd(conn, @"
                    SELECT Id, EmployeeId, PayrollId, FullName, RequestDate, Status, Reason, UpdatedDate 
                    FROM PayslipRequests 
                    WHERE Id = @Id");
                cmd.Parameters.AddWithValue("@Id", id);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return new PayslipRequestModel
                    {
                        Id = Get<int>(reader, "Id"),
                        EmployeeId = Get<int>(reader, "EmployeeId"),
                        PayrollId = reader.IsDBNull(reader.GetOrdinal("PayrollId")) ? 0 : reader.GetInt32(reader.GetOrdinal("PayrollId")),
                        FullName = Get<string>(reader, "FullName"),
                        RequestDate = Get<DateTime>(reader, "RequestDate"),
                        Status = Get<string>(reader, "Status"),
                        Reason = GetNullableString(reader, "Reason"),
                        UpdatedDate = GetNullable<DateTime>(reader, "UpdatedDate")
                    };
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving payslip request: {ex.Message}");
            }
            return null;
        }

        // Create request
        public bool CreatePayslipRequest(int employeeId, int payrollId, string fullName, string? reason = null)
            => CreatePayslipRequest(new PayslipRequestModel
            {
                EmployeeId = employeeId,
                PayrollId = payrollId,
                FullName = fullName,
                Reason = reason,
                Status = "Pending",
                RequestDate = DateTime.Now
            });

        public bool CreatePayslipRequest(PayslipRequestModel request)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                using var cmd = Cmd(conn, @"
                    INSERT INTO PayslipRequests (EmployeeId, PayrollId, FullName, RequestDate, Status, Reason) 
                    VALUES (@EmployeeId, @PayrollId, @FullName, @RequestDate, @Status, @Reason)");

                cmd.Parameters.AddWithValue("@EmployeeId", request.EmployeeId);
                cmd.Parameters.AddWithValue("@PayrollId", request.PayrollId);
                cmd.Parameters.AddWithValue("@FullName", request.FullName ?? string.Empty);
                cmd.Parameters.AddWithValue("@RequestDate", request.RequestDate == default ? DateTime.Now : request.RequestDate);
                cmd.Parameters.AddWithValue("@Status", string.IsNullOrWhiteSpace(request.Status) ? "Pending" : request.Status);
                cmd.Parameters.AddWithValue("@Reason", (object?)request.Reason ?? DBNull.Value);

                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating payslip request: {ex.Message}");
            }
        }

        public bool UpdateRequestStatus(int id, string newStatus)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                using var cmd = Cmd(conn, @"
                    UPDATE PayslipRequests 
                    SET Status = @Status,
                        UpdatedDate = NOW()
                    WHERE Id = @Id");
                cmd.Parameters.AddWithValue("@Status", newStatus);
                cmd.Parameters.AddWithValue("@Id", id);

                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating request status: {ex.Message}");
            }
        }

        // Approve (optionally fulfill now)
        public bool ApprovePayslipRequest(int id, bool fulfillNow = true)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                var req = GetPayslipRequestByIdInternal(conn, tx, id);
                if (req == null) throw new Exception("Request not found.");

                using (var upd = Cmd(conn, @"
                    UPDATE PayslipRequests
                    SET Status = 'Approved',
                        UpdatedDate = NOW()
                    WHERE Id = @Id", tx))
                {
                    upd.Parameters.AddWithValue("@Id", id);
                    upd.ExecuteNonQuery();
                }

                if (fulfillNow && req.PayrollId > 0)
                {
                    _ = CreatePayslipFromPayrollIdInternal(conn, tx, req.PayrollId);

                    using var done = Cmd(conn, @"
                        UPDATE PayslipRequests
                        SET Status = 'Completed',
                            UpdatedDate = NOW()
                        WHERE Id = @Id", tx);
                    done.Parameters.AddWithValue("@Id", id);
                    done.ExecuteNonQuery();
                }

                tx.Commit();
                return true;
            }
            catch (Exception ex)
            {
                tx.Rollback();
                throw new Exception($"Error approving payslip request: {ex.Message}");
            }
        }

        public bool RejectPayslipRequest(int id, string? reason = null)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                using var cmd = Cmd(conn, @"
                    UPDATE PayslipRequests 
                    SET Status = 'Rejected',
                        Reason = COALESCE(@Reason, Reason),
                        UpdatedDate = NOW()
                    WHERE Id = @Id");
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@Reason", (object?)reason ?? DBNull.Value);

                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error rejecting payslip request: {ex.Message}");
            }
        }

        public bool UpdatePayslipRequest(PayslipRequestModel request)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                using var cmd = Cmd(conn, @"
                    UPDATE PayslipRequests 
                    SET EmployeeId = @EmployeeId, 
                        PayrollId = @PayrollId,
                        FullName = @FullName, 
                        RequestDate = @RequestDate, 
                        Status = @Status,
                        Reason = @Reason,
                        UpdatedDate = NOW()
                    WHERE Id = @Id");
                cmd.Parameters.AddWithValue("@Id", request.Id);
                cmd.Parameters.AddWithValue("@EmployeeId", request.EmployeeId);
                cmd.Parameters.AddWithValue("@PayrollId", request.PayrollId);
                cmd.Parameters.AddWithValue("@FullName", request.FullName ?? string.Empty);
                cmd.Parameters.AddWithValue("@RequestDate", request.RequestDate == default ? DateTime.Now : request.RequestDate);
                cmd.Parameters.AddWithValue("@Status", string.IsNullOrWhiteSpace(request.Status) ? "Pending" : request.Status);
                cmd.Parameters.AddWithValue("@Reason", (object?)request.Reason ?? DBNull.Value);

                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating payslip request: {ex.Message}");
            }
        }

        public bool DeletePayslipRequest(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                using var cmd = Cmd(conn, "DELETE FROM PayslipRequests WHERE Id = @Id");
                cmd.Parameters.AddWithValue("@Id", id);

                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deleting payslip request: {ex.Message}");
            }
        }

        public bool RequestExists(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                using var cmd = Cmd(conn, "SELECT COUNT(*) FROM PayslipRequests WHERE Id = @Id");
                cmd.Parameters.AddWithValue("@Id", id);

                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error checking request existence: {ex.Message}");
            }
        }

        public List<PayslipRequestModel> GetRequestsByStatus(string status)
        {
            var list = new List<PayslipRequestModel>();
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                using var cmd = Cmd(conn, @"
                    SELECT Id, EmployeeId, PayrollId, FullName, RequestDate, Status, Reason, UpdatedDate 
                    FROM PayslipRequests 
                    WHERE Status = @Status 
                    ORDER BY RequestDate DESC");
                cmd.Parameters.AddWithValue("@Status", status);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new PayslipRequestModel
                    {
                        Id = Get<int>(reader, "Id"),
                        EmployeeId = Get<int>(reader, "EmployeeId"),
                        PayrollId = reader.IsDBNull(reader.GetOrdinal("PayrollId")) ? 0 : reader.GetInt32(reader.GetOrdinal("PayrollId")),
                        FullName = Get<string>(reader, "FullName"),
                        RequestDate = Get<DateTime>(reader, "RequestDate"),
                        Status = Get<string>(reader, "Status"),
                        Reason = GetNullableString(reader, "Reason"),
                        UpdatedDate = GetNullable<DateTime>(reader, "UpdatedDate")
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving requests by status: {ex.Message}");
            }
            return list;
        }

        public List<PayslipRequestModel> GetRequestsByEmployee(int employeeId)
        {
            var list = new List<PayslipRequestModel>();
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                using var cmd = Cmd(conn, @"
                    SELECT Id, EmployeeId, PayrollId, FullName, RequestDate, Status, Reason, UpdatedDate
                    FROM PayslipRequests
                    WHERE EmployeeId = @EmployeeId
                    ORDER BY RequestDate DESC");
                cmd.Parameters.AddWithValue("@EmployeeId", employeeId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new PayslipRequestModel
                    {
                        Id = Get<int>(reader, "Id"),
                        EmployeeId = Get<int>(reader, "EmployeeId"),
                        PayrollId = reader.IsDBNull(reader.GetOrdinal("PayrollId")) ? 0 : reader.GetInt32(reader.GetOrdinal("PayrollId")),
                        FullName = Get<string>(reader, "FullName"),
                        RequestDate = Get<DateTime>(reader, "RequestDate"),
                        Status = Get<string>(reader, "Status"),
                        Reason = GetNullableString(reader, "Reason"),
                        UpdatedDate = GetNullable<DateTime>(reader, "UpdatedDate")
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving employee requests: {ex.Message}");
            }
            return list;
        }

        public bool FulfillPayslipRequest(int requestId)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                var req = GetPayslipRequestByIdInternal(conn, tx, requestId);
                if (req == null) throw new Exception("Request not found.");
                if (req.PayrollId <= 0) throw new Exception("Request has no PayrollId to fulfill.");

                _ = CreatePayslipFromPayrollIdInternal(conn, tx, req.PayrollId);

                using var cmd = Cmd(conn, @"
                    UPDATE PayslipRequests
                    SET Status = 'Completed',
                        UpdatedDate = NOW()
                    WHERE Id = @Id", tx);
                cmd.Parameters.AddWithValue("@Id", requestId);
                cmd.ExecuteNonQuery();

                tx.Commit();
                return true;
            }
            catch (Exception ex)
            {
                tx.Rollback();
                throw new Exception($"Error fulfilling payslip request: {ex.Message}");
            }
        }

        #region ===== Internal shared ops =====

        private PayslipRequestModel? GetPayslipRequestByIdInternal(MySqlConnection conn, MySqlTransaction? tx, int id)
        {
            using var cmd = Cmd(conn, @"
                SELECT Id, EmployeeId, PayrollId, FullName, RequestDate, Status, Reason, UpdatedDate 
                FROM PayslipRequests 
                WHERE Id = @Id", tx);
            cmd.Parameters.AddWithValue("@Id", id);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var item = new PayslipRequestModel
                {
                    Id = Get<int>(reader, "Id"),
                    EmployeeId = Get<int>(reader, "EmployeeId"),
                    PayrollId = reader.IsDBNull(reader.GetOrdinal("PayrollId")) ? 0 : reader.GetInt32(reader.GetOrdinal("PayrollId")),
                    FullName = Get<string>(reader, "FullName"),
                    RequestDate = Get<DateTime>(reader, "RequestDate"),
                    Status = Get<string>(reader, "Status"),
                    Reason = GetNullableString(reader, "Reason"),
                    UpdatedDate = GetNullable<DateTime>(reader, "UpdatedDate")
                };
                return item;
            }
            return null;
        }

        private int CreatePayslipFromPayrollIdInternal(MySqlConnection conn, MySqlTransaction? tx, int payrollId)
        {
            const string selectOne = @"
                SELECT employee_id, start_date, end_date,
                       sss_deduction, philhealth_deduction, pagibig_deduction, other_deductions,
                       net_salary
                FROM payroll
                WHERE id = @pid
                LIMIT 1;";

            const string existsPayslipSql = @"
                SELECT COUNT(*) FROM Payslips
                WHERE EmployeeId = @empId AND PayDate = @payDate;";

            const string insertPayslipSql = @"
                INSERT INTO Payslips
                    (EmployeeId, PayDate, HoursWorked, RatePerHour, Deductions, NetSalary, UpdatedDate)
                VALUES
                    (@empId, @payDate, 0, 0, @deductions, @netSalary, NOW());";

            int empId;
            DateTime endDate;
            decimal deductions = 0m;
            decimal net = 0m;

            using (var cmd = Cmd(conn, selectOne, tx))
            {
                cmd.Parameters.AddWithValue("@pid", payrollId);
                using var rdr = cmd.ExecuteReader();
                if (!rdr.Read()) throw new Exception($"Payroll row not found: {payrollId}");

                empId = Get<int>(rdr, "employee_id");
                endDate = Get<DateTime>(rdr, "end_date");
                decimal sss = rdr.IsDBNull(rdr.GetOrdinal("sss_deduction")) ? 0m : rdr.GetDecimal(rdr.GetOrdinal("sss_deduction"));
                decimal ph = rdr.IsDBNull(rdr.GetOrdinal("philhealth_deduction")) ? 0m : rdr.GetDecimal(rdr.GetOrdinal("philhealth_deduction"));
                decimal pi = rdr.IsDBNull(rdr.GetOrdinal("pagibig_deduction")) ? 0m : rdr.GetDecimal(rdr.GetOrdinal("pagibig_deduction"));
                decimal oth = rdr.IsDBNull(rdr.GetOrdinal("other_deductions")) ? 0m : rdr.GetDecimal(rdr.GetOrdinal("other_deductions"));
                net = rdr.IsDBNull(rdr.GetOrdinal("net_salary")) ? 0m : rdr.GetDecimal(rdr.GetOrdinal("net_salary"));
                deductions = sss + ph + pi + oth;
            }

            using (var existsCmd = Cmd(conn, existsPayslipSql, tx))
            {
                existsCmd.Parameters.AddWithValue("@empId", empId);
                existsCmd.Parameters.AddWithValue("@payDate", endDate.Date);
                var exists = Convert.ToInt32(existsCmd.ExecuteScalar()) > 0;
                if (exists) return 0;
            }

            using (var ins = Cmd(conn, insertPayslipSql, tx))
            {
                ins.Parameters.AddWithValue("@empId", empId);
                ins.Parameters.AddWithValue("@payDate", endDate.Date);
                ins.Parameters.AddWithValue("@deductions", deductions);
                ins.Parameters.AddWithValue("@netSalary", net);
                return ins.ExecuteNonQuery();
            }
        }

        #endregion
    }
}
