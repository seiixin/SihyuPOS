#nullable enable
using System;

namespace SihyuPOSPayroll.Models
{
    /// <summary>
    /// Tracks a single login/logout cycle for a user.
    /// Maps to the <c>login_sessions</c> table.
    /// </summary>
    public class LoginSessionModel
    {
        public int       Id         { get; set; }

        /// <summary>FK → users.id</summary>
        public int       UserId     { get; set; }

        /// <summary>
        /// Opaque token generated at login time (GUID).
        /// Can be used to invalidate a specific session.
        /// </summary>
        public string    Token      { get; set; } = string.Empty;

        public DateTime  LoggedInAt  { get; set; }
        public DateTime? LoggedOutAt { get; set; }

        /// <summary>True while LoggedOutAt is null.</summary>
        public bool IsActive => LoggedOutAt is null;
    }
}
