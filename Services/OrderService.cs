using SihyuPOSPayroll.Models;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SihyuPOSPayroll.Services
{
    public class OrderService
    {
        private readonly string _connectionString =
            "server=localhost;user=root;password=;database=sihyu_pos;";

        // ---------------- Table dropdown model ----------------
        public sealed class TableOption
        {
            public string TableNumber { get; set; } = string.Empty;   // "T01"
            public string Status { get; set; } = "Available";          // "Available" | "Occupied"
            public bool Selectable => Status == "Available";           // disable occupied in UI
        }

        // For the status check
        private static readonly string[] _openStatuses = new[] { "Pending", "Preparing", "Served" };

        // ---------------- TABLES for the picker ----------------
        /// <summary>
        /// Returns all cafe tables with derived status. If currentOrderId is provided,
        /// occupancy by that order is ignored so its current table won�t appear �blocked�.
        /// </summary>
        public List<TableOption> GetTablesForPicker(int? currentOrderId = null)
        {
            var result = new List<TableOption>();

            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            var sql = $@"
                SELECT
                    t.table_number AS TableNumber,
                    CASE WHEN EXISTS (
                        SELECT 1 FROM orders o
                        WHERE o.table_number = t.table_number
                          AND o.payment_status = 'Unpaid'
                          AND o.order_status IN ('{string.Join("','", _openStatuses)}')
                          AND (@ignoreId IS NULL OR o.id <> @ignoreId)
                    )
                    THEN 'Occupied' ELSE 'Available' END AS Status
                FROM cafe_tables t
                ORDER BY t.table_number;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ignoreId", (object?)currentOrderId ?? DBNull.Value);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                result.Add(new TableOption
                {
                    TableNumber = r.GetString("TableNumber"),
                    Status = r.GetString("Status")
                });
            }

            return result;
        }

        // ---------------- READ (Orders + Items) ----------------
        public List<OrderModel> GetAllOrders()
        {
            var orders = new List<OrderModel>();

            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();

                const string query = @"
                    SELECT 
                        o.id,
                        o.customer_id,
                        o.table_number,
                        o.total_amount,
                        o.payment_status,
                        o.created_at,
                        o.cash_register_id,
                        o.order_status,
                        o.ordered_by_user_id
                    FROM orders o
                    ORDER BY o.created_at DESC";

                using (var cmd = new MySqlCommand(query, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string? paymentStatusStr = reader.IsDBNull(reader.GetOrdinal("payment_status"))
                            ? null : reader.GetString("payment_status");

                        string? orderStatusStr = reader.IsDBNull(reader.GetOrdinal("order_status"))
                            ? null : reader.GetString("order_status");

                        decimal total = reader.IsDBNull(reader.GetOrdinal("total_amount"))
                            ? 0m : reader.GetDecimal("total_amount");

                        var model = new OrderModel
                        {
                            Id = reader.GetInt32("id"),
                            CustomerId = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                ? null : reader.GetInt32("customer_id"),
                            TableNumber = reader.IsDBNull(reader.GetOrdinal("table_number"))
                                ? null : reader.GetString("table_number"),
                            TotalAmount = total,
                            PaymentStatus = Enum.TryParse(paymentStatusStr ?? "", true, out PaymentStatus ps)
                                ? ps : PaymentStatus.Unpaid,
                            CreatedAt = reader.GetDateTime("created_at"),
                            CashRegisterId = reader.IsDBNull(reader.GetOrdinal("cash_register_id"))
                                ? null : reader.GetInt32("cash_register_id"),
                            OrderStatus = Enum.TryParse(orderStatusStr ?? "", true, out OrderStatus os)
                                ? os : OrderStatus.Pending,
                            OrderedByUserId = reader.IsDBNull(reader.GetOrdinal("ordered_by_user_id"))
                                ? null : reader.GetInt32("ordered_by_user_id"),
                            Items = new List<OrderItemModel>()
                        };

                        orders.Add(model);
                    }
                }
            }

            foreach (var order in orders)
                order.Items = GetOrderItems(order.Id);

            return orders;
        }

        public List<OrderItemModel> GetOrderItems(int orderId)
        {
            var items = new List<OrderItemModel>();

            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            const string query = @"
                SELECT 
                    oi.id,
                    oi.order_id,
                    oi.product_id,
                    oi.quantity,
                    oi.unit_price,
                    m.name AS product_name,
                    m.category
                FROM order_items oi
                INNER JOIN menu m ON oi.product_id = m.id
                WHERE oi.order_id = @orderId";

            using var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@orderId", orderId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                items.Add(new OrderItemModel
                {
                    Id = reader.GetInt32("id"),
                    OrderId = reader.GetInt32("order_id"),
                    ProductId = reader.GetInt32("product_id"),
                    Quantity = reader.GetInt32("quantity"),
                    UnitPrice = reader.IsDBNull(reader.GetOrdinal("unit_price"))
                        ? 0m : reader.GetDecimal("unit_price"),
                    ProductName = reader.IsDBNull(reader.GetOrdinal("product_name"))
                        ? null : reader.GetString("product_name"),
                    Category = reader.IsDBNull(reader.GetOrdinal("category"))
                        ? null : reader.GetString("category")
                });
            }

            return items;
        }

        // ---------------- CREATE ----------------
        public int AddOrder(OrderModel order)
        {
            if (order.Items != null && order.Items.Count > 0)
                order.TotalAmount = order.Items.Sum(x => x.UnitPrice * x.Quantity);

            if (order.CreatedAt == default)
                order.CreatedAt = DateTime.Now;

            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                // Guard: table must be selected and available
                EnsureTableAvailable(connection, transaction, order.TableNumber, ignoreOrderId: null);

                const string insertOrder = @"
                    INSERT INTO orders 
                        (customer_id, table_number, total_amount, payment_status, created_at, cash_register_id, order_status, ordered_by_user_id)
                    VALUES
                        (@customerId, @tableNumber, @totalAmount, @paymentStatus, @createdAt, @cashRegisterId, @orderStatus, @orderedByUserId);
                    SELECT LAST_INSERT_ID();";

                int newOrderId;
                using (var cmd = new MySqlCommand(insertOrder, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@customerId", (object?)order.CustomerId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@tableNumber", (object?)order.TableNumber ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@totalAmount", order.TotalAmount);
                    cmd.Parameters.AddWithValue("@paymentStatus", order.PaymentStatus.ToString());
                    cmd.Parameters.AddWithValue("@createdAt", order.CreatedAt);
                    cmd.Parameters.AddWithValue("@cashRegisterId", (object?)order.CashRegisterId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@orderStatus", order.OrderStatus.ToString());
                    cmd.Parameters.AddWithValue("@orderedByUserId", (object?)order.OrderedByUserId ?? DBNull.Value);

                    newOrderId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                if (order.Items != null)
                    foreach (var item in order.Items)
                        AddOrderItem(connection, transaction, newOrderId, item);

                // If the order starts as Paid, ensure a receipt exists
                if (order.PaymentStatus == PaymentStatus.Paid)
                {
                    EnsureReceiptExists(connection, transaction, newOrderId, order.TotalAmount);
                }

                transaction.Commit();
                return newOrderId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private void AddOrderItem(MySqlConnection connection, MySqlTransaction tx, int orderId, OrderItemModel item)
        {
            const string insertItem = @"
                INSERT INTO order_items (order_id, product_id, quantity, unit_price)
                VALUES (@orderId, @productId, @quantity, @unitPrice)";

            using var cmd = new MySqlCommand(insertItem, connection, tx);
            cmd.Parameters.AddWithValue("@orderId", orderId);
            cmd.Parameters.AddWithValue("@productId", item.ProductId);
            cmd.Parameters.AddWithValue("@quantity", item.Quantity);
            cmd.Parameters.AddWithValue("@unitPrice", item.UnitPrice);
            cmd.ExecuteNonQuery();
        }

        // ---------------- UPDATE ----------------
        public void UpdateOrder(OrderModel order)
        {
            if (order.Items != null && order.Items.Count > 0)
                order.TotalAmount = order.Items.Sum(x => x.UnitPrice * x.Quantity);

            using var connection = new MySqlConnection(_connectionString);
            connection.Open();
            using var tx = connection.BeginTransaction();
            try
            {
                // Fetch previous payment status to detect transition (Paid/Unpaid)
                string prevPaymentStatus = "Unpaid";
                using (var getPrev = new MySqlCommand(
                    "SELECT payment_status FROM orders WHERE id=@id LIMIT 1;", connection, tx))
                {
                    getPrev.Parameters.AddWithValue("@id", order.Id);
                    var obj = getPrev.ExecuteScalar();
                    if (obj != null && obj != DBNull.Value)
                        prevPaymentStatus = Convert.ToString(obj) ?? "Unpaid";
                }

                // Guard: table must be available; ignore current order�s own lock
                EnsureTableAvailable(connection, tx, order.TableNumber, ignoreOrderId: order.Id);

                const string updateOrder = @"
                    UPDATE orders SET
                        customer_id=@customerId,
                        table_number=@tableNumber,
                        total_amount=@totalAmount,
                        payment_status=@paymentStatus,
                        cash_register_id=@cashRegisterId,
                        order_status=@orderStatus,
                        ordered_by_user_id=@orderedByUserId
                    WHERE id=@id";

                using (var cmd = new MySqlCommand(updateOrder, connection, tx))
                {
                    cmd.Parameters.AddWithValue("@id", order.Id);
                    cmd.Parameters.AddWithValue("@customerId", (object?)order.CustomerId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@tableNumber", (object?)order.TableNumber ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@totalAmount", order.TotalAmount);
                    cmd.Parameters.AddWithValue("@paymentStatus", order.PaymentStatus.ToString());
                    cmd.Parameters.AddWithValue("@cashRegisterId", (object?)order.CashRegisterId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@orderStatus", order.OrderStatus.ToString());
                    cmd.Parameters.AddWithValue("@orderedByUserId", (object?)order.OrderedByUserId ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }

                // Replace all items (simple approach)
                using (var cmdDel = new MySqlCommand("DELETE FROM order_items WHERE order_id=@id", connection, tx))
                {
                    cmdDel.Parameters.AddWithValue("@id", order.Id);
                    cmdDel.ExecuteNonQuery();
                }

                if (order.Items != null)
                    foreach (var item in order.Items)
                        AddOrderItem(connection, tx, order.Id, item);

                // ===== Receipt handling (atomic with the update) =====
                bool wasPaid = prevPaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase);
                bool nowPaid = order.PaymentStatus == PaymentStatus.Paid;

                if (!wasPaid && nowPaid)
                {
                    // Transitioned to Paid ? ensure receipt exists
                    EnsureReceiptExists(connection, tx, order.Id, order.TotalAmount);
                }
                else if (wasPaid && !nowPaid)
                {
                    // Transitioned to Unpaid ? decide policy
                    // Uncomment to remove the receipt when reverting to Unpaid:
                    // RemoveReceiptIfAny(connection, tx, order.Id);
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // ---------------- DELETE ----------------
        public void DeleteOrder(int orderId)
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();
            using var tx = connection.BeginTransaction();
            try
            {
                using (var cmd = new MySqlCommand("DELETE FROM order_items WHERE order_id=@id", connection, tx))
                {
                    cmd.Parameters.AddWithValue("@id", orderId);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new MySqlCommand("DELETE FROM orders WHERE id=@id", connection, tx))
                {
                    cmd.Parameters.AddWithValue("@id", orderId);
                    cmd.ExecuteNonQuery();
                }

                // Optional: also remove receipts for that order if desired
                // using (var cmd = new MySqlCommand("DELETE FROM receipts WHERE order_id=@id", connection, tx))
                // {
                //     cmd.Parameters.AddWithValue("@id", orderId);
                //     cmd.ExecuteNonQuery();
                // }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // ---------------- MENU PRODUCTS (for dropdown) ----------------
        public List<MenuModel> GetAllMenu()
        {
            var products = new List<MenuModel>();

            using var connection = new MySqlConnection(_connectionString);
            connection.Open();
            const string query = "SELECT id, name, category, price FROM menu ORDER BY name";

            using var cmd = new MySqlCommand(query, connection);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                products.Add(new MenuModel
                {
                    Id = reader.GetInt32("id"),
                    Name = reader.IsDBNull(reader.GetOrdinal("name")) ? string.Empty : reader.GetString("name"),
                    Category = reader.IsDBNull(reader.GetOrdinal("category")) ? string.Empty : reader.GetString("category"),
                    Price = reader.IsDBNull(reader.GetOrdinal("price")) ? (decimal?)null : reader.GetDecimal("price")
                });
            }

            return products;
        }

        // ---------------- Availability helpers ----------------
        /// <summary>
        /// Validates table availability. Table is OPTIONAL in two scenarios:
        ///   1. StoreMode — no tables concept at all.
        ///   2. RestaurantMode TakeOut — customer takes food away, no table needed.
        /// If tableNumber is supplied, it must not be occupied by another open order.
        /// </summary>
        private void EnsureTableAvailable(MySqlConnection conn, MySqlTransaction? tx, string? tableNumber, int? ignoreOrderId)
        {
            // Table is optional — skip the null guard entirely.
            // Only validate occupancy when a table number was actually provided.
            if (string.IsNullOrWhiteSpace(tableNumber))
                return;

            if (IsTableOccupied(conn, tx, tableNumber!, ignoreOrderId))
                throw new InvalidOperationException($"Table {tableNumber} is currently occupied.");
        }

        private bool IsTableOccupied(MySqlConnection conn, MySqlTransaction? tx, string tableNumber, int? ignoreOrderId)
        {
            var sql = $@"
                SELECT EXISTS(
                    SELECT 1 FROM orders o
                    WHERE o.table_number = @tn
                      AND o.payment_status = 'Unpaid'
                      AND o.order_status IN ('{string.Join("','", _openStatuses)}')
                      AND (@ignoreId IS NULL OR o.id <> @ignoreId)
                )";

            using var cmd = new MySqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@tn", tableNumber);
            cmd.Parameters.AddWithValue("@ignoreId", (object?)ignoreOrderId ?? DBNull.Value);

            var obj = cmd.ExecuteScalar();
            return Convert.ToInt32(obj) == 1;
        }

        // ---------------- Receipts helpers (transactional) ----------------
        private void EnsureReceiptExists(MySqlConnection conn, MySqlTransaction tx, int orderId, decimal amount)
        {
            // Check if a receipt already exists
            using (var check = new MySqlCommand("SELECT id FROM receipts WHERE order_id=@oid LIMIT 1;", conn, tx))
            {
                check.Parameters.AddWithValue("@oid", orderId);
                var existing = check.ExecuteScalar();
                if (existing != null && existing != DBNull.Value) return;
            }

            // Insert a new receipt
            using (var ins = new MySqlCommand(
                "INSERT INTO receipts (order_id, amount_paid, issued_at) VALUES (@oid, @amt, NOW());", conn, tx))
            {
                ins.Parameters.AddWithValue("@oid", orderId);
                ins.Parameters.AddWithValue("@amt", amount);
                ins.ExecuteNonQuery();
            }
        }

        private void RemoveReceiptIfAny(MySqlConnection conn, MySqlTransaction tx, int orderId)
        {
            using var del = new MySqlCommand("DELETE FROM receipts WHERE order_id=@id;", conn, tx);
            del.Parameters.AddWithValue("@id", orderId);
            del.ExecuteNonQuery();
        }
    }
}
