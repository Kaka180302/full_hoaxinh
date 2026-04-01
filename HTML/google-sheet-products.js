(function () {
    const productTrack = document.querySelector(".product_track");

    if (!productTrack) {
        return;
    }

    const sheetId = productTrack.dataset.sheetId?.trim();
    const sheetGid = productTrack.dataset.sheetGid?.trim() || "0";
    const callbackName = `googleSheetProductsCallback_${Date.now()}`;
    const popupProduct = document.querySelector(".popup_product");
    const popupProductOverlay = document.querySelector(".popup_product--overlay");
    const popupProductClose = document.getElementById("btn_closePopup");
    const popupProductImage = document.querySelector(".popup_product--img");
    const popupProductTitle = document.querySelector(".popup_product--title");
    const popupProductPrice = document.querySelector(".popup_product--price");
    const popupProductSummary = document.querySelector(".popup_product--summary");
    const popupProductDesc = document.querySelector(".popup_product--desc");
    const popupProductBuyBtn = document.querySelector(".popup_product--buyBtn");
    const popupProductQty = document.getElementById("qty");
    const popupProductSuggestList = document.querySelector(".popup_productSuggestList");
    const popupProductSuggestViewport = document.querySelector(".popup_productSuggestViewport");
    const popupProductSuggestPrev = document.querySelector(".popup_productSuggestNav--prev");
    const popupProductSuggestNext = document.querySelector(".popup_productSuggestNav--next");

    window.googleSheetProductsForOrder = [];
    window.googleSheetCatalog = [];

    function normalizeHeader(value = "") {
        return value
            .toString()
            .trim()
            .toLowerCase()
            .normalize("NFD")
            .replace(/[\u0300-\u036f]/g, "")
            .replace(/[^a-z0-9]/g, "");
    }

    function normalizeCategory(value = "") {
        const category = normalizeHeader(value);

        if (category.includes("mypham")) {
            return "mypham";
        }

        if (category.includes("thietbi") || category.includes("giadung")) {
            return "thietbi";
        }

        if (category.includes("thucpham")) {
            return "thucpham";
        }

        return "all";
    }

    function escapeHtml(value = "") {
        return value
            .toString()
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function formatPrice(value = "") {
        const numericValue = Number(value.toString().replace(/[^\d.-]/g, ""));

        if (Number.isNaN(numericValue)) {
            return value || "Lien he";
        }

        return `${numericValue.toLocaleString("vi-VN")} đ`;
    }



    function getFieldValue(product, fieldNames) {
        for (const fieldName of fieldNames) {
            const matchedKey = Object.keys(product).find(
                (key) => normalizeHeader(key) === normalizeHeader(fieldName)
            );

            if (matchedKey && product[matchedKey]) {
                return product[matchedKey];
            }
        }

        return "";
    }


    function toListHtml(value = "", listClassName = "popup_product--descList", emptyText = "") {
        const items = value
            .split(/\r?\n+/)
            .map((item) => item.trim())
            .filter(Boolean);

        if (!items.length) {
            return emptyText ? `<ul class="${listClassName}"><li>${escapeHtml(emptyText)}</li></ul>` : "";
        }

        return `<ul class="${listClassName}">${items.map((item) => `<li>${escapeHtml(item)}</li>`).join("")}</ul>`;
    }

    function renderSuggestedProducts(currentProduct, currentIndex) {
        if (!popupProductSuggestList) {
            return;
        }

        const sameCategorySuggestions = window.googleSheetCatalog
            .map((product, index) => ({ ...product, index }))
            .filter((product) => product.index !== currentIndex && product.categoryKey === currentProduct.categoryKey)
            .slice(0, 10);

        const extraSuggestions = window.googleSheetCatalog
            .map((product, index) => ({ ...product, index }))
            .filter((product) =>
                product.index !== currentIndex &&
                product.categoryKey !== currentProduct.categoryKey &&
                !sameCategorySuggestions.some((item) => item.index === product.index)
            )
            .slice(0, Math.max(0, 10 - sameCategorySuggestions.length));

        const suggestions = [...sameCategorySuggestions, ...extraSuggestions];

        if (!suggestions.length) {
            popupProductSuggestList.innerHTML = '<div class="popup_productSuggestEmpty">Chua co san pham goi y cung danh muc.</div>';
            popupProductSuggestViewport.scrollLeft = 0;
            updateSuggestSliderButtons();
            return;
        }

        popupProductSuggestList.innerHTML = suggestions.map((product) => `
            <article class="popup_productSuggestItem" data-product-index="${product.index}">
                <img src="${escapeHtml(product.image || "https://placehold.co/300x300?text=No+Image")}" alt="${escapeHtml(product.name || "San pham")}">
                <h4 class="popup_productSuggestTitle">${escapeHtml(product.name || "San pham")}</h4>
                <p class="popup_productSuggestPrice">${escapeHtml(formatPrice(product.price))}</p>
            </article>
        `).join("");

        popupProductSuggestViewport.scrollLeft = 0;
        window.requestAnimationFrame(() => {
            window.requestAnimationFrame(() => {
                updateSuggestSliderButtons();
            });
        });

        popupProductSuggestList.querySelectorAll(".popup_productSuggestItem").forEach((item) => {
            item.addEventListener("click", () => {
                openProductDetail(Number(item.dataset.productIndex));
            });
        });

        popupProductSuggestList.querySelectorAll("img").forEach((img) => {
            img.addEventListener("load", updateSuggestSliderButtons, { once: true });
            img.addEventListener("error", updateSuggestSliderButtons, { once: true });
        });
    }

    function getSuggestSlideWidth() {
        const firstItem = popupProductSuggestList?.querySelector(".popup_productSuggestItem");

        if (!firstItem) {
            return 0;
        }

        const styles = window.getComputedStyle(popupProductSuggestList);
        const gap = parseFloat(styles.columnGap || styles.gap || "0");

        const measuredWidth = firstItem.getBoundingClientRect().width + gap;

        if (measuredWidth > 0) {
            return measuredWidth;
        }

        return popupProductSuggestViewport
            ? Math.max(240, popupProductSuggestViewport.clientWidth * 0.85)
            : 320;
    }

    function updateSuggestSliderButtons() {
        if (!popupProductSuggestList || !popupProductSuggestViewport || !popupProductSuggestPrev || !popupProductSuggestNext) {
            return;
        }

        const maxTranslate = Math.max(0, popupProductSuggestViewport.scrollWidth - popupProductSuggestViewport.clientWidth);
        const currentTranslate = popupProductSuggestViewport.scrollLeft;

        popupProductSuggestPrev.disabled = currentTranslate <= 0;
        popupProductSuggestNext.disabled = currentTranslate >= maxTranslate - 1;
    }

    function moveSuggestSlider(direction) {
        if (!popupProductSuggestList || !popupProductSuggestViewport) {
            return;
        }

        const step = getSuggestSlideWidth();
        const delta = direction === "next" ? step : -step;

        popupProductSuggestViewport.scrollBy({
            left: delta,
            behavior: "smooth"
        });

        window.setTimeout(updateSuggestSliderButtons, 320);
    }

    function openProductDetail(productIndex) {
        const product = window.googleSheetCatalog[productIndex];

        if (!product || !popupProduct) {
            return;
        }

        popupProductImage.src = product.image || "https://placehold.co/600x400?text=No+Image";
        popupProductImage.alt = product.name || "San pham";
        popupProductTitle.textContent = product.name || "San pham";
        popupProductPrice.textContent = formatPrice(product.price);
        if (popupProductSummary) {
            popupProductSummary.innerHTML = toListHtml(product.summary || "", "popup_product--summaryList");
            popupProductSummary.style.display = product.summary ? "block" : "none";
        }
        popupProductDesc.innerHTML = toListHtml(product.description || "", "popup_product--descList", "Chua co mo ta san pham.");
        popupProductBuyBtn.dataset.name = product.name || "";
        popupProductBuyBtn.dataset.price = product.price || "";
        popupProductQty.value = "1";
        renderSuggestedProducts(product, productIndex);
        popupProduct.style.display = "flex";
    }

    function closeProductDetail() {
        if (!popupProduct) {
            return;
        }

        popupProduct.style.display = "none";

        const popupAction = document.querySelector('.popup_productAction');
        const actionDummy = document.querySelector('.action-dummy-anchor');
        if(popupAction) {
            popupAction.classList.remove('is-sticky');
            popupAction.style.width = '';
            popupAction.style.left = '';
            popupAction.style.bottom = '';
        }
        if(actionDummy) actionDummy.style.height = '0px';
    }

// XỬ LÝ CLICK MỞ POPUP CHI TIẾT SẢN PHẨM (PHIÊN BẢN CHẮC CÚ 100%)
    function bindProductDetailEvents() {
        // Tìm tất cả các ảnh và tiêu đề có thể click
        const triggers = document.querySelectorAll(".product_detailTrigger, .product_detailImage");
        
        triggers.forEach(trigger => {
            // Dùng onclick trực tiếp đè lên mọi thứ
            trigger.onclick = function(e) {
                e.stopPropagation(); // Ngăn không cho thằng Slider cướp click
                
                const index = this.getAttribute("data-product-index");
                console.log("Đã click vô sản phẩm số:", index); // In ra để check
                
                if (index !== null) {
                    openProductDetail(Number(index));
                    console.log("Đã gọi lệnh mở Popup!");
                } else {
                    console.error("Lỗi: Không tìm thấy data-product-index");
                }
            };
        });
    }

    if (popupProductClose) {
        popupProductClose.addEventListener("click", closeProductDetail);
    }

    if (popupProductOverlay) {
        popupProductOverlay.addEventListener("click", closeProductDetail);
    }

    if (popupProductSuggestPrev) {
        popupProductSuggestPrev.addEventListener("click", () => moveSuggestSlider("prev"));
    }

    if (popupProductSuggestNext) {
        popupProductSuggestNext.addEventListener("click", () => moveSuggestSlider("next"));
    }

    if (popupProductSuggestViewport) {
        popupProductSuggestViewport.addEventListener("scroll", updateSuggestSliderButtons);
    }

    window.addEventListener("resize", updateSuggestSliderButtons);

    if (popupProductBuyBtn) {
        popupProductBuyBtn.addEventListener("click", () => {
            const qty = Math.max(1, Number(popupProductQty?.value || 1));


            if (typeof window.openOrder === "function") {
                window.openOrder();
            }

            if (typeof window.addProduct === "function") {
                window.addProduct(
                    popupProductBuyBtn.dataset.name || "",
                    popupProductBuyBtn.dataset.price || ""
                );
            }

            const rows = document.querySelectorAll(".order__productRow");
            const lastRowQty = rows.length ? rows[rows.length - 1].querySelector(".order__qty") : null;

            if (lastRowQty) {
                lastRowQty.value = qty;
                lastRowQty.dispatchEvent(new Event("input", { bubbles: true }));
            }
        });
    }


    // ggsheet.js

    // =========================================
    // JS CHO LOAD SẢN PHẨM DẠNG SLICE & GRID
    // =========================================

    // Cài đặt số lượng load
    const CONFIG_LOAD = {
        pc: {
            initial: 20, // Load lần đầu 20 cái (5 hàng x 4 cột)
            slice: 12 // Mỗi lần Xem thêm load 12 cái (3 hàng x 4 cột)
        },
        tablet: {
            initial: 18, // Load lần đầu 18 cái (6 hàng x 3 cột)
            slice: 9 // Mỗi lần Xem thêm load 9 cái (3 hàng x 3 cột)
        },
        mobile: {
            initial: 16, // Load lần đầu 16 cái (8 hàng x 2 cột)
            slice: 10 // Mỗi lần Xem thêm load 10 cái (5 hàng x 2 cột)
        }
    }

    // Biến trạng thái: lưu kho sản phẩm, vị trí hiện tại và cái kho sản phẩm đã lọc
    window.allFilteredProducts = []; 
    let currentSliceIndex = 0; 

    // Hàm lấy cấu hình load theo thiết bị
    function getLoadConfig() {
        if (window.innerWidth > 1024) return CONFIG_LOAD.pc;
        if (window.innerWidth > 768) return CONFIG_LOAD.tablet;
        return CONFIG_LOAD.mobile;
    }

    // --- (HÀM CŨ) renderProducts: Bây giờ chỉ làm nhiệm vụ CẤP PHÁT KHO SẢN PHẨM và BẮT ĐẦU LOAD SLICE ĐẦU TIÊN ---
    // --- HÀM RENDER SẢN PHẨM ĐÃ ĐƯỢC DỌN DẸP SẠCH SẼ TÀN DƯ SLIDER ---
    function renderProducts(products) {
        if (!products.length) {
            productTrack.innerHTML = '<p class="google-sheet-status">Google Sheet chưa có dữ liệu sản phẩm.</p>';
            return;
        }

        // 1. Cấp phát kho catalog sản phẩm (Dùng để hiển thị lên Web)
        window.googleSheetCatalog = products.map((product) => ({
            id: product.id,           // Lấy ID thật từ DB
            name: product.ten,        // "ten" này là do mình đã map ở hàm fetch lúc nãy
            image: product.hinh,      // "hinh" tương tự
            price: product.gia,
            summary: product.tomtat,
            description: product.mota,
            categoryKey: normalizeCategory(product.loai)
        }));

        // 2. Cấp phát danh sách cho việc chọn sản phẩm trong đơn hàng
        window.googleSheetProductsForOrder = products.map((product) => ({
            id: product.id,
            name: product.ten,
            price: product.gia
        })).filter((product) => product.name);

        // Bắt đầu logic chia Slice
        window.allFilteredProducts = window.googleSheetCatalog;
        productTrack.innerHTML = '';
        currentSliceIndex = 0; 

        // Load slice đầu tiên ra
        renderProductsGridSlice();

        // Chỉ giữ lại mấy hàm cần thiết cho chức năng
        bindProductDetailEvents();
        bindListAddCartEvents();
        
        if (typeof window.refreshOrderProductSelects === "function") {
            window.refreshOrderProductSelects();
        }

        syncCartWithGoogleSheet();
    }


    // --- (HÀM MỚI): CHỊU TRÁCH NHIỆM CHÍNH CHO VIỆC LOAD SLICE & GRID ---
    const btnLoadMore = document.getElementById("load-more-btn");
    
    function renderProductsGridSlice() {
        if (!window.allFilteredProducts.length) return;

        // 1. Lấy cấu hình cho thiết bị hiện tại
        const config = getLoadConfig();

        // 2. Xác định vị trí kết thúc miếng slice
        let sliceSize = currentSliceIndex === 0 ? config.initial : config.slice;
        let endIndex = Math.min(window.allFilteredProducts.length, currentSliceIndex + sliceSize);
        
        // 3. Cắt miếng slice từ kho
        let currentSliceProducts = window.allFilteredProducts.slice(currentSliceIndex, endIndex);

        // 4. Máp ra HTML cho từng thẻ sản phẩm trong slice
        const html = currentSliceProducts.map((product, index) => {
            // Xác định index thực tế của sản phẩm trong catalog (để mở popup detail)
            const realCatalogIndex = window.googleSheetCatalog.findIndex(p => p.name === product.name);

            return `
                <div class="product_listItem" data-category="${escapeHtml(product.categoryKey)}" style="opacity: 0; transform: translateY(20px);">
                    <div class="product_imgWrap">
                        <img
                            src="${escapeHtml(product.image || "https://placehold.co/600x400?text=No+Image")}"
                            alt="${escapeHtml(product.name || "San pham")}"
                            class="product_listItem--img product_detailImage"
                            data-product-index="${realCatalogIndex}"
                        >
                    </div>
                    <h3
                        class="product_listItem--title product_detailTrigger"
                        data-product-index="${realCatalogIndex}"
                    >${escapeHtml(product.name || "Chua co ten san pham")}</h3>
                    <div class="product_listItem--cost">${escapeHtml(formatPrice(product.price))}</div>
                    
                        <button
                        type="button"
                        class="product_hoverBtn"
                        data-name="${escapeHtml(product.name || "")}"
                        data-price="${escapeHtml(product.price || "")}"
                        >
                            <svg class="icon_addCart" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 640 640">
                                <path d="M24 48C10.7 48 0 58.7 0 72C0 85.3 10.7 96 24 96L69.3 96C73.2 96 76.5 98.8 77.2 102.6L129.3 388.9C135.5 423.1 165.3 448 200.1 448L456 448C469.3 448 480 437.3 480 424C480 410.7 469.3 400 456 400L200.1 400C188.5 400 178.6 391.7 176.5 380.3L171.4 352L475 352C505.8 352 532.2 330.1 537.9 299.8L568.9 133.9C572.6 114.2 557.5 96 537.4 96L124.7 96L124.3 94C119.5 67.4 96.3 48 69.2 48L24 48zM208 576C234.5 576 256 554.5 256 528C256 501.5 234.5 480 208 480C181.5 480 160 501.5 160 528C160 554.5 181.5 576 208 576zM432 576C458.5 576 480 554.5 480 528C480 501.5 458.5 480 432 480C405.5 480 384 501.5 384 528C384 554.5 405.5 576 432 576z" />
                            </svg>
                        </button>

                        
                    
                </div>
            `;
        }).join("");

        // 5. Append (thêm tiếp) HTML vào Grid, thay vì thay thế toàn bộ
        if (currentSliceIndex === 0) {
            productTrack.innerHTML = html;
        } else {
            productTrack.insertAdjacentHTML('beforeend', html);
        }

        // 6. Hiệu ứng fade in (hiện dần) cho sản phẩm mới
        requestAnimationFrame(() => {
            productTrack.querySelectorAll(".product_listItem").forEach(item => {
                item.style.opacity = '1';
                item.style.transform = 'translateY(0)';
            });
        });

        // 7. Cập nhật vị trí bắt đầu cho miếng slice tiếp theo
        currentSliceIndex = endIndex;

        // 8. Ẩn/Hiện nút "Xem thêm" dựa trên số lượng sản phẩm còn lại
        if (btnLoadMore) {
            btnLoadMore.style.display = currentSliceIndex < window.allFilteredProducts.length ? "inline-block" : "none";
        }

        // 9. (Giữ nguyên logic cũ): Gắn lại các sự kiện detail, add cart cho các thẻ mới render
        bindProductDetailEvents();
        bindListAddCartEvents();
    }


    // =========================================
    // XỬ LÝ FILTER & NÚT "XEM THÊM"
    // =========================================

    // A. XỬ LÝ NÚT FILTER CATEGORY
    const filterButtons = document.querySelectorAll(".product_nav--item");
    filterButtons.forEach(btn => {
        btn.addEventListener("click", () => {
            // 1. (Giữ nguyên cũ): Đổi active button
            filterButtons.forEach(b => b.classList.remove("active"));
            btn.classList.add("active");

            // 2. Lấy tên Category để filter
            const filterValue = btn.getAttribute("data-filter");

            // 3. Reset lại cái Grid và vị trí slice về số 0
            productTrack.innerHTML = '';
            currentSliceIndex = 0; 
            
            // 4. CHIA SLICE KHI FILTER NẰM Ở ĐÂY NÈ: 
            // - Dò kho full (window.googleSheetCatalog) để tìm sản phẩm Category đó.
            window.allFilteredProducts = window.googleSheetCatalog.filter(p => {
                if(filterValue === "all") return true; // Lấy hết
                return normalizeCategory(p.categoryKey) === normalizeCategory(filterValue); // So sánh category key
            });

            // - Load miếng đầu tiên của kho Filter đó ra
            renderProductsGridSlice();
        });
    });

    // B. XỬ LÝ NÚT "XEM THÊM"
    if(btnLoadMore){
        btnLoadMore.addEventListener("click", () => {
            // Khi bấm chỉ đơn giản là gọi hàm load miếng slice tiếp theo thôi haha
            renderProductsGridSlice();
        });
    }


  // --- THAY THẾ ĐOẠN GỌI GOOGLE SHEET ---
async function loadProductsFromBackend() {
    productTrack.innerHTML = '<p class="google-sheet-status">Đang tải sản phẩm từ hệ thống Hoa Xinh...</p>';

    try {
        // Gọi đến API Spring Boot của ông (mặc định lấy page 0, size 100 để test)
        const response = await fetch('http://localhost:8080/api/products?page=0&size=100');
        
        if (!response.ok) {
            throw new Error("Không thể kết nối đến Backend!");
        }

        const data = await response.json();
        
        // Spring Boot Pageable trả về data nằm trong mục .content
        const productsFromDB = data.content; 

        // Chuyển đổi dữ liệu từ Backend sang format mà Web của ông đang dùng
        const formattedProducts = productsFromDB.map(p => ({
            "ten": p.name,
            "gia": p.price,
            "hinh": p.imageURL,
            "tomtat": p.summary,
            "mota": p.descriptions,
            "loai": p.categoryId ? p.categoryId.name : "all" // Lấy tên danh mục
        }));

        renderProducts(formattedProducts);

    } catch (error) {
        productTrack.innerHTML = '<p class="google-sheet-status">Lỗi kết nối Backend. Hãy chắc chắn Server Spring Boot đang chạy!</p>';
        console.error("Backend fetch failed:", error);
    }
}

// Gọi hàm chạy khi trang web load
loadProductsFromBackend();

    // *********************Cart*****************************
    const cartWrap = document.querySelector(".main_cart--wrap");
    const totalCartEl = document.getElementById("total_cart");
    const checkAllEl = document.querySelector(".end_cart--checkAll input");
    const btnOrderCart = document.querySelector(".btn_buyCart");

    let carts = [];

    function saveCart() {
        localStorage.setItem("cart", JSON.stringify(carts));
    }

    function loadCart() {
        const data = localStorage.getItem("cart");
        carts = data ? JSON.parse(data) : [];
    }

    function syncCartWithGoogleSheet() {
        if (!carts.length || !window.googleSheetCatalog.length) return;

        let hasChanged = false;

        carts.forEach(cartItem => {
            // Tìm sản phẩm tương ứng trên Google Sheet (dựa vào tên)
            const liveProduct = window.googleSheetCatalog.find(p => p.name === cartItem.name);

            if (liveProduct) {
                const livePrice = Number(liveProduct.price);
                
                // Nếu giá trên LocalStorage khác giá trên Sheet thì cập nhật lại
                if (cartItem.price !== livePrice) {
                    cartItem.price = livePrice;
                    hasChanged = true;
                }
                
                // Cập nhật luôn hình ảnh phòng trường hợp ông đổi link ảnh trên Sheet
                if (cartItem.image !== liveProduct.image) {
                    cartItem.image = liveProduct.image;
                    hasChanged = true;
                }
            }
        });

        // Nếu phát hiện có sự thay đổi giá/hình thì lưu đè lại LocalStorage và vẽ lại giỏ hàng
        if (hasChanged) {
            saveCart();
            renderCart();
            updateCartTotal();
        }
    }

    const cartCountEl = document.querySelectorAll(".cart_count");

    function updateCartCount() {
        const totalQty = carts.reduce((sum, item) => sum + item.qty, 0);
        cartCountEl.forEach(qty => {
            qty.innerHTML = totalQty;
        })
    }

    loadCart();
    renderCart();
    updateCartTotal();
    updateCartCount();
    function formatPriceCart(price) {
        return Number(price).toLocaleString("vi-VN") + "đ";
    }

    

    function renderCart() {

        const emptyCartMsg = document.querySelector(".main_cart--empty");
        const cartDetailHeader = document.querySelector(".main_cart--detail");
        

        if (!carts.length) {
            cartWrap.innerHTML = "";
            totalCartEl.innerText = "0đ";
            
            
            emptyCartMsg.style.display = "flex"; 
            
            cartDetailHeader.style.display = "none";
            
            return;
        }

        emptyCartMsg.style.display = "none";
        
        cartDetailHeader.style.display = "flex"; 

        cartWrap.innerHTML = carts.map((item, index) => `
            <div class="main_cart--item" data-index="${index}">
                <div class="main_cart--itemBtnRemove">✕</div>
                <input type="checkbox" class="cart_check">
                <img src="${item.image}" class="main_cart--itemImg">

                <div class="main_cart--itemContent">
                    <h5 class="main_cart--itemName">${item.name}</h5>

                    <div class="main_cart--itemQty">
                        <div>Số lượng:</div>
                        <div class="qty-wrapper">
                            <button type="button" class="qty-btn qty-minus">-</button>
                            <input type="number" class="cart_qty" value="${item.qty}" min="1">
                            <button type="button" class="qty-btn qty-plus">+</button>
                        </div>
                    </div>

                    <div class="main_cart--itemPrice">
                        <div>Giá: </div>
                        <div>${formatPriceCart(item.price)}</div>
                    </div>
                </div>

                <div class="main_cart--itemTotal">${formatPriceCart(item.price * item.qty)}</div>
            </div>
        `).join("");

        bindCartEvents();
        updateCartTotal();
        updateCartCount();
    }

    function showAddCartSuccess() {
        
        const toast = document.createElement("div");
        toast.className = "toast-msg";
        toast.innerHTML = "✔ Đã thêm sản phẩm vào giỏ hàng!";

        
        document.body.appendChild(toast);

        setTimeout(() => {
            toast.classList.add("show");
        }, 10);

        setTimeout(() => {
            toast.classList.remove("show");
            
            setTimeout(() => {
                toast.remove();
            }, 400);
        }, 3000);
    }

   // HÀM XỬ LÝ ẢNH BAY VÀO GIỎ HÀNG
    function flyToCart(imgElement) {
        // Tìm TẤT CẢ các giỏ hàng đang có trong HTML
        const cartIcons = Array.from(document.querySelectorAll(".icon_cartWrap"));
        
        // Tuyệt chiêu: Lọc ra cái giỏ hàng ĐANG HIỂN THỊ trên màn hình (chiều rộng > 0)
        const activeCartIcon = cartIcons.find(icon => icon.offsetWidth > 0);

        if (!imgElement || !activeCartIcon) return;

        // 1. Lấy tọa độ
        const imgRect = imgElement.getBoundingClientRect();
        const cartRect = activeCartIcon.getBoundingClientRect();

        // 2. Tạo ảnh clone
        const flyingImg = document.createElement("img");
        flyingImg.src = imgElement.src;
        flyingImg.classList.add("flying-img");

        // 3. Đặt tọa độ đè lên ảnh gốc
        flyingImg.style.left = `${imgRect.left}px`;
        flyingImg.style.top = `${imgRect.top}px`;
        flyingImg.style.width = `${imgRect.width}px`;
        flyingImg.style.height = `${imgRect.height}px`;

        document.body.appendChild(flyingImg);

        // 4. Kích hoạt bay về giỏ hàng đang active
        setTimeout(() => {
            flyingImg.style.left = `${cartRect.left + cartRect.width / 2 - 10}px`; 
            flyingImg.style.top = `${cartRect.top + cartRect.height / 2 - 10}px`;
            flyingImg.style.width = "20px"; 
            flyingImg.style.height = "20px";
            flyingImg.style.opacity = "0.2"; 
        }, 10);

        // 5. Dọn rác & rung giỏ hàng
        setTimeout(() => {
            flyingImg.remove();
            activeCartIcon.classList.add("shake-cart");
            setTimeout(() => activeCartIcon.classList.remove("shake-cart"), 300);
        }, 800);
    }


    // HÀM DÙNG CHUNG ĐỂ THÊM VÀO GIỎ HÀNG
    function addItemToCart(name, price, image, qty) {
        const exist = carts.find(item => item.name === name);

        if (exist) {
            exist.qty += qty;
        } else {
            carts.push({ name, price, image, qty });
        }

        saveCart();
        renderCart();
        showAddCartSuccess();
    }

    // Xử lý click nút Thêm vào giỏ hàng Ở TRONG POPUP CHI TIẾT
    const popupAddCartBtn = document.querySelector(".popup_product--addCartBtn"); // Nút ở html gốc của popup
    if (popupAddCartBtn) {
        popupAddCartBtn.addEventListener("click", () => {
            const name = popupProductTitle.textContent;
            const price = Number(popupProductBuyBtn.dataset.price || 0);
            const image = popupProductImage.src;
            const qty = Number(popupProductQty.value || 1);

            flyToCart(popupProductImage);

            addItemToCart(name, price, image, qty);
        });
    }

// XỬ LÝ CLICK NÚT THÊM VÀO GIỎ HÀNG Ở NGOÀI DANH SÁCH
    function bindListAddCartEvents() {
        // Gom CẢ 2 NÚT: nút chữ (.list_addCartBtn) và nút Icon tròn (.product_hoverBtn)
        productTrack.querySelectorAll(".list_addCartBtn, .product_hoverBtn").forEach(btn => {
            if (btn.dataset.cartBound === "true") return; 
            
            btn.dataset.cartBound = "true";
            btn.addEventListener("click", (e) => {
                e.stopPropagation(); 

                const name = btn.dataset.name;
                const price = Number(btn.dataset.price);
                const qty = 1; 

                // Tìm thẻ cha để moi cái link ảnh ra (đề phòng nút Icon không lưu link ảnh)
                const productCard = btn.closest(".product_listItem");
                const imgElement = productCard ? productCard.querySelector(".product_listItem--img") : null;
                const image = btn.dataset.image || (imgElement ? imgElement.src : "");
                
                // Bắt đầu cho ảnh bay
                if (imgElement) flyToCart(imgElement);
                
                addItemToCart(name, price, image, qty);
            });
        });
    }

function bindCartEvents() {
    // 1. Nút xóa sản phẩm
    document.querySelectorAll(".main_cart--itemBtnRemove").forEach(btn => {
        btn.onclick = (e) => {
            e.stopPropagation();
            const index = e.target.closest(".main_cart--item").dataset.index;
            carts.splice(index, 1);
            updateCartCount();
            saveCart();
            renderCart(); // Xóa sản phẩm thì bắt buộc phải render lại để cập nhật danh sách
            
            // Nếu xóa hết giỏ hàng thì bỏ check "Chọn tất cả"
            if(carts.length === 0) checkAllEl.checked = false; 
        };
    });

    // 2. Ô input thay đổi số lượng
    document.querySelectorAll(".cart_qty").forEach(input => {
        input.oninput = (e) => {
            const itemEl = e.target.closest(".main_cart--item");
            const index = itemEl.dataset.index;
            let newQty = Number(e.target.value);

            // Đảm bảo số lượng không được nhỏ hơn 1
            if (newQty < 1) {
                newQty = 1;
                e.target.value = 1;
            }

            // Cập nhật mảng giỏ hàng và lưu lại
            carts[index].qty = newQty;
            saveCart();

            // Chỉ cập nhật DOM: tính lại thành tiền của RIÊNG sản phẩm này thay vì render toàn bộ giỏ hàng
            const itemTotalEl = itemEl.querySelector(".main_cart--itemTotal");
            if (itemTotalEl) {
                itemTotalEl.innerText = formatPriceCart(carts[index].price * carts[index].qty);
            }

            // Cập nhật tổng số lượng icon giỏ hàng và tổng tiền đang chọn
            updateCartCount();
            updateCartTotal();
        };
    });

    // 3. Tự động cập nhật nút "Chọn tất cả" nếu user check tay từng sản phẩm
    document.querySelectorAll(".cart_check").forEach(cb => {
        cb.onchange = () => {
            updateCartTotal();
            
            // Kiểm tra xem tất cả các sản phẩm có đang được check không
            const totalChecks = document.querySelectorAll(".cart_check").length;
            const checkedCount = document.querySelectorAll(".cart_check:checked").length;
            
            // Nếu tổng số check bằng với số sản phẩm, tự động đánh dấu checkAll
            checkAllEl.checked = (totalChecks > 0 && totalChecks === checkedCount);
        };
    });

    // 4. Bắt sự kiện bấm nút Trừ
    document.querySelectorAll(".qty-minus").forEach(btn => {
        btn.onclick = (e) => {
            // Tìm ô input nằm ngay kế bên nút Trừ
            const input = e.target.nextElementSibling;
            if (input.value > 1) {
                input.value = Number(input.value) - 1;
                // Kích hoạt sự kiện 'input' để nó tự chạy logic tính tiền ở trên
                input.dispatchEvent(new Event('input', { bubbles: true })); 
            }
        };
    });

    // 5. Bắt sự kiện bấm nút Cộng
    document.querySelectorAll(".qty-plus").forEach(btn => {
        btn.onclick = (e) => {
            // Tìm ô input nằm ngay kế bên trước nút Cộng
            const input = e.target.previousElementSibling;
            input.value = Number(input.value) + 1;
            // Kích hoạt sự kiện 'input' để nó tự chạy logic tính tiền ở trên
            input.dispatchEvent(new Event('input', { bubbles: true })); 
        };
    });
}

    function updateCartTotal() {
        let total = 0;

        document.querySelectorAll(".main_cart--item").forEach((itemEl, index) => {
            const checked = itemEl.querySelector(".cart_check").checked;

            if (checked) {
                total += carts[index].price * carts[index].qty;
            }
        });

        totalCartEl.innerText = formatPriceCart(total);
    }

    checkAllEl.onchange = () => {
        const checked = checkAllEl.checked;

        document.querySelectorAll(".cart_check").forEach(cb => {
            cb.checked = checked;
        });

        updateCartTotal();
    };

btnOrderCart.onclick = () => {
    const selectedItems = carts.filter((item, index) => {
        const el = document.querySelector(`.main_cart--item[data-index="${index}"]`);
        return el && el.querySelector(".cart_check").checked;
    });

    if (!selectedItems.length) {
        alert("Chọn ít nhất 1 sản phẩm");
        return;
    }

    window.productBox.innerHTML = "";
    window.openOrder(); // Mở cái Form điền Tên, SĐT, Địa chỉ

    selectedItems.forEach(item => {
        // QUAN TRỌNG: Truyền thêm ID vào hàm addProduct nếu hàm đó hỗ trợ
        window.addProduct(item.name, item.price, item.qty, item.id); 
    });

    window.updateTotal();
};
// ================= XỬ LÝ NÚT TĂNG GIẢM SỐ LƯỢNG Ở POPUP =================
const popupQtyMinus = document.getElementById("popup-qty-minus");
const popupQtyPlus = document.getElementById("popup-qty-plus");
const popupQtyInput = document.getElementById("qty");

if (popupQtyMinus && popupQtyPlus && popupQtyInput) {
    // Bấm nút Trừ
    popupQtyMinus.addEventListener("click", () => {
        let currentQty = Number(popupQtyInput.value);
        if (currentQty > 1) {
            popupQtyInput.value = currentQty - 1;
        }
    });

    // Bấm nút Cộng
    popupQtyPlus.addEventListener("click", () => {
        let currentQty = Number(popupQtyInput.value);
        popupQtyInput.value = currentQty + 1;
    });

    // Ngăn khách hàng tự gõ số âm hoặc số 0 vào ô
    popupQtyInput.addEventListener("input", () => {
        if (popupQtyInput.value !== "" && Number(popupQtyInput.value) < 1) {
            popupQtyInput.value = 1;
        }
    });
}

    // =========================================
    // XỬ LÝ THANH MUA HÀNG BÁM ĐÁY (ALL DEVICES)
    // =========================================
    const popupProductWrapObj = document.querySelector('.popup_productWrap');
    const popupActionObj = document.querySelector('.popup_productAction');

    let actionDummyObj = document.querySelector('.action-dummy-anchor');
    if (!actionDummyObj && popupActionObj) {
        actionDummyObj = document.createElement('div');
        actionDummyObj.className = 'action-dummy-anchor';
        popupActionObj.parentNode.insertBefore(actionDummyObj, popupActionObj);
    }

    if (popupProductWrapObj && popupActionObj && actionDummyObj) {
        popupProductWrapObj.addEventListener('scroll', () => {
            // Đã tháo xích window.innerWidth <= 768, giờ chạy trên mọi mặt trận
            const wrapRect = popupProductWrapObj.getBoundingClientRect();
            const dummyRect = actionDummyObj.getBoundingClientRect();

            if (dummyRect.top < wrapRect.top) {
                popupActionObj.classList.add('is-sticky');
                actionDummyObj.style.height = popupActionObj.offsetHeight + 'px'; 
                
                // BÍ KÍP ĐÂY: Ép chiều rộng và lề trái khớp 100% với cái Popup
                popupActionObj.style.width = wrapRect.width + 'px';
                popupActionObj.style.left = wrapRect.left + 'px';
                // Ép bám sát đúng cái mép đáy của Popup (vì PC popup không chạm đáy màn hình)
                popupActionObj.style.bottom = (window.innerHeight - wrapRect.bottom) + 'px';
            } else {
                popupActionObj.classList.remove('is-sticky');
                actionDummyObj.style.height = '0px'; 
                
                // Trả lại tự do cho CSS
                popupActionObj.style.width = '';
                popupActionObj.style.left = '';
                popupActionObj.style.bottom = '';
            }
        });
    }
    
})();
