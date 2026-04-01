using System.ComponentModel.DataAnnotations;

namespace HoaXinhStore.Web.Entities;

public class CustomerAddress
{
    public int Id { get; set; }

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    [Required, MaxLength(150)]
    public string ReceiverName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string AddressLine { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Ward { get; set; } = string.Empty;

    [MaxLength(100)]
    public string District { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Province { get; set; } = string.Empty;

    public bool IsDefault { get; set; }
}
