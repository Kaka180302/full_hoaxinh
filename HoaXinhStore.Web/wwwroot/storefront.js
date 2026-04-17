(function () {
    let hxModalEl = null;
    let hxModalDismissHandler = null;
    let cartCache = [];
    let cartInitPromise = null;

    function readCart() { return Array.isArray(cartCache) ? cartCache : []; }

    function writeCart(items) {
        cartCache = normalizeCart(items);
        persistCartToServer(cartCache);
    }

    async function loadCartFromServer() {
        try {
            const res = await fetch("/Store/CartItems", { cache: "no-store" });
            if (!res.ok) { cartCache = []; return; }
            const data = await res.json();
            const items = Array.isArray(data?.items) ? data.items : [];
            cartCache = normalizeCart(items.map(i => ({
                id: Number(i.productId || i.id || 0),
                variantId: i.variantId ? Number(i.variantId) : null,
                variantName: i.variantName || "",
                name: i.name || "",
                qty: Math.max(1, Number(i.qty || i.quantity || 1)),
                price: Number(i.price || 0),
                originalPrice: Number(i.originalPrice || i.price || 0),
                salePrice: i.salePrice != null ? Number(i.salePrice) : null,
                image: i.image || "",
                stock: Math.max(0, Number(i.stock || 0)),
                unitName: i.unitName || "",
                unitFactor: Math.max(1, Number(i.unitFactor || 1)),
                checked: i.checked !== false
            })));
        } catch { cartCache = []; }
    }

    function ensureCartReady() {
        if (!cartInitPromise) cartInitPromise = loadCartFromServer();
        return cartInitPromise;
    }

    function persistCartToServer(items) {
        const payload = {
            items: normalizeCart(items).map(i => ({
                productId: Number(i.id || 0),
                variantId: i.variantId ? Number(i.variantId) : null,
                qty: Math.max(1, Number(i.qty || 1)),
                checked: i.checked !== false,
                unitName: i.unitName || i.variantName || "",
                unitFactor: Math.max(1, Number(i.unitFactor || 1))
            }))
        };
        fetch("/Store/CartReplace", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        }).catch(() => { });
    }

    function normalizeCart(items) {
        return (Array.isArray(items) ? items : []).map(item => ({
            ...item,
            checked: item.checked !== false,
            unitFactor: Math.max(1, Number(item.unitFactor || 1)),
            unitName: item.unitName || "",
            unitOptions: Array.isArray(item.unitOptions) ? item.unitOptions : []
        }));
    }

    function formatVnd(value) {
        return Number(value || 0).toLocaleString("vi-VN") + " đ";
    }

    function getEffectivePrice(product) {
        return Number(product.salePrice ?? product.price ?? 0);
    }

    function ensureModal() {
        if (hxModalEl) return hxModalEl;
        hxModalEl = document.createElement("div");
        hxModalEl.className = "hx-popup-backdrop";
        hxModalEl.innerHTML = '<div class="hx-popup-card"><button type="button" class="hx-popup-close">×</button><div class="hx-popup-body"></div></div>';
        hxModalEl.style.display = "none";
        document.body.appendChild(hxModalEl);
        hxModalEl.querySelector(".hx-popup-close")?.addEventListener("click", () => closeModal({ reason: "dismiss" }));
        hxModalEl.addEventListener("click", (e) => { if (e.target === hxModalEl) closeModal({ reason: "dismiss" }); });
        return hxModalEl;
    }

    function closeModal(options = {}) {
        const modal = ensureModal();
        const reason = String(options.reason || "dismiss");
        const silent = options.silent === true;
        const dismissHandler = hxModalDismissHandler;
        hxModalDismissHandler = null;
        modal.classList.remove("is-open", "hx-popup-success", "hx-popup-error");
        modal.style.display = "none";
        if (!silent && typeof dismissHandler === "function") {
            dismissHandler(reason);
        }
    }

    function showNoticeModal(message, title = "Thông báo", status = "info") {
        const modal = ensureModal();
        hxModalDismissHandler = null;
        modal.classList.remove("hx-popup-success", "hx-popup-error");
        if (status === "success") modal.classList.add("hx-popup-success");
        if (status === "error") modal.classList.add("hx-popup-error");
        const body = modal.querySelector(".hx-popup-body");
        if (!body) return;
        body.innerHTML = `
            <h4>${title}</h4>
            <p>${message}</p>
            <div class="hx-popup-actions">
                <button type="button" class="mini-cart-view-btn" id="hxPopupOkBtn">Đã hiểu</button>
            </div>
        `;
        modal.style.display = "flex";
        requestAnimationFrame(() => modal.classList.add("is-open"));
        body.querySelector("#hxPopupOkBtn")?.addEventListener("click", () => closeModal({ reason: "ack" }));
    }

    function showQrPayPopup(onConfirmPaid, onCancelled) {
        const modal = ensureModal();
        modal.classList.remove("hx-popup-success", "hx-popup-error");
        const body = modal.querySelector(".hx-popup-body");
        if (!body) return;
        hxModalDismissHandler = (reason) => {
            if (reason === "dismiss" && typeof onCancelled === "function") {
                onCancelled();
            }
        };
        body.innerHTML = `
            <h4>Quét mã QR để thanh toán</h4>
            <p>Vui lòng quét mã bên dưới và hoàn tất chuyển khoản cho đơn hàng của bạn.</p>
            <div class="hx-qrpay-popup">
                <img src="/assets/img/QR_cty.jpg" alt="Mã QR thanh toán Hoa Xinh Store" />
            </div>
            <div class="hx-popup-actions">
                <button type="button" class="mini-cart-view-btn" id="hxQrPayConfirmBtn">Tôi đã chuyển khoản</button>
            </div>
        `;
        modal.style.display = "flex";
        requestAnimationFrame(() => modal.classList.add("is-open"));
        body.querySelector("#hxQrPayConfirmBtn")?.addEventListener("click", () => {
            closeModal({ reason: "confirmed" });
            if (typeof onConfirmPaid === "function") {
                onConfirmPaid();
            }
        });
    }

    function showAutoQrDynamicPopup(orderNo, amountText, qrImageUrl, onConfirmPaid, onCancelled) {
        const modal = ensureModal();
        modal.classList.remove("hx-popup-success", "hx-popup-error");
        const body = modal.querySelector(".hx-popup-body");
        if (!body) return;
        hxModalDismissHandler = (reason) => {
            if (reason === "dismiss" && typeof onCancelled === "function") {
                onCancelled();
            }
        };
        body.innerHTML = `
            <h4>Thanh toán QR tự động</h4>
            <p>Đơn <strong>${orderNo || ""}</strong> - Số tiền: <strong>${amountText || ""}</strong></p>
            <div class="hx-qrpay-popup">
                <img src="${qrImageUrl}" alt="Mã QR tự động thanh toán" />
            </div>
            <div class="hx-popup-actions">
                <button type="button" class="mini-cart-view-btn" id="hxAutoQrConfirmBtn">Tôi đã chuyển khoản</button>
            </div>
        `;
        modal.style.display = "flex";
        requestAnimationFrame(() => modal.classList.add("is-open"));
        body.querySelector("#hxAutoQrConfirmBtn")?.addEventListener("click", () => {
            if (typeof onConfirmPaid === "function") {
                onConfirmPaid();
            }
        });
    }

    function showPreOrderModal(item, qty = 1) {
        const modal = ensureModal();
        hxModalDismissHandler = null;
        const body = modal.querySelector(".hx-popup-body");
        if (!body) return;
        body.innerHTML = `
            <div class="hx-preorder-popup">
            <h4>Đặt trước sản phẩm</h4>
            <p class="hx-preorder-title"><strong>${item.name || ""}</strong></p>
            <form id="hxPreOrderForm" class="hx-preorder-form">
                <input type="hidden" name="productId" value="${item.id}" />
                <div class="hx-preorder-qty">
                    <label for="hxPreOrderQtyInput">Số lượng</label>
                    <div class="hx-qty-control">
                        <button type="button" class="hx-qty-btn" data-qty-minus>-</button>
                        <input id="hxPreOrderQtyInput" type="number" name="quantity" min="1" value="${Math.max(1, Number(qty || 1))}" class="form-control hx-qty-input" />
                        <button type="button" class="hx-qty-btn" data-qty-plus>+</button>
                    </div>
                </div>
                <input type="text" name="customerName" class="form-control" placeholder="Họ tên" required />
                <input type="text" name="phoneNumber" class="form-control" placeholder="Số điện thoại" required />
                <input type="text" name="address" class="form-control" placeholder="Địa chỉ" required />
                <input type="email" name="email" class="form-control" placeholder="Email (không bắt buộc)" />
                <textarea name="note" class="form-control" rows="3" placeholder="Ghi chú"></textarea>
                <button type="submit" class="mini-cart-view-btn">Gửi yêu cầu đặt trước</button>
            </form>
            <div id="hxPreOrderMsg" class="hx-preorder-msg"></div>
            </div>
        `;
        modal.style.display = "flex";
        requestAnimationFrame(() => modal.classList.add("is-open"));
        const form = body.querySelector("#hxPreOrderForm");
        const msg = body.querySelector("#hxPreOrderMsg");
        const qtyInput = body.querySelector("#hxPreOrderQtyInput");
        body.querySelector("[data-qty-minus]")?.addEventListener("click", () => {
            if (!qtyInput) return;
            qtyInput.value = String(Math.max(1, Number(qtyInput.value || 1) - 1));
        });
        body.querySelector("[data-qty-plus]")?.addEventListener("click", () => {
            if (!qtyInput) return;
            qtyInput.value = String(Math.max(1, Number(qtyInput.value || 1) + 1));
        });
        form?.addEventListener("submit", async (e) => {
            e.preventDefault();
            const fd = new FormData(form);
            const productIdFromPath = (() => {
                const m = window.location.pathname.match(/\/Store\/ProductDetail\/(\d+)/i);
                return m ? Number(m[1] || 0) : 0;
            })();
            const customerName = String(fd.get("customerName") || "").trim();
            const phoneNumber = String(fd.get("phoneNumber") || "").trim();
            const quantity = Math.max(1, Number(fd.get("quantity") || 1));
            const payload = {
                productId: Number(fd.get("productId") || 0) || productIdFromPath,
                quantity: quantity,
                customerName: customerName,
                phoneNumber: phoneNumber,
                email: String(fd.get("email") || "").trim(),
                address: String(fd.get("address") || "").trim(),
                note: String(fd.get("note") || "").trim()
            };
            if (!payload.productId || !payload.customerName || !payload.phoneNumber || !payload.address) {
                if (msg) msg.textContent = "Vui lòng nhập đủ thông tin bắt buộc.";
                return;
            }
            const res = await fetch("/Store/SubmitPreOrderPopup", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });
            let data = null;
            try {
                data = await res.json();
            } catch {
                data = { ok: false, message: "Không thể gửi yêu cầu đặt trước." };
            }
            if (data.ok) {
                closeModal();
                showNoticeModal(
                    data?.message || "Yêu cầu đặt trước đã được gửi thành công.",
                    data?.title || "Đặt trước thành công",
                    "success"
                );
            } else if (msg) {
                msg.textContent = data?.message || "Không thể gửi yêu cầu.";
            }
        });
    }

    function handleStockOverflow(item, desiredQty) {
        const stock = Math.max(0, Number(item.stock || 0));
        if (stock === 0) {
            showPreOrderModal(item, desiredQty);
            return { action: "stop", qty: Number(item.qty || 1) };
        }

        if (desiredQty <= stock) {
            return { action: "ok", qty: desiredQty };
        }
        showNoticeModal(`Sản phẩm hiện chỉ còn ${stock} trong kho.`);
        return { action: "cancel", qty: Number(item.qty || 1) };
    }

    async function addToCart(product, qty = 1) {
        await ensureCartReady();
        const cart = readCart();
        const found = cart.find(x => Number(x.id) === Number(product.id) && Number(x.variantId || 0) === Number(product.variantId || 0));
        const effectivePrice = getEffectivePrice(product);
        const stock = Math.max(0, Number(product.stock || 0));
        const nextQty = Number(found?.qty || 0) + Math.max(1, Number(qty || 1));
        const stockDecision = handleStockOverflow({ ...product, stock, qty: Number(found?.qty || 0) }, nextQty);
        if (stockDecision.action === "stop" || stockDecision.action === "cancel") {
            return;
        }

        if (found) {
            found.qty = Math.max(1, Number(stockDecision.qty || (Number(found.qty || 1) + Number(qty || 1))));
            found.price = effectivePrice;
            found.originalPrice = Number(product.price || 0);
            found.salePrice = product.salePrice != null ? Number(product.salePrice) : null;
            found.image = product.image || found.image || "";
            found.name = product.name || found.name || "";
            found.stock = stock;
            found.unitFactor = Math.max(1, Number(product.unitFactor || found.unitFactor || 1));
            found.unitName = product.unitName || found.unitName || "";
            found.unitOptions = Array.isArray(product.unitOptions) ? product.unitOptions : (found.unitOptions || []);
        } else {
            cart.push({
                id: Number(product.id),
                variantId: product.variantId ? Number(product.variantId) : null,
                variantName: product.variantName || "",
                name: product.name || "",
                qty: Math.max(1, Number(stockDecision.qty || qty || 1)),
                price: effectivePrice,
                originalPrice: Number(product.price || 0),
                salePrice: product.salePrice != null ? Number(product.salePrice) : null,
                image: product.image || "",
                stock: stock,
                unitName: product.unitName || "",
                unitFactor: Math.max(1, Number(product.unitFactor || 1)),
                unitOptions: Array.isArray(product.unitOptions) ? product.unitOptions : [],
                checked: true
            });
        }
        writeCart(cart);
        updateCartBadge();
        renderMiniCart();
    }

    function getVisibleCartTarget() {
        const candidates = [
            ...document.querySelectorAll(".icon_cartWrap .cart_iconLink"),
            ...document.querySelectorAll(".icon_cartWrap"),
            ...document.querySelectorAll(".store-cart-link"),
            ...document.querySelectorAll(".cart_iconLink")
        ];
        return candidates.find((el) => {
            if (!el) return false;
            const style = window.getComputedStyle(el);
            const rect = el.getBoundingClientRect();
            return style.display !== "none" && style.visibility !== "hidden" && rect.width > 0 && rect.height > 0;
        }) || null;
    }

    function animateFlyToCart(fromButton) {
        if (!fromButton) return;
        const card = fromButton.closest(".luxe-card, .hx-home2-product-card, .product_listItem, article, .card");
        const sourceImg = card?.querySelector("img") || document.querySelector("#productMainImage");
        const cartTarget = getVisibleCartTarget();
        if (!cartTarget) return;

        const imgRect = sourceImg
            ? sourceImg.getBoundingClientRect()
            : fromButton.getBoundingClientRect();
        const cartRect = cartTarget.getBoundingClientRect();
        const clone = sourceImg ? sourceImg.cloneNode(true) : document.createElement("div");
        if (!sourceImg) {
            clone.style.background = "#d5b183";
            clone.style.borderRadius = "10px";
        }
        clone.classList.add("hx-fly-item");
        clone.style.left = `${imgRect.left}px`;
        clone.style.top = `${imgRect.top}px`;
        clone.style.width = `${imgRect.width}px`;
        clone.style.height = `${imgRect.height}px`;
        clone.style.position = "fixed";
        clone.style.zIndex = "3000";
        clone.style.pointerEvents = "none";
        clone.style.transition = "all 0.62s cubic-bezier(0.22, 0.61, 0.36, 1)";
        document.body.appendChild(clone);

        requestAnimationFrame(() => {
            clone.style.left = `${cartRect.left + cartRect.width / 2 - 12}px`;
            clone.style.top = `${cartRect.top + cartRect.height / 2 - 12}px`;
            clone.style.width = "24px";
            clone.style.height = "24px";
            clone.style.opacity = "0.2";
        });

        setTimeout(() => clone.remove(), 650);
    }

    function initFeaturedSlider() {
        const root = document.querySelector("[data-f5-slider]");
        if (!root) return;
        const windowEl = root.querySelector("[data-f5-viewport]");
        const track = root.querySelector("[data-f5-track]");
        const prev = root.querySelector("[data-f5-prev]");
        const next = root.querySelector("[data-f5-next]");
        if (!windowEl || !track || !prev || !next) return;

        const cards = () => [...track.querySelectorAll("[data-f5-card]")];
        const getVisibleCount = () => {
            const w = window.innerWidth || 1200;
            if (w <= 767) return 2;
            if (w <= 1199) return 3;
            return 5;
        };
        const layoutCards = () => {
            const list = cards();
            if (!list.length) return;
            const visible = getVisibleCount();
            const gap = visible === 5 ? 16 : 12;
            const available = Math.max(320, windowEl.clientWidth);
            const cardWidth = Math.floor((available - gap * (visible - 1)) / visible);
            list.forEach((card) => {
                card.style.flex = `0 0 ${cardWidth}px`;
                card.style.width = `${cardWidth}px`;
            });
            track.style.gap = `${gap}px`;
        };
        const cardStep = () => {
            const first = cards()[0];
            if (!first) return 0;
            const style = window.getComputedStyle(track);
            const gap = parseFloat(style.columnGap || style.gap || "0") || 0;
            return first.getBoundingClientRect().width + gap;
        };
        const maxScroll = () => Math.max(0, windowEl.scrollWidth - windowEl.clientWidth);
        const apply = () => {
            const showNav = maxScroll() > 0;
            prev.style.display = showNav ? "inline-flex" : "none";
            next.style.display = showNav ? "inline-flex" : "none";
            if (!showNav) windowEl.scrollLeft = 0;
        };

        prev.addEventListener("click", () => {
            const step = cardStep();
            if (!step) return;
            const max = maxScroll();
            let target = windowEl.scrollLeft - step;
            if (target <= 0) target = max;
            windowEl.scrollTo({ left: target, behavior: "smooth" });
        });
        next.addEventListener("click", () => {
            const step = cardStep();
            if (!step) return;
            const max = maxScroll();
            let target = windowEl.scrollLeft + step;
            if (target >= max - 1) target = 0;
            windowEl.scrollTo({ left: target, behavior: "smooth" });
        });
        window.addEventListener("resize", () => {
            layoutCards();
            windowEl.scrollLeft = 0;
            apply();
        });
        layoutCards();
        windowEl.scrollLeft = 0;
        apply();
    }

    function initHeaderScrollEffect() {
        const desktopHeader = document.querySelector(".header_info");
        const mobileHeader = document.querySelector(".header_mobile");
        if (!desktopHeader && !mobileHeader) return;
        const onScroll = () => {
            const isScrolled = window.scrollY > 10;
            if (desktopHeader) desktopHeader.classList.toggle("scrolled", isScrolled);
            if (mobileHeader) mobileHeader.classList.toggle("scrolled", isScrolled);
        };
        onScroll();
        window.addEventListener("scroll", onScroll, { passive: true });
    }

    function bindLegacyHeaderMenu() {
        const menu = document.querySelector(".menu_nav");
        const toggleBtn = document.getElementById("menuToggle");
        const closeBtn = document.getElementById("btn_close");
        if (!menu || !toggleBtn) return;

        const openMenu = () => menu.classList.add("active");
        const closeMenu = () => menu.classList.remove("active");
        const toggleMenu = () => menu.classList.toggle("active");

        toggleBtn.addEventListener("click", (e) => {
            e.preventDefault();
            toggleMenu();
        });
        closeBtn?.addEventListener("click", (e) => {
            e.preventDefault();
            closeMenu();
        });
        document.addEventListener("click", (e) => {
            if (!menu.classList.contains("active")) return;
            if (menu.contains(e.target) || toggleBtn.contains(e.target)) return;
            closeMenu();
        });
        window.addEventListener("resize", () => {
            if (window.innerWidth > 991) closeMenu();
        });

        const submenuItems = [
            ...document.querySelectorAll(".header_navList--item"),
            ...document.querySelectorAll(".store-topnav .store-topnav-item.has-submenu")
        ];
        submenuItems.forEach((item) => {
            const submenu = item.querySelector(".menu_product");
            if (!submenu) return;
            item.addEventListener("mouseenter", () => submenu.classList.add("is-open"));
            item.addEventListener("mouseleave", () => submenu.classList.remove("is-open"));
            item.addEventListener("click", (e) => {
                const link = e.target.closest("a");
                if (!link || !item.contains(link)) return;
                if (window.innerWidth > 991) return;
                if (!submenu.contains(e.target) && link.getAttribute("href")) {
                    e.preventDefault();
                    submenu.classList.toggle("is-open");
                }
            });
        });
    }

    function updateCartBadge() {
        const totalQty = readCart().reduce((sum, item) => sum + Number(item.qty || 0), 0);
        document.querySelectorAll(".cart_count").forEach(el => {
            el.textContent = totalQty;
            el.style.display = totalQty > 0 ? "inline-flex" : "none";
        });
    }

    function renderMiniCart() {
        const containers = [
            ...document.querySelectorAll("#miniCartContent"),
            ...document.querySelectorAll("[data-mini-cart-content]")
        ];
        if (!containers.length) return;
        const cart = readCart();
        if (!cart.length) {
            containers.forEach(container => {
                container.innerHTML = `
                    <div class="mini-cart-title">Sản phẩm mới thêm</div>
                    <div class="mini-cart-empty">Giỏ hàng trống</div>
                    <div class="mini-cart-footer">
                        <a href="/Store/Cart" class="mini-cart-view-btn">Xem Giỏ Hàng</a>
                    </div>
                `;
            });
            return;
        }

        const latestItems = cart.slice().reverse().slice(0, 3);
        const html = `
            <div class="mini-cart-title">Sản phẩm mới thêm</div>
            ${latestItems.map(item => `
            <div class="mini-cart-item">
                <a href="/Store/ProductDetail/${item.id}" class="mini-cart-thumb-wrap">
                    <img src="${item.image || '/assets/img/logo/hoa_xinh_group_fav.png'}" alt="">
                </a>
                <div class="mini-cart-meta">
                    <a href="/Store/ProductDetail/${item.id}" class="mini-cart-name">${item.name || ''}</a>
                    <div class="mini-cart-price">${formatVnd(Number(item.price || 0) * Number(item.qty || 1))}</div>
                </div>
            </div>
            `).join("")}
            <div class="mini-cart-footer">
                <a href="/Store/Cart" class="mini-cart-view-btn">Xem Giỏ Hàng</a>
            </div>
        `;
        containers.forEach(container => {
            container.innerHTML = html;
        });
    }

    function cartItemKey(item) {
        return `${Number(item.id || item.productId || 0)}_${Number(item.variantId || 0)}`;
    }

    async function syncCartWithServer() {
        const localItems = normalizeCart(readCart());
        try {
            const payload = {
                items: localItems.map(i => ({
                    productId: Number(i.id || 0),
                    variantId: Number(i.variantId || 0),
                    qty: Math.max(1, Number(i.qty || 1)),
                    unitFactor: Math.max(1, Number(i.unitFactor || 1)),
                    unitName: i.unitName || "",
                    variantName: i.variantName || ""
                }))
            };
            const res = await fetch("/Store/SyncCart", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });
            if (!res.ok) return;
            const data = await res.json();
            const serverItems = Array.isArray(data?.items) ? data.items : [];
            const localMap = new Map(localItems.map(i => [cartItemKey(i), i]));
            const merged = serverItems.map(s => {
                const key = cartItemKey({ id: s.productId, variantId: s.variantId });
                const old = localMap.get(key);
                return {
                    id: Number(s.productId || 0),
                    variantId: Number(s.variantId || 0) || null,
                    variantName: s.variantName || old?.variantName || "",
                    name: s.name || old?.name || "",
                    qty: Math.max(1, Number(s.qty || old?.qty || 1)),
                    price: Number(s.salePrice ?? s.price ?? old?.price ?? 0),
                    originalPrice: Number(s.price ?? old?.originalPrice ?? 0),
                    salePrice: s.salePrice != null ? Number(s.salePrice) : null,
                    image: s.image || old?.image || "",
                    stock: Math.max(0, Number(s.stock || 0)),
                    unitName: s.unitName || old?.unitName || "",
                    unitFactor: Math.max(1, Number(s.unitFactor || old?.unitFactor || 1)),
                    unitOptions: Array.isArray(old?.unitOptions) ? old.unitOptions : [],
                    checked: old?.checked !== false
                };
            });
            writeCart(merged);
            updateCartBadge();
            renderMiniCart();
            renderCartPage();
            renderCheckoutPage();
        } catch {
            // ignore temporary sync errors
        }
    }

    let storefrontVersion = null;
    let refreshingMain = false;
    async function refreshMainContentSoft() {
        if (refreshingMain) return;
        refreshingMain = true;
        try {
            const res = await fetch(window.location.href, { cache: "no-store", headers: { "X-Requested-With": "XMLHttpRequest" } });
            if (!res.ok) return;
            const html = await res.text();
            const parser = new DOMParser();
            const doc = parser.parseFromString(html, "text/html");
            const oldMain = document.querySelector("main");
            const newMain = doc.querySelector("main");
            if (oldMain && newMain) {
                oldMain.innerHTML = newMain.innerHTML;
                renderMiniCart();
                renderCartPage();
                renderCheckoutPage();
                updateCartBadge();
                document.dispatchEvent(new CustomEvent("hx:storefront-main-refreshed"));
            }
        } catch {
            // fallback no-op
        } finally {
            refreshingMain = false;
        }
    }

    async function pollStorefrontVersion() {
        try {
            const res = await fetch("/Store/StorefrontVersion", { cache: "no-store" });
            if (!res.ok) return;
            const data = await res.json();
            const version = String(data?.version || "").trim();
            if (!version) return;
            if (storefrontVersion == null) {
                storefrontVersion = version;
                return;
            }
            if (storefrontVersion !== version) {
                storefrontVersion = version;
                await refreshMainContentSoft();
            }
        } catch {
            // ignore polling errors
        }
    }

    function bindAddButtons() {
        document.addEventListener("click", (e) => {
            const btn = e.target.closest("[data-add-cart]");
            if (!btn) return;
                const product = {
                    id: btn.getAttribute("data-id"),
                    variantId: btn.getAttribute("data-variant-id") ? Number(btn.getAttribute("data-variant-id")) : null,
                    variantName: btn.getAttribute("data-variant-name") || "",
                    name: btn.getAttribute("data-name"),
                    price: Number(btn.getAttribute("data-price") || 0),
                    salePrice: btn.getAttribute("data-sale-price") ? Number(btn.getAttribute("data-sale-price")) : null,
                    image: btn.getAttribute("data-image"),
                    stock: Number(btn.getAttribute("data-stock") || 0),
                    unitName: btn.getAttribute("data-unit-name") || "",
                    unitFactor: Number(btn.getAttribute("data-unit-factor") || 1),
                    unitOptions: (() => {
                        const raw = btn.getAttribute("data-unit-options") || "[]";
                        try { return JSON.parse(raw); } catch { return []; }
                    })()
                };
                addToCart(product, 1);
                animateFlyToCart(btn);
            });

        document.addEventListener("click", (e) => {
            const btn = e.target.closest("[data-preorder-popup]");
            if (!btn) return;
            e.preventDefault();
            const pathMatch = window.location.pathname.match(/\/Store\/ProductDetail\/(\d+)/i);
            const fallbackProductId = pathMatch ? Number(pathMatch[1] || 0) : 0;
            const fallbackProductName =
                document.querySelector(".product-luxe-info h1")?.textContent?.trim() || "";
            showPreOrderModal({
                id: Number(btn.getAttribute("data-id") || 0) || fallbackProductId,
                name: btn.getAttribute("data-name") || fallbackProductName
            }, 1);
        });
    }

    function updateQuantity(index, delta) {
        const items = normalizeCart(readCart());
        if (!items[index]) return;
        const desiredQty = Math.max(1, Number(items[index].qty || 1) + delta);
        const decision = handleStockOverflow(items[index], desiredQty);
        if (decision.action === "stop" || decision.action === "cancel") {
            return;
        }
        items[index].qty = Math.max(1, Number(decision.qty || desiredQty));
        writeCart(items);
        updateCartBadge();
        renderMiniCart();
        renderCartPage();
        renderCheckoutPage();
    }

    function removeCartItem(index) {
        const items = normalizeCart(readCart());
        if (!items[index]) return;
        items.splice(index, 1);
        writeCart(items);
        updateCartBadge();
        renderMiniCart();
        renderCartPage();
        renderCheckoutPage();
    }

    function renderCartPage() {
        const tableBody = document.getElementById("cartPageItems");
        const emptyEl = document.getElementById("cartPageEmpty");
        const totalEl = document.getElementById("cartPageTotal");
        const tableWrap = document.getElementById("cartTableWrap");
        const btnCheckout = document.getElementById("btnToCheckout");
        const selectAllEl = document.getElementById("cartSelectAll");
        const paginationEl = document.getElementById("cartPagination");
        if (!tableBody || !emptyEl || !totalEl) return;

        const cart = normalizeCart(readCart());
        if (!cart.length) {
            tableBody.innerHTML = "";
            if (paginationEl) paginationEl.innerHTML = "";
            emptyEl.classList.remove("d-none");
            if (tableWrap) tableWrap.classList.add("d-none");
            if (btnCheckout) btnCheckout.classList.add("disabled");
            totalEl.textContent = formatVnd(0);
            return;
        }

        emptyEl.classList.add("d-none");
        if (tableWrap) tableWrap.classList.remove("d-none");
        if (btnCheckout) btnCheckout.classList.remove("disabled");

        tableBody.innerHTML = cart.map((item, idx) => {
            const qty = Number(item.qty || 1);
            const line = Number(item.price || 0) * qty;
            const hasDiscount = item.salePrice != null && Number(item.originalPrice || 0) > Number(item.price || 0);
            const image = item.image || "/assets/img/logo/hoa_xinh_group_fav.png";
            const unitLabel = (item.variantName || item.unitName || "").toString().trim() || "Mặc định";
            const unitSelectHtml = `<span class="cart-unit-pill">${unitLabel}</span>`;

            return `
                <div class="cart-item">
                    <div class="cart-col cart-col-check">
                        <input type="checkbox" data-cart-check="${idx}" ${item.checked ? "checked" : ""} />
                    </div>
                    <div class="cart-col cart-col-product">
                        <div class="cart-product">
                            <a href="/Store/ProductDetail/${item.id}">
                                <img src="${image}" class="cart-page-img" alt="">
                            </a>
                            <div>
                                <a class="name" href="/Store/ProductDetail/${item.id}">${item.name || ""}</a>
                            </div>
                        </div>
                    </div>
                    <div class="cart-col cart-col-unit">${unitSelectHtml}</div>
                    <div class="cart-col cart-col-price cart-price">
                        ${hasDiscount ? `<div class="old">${formatVnd(item.originalPrice)}</div>` : ""}
                        <div class="sale">${formatVnd(item.price)}</div>
                    </div>
                    <div class="cart-col cart-col-qty">
                        <div class="qty-wrap">
                            <button type="button" data-cart-minus="${idx}">-</button>
                            <input type="number" min="1" value="${qty}" data-cart-qty="${idx}" />
                            <button type="button" data-cart-plus="${idx}">+</button>
                        </div>
                    </div>
                    <div class="cart-col cart-col-total cart-line-total">${formatVnd(line)}</div>
                    <div class="cart-col cart-col-action">
                        <button type="button" class="remove" data-cart-remove="${idx}">
                            <i class="fa-solid fa-trash"></i> Xóa
                        </button>
                    </div>
                </div>
            `;
        }).join("");

        const checkedItems = cart.filter(item => item.checked !== false);
        const total = checkedItems.reduce((sum, item) => sum + Number(item.price || 0) * Number(item.qty || 1), 0);
        totalEl.textContent = formatVnd(total);

        if (selectAllEl) {
            selectAllEl.checked = cart.length > 0 && cart.every(x => x.checked !== false);
            selectAllEl.onchange = () => {
                const items = normalizeCart(readCart()).map(x => ({ ...x, checked: !!selectAllEl.checked }));
                writeCart(items);
                renderCartPage();
                renderCheckoutPage();
            };
        }

        if (paginationEl) paginationEl.innerHTML = "";

        tableBody.querySelectorAll("[data-cart-remove]").forEach(btn => {
            btn.addEventListener("click", () => removeCartItem(Number(btn.getAttribute("data-cart-remove"))));
        });
        tableBody.querySelectorAll("[data-cart-check]").forEach(input => {
            input.addEventListener("change", () => {
                const idx = Number(input.getAttribute("data-cart-check"));
                const items = normalizeCart(readCart());
                if (!items[idx]) return;
                items[idx].checked = !!input.checked;
                writeCart(items);
                renderCartPage();
                renderCheckoutPage();
            });
        });
        tableBody.querySelectorAll("[data-cart-minus]").forEach(btn => {
            btn.addEventListener("click", () => updateQuantity(Number(btn.getAttribute("data-cart-minus")), -1));
        });
        tableBody.querySelectorAll("[data-cart-plus]").forEach(btn => {
            btn.addEventListener("click", () => updateQuantity(Number(btn.getAttribute("data-cart-plus")), 1));
        });
        tableBody.querySelectorAll("[data-cart-qty]").forEach(input => {
            input.addEventListener("change", () => {
                const idx = Number(input.getAttribute("data-cart-qty"));
                const items = normalizeCart(readCart());
                if (!items[idx]) return;
                const desiredQty = Math.max(1, Number(input.value || 1));
                const decision = handleStockOverflow(items[idx], desiredQty);
                if (decision.action === "stop" || decision.action === "cancel") {
                    renderCartPage();
                    return;
                }
                items[idx].qty = Math.max(1, Number(decision.qty || desiredQty));
                writeCart(items);
                updateCartBadge();
                renderMiniCart();
                renderCartPage();
                renderCheckoutPage();
            });
        });
        // Unit selection is controlled at product detail via variant chips.
    }

    function renderCheckoutPage() {
        const checkoutItems = document.getElementById("checkoutItems");
        const subtotalEl = document.getElementById("checkoutSubtotal");
        const grandEl = document.getElementById("checkoutGrandTotal");
        const hiddenItems = document.getElementById("checkoutPageHiddenItems");
        const submitBtn = document.querySelector("#checkoutPageForm button[type='submit']");
        if (!checkoutItems || !subtotalEl || !grandEl || !hiddenItems) return;

        const mode = new URLSearchParams(window.location.search).get("mode");
        let selected = [];
        if (mode === "buy-now") {
            try {
                const raw = sessionStorage.getItem("hx_buy_now_item");
                const item = raw ? JSON.parse(raw) : null;
                if (item && item.id) {
                    selected = [item];
                }
            } catch { selected = []; }
        } else {
            const cart = normalizeCart(readCart());
            selected = cart.filter(item => item.checked !== false);
        }
        if (!selected.length) {
            checkoutItems.innerHTML = '<div class="text-muted">Giỏ hàng trống. Vui lòng quay lại để thêm sản phẩm.</div>';
            subtotalEl.textContent = formatVnd(0);
            grandEl.textContent = formatVnd(0);
            hiddenItems.innerHTML = "";
            if (submitBtn) submitBtn.setAttribute("disabled", "disabled");
            return;
        }

        if (submitBtn) submitBtn.removeAttribute("disabled");

        checkoutItems.innerHTML = selected.map((item) => `
            <div class="checkout-item">
                <div>
                    <a href="/Store/ProductDetail/${item.id}" class="name">${item.name || ""}</a>
                    <div class="qty">SL: ${Number(item.qty || 1)}${(item.variantName || item.unitName) ? ` • ${(item.variantName || item.unitName)}` : ""}</div>
                </div>
                <strong class="checkout-item-price">${formatVnd(Number(item.price || 0) * Number(item.qty || 1))}</strong>
            </div>
        `).join("");

        hiddenItems.innerHTML = selected.map((item, idx) => `
            <input type="hidden" name="Items[${idx}].ProductId" value="${item.id}" />
            <input type="hidden" name="Items[${idx}].VariantId" value="${item.variantId || ""}" />
            <input type="hidden" name="Items[${idx}].Quantity" value="${Math.max(1, Number(item.qty || 1))}" />
            <input type="hidden" name="Items[${idx}].UnitFactor" value="${Math.max(1, Number(item.unitFactor || 1))}" />
            <input type="hidden" name="Items[${idx}].UnitName" value="${item.variantName || item.unitName || ""}" />
        `).join("");

        const total = selected.reduce((sum, item) => sum + Number(item.price || 0) * Number(item.qty || 1), 0);
        subtotalEl.textContent = formatVnd(total);
        grandEl.textContent = formatVnd(total);
    }

    function bindMobileMenu() {
        const btn = document.getElementById("storeMobileMenuBtn");
        const menu = document.getElementById("storeMobileMenu");
        const overlay = document.getElementById("storeMobileMenuOverlay");
        if (!btn || !menu) return;

        const closeMenu = () => {
            menu.classList.remove("open");
            overlay?.classList.remove("open");
            btn.setAttribute("aria-expanded", "false");
        };

        btn.addEventListener("click", () => {
            const next = !menu.classList.contains("open");
            menu.classList.toggle("open", next);
            overlay?.classList.toggle("open", next);
            btn.setAttribute("aria-expanded", next ? "true" : "false");
        });

        menu.querySelectorAll("a").forEach(link => {
            link.addEventListener("click", () => {
                closeMenu();
            });
        });

        overlay?.addEventListener("click", closeMenu);

        window.addEventListener("resize", () => {
            if (window.innerWidth > 576) {
                closeMenu();
            }
        });
    }

    async function loadSignalRClient() {
        if (window.signalR?.HubConnectionBuilder) return true;
        return await new Promise((resolve) => {
            const script = document.createElement("script");
            script.src = "https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.7/signalr.min.js";
            script.onload = () => resolve(!!window.signalR?.HubConnectionBuilder);
            script.onerror = () => resolve(false);
            document.head.appendChild(script);
        });
    }

    async function connectStorefrontHub() {
        const ok = await loadSignalRClient();
        if (!ok) return;
        try {
            const connection = new window.signalR.HubConnectionBuilder()
                .withUrl("/hubs/storefront")
                .withAutomaticReconnect()
                .build();
            connection.on("storefront-updated", async () => {
                await syncCartWithServer();
                await refreshMainContentSoft();
            });
            await connection.start();
        } catch {
            // ignore hub errors, polling still works
        }
    }

    function bindDetailQtyDelegation() {
        if (window.__hxDetailQtyBound) return;
        window.__hxDetailQtyBound = true;
        document.addEventListener("click", (e) => {
            const minusBtn = e.target.closest("#productQtyMinus");
            const plusBtn = e.target.closest("#productQtyPlus");
            if (!minusBtn && !plusBtn) return;
            const input = document.getElementById("productQtyInput");
            if (!input) return;
            const current = Math.max(1, parseInt(String(input.value || "1"), 10) || 1);
            input.value = String(minusBtn ? Math.max(1, current - 1) : current + 1);
            input.dispatchEvent(new Event("input", { bubbles: true }));
        });
        document.addEventListener("input", (e) => {
            const input = e.target.closest("#productQtyInput");
            if (!input) return;
            const n = Math.max(1, parseInt(String(input.value || "1"), 10) || 1);
            input.value = String(n);
        });
    }

    document.addEventListener("DOMContentLoaded", async () => {
        initHeaderScrollEffect();
        await ensureCartReady();
        updateCartBadge();
        renderMiniCart();
        syncCartWithServer();
        bindAddButtons();
        document.addEventListener("hx:preorder-open", (e) => {
            const detail = e?.detail || {};
            showPreOrderModal({
                id: Number(detail.id || 0),
                name: detail.name || ""
            }, Math.max(1, Number(detail.qty || 1)));
        });
        window.HXStorefront = {
            showPreOrderModal,
            showNoticeModal,
            addToCart,
            flyToCart: animateFlyToCart,
            getCart: () => readCart(),
            syncCart: syncCartWithServer
        };
        renderCartPage();
        renderCheckoutPage();
        bindMobileMenu();
        bindLegacyHeaderMenu();
        initFeaturedSlider();
        bindDetailQtyDelegation();
        connectStorefrontHub();

        const reloadBtn = document.getElementById("btnReloadCart");
        if (reloadBtn) {
            reloadBtn.addEventListener("click", () => {
                renderCartPage();
            });
        }

        const btnToCheckout = document.getElementById("btnToCheckout");
        if (btnToCheckout) {
            btnToCheckout.addEventListener("click", (e) => {
                const selectedCount = normalizeCart(readCart()).filter(x => x.checked !== false).length;
                if (!selectedCount) {
                    e.preventDefault();
                }
            });
        }

        const vatToggle = document.getElementById("vatToggle");
        const vatFields = document.getElementById("vatFields");
        if (vatToggle && vatFields) {
            vatToggle.addEventListener("change", () => {
                vatFields.classList.toggle("d-none", !vatToggle.checked);
            });
        }

        document.querySelectorAll(".payment-choice input[type='radio']").forEach(input => {
            input.addEventListener("change", () => {
                document.querySelectorAll(".payment-choice").forEach(el => el.classList.remove("active"));
                const parent = input.closest(".payment-choice");
                if (parent) parent.classList.add("active");
                const qrPayConfirmedInput = document.getElementById("qrPayConfirmedInput");
                if (qrPayConfirmedInput && String(input.value || "").toUpperCase() !== "QRPAY") {
                    qrPayConfirmedInput.value = "false";
                }
            });
        });

        const checkoutForm = document.getElementById("checkoutPageForm");
        const qrPayConfirmedInput = document.getElementById("qrPayConfirmedInput");
        if (checkoutForm && qrPayConfirmedInput) {
            checkoutForm.addEventListener("submit", (e) => {
                const method = String(checkoutForm.querySelector("input[name='PaymentMethod']:checked")?.value || "COD").toUpperCase();
                if (method !== "QRPAY" || qrPayConfirmedInput.value === "true") {
                    return;
                }

                e.preventDefault();
                showQrPayPopup(
                    () => {
                        showNoticeModal(
                            "Hoa Xinh Store cảm ơn bạn đã tin tưởng. Chúng tôi đã ghi nhận thanh toán của bạn và đang xử lý đơn hàng.",
                            "Thanh toán thành công",
                            "success"
                        );
                        qrPayConfirmedInput.value = "true";
                        window.setTimeout(() => checkoutForm.submit(), 900);
                    },
                    () => {
                        qrPayConfirmedInput.value = "false";
                        showNoticeModal(
                            "Bạn đã hủy thanh toán QR. Đơn hàng chưa được tạo.",
                            "Đã hủy thanh toán",
                            "error"
                        );
                    });
            });
        }

        if (checkoutForm) {
            const orderNo = String(checkoutForm.getAttribute("data-auto-qr-order-no") || "").trim();
            const amountRaw = Number(checkoutForm.getAttribute("data-auto-qr-amount") || 0);
            const qrImageUrl = String(checkoutForm.getAttribute("data-auto-qr-image-url") || "").trim();
            if (orderNo && qrImageUrl) {
                const antiForgery = checkoutForm.querySelector("input[name='__RequestVerificationToken']")?.value || "";
                showAutoQrDynamicPopup(
                    orderNo,
                    formatVnd(amountRaw),
                    qrImageUrl,
                    async () => {
                        try {
                            const body = new URLSearchParams();
                            body.set("orderNo", orderNo);
                            body.set("__RequestVerificationToken", antiForgery);
                            const res = await fetch("/Store/ConfirmAutoQrPayment", {
                                method: "POST",
                                headers: { "Content-Type": "application/x-www-form-urlencoded;charset=UTF-8" },
                                body: body.toString()
                            });
                            const data = await res.json().catch(() => ({ ok: false }));
                            if (!res.ok || !data?.ok) {
                                showNoticeModal(data?.message || "Không thể xác nhận thanh toán QR tự động.", "Lỗi xác nhận", "error");
                                return;
                            }

                            showNoticeModal(
                                data?.message || "Thanh toán thành công. Cảm ơn bạn đã tin tưởng Hoa Xinh Store.",
                                "Thanh toán thành công",
                                "success"
                            );
                            checkoutForm.setAttribute("data-auto-qr-order-no", "");
                            window.setTimeout(() => { window.location.href = "/Store"; }, 1200);
                        } catch {
                            showNoticeModal("Không thể xác nhận thanh toán QR tự động.", "Lỗi xác nhận", "error");
                        }
                    },
                    async () => {
                        try {
                            const body = new URLSearchParams();
                            body.set("orderNo", orderNo);
                            body.set("__RequestVerificationToken", antiForgery);
                            const res = await fetch("/Store/CancelAutoQrPayment", {
                                method: "POST",
                                headers: { "Content-Type": "application/x-www-form-urlencoded;charset=UTF-8" },
                                body: body.toString()
                            });
                            const data = await res.json().catch(() => ({ ok: false }));
                            if (!res.ok || !data?.ok) {
                                showNoticeModal(data?.message || "Không thể hủy thanh toán QR tự động.", "Lỗi hủy thanh toán", "error");
                                return;
                            }
                            showNoticeModal(
                                data?.message || "Bạn đã hủy thanh toán QR tự động.",
                                "Đã hủy thanh toán",
                                "error"
                            );
                            checkoutForm.setAttribute("data-auto-qr-order-no", "");
                            window.setTimeout(() => { window.location.href = "/Store"; }, 1200);
                        } catch {
                            showNoticeModal("Không thể hủy thanh toán QR tự động.", "Lỗi hủy thanh toán", "error");
                        }
                    });
            }
        }

        pollStorefrontVersion();
        setInterval(syncCartWithServer, 15000);
        setInterval(pollStorefrontVersion, 10000);
    });
})();
