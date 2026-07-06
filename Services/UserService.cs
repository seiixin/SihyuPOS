using SihyuPOSPayroll.Models;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;

namespace SihyuPOSPayroll.Services
{
    public class UserService
    {
        private readonly string _connectionString = "server=localhost;user=root;password=;database=sihyu_pos;";

        #region READ

        /// <summary>
        /// Get all users with linked employee info.
        /// </summary>
        public List<UserModel> GetAllUsers()
        {
            var users = new List<UserModel>();

            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                const string query = @"
                    SELECT 
                        u.id,
                        u.email,
                        u.password,
                        u.role,
                        u.employee_id,
                        e.full_name
                    FROM users u
                    LEFT JOIN employees e ON u.employee_id = e.id";

                using var cmd = new MySqlCommand(query, connection);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var user = new UserModel
                    {
                        Id = reader.GetInt32("id"),
                        Email = reader["email"]?.ToString(),
                        Password = reader["password"]?.ToString(),
                        Role = reader["role"]?.ToString(),
                        EmployeeId = reader.IsDBNull(reader.GetOrdinal("employee_id")) ? null : reader.GetInt32("employee_id"),
                        Employee = new EmployeeModel
                        {
                            FullName = reader["full_name"]?.ToString() ?? ""
                        }
                    };

                    users.Add(user);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error loading users: " + ex.Message);
            }

            return users;
        }

        #endregion

        #region CREATE

        /// <summary>
        /// Add a new user.
        /// </summary>
        public bool AddUser(UserModel user)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                const string query = @"
                    INSERT INTO users (email, password, role, employee_id)
                    VALUES (@Email, @Password, @Role, @EmployeeId)";

                using var cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@Email", user.Email);
                cmd.Parameters.AddWithValue("@Password", user.Password);
                cmd.Parameters.AddWithValue("@Role", user.Role);
                if (user.EmployeeId.HasValue)
                    cmd.Parameters.AddWithValue("@EmployeeId", user.EmployeeId);
                else
                    cmd.Parameters.AddWithValue("@EmployeeId", DBNull.Value);

                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error adding user: " + ex.Message);
                return false;
            }
        }

        #endregion

        #region UPDATE

        /// <summary>
        /// Update existing user.
        /// </summary>
        public bool UpdateUser(UserModel user)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                const string query = @"
                    UPDATE users 
                    SET email = @Email, password = @Password, role = @Role, employee_id = @EmployeeId
                    WHERE id = @Id";

                using var cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@Email", user.Email);
                cmd.Parameters.AddWithValue("@Password", user.Password);
                cmd.Parameters.AddWithValue("@Role", user.Role);
                cmd.Parameters.AddWithValue("@Id", user.Id);
                if (user.EmployeeId.HasValue)
                    cmd.Parameters.AddWithValue("@EmployeeId", user.EmployeeId);
                else
                    cmd.Parameters.AddWithValue("@EmployeeId", DBNull.Value);

                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error updating user: " + ex.Message);
                return false;
            }
        }

        #endregion

        #region DELETE

        /// <summary>
        /// Delete user by ID.
        /// </summary>
        public bool DeleteUserById(int id)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                using var cmd = new MySqlCommand("DELETE FROM users WHERE id = @id", connection);
                cmd.Parameters.AddWithValue("@id", id);

                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error deleting user: " + ex.Message);
                return false;
            }
        }

        #endregion
    }
}
