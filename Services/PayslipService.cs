#nullable enable
using Microsoft.Data.Sqlite;
using SihyuPOSPayroll.Data;
using SihyuPOSPayroll.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SihyuPOSPayroll.Services
{
    /// <summary>
    /// Handles Payslips and PayslipRequests — fully SQLite-backed.
    /// Previously used MySQL; migrated to SQLite to match the rest of the app.
    /// </summary>
    public class PayslipService
    {
        // EnsureSchemaAtStartup is kept for compatibility with App.xaml.cs.
        // The actual DDL now lives in AuthSchemaInitializer (HRTables).
        private static bool _schemaChecked = false;
        public static void EnsureSchemaAtStartup()
        {
            if (_schemaChecked) return;
            // Tables are created by AuthSchemaInitializer.EnsureSchemaAtStartup().
            // Nothing extra to do here — just mark as checked so App.xaml.cs is happy.
            _schemaChecked = true;
            Debug.WriteLine("[PayslipService] Schema delegated to AuthSchemaInitializer.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static SqliteCommand Cmd(SqliteConnection conn, string sql, SqliteTransaction? tx = null)
        {
            var c = conn.CreateCommand();
            c.CommandText = sql;
            if (tx != null) c.Transaction = tx;
            return c;
        }

        private static string? GetStr(SqliteDataReader r, string col)
        {
            int o = r.GetOrdinal(col);
            return r.IsDBNull(o) ? null : r.GetString(o);
        }

        private static T? GetNullable<T>(SqliteDataReader r, string col) where T : struct
        {
            int o = r.GetOrdinal(col);
            return r.IsDBNull(o) ? (T?)null : (T)Convert.ChangeType(r.GetValue(o), typeof(T));
        }

        private static DateTime ParseDate(SqliteDataReader r, string col)
        {
            int o = r.GetOrdinal(col);
            if (r.IsDBNull(o)) return DateTime.MinValue;
            return DateTime.TryParse(r.GetString(o), out var d) ? d : DateTime.MinValue;
        }

        // ── Payslips ──────────────────────────────────────────────────────────

        public int CreatePayslipsFromPayrollPeriod(DateTime periodStart, DateTime periodEnd)
        {
            int created = 0;
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var tx   = conn.BeginTransaction();

                using var selectCmd = Cmd(conn, @"
                    SELECT employee_id, total_days_worked, gross_salary,
                           sss_deduction, philhealth_deduction, pagibig_deduction,
                           other_deductions, net_salary
                    FROM   payroll
                    WHERE  start_date = @start AND end_date = @end;", tx);
                selectCmd.Parameters.AddWithValue("@start", periodStart.Date.ToString("yyyy-MM-dd HH:mm:ss"));
                selectCmd.Parameters.AddWithValue("@end",   periodEnd.Date.ToString("yyyy-MM-dd HH:mm:ss"));

                var rows = new List<(int EmpId, decimal Hours, decimal Rate, decimal Deductions, decimal Net)>();
                using (var r = selectCmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        int     empId      = r.GetInt32(r.GetOrdinal("employee_id"));
                        int     days       = r.IsDBNull(r.GetOrdinal("total_days_worked")) ? 0 : r.GetInt32(r.GetOrdinal("total_days_worked"));
                        decimal gross      = r.IsDBNull(r.GetOrdinal("gross_salary"))      ? 0m : Convert.ToDecimal(r.GetValue(r.GetOrdinal("gross_salary")));
                        decimal sss        = r.IsDBNull(r.GetOrdinal("sss_deduction"))        ? 0m : Convert.ToDecimal(r.GetValue(r.GetOrdinal("sss_deduction")));
                        decimal ph         = r.IsDBNull(r.GetOrdinal("philhealth_deduction"))  ? 0m : Convert.ToDecimal(r.GetValue(r.GetOrdinal("philhealth_deduction")));
                        decimal pi         = r.IsDBNull(r.GetOrdinal("pagibig_deduction"))     ? 0m : Convert.ToDecimal(r.GetValue(r.GetOrdinal("pagibig_deduction")));
                        decimal oth        = r.IsDBNull(r.GetOrdinal("other_deductions"))      ? 0m : Convert.ToDecimal(r.GetValue(r.GetOrdinal("other_deductions")));
                        decimal net        = r.IsDBNull(r.GetOrdinal("net_salary"))            ? 0m : Convert.ToDecimal(r.GetValue(r.GetOrdinal("net_salary")));
                        decimal hours      = days * 8m;
                        decimal rate       = hours > 0 ? Math.Round(gross / hours, 4) : 0m;
                        rows.Add((empId, hours, rate, sss + ph + pi + oth, net));
                    }
                }

                foreach (var row in rows)
                {
                    // Skip if payslip already exists for this employee/pay-date
                    using var existsCmd = Cmd(conn, @"
                        SELECT COUNT(*) FROM Payslips
                        WHERE  EmployeeId = @empId AND PayDate = @payDate;", tx);
                    existsCmd.Parameters.AddWithValue("@empId",   row.EmpId);
                    existsCmd.Parameters.AddWithValue("@payDate", periodEnd.Date.ToString("yyyy-MM-dd"));
                    if (Convert.ToInt32(existsCmd.ExecuteScalar()) > 0) continue;

                    using var insCmd = Cmd(conn, @"
                        INSERT INTO Payslips
                            (EmployeeId, PayDate, HoursWorked, RatePerHour, Deductions, NetSalary, UpdatedDate)
                        VALUES
                            (@empId, @payDate, @hours, @rate, @deductions, @net, datetime('now'));", tx);
                    insCmd.Parameters.AddWithValue("@empId",      row.EmpId);
                    insCmd.Parameters.AddWithValue("@payDate",    periodEnd.Date.ToString("yyyy-MM-dd"));
                    insCmd.Parameters.AddWithValue("@hours",      (double)row.Hours);
                    insCmd.Parameters.AddWithValue("@rate",       (double)row.Rate);
                    insCmd.Parameters.AddWithValue("@deductions", (double)row.Deductions);
                    insCmd.Parameters.AddWithValue("@net",        (double)row.Net);
                    if (insCmd.ExecuteNonQuery() > 0) created++;
                }

                tx.Commit();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[PayslipService] CreatePayslipsFromPayrollPeriod: {ex.Message}");
            }
            return created;
        }

        public int CreatePayslipFromPayrollId(int payrollId)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var tx   = conn.BeginTransaction();
                return CreatePayslipFromPayrollIdInternal(conn, tx, payrollId);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating payslip from payroll: {ex.Message}", ex);
            }
        }

        public List<PayslipModel> GetEmployeePayslips(int employeeId)
        {
            var list = new List<PayslipModel>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = Cmd(conn, @"
                    SELECT PayslipId, EmployeeId, PayDate, HoursWorked, RatePerHour,
                           Deductions, NetSalary, UpdatedDate
                    FROM   Payslips
                    WHERE  EmployeeId = @eid
                    ORDER  BY PayDate DESC;");
                cmd.Parameters.AddWithValue("@eid", employeeId);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new PayslipModel
                    {
                        PayslipId   = r.GetInt32(r.GetOrdinal("PayslipId")),
                        EmployeeId  = r.GetInt32(r.GetOrdinal("EmployeeId")),
                        FullName    = string.Empty,
                        PayDate     = ParseDate(r, "PayDate"),
                        HoursWorked = r.IsDBNull(r.GetOrdinal("HoursWorked")) ? 0m : Convert.ToDecimal(r.GetValue(r.GetOrdinal("HoursWorked"))),
                        RatePerHour = r.IsDBNull(r.GetOrdinal("RatePerHour")) ? 0m : Convert.ToDecimal(r.GetValue(r.GetOrdinal("RatePerHour"))),
                        Deductions  = r.IsDBNull(r.GetOrdinal("Deductions"))  ? 0m : Convert.ToDecimal(r.GetValue(r.GetOrdinal("Deductions"))),
                        NetSalary   = r.IsDBNull(r.GetOrdinal("NetSalary"))   ? 0m : Convert.ToDecimal(r.GetValue(r.GetOrdinal("NetSalary"))),
                        UpdatedDate = GetNullable<DateTime>(r, "UpdatedDate"),
                        Status      = "Pending"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[PayslipService] GetEmployeePayslips: {ex.Message}");
            }
            return list;
        }

        // ── Payslip Requests ──────────────────────────────────────────────────

        public List<PayslipRequestModel> GetAllPayslipRequests()
        {
            var list = new List<PayslipRequestModel>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = Cmd(conn, @"
                    SELECT Id, EmployeeId, PayrollId, FullName, RequestDate, Status, Reason, UpdatedDate
                    FROM   PayslipRequests
                    ORDER  BY RequestDate DESC;");
                using var r = cmd.ExecuteReader();
                while (r.Read()) list.Add(MapRequest(r));
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving payslip requests: {ex.Message}", ex);
            }
            return list;
        }

        public PayslipRequestModel? GetPayslipRequestById(int id)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = Cmd(conn, @"
                    SELECT Id, EmployeeId, PayrollId, FullName, RequestDate, Status, Reason, UpdatedDate
                    FROM   PayslipRequests WHERE Id = @id;");
                cmd.Parameters.AddWithValue("@id", id);
                using var r = cmd.ExecuteReader();
                return r.Read() ? MapRequest(r) : null;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[PayslipService] GetPayslipRequestById: {ex.Message}");
                return null;
            }
        }

        public bool CreatePayslipRequest(int employeeId, int payrollId, string fullName, string? reason = null)
            => CreatePayslipRequest(new PayslipRequestModel
            {
                EmployeeId  = employeeId,
                PayrollId   = payrollId,
                FullName    = fullName,
                Reason      = reason,
                Status      = "Pending",
                RequestDate = DateTime.Now
            });

        public bool CreatePayslipRequest(PayslipRequestModel request)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = Cmd(conn, @"
                    INSERT INTO PayslipRequests
                        (EmployeeId, PayrollId, FullName, RequestDate, Status, Reason)
                    VALUES
                        (@empId, @payrollId, @fullName, @requestDate, @status, @reason);");
                cmd.Parameters.AddWithValue("@empId",       request.EmployeeId);
                cmd.Parameters.AddWithValue("@payrollId",   request.PayrollId > 0 ? (object)request.PayrollId : DBNull.Value);
                cmd.Parameters.AddWithValue("@fullName",    request.FullName ?? string.Empty);
                cmd.Parameters.AddWithValue("@requestDate", (request.RequestDate == default ? DateTime.Now : request.RequestDate).ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@status",      string.IsNullOrWhiteSpace(request.Status) ? "Pending" : request.Status);
                cmd.Parameters.AddWithValue("@reason",      (object?)request.Reason ?? DBNull.Value);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating payslip request: {ex.Message}", ex);
            }
        }

        public bool UpdateRequestStatus(int id, string newStatus)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = Cmd(conn, @"
                    UPDATE PayslipRequests
                    SET    Status      = @status,
                           UpdatedDate = datetime('now')
                    WHERE  Id = @id;");
                cmd.Parameters.AddWithValue("@status", newStatus);
                cmd.Parameters.AddWithValue("@id",     id);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating request status: {ex.Message}", ex);
            }
        }

        public bool ApprovePayslipRequest(int id, bool fulfillNow = true)
        {
            using var conn = SqliteConnectionFactory.CreateOpenConnection();
            using var tx   = conn.BeginTransaction();
            try
            {
                var req = GetPayslipRequestByIdInternal(conn, tx, id);
                if (req == null) throw new Exception("Request not found.");

                using (var upd = Cmd(conn, @"
                    UPDATE PayslipRequests SET Status = 'Approved', UpdatedDate = datetime('now')
                    WHERE Id = @id;", tx))
                {
                    upd.Parameters.AddWithValue("@id", id);
                    upd.ExecuteNonQuery();
                }

                if (fulfillNow && req.PayrollId > 0)
                {
                    CreatePayslipFromPayrollIdInternal(conn, tx, req.PayrollId);

                    using var done = Cmd(conn, @"
                        UPDATE PayslipRequests SET Status = 'Completed', UpdatedDate = datetime('now')
                        WHERE Id = @id;", tx);
                    done.Parameters.AddWithValue("@id", id);
                    done.ExecuteNonQuery();
                }

                tx.Commit();
                return true;
            }
            catch (Exception ex)
            {
                tx.Rollback();
                throw new Exception($"Error approving payslip request: {ex.Message}", ex);
            }
        }

        public bool RejectPayslipRequest(int id, string? reason = null)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = Cmd(conn, @"
                    UPDATE PayslipRequests
                    SET    Status      = 'Rejected',
                           Reason      = COALESCE(@reason, Reason),
                           UpdatedDate = datetime('now')
                    WHERE  Id = @id;");
                cmd.Parameters.AddWithValue("@id",     id);
                cmd.Parameters.AddWithValue("@reason", (object?)reason ?? DBNull.Value);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error rejecting payslip request: {ex.Message}", ex);
            }
        }

        public bool UpdatePayslipRequest(PayslipRequestModel request)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = Cmd(conn, @"
                    UPDATE PayslipRequests SET
                        EmployeeId  = @empId,
                        PayrollId   = @payrollId,
                        FullName    = @fullName,
                        RequestDate = @requestDate,
                        Status      = @status,
                        Reason      = @reason,
                        UpdatedDate = datetime('now')
                    WHERE Id = @id;");
                cmd.Parameters.AddWithValue("@id",          request.Id);
                cmd.Parameters.AddWithValue("@empId",       request.EmployeeId);
                cmd.Parameters.AddWithValue("@payrollId",   request.PayrollId > 0 ? (object)request.PayrollId : DBNull.Value);
                cmd.Parameters.AddWithValue("@fullName",    request.FullName ?? string.Empty);
                cmd.Parameters.AddWithValue("@requestDate", (request.RequestDate == default ? DateTime.Now : request.RequestDate).ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@status",      string.IsNullOrWhiteSpace(request.Status) ? "Pending" : request.Status);
                cmd.Parameters.AddWithValue("@reason",      (object?)request.Reason ?? DBNull.Value);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating payslip request: {ex.Message}", ex);
            }
        }

        public bool DeletePayslipRequest(int id)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = Cmd(conn, "DELETE FROM PayslipRequests WHERE Id = @id;");
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deleting payslip request: {ex.Message}", ex);
            }
        }

        public bool RequestExists(int id)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = Cmd(conn, "SELECT COUNT(*) FROM PayslipRequests WHERE Id = @id;");
                cmd.Parameters.AddWithValue("@id", id);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[PayslipService] RequestExists: {ex.Message}");
                return false;
            }
        }

        public List<PayslipRequestModel> GetRequestsByStatus(string status)
        {
            var list = new List<PayslipRequestModel>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = Cmd(conn, @"
                    SELECT Id, EmployeeId, PayrollId, FullName, RequestDate, Status, Reason, UpdatedDate
                    FROM   PayslipRequests
                    WHERE  Status = @status
                    ORDER  BY RequestDate DESC;");
                cmd.Parameters.AddWithValue("@status", status);
                using var r = cmd.ExecuteReader();
                while (r.Read()) list.Add(MapRequest(r));
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving requests by status: {ex.Message}", ex);
            }
            return list;
        }

        public List<PayslipRequestModel> GetRequestsByEmployee(int employeeId)
        {
            var list = new List<PayslipRequestModel>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = Cmd(conn, @"
                    SELECT Id, EmployeeId, PayrollId, FullName, RequestDate, Status, Reason, UpdatedDate
                    FROM   PayslipRequests
                    WHERE  EmployeeId = @eid
                    ORDER  BY RequestDate DESC;");
                cmd.Parameters.AddWithValue("@eid", employeeId);
                using var r = cmd.ExecuteReader();
                while (r.Read()) list.Add(MapRequest(r));
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving employee requests: {ex.Message}", ex);
            }
            return list;
        }

        public bool FulfillPayslipRequest(int requestId)
        {
            using var conn = SqliteConnectionFactory.CreateOpenConnection();
            using var tx   = conn.BeginTransaction();
            try
            {
                var req = GetPayslipRequestByIdInternal(conn, tx, requestId);
                if (req == null) throw new Exception("Request not found.");
                if (req.PayrollId <= 0) throw new Exception("Request has no PayrollId to fulfill.");

                CreatePayslipFromPayrollIdInternal(conn, tx, req.PayrollId);

                using var cmd = Cmd(conn, @"
                    UPDATE PayslipRequests SET Status = 'Completed', UpdatedDate = datetime('now')
                    WHERE Id = @id;", tx);
                cmd.Parameters.AddWithValue("@id", requestId);
                cmd.ExecuteNonQuery();

                tx.Commit();
                return true;
            }
            catch (Exception ex)
            {
                tx.Rollback();
                throw new Exception($"Error fulfilling payslip request: {ex.Message}", ex);
            }
        }

        // ── Internal shared helpers ───────────────────────────────────────────

        private static PayslipRequestModel? GetPayslipRequestByIdInternal(SqliteConnection conn, SqliteTransaction? tx, int id)
        {
            using var cmd = Cmd(conn, @"
                SELECT Id, EmployeeId, PayrollId, FullName, RequestDate, Status, Reason, UpdatedDate
                FROM   PayslipRequests WHERE Id = @id;", tx);
            cmd.Parameters.AddWithValue("@id", id);
            using var r = cmd.ExecuteReader();
            return r.Read() ? MapRequest(r) : null;
        }

        private static int CreatePayslipFromPayrollIdInternal(SqliteConnection conn, SqliteTransaction? tx, int payrollId)
        {
            int empId;
            DateTime endDate;
            decimal deductions = 0m, net = 0m;

            using (var sel = Cmd(conn, @"
                SELECT employee_id, end_date,
                       sss_deduction, philhealth_deduction, pagibig_deduction,
                       other_deductions, net_salary
                FROM   payroll WHERE id = @pid LIMIT 1;", tx))
            {
                sel.Parameters.AddWithValue("@pid", payrollId);
                using var r = sel.ExecuteReader();
                if (!r.Read()) throw new Exception($"Payroll row not found: {payrollId}");

                empId   = r.GetInt32(r.GetOrdinal("employee_id"));
                endDate = DateTime.TryParse(r.GetString(r.GetOrdinal("end_date")), out var d) ? d : DateTime.Now;
                decimal sss = r.IsDBNull(r.GetOrdinal("sss_deduction"))        ? 0m : Convert.ToDecimal(r.GetValue(r.GetOrdinal("sss_deduction")));
                decimal ph  = r.IsDBNull(r.GetOrdinal("philhealth_deduction"))  ? 0m : Convert.ToDecimal(r.GetValue(r.GetOrdinal("philhealth_deduction")));
                decimal pi  = r.IsDBNull(r.GetOrdinal("pagibig_deduction"))     ? 0m : Convert.ToDecimal(r.GetValue(r.GetOrdinal("pagibig_deduction")));
                decimal oth = r.IsDBNull(r.GetOrdinal("other_deductions"))      ? 0m : Convert.ToDecimal(r.GetValue(r.GetOrdinal("other_deductions")));
                net         = r.IsDBNull(r.GetOrdinal("net_salary"))            ? 0m : Convert.ToDecimal(r.GetValue(r.GetOrdinal("net_salary")));
                deductions  = sss + ph + pi + oth;
            }

            using var exists = Cmd(conn, "SELECT COUNT(*) FROM Payslips WHERE EmployeeId=@e AND PayDate=@d;", tx);
            exists.Parameters.AddWithValue("@e", empId);
            exists.Parameters.AddWithValue("@d", endDate.Date.ToString("yyyy-MM-dd"));
            if (Convert.ToInt32(exists.ExecuteScalar()) > 0) return 0;

            using var ins = Cmd(conn, @"
                INSERT INTO Payslips (EmployeeId, PayDate, HoursWorked, RatePerHour, Deductions, NetSalary, UpdatedDate)
                VALUES (@e, @d, 0, 0, @ded, @net, datetime('now'));", tx);
            ins.Parameters.AddWithValue("@e",   empId);
            ins.Parameters.AddWithValue("@d",   endDate.Date.ToString("yyyy-MM-dd"));
            ins.Parameters.AddWithValue("@ded", (double)deductions);
            ins.Parameters.AddWithValue("@net", (double)net);
            return ins.ExecuteNonQuery();
        }

        private static PayslipRequestModel MapRequest(SqliteDataReader r) => new PayslipRequestModel
        {
            Id          = r.GetInt32(r.GetOrdinal("Id")),
            EmployeeId  = r.GetInt32(r.GetOrdinal("EmployeeId")),
            PayrollId   = r.IsDBNull(r.GetOrdinal("PayrollId"))   ? 0      : r.GetInt32(r.GetOrdinal("PayrollId")),
            FullName    = r.IsDBNull(r.GetOrdinal("FullName"))     ? string.Empty : r.GetString(r.GetOrdinal("FullName")),
            RequestDate = ParseDate(r, "RequestDate"),
            Status      = r.IsDBNull(r.GetOrdinal("Status"))       ? "Pending" : r.GetString(r.GetOrdinal("Status")),
            Reason      = GetStr(r, "Reason"),
            UpdatedDate = GetNullable<DateTime>(r, "UpdatedDate"),
        };
    }
}
