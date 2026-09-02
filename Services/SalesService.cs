#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using SihyuPOSPayroll.Data;
using SihyuPOSPayroll.Models;

namespace SihyuPOSPayroll.Services
{
    public static class SalesService
    {
        // ── GetSales ──────────────────────────────────────────────────────────

        public static List<SalesRow> GetSales(ReportPeriod period)
        {
            var rows = new List<SalesRow>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = BuildGetSalesSql(period);

                using var rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    DateTime sdate = DateTime.Parse(rd.GetString(rd.GetOrdinal("sdate")));
                    DateTime edate = DateTime.Parse(rd.GetString(rd.GetOrdinal("edate")));
                    decimal  total = rd.IsDBNull(rd.GetOrdinal("total")) ? 0m : Convert.ToDecimal(rd.GetValue(rd.GetOrdinal("total")));
                    int      cnt   = rd.IsDBNull(rd.GetOrdinal("cnt"))   ? 0  : rd.GetInt32(rd.GetOrdinal("cnt"));

                    string label = period switch
                    {
                        ReportPeriod.Daily     => sdate.ToString("yyyy-MM-dd"),
                        ReportPeriod.Weekly    => $"{sdate:yyyy} W{System.Globalization.ISOWeek.GetWeekOfYear(sdate)}",
                        ReportPeriod.Monthly   => sdate.ToString("yyyy-MM"),
                        ReportPeriod.Quarterly => $"{sdate:yyyy} Q{((sdate.Month - 1) / 3) + 1}",
                        ReportPeriod.Yearly    => sdate.ToString("yyyy"),
                        _                      => sdate.ToString("yyyy-MM-dd"),
                    };

                    rows.Add(new SalesRow
                    {
                        Period       = label,
                        StartDate    = sdate,
                        EndDate      = edate,
                        TotalAmount  = total,
                        ReceiptCount = cnt,
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SalesService] GetSales: {ex.Message}");
            }
            return rows;
        }

        private static string BuildGetSalesSql(ReportPeriod period) => period switch
        {
            ReportPeriod.Daily => @"
                SELECT date(r.issued_at)           AS sdate,
                       date(r.issued_at)           AS edate,
                       SUM(r.amount_paid)          AS total,
                       COUNT(*)                    AS cnt
                FROM   receipts r
                GROUP  BY date(r.issued_at)
                ORDER  BY sdate DESC;",

            ReportPeriod.Weekly => @"
                SELECT strftime('%Y-%W', r.issued_at)       AS yw,
                       date(r.issued_at, 'weekday 1', '-6 days') AS sdate,
                       date(r.issued_at, 'weekday 0')       AS edate,
                       SUM(r.amount_paid)                   AS total,
                       COUNT(*)                             AS cnt
                FROM   receipts r
                GROUP  BY strftime('%Y-%W', r.issued_at)
                ORDER  BY sdate DESC;",

            ReportPeriod.Monthly => @"
                SELECT strftime('%Y-%m', r.issued_at)   AS ym,
                       date(r.issued_at, 'start of month') AS sdate,
                       date(r.issued_at, 'start of month', '+1 month', '-1 day') AS edate,
                       SUM(r.amount_paid)               AS total,
                       COUNT(*)                         AS cnt
                FROM   receipts r
                GROUP  BY strftime('%Y-%m', r.issued_at)
                ORDER  BY sdate DESC;",

            ReportPeriod.Quarterly => @"
                SELECT strftime('%Y', r.issued_at)  AS y,
                       ((CAST(strftime('%m', r.issued_at) AS INTEGER) - 1) / 3) AS q,
                       MIN(date(r.issued_at))       AS sdate,
                       MAX(date(r.issued_at))       AS edate,
                       SUM(r.amount_paid)           AS total,
                       COUNT(*)                     AS cnt
                FROM   receipts r
                GROUP  BY strftime('%Y', r.issued_at),
                          ((CAST(strftime('%m', r.issued_at) AS INTEGER) - 1) / 3)
                ORDER  BY sdate DESC;",

            _ => @"
                SELECT strftime('%Y', r.issued_at)  AS y,
                       MIN(date(r.issued_at))       AS sdate,
                       MAX(date(r.issued_at))       AS edate,
                       SUM(r.amount_paid)           AS total,
                       COUNT(*)                     AS cnt
                FROM   receipts r
                GROUP  BY strftime('%Y', r.issued_at)
                ORDER  BY sdate DESC;",
        };

        // ── GetTodayTotal ─────────────────────────────────────────────────────

