using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using SihyuPOSPayroll.Models;

namespace SihyuPOSPayroll.Services
{
    public static class CategoryService
    {
        private const string Cs = "server=localhost;user=root;password=;database=sihyu_pos;";

        // ── Ensure table exists ────────────────────────────────────────────────
        public static void EnsureTable()
        {
            using var conn = new MySqlConnection(Cs);
            conn.Open();
            const string sql = @"
                CREATE TABLE IF NOT EXISTS inventory_categories (
                    Id   INT AUTO_INCREMENT PRIMARY KEY,
                    Name VARCHAR(120) NOT NULL UNIQUE
                );";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }

        // ── Read ───────────────────────────────────────────────────────────────
        public static List<CategoryModel> GetAll()
        {
            EnsureTable();
            var list = new List<CategoryModel>();
            using var conn = new MySqlConnection(Cs);
            conn.Open();
            using var cmd = new MySqlCommand(
                "SELECT Id, Name FROM inventory_categories ORDER BY Name;", conn);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
                list.Add(new CategoryModel { Id = rd.GetInt32(0), Name = rd.GetString(1) });
            return list;
        }

        // ── Create ─────────────────────────────────────────────────────────────
        public static void Add(string name)
        {
            EnsureTable();
            using var conn = new MySqlConnection(Cs);
            conn.Open();
            using var cmd = new MySqlCommand(
                "INSERT INTO inventory_categories (Name) VALUES (@n);", conn);
            cmd.Parameters.AddWithValue("@n", name.Trim());
            cmd.ExecuteNonQuery();
        }

        // ── Update ─────────────────────────────────────────────────────────────
        public static void Update(int id, string name)
        {
            using var conn = new MySqlConnection(Cs);
            conn.Open();
            using var cmd = new MySqlCommand(
                "UPDATE inventory_categories SET Name=@n WHERE Id=@id;", conn);
            cmd.Parameters.AddWithValue("@n", name.Trim());
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        // ── Delete ─────────────────────────────────────────────────────────────
        public static void Delete(int id)
        {
            using var conn = new MySqlConnection(Cs);
            conn.Open();
            using var cmd = new MySqlCommand(
                "DELETE FROM inventory_categories WHERE Id=@id;", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        // ── Name-only list (for ComboBox) ──────────────────────────────────────
        public static List<string> GetNames()
        {
            var names = new List<string>();
            foreach (var c in GetAll()) names.Add(c.Name);
            return names;
        }
    }
}
