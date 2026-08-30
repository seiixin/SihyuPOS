#nullable enable
using Microsoft.Data.Sqlite;
using SihyuPOSPayroll.Data;
using SihyuPOSPayroll.Models;
using System;
using System.Collections.Generic;

namespace SihyuPOSPayroll.Services
{
    /// <summary>
    /// CRUD for the <c>inventory_categories</c> table (SQLite).
    /// Schema is created by <see cref="AuthSchemaInitializer"/>.
    /// </summary>
    public static class CategoryService
    {
        // ── Read ───────────────────────────────────────────────────────────────

        public static List<CategoryModel> GetAll()
        {
            var list = new List<CategoryModel>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "SELECT Id, Name FROM inventory_categories ORDER BY Name;";
                using var rd = cmd.ExecuteReader();
                while (rd.Read())
                    list.Add(new CategoryModel { Id = rd.GetInt32(0), Name = rd.GetString(1) });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CategoryService] GetAll: {ex.Message}");
            }
            return list;
        }

        public static List<string> GetNames()
        {
            var names = new List<string>();
            foreach (var c in GetAll()) names.Add(c.Name);
            return names;
        }

        // ── Create ─────────────────────────────────────────────────────────────

        public static void Add(string name)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "INSERT OR IGNORE INTO inventory_categories (Name) VALUES (@n);";
                cmd.Parameters.AddWithValue("@n", name.Trim());
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CategoryService] Add: {ex.Message}");
            }
        }

        // ── Update ─────────────────────────────────────────────────────────────

        public static void Update(int id, string name)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "UPDATE inventory_categories SET Name = @n WHERE Id = @id;";
                cmd.Parameters.AddWithValue("@n",  name.Trim());
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CategoryService] Update: {ex.Message}");
            }
        }

        // ── Delete ─────────────────────────────────────────────────────────────

        public static void Delete(int id)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM inventory_categories WHERE Id = @id;";
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CategoryService] Delete: {ex.Message}");
            }
        }

        // ── Legacy compat ──────────────────────────────────────────────────────
        /// <summary>No-op — table is created by AuthSchemaInitializer at startup.</summary>
        public static void EnsureTable() { /* handled by AuthSchemaInitializer */ }
    }
}
