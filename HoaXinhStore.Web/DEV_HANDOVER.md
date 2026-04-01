# HoaXinhStore Dev Handover

## Current architecture
- ASP.NET Core MVC (.NET 10), EF Core + SQL Server.
- `Store` front page uses server-side rendering (SSR) for categories and products.
- Admin management uses `Areas/Admin`.

## Auth and roles
- Identity enabled with `ApplicationUser`.
- Roles seeded: `Admin`, `Editor`.
- Admin login URL: `/Admin/Account/Login`.

## Backoffice modules
- Dashboard: KPIs.
- Products: list/search/filter/paging/create/edit/upload/toggle-active.
- Categories: list/create/edit.
- Orders: list/paging.

## Core files
- `Program.cs`
- `Data/AppDbContext.cs`
- `Entities/Identity/ApplicationUser.cs`
- `Services/Identity/AdminIdentitySeeder.cs`
- `Areas/Admin/*`
- `Controllers/StoreController.cs`
- `Views/Store/Index.cshtml`
- `DEV_NOTES.md`

## SQL and migration
- Idempotent migration script for team:
  - `Database/migrations_idempotent.sql`
- Base schema/seed script:
  - `Database/init_hoaxinhstore.sql`

## Important behavior decisions
- No JS/API product fetch on storefront list.
- Product/catalog data for menu and listing comes directly from SQL via MVC action.
- Existing JS still used for cart/order popup UX.

## Quick start for a new developer
1. Set SQL connection string in `appsettings.json`.
2. Run `Database/migrations_idempotent.sql` on SQL Server.
3. Run app: `dotnet run`.
4. Open `/Admin/Account/Login` and sign in with configured admin account.
