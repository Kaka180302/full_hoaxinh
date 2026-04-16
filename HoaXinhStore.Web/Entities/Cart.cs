using System.ComponentModel.DataAnnotations;

namespace HoaXinhStore.Web.Entities;

public class Cart
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Token { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<CartItem> Items { get; set; } = [];
}

