#nullable enable
using SihyuPOSPayroll.Models;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;

namespace SihyuPOSPayroll.Services
{
    /// <summary>
    /// Consolidated DB access. Authentication enforces users.is_active = 1.
    /// </summary>
    public class DatabaseService
    {
        private readonly string _connectionString = "server=localhost;user=root;password=;database=sihyu_pos;";

        #region Authentication

        /// <summary>
        /// Returns a user when email+password match AND account is active (users.is_active = 1).
        /// Returns null otherwise.
        /// NOTE: This currently compares plaintext passwords to match your existing code.
        /// </summary>
        public UserModel? AuthenticateUser(string email, string password)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                const string query = @"
                    SELECT 
                        u.id          AS user_id,
                        u.email,
                        u.password,
                        u.role,
                        u.employee_id,
                        u.is_active   AS user_is_active,
                        e.full_name,
                        e.position
                    FROM users u
                    LEFT JOIN employees e ON u.employee_id = e.id
                    WHERE u.email = @Email
                      AND u.password = @Password
                      AND u.is_active = 1
                    LIMIT 1;";

                using var cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@Email", email);
                cmd.Parameters.AddWithValue("@Password", password);

                using var reader = cmd.ExecuteReader();

                if (!reader.Read()) return null;

                EmployeeModel? employee = null;

                if (!reader.IsDBNull(reader.GetOrdinal("employee_id")))
                {
                    employee = new EmployeeModel
                    {
                        Id = reader.GetInt32("employee_id"),
                        FullName = reader["full_name"]?.ToString(),
                        Position = reader["position"]?.ToString()
                    };
                }

                // Map user, including IsActive (it will always be true here due to WHERE, but we map for completeness)
                var user = new UserModel
                {
                    Id = reader.GetInt32("user_id"),
                    Email = reader["email"]?.ToString(),
                    Password = reader["password"]?.ToString(),
                    Role = reader["role"]?.ToString(),
                    EmployeeId = reader.IsDBNull(reader.GetOrdinal("employee_id")) ? null : reader.GetInt32("employee_id"),
                    IsActive = ReadTinyIntAsBool(reader, "user_is_active", true),
                    Employee = employee
                };

                return user;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Login error: " + ex.Message);
                return null;
            }
        }

        private static bool ReadTinyIntAsBool(MySqlDataReader r, string col, bool defaultValue = false)
        {
            try
            {
                if (r.IsDBNull(r.GetOrdinal(col))) return defaultValue;
                return Convert.ToInt32(r[col]) == 1;
            }
            catch
            {
                return defaultValue;
            }
        }

        #endregion

        #region Menu

        public List<MenuModel> GetAllMenuItems()
        {
            var menuItems = new List<MenuModel>();

            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                const string query = @"
                    SELECT 
                        id,
                        name,
                        category,
                        price,
                        image_url,
                        description,
                        created_at
                    FROM menu
                    ORDER BY id DESC";

                using var cmd = new MySqlCommand(query, connection);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var item = new MenuModel
                    {
                        Id = reader.GetInt32("id"),
                        Name = reader["name"]?.ToString(),
                        Category = reader["category"]?.ToString(),
                        // keep your existing typing assumptions
                        Price = reader["price"] as decimal?,
                        ImageUrl = reader["image_url"]?.ToString(),
                        Description = reader["description"]?.ToString(),
                        CreatedAt = reader.GetDateTime("created_at")
                    };

                    menuItems.Add(item);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error loading menu items: " + ex.Message);
            }

            return menuItems;
        }

        #endregion

        #region Inventory

        public List<InventoryItem> GetInventoryItems()
        {
            var items = new List<InventoryItem>();

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                const string query = @"
                    SELECT
                        i.id,
                        p.name AS ProductName,
                        c.name AS CategoryName,
                        i.quantity,
                        i.expiry_date
                    FROM inventory i
                    JOIN products p ON i.product_id = p.id
                    LEFT JOIN categories c ON p.category_id = c.id
                    ORDER BY i.id DESC LIMIT 0, 25;";

                using var cmd = new MySqlCommand(query, conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var item = new InventoryItem
                    {
                        Id = reader.GetInt32("id"),
                        ProductName = reader["ProductName"]?.ToString() ?? "",
                        CategoryName = reader["CategoryName"]?.ToString() ?? "",
                        Quantity = reader.GetInt32("quantity"),
                        ExpiryDate = reader["expiry_date"] != DBNull.Value
                            ? Convert.ToDateTime(reader["expiry_date"])
                            : (DateTime?)null
                    };

                    items.Add(item);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error loading inventory items: " + ex.Message);
            }

            return items;
        }

        #endregion
    }
}
