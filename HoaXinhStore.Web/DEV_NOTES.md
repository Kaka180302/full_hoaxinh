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
## 2026-04-02 - VNPAY flow hardening + admin localization + payment UX simplification
- Reviewed VNPAY sandbox Pay documentation and aligned payment URL generation in `Services/Payments/VnpayService.cs`:
  - keep gateway url `https://sandbox.vnpayment.vn/paymentv2/vpcpay.html`
  - preserve HMAC SHA512 signing and ordered query construction
  - sanitize `vnp_OrderInfo` to stable ASCII format for gateway compatibility
- Stabilized payment return flow in `Controllers/StoreController.cs`:
  - `returnUrl` generated from current request host/scheme for active environment
  - callback always redirects to homepage with checkout status message
- Fixed checkout message transport in `Views/Store/Index.cshtml`:
  - serialize `window.__checkoutMessage` / `window.__checkoutStatus` via JSON to avoid JS break on special chars.
- Updated customer-facing checkout messages in `StoreController` to full Vietnamese with dấu.
- Admin improvements:
  - Added product detail page:
    - controller action `Areas/Admin/Controllers/ProductsController.cs::Details`
    - view `Areas/Admin/Views/Products/Details.cshtml`
    - `Xem chi tiết` button on product list
  - Localized admin pages to Vietnamese:
    - login page labels/buttons
    - dashboard/order/category/product/policy view titles and status labels
    - admin layout `<html lang="vi">` and title suffix
  - Added Vietnamese display names and validation messages in admin viewmodels:
    - `ViewModels/Admin/AdminProductEditViewModel.cs`
    - `ViewModels/Admin/CategoryEditViewModel.cs`
    - `ViewModels/Admin/LoginViewModel.cs`
- Payment option UX simplified in storefront order popup (`Views/Store/Index.cshtml`):
  - reduced to 2 options: `COD` and `Thanh toán Online`
  - removed separate ATM iBanking card from popup UI
  - online method now routes to VNPAY page where user selects bank / QR / international card
- Updated redirect loading text in `wwwroot/script.js` to Vietnamese with dấu:
  - `Đang chuyển hướng...`
## 2026-04-03 - Theo doi don hang + quan tri don hang nang cao + webhook van chuyen
- Bo sung trang tra cuu don hang cho khach: `GET/POST /Store/TrackOrder`.
  - Khach nhap `Ma don` + `So dien thoai` de xem ngay trang thai don, thanh toan, van don, don vi van chuyen.
  - Ho tro deep-link truc tiep: `/Store/TrackOrder?orderNo=...&phoneNumber=...`.
- Cap nhat mail cam on/xac nhan thanh toan gui khach:
  - Them dong `Theo doi don hang` kem link track rieng theo tung don.
  - Link duoc tao dong tu host hien tai trong `StoreController` va `Api/PaymentController`.
- Nang cap trang Admin Don hang:
  - Tach 2 tab: `Chua hoan thanh` va `Da hoan thanh`.
  - Them bo loc/tim kiem theo: tu khoa (ma don/ten/sdt/email), trang thai don, trang thai thanh toan.
  - Them nut `Xem chi tiet` va man hinh chi tiet don de cap nhat trang thai nhanh.
- Bo sung cap nhat van chuyen qua API webhook:
  - Endpoint: `POST /api/shipping/webhook`.
  - Header bao mat: `X-Shipping-Key` (doi chieu voi `ShippingIntegration:WebhookKey`).
  - Body mau:
    {
      "orderNo": "HX...",
      "carrier": "GHN",
      "trackingCode": "ABC123",
      "status": "shipping",
      "note": "Dang tren duong giao"
    }
  - Mapping trang thai webhook -> he thong:
    - `pending` -> `Confirmed`
    - `picked` -> `Preparing`
    - `shipping`/`in_transit` -> `Shipping`
    - `delivered` -> `Completed`
    - `failed` -> `DeliveryFailed`
    - `cancelled` -> `Cancelled`
    - `returned` -> `Returned`
- Da them cau hinh webhook trong `appsettings.json`:
  - `ShippingIntegration:WebhookKey`

