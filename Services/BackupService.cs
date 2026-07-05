#nullable enable
using SihyuPOSPayroll.Helpers;
using MySql.Data.MySqlClient;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace SihyuPOSPayroll.Services
{
    public class BackupService
    {
        private readonly string _connectionString;
        private readonly AuditLogService _auditLogService;

        public BackupService()
        {
            _connectionString = ConfigurationHelper.GetConnectionString();
            _auditLogService = new AuditLogService();
        }

        public async Task<string> BackupDatabaseAsync(string backupDirectory)
        {
            try
            {
                if (!Directory.Exists(backupDirectory))
                {
                    Directory.CreateDirectory(backupDirectory);
                }

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupFileName = $"hillscafe_backup_{timestamp}.sql";
                var backupFilePath = Path.Combine(backupDirectory, backupFileName);

                var builder = new MySqlConnectionStringBuilder(_connectionString);
                var server = builder.Server;
                var port = builder.Port;
                var database = builder.Database;
                var username = builder.UserID;
                var password = builder.Password;

                var mysqldumpPath = FindMySqlDump();
                if (string.IsNullOrEmpty(mysqldumpPath))
                {
                    throw new Exception("mysqldump not found. Please ensure MySQL is installed and in PATH.");
                }

                var arguments = $"--host={server} --port={port} --user={username} --password={password} --databases {database} --result-file=\"{backupFilePath}\"";
                
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = mysqldumpPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    throw new Exception($"Backup failed: {error}");
                }

                _auditLogService.LogAction("BACKUP_DATABASE", "Database", null, null, null, $"Database backed up to {backupFilePath}");

                return backupFilePath;
            }
            catch (Exception ex)
            {
                _auditLogService.LogAction("BACKUP_DATABASE_FAILED", "Database", null, null, null, ex.Message);
                throw;
            }
        }

        public async Task RestoreDatabaseAsync(string backupFilePath)
        {
            try
            {
                if (!File.Exists(backupFilePath))
                {
                    throw new FileNotFoundException("Backup file not found", backupFilePath);
                }

                var builder = new MySqlConnectionStringBuilder(_connectionString);
                var server = builder.Server;
                var port = builder.Port;
                var username = builder.UserID;
                var password = builder.Password;

                var mysqlPath = FindMySql();
                if (string.IsNullOrEmpty(mysqlPath))
                {
                    throw new Exception("mysql command not found. Please ensure MySQL is installed and in PATH.");
                }

                var arguments = $"--host={server} --port={port} --user={username} --password={password}";
                
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = mysqlPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();

                await using var writer = process.StandardInput;
                await using var reader = File.OpenText(backupFilePath);
                await writer.WriteAsync(await reader.ReadToEndAsync());
                writer.Close();

                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    throw new Exception($"Restore failed: {error}");
                }

                _auditLogService.LogAction("RESTORE_DATABASE", "Database", null, null, null, $"Database restored from {backupFilePath}");
            }
            catch (Exception ex)
            {
                _auditLogService.LogAction("RESTORE_DATABASE_FAILED", "Database", null, null, null, ex.Message);
                throw;
            }
        }

        private static string? FindMySqlDump()
        {
            var paths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MySQL", "MySQL Server 8.0", "bin", "mysqldump.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MySQL", "MySQL Server 8.0", "bin", "mysqldump.exe"),
                "mysqldump"
            };

            foreach (var path in paths)
            {
                if (File.Exists(path))
                    return path;
            }

            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = "mysqldump",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                });
                
                if (process != null)
                {
                    var result = process.StandardOutput.ReadLine();
                    if (!string.IsNullOrEmpty(result) && File.Exists(result))
                        return result;
                }
            }
            catch
            {
                // Ignore
            }

            return null;
        }

        private static string? FindMySql()
        {
            var paths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MySQL", "MySQL Server 8.0", "bin", "mysql.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MySQL", "MySQL Server 8.0", "bin", "mysql.exe"),
                "mysql"
            };

            foreach (var path in paths)
            {
                if (File.Exists(path))
                    return path;
            }

            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = "mysql",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                });
                
                if (process != null)
                {
                    var result = process.StandardOutput.ReadLine();
                    if (!string.IsNullOrEmpty(result) && File.Exists(result))
                        return result;
                }
            }
            catch
            {
                // Ignore
            }

            return null;
        }
    }
}
