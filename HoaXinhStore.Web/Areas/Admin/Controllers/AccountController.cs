using HoaXinhStore.Web.Entities.Identity;
using HoaXinhStore.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HoaXinhStore.Web.Areas.Admin.Controllers;

[Area("Admin")]
public class AccountController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl ?? string.Empty });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.ErrorMessage = "Vui lòng nhập tài khoản và mật khẩu.";
            return View(model);
        }

        var normalizedInput = model.UsernameOrEmail.Trim();
        var user = await userManager.FindByNameAsync(normalizedInput)
                   ?? await userManager.FindByEmailAsync(normalizedInput);

        if (user is null)
        {
            model.ErrorMessage = "Tài khoản không tồn tại.";
            return View(model);
        }

        var isAdmin = await userManager.IsInRoleAsync(user, "Admin");
        if (!isAdmin)
        {
            model.ErrorMessage = "Tài khoản không có quyền quản trị.";
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: false);
        if (result.IsLockedOut)
        {
            model.ErrorMessage = "Tài khoản đang bị khóa tạm thời.";
            return View(model);
        }

        if (result.IsNotAllowed)
        {
            model.ErrorMessage = "Tài khoản chưa được phép đăng nhập.";
            return View(model);
        }

        if (!result.Succeeded)
        {
            model.ErrorMessage = "Mật khẩu không đúng.";
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction("Login", "Account", new { area = "Admin" });
    }
}
