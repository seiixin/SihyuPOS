using SihyuPOSPayroll.Models;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;

namespace SihyuPOSPayroll.Services
{
    public class MenuService
    {
        private readonly string _connectionString;

        public MenuService()
        {
            _connectionString = "server=localhost;user=root;password=;database=sihyu_pos;";
        }

        public List<MenuModel> GetAllMenuItems()
        {
            var menuItems = new List<MenuModel>();

            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                string query = "SELECT Id, Name, Category, Price, image_url, Description, Created_At FROM Menu";

                using var command = new MySqlCommand(query, connection);
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    menuItems.Add(new MenuModel
                    {
                        Id = reader.GetInt32("Id"),
                        Name = reader.IsDBNull("Name") ? string.Empty : reader.GetString("Name"),
                        Category = reader.IsDBNull("Category") ? string.Empty : reader.GetString("Category"),
                        Price = reader.IsDBNull("Price") ? 0 : reader.GetDecimal("Price"),
                        ImageUrl = reader.IsDBNull("image_url") ? string.Empty : reader.GetString("image_url"),
                        Description = reader.IsDBNull("Description") ? string.Empty : reader.GetString("Description"),
                        CreatedAt = reader.IsDBNull("Created_At") ? DateTime.MinValue : reader.GetDateTime("Created_At")
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error retrieving menu items.", ex);
            }

            return menuItems;
        }

        public void AddMenuItem(MenuModel menuItem)
        {
            if (menuItem == null) throw new ArgumentNullException(nameof(menuItem));

            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                string query = @"
                    INSERT INTO Menu (Name, Category, Price, image_url, Description, Created_At)
                    VALUES (@Name, @Category, @Price, @ImageUrl, @Description, @CreatedAt)";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@Name", menuItem.Name ?? string.Empty);
                command.Parameters.AddWithValue("@Category", menuItem.Category ?? string.Empty);
                command.Parameters.AddWithValue("@Price", menuItem.Price);
                command.Parameters.AddWithValue("@ImageUrl", menuItem.ImageUrl ?? string.Empty);
                command.Parameters.AddWithValue("@Description", menuItem.Description ?? string.Empty);
                command.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception("Error adding menu item.", ex);
            }
        }

        public void UpdateMenuItem(MenuModel menuItem)
        {
            if (menuItem == null) throw new ArgumentNullException(nameof(menuItem));

            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                string query = @"
                    UPDATE Menu
                    SET Name = @Name, Category = @Category, Price = @Price,
                        image_url = @ImageUrl, Description = @Description
                    WHERE Id = @Id";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@Id", menuItem.Id);
                command.Parameters.AddWithValue("@Name", menuItem.Name ?? string.Empty);
                command.Parameters.AddWithValue("@Category", menuItem.Category ?? string.Empty);
                command.Parameters.AddWithValue("@Price", menuItem.Price);
                command.Parameters.AddWithValue("@ImageUrl", menuItem.ImageUrl ?? string.Empty);
                command.Parameters.AddWithValue("@Description", menuItem.Description ?? string.Empty);

                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception("Error updating menu item.", ex);
            }
        }

        public void DeleteMenuItem(int menuItemId)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                string query = "DELETE FROM Menu WHERE Id = @Id";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@Id", menuItemId);

                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception("Error deleting menu item.", ex);
            }
        }

        // Optional legacy alias
        public void InsertMenuItem(MenuModel menuItem)
        {
            AddMenuItem(menuItem);
        }
    }
}
