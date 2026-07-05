#nullable enable
using SihyuPOSPayroll.Helpers;
using SihyuPOSPayroll.Models;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text.Json;

namespace SihyuPOSPayroll.Services
{
    public class AuditLogService
    {
        private readonly string _connectionString;

        public AuditLogService()
        {
            _connectionString = ConfigurationHelper.GetConnectionString();
        }

        public void LogAction(string actionType, string? entityType = null, int? entityId = null,
            object? oldValue = null, object? newValue = null, string? description = null)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                const string query = @"
                    INSERT INTO audit_logs 
                    (user_id, employee_id, action_type, entity_type, entity_id, old_value, new_value, description, ip_address, user_agent, created_at)
                    VALUES 
                    (@UserId, @EmployeeId, @ActionType, @EntityType, @EntityId, @OldValue, @NewValue, @Description, @IpAddress, @UserAgent, NOW());";

                using var cmd = new MySqlCommand(query, connection);
                
                cmd.Parameters.AddWithValue("@UserId", Session.CurrentUserId > 0 ? Session.CurrentUserId : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@EmployeeId", (object)DBNull.Value); // Can be linked to employee later
                cmd.Parameters.AddWithValue("@ActionType", actionType);
                cmd.Parameters.AddWithValue("@EntityType", entityType ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@EntityId", entityId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@OldValue", oldValue != null ? JsonSerializer.Serialize(oldValue) : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@NewValue", newValue != null ? JsonSerializer.Serialize(newValue) : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Description", description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@IpAddress", GetLocalIpAddress() ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@UserAgent", Environment.OSVersion.VersionString);

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error logging audit action: " + ex.Message);
            }
        }

        public List<AuditLogModel> GetAllAuditLogs(int limit = 100)
        {
            var logs = new List<AuditLogModel>();

            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                const string query = @"
                    SELECT 
                        id, user_id, employee_id, action_type, entity_type, entity_id, 
                        old_value, new_value, description, ip_address, user_agent, created_at
                    FROM audit_logs
                    ORDER BY created_at DESC
                    LIMIT @Limit;";

                using var cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@Limit", limit);

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    logs.Add(new AuditLogModel
                    {
                        Id = reader.GetInt64("id"),
                        UserId = reader.IsDBNull("user_id") ? null : reader.GetInt32("user_id"),
                        EmployeeId = reader.IsDBNull("employee_id") ? null : reader.GetInt32("employee_id"),
                        ActionType = reader["action_type"]?.ToString() ?? string.Empty,
                        EntityType = reader["entity_type"]?.ToString(),
                        EntityId = reader.IsDBNull("entity_id") ? null : reader.GetInt32("entity_id"),
                        OldValue = reader["old_value"]?.ToString(),
                        NewValue = reader["new_value"]?.ToString(),
                        Description = reader["description"]?.ToString(),
                        IpAddress = reader["ip_address"]?.ToString(),
                        UserAgent = reader["user_agent"]?.ToString(),
                        CreatedAt = reader.GetDateTime("created_at")
                    });
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error loading audit logs: " + ex.Message);
            }

            return logs;
        }

        private static string? GetLocalIpAddress()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up)
                    {
                        foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                return ip.Address.ToString();
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore any errors getting IP address
            }
            return null;
        }
    }
}
