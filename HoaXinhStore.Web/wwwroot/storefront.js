(function () {
    const CART_KEY = "cart";
    const CART_PAGE_SIZE = 6;
    let cartPage = 1;
    let hxModalEl = null;

    function readCart() {
        try {
            const raw = localStorage.getItem(CART_KEY);
            return raw ? JSON.parse(raw) : [];
        } catch {
            return [];
        }
    }

    function writeCart(items) {
        localStorage.setItem(CART_KEY, JSON.stringify(items));
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
        hxModalEl.querySelector(".hx-popup-close")?.addEventListener("click", () => closeModal());
        hxModalEl.addEventListener("click", (e) => { if (e.target === hxModalEl) closeModal(); });
        return hxModalEl;
    }

    function closeModal() {
        const modal = ensureModal();
        modal.style.display = "none";
    }

    function showNoticeModal(message) {
        const modal = ensureModal();
        const body = modal.querySelector(".hx-popup-body");
        if (!body) return;
        body.innerHTML = `<h4>Thông báo</h4><p>${message}</p><button type="button" class="mini-cart-view-btn" id="hxPopupOkBtn">Đã hiểu</button>`;
        modal.style.display = "flex";
        body.querySelector("#hxPopupOkBtn")?.addEventListener("click", closeModal);
    }

    function showPreOrderModal(item, qty = 1) {
        const modal = ensureModal();
        const body = modal.querySelector(".hx-popup-body");
        if (!body) return;
        body.innerHTML = `
            <div class="hx-preorder-popup">
            <h4>Đặt trước sản phẩm</h4>
            <p class="hx-preorder-title"><strong>${item.name || ""}</strong></p>
            <form id="hxPreOrderForm" class="hx-preorder-form">
                <input type="hidden" name="productId" value="${item.id}" />
                <input type="number" name="quantity" min="1" value="${Math.max(1, Number(qty || 1))}" class="form-control" placeholder="Số lượng" />
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
        const form = body.querySelector("#hxPreOrderForm");
        const msg = body.querySelector("#hxPreOrderMsg");
        form?.addEventListener("submit", async (e) => {
            e.preventDefault();
            const fd = new FormData(form);
            const payload = {
                productId: Number(fd.get("productId") || 0),
                quantity: Number(fd.get("quantity") || 1),
                customerName: String(fd.get("customerName") || ""),
                phoneNumber: String(fd.get("phoneNumber") || ""),
                email: String(fd.get("email") || ""),
                address: String(fd.get("address") || ""),
                note: String(fd.get("note") || "")
            };
            const res = await fetch("/Store/SubmitPreOrderPopup", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });
            const data = await res.json();
            if (msg) msg.textContent = data.message || (data.ok ? "Đã gửi yêu cầu." : "Không thể gửi yêu cầu.");
            if (data.ok) {
                setTimeout(closeModal, 900);
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

    function addToCart(product, qty = 1) {
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

    function animateFlyToCart(fromButton) {
        if (!fromButton) return;
        const card = fromButton.closest(".luxe-card, .hx-home2-product-card, .product_listItem, article, .card");
        const sourceImg = card?.querySelector("img");
        const cartTarget =
            document.querySelector(".icon_cartWrap") ||
            document.querySelector(".store-cart-link") ||
            document.querySelector(".cart_iconLink");
        if (!sourceImg || !cartTarget) return;

        const imgRect = sourceImg.getBoundingClientRect();
        const cartRect = cartTarget.getBoundingClientRect();
        const clone = sourceImg.cloneNode(true);
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
                    <img src="${item.image || '/assets/img/hoa_xinh_group_fav.png'}" alt="">
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
            showPreOrderModal({
                id: Number(btn.getAttribute("data-id") || 0),
                name: btn.getAttribute("data-name") || ""
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

        const totalPages = Math.max(1, Math.ceil(cart.length / CART_PAGE_SIZE));
        cartPage = Math.min(Math.max(1, cartPage), totalPages);
        const start = (cartPage - 1) * CART_PAGE_SIZE;
        const end = start + CART_PAGE_SIZE;
        const pageItems = cart.slice(start, end);

        tableBody.innerHTML = pageItems.map((item, pageIdx) => {
            const idx = start + pageIdx;
            const qty = Number(item.qty || 1);
            const line = Number(item.price || 0) * qty;
            const hasDiscount = item.salePrice != null && Number(item.originalPrice || 0) > Number(item.price || 0);
            const image = item.image || "/assets/img/hoa_xinh_group_fav.png";
            const optionMap = new Map();
            optionMap.set(1, "Đơn vị lẻ");
            (Array.isArray(item.unitOptions) ? item.unitOptions : []).forEach(x => {
                const factor = Math.max(1, Number(x.factor || x.Factor || 1));
                const name = (x.name || x.Name || "").toString().trim();
                if (name) optionMap.set(factor, name);
            });
            const optionEntries = Array.from(optionMap.entries());
            const hasUnitOptions = optionEntries.length > 1;
            const unitSelectHtml = hasUnitOptions
                ? `<select class="cart-unit-select" data-cart-unit="${idx}">${optionEntries.map(([factor, name]) =>
                    `<option value="${factor}" ${Number(item.unitFactor || 1) === factor ? "selected" : ""}>${name}</option>`
                ).join("")}</select>`
                : `<span class="cart-unit-pill">Đơn vị lẻ</span>`;

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

        if (paginationEl) {
            paginationEl.innerHTML = Array.from({ length: totalPages }, (_, i) => {
                const pageNo = i + 1;
                const active = pageNo === cartPage ? " active" : "";
                return `<button type="button" class="cart-page-btn${active}" data-cart-page="${pageNo}">${pageNo}</button>`;
            }).join("");
            paginationEl.querySelectorAll("[data-cart-page]").forEach(btn => {
                btn.addEventListener("click", () => {
                    cartPage = Number(btn.getAttribute("data-cart-page") || 1);
                    renderCartPage();
                });
            });
        }

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
        tableBody.querySelectorAll("[data-cart-unit]").forEach(select => {
            select.addEventListener("change", () => {
                const idx = Number(select.getAttribute("data-cart-unit"));
                const items = normalizeCart(readCart());
                if (!items[idx]) return;
                const oldFactor = Math.max(1, Number(items[idx].unitFactor || 1));
                const oldQty = Math.max(1, Number(items[idx].qty || 1));
                const newFactor = Math.max(1, Number(select.value || 1));
                const baseQty = oldQty * oldFactor;
                const newQty = Math.max(1, Math.round(baseQty / newFactor));
                const selectedName = select.options[select.selectedIndex]?.text || "Đơn vị lẻ";

                items[idx].qty = newQty;
                items[idx].unitFactor = newFactor;
                items[idx].unitName = newFactor === 1 ? "" : selectedName;
                writeCart(items);
                updateCartBadge();
                renderMiniCart();
                renderCartPage();
                renderCheckoutPage();
            });
        });
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
                    <div class="qty">SL: ${Number(item.qty || 1)}${item.variantName ? ` • ${item.variantName}` : ""}${item.unitName ? ` • ${item.unitName}` : ""}</div>
                </div>
                <strong class="checkout-item-price">${formatVnd(Number(item.price || 0) * Number(item.qty || 1))}</strong>
            </div>
        `).join("");

        hiddenItems.innerHTML = selected.map((item, idx) => `
            <input type="hidden" name="Items[${idx}].ProductId" value="${item.id}" />
            <input type="hidden" name="Items[${idx}].VariantId" value="${item.variantId || ""}" />
            <input type="hidden" name="Items[${idx}].Quantity" value="${Math.max(1, Number(item.qty || 1))}" />
            <input type="hidden" name="Items[${idx}].UnitFactor" value="${Math.max(1, Number(item.unitFactor || 1))}" />
            <input type="hidden" name="Items[${idx}].UnitName" value="${item.unitName || ""}" />
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

    document.addEventListener("DOMContentLoaded", () => {
        updateCartBadge();
        renderMiniCart();
        bindAddButtons();
        renderCartPage();
        renderCheckoutPage();
        bindMobileMenu();

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
            });
        });
    });
})();
