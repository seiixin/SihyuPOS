using System;
using System.Collections.Generic;
using System.Data;
using SihyuPOSPayroll.Models;
using MySql.Data.MySqlClient;

namespace SihyuPOSPayroll.Services
{
    public static class SalesService
    {
        private const string ConnectionString = "server=localhost;user=root;password=;database=sihyu_pos;";

        public static List<SalesRow> GetSales(ReportPeriod period)
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();

            string sql = period switch
            {
                ReportPeriod.Daily => @"
                    SELECT DATE(r.issued_at) AS sdate,
                           DATE(r.issued_at) AS edate,
                           SUM(r.amount_paid) AS total,
                           COUNT(*) AS cnt
                    FROM receipts r
                    GROUP BY DATE(r.issued_at)
                    ORDER BY sdate DESC;",

                ReportPeriod.Weekly => @"
                    SELECT YEARWEEK(r.issued_at,1) AS yw,
                           MIN(DATE(r.issued_at)) AS sdate,
                           MAX(DATE(r.issued_at)) AS edate,
                           SUM(r.amount_paid) AS total,
                           COUNT(*) AS cnt
                    FROM receipts r
                    GROUP BY YEARWEEK(r.issued_at,1)
                    ORDER BY sdate DESC;",

                ReportPeriod.Monthly => @"
                    SELECT DATE_FORMAT(r.issued_at, '%Y-%m') AS ym,
                           MIN(DATE(r.issued_at)) AS sdate,
                           MAX(DATE(r.issued_at)) AS edate,
                           SUM(r.amount_paid) AS total,
                           COUNT(*) AS cnt
                    FROM receipts r
                    GROUP BY DATE_FORMAT(r.issued_at, '%Y-%m')
                    ORDER BY sdate DESC;",

                ReportPeriod.Quarterly => @"
                    SELECT YEAR(r.issued_at) AS y,
                           QUARTER(r.issued_at) AS q,
                           MIN(DATE(r.issued_at)) AS sdate,
                           MAX(DATE(r.issued_at)) AS edate,
                           SUM(r.amount_paid) AS total,
                           COUNT(*) AS cnt
                    FROM receipts r
                    GROUP BY YEAR(r.issued_at), QUARTER(r.issued_at)
                    ORDER BY sdate DESC;",

                _ => @"
                    SELECT YEAR(r.issued_at) AS y,
                           MIN(DATE(r.issued_at)) AS sdate,
                           MAX(DATE(r.issued_at)) AS edate,
                           SUM(r.amount_paid) AS total,
                           COUNT(*) AS cnt
                    FROM receipts r
                    GROUP BY YEAR(r.issued_at)
                    ORDER BY sdate DESC;"
            };

            using var cmd = new MySqlCommand(sql, conn);
            using var rd = cmd.ExecuteReader();

            var rows = new List<SalesRow>();
            while (rd.Read())
            {
                DateTime sdate = rd.GetDateTime("sdate");
                DateTime edate = rd.GetDateTime("edate");
                decimal total = rd.IsDBNull(rd.GetOrdinal("total")) ? 0m : rd.GetDecimal("total");
                int cnt = rd.IsDBNull(rd.GetOrdinal("cnt")) ? 0 : rd.GetInt32("cnt");

                string label = period switch
                {
                    ReportPeriod.Daily => sdate.ToString("yyyy-MM-dd"),
                    ReportPeriod.Weekly => $"{sdate:yyyy} W{System.Globalization.ISOWeek.GetWeekOfYear(sdate)}",
                    ReportPeriod.Monthly => sdate.ToString("yyyy-MM"),
                    ReportPeriod.Quarterly => $"{sdate:yyyy} Q{((sdate.Month - 1) / 3) + 1}",
                    ReportPeriod.Yearly => sdate.ToString("yyyy"),
                    _ => sdate.ToString("yyyy-MM-dd")
                };

                rows.Add(new SalesRow
                {
                    Period = label,
                    StartDate = sdate,
                    EndDate = edate,
                    TotalAmount = total,
                    ReceiptCount = cnt
                });
            }

            return rows;
        }
        /// <summary>
        /// Returns total sales for today from the receipts table.
        /// Falls back to summing paid orders if receipts table is empty.
        /// </summary>
        public static decimal GetTodayTotal()
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();

            // Primary: sum receipts issued today
            const string receiptSql = @"
                SELECT COALESCE(SUM(r.amount_paid), 0)
                FROM receipts r
                WHERE DATE(r.issued_at) = CURDATE();";

