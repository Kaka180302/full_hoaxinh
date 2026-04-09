using System.ComponentModel.DataAnnotations;

namespace HoaXinhStore.Web.Entities;

public class OrderTimeline
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }

    [Required, MaxLength(40)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(40)]
    public string FromStatus { get; set; } = string.Empty;

    [MaxLength(40)]
    public string ToStatus { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Note { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
