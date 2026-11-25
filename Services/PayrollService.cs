// Services/PayrollService.cs
// - Uses AttendanceService.GetWorkedDaysCount(...) as the single source of truth
// - GenerateForPeriod(...) relies on BOTH time_in AND time_out to count a worked day
// - GrossSalary = ratePerDay * TotalDaysWorked

#nullable enable
using Microsoft.Data.Sqlite;
using SihyuPOSPayroll.Data;
using SihyuPOSPayroll.Models;
using System;
using System.Collections.Generic;

namespace SihyuPOSPayroll.Services
{
    public class PayrollService
    {
        private readonly AttendanceService _attendanceService;
        public string? LastError { get; private set; }

        // Constructor kept for interface compatibility; connection string is ignored —
        // all data now comes from SQLite via SqliteConnectionFactory.
        public PayrollService(string? connectionString = null)
        {
            _attendanceService = new AttendanceService();
        }

        // ── Period helpers ────────────────────────────────────────────────────

        public int CountWorkedDays(int employeeId, DateTime start, DateTime end)
        {
            try
            {
                return _attendanceService.GetWorkedDaysCount(employeeId, start, end);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.Error.WriteLine("Error counting worked days via AttendanceService: " + ex);
                return 0;
            }
        }

        public (DateTime start, DateTime end) GetFirstHalfRange(int year, int month)
            => (new DateTime(year, month, 1,  0,  0,  0),
                new DateTime(year, month, 15, 23, 59, 59));

        public (DateTime start, DateTime end) GetSecondHalfRange(int year, int month)
            => (new DateTime(year, month, 16, 0,  0,  0),
                new DateTime(year, month, DateTime.DaysInMonth(year, month), 23, 59, 59));

        // ── Generation ────────────────────────────────────────────────────────

        public List<PayrollModel> GenerateForPeriod(DateTime periodStart, DateTime periodEnd)
        {
            LastError = null;
            var results = new List<PayrollModel>();

            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();

                // Pull employees (include position and salary_per_day if present)
                var employees = new List<(int Id, string Name, string? Position, decimal? SalaryPerDay)>();
                using (var empCmd = conn.CreateCommand())
                {
                    empCmd.CommandText = @"
                        SELECT id, full_name, position, salary_per_day
                        FROM   employees;";
                    using var r = empCmd.ExecuteReader();
                    while (r.Read())
                    {
                        int     id   = r.GetInt32(r.GetOrdinal("id"));
                        string  name = r.IsDBNull(r.GetOrdinal("full_name"))  ? "" : r.GetString(r.GetOrdinal("full_name"));
                        string? pos  = r.IsDBNull(r.GetOrdinal("position"))   ? null : r.GetString(r.GetOrdinal("position"));
                        decimal? sal = r.IsDBNull(r.GetOrdinal("salary_per_day"))
                                           ? (decimal?)null
                                           : Convert.ToDecimal(r.GetValue(r.GetOrdinal("salary_per_day")));
                        employees.Add((id, name, pos, sal));
                    }
                }

                var posService = new PositionSalaryService();

                foreach (var emp in employees)
                {
                    int daysWorked = _attendanceService.GetWorkedDaysCount(emp.Id, periodStart, periodEnd);

                    decimal ratePerDay = 0m;
                    if (!string.IsNullOrWhiteSpace(emp.Position) &&
                        posService.TryGetRate(emp.Position, out var rateFromPosition) &&
                        rateFromPosition > 0)
                    {
                        ratePerDay = rateFromPosition;
                    }
                    else if (emp.SalaryPerDay.HasValue && emp.SalaryPerDay.Value > 0)
                    {
                        ratePerDay = Math.Round(emp.SalaryPerDay.Value, 2, MidpointRounding.AwayFromZero);
                    }
                    else
                    {
                        ratePerDay = GetLastPerDayRate(conn, emp.Id);
                    }

                    decimal gross          = Math.Round(ratePerDay * daysWorked, 2);
                    decimal sss            = 0m;
                    decimal philhealth     = 0m;
                    decimal pagibig        = 0m;
                    decimal bonus          = 0m;
                    decimal otherDeductions = 0m;
                    decimal net            = Math.Max(0m, gross - (sss + philhealth + pagibig + otherDeductions) + bonus);

                    results.Add(new PayrollModel
                    {
                        EmployeeId          = emp.Id,
                        EmployeeFullName    = emp.Name,
                        StartDate           = periodStart.Date,
                        EndDate             = periodEnd.Date,
                        TotalDaysWorked     = daysWorked,
                        GrossSalary         = gross,
                        SssDeduction        = Math.Round(sss,            2),
                        PhilhealthDeduction = Math.Round(philhealth,     2),
                        PagibigDeduction    = Math.Round(pagibig,        2),
                        OtherDeductions     = Math.Round(otherDeductions, 2),
                        Bonus               = Math.Round(bonus,          2),
                        NetSalary           = Math.Round(net,            2),
                        BranchName          = string.Empty,
                        ShiftType           = string.Empty
                    });
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.Error.WriteLine("Error generating payrolls: " + ex);
            }

            return results;
        }

