#nullable enable
using Microsoft.Data.Sqlite;
using SihyuPOSPayroll.Data;
using System;
using System.Collections.Generic;

namespace SihyuPOSPayroll.Services
{
    public class TableModel
    {
        public int    Id           { get; set; }
        public string TableNumber  { get; set; } = string.Empty;
        public string Status       { get; set; } = "Available"; // "Available" | "Occupied"
        public bool   IsAvailable  => Status == "Available";
    }

    /// <summary>
    /// CRUD for the <c>cafe_tables</c> table (SQLite).
    /// Status is derived at query time from open orders — no manual flag needed.
    /// </summary>
    public static class TableService
    {
        // ── Read ───────────────────────────────────────────────────────────────

        public static List<TableModel> GetAllTables()
        {
            var tables = new List<TableModel>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT
                        t.id,
                        t.table_number,
                        CASE WHEN EXISTS (
                            SELECT 1 FROM orders o
                            WHERE  o.table_number  = t.table_number
                              AND  o.payment_status = 'Unpaid'
                              AND  o.order_status  IN ('Pending','Preparing','Served')
                        ) THEN 'Occupied' ELSE 'Available' END AS Status
                    FROM   cafe_tables t
                    ORDER  BY t.table_number;";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    tables.Add(new TableModel
                    {
                        Id          = reader.GetInt32(reader.GetOrdinal("id")),
                        TableNumber = reader.GetString(reader.GetOrdinal("table_number")),
                        Status      = reader.GetString(reader.GetOrdinal("Status")),
                    });
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[TableService] GetAllTables: {ex.Message}");
            }
            return tables;
        }

        // ── Create ─────────────────────────────────────────────────────────────

        public static void AddTable(string tableNumber)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT OR IGNORE INTO cafe_tables (table_number, is_available)
                    VALUES (@tn, 1);";
                cmd.Parameters.AddWithValue("@tn", tableNumber);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[TableService] AddTable: {ex.Message}");
            }
        }

        // ── Update ─────────────────────────────────────────────────────────────

        public static void UpdateTable(int tableId, string tableNumber)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "UPDATE cafe_tables SET table_number = @tn WHERE id = @id;";
                cmd.Parameters.AddWithValue("@tn", tableNumber);
                cmd.Parameters.AddWithValue("@id", tableId);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[TableService] UpdateTable: {ex.Message}");
            }
        }

        public static void SetAvailabilityManual(int tableId, bool available)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "UPDATE cafe_tables SET is_available = @avail WHERE id = @id;";
                cmd.Parameters.AddWithValue("@avail", available ? 1 : 0);
                cmd.Parameters.AddWithValue("@id",    tableId);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[TableService] SetAvailabilityManual: {ex.Message}");
            }
        }

        // ── Delete ─────────────────────────────────────────────────────────────

        public static void DeleteTable(int tableId)
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM cafe_tables WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", tableId);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[TableService] DeleteTable: {ex.Message}");
            }
        }
    }
}
