#nullable enable
using Microsoft.Data.Sqlite;
using SihyuPOSPayroll.Data;
using SihyuPOSPayroll.Models;
using System;
using System.Collections.Generic;

namespace SihyuPOSPayroll.Services
{
    /// <summary>
    /// CRUD for the <c>menu</c> table (SQLite).
    /// Column names match the schema exactly: Id, Name, Category, Price, image_url,
    /// Description, Created_At — preserved from the original MySQL queries.
    /// </summary>
    public class MenuService
    {
        // ── Read ───────────────────────────────────────────────────────────────

        public List<MenuModel> GetAllMenuItems()
        {
            var items = new List<MenuModel>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT Id, Name, Category, Price, image_url, Description, Created_At
                    FROM   menu
                    ORDER  BY Name;";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    items.Add(MapMenu(reader));
            }
            catch (Exception ex)
            {
                // BUG 19 FIX: log and return empty list instead of throwing.
                // Callers (POS picker, OrderService) do not catch exceptions from
                // this method; a throw here crashes the product list load entirely.
                Console.Error.WriteLine($"[MenuService] GetAllMenuItems: {ex.Message}");
            }
            return items;
        }

        // ── Create ─────────────────────────────────────────────────────────────

        public bool AddMenuItem(MenuModel item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO menu (Name, Category, Price, image_url, Description, Created_At)
                    VALUES (@name, @category, @price, @imageUrl, @description, datetime('now'));";

                BindMenuParams(cmd, item);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                // BUG 19 FIX: log and return false instead of throwing.
                Console.Error.WriteLine($"[MenuService] AddMenuItem: {ex.Message}");
                return false;
            }
        }

        // Legacy alias
        public bool InsertMenuItem(MenuModel item) => AddMenuItem(item);

        // ── Update ─────────────────────────────────────────────────────────────

        public bool UpdateMenuItem(MenuModel item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE menu
                    SET    Name        = @name,
                           Category   = @category,
                           Price      = @price,
                           image_url  = @imageUrl,
                           Description = @description
                    WHERE  Id = @id;";

                BindMenuParams(cmd, item);
                cmd.Parameters.AddWithValue("@id", item.Id);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                // BUG 19 FIX: log and return false instead of throwing.
                Console.Error.WriteLine($"[MenuService] UpdateMenuItem: {ex.Message}");
                return false;
            }
        }

        // ── Delete ─────────────────────────────────────────────────────────────

        public bool DeleteMenuItem(int menuItemId)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM menu WHERE Id = @id;";
                cmd.Parameters.AddWithValue("@id", menuItemId);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                // BUG 19 FIX: log and return false instead of throwing.
                Console.Error.WriteLine($"[MenuService] DeleteMenuItem: {ex.Message}");
                return false;
            }
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private static MenuModel MapMenu(SqliteDataReader r) => new MenuModel
        {
            Id          = r.GetInt32(r.GetOrdinal("Id")),
            Name        = r.IsDBNull(r.GetOrdinal("Name"))        ? string.Empty : r.GetString(r.GetOrdinal("Name")),
            Category    = r.IsDBNull(r.GetOrdinal("Category"))    ? string.Empty : r.GetString(r.GetOrdinal("Category")),
            Price       = r.IsDBNull(r.GetOrdinal("Price"))       ? 0m           : Convert.ToDecimal(r.GetValue(r.GetOrdinal("Price"))),
            ImageUrl    = r.IsDBNull(r.GetOrdinal("image_url"))   ? string.Empty : r.GetString(r.GetOrdinal("image_url")),
            Description = r.IsDBNull(r.GetOrdinal("Description")) ? string.Empty : r.GetString(r.GetOrdinal("Description")),
            CreatedAt   = r.IsDBNull(r.GetOrdinal("Created_At"))  ? DateTime.MinValue
                              : DateTime.TryParse(r.GetString(r.GetOrdinal("Created_At")), out var dt) ? dt : DateTime.MinValue,
        };

        private static void BindMenuParams(SqliteCommand cmd, MenuModel item)
        {
            cmd.Parameters.AddWithValue("@name",        item.Name        ?? string.Empty);
            cmd.Parameters.AddWithValue("@category",    item.Category    ?? string.Empty);
            cmd.Parameters.AddWithValue("@price",       (object?)item.Price ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@imageUrl",    item.ImageUrl    ?? string.Empty);
            cmd.Parameters.AddWithValue("@description", item.Description ?? string.Empty);
        }
    }
}
