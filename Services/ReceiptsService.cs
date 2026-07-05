using System;
using System.Collections.Generic;
using System.Data;
using SihyuPOSPayroll.Helpers;
using SihyuPOSPayroll.Models;
using MySql.Data.MySqlClient;

namespace SihyuPOSPayroll.Services
{
    public static class ReceiptsServices
    {
        private static string GetConnectionString() => ConfigurationHelper.GetConnectionString();

        // ======================================================
        // BASIC CRUD + SEARCH
        // ======================================================

        /// <summary>
        /// Get all receipts, optional search & limit.
        /// Search applies to OrderId, TableNumber (string-like), and Date(MM/dd/yy).
        /// </summary>
        public static List<ReceiptsModel> GetAllReceipts(string? searchTerm = null, int? limit = null)
        {
            var receipts = new List<ReceiptsModel>();

            using var conn = new MySqlConnection(GetConnectionString());
            conn.Open();

            var sql = @"
                SELECT 
                    r.id                                  AS ReceiptId, 
                    r.order_id                            AS OrderId, 
                    /* may be varchar or int in DB; force as text for searching */
                    COALESCE(o.table_number, '')          AS TableNumber,
                    DATE_FORMAT(r.issued_at, '%m/%d/%y')  AS Date, 
                    r.amount_paid                         AS Amount
                FROM receipts r
                LEFT JOIN orders o ON r.order_id = o.id";

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                sql += @"
                WHERE CAST(r.order_id AS CHAR) LIKE @t
                   OR COALESCE(o.table_number,'') LIKE @t
                   OR DATE_FORMAT(r.issued_at, '%m/%d/%y') LIKE @t";
            }

            sql += " ORDER BY r.issued_at DESC";
            if (limit.HasValue && limit > 0) sql += " LIMIT @limit;";

            using var cmd = new MySqlCommand(sql, conn);
            if (!string.IsNullOrWhiteSpace(searchTerm))
                cmd.Parameters.AddWithValue("@t", $"%{searchTerm}%");
            if (limit.HasValue && limit > 0)
                cmd.Parameters.AddWithValue("@limit", limit.Value);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                receipts.Add(Map(reader));
            }

