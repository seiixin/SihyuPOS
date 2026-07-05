#nullable enable
using System;

namespace SihyuPOSPayroll.Models
{
    /// <summary>
    /// Application user linked to an employee record.
    /// Active users can log in; inactive users cannot.
    /// Maps to `users` table where `is_active` is TINYINT(1).
    /// </summary>
    public class UserModel
    {
        public int Id { get; set; }

        public string? Email { get; set; } = string.Empty;

        // NOTE: store hashed password in DB; avoid exposing plaintext.
        public string? Password { get; set; } = string.Empty;

        /// <summary>
        /// "Admin" | "Employee" (default) | other roles as needed.
        /// </summary>
        public string? Role { get; set; } = "Employee";

        /// <summary>
        /// FK to employees.id (nullable).
        /// </summary>
        public int? EmployeeId { get; set; }

        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// True = Active (can log in); False = Inactive (cannot log in).
        /// Default aligns with DB DEFAULT 1.
        /// </summary>
        public bool IsActive { get; set; } = true;

        // Convenience/computed props (optional helpers)
        public bool CanLogin => IsActive;
        public string StatusText => IsActive ? "Active" : "Inactive";

        // Back-link to employee (optional, not always populated).
        public EmployeeModel? Employee { get; set; }
    }
}
