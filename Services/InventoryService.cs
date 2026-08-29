using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Data;
using MySql.Data.MySqlClient;
using SihyuPOSPayroll.Models;

namespace SihyuPOSPayroll.Services
{
    public class InventoryService
    {
        private readonly string connectionString = "server=localhost;user=root;password=;database=sihyu_pos;";

        public InventoryService()
        {
            EnsureBarcodeAndPriceColumns();
            EnsureImagePathColumn();
        }

        private void EnsureBarcodeAndPriceColumns()
        {
            try
            {
                using var conn = new MySqlConnection(connectionString);
                conn.Open();

                // Barcode
                const string checkBarcodeSql = @"
                    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME   = 'inventory_items'
                      AND COLUMN_NAME  = 'Barcode';";
                using var cmdB = new MySqlCommand(checkBarcodeSql, conn);
                if (Convert.ToInt32(cmdB.ExecuteScalar()) == 0)
                {
                    using var alter = new MySqlCommand(
                        "ALTER TABLE inventory_items ADD COLUMN Barcode VARCHAR(64) NULL AFTER Id;", conn);
                    alter.ExecuteNonQuery();
                    using var idx = new MySqlCommand(
                        "CREATE INDEX idx_inventory_barcode ON inventory_items(Barcode);", conn);
                    try { idx.ExecuteNonQuery(); } catch { }
                }

                // Price
                const string checkPriceSql = @"
                    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME   = 'inventory_items'
                      AND COLUMN_NAME  = 'Price';";
                using var cmdP = new MySqlCommand(checkPriceSql, conn);
                if (Convert.ToInt32(cmdP.ExecuteScalar()) == 0)
                {
                    using var alter = new MySqlCommand(
                        "ALTER TABLE inventory_items ADD COLUMN Price DECIMAL(12,2) NOT NULL DEFAULT 0 AFTER Quantity;", conn);
                    alter.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InventoryService] EnsureBarcodeAndPriceColumns: {ex.Message}");
            }
        }

        private void EnsureImagePathColumn()
        {
            try
            {
                using var conn = new MySqlConnection(connectionString);
                conn.Open();

                const string checkSql = @"
                    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME   = 'inventory_items'
                      AND COLUMN_NAME  = 'ImagePath';";

                using var checkCmd = new MySqlCommand(checkSql, conn);
                var count = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (count == 0)
                {
                    using var alterCmd = new MySqlCommand(
                        "ALTER TABLE inventory_items ADD COLUMN ImagePath VARCHAR(512) AFTER ExpiryDate;",
                        conn);
                    alterCmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InventoryService] EnsureImagePathColumn: {ex.Message}");
            }
        }

