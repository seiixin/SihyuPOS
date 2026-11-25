#nullable enable
using Microsoft.Data.Sqlite;
using SihyuPOSPayroll.Data;
using SihyuPOSPayroll.Models;
using System;
using System.Collections.Generic;
using System.Data;

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
        // ── user -> employee resolver ─────────────────────────────────────────

        private static int GetEmployeeIdOrThrow(int userId)
        {
            using var conn = SqliteConnectionFactory.CreateOpenConnection();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT employee_id
                FROM   users
                WHERE  id = @uid
                LIMIT 1;";
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

        // ── Core CRUD ─────────────────────────────────────────────────────────

        public int Create(LeaveRequestModel m)
        {
            if (m.EmployeeId <= 0)
                throw new ArgumentException("EmployeeId must be a valid employees.id", nameof(m.EmployeeId));

            using var conn = SqliteConnectionFactory.CreateOpenConnection();
            using var cmd  = conn.CreateCommand();
            // BUG FIX: replaced MySQL NOW() with datetime('now')
            cmd.CommandText = @"
                INSERT INTO leave_requests
                    (employee_id, leave_type, reason, date_from, date_to, half_day, status, created_at)
                VALUES
                    (@empId, @type, @reason, @from, @to, @half, @status, datetime('now'));
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@empId",  m.EmployeeId);
            cmd.Parameters.AddWithValue("@type",   m.LeaveType.ToString());
            cmd.Parameters.AddWithValue("@reason", (object?)m.Reason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@from",   m.DateFrom.Date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@to",     m.DateTo.Date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@half",   m.HalfDay ? 1 : 0);
            cmd.Parameters.AddWithValue("@status", m.Status.ToString());

            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public bool Update(LeaveRequestModel m)
        {
            using var conn = SqliteConnectionFactory.CreateOpenConnection();
            using var cmd  = conn.CreateCommand();
            // BUG FIX: replaced MySQL NOW() with datetime('now')
            cmd.CommandText = @"
                UPDATE leave_requests SET
                    leave_type = @type,
                    reason     = @reason,
                    date_from  = @from,
                    date_to    = @to,
                    half_day   = @half,
                    status     = @status,
                    updated_at = datetime('now')
                WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id",     m.Id);
            cmd.Parameters.AddWithValue("@type",   m.LeaveType.ToString());
            cmd.Parameters.AddWithValue("@reason", (object?)m.Reason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@from",   m.DateFrom.Date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@to",     m.DateTo.Date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@half",   m.HalfDay ? 1 : 0);
            cmd.Parameters.AddWithValue("@status", m.Status.ToString());

            return cmd.ExecuteNonQuery() > 0;
        }

        public bool UpdateStatus(int id, LeaveStatus status, int? approverUserId = null)
        {
            using var conn = SqliteConnectionFactory.CreateOpenConnection();
            using var cmd  = conn.CreateCommand();

            // BUG FIX: replaced MySQL NOW() with datetime('now')
            var sql = "UPDATE leave_requests SET status = @status, updated_at = datetime('now')";

            if (status == LeaveStatus.Approved || status == LeaveStatus.Rejected)
                sql += ", approver_id = @approver, approved_at = datetime('now')";

            sql += " WHERE id = @id;";
            cmd.CommandText = sql;

            cmd.Parameters.AddWithValue("@id",     id);
            cmd.Parameters.AddWithValue("@status", status.ToString());
            if (status == LeaveStatus.Approved || status == LeaveStatus.Rejected)
                cmd.Parameters.AddWithValue("@approver", (object?)approverUserId ?? DBNull.Value);

            return cmd.ExecuteNonQuery() > 0;
        }

        public bool Delete(int id)
        {
            using var conn = SqliteConnectionFactory.CreateOpenConnection();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM leave_requests WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }

        // ── Read ──────────────────────────────────────────────────────────────

        public LeaveRequestModel? GetById(int id)
        {
            using var conn = SqliteConnectionFactory.CreateOpenConnection();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT lr.*, e.full_name AS employee_name
                FROM   leave_requests lr
                LEFT JOIN employees e ON e.id = lr.employee_id
                WHERE  lr.id = @id
                LIMIT 1;";
            cmd.Parameters.AddWithValue("@id", id);

            using var r = cmd.ExecuteReader();
            return r.Read() ? Map(r) : null;
        }

        public List<LeaveRequestModel> GetForEmployee(
            int employeeId,
            DateTime? from   = null,
            DateTime? to     = null,
            LeaveStatus? status = null)
        {
            using var conn = SqliteConnectionFactory.CreateOpenConnection();
            using var cmd  = conn.CreateCommand();

            var sql = @"
                SELECT lr.*, e.full_name AS employee_name
                FROM   leave_requests lr
                LEFT JOIN employees e ON e.id = lr.employee_id
                WHERE  lr.employee_id = @empId";

            if (from.HasValue)   { sql += " AND lr.date_to   >= @from";   cmd.Parameters.AddWithValue("@from",   from.Value.Date.ToString("yyyy-MM-dd")); }
            if (to.HasValue)     { sql += " AND lr.date_from <= @to";     cmd.Parameters.AddWithValue("@to",     to.Value.Date.ToString("yyyy-MM-dd")); }
            if (status.HasValue) { sql += " AND lr.status     = @status"; cmd.Parameters.AddWithValue("@status", status.Value.ToString()); }
            sql += " ORDER BY lr.date_from DESC, lr.id DESC;";

            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@empId", employeeId);

            var list = new List<LeaveRequestModel>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public List<LeaveRequestModel> GetAll(
            DateTime? from   = null,
            DateTime? to     = null,
            LeaveStatus? status = null)
        {
            using var conn = SqliteConnectionFactory.CreateOpenConnection();
            using var cmd  = conn.CreateCommand();

            var sql = @"
                SELECT lr.*, e.full_name AS employee_name
                FROM   leave_requests lr
                LEFT JOIN employees e ON e.id = lr.employee_id
                WHERE  1=1";

            if (from.HasValue)   { sql += " AND lr.date_to   >= @from";   cmd.Parameters.AddWithValue("@from",   from.Value.Date.ToString("yyyy-MM-dd")); }
            if (to.HasValue)     { sql += " AND lr.date_from <= @to";     cmd.Parameters.AddWithValue("@to",     to.Value.Date.ToString("yyyy-MM-dd")); }
            if (status.HasValue) { sql += " AND lr.status     = @status"; cmd.Parameters.AddWithValue("@status", status.Value.ToString()); }
            sql += " ORDER BY lr.date_from DESC, lr.id DESC;";

            cmd.CommandText = sql;

            var list = new List<LeaveRequestModel>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public bool Approve(int id, int approverUserId) => UpdateStatus(id, LeaveStatus.Approved, approverUserId);
        public bool Reject(int id,  int approverUserId) => UpdateStatus(id, LeaveStatus.Rejected, approverUserId);

        public bool Cancel(int id)
        {
            using var conn = SqliteConnectionFactory.CreateOpenConnection();
            using var cmd  = conn.CreateCommand();
            // BUG FIX: replaced MySQL NOW() with datetime('now')
            cmd.CommandText = "UPDATE leave_requests SET status = 'Cancelled', updated_at = datetime('now') WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool IsOnApprovedLeave(int employeeId, DateTime date)
        {
            using var conn = SqliteConnectionFactory.CreateOpenConnection();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT 1
                FROM   leave_requests
                WHERE  employee_id = @empId
                  AND  status      = 'Approved'
                  AND  @date BETWEEN date_from AND date_to
                LIMIT 1;";
            cmd.Parameters.AddWithValue("@empId", employeeId);
            cmd.Parameters.AddWithValue("@date",  date.Date.ToString("yyyy-MM-dd"));
            using var r = cmd.ExecuteReader();
            return r.Read();
        }

        // ── Mapper ────────────────────────────────────────────────────────────

        private static LeaveRequestModel Map(SqliteDataReader r)
        {
            var model = new LeaveRequestModel
            {
                Id         = r.GetInt32(r.GetOrdinal("id")),
                EmployeeId = r.GetInt32(r.GetOrdinal("employee_id")),
                LeaveType  = Enum.TryParse<LeaveType>(
                                 r.IsDBNull(r.GetOrdinal("leave_type")) ? "" : r.GetString(r.GetOrdinal("leave_type")),
                                 true, out var lt) ? lt : LeaveType.Other,
                Reason     = r.IsDBNull(r.GetOrdinal("reason")) ? null : r.GetString(r.GetOrdinal("reason")),
                DateFrom   = DateTime.Parse(r.GetString(r.GetOrdinal("date_from"))),
                DateTo     = DateTime.Parse(r.GetString(r.GetOrdinal("date_to"))),
                HalfDay    = Convert.ToInt32(r.GetValue(r.GetOrdinal("half_day"))) == 1,
                Status     = Enum.TryParse<LeaveStatus>(
                                 r.IsDBNull(r.GetOrdinal("status")) ? "" : r.GetString(r.GetOrdinal("status")),
                                 true, out var ls) ? ls : LeaveStatus.Pending,
                ApproverUserId = HasColumn(r, "approver_id") && !r.IsDBNull(r.GetOrdinal("approver_id"))
                                     ? r.GetInt32(r.GetOrdinal("approver_id"))
                                     : (int?)null,
                ApprovedAt     = HasColumn(r, "approved_at") && !r.IsDBNull(r.GetOrdinal("approved_at"))
                                     ? DateTime.TryParse(r.GetString(r.GetOrdinal("approved_at")), out var aa) ? aa : (DateTime?)null
                                     : (DateTime?)null,
                CreatedAt  = HasColumn(r, "created_at") && !r.IsDBNull(r.GetOrdinal("created_at"))
                                 ? DateTime.TryParse(r.GetString(r.GetOrdinal("created_at")), out var ca) ? ca : DateTime.Now
                                 : DateTime.Now,
                UpdatedAt  = HasColumn(r, "updated_at") && !r.IsDBNull(r.GetOrdinal("updated_at"))
                                 ? DateTime.TryParse(r.GetString(r.GetOrdinal("updated_at")), out var ua) ? ua : (DateTime?)null
                                 : (DateTime?)null
            };

            if (HasColumn(r, "employee_name") && !r.IsDBNull(r.GetOrdinal("employee_name")))
                model.EmployeeName = r.GetString(r.GetOrdinal("employee_name"));

            return model;
        }

        private static bool HasColumn(IDataRecord r, string columnName)
        {
            for (int i = 0; i < r.FieldCount; i++)
                if (string.Equals(r.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
