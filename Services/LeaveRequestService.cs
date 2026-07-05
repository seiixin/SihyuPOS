#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using SihyuPOSPayroll.Helpers;
using SihyuPOSPayroll.Models;
using MySql.Data.MySqlClient;

namespace SihyuPOSPayroll.Services
{
    public interface ILeaveRequestService
    {
        // Core API
        int Create(LeaveRequestModel model);
        bool Update(LeaveRequestModel model);
        bool UpdateStatus(int id, LeaveStatus status, int? approverUserId = null);
        bool Delete(int id);
        LeaveRequestModel? GetById(int id);
        List<LeaveRequestModel> GetForEmployee(int employeeId, DateTime? from = null, DateTime? to = null, LeaveStatus? status = null);
        List<LeaveRequestModel> GetAll(DateTime? from = null, DateTime? to = null, LeaveStatus? status = null);
        bool Approve(int id, int approverUserId);
        bool Reject(int id, int approverUserId);
        bool Cancel(int id);
        bool IsOnApprovedLeave(int employeeId, DateTime date);

        // Convenience: you only have a userId
        int CreateForUser(int userId, LeaveRequestModel model);
        List<LeaveRequestModel> GetForUser(int userId, DateTime? from = null, DateTime? to = null, LeaveStatus? status = null);
        bool IsOnApprovedLeaveForUser(int userId, DateTime date);
    }

    public sealed class LeaveRequestService : ILeaveRequestService
    {
        private readonly string _cs;

        public LeaveRequestService(string? connectionString = null)
        {
            _cs = string.IsNullOrWhiteSpace(connectionString)
                ? ConfigurationHelper.GetConnectionString()
                : connectionString!;
        }

        // ===== user -> employee resolver (reads users.employee_id) =====
        private int GetEmployeeIdOrThrow(int userId)
        {
            using var conn = new MySqlConnection(_cs);
            conn.Open();

            const string sql = @"
                SELECT employee_id
                FROM users
                WHERE id = @uid
                LIMIT 1;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@uid", userId);

            var res = cmd.ExecuteScalar();
            if (res == null || res == DBNull.Value)
                throw new InvalidOperationException("No employee profile linked to this user.");

            return Convert.ToInt32(res);
        }

        public int CreateForUser(int userId, LeaveRequestModel m)
        {
            m.EmployeeId = GetEmployeeIdOrThrow(userId);
            return Create(m);
        }

        public List<LeaveRequestModel> GetForUser(int userId, DateTime? from = null, DateTime? to = null, LeaveStatus? status = null)
        {
            var empId = GetEmployeeIdOrThrow(userId);
            return GetForEmployee(empId, from, to, status);
        }

        public bool IsOnApprovedLeaveForUser(int userId, DateTime date)
        {
            var empId = GetEmployeeIdOrThrow(userId);
            return IsOnApprovedLeave(empId, date);
        }

