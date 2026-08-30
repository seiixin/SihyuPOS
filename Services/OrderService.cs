#nullable enable
using Microsoft.Data.Sqlite;
using SihyuPOSPayroll.Data;
using SihyuPOSPayroll.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SihyuPOSPayroll.Services
{
    public class OrderService
    {
        // ── Table picker model ────────────────────────────────────────────────

        public sealed class TableOption
        {
            public string TableNumber { get; set; } = string.Empty;
            public string Status      { get; set; } = "Available";
            public bool   Selectable  => Status == "Available";
        }

        private static readonly string[] _openStatuses = { "Pending", "Preparing", "Served" };

        // ── Table picker ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns all café tables with derived occupancy status.
        /// If <paramref name="currentOrderId"/> is supplied, that order's table
        /// is not counted as occupied (prevents blocking the current order's table).
        /// </summary>
        public List<TableOption> GetTablesForPicker(int? currentOrderId = null)
        {
            var result = new List<TableOption>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();

                // SQLite does not support string interpolation of IN lists;
                // build a literal for the status values (safe — they are code constants).
                var inList = string.Join("','", _openStatuses);
                cmd.CommandText = $@"
                    SELECT
                        t.table_number AS TableNumber,
                        CASE WHEN EXISTS (
                            SELECT 1 FROM orders o
                            WHERE  o.table_number  = t.table_number
                              AND  o.payment_status = 'Unpaid'
                              AND  o.order_status  IN ('{inList}')
                              AND  (@ignoreId IS NULL OR o.id != @ignoreId)
                        ) THEN 'Occupied' ELSE 'Available' END AS Status
                    FROM   cafe_tables t
                    ORDER  BY t.table_number;";

                cmd.Parameters.AddWithValue("@ignoreId",
                    currentOrderId.HasValue ? (object)currentOrderId.Value : DBNull.Value);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                    result.Add(new TableOption
                    {
                        TableNumber = r.GetString(r.GetOrdinal("TableNumber")),
                        Status      = r.GetString(r.GetOrdinal("Status")),
                    });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[OrderService] GetTablesForPicker: {ex.Message}");
            }
            return result;
        }

        // ── READ ──────────────────────────────────────────────────────────────

        public List<OrderModel> GetAllOrders()
        {
            var orders = new List<OrderModel>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT id, customer_id, table_number, total_amount,
                           payment_status, order_status, cash_register_id,
                           ordered_by_user_id, created_at
                    FROM   orders
                    ORDER  BY created_at DESC;";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    orders.Add(MapOrder(reader));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[OrderService] GetAllOrders: {ex.Message}");
            }

            foreach (var order in orders)
                order.Items = GetOrderItems(order.Id);

            return orders;
        }

        public List<OrderItemModel> GetOrderItems(int orderId)
        {
            var items = new List<OrderItemModel>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT
                        oi.id, oi.order_id, oi.product_id,
                        oi.quantity, oi.unit_price,
                        m.Name AS product_name,
                        m.Category AS category
                    FROM   order_items oi
                    LEFT JOIN menu m ON m.Id = oi.product_id
                    WHERE  oi.order_id = @orderId;";
                cmd.Parameters.AddWithValue("@orderId", orderId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    items.Add(MapOrderItem(reader));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[OrderService] GetOrderItems: {ex.Message}");
            }
            return items;
        }

        // ── CREATE ────────────────────────────────────────────────────────────

        public int AddOrder(OrderModel order)
        {
            if (order.Items?.Count > 0)
                order.TotalAmount = order.Items.Sum(x => x.UnitPrice * x.Quantity);

            if (order.CreatedAt == default)
                order.CreatedAt = DateTime.Now;

            using var conn = SqliteConnectionFactory.CreateOpenConnection();
            using var tx   = conn.BeginTransaction();
            try
            {
                EnsureTableAvailable(conn, tx, order.TableNumber, ignoreOrderId: null);

                int newOrderId;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        INSERT INTO orders
                            (customer_id, table_number, total_amount, payment_status,
                             order_status, cash_register_id, ordered_by_user_id, created_at)
                        VALUES
                            (@customerId, @tableNumber, @totalAmount, @paymentStatus,
                             @orderStatus, @cashRegisterId, @orderedByUserId, @createdAt);
                        SELECT last_insert_rowid();";

                    BindOrderParams(cmd, order);
                    newOrderId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                if (order.Items != null)
                    foreach (var item in order.Items)
                        InsertOrderItem(conn, tx, newOrderId, item);

                if (order.PaymentStatus == PaymentStatus.Paid)
                    EnsureReceiptExists(conn, tx, newOrderId, order.TotalAmount);

                tx.Commit();
                return newOrderId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // ── UPDATE ────────────────────────────────────────────────────────────

        public void UpdateOrder(OrderModel order)
        {
            if (order.Items?.Count > 0)
                order.TotalAmount = order.Items.Sum(x => x.UnitPrice * x.Quantity);

            using var conn = SqliteConnectionFactory.CreateOpenConnection();
            using var tx   = conn.BeginTransaction();
            try
            {
                // Capture previous payment status before overwriting
                string prevPayment = "Unpaid";
                using (var getPrev = conn.CreateCommand())
                {
                    getPrev.Transaction  = tx;
                    getPrev.CommandText  = "SELECT payment_status FROM orders WHERE id=@id LIMIT 1;";
                    getPrev.Parameters.AddWithValue("@id", order.Id);
                    var obj = getPrev.ExecuteScalar();
                    if (obj != null && obj != DBNull.Value)
                        prevPayment = obj.ToString() ?? "Unpaid";
                }

                EnsureTableAvailable(conn, tx, order.TableNumber, ignoreOrderId: order.Id);

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        UPDATE orders SET
                            customer_id        = @customerId,
                            table_number       = @tableNumber,
                            total_amount       = @totalAmount,
                            payment_status     = @paymentStatus,
                            order_status       = @orderStatus,
                            cash_register_id   = @cashRegisterId,
                            ordered_by_user_id = @orderedByUserId
                        WHERE id = @id;";

                    BindOrderParams(cmd, order);
                    cmd.Parameters.AddWithValue("@id", order.Id);
                    cmd.ExecuteNonQuery();
                }

                // Replace items
                using (var del = conn.CreateCommand())
                {
                    del.Transaction = tx;
                    del.CommandText = "DELETE FROM order_items WHERE order_id=@id;";
                    del.Parameters.AddWithValue("@id", order.Id);
                    del.ExecuteNonQuery();
                }

                if (order.Items != null)
                    foreach (var item in order.Items)
                        InsertOrderItem(conn, tx, order.Id, item);

                // Receipt sync
                bool wasPaid = prevPayment.Equals("Paid", StringComparison.OrdinalIgnoreCase);
                bool nowPaid = order.PaymentStatus == PaymentStatus.Paid;

                if (!wasPaid && nowPaid)
                    EnsureReceiptExists(conn, tx, order.Id, order.TotalAmount);

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // ── DELETE ────────────────────────────────────────────────────────────

        public void DeleteOrder(int orderId)
        {
            using var conn = SqliteConnectionFactory.CreateOpenConnection();
            using var tx   = conn.BeginTransaction();
            try
            {
                // order_items and receipts cascade via FK ON DELETE CASCADE
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM orders WHERE id=@id;";
                cmd.Parameters.AddWithValue("@id", orderId);
                cmd.ExecuteNonQuery();

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // ── MENU PRODUCTS (for POS dropdown) ─────────────────────────────────

        /// <summary>
        /// StoreMode  → reads from <c>inventory_items</c> (barcode + price included).
        /// Restaurant → reads from <c>menu</c>.
        /// </summary>
        public List<MenuModel> GetAllMenu()
        {
            var products = new List<MenuModel>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();

                bool isStoreMode = SettingsService.Instance.CurrentMode == SystemMode.StoreMode;

                if (isStoreMode)
                {
                    cmd.CommandText = @"
                        SELECT Id          AS id,
                               Barcode     AS barcode,
                               ProductName AS name,
                               CategoryName AS category,
                               Price       AS price,
                               ImagePath   AS image_url
                        FROM   inventory_items
                        ORDER  BY ProductName;";
                }
                else
                {
                    cmd.CommandText = @"
                        SELECT Id, NULL AS barcode, Name AS name,
                               Category AS category, Price AS price,
                               image_url
                        FROM   menu
                        ORDER  BY Name;";
                }

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    products.Add(new MenuModel
                    {
                        Id       = reader.GetInt32(reader.GetOrdinal("id")),
                        Barcode  = reader.IsDBNull(reader.GetOrdinal("barcode"))   ? null : reader.GetString(reader.GetOrdinal("barcode")),
                        Name     = reader.IsDBNull(reader.GetOrdinal("name"))      ? string.Empty : reader.GetString(reader.GetOrdinal("name")),
                        Category = reader.IsDBNull(reader.GetOrdinal("category"))  ? string.Empty : reader.GetString(reader.GetOrdinal("category")),
                        Price    = reader.IsDBNull(reader.GetOrdinal("price"))     ? 0m : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("price"))),
                        ImageUrl = reader.IsDBNull(reader.GetOrdinal("image_url")) ? null : reader.GetString(reader.GetOrdinal("image_url")),
                    });
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[OrderService] GetAllMenu: {ex.Message}");
            }
            return products;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static OrderModel MapOrder(SqliteDataReader r)
        {
            var payStr = r.IsDBNull(r.GetOrdinal("payment_status")) ? null : r.GetString(r.GetOrdinal("payment_status"));
            var ordStr = r.IsDBNull(r.GetOrdinal("order_status"))   ? null : r.GetString(r.GetOrdinal("order_status"));
            var createdRaw = r.IsDBNull(r.GetOrdinal("created_at"))
                ? DateTime.Now
                : DateTime.TryParse(r.GetString(r.GetOrdinal("created_at")), out var d) ? d : DateTime.Now;

            return new OrderModel
            {
                Id              = r.GetInt32(r.GetOrdinal("id")),
                CustomerId      = r.IsDBNull(r.GetOrdinal("customer_id"))         ? null : r.GetInt32(r.GetOrdinal("customer_id")),
                TableNumber     = r.IsDBNull(r.GetOrdinal("table_number"))        ? null : r.GetString(r.GetOrdinal("table_number")),
                TotalAmount     = r.IsDBNull(r.GetOrdinal("total_amount"))        ? 0m   : Convert.ToDecimal(r.GetValue(r.GetOrdinal("total_amount"))),
                PaymentStatus   = Enum.TryParse(payStr ?? "", true, out PaymentStatus ps) ? ps : PaymentStatus.Unpaid,
                OrderStatus     = Enum.TryParse(ordStr ?? "", true, out OrderStatus os)   ? os : OrderStatus.Pending,
                CashRegisterId  = r.IsDBNull(r.GetOrdinal("cash_register_id"))    ? null : r.GetInt32(r.GetOrdinal("cash_register_id")),
                OrderedByUserId = r.IsDBNull(r.GetOrdinal("ordered_by_user_id")) ? null : r.GetInt32(r.GetOrdinal("ordered_by_user_id")),
                CreatedAt       = createdRaw,
                Items           = new List<OrderItemModel>(),
            };
        }

        private static OrderItemModel MapOrderItem(SqliteDataReader r) => new OrderItemModel
        {
            Id          = r.GetInt32(r.GetOrdinal("id")),
            OrderId     = r.GetInt32(r.GetOrdinal("order_id")),
            ProductId   = r.GetInt32(r.GetOrdinal("product_id")),
            Quantity    = r.GetInt32(r.GetOrdinal("quantity")),
            UnitPrice   = r.IsDBNull(r.GetOrdinal("unit_price"))    ? 0m  : Convert.ToDecimal(r.GetValue(r.GetOrdinal("unit_price"))),
            ProductName = r.IsDBNull(r.GetOrdinal("product_name"))  ? null : r.GetString(r.GetOrdinal("product_name")),
            Category    = r.IsDBNull(r.GetOrdinal("category"))      ? null : r.GetString(r.GetOrdinal("category")),
        };

        private static void BindOrderParams(SqliteCommand cmd, OrderModel order)
        {
            cmd.Parameters.AddWithValue("@customerId",       (object?)order.CustomerId      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tableNumber",      (object?)order.TableNumber     ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@totalAmount",      order.TotalAmount);
            cmd.Parameters.AddWithValue("@paymentStatus",    order.PaymentStatus.ToString());
            cmd.Parameters.AddWithValue("@orderStatus",      order.OrderStatus.ToString());
            cmd.Parameters.AddWithValue("@cashRegisterId",   (object?)order.CashRegisterId  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@orderedByUserId",  (object?)order.OrderedByUserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@createdAt",        order.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        private static void InsertOrderItem(SqliteConnection conn, SqliteTransaction tx, int orderId, OrderItemModel item)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO order_items (order_id, product_id, quantity, unit_price)
                VALUES (@orderId, @productId, @quantity, @unitPrice);";
            cmd.Parameters.AddWithValue("@orderId",   orderId);
            cmd.Parameters.AddWithValue("@productId", item.ProductId);
            cmd.Parameters.AddWithValue("@quantity",  item.Quantity);
            cmd.Parameters.AddWithValue("@unitPrice", item.UnitPrice);
            cmd.ExecuteNonQuery();
        }

        private void EnsureTableAvailable(SqliteConnection conn, SqliteTransaction tx, string? tableNumber, int? ignoreOrderId)
        {
            if (string.IsNullOrWhiteSpace(tableNumber)) return;
            if (IsTableOccupied(conn, tx, tableNumber!, ignoreOrderId))
                throw new InvalidOperationException($"Table {tableNumber} is currently occupied.");
        }

        private bool IsTableOccupied(SqliteConnection conn, SqliteTransaction tx, string tableNumber, int? ignoreOrderId)
        {
            var inList = string.Join("','", _openStatuses);
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = $@"
                SELECT COUNT(*) FROM orders o
                WHERE  o.table_number  = @tn
                  AND  o.payment_status = 'Unpaid'
                  AND  o.order_status  IN ('{inList}')
                  AND  (@ignoreId IS NULL OR o.id != @ignoreId);";
            cmd.Parameters.AddWithValue("@tn",       tableNumber);
            cmd.Parameters.AddWithValue("@ignoreId", ignoreOrderId.HasValue ? (object)ignoreOrderId.Value : DBNull.Value);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static void EnsureReceiptExists(SqliteConnection conn, SqliteTransaction tx, int orderId, decimal amount)
        {
            using var check = conn.CreateCommand();
            check.Transaction = tx;
            check.CommandText = "SELECT id FROM receipts WHERE order_id=@oid LIMIT 1;";
            check.Parameters.AddWithValue("@oid", orderId);
            if (check.ExecuteScalar() != null) return;

            using var ins = conn.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = @"
                INSERT INTO receipts (order_id, amount_paid, issued_at)
                VALUES (@oid, @amt, datetime('now'));";
            ins.Parameters.AddWithValue("@oid", orderId);
            ins.Parameters.AddWithValue("@amt", amount);
            ins.ExecuteNonQuery();
        }
    }
}