        public ObservableCollection<InventoryItem> GetAllItems()
        {
            var items = new ObservableCollection<InventoryItem>();

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                string query = @"SELECT Id, Barcode, ProductName, CategoryName, Quantity, Price, ExpiryDate, ImagePath
                                FROM inventory_items ORDER BY ProductName";

                using (var command = new MySqlCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new InventoryItem
                        {
                            Id           = reader.GetInt32("Id"),
                            Barcode      = reader.IsDBNull("Barcode")    ? null : reader.GetString("Barcode"),
                            ProductName  = reader.GetString("ProductName"),
                            CategoryName = reader.IsDBNull("CategoryName") ? null : reader.GetString("CategoryName"),
                            Quantity     = reader.GetInt32("Quantity"),
                            Price        = reader.GetDecimal("Price"),
                            ExpiryDate   = reader.IsDBNull("ExpiryDate")   ? null : reader.GetDateTime("ExpiryDate"),
                            ImagePath    = reader.IsDBNull("ImagePath")    ? null : reader.GetString("ImagePath"),
                        });
                    }
                }
            }

            return items;
        }

        /// <summary>Exact barcode match. Returns null if not found.</summary>
        public InventoryItem? GetItemByBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return null;
            try
            {
                using var conn = new MySqlConnection(connectionString);
                conn.Open();
                const string sql = @"SELECT Id, Barcode, ProductName, CategoryName, Quantity, Price, ExpiryDate, ImagePath
                                     FROM inventory_items
                                     WHERE Barcode = @Barcode
                                     LIMIT 1";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Barcode", barcode.Trim());
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return new InventoryItem
                    {
                        Id           = reader.GetInt32("Id"),
                        Barcode      = reader.IsDBNull("Barcode")      ? null : reader.GetString("Barcode"),
                        ProductName  = reader.GetString("ProductName"),
                        CategoryName = reader.IsDBNull("CategoryName") ? null : reader.GetString("CategoryName"),
                        Quantity     = reader.GetInt32("Quantity"),
                        Price        = reader.GetDecimal("Price"),
                        ExpiryDate   = reader.IsDBNull("ExpiryDate")   ? null : reader.GetDateTime("ExpiryDate"),
                        ImagePath    = reader.IsDBNull("ImagePath")    ? null : reader.GetString("ImagePath"),
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InventoryService] GetItemByBarcode: {ex.Message}");
            }
            return null;
        }

        public InventoryItem? GetItemById(int id)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                string query = @"SELECT Id, Barcode, ProductName, CategoryName, Quantity, Price, ExpiryDate, ImagePath
                                FROM inventory_items WHERE Id = @Id";

                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new InventoryItem
                            {
                                Id = reader.GetInt32("Id"),
                                Barcode = reader.IsDBNull("Barcode") ? null : reader.GetString("Barcode"),
                                ProductName = reader.GetString("ProductName"),
                                CategoryName = reader.IsDBNull("CategoryName") ? null : reader.GetString("CategoryName"),
                                Quantity = reader.GetInt32("Quantity"),
                                Price = reader.GetDecimal("Price"),
                                ExpiryDate = reader.IsDBNull("ExpiryDate") ? null : reader.GetDateTime("ExpiryDate"),
                                ImagePath = reader.IsDBNull("ImagePath") ? null : reader.GetString("ImagePath"),
                            };
                        }
                    }
                }
            }
            return null;
        }

        public bool AddItem(InventoryItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"INSERT INTO inventory_items (Barcode, ProductName, CategoryName, Quantity, Price, ExpiryDate, ImagePath) 
                                    VALUES (@Barcode, @ProductName, @CategoryName, @Quantity, @Price, @ExpiryDate, @ImagePath)";

                    using (var command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Barcode",      item.Barcode      ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@ProductName",  item.ProductName);
                        command.Parameters.AddWithValue("@CategoryName", item.CategoryName ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Quantity",     item.Quantity);
                        command.Parameters.AddWithValue("@Price",        item.Price);
                        command.Parameters.AddWithValue("@ExpiryDate",   item.ExpiryDate  ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@ImagePath",    item.ImagePath   ?? (object)DBNull.Value);

                        int result = command.ExecuteNonQuery();
                        return result > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding item: {ex.Message}");
                return false;
            }
        }

        public bool UpdateItem(InventoryItem updatedItem)
        {
            if (updatedItem == null) throw new ArgumentNullException(nameof(updatedItem));

            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"UPDATE inventory_items 
                                    SET Barcode      = @Barcode,
                                        ProductName  = @ProductName,
                                        CategoryName = @CategoryName,
                                        Quantity     = @Quantity,
                                        Price        = @Price,
                                        ExpiryDate   = @ExpiryDate,
                                        ImagePath    = @ImagePath
                                    WHERE Id = @Id";

                    using (var command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id",           updatedItem.Id);
                        command.Parameters.AddWithValue("@Barcode",      updatedItem.Barcode      ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@ProductName",  updatedItem.ProductName);
                        command.Parameters.AddWithValue("@CategoryName", updatedItem.CategoryName ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Quantity",     updatedItem.Quantity);
                        command.Parameters.AddWithValue("@Price",        updatedItem.Price);
                        command.Parameters.AddWithValue("@ExpiryDate",   updatedItem.ExpiryDate  ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@ImagePath",    updatedItem.ImagePath   ?? (object)DBNull.Value);

                        int result = command.ExecuteNonQuery();
                        return result > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating item: {ex.Message}");
                return false;
            }
        }

        public bool DeleteItem(int id)
        {
            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "DELETE FROM inventory_items WHERE Id = @Id";

                    using (var command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", id);
                        int result = command.ExecuteNonQuery();
                        return result > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting item: {ex.Message}");
                return false;
            }
        }

        public ObservableCollection<InventoryItem> SearchItems(string searchText)
        {
            var items = new ObservableCollection<InventoryItem>();

            if (string.IsNullOrWhiteSpace(searchText))
                return GetAllItems();

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                string query = @"SELECT Id, Barcode, ProductName, CategoryName, Quantity, Price, ExpiryDate, ImagePath
                                FROM inventory_items 
                                WHERE ProductName LIKE @SearchText 
                                   OR CategoryName LIKE @SearchText 
                                   OR Barcode LIKE @SearchText
                                ORDER BY ProductName";

                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SearchText", $"%{searchText}%");
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new InventoryItem
                            {
                                Id           = reader.GetInt32("Id"),
                                Barcode      = reader.IsDBNull("Barcode")    ? null : reader.GetString("Barcode"),
                                ProductName  = reader.GetString("ProductName"),
                                CategoryName = reader.IsDBNull("CategoryName") ? null : reader.GetString("CategoryName"),
                                Quantity     = reader.GetInt32("Quantity"),
                                Price        = reader.GetDecimal("Price"),
                                ExpiryDate   = reader.IsDBNull("ExpiryDate")   ? null : reader.GetDateTime("ExpiryDate"),
                                ImagePath    = reader.IsDBNull("ImagePath")    ? null : reader.GetString("ImagePath"),
                            });
                        }
                    }
                }
            }

            return items;
        }

        public ObservableCollection<InventoryItem> GetExpiringItems()
        {
            var items = new ObservableCollection<InventoryItem>();

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                string query = @"SELECT Id, Barcode, ProductName, CategoryName, Quantity, Price, ExpiryDate 
                                FROM inventory_items 
                                WHERE ExpiryDate IS NOT NULL 
                                  AND ExpiryDate <= DATE_ADD(CURDATE(), INTERVAL 7 DAY)
                                ORDER BY ExpiryDate";

                using (var command = new MySqlCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new InventoryItem
                        {
                            Id = reader.GetInt32("Id"),
                            Barcode = reader.IsDBNull("Barcode") ? null : reader.GetString("Barcode"),
                            ProductName = reader.GetString("ProductName"),
                            CategoryName = reader.IsDBNull("CategoryName") ? null : reader.GetString("CategoryName"),
                            Quantity = reader.GetInt32("Quantity"),
                            Price = reader.GetDecimal("Price"),
                            ExpiryDate = reader.GetDateTime("ExpiryDate")
                        });
                    }
                }
            }

            return items;
        }

        public ObservableCollection<InventoryItem> GetLowStockItems(int threshold = 10)
        {
            var items = new ObservableCollection<InventoryItem>();

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                string query = @"SELECT Id, Barcode, ProductName, CategoryName, Quantity, Price, ExpiryDate 
                                FROM inventory_items 
                                WHERE Quantity <= @Threshold 
                                ORDER BY Quantity";

                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Threshold", threshold);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new InventoryItem
                            {
                                Id = reader.GetInt32("Id"),
                                Barcode = reader.IsDBNull("Barcode") ? null : reader.GetString("Barcode"),
                                ProductName = reader.GetString("ProductName"),
                                CategoryName = reader.IsDBNull("CategoryName") ? null : reader.GetString("CategoryName"),
                                Quantity = reader.GetInt32("Quantity"),
                                Price = reader.GetDecimal("Price"),
                                ExpiryDate = reader.IsDBNull("ExpiryDate") ? null : reader.GetDateTime("ExpiryDate")
                            });
                        }
                    }
                }
            }

            return items;
        }

        public void InitializeDatabase()
        {
            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    string createTableQuery = @"
                        CREATE TABLE IF NOT EXISTS inventory_items (
                            Id           INT AUTO_INCREMENT PRIMARY KEY,
                            Barcode      VARCHAR(64) NULL,
                            ProductName  VARCHAR(255) NOT NULL,
                            CategoryName VARCHAR(255),
                            Quantity     INT NOT NULL DEFAULT 0,
                            Price        DECIMAL(12,2) NOT NULL DEFAULT 0,
                            ExpiryDate   DATE,
                            ImagePath    VARCHAR(512),
                            CreatedAt    TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                            UpdatedAt    TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                            INDEX idx_inventory_barcode (Barcode)
                        )";

                    using (var command = new MySqlCommand(createTableQuery, connection))
                        command.ExecuteNonQuery();

                    string countQuery = "SELECT COUNT(*) FROM inventory_items";
                    using (var countCommand = new MySqlCommand(countQuery, connection))
                    {
                        int count = Convert.ToInt32(countCommand.ExecuteScalar());
                        if (count == 0) InsertSampleData(connection);
                    }

                    CategoryService.EnsureTable();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing database: {ex.Message}");
            }
        }

        private void InsertSampleData(MySqlConnection connection)
        {
            string insertQuery = @"
                INSERT INTO inventory_items (Barcode, ProductName, CategoryName, Quantity, Price, ExpiryDate) VALUES
                (NULL, 'Coffee Beans - Arabica', 'Beverages', 25, 250.00, DATE_ADD(CURDATE(), INTERVAL 30 DAY)),
                (NULL, 'Milk - Whole', 'Dairy', 8, 95.00, DATE_ADD(CURDATE(), INTERVAL 3 DAY)),
                (NULL, 'Sugar - White', 'Sweeteners', 50, 65.00, NULL),
                (NULL, 'Croissants - Frozen', 'Bakery', 15, 45.00, DATE_ADD(CURDATE(), INTERVAL 5 DAY)),
                (NULL, 'Cheese - Cheddar', 'Dairy', 3, 180.00, DATE_ADD(CURDATE(), INTERVAL 1 DAY))";

            using (var command = new MySqlCommand(insertQuery, connection))
            {
                command.ExecuteNonQuery();
            }
        }
    }
}
