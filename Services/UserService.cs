#nullable enable
using Microsoft.Data.Sqlite;
using SihyuPOSPayroll.Data;
using SihyuPOSPayroll.Helpers;
using SihyuPOSPayroll.Models;
using System;
using System.Collections.Generic;

namespace SihyuPOSPayroll.Services
{
    /// <summary>
    /// CRUD service for the <c>users</c> table (SQLite).
    ///
    /// Bug fixed: AddUser and UpdateUser now hash passwords with BCrypt before
    /// writing to the database. Previously passwords were stored in plain text,
    /// which meant newly created accounts could never authenticate.
    ///
    /// Password update rule:
    ///   - If the caller sets a non-empty Password on the model, it is hashed and saved.
    ///   - If Password is null or empty on UpdateUser, the existing hash is left untouched
    ///     (allows updating email/role without knowing the current password).
    /// </summary>
    public class UserService
    {
        #region READ

        /// <summary>Returns all users with their linked employee name.</summary>
        public List<UserModel> GetAllUsers()
        {
            var users = new List<UserModel>();

            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();

                cmd.CommandText = @"
                    SELECT
                        u.id,
                        u.email,
                        u.password,
                        u.role,
                        u.employee_id,
                        u.is_active,
                        u.created_at,
                        e.full_name
                    FROM   users u
                    LEFT JOIN employees e ON e.id = u.employee_id
                    ORDER  BY u.id ASC;";

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    users.Add(new UserModel
                    {
                        Id         = reader.GetInt32(reader.GetOrdinal("id")),
                        Email      = reader.GetString(reader.GetOrdinal("email")),
                        Password   = reader.GetString(reader.GetOrdinal("password")),
                        Role       = reader.GetString(reader.GetOrdinal("role")),
                        EmployeeId = reader.IsDBNull(reader.GetOrdinal("employee_id"))
                                         ? null
                                         : reader.GetInt32(reader.GetOrdinal("employee_id")),
                        IsActive   = reader.GetInt32(reader.GetOrdinal("is_active")) == 1,
                        CreatedAt  = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at"))),
                        Employee   = new EmployeeModel
                        {
                            FullName = reader.IsDBNull(reader.GetOrdinal("full_name"))
                                           ? string.Empty
                                           : reader.GetString(reader.GetOrdinal("full_name"))
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[UserService] GetAllUsers error: {ex.Message}");
            }

            return users;
        }

        /// <summary>Returns a single user by primary key, or null if not found.</summary>
        public UserModel? GetUserById(int id)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();

                cmd.CommandText = @"
                    SELECT
                        u.id, u.email, u.password, u.role,
                        u.employee_id, u.is_active, u.created_at,
                        e.full_name
                    FROM   users u
                    LEFT JOIN employees e ON e.id = u.employee_id
                    WHERE  u.id = @id
                    LIMIT  1;";
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read()) return null;

                return new UserModel
                {
                    Id         = reader.GetInt32(reader.GetOrdinal("id")),
                    Email      = reader.GetString(reader.GetOrdinal("email")),
                    Password   = reader.GetString(reader.GetOrdinal("password")),
                    Role       = reader.GetString(reader.GetOrdinal("role")),
                    EmployeeId = reader.IsDBNull(reader.GetOrdinal("employee_id"))
                                     ? null
                                     : reader.GetInt32(reader.GetOrdinal("employee_id")),
                    IsActive   = reader.GetInt32(reader.GetOrdinal("is_active")) == 1,
                    CreatedAt  = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at"))),
                    Employee   = new EmployeeModel
                    {
                        FullName = reader.IsDBNull(reader.GetOrdinal("full_name"))
                                       ? string.Empty
                                       : reader.GetString(reader.GetOrdinal("full_name"))
                    }
                };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[UserService] GetUserById error: {ex.Message}");
                return null;
            }
        }

        /// <summary>Returns true when an active user with this email already exists.</summary>
        public bool EmailExists(string email, int excludeUserId = 0)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();

                cmd.CommandText = @"
                    SELECT COUNT(*) FROM users
                    WHERE  email = @email COLLATE NOCASE
                      AND  id   != @excludeId;";
                cmd.Parameters.AddWithValue("@email",     email);
                cmd.Parameters.AddWithValue("@excludeId", excludeUserId);

