#nullable enable
using Microsoft.Data.Sqlite;
using SihyuPOSPayroll.Helpers;
using System.IO;

namespace SihyuPOSPayroll.Data
{
    /// <summary>
    /// Single point of truth for the SQLite database file path and connection creation.
    /// All SQLite-backed services obtain connections from here so the path is never
    /// hard-coded across multiple files.
    ///
    /// The database file is placed next to the running executable so the app is
    /// fully portable (no installation required).
    /// </summary>
    public static class SqliteConnectionFactory
    {
        // ── Database file location ────────────────────────────────────────────
        // Priority:  appsettings.json "ConnectionStrings:SQLiteConnection"
        //            → default: <exe-dir>/sihyupos.db
        private static string? _resolvedPath;

        /// <summary>Absolute path to the .db file (resolved once, then cached).</summary>
        public static string DatabasePath
        {
            get
            {
                if (_resolvedPath is not null) return _resolvedPath;

                // Try appsettings.json first
                var cs = ConfigurationHelper.GetConnectionString("SQLiteConnection");

                if (!string.IsNullOrWhiteSpace(cs))
                {
                    // Support both a plain file path and a proper connection string
                    // e.g. "Data Source=sihyupos.db" or just "sihyupos.db"
                    var builder = new SqliteConnectionStringBuilder(cs);
                    var source = builder.DataSource;

                    _resolvedPath = Path.IsPathRooted(source)
                        ? source
                        : Path.Combine(AppContext.BaseDirectory, source);
                }
                else
                {
                    _resolvedPath = Path.Combine(AppContext.BaseDirectory, "sihyupos.db");
                }

                return _resolvedPath;
            }
        }

        /// <summary>Returns the connection string for the resolved database path.</summary>
        public static string ConnectionString =>
            new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString();

        /// <summary>
        /// Opens and returns a new <see cref="SqliteConnection"/>.
        /// The caller is responsible for disposal (use inside a <c>using</c> block).
        /// </summary>
        public static SqliteConnection CreateOpenConnection()
        {
            var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            // Enable WAL mode for better concurrent read performance and
            // foreign-key enforcement (SQLite disables FKs by default).
            using var pragmaFk  = connection.CreateCommand();
            using var pragmaWal = connection.CreateCommand();
            pragmaFk.CommandText  = "PRAGMA foreign_keys = ON;";
            pragmaWal.CommandText = "PRAGMA journal_mode = WAL;";
            pragmaFk.ExecuteNonQuery();
            pragmaWal.ExecuteNonQuery();

            return connection;
        }

        /// <summary>
        /// Returns an un-opened <see cref="SqliteConnection"/> for cases where
        /// the caller needs to configure the connection before opening.
        /// </summary>
        public static SqliteConnection CreateConnection() =>
            new SqliteConnection(ConnectionString);
    }
}
