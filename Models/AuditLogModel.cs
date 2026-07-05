#nullable enable
using System;

namespace SihyuPOSPayroll.Models
{
    public class AuditLogModel
    {
        public long Id { get; set; }
        public int? UserId { get; set; }
        public int? EmployeeId { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string? EntityType { get; set; }
        public int? EntityId { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? Description { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