        // ==================== core ====================
        public int Create(LeaveRequestModel m)
        {
            if (m.EmployeeId <= 0)
                throw new ArgumentException("EmployeeId must be a valid employees.id", nameof(m.EmployeeId));

            using var conn = new MySqlConnection(_cs);
            conn.Open();

            const string sql = @"
INSERT INTO leave_requests
(employee_id, leave_type, reason, date_from, date_to, half_day, status, created_at)
VALUES
(@empId, @type, @reason, @from, @to, @half, @status, NOW());
SELECT LAST_INSERT_ID();";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.Add("@empId", MySqlDbType.Int32).Value = m.EmployeeId;
            cmd.Parameters.Add("@type", MySqlDbType.VarChar).Value = m.LeaveType.ToString();
            cmd.Parameters.Add("@reason", MySqlDbType.VarChar).Value = (object?)m.Reason ?? DBNull.Value;
            cmd.Parameters.Add("@from", MySqlDbType.Date).Value = m.DateFrom.Date;
            cmd.Parameters.Add("@to", MySqlDbType.Date).Value = m.DateTo.Date;
            cmd.Parameters.Add("@half", MySqlDbType.Bit).Value = m.HalfDay ? 1 : 0;
            cmd.Parameters.Add("@status", MySqlDbType.VarChar).Value = m.Status.ToString();

            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public bool Update(LeaveRequestModel m)
        {
            using var conn = new MySqlConnection(_cs);
            conn.Open();

            const string sql = @"
UPDATE leave_requests
SET
    leave_type = @type,
    reason     = @reason,
    date_from  = @from,
    date_to    = @to,
    half_day   = @half,
    status     = @status,
    updated_at = NOW()
WHERE id = @id;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.Add("@id", MySqlDbType.Int32).Value = m.Id;
            cmd.Parameters.Add("@type", MySqlDbType.VarChar).Value = m.LeaveType.ToString();
            cmd.Parameters.Add("@reason", MySqlDbType.VarChar).Value = (object?)m.Reason ?? DBNull.Value;
            cmd.Parameters.Add("@from", MySqlDbType.Date).Value = m.DateFrom.Date;
            cmd.Parameters.Add("@to", MySqlDbType.Date).Value = m.DateTo.Date;
            cmd.Parameters.Add("@half", MySqlDbType.Bit).Value = m.HalfDay ? 1 : 0;
            cmd.Parameters.Add("@status", MySqlDbType.VarChar).Value = m.Status.ToString();

            return cmd.ExecuteNonQuery() > 0;
        }

        public bool UpdateStatus(int id, LeaveStatus status, int? approverUserId = null)
        {
            using var conn = new MySqlConnection(_cs);
            conn.Open();

            var sql = @"
UPDATE leave_requests
SET status = @status, updated_at = NOW()";

            if (status == LeaveStatus.Approved || status == LeaveStatus.Rejected)
                sql += ", approver_id = @approver, approved_at = NOW()";

            sql += " WHERE id = @id;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.Add("@id", MySqlDbType.Int32).Value = id;
            cmd.Parameters.Add("@status", MySqlDbType.VarChar).Value = status.ToString();
            if (status == LeaveStatus.Approved || status == LeaveStatus.Rejected)
                cmd.Parameters.Add("@approver", MySqlDbType.Int32).Value = (object?)approverUserId ?? DBNull.Value;

            return cmd.ExecuteNonQuery() > 0;
        }

        public bool Delete(int id)
        {
            using var conn = new MySqlConnection(_cs);
            conn.Open();
            const string sql = "DELETE FROM leave_requests WHERE id = @id;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.Add("@id", MySqlDbType.Int32).Value = id;
            return cmd.ExecuteNonQuery() > 0;
        }

        // ======= JOIN employees and include e.full_name AS employee_name =======

        public LeaveRequestModel? GetById(int id)
        {
            using var conn = new MySqlConnection(_cs);
            conn.Open();

            const string sql = @"
SELECT lr.*, e.full_name AS employee_name
FROM leave_requests lr
LEFT JOIN employees e ON e.id = lr.employee_id
WHERE lr.id = @id
LIMIT 1;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.Add("@id", MySqlDbType.Int32).Value = id;

            using var r = cmd.ExecuteReader();
            return r.Read() ? Map(r) : null;
        }

        public List<LeaveRequestModel> GetForEmployee(int employeeId, DateTime? from = null, DateTime? to = null, LeaveStatus? status = null)
        {
            using var conn = new MySqlConnection(_cs);
            conn.Open();

            var sql = @"
SELECT lr.*, e.full_name AS employee_name
FROM leave_requests lr
LEFT JOIN employees e ON e.id = lr.employee_id
WHERE lr.employee_id = @empId";

            if (from.HasValue) sql += " AND lr.date_to   >= @from";
            if (to.HasValue) sql += " AND lr.date_from <= @to";
            if (status.HasValue) sql += " AND lr.status = @status";
            sql += " ORDER BY lr.date_from DESC, lr.id DESC";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.Add("@empId", MySqlDbType.Int32).Value = employeeId;
            if (from.HasValue) cmd.Parameters.Add("@from", MySqlDbType.Date).Value = from.Value.Date;
            if (to.HasValue) cmd.Parameters.Add("@to", MySqlDbType.Date).Value = to.Value.Date;
            if (status.HasValue) cmd.Parameters.Add("@status", MySqlDbType.VarChar).Value = status.Value.ToString();

            var list = new List<LeaveRequestModel>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public List<LeaveRequestModel> GetAll(DateTime? from = null, DateTime? to = null, LeaveStatus? status = null)
        {
            using var conn = new MySqlConnection(_cs);
            conn.Open();

            var sql = @"
SELECT lr.*, e.full_name AS employee_name
FROM leave_requests lr
LEFT JOIN employees e ON e.id = lr.employee_id
WHERE 1=1";

            if (from.HasValue) sql += " AND lr.date_to   >= @from";
            if (to.HasValue) sql += " AND lr.date_from <= @to";
            if (status.HasValue) sql += " AND lr.status = @status";
            sql += " ORDER BY lr.date_from DESC, lr.id DESC";

            using var cmd = new MySqlCommand(sql, conn);
            if (from.HasValue) cmd.Parameters.Add("@from", MySqlDbType.Date).Value = from.Value.Date;
            if (to.HasValue) cmd.Parameters.Add("@to", MySqlDbType.Date).Value = to.Value.Date;
            if (status.HasValue) cmd.Parameters.Add("@status", MySqlDbType.VarChar).Value = status.Value.ToString();

            var list = new List<LeaveRequestModel>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public bool Approve(int id, int approverUserId) => UpdateStatus(id, LeaveStatus.Approved, approverUserId);
        public bool Reject(int id, int approverUserId) => UpdateStatus(id, LeaveStatus.Rejected, approverUserId);

        public bool Cancel(int id)
        {
            using var conn = new MySqlConnection(_cs);
            conn.Open();
            const string sql = "UPDATE leave_requests SET status = 'Cancelled', updated_at = NOW() WHERE id = @id;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.Add("@id", MySqlDbType.Int32).Value = id;
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool IsOnApprovedLeave(int employeeId, DateTime date)
        {
            using var conn = new MySqlConnection(_cs);
            conn.Open();
            const string sql = @"
SELECT 1
FROM leave_requests
WHERE employee_id = @empId
  AND status = 'Approved'
  AND @date BETWEEN date_from AND date_to
LIMIT 1;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.Add("@empId", MySqlDbType.Int32).Value = employeeId;
            cmd.Parameters.Add("@date", MySqlDbType.Date).Value = date.Date;
            using var r = cmd.ExecuteReader();
            return r.Read();
        }

        // ==================== mapper ====================
        private static LeaveRequestModel Map(MySqlDataReader r)
        {
            var model = new LeaveRequestModel
            {
                Id = r.GetInt32("id"),
                EmployeeId = r.GetInt32("employee_id"),
                LeaveType = Enum.TryParse<LeaveType>(r["leave_type"]?.ToString(), true, out var lt) ? lt : LeaveType.Other,
                Reason = r.IsDBNull(r.GetOrdinal("reason")) ? null : r.GetString("reason"),
                DateFrom = r.GetDateTime("date_from"),
                DateTo = r.GetDateTime("date_to"),
                HalfDay = Convert.ToInt32(r["half_day"]) == 1,
                Status = Enum.TryParse<LeaveStatus>(r["status"]?.ToString(), true, out var ls) ? ls : LeaveStatus.Pending,
                ApproverUserId = HasColumn(r, "approver_id") && !r.IsDBNull(r.GetOrdinal("approver_id")) ? r.GetInt32("approver_id") : (int?)null,
                ApprovedAt = HasColumn(r, "approved_at") && !r.IsDBNull(r.GetOrdinal("approved_at")) ? r.GetDateTime("approved_at") : (DateTime?)null,
                CreatedAt = HasColumn(r, "created_at") && !r.IsDBNull(r.GetOrdinal("created_at")) ? r.GetDateTime("created_at") : DateTime.Now,
                UpdatedAt = HasColumn(r, "updated_at") && !r.IsDBNull(r.GetOrdinal("updated_at")) ? r.GetDateTime("updated_at") : (DateTime?)null
            };

            // Map joined employee name if present
            if (HasColumn(r, "employee_name") && !r.IsDBNull(r.GetOrdinal("employee_name")))
                model.EmployeeName = r.GetString("employee_name");

            return model;
        }

        private static bool HasColumn(IDataRecord r, string columnName)
        {
            // Safe check to allow mapper reuse with/without JOIN
            for (int i = 0; i < r.FieldCount; i++)
            {
                if (string.Equals(r.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
