#nullable enable
using Microsoft.Data.Sqlite;
using SihyuPOSPayroll.Data;
using SihyuPOSPayroll.Models;
using System;
using System.Collections.Generic;
namespace SihyuPOSPayroll.Services
{
    /// <summary>
    /// CRUD service for the <c>roles</c> table (SQLite).
    /// Built-in roles (Admin, Cashier, Employee) can be renamed/described but not deleted
    /// while users are still assigned to them — the service enforces this via a usage check.
    /// </summary>
    public class RoleService
    {
        // ── READ ──────────────────────────────────────────────────────────────

        public List<RoleModel> GetAllRoles()
        {
            var list = new List<RoleModel>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "SELECT id, name, description FROM roles ORDER BY id ASC;";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    list.Add(Map(r));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[RoleService] GetAllRoles: {ex.Message}");
            }
            return list;
        }

        public RoleModel? GetById(int id)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "SELECT id, name, description FROM roles WHERE id = @id LIMIT 1;";
                cmd.Parameters.AddWithValue("@id", id);
                using var r = cmd.ExecuteReader();
                return r.Read() ? Map(r) : null;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[RoleService] GetById: {ex.Message}");
                return null;
            }
        }

        /// <summary>Returns all distinct role name strings currently assigned to users.</summary>
        public List<string> GetRoleNamesInUse()
        {
            var list = new List<string>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "SELECT DISTINCT role FROM users WHERE role IS NOT NULL;";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var v = r.IsDBNull(0) ? null : r.GetString(0);
                    if (!string.IsNullOrWhiteSpace(v)) list.Add(v);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[RoleService] GetRoleNamesInUse: {ex.Message}");
            }
            return list;
        }

        /// <summary>Returns how many users are assigned to the given role name.</summary>
        public int CountUsersWithRole(string roleName)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM users WHERE role = @role;";
                cmd.Parameters.AddWithValue("@role", roleName.Trim());
                var res = cmd.ExecuteScalar();
                return res == null || res == DBNull.Value ? 0 : Convert.ToInt32(res);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[RoleService] CountUsersWithRole: {ex.Message}");
                return 0;
            }
        }

        // ── CREATE ────────────────────────────────────────────────────────────

        public bool AddRole(RoleModel role)
        {
            if (string.IsNullOrWhiteSpace(role.Name)) return false;
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO roles (name, description)
                    VALUES (@name, @desc);";
                cmd.Parameters.AddWithValue("@name", role.Name.Trim());
                cmd.Parameters.AddWithValue("@desc", role.Description?.Trim() ?? string.Empty);
                var rows = cmd.ExecuteNonQuery();
                return rows > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[RoleService] AddRole: {ex.Message}");
                return false;
            }
        }

        // ── UPDATE ────────────────────────────────────────────────────────────

        /// <summary>
        /// Updates the role row AND renames the role value in the users table
        /// so existing user assignments stay consistent.
        /// </summary>
        public bool UpdateRole(RoleModel role, string oldName)
        {
            if (string.IsNullOrWhiteSpace(role.Name)) return false;
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var tx   = conn.BeginTransaction();

                // 1. Update the roles row
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        UPDATE roles SET name = @name, description = @desc
                        WHERE id = @id;";
                    cmd.Parameters.AddWithValue("@name", role.Name.Trim());
                    cmd.Parameters.AddWithValue("@desc", role.Description?.Trim() ?? string.Empty);
                    cmd.Parameters.AddWithValue("@id",   role.Id);
                    cmd.ExecuteNonQuery();
                }

                // 2. Cascade rename into users table (if name changed)
                if (!string.Equals(oldName, role.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    using var syncCmd = conn.CreateCommand();
                    syncCmd.Transaction = tx;
                    syncCmd.CommandText = "UPDATE users SET role = @newName WHERE role = @oldName;";
                    syncCmd.Parameters.AddWithValue("@newName", role.Name.Trim());
                    syncCmd.Parameters.AddWithValue("@oldName", oldName.Trim());
                    syncCmd.ExecuteNonQuery();
                }

                tx.Commit();
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[RoleService] UpdateRole: {ex.Message}");
                return false;
            }
        }

        // ── DELETE ────────────────────────────────────────────────────────────

        /// <summary>
        /// Deletes a role. Fails (returns false) if any user is still assigned to it.
        /// </summary>
        public bool DeleteRole(int id, string roleName)
        {
            try
            {
                int inUse = CountUsersWithRole(roleName);
                if (inUse > 0) return false; // caller should surface a message

                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM roles WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[RoleService] DeleteRole: {ex.Message}");
                return false;
            }
        }

        // ── HELPER ────────────────────────────────────────────────────────────

        private static RoleModel Map(SqliteDataReader r) => new()
        {
            Id          = r.GetInt32(r.GetOrdinal("id")),
            Name        = r.IsDBNull(r.GetOrdinal("name"))        ? string.Empty : r.GetString(r.GetOrdinal("name")),
            Description = r.IsDBNull(r.GetOrdinal("description")) ? string.Empty : r.GetString(r.GetOrdinal("description")),
        };

        // ── ROLE PERMISSIONS ─────────────────────────────────────────────────

        /// <summary>Returns the set of module_key strings granted to a role by name.</summary>
        public HashSet<string> GetModulesForRole(string roleName)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(roleName)) return set;
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT module_key FROM role_permissions
                    WHERE role_name = @role;";
                cmd.Parameters.AddWithValue("@role", roleName.Trim());
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    if (!r.IsDBNull(0)) set.Add(r.GetString(0));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[RoleService] GetModulesForRole: {ex.Message}");
            }
            return set;
        }

        /// <summary>
        /// Replaces all module grants for a role in a single transaction:
        /// deletes all existing entries then inserts the provided set.
        /// </summary>
        public bool SaveModulesForRole(string roleName, IEnumerable<string> moduleKeys)
        {
            if (string.IsNullOrWhiteSpace(roleName)) return false;
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var tx   = conn.BeginTransaction();

                using (var del = conn.CreateCommand())
                {
                    del.Transaction  = tx;
                    del.CommandText  = "DELETE FROM role_permissions WHERE role_name = @role;";
                    del.Parameters.AddWithValue("@role", roleName.Trim());
                    del.ExecuteNonQuery();
                }

                using (var ins = conn.CreateCommand())
                {
                    ins.Transaction  = tx;
                    ins.CommandText  = @"
                        INSERT OR IGNORE INTO role_permissions (role_name, module_key)
                        VALUES (@role, @module);";
                    var pRole   = ins.Parameters.Add("@role",   SqliteType.Text);
                    var pModule = ins.Parameters.Add("@module", SqliteType.Text);
                    pRole.Value = roleName.Trim();

                    foreach (var key in moduleKeys)
                    {
                        if (string.IsNullOrWhiteSpace(key)) continue;
                        pModule.Value = key.Trim();
                        ins.ExecuteNonQuery();
                    }
                }

                tx.Commit();
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[RoleService] SaveModulesForRole: {ex.Message}");
                return false;
            }
        }
    }
}