            return receipts;
        }

        /// <summary>
        /// Get one receipt by receipt id.
        /// </summary>
        public static ReceiptsModel? GetById(int id)
        {
            using var conn = new MySqlConnection(GetConnectionString());
            conn.Open();

            var sql = @"
                SELECT 
                    r.id                                  AS ReceiptId, 
                    r.order_id                            AS OrderId, 
                    COALESCE(o.table_number, '')          AS TableNumber, 
                    DATE_FORMAT(r.issued_at, '%m/%d/%y')  AS Date, 
                    r.amount_paid                         AS Amount
                FROM receipts r
                LEFT JOIN orders o ON r.order_id = o.id
                WHERE r.id = @id
                LIMIT 1;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        /// <summary>
        /// Get one receipt by order id (convenience).
        /// </summary>
        public static ReceiptsModel? GetByOrderId(int orderId)
        {
            using var conn = new MySqlConnection(GetConnectionString());
            conn.Open();

            var sql = @"
                SELECT 
                    r.id                                  AS ReceiptId, 
                    r.order_id                            AS OrderId, 
                    COALESCE(o.table_number, '')          AS TableNumber, 
                    DATE_FORMAT(r.issued_at, '%m/%d/%y')  AS Date, 
                    r.amount_paid                         AS Amount
                FROM receipts r
                LEFT JOIN orders o ON r.order_id = o.id
                WHERE r.order_id = @oid
                LIMIT 1;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@oid", orderId);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        /// <summary>
        /// Create a receipt (issued_at = NOW()).
        /// </summary>
        public static int Create(ReceiptsModel model)
        {
            using var conn = new MySqlConnection(GetConnectionString());
            conn.Open();

            const string sql = @"
                INSERT INTO receipts (order_id, amount_paid, issued_at)
                VALUES (@orderId, @amount, NOW());
                SELECT LAST_INSERT_ID();";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@orderId", model.OrderId);
            cmd.Parameters.AddWithValue("@amount", model.Amount);

            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        /// <summary>
        /// Update receipt fields.
        /// </summary>
        public static void Update(ReceiptsModel model)
        {
            using var conn = new MySqlConnection(GetConnectionString());
            conn.Open();

            const string sql = @"
                UPDATE receipts
                SET order_id = @orderId,
                    amount_paid = @amount
                WHERE id = @id;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@orderId", model.OrderId);
            cmd.Parameters.AddWithValue("@amount", model.Amount);
            cmd.Parameters.AddWithValue("@id", model.ReceiptId);

            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Delete by receipt id.
        /// </summary>
        public static void Delete(int id)
        {
            using var conn = new MySqlConnection(GetConnectionString());
            conn.Open();

            const string sql = "DELETE FROM receipts WHERE id = @id;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Delete by order id (convenience).
        /// </summary>
        public static void DeleteByOrderId(int orderId)
        {
            using var conn = new MySqlConnection(GetConnectionString());
            conn.Open();

            const string sql = "DELETE FROM receipts WHERE order_id = @oid;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@oid", orderId);
            cmd.ExecuteNonQuery();
        }

        // ======================================================
        // AUTO-SYNC HELPERS (PAID orders => receipts)
        // ======================================================

        /// <summary>
        /// Ensure a receipt exists for an order IF the order is Paid.
        /// Returns receipt id, or null if order is not Paid.
        /// </summary>
        public static int? EnsureForPaidOrder(int orderId)
        {
            using var conn = new MySqlConnection(GetConnectionString());
            conn.Open();

            // 1) Check order payment status + total
            string? paymentStatus = null;
            decimal total = 0m;
            using (var cmd = new MySqlCommand(
                "SELECT payment_status, COALESCE(total_amount,0) AS total_amount FROM orders WHERE id=@id", conn))
            {
                cmd.Parameters.AddWithValue("@id", orderId);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    paymentStatus = r.IsDBNull(r.GetOrdinal("payment_status")) ? null : r.GetString("payment_status");
                    total = r.GetDecimal(r.GetOrdinal("total_amount"));
                }
            }

            if (!string.Equals(paymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
                return null; // not paid -> no receipt

            // 2) If receipt exists, return it (and sync amount)
            using (var check = new MySqlCommand("SELECT id FROM receipts WHERE order_id=@oid LIMIT 1", conn))
            {
                check.Parameters.AddWithValue("@oid", orderId);
                var existing = check.ExecuteScalar();
                if (existing != null && existing != DBNull.Value)
                {
                    using var upd = new MySqlCommand("UPDATE receipts SET amount_paid=@amt WHERE id=@rid", conn);
                    upd.Parameters.AddWithValue("@amt", total);
                    upd.Parameters.AddWithValue("@rid", Convert.ToInt32(existing));
                    upd.ExecuteNonQuery();
                    return Convert.ToInt32(existing);
                }
            }

            // 3) Create receipt now
            using (var ins = new MySqlCommand(@"
                INSERT INTO receipts (order_id, amount_paid, issued_at)
                VALUES (@oid, @amt, NOW());
                SELECT LAST_INSERT_ID();", conn))
            {
                ins.Parameters.AddWithValue("@oid", orderId);
                ins.Parameters.AddWithValue("@amt", total);
                return Convert.ToInt32(ins.ExecuteScalar());
            }
        }

        /// <summary>
        /// Sync one order: Paid -> ensure receipt; else -> delete any receipt.
        /// </summary>
        public static void SyncForOrder(int orderId)
        {
            using var conn = new MySqlConnection(GetConnectionString());
            conn.Open();

            string? paymentStatus = null;
            using (var cmd = new MySqlCommand("SELECT payment_status FROM orders WHERE id=@id", conn))
            {
                cmd.Parameters.AddWithValue("@id", orderId);
                var val = cmd.ExecuteScalar();
                paymentStatus = val == null || val == DBNull.Value ? null : Convert.ToString(val);
            }

            if (string.Equals(paymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                EnsureForPaidOrder(orderId);
            }
            else
            {
                DeleteByOrderId(orderId);
            }
        }

        /// <summary>
        /// Bulk ensure receipts for any PAID orders lacking one.
        /// </summary>
        public static int EnsureAllForPaidOrders()
        {
            using var conn = new MySqlConnection(GetConnectionString());
            conn.Open();

            const string sql = @"
                INSERT INTO receipts (order_id, amount_paid, issued_at)
                SELECT o.id, COALESCE(o.total_amount,0), NOW()
                FROM orders o
                LEFT JOIN receipts r ON r.order_id = o.id
                WHERE o.payment_status = 'Paid' AND r.id IS NULL;";

            using var cmd = new MySqlCommand(sql, conn);
            return cmd.ExecuteNonQuery();
        }

        // ======================================================
        // RECEIPT DETAILS (header + lines) for print/export
        // ======================================================

        public static ReceiptDetailsModel? GetDetailsByReceiptId(int receiptId)
        {
            using var conn = new MySqlConnection(GetConnectionString());
            conn.Open();

            // 1) Header (table_number may be string in DB; we’ll parse to int)
            const string headerSql = @"
                SELECT 
                    r.id                                  AS ReceiptId,
                    r.order_id                            AS OrderId,
                    COALESCE(o.table_number, '')          AS TableNumber,
                    DATE_FORMAT(r.issued_at, '%m/%d/%y')  AS Date,
                    r.amount_paid                         AS Amount
                FROM receipts r
                LEFT JOIN orders o ON o.id = r.order_id
                WHERE r.id = @rid
                LIMIT 1;";

            using var cmdHeader = new MySqlCommand(headerSql, conn);
            cmdHeader.Parameters.AddWithValue("@rid", receiptId);

            ReceiptsModel? header = null;
            using (var rh = cmdHeader.ExecuteReader())
            {
                if (rh.Read())
                    header = Map(rh);
            }
            if (header == null) return null;

            var details = new ReceiptDetailsModel { Header = header };

            // 2) Lines
            const string linesSql = @"
                SELECT 
                    oi.product_id        AS ProductId,
                    m.name               AS ProductName,
                    oi.quantity          AS Quantity,
                    oi.unit_price        AS UnitPrice
                FROM order_items oi
                INNER JOIN orders o ON o.id = oi.order_id
                LEFT JOIN menu m    ON m.id = oi.product_id
                WHERE o.id = @orderId;";

            using var cmdLines = new MySqlCommand(linesSql, conn);
            cmdLines.Parameters.AddWithValue("@orderId", header.OrderId);

            using var rl = cmdLines.ExecuteReader();
            while (rl.Read())
            {
                var productId = rl.GetInt32(rl.GetOrdinal("ProductId"));
                var productName = rl.IsDBNull(rl.GetOrdinal("ProductName")) ? null : rl.GetString(rl.GetOrdinal("ProductName"));
                var qty = rl.GetInt32(rl.GetOrdinal("Quantity"));
                var unit = rl.GetDecimal(rl.GetOrdinal("UnitPrice"));

                details.Lines.Add(new ReceiptLineModel
                {
                    ProductId = productId,
                    ProductName = productName,
                    Quantity = qty,
                    UnitPrice = unit
                });
            }

            return details;
        }

        // ======================================================
        // MAPPER
        // ======================================================

        private static ReceiptsModel Map(IDataRecord r) => new ReceiptsModel
        {
            ReceiptId = r.GetInt32(r.GetOrdinal("ReceiptId")),
            OrderId = r.GetInt32(r.GetOrdinal("OrderId")),
            // TableNumber in model is INT. DB may give us string or int. Handle both.
            TableNumber = ReadTableNumberAsInt(r, "TableNumber"),
            Date = r.IsDBNull(r.GetOrdinal("Date")) ? "" : r.GetString(r.GetOrdinal("Date")),
            Amount = r.IsDBNull(r.GetOrdinal("Amount")) ? 0m : r.GetDecimal(r.GetOrdinal("Amount"))
        };

        private static int ReadTableNumberAsInt(IDataRecord r, string col)
        {
            int ord = r.GetOrdinal(col);
            if (r.IsDBNull(ord)) return 0;

            var type = r.GetFieldType(ord);
            try
            {
                if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
                {
                    return Convert.ToInt32(r.GetValue(ord));
                }
                if (type == typeof(string))
                {
                    var s = r.GetString(ord);
                    return int.TryParse(s, out var n) ? n : 0;
                }
                // fallback for other numeric types
                return Convert.ToInt32(r.GetValue(ord));
            }
            catch
            {
                return 0;
            }
        }
    }
}

// -----------------------------------------------------------------
// Lightweight DTOs for full-receipt export (unchanged)
// -----------------------------------------------------------------
namespace SihyuPOSPayroll.Models
{
    public class ReceiptDetailsModel
    {
        public ReceiptsModel Header { get; set; } = new ReceiptsModel();
        public List<ReceiptLineModel> Lines { get; set; } = new List<ReceiptLineModel>();
        public decimal GrandTotal
        {
            get
            {
                decimal sum = 0m;
                foreach (var l in Lines) sum += l.Subtotal;
                return sum;
            }
        }
    }

    public class ReceiptLineModel
    {
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal => UnitPrice * Quantity;
    }
}
