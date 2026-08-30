#nullable enable
using System;

namespace SihyuPOSPayroll.Models
{
    /// <summary>
    /// Application user linked to an employee record.
    /// Active users can log in; inactive users cannot.
    /// Maps to the <c>users</c> table (SQLite).
    /// </summary>
    public class UserModel
    {
        public int      Id         { get; set; }

        public string?  Email      { get; set; } = string.Empty;

        /// <summary>BCrypt-hashed password — never store or expose plain text.</summary>
        public string?  Password   { get; set; } = string.Empty;

        /// <summary>"Admin" | "Cashier" | "Employee" (default).</summary>
        public string?  Role       { get; set; } = "Employee";

        /// <summary>FK → employees.id (nullable — system/admin accounts have no employee record).</summary>
        public int?     EmployeeId { get; set; }

        /// <summary>ISO-8601 string stored in SQLite; surfaced as DateTime here.</summary>
        public DateTime CreatedAt  { get; set; }

        /// <summary>
        /// 1 = Active (can log in); 0 = Inactive (cannot log in).
        /// SQLite stores this as INTEGER (0/1).
        /// </summary>
        public bool     IsActive   { get; set; } = true;

        // ── Computed helpers ──────────────────────────────────────────────────
        public bool   CanLogin    => IsActive;
        public string StatusText  => IsActive ? "Active" : "Inactive";

        // ── Navigation (not always populated) ────────────────────────────────
        /// <summary>Linked employee record — populated by services that JOIN employees.</summary>
        public EmployeeModel? Employee { get; set; }
    }
}
