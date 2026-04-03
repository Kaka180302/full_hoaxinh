using HoaXinhStore.Web.Data;
using HoaXinhStore.Web.Entities.Identity;
using HoaXinhStore.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HoaXinhStore.Web.Areas.Admin.Controllers;

[Area("Admin")]
public class AccountController(
    AppDbContext db,
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

        var result = await signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: false);
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

        var session = new AdminLoginSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            UserName = user.UserName ?? user.Email ?? "admin",
            IpAddress = GetClientIp(),
            UserAgent = GetUserAgent(),
            CreatedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow
        };
        db.AdminLoginSessions.Add(session);
        await db.SaveChangesAsync();

        await signInManager.SignInWithClaimsAsync(
            user,
            model.RememberMe,
            [new Claim("admin_session_id", session.Id.ToString())]);

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
        await RevokeCurrentSessionAsync();
        await signInManager.SignOutAsync();
        return RedirectToAction("Login", "Account", new { area = "Admin" });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Sessions()
    {
        var userId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return RedirectToAction(nameof(Login));
        }

        var currentSessionId = User.FindFirstValue("admin_session_id") ?? string.Empty;
        var sessions = await db.AdminLoginSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync();

        ViewBag.CurrentSessionId = currentSessionId;
        return View(sessions);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogoutSession(Guid id)
    {
        var userId = userManager.GetUserId(User);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var session = await db.AdminLoginSessions
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId && s.RevokedAtUtc == null);
            if (session is not null)
            {
                session.RevokedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }

        var currentSessionRaw = User.FindFirstValue("admin_session_id");
        if (Guid.TryParse(currentSessionRaw, out var currentSessionId) && currentSessionId == id)
        {
            await signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        return RedirectToAction(nameof(Sessions));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogoutOtherSessions()
    {
        var userId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return RedirectToAction(nameof(Sessions));
        }

        var currentSessionRaw = User.FindFirstValue("admin_session_id");
        var query = db.AdminLoginSessions.Where(s => s.UserId == userId && s.RevokedAtUtc == null);
        if (Guid.TryParse(currentSessionRaw, out var currentSessionId))
        {
            query = query.Where(s => s.Id != currentSessionId);
        }

        var sessions = await query.ToListAsync();
        foreach (var session in sessions)
        {
            session.RevokedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Sessions));
    }

    private async Task RevokeCurrentSessionAsync()
    {
        var userId = userManager.GetUserId(User);
        var sessionIdRaw = User.FindFirstValue("admin_session_id");
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(sessionIdRaw, out var sessionId))
        {
            return;
        }

        var session = await db.AdminLoginSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId && s.RevokedAtUtc == null);
        if (session is null)
        {
            return;
        }

        session.RevokedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private string GetClientIp()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }

        return HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "unknown";
    }

    private string GetUserAgent()
    {
        var ua = Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(ua))
        {
            return "unknown";
        }

        return ua.Length > 1000 ? ua[..1000] : ua;
    }
}
