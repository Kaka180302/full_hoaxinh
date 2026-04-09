using System.ComponentModel.DataAnnotations;

namespace HoaXinhStore.Web.ViewModels;

public class PreOrderViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductImage { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int AvailableQuantity { get; set; }

    [Range(1, 100000)]
    public int RequestedQuantity { get; set; } = 1;

    [Required(ErrorMessage = "Vui lòng nhập họ tên")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    public string PhoneNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;

    [Range(10, 30, ErrorMessage = "Tỷ lệ cọc chỉ từ 10% - 30%")]
    public int DepositPercent { get; set; } = 10;

    public decimal PreOrderAmount => UnitPrice * RequestedQuantity;
    public decimal DepositAmount => Math.Round(PreOrderAmount * DepositPercent / 100m, 0);
}
