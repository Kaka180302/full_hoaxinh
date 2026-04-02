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

## 2026-04-02 - VNPAY integration + checkout form update
- Integrated VNPAY payment flow in MVC (no fetch API for checkout submit).

### Backend changes
- Added options: `Options/VnpayOptions.cs`
- Added service:
  - `Services/Payments/IVnpayService.cs`
  - `Services/Payments/VnpayService.cs`
- Registered DI in `Program.cs`:
  - `Configure<VnpayOptions>("Vnpay")`
  - `AddScoped<IVnpayService, VnpayService>()`
- Added checkout endpoint in `StoreController`:
  - `POST /Store/Checkout`
  - Creates order + payment record from posted form fields `Items[i].ProductId`, `Items[i].Quantity`
  - If `PaymentMethod = VNPAY`, redirects user to generated VNPAY URL.
  - If COD, redirects back with success message.
- Added return endpoint in `StoreController`:
  - `GET /Store/PaymentReturn`
  - Verifies VNPAY secure hash, amount (`x100`), response code
  - Updates `Orders.PaymentStatus` + `Payments.Status`
- Added IPN endpoint:
  - `GET /api/payment/vnpay-ipn`
  - Source-of-truth update from VNPAY server callback
  - Handles duplicate, invalid signature, invalid amount and final status updates.

### Frontend checkout form changes
- Updated order popup form in `Views/Store/Index.cshtml`:
  - form now posts to `asp-controller="Store" asp-action="Checkout"`
  - added anti-forgery token
  - payment options changed to:
    - `COD` (thanh toan khi nhan hang)
    - `VNPAY` (thanh toan qua ngan hang)
  - added hidden container `#orderItemsHidden` to post item list in classic MVC format.
- Updated `wwwroot/script.js`:
  - Removed async fetch submit to external API.
  - On submit, map selected rows to hidden inputs:
    - `Items[0].ProductId`, `Items[0].Quantity`, ...
  - Keep client-side validation before submit.
  - Keep QR display area when selecting `VNPAY`.

### Config required before real sandbox test
- File: `appsettings.json`
- Section:
```json
"Vnpay": {
  "TmnCode": "YOUR_VNPAY_TMN_CODE",
  "HashSecret": "YOUR_VNPAY_HASH_SECRET",
  "BaseUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
  "ReturnUrl": "https://localhost:7176/Store/PaymentReturn",
  "IpnUrl": "https://localhost:7176/api/payment/vnpay-ipn"
}
```
- Note: `ReturnUrl` / `IpnUrl` must be public HTTPS URL when testing with real VNPAY callbacks.

## 2026-04-02 - Cart fix + payment option boxes + VNPAY channel mapping
- Fixed cart add flow on storefront (`.product_hoverBtn`) by adding full localStorage cart logic in `wwwroot/script.js`:
  - add/update/remove quantity
  - select items for checkout
  - open order popup from selected cart items
- Order form layout updated to responsive width and payment cards (box style) instead of dropdown:
  - COD
  - Thanh toan qua ngan hang
  - QR Pay
  - ATM iBanking
  - Credit/Debit card
- Payment method mapping to VNPAY bank channel in checkout backend:
  - `VNPAY_QR` -> `vnp_BankCode=VNPAYQR`
  - `VNPAY_BANK` / `VNPAY_ATM` -> `vnp_BankCode=VNBANK`
  - `VNPAY_INTL` -> `vnp_BankCode=INTCARD`
- Updated VNPAY service signature to allow optional bank code when building payment URL.
- Replaced static QR image block with instruction panel for real VNPAY hosted payment page.

### Important behavior note
- The QR image, bank list, and app deep-link behavior are rendered by VNPAY hosted page after redirect.
- Merchant system sends order amount and order info; VNPAY UI handles provider-specific deep-link/app open behavior.

## 2026-04-02 - Payment return UX + email confirmation
- `Store/PaymentReturn` now always redirects user back to homepage and sets clear status message:
  - success
  - failed by code
  - cancelled (`vnp_ResponseCode=24`)
  - invalid signature / invalid amount
- Checkout now builds VNPAY `returnUrl` dynamically from current request host (`Url.Action(..., Request.Scheme)`) to avoid stale localhost/port issues.
- Added email notification on paid success:
  - Service: `Services/Notifications/SmtpEmailService.cs`
  - Triggered in both return flow and IPN flow when payment transitions to Paid.
  - Email content includes customer info, product table, and total amount.
