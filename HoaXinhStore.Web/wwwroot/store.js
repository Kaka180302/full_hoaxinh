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

        return `${numericValue.toLocaleString("vi-VN")} Ä‘`;
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

// Xá»¬ LÃ CLICK Má»ž POPUP CHI TIáº¾T Sáº¢N PHáº¨M (PHIÃŠN Báº¢N CHáº®C CÃš 100%)
    function bindProductDetailEvents() {
        // TÃ¬m táº¥t cáº£ cÃ¡c áº£nh vÃ  tiÃªu Ä‘á» cÃ³ thá»ƒ click
        const triggers = document.querySelectorAll(".product_detailTrigger, .product_detailImage");
        
        triggers.forEach(trigger => {
            // DÃ¹ng onclick trá»±c tiáº¿p Ä‘Ã¨ lÃªn má»i thá»©
            trigger.onclick = function(e) {
                e.stopPropagation(); // NgÄƒn khÃ´ng cho tháº±ng Slider cÆ°á»›p click
                
                const index = this.getAttribute("data-product-index");
                console.log("ÄÃ£ click vÃ´ sáº£n pháº©m sá»‘:", index); // In ra Ä‘á»ƒ check
                
                if (index !== null) {
                    openProductDetail(Number(index));
                    console.log("ÄÃ£ gá»i lá»‡nh má»Ÿ Popup!");
                } else {
                    console.error("Lá»—i: KhÃ´ng tÃ¬m tháº¥y data-product-index");
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
    // JS CHO LOAD Sáº¢N PHáº¨M Dáº NG SLICE & GRID
    // =========================================

    // CÃ i Ä‘áº·t sá»‘ lÆ°á»£ng load
    const CONFIG_LOAD = {
        pc: {
            initial: 20, // Load láº§n Ä‘áº§u 20 cÃ¡i (5 hÃ ng x 4 cá»™t)
            slice: 12 // Má»—i láº§n Xem thÃªm load 12 cÃ¡i (3 hÃ ng x 4 cá»™t)
        },
        tablet: {
            initial: 18, // Load láº§n Ä‘áº§u 18 cÃ¡i (6 hÃ ng x 3 cá»™t)
            slice: 9 // Má»—i láº§n Xem thÃªm load 9 cÃ¡i (3 hÃ ng x 3 cá»™t)
        },
        mobile: {
            initial: 16, // Load láº§n Ä‘áº§u 16 cÃ¡i (8 hÃ ng x 2 cá»™t)
            slice: 10 // Má»—i láº§n Xem thÃªm load 10 cÃ¡i (5 hÃ ng x 2 cá»™t)
        }
    }

    // Biáº¿n tráº¡ng thÃ¡i: lÆ°u kho sáº£n pháº©m, vá»‹ trÃ­ hiá»‡n táº¡i vÃ  cÃ¡i kho sáº£n pháº©m Ä‘Ã£ lá»c
    window.allFilteredProducts = []; 
    let currentSliceIndex = 0; 

    // HÃ m láº¥y cáº¥u hÃ¬nh load theo thiáº¿t bá»‹
    function getLoadConfig() {
        if (window.innerWidth > 1024) return CONFIG_LOAD.pc;
        if (window.innerWidth > 768) return CONFIG_LOAD.tablet;
        return CONFIG_LOAD.mobile;
    }

    // --- (HÃ€M CÅ¨) renderProducts: BÃ¢y giá» chá»‰ lÃ m nhiá»‡m vá»¥ Cáº¤P PHÃT KHO Sáº¢N PHáº¨M vÃ  Báº®T Äáº¦U LOAD SLICE Äáº¦U TIÃŠN ---
    // --- HÃ€M RENDER Sáº¢N PHáº¨M ÄÃƒ ÄÆ¯á»¢C Dá»ŒN Dáº¸P Sáº CH Sáº¼ TÃ€N DÆ¯ SLIDER ---
    function renderProducts(products) {
        if (!products.length) {
            productTrack.innerHTML = '<p class="google-sheet-status">Google Sheet chÆ°a cÃ³ dá»¯ liá»‡u sáº£n pháº©m.</p>';
            return;
        }

        // 1. Cáº¥p phÃ¡t kho catalog sáº£n pháº©m (DÃ¹ng Ä‘á»ƒ hiá»ƒn thá»‹ lÃªn Web)
        window.googleSheetCatalog = products.map((product) => ({
            id: product.id,           // Láº¥y ID tháº­t tá»« DB
            name: product.ten,        // "ten" nÃ y lÃ  do mÃ¬nh Ä‘Ã£ map á»Ÿ hÃ m fetch lÃºc nÃ£y
            image: product.hinh,      // "hinh" tÆ°Æ¡ng tá»±
            price: product.gia,
            summary: product.tomtat,
            description: product.mota,
            categoryKey: normalizeCategory(product.loai)
        }));

        // 2. Cáº¥p phÃ¡t danh sÃ¡ch cho viá»‡c chá»n sáº£n pháº©m trong Ä‘Æ¡n hÃ ng
        window.googleSheetProductsForOrder = products.map((product) => ({
            id: product.id,
            name: product.ten,
            price: product.gia
        })).filter((product) => product.name);

        // Báº¯t Ä‘áº§u logic chia Slice
        window.allFilteredProducts = window.googleSheetCatalog;
        productTrack.innerHTML = '';
        currentSliceIndex = 0; 

        // Load slice Ä‘áº§u tiÃªn ra
        renderProductsGridSlice();

        // Chá»‰ giá»¯ láº¡i máº¥y hÃ m cáº§n thiáº¿t cho chá»©c nÄƒng
        bindProductDetailEvents();
        bindListAddCartEvents();
        
        if (typeof window.refreshOrderProductSelects === "function") {
            window.refreshOrderProductSelects();
        }

        syncCartWithGoogleSheet();
    }


    // --- (HÃ€M Má»šI): CHá»ŠU TRÃCH NHIá»†M CHÃNH CHO VIá»†C LOAD SLICE & GRID ---
    const btnLoadMore = document.getElementById("load-more-btn");
    
    function renderProductsGridSlice() {
        if (!window.allFilteredProducts.length) return;

        // 1. Láº¥y cáº¥u hÃ¬nh cho thiáº¿t bá»‹ hiá»‡n táº¡i
        const config = getLoadConfig();

        // 2. XÃ¡c Ä‘á»‹nh vá»‹ trÃ­ káº¿t thÃºc miáº¿ng slice
        let sliceSize = currentSliceIndex === 0 ? config.initial : config.slice;
        let endIndex = Math.min(window.allFilteredProducts.length, currentSliceIndex + sliceSize);
        
        // 3. Cáº¯t miáº¿ng slice tá»« kho
        let currentSliceProducts = window.allFilteredProducts.slice(currentSliceIndex, endIndex);

        // 4. MÃ¡p ra HTML cho tá»«ng tháº» sáº£n pháº©m trong slice
        const html = currentSliceProducts.map((product, index) => {
            // XÃ¡c Ä‘á»‹nh index thá»±c táº¿ cá»§a sáº£n pháº©m trong catalog (Ä‘á»ƒ má»Ÿ popup detail)
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

        // 5. Append (thÃªm tiáº¿p) HTML vÃ o Grid, thay vÃ¬ thay tháº¿ toÃ n bá»™
        if (currentSliceIndex === 0) {
            productTrack.innerHTML = html;
        } else {
            productTrack.insertAdjacentHTML('beforeend', html);
        }

        // 6. Hiá»‡u á»©ng fade in (hiá»‡n dáº§n) cho sáº£n pháº©m má»›i
        requestAnimationFrame(() => {
            productTrack.querySelectorAll(".product_listItem").forEach(item => {
                item.style.opacity = '1';
                item.style.transform = 'translateY(0)';
            });
        });

        // 7. Cáº­p nháº­t vá»‹ trÃ­ báº¯t Ä‘áº§u cho miáº¿ng slice tiáº¿p theo
        currentSliceIndex = endIndex;

        // 8. áº¨n/Hiá»‡n nÃºt "Xem thÃªm" dá»±a trÃªn sá»‘ lÆ°á»£ng sáº£n pháº©m cÃ²n láº¡i
        if (btnLoadMore) {
            btnLoadMore.style.display = currentSliceIndex < window.allFilteredProducts.length ? "inline-block" : "none";
        }

        // 9. (Giá»¯ nguyÃªn logic cÅ©): Gáº¯n láº¡i cÃ¡c sá»± kiá»‡n detail, add cart cho cÃ¡c tháº» má»›i render
        bindProductDetailEvents();
        bindListAddCartEvents();
    }


    // =========================================
    // Xá»¬ LÃ FILTER & NÃšT "XEM THÃŠM"
    // =========================================

    // A. Xá»¬ LÃ NÃšT FILTER CATEGORY
    const filterButtons = document.querySelectorAll(".product_nav--item");
    filterButtons.forEach(btn => {
        btn.addEventListener("click", () => {
            // 1. (Giá»¯ nguyÃªn cÅ©): Äá»•i active button
            filterButtons.forEach(b => b.classList.remove("active"));
            btn.classList.add("active");

            // 2. Láº¥y tÃªn Category Ä‘á»ƒ filter
            const filterValue = btn.getAttribute("data-filter");

            // 3. Reset láº¡i cÃ¡i Grid vÃ  vá»‹ trÃ­ slice vá» sá»‘ 0
            productTrack.innerHTML = '';
            currentSliceIndex = 0; 
            
            // 4. CHIA SLICE KHI FILTER Náº°M á»ž ÄÃ‚Y NÃˆ: 
            // - DÃ² kho full (window.googleSheetCatalog) Ä‘á»ƒ tÃ¬m sáº£n pháº©m Category Ä‘Ã³.
            window.allFilteredProducts = window.googleSheetCatalog.filter(p => {
                if(filterValue === "all") return true; // Láº¥y háº¿t
                return normalizeCategory(p.categoryKey) === normalizeCategory(filterValue); // So sÃ¡nh category key
            });

            // - Load miáº¿ng Ä‘áº§u tiÃªn cá»§a kho Filter Ä‘Ã³ ra
            renderProductsGridSlice();
        });
    });

    // B. Xá»¬ LÃ NÃšT "XEM THÃŠM"
    if(btnLoadMore){
        btnLoadMore.addEventListener("click", () => {
            // Khi báº¥m chá»‰ Ä‘Æ¡n giáº£n lÃ  gá»i hÃ m load miáº¿ng slice tiáº¿p theo thÃ´i haha
            renderProductsGridSlice();
        });
    }


  // --- THAY THáº¾ ÄOáº N Gá»ŒI GOOGLE SHEET ---
async function loadProductsFromBackend() {
    productTrack.innerHTML = '<p class="google-sheet-status">Äang táº£i sáº£n pháº©m tá»« há»‡ thá»‘ng Hoa Xinh...</p>';

    try {
        // Gá»i Ä‘áº¿n API Spring Boot cá»§a Ã´ng (máº·c Ä‘á»‹nh láº¥y page 0, size 100 Ä‘á»ƒ test)
        const response = await fetch('/api/products?page=0&size=100');
        
        if (!response.ok) {
            throw new Error("KhÃ´ng thá»ƒ káº¿t ná»‘i Ä‘áº¿n Backend!");
        }

        const data = await response.json();
        
        // Spring Boot Pageable tráº£ vá» data náº±m trong má»¥c .content
        const productsFromDB = data.content; 

        // Chuyá»ƒn Ä‘á»•i dá»¯ liá»‡u tá»« Backend sang format mÃ  Web cá»§a Ã´ng Ä‘ang dÃ¹ng
        const formattedProducts = productsFromDB.map(p => ({
            "ten": p.name,
            "gia": p.price,
            "hinh": p.imageURL,
            "tomtat": p.summary,
            "mota": p.descriptions,
            "loai": p.categoryId ? p.categoryId.name : "all" // Láº¥y tÃªn danh má»¥c
        }));

        renderProducts(formattedProducts);

    } catch (error) {
        productTrack.innerHTML = '<p class="google-sheet-status">Lá»—i káº¿t ná»‘i Backend. HÃ£y cháº¯c cháº¯n Server Spring Boot Ä‘ang cháº¡y!</p>';
        console.error("Backend fetch failed:", error);
    }
}

// Gá»i hÃ m cháº¡y khi trang web load
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
            // TÃ¬m sáº£n pháº©m tÆ°Æ¡ng á»©ng trÃªn Google Sheet (dá»±a vÃ o tÃªn)
            const liveProduct = window.googleSheetCatalog.find(p => p.name === cartItem.name);

            if (liveProduct) {
                const livePrice = Number(liveProduct.price);
                
                // Náº¿u giÃ¡ trÃªn LocalStorage khÃ¡c giÃ¡ trÃªn Sheet thÃ¬ cáº­p nháº­t láº¡i
                if (cartItem.price !== livePrice) {
                    cartItem.price = livePrice;
                    hasChanged = true;
                }
                
                // Cáº­p nháº­t luÃ´n hÃ¬nh áº£nh phÃ²ng trÆ°á»ng há»£p Ã´ng Ä‘á»•i link áº£nh trÃªn Sheet
                if (cartItem.image !== liveProduct.image) {
                    cartItem.image = liveProduct.image;
                    hasChanged = true;
                }
            }
        });

        // Náº¿u phÃ¡t hiá»‡n cÃ³ sá»± thay Ä‘á»•i giÃ¡/hÃ¬nh thÃ¬ lÆ°u Ä‘Ã¨ láº¡i LocalStorage vÃ  váº½ láº¡i giá» hÃ ng
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
        return Number(price).toLocaleString("vi-VN") + "Ä‘";
    }

    

    function renderCart() {

        const emptyCartMsg = document.querySelector(".main_cart--empty");
        const cartDetailHeader = document.querySelector(".main_cart--detail");
        

        if (!carts.length) {
            cartWrap.innerHTML = "";
            totalCartEl.innerText = "0Ä‘";
            
            
            emptyCartMsg.style.display = "flex"; 
            
            cartDetailHeader.style.display = "none";
            
            return;
        }

        emptyCartMsg.style.display = "none";
        
        cartDetailHeader.style.display = "flex"; 

        cartWrap.innerHTML = carts.map((item, index) => `
            <div class="main_cart--item" data-index="${index}">
                <div class="main_cart--itemBtnRemove">âœ•</div>
                <input type="checkbox" class="cart_check">
                <img src="${item.image}" class="main_cart--itemImg">

                <div class="main_cart--itemContent">
                    <h5 class="main_cart--itemName">${item.name}</h5>

                    <div class="main_cart--itemQty">
                        <div>Sá»‘ lÆ°á»£ng:</div>
                        <div class="qty-wrapper">
                            <button type="button" class="qty-btn qty-minus">-</button>
                            <input type="number" class="cart_qty" value="${item.qty}" min="1">
                            <button type="button" class="qty-btn qty-plus">+</button>
                        </div>
                    </div>

                    <div class="main_cart--itemPrice">
                        <div>GiÃ¡: </div>
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
        toast.innerHTML = "âœ” ÄÃ£ thÃªm sáº£n pháº©m vÃ o giá» hÃ ng!";

        
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

   // HÃ€M Xá»¬ LÃ áº¢NH BAY VÃ€O GIá»Ž HÃ€NG
    function flyToCart(imgElement) {
        // TÃ¬m Táº¤T Cáº¢ cÃ¡c giá» hÃ ng Ä‘ang cÃ³ trong HTML
        const cartIcons = Array.from(document.querySelectorAll(".icon_cartWrap"));
        
        // Tuyá»‡t chiÃªu: Lá»c ra cÃ¡i giá» hÃ ng ÄANG HIá»‚N THá»Š trÃªn mÃ n hÃ¬nh (chiá»u rá»™ng > 0)
        const activeCartIcon = cartIcons.find(icon => icon.offsetWidth > 0);

        if (!imgElement || !activeCartIcon) return;

        // 1. Láº¥y tá»a Ä‘á»™
        const imgRect = imgElement.getBoundingClientRect();
        const cartRect = activeCartIcon.getBoundingClientRect();

        // 2. Táº¡o áº£nh clone
        const flyingImg = document.createElement("img");
        flyingImg.src = imgElement.src;
        flyingImg.classList.add("flying-img");

        // 3. Äáº·t tá»a Ä‘á»™ Ä‘Ã¨ lÃªn áº£nh gá»‘c
        flyingImg.style.left = `${imgRect.left}px`;
        flyingImg.style.top = `${imgRect.top}px`;
        flyingImg.style.width = `${imgRect.width}px`;
        flyingImg.style.height = `${imgRect.height}px`;

        document.body.appendChild(flyingImg);

        // 4. KÃ­ch hoáº¡t bay vá» giá» hÃ ng Ä‘ang active
        setTimeout(() => {
            flyingImg.style.left = `${cartRect.left + cartRect.width / 2 - 10}px`; 
            flyingImg.style.top = `${cartRect.top + cartRect.height / 2 - 10}px`;
            flyingImg.style.width = "20px"; 
            flyingImg.style.height = "20px";
            flyingImg.style.opacity = "0.2"; 
        }, 10);

        // 5. Dá»n rÃ¡c & rung giá» hÃ ng
        setTimeout(() => {
            flyingImg.remove();
            activeCartIcon.classList.add("shake-cart");
            setTimeout(() => activeCartIcon.classList.remove("shake-cart"), 300);
        }, 800);
    }


    // HÃ€M DÃ™NG CHUNG Äá»‚ THÃŠM VÃ€O GIá»Ž HÃ€NG
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

    // Xá»­ lÃ½ click nÃºt ThÃªm vÃ o giá» hÃ ng á»ž TRONG POPUP CHI TIáº¾T
    const popupAddCartBtn = document.querySelector(".popup_product--addCartBtn"); // NÃºt á»Ÿ html gá»‘c cá»§a popup
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

// Xá»¬ LÃ CLICK NÃšT THÃŠM VÃ€O GIá»Ž HÃ€NG á»ž NGOÃ€I DANH SÃCH
    function bindListAddCartEvents() {
        // Gom Cáº¢ 2 NÃšT: nÃºt chá»¯ (.list_addCartBtn) vÃ  nÃºt Icon trÃ²n (.product_hoverBtn)
        productTrack.querySelectorAll(".list_addCartBtn, .product_hoverBtn").forEach(btn => {
            if (btn.dataset.cartBound === "true") return; 
            
            btn.dataset.cartBound = "true";
            btn.addEventListener("click", (e) => {
                e.stopPropagation(); 

                const name = btn.dataset.name;
                const price = Number(btn.dataset.price);
                const qty = 1; 

                // TÃ¬m tháº» cha Ä‘á»ƒ moi cÃ¡i link áº£nh ra (Ä‘á» phÃ²ng nÃºt Icon khÃ´ng lÆ°u link áº£nh)
                const productCard = btn.closest(".product_listItem");
                const imgElement = productCard ? productCard.querySelector(".product_listItem--img") : null;
                const image = btn.dataset.image || (imgElement ? imgElement.src : "");
                
                // Báº¯t Ä‘áº§u cho áº£nh bay
                if (imgElement) flyToCart(imgElement);
                
                addItemToCart(name, price, image, qty);
            });
        });
    }

function bindCartEvents() {
    // 1. NÃºt xÃ³a sáº£n pháº©m
    document.querySelectorAll(".main_cart--itemBtnRemove").forEach(btn => {
        btn.onclick = (e) => {
            e.stopPropagation();
            const index = e.target.closest(".main_cart--item").dataset.index;
            carts.splice(index, 1);
            updateCartCount();
            saveCart();
            renderCart(); // XÃ³a sáº£n pháº©m thÃ¬ báº¯t buá»™c pháº£i render láº¡i Ä‘á»ƒ cáº­p nháº­t danh sÃ¡ch
            
            // Náº¿u xÃ³a háº¿t giá» hÃ ng thÃ¬ bá» check "Chá»n táº¥t cáº£"
            if(carts.length === 0) checkAllEl.checked = false; 
        };
    });

    // 2. Ã” input thay Ä‘á»•i sá»‘ lÆ°á»£ng
    document.querySelectorAll(".cart_qty").forEach(input => {
        input.oninput = (e) => {
            const itemEl = e.target.closest(".main_cart--item");
            const index = itemEl.dataset.index;
            let newQty = Number(e.target.value);

            // Äáº£m báº£o sá»‘ lÆ°á»£ng khÃ´ng Ä‘Æ°á»£c nhá» hÆ¡n 1
            if (newQty < 1) {
                newQty = 1;
                e.target.value = 1;
            }

            // Cáº­p nháº­t máº£ng giá» hÃ ng vÃ  lÆ°u láº¡i
            carts[index].qty = newQty;
            saveCart();

            // Chá»‰ cáº­p nháº­t DOM: tÃ­nh láº¡i thÃ nh tiá»n cá»§a RIÃŠNG sáº£n pháº©m nÃ y thay vÃ¬ render toÃ n bá»™ giá» hÃ ng
            const itemTotalEl = itemEl.querySelector(".main_cart--itemTotal");
            if (itemTotalEl) {
                itemTotalEl.innerText = formatPriceCart(carts[index].price * carts[index].qty);
            }

            // Cáº­p nháº­t tá»•ng sá»‘ lÆ°á»£ng icon giá» hÃ ng vÃ  tá»•ng tiá»n Ä‘ang chá»n
            updateCartCount();
            updateCartTotal();
        };
    });

    // 3. Tá»± Ä‘á»™ng cáº­p nháº­t nÃºt "Chá»n táº¥t cáº£" náº¿u user check tay tá»«ng sáº£n pháº©m
    document.querySelectorAll(".cart_check").forEach(cb => {
        cb.onchange = () => {
            updateCartTotal();
            
            // Kiá»ƒm tra xem táº¥t cáº£ cÃ¡c sáº£n pháº©m cÃ³ Ä‘ang Ä‘Æ°á»£c check khÃ´ng
            const totalChecks = document.querySelectorAll(".cart_check").length;
            const checkedCount = document.querySelectorAll(".cart_check:checked").length;
            
            // Náº¿u tá»•ng sá»‘ check báº±ng vá»›i sá»‘ sáº£n pháº©m, tá»± Ä‘á»™ng Ä‘Ã¡nh dáº¥u checkAll
            checkAllEl.checked = (totalChecks > 0 && totalChecks === checkedCount);
        };
    });

    // 4. Báº¯t sá»± kiá»‡n báº¥m nÃºt Trá»«
    document.querySelectorAll(".qty-minus").forEach(btn => {
        btn.onclick = (e) => {
            // TÃ¬m Ã´ input náº±m ngay káº¿ bÃªn nÃºt Trá»«
            const input = e.target.nextElementSibling;
            if (input.value > 1) {
                input.value = Number(input.value) - 1;
                // KÃ­ch hoáº¡t sá»± kiá»‡n 'input' Ä‘á»ƒ nÃ³ tá»± cháº¡y logic tÃ­nh tiá»n á»Ÿ trÃªn
                input.dispatchEvent(new Event('input', { bubbles: true })); 
            }
        };
    });

    // 5. Báº¯t sá»± kiá»‡n báº¥m nÃºt Cá»™ng
    document.querySelectorAll(".qty-plus").forEach(btn => {
        btn.onclick = (e) => {
            // TÃ¬m Ã´ input náº±m ngay káº¿ bÃªn trÆ°á»›c nÃºt Cá»™ng
            const input = e.target.previousElementSibling;
            input.value = Number(input.value) + 1;
            // KÃ­ch hoáº¡t sá»± kiá»‡n 'input' Ä‘á»ƒ nÃ³ tá»± cháº¡y logic tÃ­nh tiá»n á»Ÿ trÃªn
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
        alert("Chá»n Ã­t nháº¥t 1 sáº£n pháº©m");
        return;
    }

    window.productBox.innerHTML = "";
    window.openOrder(); // Má»Ÿ cÃ¡i Form Ä‘iá»n TÃªn, SÄT, Äá»‹a chá»‰

    selectedItems.forEach(item => {
        // QUAN TRá»ŒNG: Truyá»n thÃªm ID vÃ o hÃ m addProduct náº¿u hÃ m Ä‘Ã³ há»— trá»£
        window.addProduct(item.name, item.price, item.qty, item.id); 
    });

    window.updateTotal();
};
// ================= Xá»¬ LÃ NÃšT TÄ‚NG GIáº¢M Sá» LÆ¯á»¢NG á»ž POPUP =================
const popupQtyMinus = document.getElementById("popup-qty-minus");
const popupQtyPlus = document.getElementById("popup-qty-plus");
const popupQtyInput = document.getElementById("qty");

if (popupQtyMinus && popupQtyPlus && popupQtyInput) {
    // Báº¥m nÃºt Trá»«
    popupQtyMinus.addEventListener("click", () => {
        let currentQty = Number(popupQtyInput.value);
        if (currentQty > 1) {
            popupQtyInput.value = currentQty - 1;
        }
    });

    // Báº¥m nÃºt Cá»™ng
    popupQtyPlus.addEventListener("click", () => {
        let currentQty = Number(popupQtyInput.value);
        popupQtyInput.value = currentQty + 1;
    });

    // NgÄƒn khÃ¡ch hÃ ng tá»± gÃµ sá»‘ Ã¢m hoáº·c sá»‘ 0 vÃ o Ã´
    popupQtyInput.addEventListener("input", () => {
        if (popupQtyInput.value !== "" && Number(popupQtyInput.value) < 1) {
            popupQtyInput.value = 1;
        }
    });
}

    // =========================================
    // Xá»¬ LÃ THANH MUA HÃ€NG BÃM ÄÃY (ALL DEVICES)
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
            // ÄÃ£ thÃ¡o xÃ­ch window.innerWidth <= 768, giá» cháº¡y trÃªn má»i máº·t tráº­n
            const wrapRect = popupProductWrapObj.getBoundingClientRect();
            const dummyRect = actionDummyObj.getBoundingClientRect();

            if (dummyRect.top < wrapRect.top) {
                popupActionObj.classList.add('is-sticky');
                actionDummyObj.style.height = popupActionObj.offsetHeight + 'px'; 
                
                // BÃ KÃP ÄÃ‚Y: Ã‰p chiá»u rá»™ng vÃ  lá» trÃ¡i khá»›p 100% vá»›i cÃ¡i Popup
                popupActionObj.style.width = wrapRect.width + 'px';
                popupActionObj.style.left = wrapRect.left + 'px';
                // Ã‰p bÃ¡m sÃ¡t Ä‘Ãºng cÃ¡i mÃ©p Ä‘Ã¡y cá»§a Popup (vÃ¬ PC popup khÃ´ng cháº¡m Ä‘Ã¡y mÃ n hÃ¬nh)
                popupActionObj.style.bottom = (window.innerHeight - wrapRect.bottom) + 'px';
            } else {
                popupActionObj.classList.remove('is-sticky');
                actionDummyObj.style.height = '0px'; 
                
                // Tráº£ láº¡i tá»± do cho CSS
                popupActionObj.style.width = '';
                popupActionObj.style.left = '';
                popupActionObj.style.bottom = '';
            }
        });
    }
    
})();