        // ── Batch save ────────────────────────────────────────────────────────

        public bool SaveGeneratedPayrolls(IEnumerable<PayrollModel> payrolls)
        {
            LastError = null;
            if (payrolls == null) { LastError = "No payrolls to save."; return false; }

            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var tx   = conn.BeginTransaction();

                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO payroll (
                        employee_id, start_date, end_date, total_days_worked, gross_salary,
                        sss_deduction, philhealth_deduction, pagibig_deduction, other_deductions, bonus,
                        net_salary, branch_name, shift_type
                    ) VALUES (
                        @EmployeeId, @StartDate, @EndDate, @TotalDaysWorked, @GrossSalary,
                        @SssDeduction, @PhilhealthDeduction, @PagibigDeduction, @OtherDeductions, @Bonus,
                        @NetSalary, @BranchName, @ShiftType
                    );";

                // Pre-create parameters once, reuse per row
                var pEmployeeId          = cmd.Parameters.Add("@EmployeeId",          SqliteType.Integer);
                var pStartDate           = cmd.Parameters.Add("@StartDate",           SqliteType.Text);
                var pEndDate             = cmd.Parameters.Add("@EndDate",             SqliteType.Text);
                var pTotalDaysWorked     = cmd.Parameters.Add("@TotalDaysWorked",     SqliteType.Integer);
                var pGrossSalary         = cmd.Parameters.Add("@GrossSalary",         SqliteType.Real);
                var pSssDeduction        = cmd.Parameters.Add("@SssDeduction",        SqliteType.Real);
                var pPhilhealthDeduction = cmd.Parameters.Add("@PhilhealthDeduction", SqliteType.Real);
                var pPagibigDeduction    = cmd.Parameters.Add("@PagibigDeduction",    SqliteType.Real);
                var pOtherDeductions     = cmd.Parameters.Add("@OtherDeductions",     SqliteType.Real);
                var pBonus               = cmd.Parameters.Add("@Bonus",               SqliteType.Real);
                var pNetSalary           = cmd.Parameters.Add("@NetSalary",           SqliteType.Real);
                var pBranchName          = cmd.Parameters.Add("@BranchName",          SqliteType.Text);
                var pShiftType           = cmd.Parameters.Add("@ShiftType",           SqliteType.Text);

                foreach (var p in payrolls)
                {
                    pEmployeeId.Value          = p.EmployeeId;
                    pStartDate.Value           = p.StartDate.ToString("yyyy-MM-dd HH:mm:ss");
                    pEndDate.Value             = p.EndDate.ToString("yyyy-MM-dd HH:mm:ss");
                    pTotalDaysWorked.Value     = p.TotalDaysWorked;
                    pGrossSalary.Value         = (double)p.GrossSalary;
                    pSssDeduction.Value        = (double)p.SssDeduction;
                    pPhilhealthDeduction.Value = (double)p.PhilhealthDeduction;
                    pPagibigDeduction.Value    = (double)p.PagibigDeduction;
                    pOtherDeductions.Value     = (double)p.OtherDeductions;
                    pBonus.Value               = (double)p.Bonus;
                    pNetSalary.Value           = (double)p.NetSalary;
                    pBranchName.Value          = string.IsNullOrWhiteSpace(p.BranchName) ? (object)DBNull.Value : p.BranchName!;
                    pShiftType.Value           = string.IsNullOrWhiteSpace(p.ShiftType)  ? (object)DBNull.Value : p.ShiftType!;
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.Error.WriteLine("Error saving generated payrolls: " + ex);
                return false;
            }
        }

        // ── CRUD ──────────────────────────────────────────────────────────────

