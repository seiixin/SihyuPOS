using System;
using System.Collections.Generic;
using System.Data;
using MySql.Data.MySqlClient;

namespace SihyuPOSPayroll.Services
{
    public class TableModel
    {
        public int Id { get; set; }
        public string TableNumber { get; set; } = string.Empty;
        public string Status { get; set; } = "Available"; // "Available" or "Occupied"
        public bool IsAvailable => Status == "Available";
    }

    public static class TableService
    {
        private const string ConnectionString = "server=localhost;user=root;password=;database=sihyu_pos;";

        /// <summary>
        /// Fetch all caf� tables with derived status ("Available"/"Occupied").
        /// Status is computed from active (unpaid + open) orders.
        /// </summary>
        public static List<TableModel> GetAllTables()
        {
            var tables = new List<TableModel>();

            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();

            var sql = @"
                SELECT 
                    t.id,
                    t.table_number,
                    CASE 
                      WHEN EXISTS (
                        SELECT 1 FROM orders o
                         WHERE o.table_number = t.table_number
                           AND o.payment_status = 'Unpaid'
                           AND o.order_status IN ('Pending','Preparing','Served')
                      )
                      THEN 'Occupied'
                      ELSE 'Available'
                    END AS Status
                FROM cafe_tables t
                ORDER BY t.table_number;";

            using var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                tables.Add(new TableModel
                {
                    Id = reader.GetInt32("id"),
                    TableNumber = reader.GetString("table_number"),
                    Status = reader.GetString("Status")
                });
            }

            return tables;
        }

        /// <summary>
        /// Optional manual override of is_available column in cafe_tables.
        /// Usually not recommended since status is derived automatically,
        /// but kept here in case admins need to force a value.
        /// </summary>
        public static void SetAvailabilityManual(int tableId, bool available)
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();

            var sql = "UPDATE cafe_tables SET is_available = @avail WHERE id = @id;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@avail", available ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", tableId);

            cmd.ExecuteNonQuery();
        }

        /// <summary>Inserts a new table row.</summary>
        public static void AddTable(string tableNumber)
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            using var cmd = new MySqlCommand(
                "INSERT INTO cafe_tables (table_number, is_available) VALUES (@tn, 1);", conn);
            cmd.Parameters.AddWithValue("@tn", tableNumber);
            cmd.ExecuteNonQuery();
        }

        /// <summary>Renames an existing table.</summary>
        public static void UpdateTable(int tableId, string tableNumber)
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            using var cmd = new MySqlCommand(
                "UPDATE cafe_tables SET table_number = @tn WHERE id = @id;", conn);
            cmd.Parameters.AddWithValue("@tn", tableNumber);
            cmd.Parameters.AddWithValue("@id", tableId);
            cmd.ExecuteNonQuery();
        }

        /// <summary>Deletes a table. Caller should confirm first.</summary>
        public static void DeleteTable(int tableId)
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            using var cmd = new MySqlCommand(
                "DELETE FROM cafe_tables WHERE id = @id;", conn);
            cmd.Parameters.AddWithValue("@id", tableId);
            cmd.ExecuteNonQuery();
        }
    }
}
