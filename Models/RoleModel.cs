#nullable enable
namespace SihyuPOSPayroll.Models
{
    /// <summary>
    /// Represents an application role stored in the <c>roles</c> table.
    /// Built-in roles: Admin, Cashier, Employee.
    /// </summary>
    public class RoleModel
    {
        public int    Id          { get; set; }
        public string Name        { get; set; } = string.Empty;   // "Admin" | "Cashier" | "Employee"
        public string Description { get; set; } = string.Empty;
    }
}