            using (var cmd = new MySqlCommand(receiptSql, conn))
            {
                var obj = cmd.ExecuteScalar();
                decimal fromReceipts = (obj == null || obj == DBNull.Value) ? 0m : Convert.ToDecimal(obj);
                if (fromReceipts > 0) return fromReceipts;
            }

            // Fallback: sum paid orders created today (in case receipt row is missing)
            const string orderSql = @"
                SELECT COALESCE(SUM(total_amount), 0)
                FROM orders
                WHERE payment_status = 'Paid'
                  AND DATE(created_at) = CURDATE();";

            using (var cmd2 = new MySqlCommand(orderSql, conn))
            {
                var obj2 = cmd2.ExecuteScalar();
                return (obj2 == null || obj2 == DBNull.Value) ? 0m : Convert.ToDecimal(obj2);
            }
        }

        /// <summary>
        /// Returns total sales for yesterday (same dual-source logic).
        /// </summary>
        public static decimal GetYesterdayTotal()
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();

            const string receiptSql = @"
                SELECT COALESCE(SUM(r.amount_paid), 0)
                FROM receipts r
                WHERE DATE(r.issued_at) = DATE_SUB(CURDATE(), INTERVAL 1 DAY);";

            using (var cmd = new MySqlCommand(receiptSql, conn))
            {
                var obj = cmd.ExecuteScalar();
                decimal fromReceipts = (obj == null || obj == DBNull.Value) ? 0m : Convert.ToDecimal(obj);
                if (fromReceipts > 0) return fromReceipts;
            }

            const string orderSql = @"
                SELECT COALESCE(SUM(total_amount), 0)
                FROM orders
                WHERE payment_status = 'Paid'
                  AND DATE(created_at) = DATE_SUB(CURDATE(), INTERVAL 1 DAY);";

            using (var cmd2 = new MySqlCommand(orderSql, conn))
            {
                var obj2 = cmd2.ExecuteScalar();
                return (obj2 == null || obj2 == DBNull.Value) ? 0m : Convert.ToDecimal(obj2);
            }
        }

        /// <summary>
        /// Returns daily sales rows for a given month/year, reading from both
        /// receipts (primary) and paid orders (fallback/merge).
        /// </summary>
        public static List<SalesRow> GetDailySalesForMonth(int year, int month)
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();

            // Try receipts first
            const string receiptSql = @"
                SELECT DATE(r.issued_at) AS sdate,
                       SUM(r.amount_paid) AS total,
                       COUNT(*) AS cnt
                FROM receipts r
                WHERE YEAR(r.issued_at) = @yr AND MONTH(r.issued_at) = @mo
                GROUP BY DATE(r.issued_at)
                ORDER BY sdate;";

            var rows = new List<SalesRow>();
            using (var cmd = new MySqlCommand(receiptSql, conn))
            {
                cmd.Parameters.AddWithValue("@yr", year);
                cmd.Parameters.AddWithValue("@mo", month);
                using var rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    rows.Add(new SalesRow
                    {
                        StartDate    = rd.GetDateTime("sdate"),
                        EndDate      = rd.GetDateTime("sdate"),
                        TotalAmount  = rd.IsDBNull(rd.GetOrdinal("total")) ? 0m : rd.GetDecimal("total"),
                        ReceiptCount = rd.IsDBNull(rd.GetOrdinal("cnt"))   ? 0   : rd.GetInt32("cnt"),
                    });
                }
            }

            if (rows.Count > 0) return rows;

            // Fallback: paid orders
            const string orderSql = @"
                SELECT DATE(created_at) AS sdate,
                       SUM(total_amount) AS total,
                       COUNT(*) AS cnt
                FROM orders
                WHERE payment_status = 'Paid'
                  AND YEAR(created_at) = @yr AND MONTH(created_at) = @mo
                GROUP BY DATE(created_at)
                ORDER BY sdate;";

            using (var cmd2 = new MySqlCommand(orderSql, conn))
            {
                cmd2.Parameters.AddWithValue("@yr", year);
                cmd2.Parameters.AddWithValue("@mo", month);
                using var rd2 = cmd2.ExecuteReader();
                while (rd2.Read())
                {
                    rows.Add(new SalesRow
                    {
                        StartDate    = rd2.GetDateTime("sdate"),
                        EndDate      = rd2.GetDateTime("sdate"),
                        TotalAmount  = rd2.IsDBNull(rd2.GetOrdinal("total")) ? 0m : rd2.GetDecimal("total"),
                        ReceiptCount = rd2.IsDBNull(rd2.GetOrdinal("cnt"))   ? 0   : rd2.GetInt32("cnt"),
                    });
                }
            }

            return rows;
        }
    }
}