                return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L) > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[UserService] EmailExists error: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region CREATE

        /// <summary>
        /// Inserts a new user. The plain-text <c>user.Password</c> is BCrypt-hashed
        /// before being written to the database.
        /// </summary>
        public bool AddUser(UserModel user)
        {
            if (string.IsNullOrWhiteSpace(user.Password))
            {
                Console.Error.WriteLine("[UserService] AddUser: password cannot be empty.");
                return false;
            }

            try
            {
                // Hash the plain-text password before persisting
                var hashedPassword = PasswordHasher.Hash(user.Password);

                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();

                cmd.CommandText = @"
                    INSERT INTO users (email, password, role, employee_id, is_active, created_at)
                    VALUES (@email, @password, @role, @employeeId, @isActive, datetime('now'));";

                cmd.Parameters.AddWithValue("@email",      user.Email ?? string.Empty);
                cmd.Parameters.AddWithValue("@password",   hashedPassword);
                cmd.Parameters.AddWithValue("@role",       user.Role ?? "Employee");
                cmd.Parameters.AddWithValue("@isActive",   user.IsActive ? 1 : 0);

                if (user.EmployeeId.HasValue)
                    cmd.Parameters.AddWithValue("@employeeId", user.EmployeeId.Value);
                else
                    cmd.Parameters.AddWithValue("@employeeId", DBNull.Value);

                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[UserService] AddUser error: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region UPDATE

        /// <summary>
        /// Updates an existing user's email, role, employee link, and active status.
        ///
        /// Password handling:
        ///   - Non-empty <c>user.Password</c> → hashed and saved (password change).
        ///   - Null or empty <c>user.Password</c> → existing hash is preserved (no change).
        /// </summary>
        public bool UpdateUser(UserModel user)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();

                if (!string.IsNullOrWhiteSpace(user.Password))
                {
                    // Password change requested — hash and update everything
                    var hashedPassword = PasswordHasher.Hash(user.Password);

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        UPDATE users
                        SET    email       = @email,
                               password   = @password,
                               role       = @role,
                               employee_id = @employeeId,
                               is_active  = @isActive
                        WHERE  id = @id;";

                    cmd.Parameters.AddWithValue("@email",      user.Email ?? string.Empty);
                    cmd.Parameters.AddWithValue("@password",   hashedPassword);
                    cmd.Parameters.AddWithValue("@role",       user.Role ?? "Employee");
                    cmd.Parameters.AddWithValue("@isActive",   user.IsActive ? 1 : 0);
                    cmd.Parameters.AddWithValue("@id",         user.Id);

                    if (user.EmployeeId.HasValue)
                        cmd.Parameters.AddWithValue("@employeeId", user.EmployeeId.Value);
                    else
                        cmd.Parameters.AddWithValue("@employeeId", DBNull.Value);

                    return cmd.ExecuteNonQuery() > 0;
                }
                else
                {
                    // No password supplied — leave the existing hash untouched
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        UPDATE users
                        SET    email        = @email,
                               role         = @role,
                               employee_id  = @employeeId,
                               is_active    = @isActive
                        WHERE  id = @id;";

                    cmd.Parameters.AddWithValue("@email",      user.Email ?? string.Empty);
                    cmd.Parameters.AddWithValue("@role",       user.Role ?? "Employee");
                    cmd.Parameters.AddWithValue("@isActive",   user.IsActive ? 1 : 0);
                    cmd.Parameters.AddWithValue("@id",         user.Id);

                    if (user.EmployeeId.HasValue)
                        cmd.Parameters.AddWithValue("@employeeId", user.EmployeeId.Value);
                    else
                        cmd.Parameters.AddWithValue("@employeeId", DBNull.Value);

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[UserService] UpdateUser error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Changes only the password for a given user. The new plain-text password
        /// is BCrypt-hashed before being saved.
        /// </summary>
        public bool ChangePassword(int userId, string newPlainTextPassword)
        {
            if (string.IsNullOrWhiteSpace(newPlainTextPassword))
            {
                Console.Error.WriteLine("[UserService] ChangePassword: new password cannot be empty.");
                return false;
            }

            try
            {
                var hash = PasswordHasher.Hash(newPlainTextPassword);

                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();

                cmd.CommandText = "UPDATE users SET password = @hash WHERE id = @id;";
                cmd.Parameters.AddWithValue("@hash", hash);
                cmd.Parameters.AddWithValue("@id",   userId);

                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[UserService] ChangePassword error: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region DELETE

        /// <summary>Deletes a user by ID. Also cascades to login_sessions via FK.</summary>
        public bool DeleteUserById(int id)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();

                cmd.CommandText = "DELETE FROM users WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", id);

                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[UserService] DeleteUserById error: {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}