        /// <summary>
        /// Sum of receipts issued today.
        /// Falls back to summing paid orders if no receipts exist for today.
        /// </summary>
        public static decimal GetTodayTotal()
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();

                // Primary: receipts
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT COALESCE(SUM(amount_paid), 0)
                        FROM   receipts
                        WHERE  date(issued_at) = date('now');";
                    var obj = cmd.ExecuteScalar();
                    decimal v = (obj == null || obj == DBNull.Value) ? 0m : Convert.ToDecimal(obj);
                    if (v > 0) return v;
                }

                // Fallback: paid orders
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT COALESCE(SUM(total_amount), 0)
                        FROM   orders
                        WHERE  payment_status = 'Paid'
                          AND  date(created_at) = date('now');";
                    var obj = cmd.ExecuteScalar();
                    return (obj == null || obj == DBNull.Value) ? 0m : Convert.ToDecimal(obj);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SalesService] GetTodayTotal: {ex.Message}");
                return 0m;
            }
        }

        // ── GetYesterdayTotal ─────────────────────────────────────────────────

        /// <summary>Returns total sales for yesterday.</summary>
        public static decimal GetYesterdayTotal()
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();

                // Primary: receipts
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT COALESCE(SUM(amount_paid), 0)
                        FROM   receipts
                        WHERE  date(issued_at) = date('now', '-1 day');";
                    var obj = cmd.ExecuteScalar();
                    decimal v = (obj == null || obj == DBNull.Value) ? 0m : Convert.ToDecimal(obj);
                    if (v > 0) return v;
                }

                // Fallback: paid orders
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT COALESCE(SUM(total_amount), 0)
                        FROM   orders
                        WHERE  payment_status = 'Paid'
                          AND  date(created_at) = date('now', '-1 day');";
                    var obj = cmd.ExecuteScalar();
                    return (obj == null || obj == DBNull.Value) ? 0m : Convert.ToDecimal(obj);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SalesService] GetYesterdayTotal: {ex.Message}");
                return 0m;
            }
        }

        // ── GetDailySalesForMonth ─────────────────────────────────────────────

        /// <summary>
        /// Daily sales rows for a given year/month.
        /// Reads from receipts first; falls back to paid orders.
        /// </summary>
        public static List<SalesRow> GetDailySalesForMonth(int year, int month)
        {
            var rows = new List<SalesRow>();
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();

                // Build the month prefix for LIKE matching: e.g. "2026-08"
                string monthPrefix = $"{year:D4}-{month:D2}";

                // Primary: receipts
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT date(issued_at)       AS sdate,
                               SUM(amount_paid)      AS total,
                               COUNT(*)              AS cnt
                        FROM   receipts
                        WHERE  strftime('%Y-%m', issued_at) = @ym
                        GROUP  BY date(issued_at)
                        ORDER  BY sdate;";
                    cmd.Parameters.AddWithValue("@ym", monthPrefix);

                    using var rd = cmd.ExecuteReader();
                    while (rd.Read())
                    {
                        rows.Add(MapDailyRow(rd));
                    }
                }

                if (rows.Count > 0) return rows;

                // Fallback: paid orders
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT date(created_at)      AS sdate,
                               SUM(total_amount)     AS total,
                               COUNT(*)              AS cnt
                        FROM   orders
                        WHERE  payment_status = 'Paid'
                          AND  strftime('%Y-%m', created_at) = @ym
                        GROUP  BY date(created_at)
                        ORDER  BY sdate;";
                    cmd.Parameters.AddWithValue("@ym", monthPrefix);

                    using var rd = cmd.ExecuteReader();
                    while (rd.Read())
                    {
                        rows.Add(MapDailyRow(rd));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SalesService] GetDailySalesForMonth: {ex.Message}");
            }
            return rows;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static SalesRow MapDailyRow(SqliteDataReader rd)
        {
            DateTime sdate = DateTime.Parse(rd.GetString(rd.GetOrdinal("sdate")));
            decimal  total = rd.IsDBNull(rd.GetOrdinal("total")) ? 0m : Convert.ToDecimal(rd.GetValue(rd.GetOrdinal("total")));
            int      cnt   = rd.IsDBNull(rd.GetOrdinal("cnt"))   ? 0  : rd.GetInt32(rd.GetOrdinal("cnt"));

            return new SalesRow
            {
                StartDate    = sdate,
                EndDate      = sdate,
                TotalAmount  = total,
                ReceiptCount = cnt,
            };
        }
    }
}