- Added SMTP config section in `appsettings.json`.

## 2026-04-02 - Footer policy column + popup content
- Added new footer column `Chinh Sach` in `Views/Store/Partials/_Footer.cshtml` with 7 policy items:
  - Ve Chung Toi
  - Huong dan mua dan hang
  - Chinh sach thanh toan
  - Chinh sach bao hanh
  - Chinh sach giao nhan va van chuyen
  - Chinh sach doi tra va hoan tien
  - Chinh Sach Bao Mat Thong Tin Ca Nhan
- Added reusable popup `#policyPopup` in footer partial; each item opens popup with title + content + source link.
- Added footer policy and popup styles in `wwwroot/assets/style.css`.
- Added popup behavior + policy content map in `wwwroot/script.js`.
- Source references were collected from `https://hoaxinhstore.vn` footer links:
  - `/shopinfo/company.html`
  - `/member/huongdanmuahang.html`
  - `/member/agreement.html`
  - `/shopinfo/guide.html`
  - `/member/chinhsachgiaonhan.html`
  - `/member/chinhsachdoitravahoantien.html`
  - `/member/privacy.html`

## 2026-04-02 - Policy popup switched to full source content
- Replaced summarized policy text with full policy content extracted from source pages on `hoaxinhstore.vn`.
- `script.js` now renders policy popup body via `innerHTML` to preserve original structure/line-breaks.
- Popup body now has its own scroll area (`max-height` + `overflow-y: auto`) so long policy pages can be read fully inside modal.

## 2026-04-02 - Policy JSON + Admin editing
- Added file-based policy source: `Data/policies.json`.
- Added policy service:
  - `Services/Policies/IPolicyContentService.cs`
  - `Services/Policies/JsonPolicyContentService.cs`
  - `Services/Policies/PolicyContentItem.cs`
- Storefront now loads policy content from JSON:
  - `StoreController` injects `IPolicyContentService` and passes `PolicyData` to `StoreIndexViewModel`.
  - `Views/Store/Index.cshtml` exports `window.__policyData` for frontend popup.
  - `wwwroot/script.js` consumes `window.__policyData` instead of hardcoded policy block.
- Added Admin management UI for policy content:
  - Controller: `Areas/Admin/Controllers/PoliciesController.cs`
  - View: `Areas/Admin/Views/Policies/Index.cshtml`
  - View models: `ViewModels/Admin/PolicyEditViewModel.cs`
  - Admin navbar link added in `Areas/Admin/Views/Shared/_AdminLayout.cshtml`.

## 2026-04-02 - Popup scroll lock behavior
- Added global body scroll-lock helper in `wwwroot/script.js` (`syncGlobalScrollLock`).
- When any overlay UI is open, outside page scroll is blocked and user can scroll only inside popup/cart:
  - order popup (`.order.active`)
  - product detail popup (`.popup_product`)
  - policy popup (`.policy_popup.active`)
  - cart drawer (`.cart.active_cart`)
- Added `body.modal-lock` style in `wwwroot/assets/style.css`:
  - `overflow: hidden;`
  - `height: 100vh;`

## 2026-04-02 - Policy text cleanup + scroll lock stabilization
- Policy popup text normalization improved in `wwwroot/script.js`:
  - Convert legacy html snippets (`<br>`, `<p>`, `<li>`) to plain text lines.
  - Decode html entities and collapse excessive blank lines/spacing.
  - Remove noisy empty rows so policy content is readable in popup.
- Scroll lock behavior stabilized:
  - Preserve current page position when opening/closing popup (no jump to top).
  - Lock/unlock on both `html` and `body` class `modal-lock`.
  - Cart drawer lock behavior:
    - mobile: lock outside scroll
    - desktop: allow normal page scroll

## 2026-04-02 - Mobile footer accordion redesign
- Added dedicated mobile footer layout (desktop footer unchanged):
  - Contact card with logo, hotline, address, email.
  - Accordion sections: `Hỗ trợ`, `Dịch vụ`, `Trang website`, `Giờ làm việc`.
  - Tap to expand/collapse each section.
- Added policy shortcut buttons in accordion panels, reusing existing policy popup flow.
- Added icons for address/email on mobile contact card (reused existing desktop svg icons).
- Replaced accordion chevron with provided SVG arrow and rotate animation on expand.
- Files touched:
  - `Views/Store/Partials/_Footer.cshtml`
  - `wwwroot/assets/style.css`
  - `wwwroot/script.js`
