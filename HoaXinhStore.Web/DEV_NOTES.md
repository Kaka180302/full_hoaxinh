# HoaXinhStore - Developer Notes

## Date
- 2026-04-01

## Major changes delivered
1. Admin authentication and authorization (Identity + Role)
2. Admin Area with product/category/order management
3. Product loading on storefront changed to server-side rendering from database (no JS/API fetch for product list)
4. Handover documentation and migration SQL script for other developers

## 1) Identity + Role system
- Added `ApplicationUser`: `Entities/Identity/ApplicationUser.cs`
- `AppDbContext` now inherits `IdentityDbContext<ApplicationUser>`
- Configured Identity and cookie paths in `Program.cs`
  - Login path: `/Admin/Account/Login`
- Added startup seeder service:
  - `Services/Identity/AdminIdentitySeeder.cs`
  - Creates roles: `Admin`, `Editor`
  - Creates default admin account from `appsettings.json` -> `AdminAccount`

### Admin default account config
- File: `appsettings.json`
- Section:
```json
"AdminAccount": {
  "Email": "admin@hoaxinhstore.local",
  "Password": "Admin@123456",
  "FullName": "System Admin"
}
```

## 2) Admin Area
### Account
- `Areas/Admin/Controllers/AccountController.cs`
- Login/Logout implemented.

### Dashboard
- `Areas/Admin/Controllers/DashboardController.cs`
- Shows quick stats: products/categories/orders/pending orders.

### Product management (full MVP)
- `Areas/Admin/Controllers/ProductsController.cs`
- Features:
  - List + search + category filter + pagination
  - Create/Edit product
  - Upload image to `/wwwroot/uploads/products`
  - Toggle active/inactive
- Views:
  - `Areas/Admin/Views/Products/Index.cshtml`
  - `Areas/Admin/Views/Products/Edit.cshtml`
- ViewModel:
  - `ViewModels/Admin/AdminProductEditViewModel.cs`

### Category management
- `Areas/Admin/Controllers/CategoriesController.cs`
- Create/Edit/List category.
- Views:
  - `Areas/Admin/Views/Categories/Index.cshtml`
  - `Areas/Admin/Views/Categories/Edit.cshtml`
- ViewModel:
  - `ViewModels/Admin/CategoryEditViewModel.cs`

### Order management
- `Areas/Admin/Controllers/OrdersController.cs`
- List orders with pagination.
- View:
  - `Areas/Admin/Views/Orders/Index.cshtml`

### Admin shared layout
- `Areas/Admin/Views/Shared/_AdminLayout.cshtml`
- `Areas/Admin/Views/_ViewStart.cshtml`

## 3) Storefront product loading strategy
- Changed to SSR from database in `StoreController` + `Views/Store/Index.cshtml`
- No JS/API fetch for initial product list.
- Only lightweight client-side tab filter remains (`filterProducts`).

## 4) DB migration script for team
- Generated idempotent script:
  - `Database/migrations_idempotent.sql`
- Use this script on SQL Server to apply all EF migrations safely.

## Important update for existing manual database
- If the database was already created from `Database/init_hoaxinhstore.sql`, do **NOT** run `migrations_idempotent.sql` directly.
- Use this compatibility upgrade script instead:
  - `Database/upgrade_existing_db_for_admin.sql`
- This script:
  - adds missing backoffice columns only if absent,
  - creates Identity tables if absent,
  - inserts baseline rows into `__EFMigrationsHistory` to prevent EF from replaying old conflicting migrations.

## Deployment / setup checklist for next dev
1. Update connection string in `appsettings.json`
2. Run SQL migration script: `Database/migrations_idempotent.sql`
3. Verify `Categories` has `Slug` and `IsActive`, `Products` has `IsActive`
4. Start app: `dotnet run`
5. Login admin: `/Admin/Account/Login`

## Known caveats
- If Identity tables are missing, startup seeder logs warning and skips seed.
- Existing frontend `script.js` still handles cart/order popup behavior.

## Recommended next tasks
1. Add order detail page and status update actions in admin.
2. Add product image preview and validation for upload type/size.
3. Add audit fields (`UpdatedAtUtc`, `UpdatedBy`) in product/category/order updates.
4. Add integration tests for Admin controllers (auth + CRUD).

## VNPAY return/IPN flow (agreed)
- Reference doc: `https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html`
- Return URL is for user-facing result display. Typical params returned:
  - `vnp_TxnRef`
  - `vnp_Amount`
  - `vnp_OrderInfo`
  - `vnp_ResponseCode`
  - `vnp_TransactionStatus`
  - `vnp_TransactionNo`
  - `vnp_BankCode`
  - `vnp_PayDate`
  - `vnp_BankTranNo` (optional)
  - `vnp_CardType` (optional)
  - `vnp_SecureHashType` (optional)
  - `vnp_SecureHash`
- Backend must verify `vnp_SecureHash` before trusting response data.
- Do not finalize order state using Return URL only.
- Final payment state must be confirmed and updated by IPN endpoint (server-to-server).
- `vnp_Amount` uses smallest currency unit (`x100`) and must be validated against order amount in DB.
