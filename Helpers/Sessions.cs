#nullable enable
using SihyuPOSPayroll.Models;

namespace SihyuPOSPayroll.Helpers
{
    /// <summary>
    /// In-memory session state for the currently authenticated user.
    /// Populated by <see cref="SihyuPOSPayroll.Services.AuthService"/> on login
    /// and cleared on logout.
    ///
    /// This is a process-local singleton (static class) — suitable for a desktop
    /// WPF application where only one user is active at a time.
    /// </summary>
    public static class Session
    {
        // ── Core identity ─────────────────────────────────────────────────────

        /// <summary>0 when no user is logged in.</summary>
        public static int    CurrentUserId   { get; set; }

        /// <summary>"Admin" | "Cashier" | "Employee" — empty string when not logged in.</summary>
        public static string CurrentUserRole { get; set; } = string.Empty;

        // ── Extended session data ─────────────────────────────────────────────

        /// <summary>
        /// The full authenticated user model.
        /// Populated after a successful login; null when logged out.
        /// </summary>
        public static UserModel? CurrentUser { get; set; }

        /// <summary>
        /// The session token (GUID string) written to <c>login_sessions</c>.
        /// Used to mark the session as logged-out in the DB.
        /// </summary>
        public static string? ActiveToken { get; set; }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>True when a user is currently authenticated.</summary>
        public static bool IsLoggedIn => CurrentUser is not null && CurrentUserId > 0;

        public static bool IsAdmin    => CurrentUserRole == "Admin";
        public static bool IsCashier  => CurrentUserRole == "Cashier";
        public static bool IsEmployee => CurrentUserRole == "Employee";

        /// <summary>Clears all session state (call on logout).</summary>
        public static void Clear()
        {
            CurrentUserId   = 0;
            CurrentUserRole = string.Empty;
            CurrentUser     = null;
            ActiveToken     = null;
        }
    }
}
