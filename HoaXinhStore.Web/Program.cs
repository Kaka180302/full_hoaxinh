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
        await db.Database.ExecuteSqlRawAsync(
            """
            IF COL_LENGTH(N'dbo.Products', N'SalePrice') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Products] ADD [SalePrice] DECIMAL(18,2) NULL;
            END
            """);
        await db.Database.ExecuteSqlRawAsync(
            """
            IF COL_LENGTH(N'dbo.Categories', N'SkuPrefix') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Categories] ADD [SkuPrefix] NVARCHAR(30) NOT NULL CONSTRAINT DF_Categories_SkuPrefix DEFAULT N'';
            END
            IF COL_LENGTH(N'dbo.Categories', N'ParentCategoryId') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Categories] ADD [ParentCategoryId] INT NULL;
                CREATE INDEX IX_Categories_ParentCategoryId ON [dbo].[Categories]([ParentCategoryId]);
                ALTER TABLE [dbo].[Categories] WITH CHECK ADD CONSTRAINT FK_Categories_Categories_ParentCategoryId FOREIGN KEY([ParentCategoryId]) REFERENCES [dbo].[Categories]([Id]);
            END
            IF COL_LENGTH(N'dbo.Products', N'BrandName') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Products] ADD [BrandName] NVARCHAR(120) NOT NULL CONSTRAINT DF_Products_BrandName DEFAULT N'';
            END
            IF COL_LENGTH(N'dbo.Products', N'BrandImageUrl') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Products] ADD [BrandImageUrl] NVARCHAR(500) NOT NULL CONSTRAINT DF_Products_BrandImageUrl DEFAULT N'';
            END
            IF COL_LENGTH(N'dbo.Products', N'BrandId') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Products] ADD [BrandId] INT NULL;
                CREATE INDEX IX_Products_BrandId ON [dbo].[Products]([BrandId]);
            END
            IF COL_LENGTH(N'dbo.Products', N'IsPreOrderEnabled') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Products] ADD [IsPreOrderEnabled] BIT NOT NULL CONSTRAINT DF_Products_IsPreOrderEnabled DEFAULT 1;
            END
            IF COL_LENGTH(N'dbo.OrderItems', N'UnitFactor') IS NULL
            BEGIN
                ALTER TABLE [dbo].[OrderItems] ADD [UnitFactor] INT NOT NULL CONSTRAINT DF_OrderItems_UnitFactor DEFAULT 1;
            END
            IF COL_LENGTH(N'dbo.OrderItems', N'UnitName') IS NULL
            BEGIN
                ALTER TABLE [dbo].[OrderItems] ADD [UnitName] NVARCHAR(80) NOT NULL CONSTRAINT DF_OrderItems_UnitName DEFAULT N'';
            END
            IF COL_LENGTH(N'dbo.OrderItems', N'IsPreOrder') IS NULL
            BEGIN
                ALTER TABLE [dbo].[OrderItems] ADD [IsPreOrder] BIT NOT NULL CONSTRAINT DF_OrderItems_IsPreOrder DEFAULT 0;
            END
            IF COL_LENGTH(N'dbo.OrderItems', N'VariantId') IS NULL
            BEGIN
                ALTER TABLE [dbo].[OrderItems] ADD [VariantId] INT NULL;
                CREATE INDEX IX_OrderItems_VariantId ON [dbo].[OrderItems]([VariantId]);
            END
            IF COL_LENGTH(N'dbo.OrderItems', N'VariantNameSnapshot') IS NULL
            BEGIN
                ALTER TABLE [dbo].[OrderItems] ADD [VariantNameSnapshot] NVARCHAR(120) NOT NULL CONSTRAINT DF_OrderItems_VariantNameSnapshot DEFAULT N'';
            END
            IF OBJECT_ID(N'dbo.ProductVariants', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ProductVariants](
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [ProductId] INT NOT NULL,
                    [Sku] NVARCHAR(80) NOT NULL,
                    [Name] NVARCHAR(120) NOT NULL,
                    [Price] DECIMAL(18,2) NOT NULL,
                    [SalePrice] DECIMAL(18,2) NULL,
                    [Barcode] NVARCHAR(80) NOT NULL CONSTRAINT DF_ProductVariants_Barcode DEFAULT N'',
                    [WeightGram] INT NULL,
                    [LengthMm] INT NULL,
                    [WidthMm] INT NULL,
                    [HeightMm] INT NULL,
                    [ImageUrl] NVARCHAR(500) NOT NULL CONSTRAINT DF_ProductVariants_ImageUrl DEFAULT N'',
                    [StockQuantity] INT NOT NULL CONSTRAINT DF_ProductVariants_Stock DEFAULT 0,
                    [IsActive] BIT NOT NULL CONSTRAINT DF_ProductVariants_IsActive DEFAULT 1,
                    [SortOrder] INT NOT NULL CONSTRAINT DF_ProductVariants_Sort DEFAULT 0,
                    CONSTRAINT FK_ProductVariants_Products_ProductId FOREIGN KEY([ProductId]) REFERENCES [dbo].[Products]([Id]) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX IX_ProductVariants_Sku ON [dbo].[ProductVariants]([Sku]);
                CREATE INDEX IX_ProductVariants_ProductId ON [dbo].[ProductVariants]([ProductId]);
            END
            IF COL_LENGTH(N'dbo.ProductVariants', N'Barcode') IS NULL
            BEGIN
                ALTER TABLE [dbo].[ProductVariants] ADD [Barcode] NVARCHAR(80) NOT NULL CONSTRAINT DF_ProductVariants_Barcode DEFAULT N'';
            END
            IF COL_LENGTH(N'dbo.ProductVariants', N'WeightGram') IS NULL
            BEGIN
                ALTER TABLE [dbo].[ProductVariants] ADD [WeightGram] INT NULL;
            END
            IF COL_LENGTH(N'dbo.ProductVariants', N'LengthMm') IS NULL
            BEGIN
                ALTER TABLE [dbo].[ProductVariants] ADD [LengthMm] INT NULL;
            END
            IF COL_LENGTH(N'dbo.ProductVariants', N'WidthMm') IS NULL
            BEGIN
                ALTER TABLE [dbo].[ProductVariants] ADD [WidthMm] INT NULL;
            END
            IF COL_LENGTH(N'dbo.ProductVariants', N'HeightMm') IS NULL
            BEGIN
                ALTER TABLE [dbo].[ProductVariants] ADD [HeightMm] INT NULL;
            END
            IF COL_LENGTH(N'dbo.ProductVariants', N'ImageUrl') IS NULL
            BEGIN
                ALTER TABLE [dbo].[ProductVariants] ADD [ImageUrl] NVARCHAR(500) NOT NULL CONSTRAINT DF_ProductVariants_ImageUrl DEFAULT N'';
            END
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_OrderItems_ProductVariants_VariantId')
            BEGIN
                ALTER TABLE [dbo].[OrderItems] WITH CHECK ADD CONSTRAINT [FK_OrderItems_ProductVariants_VariantId] FOREIGN KEY([VariantId]) REFERENCES [dbo].[ProductVariants]([Id]);
            END
            IF OBJECT_ID(N'dbo.OrderTimelines', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[OrderTimelines](
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [OrderId] INT NOT NULL,
                    [Action] NVARCHAR(40) NOT NULL,
                    [FromStatus] NVARCHAR(40) NOT NULL CONSTRAINT DF_OrderTimelines_FromStatus DEFAULT N'',
                    [ToStatus] NVARCHAR(40) NOT NULL CONSTRAINT DF_OrderTimelines_ToStatus DEFAULT N'',
                    [Note] NVARCHAR(500) NOT NULL CONSTRAINT DF_OrderTimelines_Note DEFAULT N'',
                    [CreatedAtUtc] DATETIME2 NOT NULL CONSTRAINT DF_OrderTimelines_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_OrderTimelines_Orders_OrderId FOREIGN KEY([OrderId]) REFERENCES [dbo].[Orders]([Id]) ON DELETE CASCADE
                );
                CREATE INDEX IX_OrderTimelines_OrderId ON [dbo].[OrderTimelines]([OrderId]);
            END
            IF OBJECT_ID(N'dbo.PreOrderRequests', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[PreOrderRequests](
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [ProductId] INT NOT NULL,
                    [ProductNameSnapshot] NVARCHAR(255) NOT NULL,
                    [ProductSkuSnapshot] NVARCHAR(50) NOT NULL,
                    [RequestedQuantity] INT NOT NULL,
                    [AvailableQuantity] INT NOT NULL,
                    [MissingQuantity] INT NOT NULL,
                    [CustomerName] NVARCHAR(120) NOT NULL,
                    [PhoneNumber] NVARCHAR(30) NOT NULL,
                    [Email] NVARCHAR(120) NOT NULL CONSTRAINT DF_PreOrderRequests_Email DEFAULT N'',
                    [Address] NVARCHAR(500) NOT NULL CONSTRAINT DF_PreOrderRequests_Address DEFAULT N'',
                    [DepositPercent] DECIMAL(5,2) NOT NULL,
                    [UnitPriceSnapshot] DECIMAL(18,2) NOT NULL,
                    [PreOrderAmount] DECIMAL(18,2) NOT NULL,
                    [DepositAmount] DECIMAL(18,2) NOT NULL,
                    [Status] NVARCHAR(30) NOT NULL CONSTRAINT DF_PreOrderRequests_Status DEFAULT N'Pending',
                    [CreatedAtUtc] DATETIME2 NOT NULL CONSTRAINT DF_PreOrderRequests_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT [FK_PreOrderRequests_Products_ProductId] FOREIGN KEY([ProductId]) REFERENCES [dbo].[Products]([Id])
                );
                CREATE INDEX IX_PreOrderRequests_CreatedAtUtc ON [dbo].[PreOrderRequests]([CreatedAtUtc]);
                CREATE INDEX IX_PreOrderRequests_ProductId ON [dbo].[PreOrderRequests]([ProductId]);
            END
            IF OBJECT_ID(N'dbo.CategoryBrands', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[CategoryBrands](
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [CategoryId] INT NOT NULL,
                    [Name] NVARCHAR(120) NOT NULL,
                    [ImageUrl] NVARCHAR(500) NOT NULL CONSTRAINT DF_CategoryBrands_ImageUrl DEFAULT N'',
                    [IsActive] BIT NOT NULL CONSTRAINT DF_CategoryBrands_IsActive DEFAULT 1,
                    CONSTRAINT [FK_CategoryBrands_Categories_CategoryId] FOREIGN KEY([CategoryId]) REFERENCES [dbo].[Categories]([Id]) ON DELETE CASCADE
                );
                CREATE INDEX IX_CategoryBrands_CategoryId ON [dbo].[CategoryBrands]([CategoryId]);
            END
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Products_CategoryBrands_BrandId')
            BEGIN
                ALTER TABLE [dbo].[Products] WITH CHECK ADD CONSTRAINT [FK_Products_CategoryBrands_BrandId] FOREIGN KEY([BrandId]) REFERENCES [dbo].[CategoryBrands]([Id]) ON DELETE SET NULL;
            END
            IF OBJECT_ID(N'dbo.VariantUnitPresets', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[VariantUnitPresets](
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [Name] NVARCHAR(120) NOT NULL,
                    [UnitTemplate] NVARCHAR(40) NOT NULL CONSTRAINT DF_VariantUnitPresets_UnitTemplate DEFAULT N'single',
                    [Unit2Name] NVARCHAR(80) NOT NULL CONSTRAINT DF_VariantUnitPresets_Unit2Name DEFAULT N'Hộp',
                    [Unit2Factor] INT NOT NULL CONSTRAINT DF_VariantUnitPresets_Unit2Factor DEFAULT 10,
                    [Unit3Name] NVARCHAR(80) NOT NULL CONSTRAINT DF_VariantUnitPresets_Unit3Name DEFAULT N'Thùng',
                    [Unit3Factor] INT NOT NULL CONSTRAINT DF_VariantUnitPresets_Unit3Factor DEFAULT 20,
                    [IsActive] BIT NOT NULL CONSTRAINT DF_VariantUnitPresets_IsActive DEFAULT 1
                );
                CREATE UNIQUE INDEX IX_VariantUnitPresets_Name ON [dbo].[VariantUnitPresets]([Name]);
            END
            IF OBJECT_ID(N'dbo.ProductAttributes', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ProductAttributes](
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [Name] NVARCHAR(120) NOT NULL,
                    [Description] NVARCHAR(500) NOT NULL CONSTRAINT DF_ProductAttributes_Description DEFAULT N'',
                    [SortOrder] INT NOT NULL CONSTRAINT DF_ProductAttributes_SortOrder DEFAULT 0,
                    [IsActive] BIT NOT NULL CONSTRAINT DF_ProductAttributes_IsActive DEFAULT 1
                );
                CREATE UNIQUE INDEX IX_ProductAttributes_Name ON [dbo].[ProductAttributes]([Name]);
            END
            IF OBJECT_ID(N'dbo.ProductAttributeValues', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ProductAttributeValues](
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [ProductAttributeId] INT NOT NULL,
                    [Value] NVARCHAR(120) NOT NULL,
                    [SortOrder] INT NOT NULL CONSTRAINT DF_ProductAttributeValues_SortOrder DEFAULT 0,
                    [IsActive] BIT NOT NULL CONSTRAINT DF_ProductAttributeValues_IsActive DEFAULT 1,
                    CONSTRAINT FK_ProductAttributeValues_ProductAttributes_ProductAttributeId FOREIGN KEY([ProductAttributeId]) REFERENCES [dbo].[ProductAttributes]([Id]) ON DELETE CASCADE
                );
                CREATE INDEX IX_ProductAttributeValues_ProductAttributeId ON [dbo].[ProductAttributeValues]([ProductAttributeId]);
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
