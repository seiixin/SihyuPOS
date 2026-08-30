#nullable enable
using Microsoft.Data.Sqlite;
using SihyuPOSPayroll.Data;
using SihyuPOSPayroll.Models;
using System;
using System.Collections.ObjectModel;

namespace SihyuPOSPayroll.Services
{
    /// <summary>
    /// CRUD service for the <c>inventory_items</c> table (SQLite).
    /// Column names are PascalCase to match the existing schema and query patterns.
    /// Schema is created by <see cref="AuthSchemaInitializer"/>.
    ///
    /// Notes vs. old MySQL version:
    ///   • Constructor no longer runs INFORMATION_SCHEMA migrations — columns are
    ///     defined correctly in the schema from the start (Barcode, Price, ImagePath).
    ///   • DATE_ADD / CURDATE() replaced with SQLite-compatible date('now', '+7 days').
    ///   • GetExpiringItems uses date comparison on TEXT 'YYYY-MM-DD' columns which
    ///     sorts correctly in SQLite.
    /// </summary>
    public class InventoryService
    {
        // Schema is guaranteed by AuthSchemaInitializer — no constructor work needed.

        // ── Read ───────────────────────────────────────────────────────────────

        public ObservableCollection<InventoryItem> GetAllItems()
        {
            var items = new ObservableCollection<InventoryItem>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT Id, Barcode, ProductName, CategoryName,
                           Quantity, Price, ExpiryDate, ImagePath
                    FROM   inventory_items
                    ORDER  BY ProductName;";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    items.Add(MapItem(reader));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InventoryService] GetAllItems: {ex.Message}");
            }
            return items;
        }

        public InventoryItem? GetItemByBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return null;
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT Id, Barcode, ProductName, CategoryName,
                           Quantity, Price, ExpiryDate, ImagePath
                    FROM   inventory_items
                    WHERE  Barcode = @barcode
                    LIMIT  1;";
                cmd.Parameters.AddWithValue("@barcode", barcode.Trim());
                using var reader = cmd.ExecuteReader();
                return reader.Read() ? MapItem(reader) : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InventoryService] GetItemByBarcode: {ex.Message}");
                return null;
            }
        }

        public InventoryItem? GetItemById(int id)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT Id, Barcode, ProductName, CategoryName,
                           Quantity, Price, ExpiryDate, ImagePath
                    FROM   inventory_items
                    WHERE  Id = @id;";
                cmd.Parameters.AddWithValue("@id", id);
                using var reader = cmd.ExecuteReader();
                return reader.Read() ? MapItem(reader) : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InventoryService] GetItemById: {ex.Message}");
                return null;
            }
        }

        public ObservableCollection<InventoryItem> SearchItems(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText)) return GetAllItems();

            var items = new ObservableCollection<InventoryItem>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT Id, Barcode, ProductName, CategoryName,
                           Quantity, Price, ExpiryDate, ImagePath
                    FROM   inventory_items
                    WHERE  ProductName  LIKE @q
                       OR  CategoryName LIKE @q
                       OR  Barcode      LIKE @q
                    ORDER  BY ProductName;";
                cmd.Parameters.AddWithValue("@q", $"%{searchText}%");
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    items.Add(MapItem(reader));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InventoryService] SearchItems: {ex.Message}");
            }
            return items;
        }

        /// <summary>Items expiring within the next 7 days (or already expired).</summary>
        public ObservableCollection<InventoryItem> GetExpiringItems()
        {
            var items = new ObservableCollection<InventoryItem>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                // TEXT date comparison works correctly for ISO-8601 'YYYY-MM-DD' strings.
                cmd.CommandText = @"
                    SELECT Id, Barcode, ProductName, CategoryName,
                           Quantity, Price, ExpiryDate, ImagePath
                    FROM   inventory_items
                    WHERE  ExpiryDate IS NOT NULL
                      AND  ExpiryDate <= date('now', '+7 days')
                    ORDER  BY ExpiryDate;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    items.Add(MapItem(reader));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InventoryService] GetExpiringItems: {ex.Message}");
            }
            return items;
        }

        public ObservableCollection<InventoryItem> GetLowStockItems(int threshold = 10)
        {
            var items = new ObservableCollection<InventoryItem>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT Id, Barcode, ProductName, CategoryName,
                           Quantity, Price, ExpiryDate, ImagePath
                    FROM   inventory_items
                    WHERE  Quantity <= @threshold
                    ORDER  BY Quantity;";
                cmd.Parameters.AddWithValue("@threshold", threshold);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    items.Add(MapItem(reader));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InventoryService] GetLowStockItems: {ex.Message}");
            }
            return items;
        }

        // ── Create ─────────────────────────────────────────────────────────────

        public bool AddItem(InventoryItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO inventory_items
                        (Barcode, ProductName, CategoryName, Quantity, Price, ExpiryDate, ImagePath)
                    VALUES
                        (@barcode, @productName, @categoryName, @quantity, @price, @expiryDate, @imagePath);";

                BindItemParams(cmd, item);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InventoryService] AddItem: {ex.Message}");
                return false;
            }
        }

        // ── Update ─────────────────────────────────────────────────────────────

        public bool UpdateItem(InventoryItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE inventory_items SET
                        Barcode      = @barcode,
                        ProductName  = @productName,
                        CategoryName = @categoryName,
                        Quantity     = @quantity,
                        Price        = @price,
                        ExpiryDate   = @expiryDate,
                        ImagePath    = @imagePath,
                        UpdatedAt    = datetime('now')
                    WHERE Id = @id;";

                BindItemParams(cmd, item);
                cmd.Parameters.AddWithValue("@id", item.Id);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InventoryService] UpdateItem: {ex.Message}");
                return false;
            }
        }

        // ── Delete ─────────────────────────────────────────────────────────────

        public bool DeleteItem(int id)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM inventory_items WHERE Id = @id;";
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InventoryService] DeleteItem: {ex.Message}");
                return false;
            }
        }

        // ── Legacy compat ──────────────────────────────────────────────────────
        /// <summary>
        /// No-op — schema and sample data are handled by
        /// <see cref="AuthSchemaInitializer"/> at startup.
        /// </summary>
        public void InitializeDatabase() { /* handled by AuthSchemaInitializer */ }

        // ── Private helpers ────────────────────────────────────────────────────

        private static InventoryItem MapItem(SqliteDataReader r) => new InventoryItem
        {
            Id           = r.GetInt32(r.GetOrdinal("Id")),
            Barcode      = r.IsDBNull(r.GetOrdinal("Barcode"))      ? null : r.GetString(r.GetOrdinal("Barcode")),
            ProductName  = r.GetString(r.GetOrdinal("ProductName")),
            CategoryName = r.IsDBNull(r.GetOrdinal("CategoryName")) ? null : r.GetString(r.GetOrdinal("CategoryName")),
            Quantity     = r.GetInt32(r.GetOrdinal("Quantity")),
            Price        = Convert.ToDecimal(r.GetValue(r.GetOrdinal("Price"))),
            ExpiryDate   = r.IsDBNull(r.GetOrdinal("ExpiryDate"))   ? null
                               : DateTime.TryParse(r.GetString(r.GetOrdinal("ExpiryDate")), out var d) ? d : null,
            ImagePath    = r.IsDBNull(r.GetOrdinal("ImagePath"))    ? null : r.GetString(r.GetOrdinal("ImagePath")),
        };

        private static void BindItemParams(SqliteCommand cmd, InventoryItem item)
        {
            cmd.Parameters.AddWithValue("@barcode",      (object?)item.Barcode      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@productName",  item.ProductName);
            cmd.Parameters.AddWithValue("@categoryName", (object?)item.CategoryName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@quantity",     item.Quantity);
            cmd.Parameters.AddWithValue("@price",        item.Price);
            cmd.Parameters.AddWithValue("@expiryDate",   item.ExpiryDate.HasValue
                                                             ? item.ExpiryDate.Value.ToString("yyyy-MM-dd")
                                                             : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@imagePath",    (object?)item.ImagePath    ?? DBNull.Value);
        }
    }
}
