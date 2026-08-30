#nullable enable
using Microsoft.Data.Sqlite;
using SihyuPOSPayroll.Data;
using SihyuPOSPayroll.Helpers;
using SihyuPOSPayroll.Models;
using System;
using System.Diagnostics;

namespace SihyuPOSPayroll.Services
{
    /// <summary>
    /// Handles authentication (login / logout) against the SQLite <c>users</c> table.
    /// On a successful login it:
    ///   1. Verifies email + BCrypt password.
    ///   2. Generates a session token and writes a row to <c>login_sessions</c>.
    ///   3. Populates the in-memory <see cref="Session"/> singleton.
    ///
    /// On logout it:
    ///   1. Stamps <c>logged_out_at</c> on the active session row.
    ///   2. Clears the <see cref="Session"/> singleton.
    /// </summary>
    public class AuthService
    {
        // ── Login ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Authenticates a user by email and plain-text password.
        /// </summary>
        /// <returns>
        /// The authenticated <see cref="UserModel"/> (with linked <see cref="EmployeeModel"/>
        /// when one exists), or <c>null</c> if credentials are wrong / account inactive.
        /// </returns>
        public UserModel? Login(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return null;

            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();

                var user = FetchActiveUser(conn, email.Trim());
                if (user is null) return null;

                // Verify BCrypt hash — returns null on mismatch
                if (!PasswordHasher.Verify(password, user.Password ?? string.Empty))
                {
                    Debug.WriteLine($"[Auth] Password mismatch for {email}");
                    return null;
                }

                // Issue a session token and persist it
                var token = Guid.NewGuid().ToString("N"); // 32-char hex, no hyphens
                RecordSessionStart(conn, user.Id, token);

                // Populate the in-memory session
                Session.CurrentUserId   = user.Id;
                Session.CurrentUserRole = user.Role ?? "Employee";
                Session.CurrentUser     = user;
                Session.ActiveToken     = token;

                Debug.WriteLine($"[Auth] Login OK — userId={user.Id}, role={user.Role}");
                return user;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Auth] Login error: {ex.Message}");
                return null;
            }
        }

        // ── Logout ────────────────────────────────────────────────────────────

        /// <summary>
        /// Ends the current session: stamps <c>logged_out_at</c> in the DB and
        /// clears the in-memory <see cref="Session"/>.
        /// Safe to call even if no session is active (no-op).
        /// </summary>
        public void Logout()
        {
            var token = Session.ActiveToken;

            if (!string.IsNullOrEmpty(token))
            {
                try
                {
                    using var conn = SqliteConnectionFactory.CreateOpenConnection();
                    RecordSessionEnd(conn, token);
                }
                catch (Exception ex)
                {
                    // Non-fatal — clear in-memory state regardless.
                    Debug.WriteLine($"[Auth] Logout DB error: {ex.Message}");
                }
            }

            Session.Clear();
            Debug.WriteLine("[Auth] Session cleared.");
        }

        // ── Session query helpers ─────────────────────────────────────────────

        /// <summary>Returns the currently active session token's DB row, or null.</summary>
        public LoginSessionModel? GetActiveSession()
        {
            var token = Session.ActiveToken;
            if (string.IsNullOrEmpty(token)) return null;

            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();

                cmd.CommandText = @"
                    SELECT id, user_id, token, logged_in_at, logged_out_at
                    FROM   login_sessions
                    WHERE  token = @token
                    LIMIT  1;";
                cmd.Parameters.AddWithValue("@token", token);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read()) return null;

                return MapSession(reader);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Auth] GetActiveSession error: {ex.Message}");
                return null;
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Fetches a user by email where <c>is_active = 1</c>, joined with employees.
        /// Returns null if no match.
        /// </summary>
        private static UserModel? FetchActiveUser(SqliteConnection conn, string email)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    u.id          AS user_id,
                    u.email,
                    u.password,
                    u.role,
                    u.employee_id,
                    u.is_active,
                    u.created_at,
                    e.full_name,
                    e.position
                FROM   users u
                LEFT JOIN employees e ON e.id = u.employee_id
                WHERE  u.email    = @email COLLATE NOCASE
                  AND  u.is_active = 1
                LIMIT  1;";
            cmd.Parameters.AddWithValue("@email", email);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            EmployeeModel? employee = null;
            if (!reader.IsDBNull(reader.GetOrdinal("employee_id")))
            {
                employee = new EmployeeModel
                {
                    Id       = reader.GetInt32(reader.GetOrdinal("employee_id")),
                    FullName = reader.IsDBNull(reader.GetOrdinal("full_name"))
                                   ? null
                                   : reader.GetString(reader.GetOrdinal("full_name")),
                    Position = reader.IsDBNull(reader.GetOrdinal("position"))
                                   ? null
                                   : reader.GetString(reader.GetOrdinal("position")),
                };
            }

            var createdAtRaw = reader.IsDBNull(reader.GetOrdinal("created_at"))
                ? DateTime.UtcNow
                : DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at")));

            return new UserModel
            {
                Id         = reader.GetInt32(reader.GetOrdinal("user_id")),
                Email      = reader.GetString(reader.GetOrdinal("email")),
                Password   = reader.GetString(reader.GetOrdinal("password")),
                Role       = reader.GetString(reader.GetOrdinal("role")),
                EmployeeId = reader.IsDBNull(reader.GetOrdinal("employee_id"))
                                 ? null
                                 : reader.GetInt32(reader.GetOrdinal("employee_id")),
                IsActive   = reader.GetInt32(reader.GetOrdinal("is_active")) == 1,
                CreatedAt  = createdAtRaw,
                Employee   = employee,
            };
        }

        private static void RecordSessionStart(SqliteConnection conn, int userId, string token)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO login_sessions (user_id, token, logged_in_at)
                VALUES (@userId, @token, datetime('now'));";
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@token",  token);
            cmd.ExecuteNonQuery();
        }

        private static void RecordSessionEnd(SqliteConnection conn, string token)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE login_sessions
                SET    logged_out_at = datetime('now')
                WHERE  token = @token;";
            cmd.Parameters.AddWithValue("@token", token);
            cmd.ExecuteNonQuery();
        }

        private static LoginSessionModel MapSession(SqliteDataReader r)
        {
            var loggedInAt = DateTime.Parse(r.GetString(r.GetOrdinal("logged_in_at")));

            DateTime? loggedOutAt = null;
            if (!r.IsDBNull(r.GetOrdinal("logged_out_at")))
                loggedOutAt = DateTime.Parse(r.GetString(r.GetOrdinal("logged_out_at")));

            return new LoginSessionModel
            {
                Id          = r.GetInt32(r.GetOrdinal("id")),
                UserId      = r.GetInt32(r.GetOrdinal("user_id")),
                Token       = r.GetString(r.GetOrdinal("token")),
                LoggedInAt  = loggedInAt,
                LoggedOutAt = loggedOutAt,
            };
        }
    }
}
