using System.ComponentModel.DataAnnotations;

namespace HoaXinhStore.Web.ViewModels.Admin;

public class LoginViewModel
{
    [Required]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }

    public string ReturnUrl { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;
}