### Luu y van hanh
- Can doi `ShippingIntegration:WebhookKey` sang key thuc te truoc khi public.
- Link track chua duoc ma hoa token, hien dang dung cap `orderNo + phone` de khop don.
- Nen gioi han IP/ky chu ky phia webhook neu dau noi don vi van chuyen thuc te.
## 2026-04-03 - Mail 2 chieu cho COD va Online + mau webhook don vi van chuyen
- Bo sung gui mail ngay khi bam Dat mua (ca COD va Online):
  - Khach nhan mail `Da tiep nhan don hang` + link theo doi don rieng.
  - Cong ty nhan mail `Don hang moi` cung thong tin day du.
- Luong Online van giu mail `Xac nhan thanh toan thanh cong` khi VNPAY tra ket qua thanh cong.
- Cap nhat service email:
  - Interface: `IEmailService.SendOrderPlacedAsync(...)`
  - Implementation: `SmtpEmailService` them template mail tiep nhan don.
- Them tai lieu mau payload dau noi webhook van chuyen:
  - `Docs/ShippingWebhookSamples.md`
  - Co san mau cho GHN, GHTK, Ahamove va lenh test curl.
## 2026-04-03 - Bao mat phien admin + lich su dang nhap
- Bo sung bang `AdminLoginSessions` de luu lich su dang nhap admin:
  - UserId, UserName, IP, UserAgent, CreatedAtUtc, LastSeenAtUtc, RevokedAtUtc.
- App startup tu dong tao bang neu chua ton tai (IF OBJECT_ID ... CREATE TABLE) de khong can migration thu cong.
- Login admin:
  - Khong dung sign-in truc tiep nua; xac thuc mat khau -> tao session row -> sign-in kem claim `admin_session_id`.
- Cookie validation:
  - Moi request admin se doi chieu `admin_session_id` voi DB.
  - Neu session bi revoke/khong ton tai => reject principal + signout ngay.
- Bo sung trang quan ly phien dang nhap:
  - URL: `/Admin/Account/Sessions`
  - Xem lich su dang nhap theo thoi gian.
  - Nut `Dang xuat phien nay`.
  - Nut `Dang xuat tat ca phien khac`.
- Them menu `Lich su dang nhap` tren navbar admin.
## 2026-04-03 - Hoan thien quan tri don hang, email va bao mat dang nhap admin
- Quan tri don hang:
  - Them phan trang server-side cho 2 tab `Chua hoan thanh` / `Da hoan thanh`.
  - Giu nguyen bo loc/tim kiem khi chuyen trang.
  - Fix loi EF LINQ khong translate duoc ham custom `IsCompletedStatus` trong query (doi sang dieu kien truc tiep `OrderStatus ==/!= "Completed"`).
- Email thong bao don moi cho cong ty:
  - Them dong `Phuong thuc thanh toan` de ke toan de doi soat (COD/VNPAY).
- Bao mat dang nhap admin:
  - Bo sung checkbox `Ghi nho dang nhap tren thiet bi nay` o form login.
  - Bo sung lich su dang nhap + quan ly session admin:
    - Xem danh sach phien dang nhap (`/Admin/Account/Sessions`).
    - Dang xuat tung phien hoac dang xuat tat ca phien khac.
    - Moi request doi chieu session voi DB; session bi revoke se bi buoc dang xuat.
  - Chuyen trang Sessions vao layout admin (`_AdminLayout`) de dong bo giao dien quan tri.
## Stock / Dat Truoc Flow (2026-04-09)

- Luong chuan khi khach chon so luong lon hon ton kho:
  - He thong thong bao: "San pham hien chi con X trong kho".
  - Lua chon 1: Dong y mua theo so luong con lai -> tu dong dieu chinh so luong ve bang ton kho va tiep tuc thanh toan.
  - Lua chon 2: Dat truoc phan con thieu -> mo form dat truoc.
  - Lua chon 3: Khong dong y -> dong thong bao, giu nguyen trang thai hien tai.
- Neu ton kho = 0:
  - Trang chi tiet: hien "Tam het hang", hien "So luong con lai: 0".
  - Nut "Dat mua" doi thanh "Dat truoc".
  - Nut "Them vao gio" bi vo hieu hoa.
  - Tren card san pham hien nhan "Dat truoc" o goc phai tren anh (position absolute).
- Form dat truoc:
  - URL: `/Store/PreOrder?productId=...&requestedQty=...`
  - Cho phep chon ty le coc tu 10% den 30%.
  - He thong tinh san tong tien dat truoc + tien coc can thanh toan.
