using System.ComponentModel.DataAnnotations;

namespace HoaXinhStore.Web.ViewModels.Admin;

public class LoginViewModel
{
    [Display(Name = "Tài khoản")]
    [Required(ErrorMessage = "Vui lòng nhập tài khoản hoặc email.")]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [Display(Name = "Mật khẩu")]
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu."), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }

    public string ReturnUrl { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;
}