        public List<PayrollModel> GetAllPayrolls()
        {
            LastError = null;
            var list = new List<PayrollModel>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT p.id, p.employee_id, p.start_date, p.end_date, p.total_days_worked,
                           p.gross_salary, p.sss_deduction, p.philhealth_deduction,
                           p.pagibig_deduction, p.other_deductions, p.bonus,
                           p.net_salary, p.branch_name, p.shift_type,
                           e.full_name
                    FROM   payroll p
                    LEFT JOIN employees e ON p.employee_id = e.id
                    ORDER  BY p.id DESC;";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new PayrollModel
                    {
                        Id                  = r.GetInt32(r.GetOrdinal("id")),
                        EmployeeId          = r.GetInt32(r.GetOrdinal("employee_id")),
                        StartDate           = DateTime.Parse(r.GetString(r.GetOrdinal("start_date"))),
                        EndDate             = DateTime.Parse(r.GetString(r.GetOrdinal("end_date"))),
                        TotalDaysWorked     = r.GetInt32(r.GetOrdinal("total_days_worked")),
                        GrossSalary         = Convert.ToDecimal(r.GetValue(r.GetOrdinal("gross_salary"))),
                        SssDeduction        = Convert.ToDecimal(r.GetValue(r.GetOrdinal("sss_deduction"))),
                        PhilhealthDeduction = Convert.ToDecimal(r.GetValue(r.GetOrdinal("philhealth_deduction"))),
                        PagibigDeduction    = Convert.ToDecimal(r.GetValue(r.GetOrdinal("pagibig_deduction"))),
                        OtherDeductions     = Convert.ToDecimal(r.GetValue(r.GetOrdinal("other_deductions"))),
                        Bonus               = Convert.ToDecimal(r.GetValue(r.GetOrdinal("bonus"))),
                        NetSalary           = Convert.ToDecimal(r.GetValue(r.GetOrdinal("net_salary"))),
                        BranchName          = r.IsDBNull(r.GetOrdinal("branch_name")) ? string.Empty : r.GetString(r.GetOrdinal("branch_name")),
                        ShiftType           = r.IsDBNull(r.GetOrdinal("shift_type"))  ? string.Empty : r.GetString(r.GetOrdinal("shift_type")),
                        EmployeeFullName    = r.IsDBNull(r.GetOrdinal("full_name"))   ? ""            : r.GetString(r.GetOrdinal("full_name"))
                    });
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.Error.WriteLine("Error fetching payrolls: " + ex);
            }
            return list;
        }

        public bool DeletePayrollById(int id)
        {
            LastError = null;
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM payroll WHERE id = @Id;";
                cmd.Parameters.AddWithValue("@Id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.Error.WriteLine("Error deleting payroll: " + ex);
                return false;
            }
        }

        public bool AddPayroll(PayrollModel p) => SaveGeneratedPayrolls(new[] { p });

        public bool UpdatePayroll(PayrollModel p)
        {
            LastError = null;
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE payroll SET
                        employee_id          = @EmployeeId,
                        start_date           = @StartDate,
                        end_date             = @EndDate,
                        total_days_worked    = @TotalDaysWorked,
                        gross_salary         = @GrossSalary,
                        sss_deduction        = @SssDeduction,
                        philhealth_deduction = @PhilhealthDeduction,
                        pagibig_deduction    = @PagibigDeduction,
                        other_deductions     = @OtherDeductions,
                        bonus                = @Bonus,
                        net_salary           = @NetSalary,
                        branch_name          = @BranchName,
                        shift_type           = @ShiftType
                    WHERE id = @Id;";
                cmd.Parameters.AddWithValue("@Id",                  p.Id);
                cmd.Parameters.AddWithValue("@EmployeeId",          p.EmployeeId);
                cmd.Parameters.AddWithValue("@StartDate",           p.StartDate.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@EndDate",             p.EndDate.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@TotalDaysWorked",     p.TotalDaysWorked);
                cmd.Parameters.AddWithValue("@GrossSalary",         (double)p.GrossSalary);
                cmd.Parameters.AddWithValue("@SssDeduction",        (double)p.SssDeduction);
                cmd.Parameters.AddWithValue("@PhilhealthDeduction", (double)p.PhilhealthDeduction);
                cmd.Parameters.AddWithValue("@PagibigDeduction",    (double)p.PagibigDeduction);
                cmd.Parameters.AddWithValue("@OtherDeductions",     (double)p.OtherDeductions);
                cmd.Parameters.AddWithValue("@Bonus",               (double)p.Bonus);
                cmd.Parameters.AddWithValue("@NetSalary",           (double)p.NetSalary);
                cmd.Parameters.AddWithValue("@BranchName",          string.IsNullOrWhiteSpace(p.BranchName) ? (object)DBNull.Value : p.BranchName!);
                cmd.Parameters.AddWithValue("@ShiftType",           string.IsNullOrWhiteSpace(p.ShiftType)  ? (object)DBNull.Value : p.ShiftType!);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.Error.WriteLine("Error updating payroll: " + ex);
                return false;
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────

        // BUG FIX: replaced MySQL-specific IFNULL/NULLIF with SQLite-compatible COALESCE/CASE
        private static decimal GetLastPerDayRate(SqliteConnection conn, int empId)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT ROUND(
                    COALESCE(
                        gross_salary / CASE WHEN total_days_worked = 0 THEN NULL ELSE total_days_worked END,
                        0
                    ), 2) AS rate
                FROM payroll
                WHERE employee_id = @empId
                ORDER BY end_date DESC, id DESC
                LIMIT 1;";
            cmd.Parameters.AddWithValue("@empId", empId);
            var obj = cmd.ExecuteScalar();
            return (obj == null || obj == DBNull.Value) ? 0m : Convert.ToDecimal(obj);
        }
    }
}
