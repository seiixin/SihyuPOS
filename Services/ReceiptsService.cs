#nullable enable
using Microsoft.Data.Sqlite;
using SihyuPOSPayroll.Data;
using SihyuPOSPayroll.Models;
using System;
using System.Collections.Generic;
using System.Data;

namespace SihyuPOSPayroll.Services
{
    /// <summary>
    /// CRUD + sync helpers for the <c>receipts</c> table (SQLite).
    ///
    /// MySQL-specific changes:
    ///   • DATE_FORMAT(issued_at, '%m/%d/%y') → strftime('%m/%d/%y', issued_at)
    ///   • CAST(x AS CHAR) → CAST(x AS TEXT)
    ///   • NOW() → datetime('now')
    ///   • LAST_INSERT_ID() → last_insert_rowid()
    ///   • Bulk INSERT … SELECT not changed — SQLite supports it.
    /// </summary>
    public static class ReceiptsServices
    {
        // ── Read ───────────────────────────────────────────────────────────────

        public static List<ReceiptsModel> GetAllReceipts(string? searchTerm = null, int? limit = null)
        {
            var receipts = new List<ReceiptsModel>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();

                var sql = @"
                    SELECT
                        r.id                                             AS ReceiptId,
                        r.order_id                                       AS OrderId,
                        COALESCE(o.table_number, '')                     AS TableNumber,
                        strftime('%m/%d/%y', r.issued_at)                AS Date,
                        r.amount_paid                                    AS Amount
                    FROM   receipts r
                    LEFT JOIN orders o ON r.order_id = o.id";

                if (!string.IsNullOrWhiteSpace(searchTerm))
                    sql += @"
                    WHERE  CAST(r.order_id AS TEXT) LIKE @t
                       OR  COALESCE(o.table_number,'') LIKE @t
                       OR  strftime('%m/%d/%y', r.issued_at) LIKE @t";

                sql += " ORDER BY r.issued_at DESC";
                if (limit.HasValue && limit > 0) sql += " LIMIT @limit";

                cmd.CommandText = sql;
                if (!string.IsNullOrWhiteSpace(searchTerm))
                    cmd.Parameters.AddWithValue("@t", $"%{searchTerm}%");
                if (limit.HasValue && limit > 0)
                    cmd.Parameters.AddWithValue("@limit", limit.Value);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    receipts.Add(MapReceipt(reader));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ReceiptsService] GetAllReceipts: {ex.Message}");
            }
            return receipts;
        }

        public static ReceiptsModel? GetById(int id)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT
                        r.id                                  AS ReceiptId,
                        r.order_id                            AS OrderId,
                        COALESCE(o.table_number, '')          AS TableNumber,
                        strftime('%m/%d/%y', r.issued_at)     AS Date,
                        r.amount_paid                         AS Amount
                    FROM   receipts r
                    LEFT JOIN orders o ON r.order_id = o.id
                    WHERE  r.id = @id
                    LIMIT  1;";
                cmd.Parameters.AddWithValue("@id", id);
                using var reader = cmd.ExecuteReader();
                return reader.Read() ? MapReceipt(reader) : null;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ReceiptsService] GetById: {ex.Message}");
                return null;
            }
        }

        public static ReceiptsModel? GetByOrderId(int orderId)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT
                        r.id                                  AS ReceiptId,
                        r.order_id                            AS OrderId,
                        COALESCE(o.table_number, '')          AS TableNumber,
                        strftime('%m/%d/%y', r.issued_at)     AS Date,
                        r.amount_paid                         AS Amount
                    FROM   receipts r
                    LEFT JOIN orders o ON r.order_id = o.id
                    WHERE  r.order_id = @oid
                    LIMIT  1;";
                cmd.Parameters.AddWithValue("@oid", orderId);
                using var reader = cmd.ExecuteReader();
                return reader.Read() ? MapReceipt(reader) : null;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ReceiptsService] GetByOrderId: {ex.Message}");
                return null;
            }
        }

        // ── Create ─────────────────────────────────────────────────────────────

        public static int Create(ReceiptsModel model)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO receipts (order_id, amount_paid, issued_at)
                    VALUES (@orderId, @amount, datetime('now'));
                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@orderId", model.OrderId);
                cmd.Parameters.AddWithValue("@amount",  model.Amount);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ReceiptsService] Create: {ex.Message}");
                return 0;
            }
        }

        // ── Update ─────────────────────────────────────────────────────────────

        public static void Update(ReceiptsModel model)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE receipts
                    SET    order_id    = @orderId,
                           amount_paid = @amount
                    WHERE  id = @id;";
                cmd.Parameters.AddWithValue("@orderId", model.OrderId);
                cmd.Parameters.AddWithValue("@amount",  model.Amount);
                cmd.Parameters.AddWithValue("@id",      model.ReceiptId);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ReceiptsService] Update: {ex.Message}");
            }
        }

        // ── Delete ─────────────────────────────────────────────────────────────

        public static void Delete(int id)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM receipts WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ReceiptsService] Delete: {ex.Message}");
            }
        }

        public static void DeleteByOrderId(int orderId)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM receipts WHERE order_id = @oid;";
                cmd.Parameters.AddWithValue("@oid", orderId);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ReceiptsService] DeleteByOrderId: {ex.Message}");
            }
        }

        // ── Sync helpers ───────────────────────────────────────────────────────

        public static int? EnsureForPaidOrder(int orderId)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();

                string? paymentStatus = null;
                decimal total = 0m;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT payment_status, COALESCE(total_amount,0) FROM orders WHERE id=@id;";
                    cmd.Parameters.AddWithValue("@id", orderId);
                    using var r = cmd.ExecuteReader();
                    if (r.Read())
                    {
                        paymentStatus = r.IsDBNull(0) ? null : r.GetString(0);
                        total = Convert.ToDecimal(r.GetValue(1));
                    }
                }

                if (!string.Equals(paymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
                    return null;

                // Upsert: update amount if receipt exists, insert if not
                using (var check = conn.CreateCommand())
                {
                    check.CommandText = "SELECT id FROM receipts WHERE order_id=@oid LIMIT 1;";
                    check.Parameters.AddWithValue("@oid", orderId);
                    var existing = check.ExecuteScalar();
                    if (existing != null && existing != DBNull.Value)
                    {
                        var rid = Convert.ToInt32(existing);
                        using var upd = conn.CreateCommand();
                        upd.CommandText = "UPDATE receipts SET amount_paid=@amt WHERE id=@rid;";
                        upd.Parameters.AddWithValue("@amt", total);
                        upd.Parameters.AddWithValue("@rid", rid);
                        upd.ExecuteNonQuery();
                        return rid;
                    }
                }

                using (var ins = conn.CreateCommand())
                {
                    ins.CommandText = @"
                        INSERT INTO receipts (order_id, amount_paid, issued_at)
                        VALUES (@oid, @amt, datetime('now'));
                        SELECT last_insert_rowid();";
                    ins.Parameters.AddWithValue("@oid", orderId);
                    ins.Parameters.AddWithValue("@amt", total);
                    return Convert.ToInt32(ins.ExecuteScalar());
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ReceiptsService] EnsureForPaidOrder: {ex.Message}");
                return null;
            }
        }

        public static void SyncForOrder(int orderId)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                string? paymentStatus = null;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT payment_status FROM orders WHERE id=@id;";
                    cmd.Parameters.AddWithValue("@id", orderId);
                    var val = cmd.ExecuteScalar();
                    paymentStatus = val == null || val == DBNull.Value ? null : val.ToString();
                }

                if (string.Equals(paymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
                    EnsureForPaidOrder(orderId);
                else
                    DeleteByOrderId(orderId);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ReceiptsService] SyncForOrder: {ex.Message}");
            }
        }

        public static int EnsureAllForPaidOrders()
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO receipts (order_id, amount_paid, issued_at)
                    SELECT o.id, COALESCE(o.total_amount, 0), datetime('now')
                    FROM   orders o
                    LEFT JOIN receipts r ON r.order_id = o.id
                    WHERE  o.payment_status = 'Paid'
                      AND  r.id IS NULL;";
                return cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ReceiptsService] EnsureAllForPaidOrders: {ex.Message}");
                return 0;
            }
        }

        // ── Receipt details (header + lines) ───────────────────────────────────

        public static ReceiptDetailsModel? GetDetailsByReceiptId(int receiptId)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();

                ReceiptsModel? header = null;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT
                            r.id                                  AS ReceiptId,
                            r.order_id                            AS OrderId,
                            COALESCE(o.table_number, '')          AS TableNumber,
                            strftime('%m/%d/%y', r.issued_at)     AS Date,
                            r.amount_paid                         AS Amount
                        FROM   receipts r
                        LEFT JOIN orders o ON o.id = r.order_id
                        WHERE  r.id = @rid
                        LIMIT  1;";
                    cmd.Parameters.AddWithValue("@rid", receiptId);
                    using var rh = cmd.ExecuteReader();
                    if (rh.Read()) header = MapReceipt(rh);
                }

                if (header == null) return null;

                var details = new ReceiptDetailsModel { Header = header };

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT
                            oi.product_id  AS ProductId,
                            m.Name         AS ProductName,
                            oi.quantity    AS Quantity,
                            oi.unit_price  AS UnitPrice
                        FROM   order_items oi
                        INNER JOIN orders o ON o.id = oi.order_id
                        LEFT  JOIN menu   m ON m.Id = oi.product_id
                        WHERE  o.id = @orderId;";
                    cmd.Parameters.AddWithValue("@orderId", header.OrderId);
                    using var rl = cmd.ExecuteReader();
                    while (rl.Read())
                    {
                        details.Lines.Add(new ReceiptLineModel
                        {
                            ProductId   = rl.GetInt32(rl.GetOrdinal("ProductId")),
                            ProductName = rl.IsDBNull(rl.GetOrdinal("ProductName")) ? null : rl.GetString(rl.GetOrdinal("ProductName")),
                            Quantity    = rl.GetInt32(rl.GetOrdinal("Quantity")),
                            UnitPrice   = Convert.ToDecimal(rl.GetValue(rl.GetOrdinal("UnitPrice"))),
                        });
                    }
                }

                return details;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ReceiptsService] GetDetailsByReceiptId: {ex.Message}");
                return null;
            }
        }

        // ── Mapper ─────────────────────────────────────────────────────────────

        private static ReceiptsModel MapReceipt(IDataRecord r) => new ReceiptsModel
        {
            ReceiptId   = r.GetInt32(r.GetOrdinal("ReceiptId")),
            OrderId     = r.GetInt32(r.GetOrdinal("OrderId")),
            TableNumber = ReadTableNumberAsInt(r, "TableNumber"),
            Date        = r.IsDBNull(r.GetOrdinal("Date")) ? string.Empty : r.GetString(r.GetOrdinal("Date")),
            Amount      = r.IsDBNull(r.GetOrdinal("Amount")) ? 0m : Convert.ToDecimal(r.GetValue(r.GetOrdinal("Amount"))),
        };

        private static int ReadTableNumberAsInt(IDataRecord r, string col)
        {
            int ord = r.GetOrdinal(col);
            if (r.IsDBNull(ord)) return 0;
            var s = r.GetString(ord);
            return int.TryParse(s, out var n) ? n : 0;
        }
    }
}

// ── Lightweight DTOs (unchanged) ──────────────────────────────────────────────
namespace SihyuPOSPayroll.Models
{
    public class ReceiptDetailsModel
    {
        public ReceiptsModel Header { get; set; } = new ReceiptsModel();
        public List<ReceiptLineModel> Lines { get; set; } = new List<ReceiptLineModel>();
        public decimal GrandTotal
        {
            get { decimal s = 0m; foreach (var l in Lines) s += l.Subtotal; return s; }
        }
    }

    public class ReceiptLineModel
    {
        public int      ProductId   { get; set; }
        public string?  ProductName { get; set; }
        public int      Quantity    { get; set; }
        public decimal  UnitPrice   { get; set; }
        public decimal  Subtotal    => UnitPrice * Quantity;
    }
}
