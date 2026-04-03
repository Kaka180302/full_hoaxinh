using HoaXinhStore.Web.Data;
using HoaXinhStore.Web.Entities.Identity;
using HoaXinhStore.Web.Options;
using HoaXinhStore.Web.Services.Identity;
using HoaXinhStore.Web.Services.Notifications;
using HoaXinhStore.Web.Services.Payments;
using HoaXinhStore.Web.Services.Policies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<AdminAccountOptions>(builder.Configuration.GetSection("AdminAccount"));
builder.Services.Configure<VnpayOptions>(builder.Configuration.GetSection("Vnpay"));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<ShippingIntegrationOptions>(builder.Configuration.GetSection("ShippingIntegration"));
builder.Services.AddScoped<IVnpayService, VnpayService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddSingleton<IPolicyContentService, JsonPolicyContentService>();

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "HoaXinh.Admin.Auth.v2";
    options.LoginPath = "/Admin/Account/Login";
    options.AccessDeniedPath = "/Admin/Account/Login";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events.OnValidatePrincipal = async context =>
    {
        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var sessionIdRaw = context.Principal?.FindFirstValue("admin_session_id");
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(sessionIdRaw, out var sessionId))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync();
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var session = await db.AdminLoginSessions.FirstOrDefaultAsync(s =>
            s.Id == sessionId && s.UserId == userId && s.RevokedAtUtc == null);

        if (session is null)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync();
            return;
        }

        session.LastSeenAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    };
});

builder.Services.AddScoped<AdminIdentitySeeder>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'dbo.AdminLoginSessions', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[AdminLoginSessions](
                    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    [UserId] NVARCHAR(450) NOT NULL,
                    [UserName] NVARCHAR(256) NOT NULL,
                    [IpAddress] NVARCHAR(64) NOT NULL,
                    [UserAgent] NVARCHAR(1024) NOT NULL,
                    [CreatedAtUtc] DATETIME2 NOT NULL,
                    [LastSeenAtUtc] DATETIME2 NULL,
                    [RevokedAtUtc] DATETIME2 NULL
                );
                CREATE INDEX IX_AdminLoginSessions_UserId ON [dbo].[AdminLoginSessions]([UserId]);
                CREATE INDEX IX_AdminLoginSessions_CreatedAtUtc ON [dbo].[AdminLoginSessions]([CreatedAtUtc]);
            END
            """);

        var seed = scope.ServiceProvider.GetRequiredService<AdminIdentitySeeder>();
        await seed.SeedAsync();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "AdminIdentitySeeder skipped. Ensure identity tables are created and rerun.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Store}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
