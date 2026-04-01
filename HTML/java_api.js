(function () {
    // --- CẤU HÌNH KẾT NỐI BACKEND ---
    const API_BASE_URL = "http://localhost:8080/api";
    const productTrack = document.querySelector(".product_track");

    if (!productTrack) return;

    // Các biến global để quản lý dữ liệu giống bản cũ
    window.googleSheetCatalog = []; 
    window.allFilteredProducts = []; 
    let currentSliceIndex = 0; 
    let carts = [];

    // Lấy Element từ DOM (giữ nguyên từ bản cũ của ông)
    const popupProduct = document.querySelector(".popup_product");
    const popupProductImage = document.querySelector(".popup_product--img");
    const popupProductTitle = document.querySelector(".popup_product--title");
    const popupProductPrice = document.querySelector(".popup_product--price");
    const popupProductSummary = document.querySelector(".popup_product--summary");
    const popupProductDesc = document.querySelector(".popup_product--desc");
    const popupProductBuyBtn = document.querySelector(".popup_product--buyBtn");
    const popupProductQty = document.getElementById("qty");
    const popupProductSuggestList = document.querySelector(".popup_productSuggestList");
    const popupProductSuggestViewport = document.querySelector(".popup_productSuggestViewport");
    const btnLoadMore = document.getElementById("load-more-btn");

    // =========================================
    // 1. GỌI API LẤY SẢN PHẨM (THAY THẾ GGSHEET)
    // =========================================
    async function loadProductsFromBackend() {
        productTrack.innerHTML = '<p class="google-sheet-status">Đang tải hoa xinh từ hệ thống...</p>';
        try {
            // Gọi API lấy toàn bộ sản phẩm (không giới hạn để filter tại FE giống cũ)
            const response = await fetch(`${API_BASE_URL}/products?page=0&size=100`);
            if (!response.ok) throw new Error("Backend connection failed");
            
            const data = await response.json();
            const productsFromDB = data.content;

            // Map lại dữ liệu DB sang format của FE ông đang dùng
            const formattedProducts = productsFromDB.map(p => ({
                id: p.id,
                ten: p.name,
                gia: p.price,
                hinh: p.imageURL,
                tomtat: p.summary,
                mota: p.descriptions,
                loai: p.category ? p.category.name : "all"
            }));

            initProductData(formattedProducts);
        } catch (error) {
            productTrack.innerHTML = '<p class="google-sheet-status">Không kết nối được Server. Hãy bật backend .NET lên ông giáo ơi!</p>';
            console.error(error);
        }
    }

    function initProductData(products) {
        if (!products.length) {
            productTrack.innerHTML = '<p class="google-sheet-status">Hệ thống chưa có sản phẩm nào.</p>';
            return;
        }

        // Lưu vào kho Catalog (Dùng id thật của DB)
        window.googleSheetCatalog = products.map(p => ({
            id: p.id,
            name: p.ten,
            image: p.hinh,
            price: p.gia,
            summary: p.tomtat,
            description: p.mota,
            categoryKey: normalizeCategory(p.loai)
        }));

        window.allFilteredProducts = window.googleSheetCatalog;
        productTrack.innerHTML = '';
        currentSliceIndex = 0; 
        renderProductsGridSlice();
        syncCartWithBackend();
    }

    // =========================================
    // 2. RENDER GRID & LAZY LOAD (GIỮ NGUYÊN LOGIC CŨ)
    // =========================================
    function renderProductsGridSlice() {
        const config = getLoadConfig();
        let sliceSize = currentSliceIndex === 0 ? config.initial : config.slice;
        let endIndex = Math.min(window.allFilteredProducts.length, currentSliceIndex + sliceSize);
        let currentSlice = window.allFilteredProducts.slice(currentSliceIndex, endIndex);

        const html = currentSlice.map(product => {
            // Tìm index thực tế trong catalog để mở popup
            const realIdx = window.googleSheetCatalog.findIndex(p => p.id === product.id);
            return `
                <div class="product_listItem" style="opacity: 0; transform: translateY(20px);">
                    <div class="product_imgWrap">
                        <img src="${escapeHtml(product.image)}" class="product_listItem--img product_detailImage" data-product-index="${realIdx}">
                    </div>
                    <h3 class="product_listItem--title product_detailTrigger" data-product-index="${realIdx}">${escapeHtml(product.name)}</h3>
                    <div class="product_listItem--cost">${escapeHtml(formatPrice(product.price))}</div>
                    <button type="button" class="product_hoverBtn" data-id="${product.id}" data-name="${escapeHtml(product.name)}" data-price="${product.price}">
                        <svg class="icon_addCart" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 640 640"><path d="M24 48C10.7 48 0 58.7 0 72C0 85.3 10.7 96 24 96L69.3 96C73.2 96 76.5 98.8 77.2 102.6L129.3 388.9C135.5 423.1 165.3 448 200.1 448L456 448C469.3 448 480 437.3 480 424C480 410.7 469.3 400 456 400L200.1 400C188.5 400 178.6 391.7 176.5 380.3L171.4 352L475 352C505.8 352 532.2 330.1 537.9 299.8L568.9 133.9C572.6 114.2 557.5 96 537.4 96L124.7 96L124.3 94C119.5 67.4 96.3 48 69.2 48L24 48zM208 576C234.5 576 256 554.5 256 528C256 501.5 234.5 480 208 480C181.5 480 160 501.5 160 528C160 554.5 181.5 576 208 576zM432 576C458.5 576 480 554.5 480 528C480 501.5 458.5 480 432 480C405.5 480 384 501.5 384 528C384 554.5 405.5 576 432 576z"/></svg>
                    </button>
                </div>`;
        }).join("");

        if (currentSliceIndex === 0) productTrack.innerHTML = html;
        else productTrack.insertAdjacentHTML('beforeend', html);

        requestAnimationFrame(() => {
            productTrack.querySelectorAll(".product_listItem").forEach(item => {
                item.style.opacity = '1';
                item.style.transform = 'translateY(0)';
            });
        });

        currentSliceIndex = endIndex;
        if (btnLoadMore) btnLoadMore.style.display = currentSliceIndex < window.allFilteredProducts.length ? "inline-block" : "none";

        bindProductDetailEvents();
        bindListAddCartEvents();
    }

    // =========================================
    // 3. XỬ LÝ THANH TOÁN (KẾT NỐI SPRING BOOT)
    // =========================================
    const btnConfirmOrder = document.getElementById("btn_confirmOrder"); // Giả sử đây là nút trong Form Order của ông
    
    if (btnConfirmOrder) {
        btnConfirmOrder.onclick = async () => {
            // Lấy thông tin từ Form đặt hàng
            const orderData = {
                customerName: document.getElementById("order_name").value,
                phoneNumber: document.getElementById("order_phone").value,
                address: document.getElementById("order_address").value,
                email: document.getElementById("order_email").value,
                paymentMethod: "VNPAY",
                items: carts.map(item => ({
                    productId: item.id,
                    quantity: item.qty
                }))
            };

            try {
                // Bước A: Tạo Order
                const res = await fetch(`${API_BASE_URL}/orders/create`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(orderData)
                });
                const orderSaved = await res.json();

                // Bước B: Lấy link VNPay
                const payRes = await fetch(`${API_BASE_URL}/payment/create-url?orderId=${orderSaved.id}`);
                const vnpayUrl = await payRes.text();

                // Bước C: Chuyển sang thanh toán
                localStorage.removeItem("cart"); // Xóa giỏ sau khi đặt
                window.location.href = vnpayUrl;
            } catch (err) {
                alert("Lỗi hệ thống thanh toán!");
            }
        };
    }

    // =========================================
    // 4. CÁC HÀM TIỆN ÍCH (BAO GỒM ẢNH BAY)
    // =========================================
    function flyToCart(imgElement) {
        const activeCartIcon = Array.from(document.querySelectorAll(".icon_cartWrap")).find(i => i.offsetWidth > 0);
        if (!imgElement || !activeCartIcon) return;

        const imgRect = imgElement.getBoundingClientRect();
        const cartRect = activeCartIcon.getBoundingClientRect();
        const flyingImg = document.createElement("img");
        flyingImg.src = imgElement.src;
        flyingImg.className = "flying-img";
        Object.assign(flyingImg.style, {
            left: `${imgRect.left}px`, top: `${imgRect.top}px`, width: `${imgRect.width}px`, height: `${imgRect.height}px`
        });
        document.body.appendChild(flyingImg);

        setTimeout(() => {
            Object.assign(flyingImg.style, {
                left: `${cartRect.left + 10}px`, top: `${cartRect.top + 10}px`, width: "20px", height: "20px", opacity: "0.2"
            });
        }, 10);

        setTimeout(() => {
            flyingImg.remove();
            activeCartIcon.classList.add("shake-cart");
            setTimeout(() => activeCartIcon.classList.remove("shake-cart"), 300);
        }, 800);
    }

    function bindListAddCartEvents() {
        productTrack.querySelectorAll(".product_hoverBtn").forEach(btn => {
            if (btn.dataset.bound) return;
            btn.dataset.bound = "true";
            btn.addEventListener("click", (e) => {
                e.stopPropagation();
                const productCard = btn.closest(".product_listItem");
                const img = productCard.querySelector(".product_listItem--img");
                flyToCart(img);
                addItemToCart(btn.dataset.id, btn.dataset.name, Number(btn.dataset.price), img.src, 1);
            });
        });
    }

    function addItemToCart(id, name, price, image, qty) {
        const exist = carts.find(i => i.id == id);
        if (exist) exist.qty += qty;
        else carts.push({ id, name, price, image, qty });
        saveCart();
        renderCart();
    }

    function saveCart() { localStorage.setItem("cart", JSON.stringify(carts)); }
    function loadCart() { carts = JSON.parse(localStorage.getItem("cart") || "[]"); }

    // --- CÁC HÀM NORMALIZE VÀ HELPER ÔNG ĐÃ CÓ (GIỮ LẠI) ---
    function normalizeHeader(v = "") { return v.toString().trim().toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/[^a-z0-9]/g, ""); }
    function normalizeCategory(v = "") { 
        const c = normalizeHeader(v);
        if (c.includes("mypham")) return "mypham";
        if (c.includes("thietbi")) return "thietbi";
        return "all";
    }
    function escapeHtml(v = "") { return v.toString().replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;"); }
    function formatPrice(v = "") { return `${Number(v).toLocaleString("vi-VN")} đ`; }
    function getLoadConfig() { return window.innerWidth > 1024 ? CONFIG_LOAD.pc : (window.innerWidth > 768 ? CONFIG_LOAD.tablet : CONFIG_LOAD.mobile); }

    const CONFIG_LOAD = { pc: { initial: 20, slice: 12 }, tablet: { initial: 18, slice: 9 }, mobile: { initial: 16, slice: 10 } };

    // Khởi chạy
    loadCart();
    loadProductsFromBackend();

})();
