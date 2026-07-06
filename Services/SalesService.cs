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
    }
}
