using System.ComponentModel.DataAnnotations;

namespace HoaXinhStore.Web.Entities;

public class Customer
{
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string CustomerType { get; set; } = "Guest";

    [Required, MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<CustomerAddress> Addresses { get; set; } = new List<CustomerAddress>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
