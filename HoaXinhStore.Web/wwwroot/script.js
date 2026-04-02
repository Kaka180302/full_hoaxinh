window.addEventListener("DOMContentLoaded", () => {
  
  // *******************Header********************
const header = document.querySelector('.header_info');

window.addEventListener('scroll', () => {
    if (!header) return;
    if (window.scrollY > 50) {
        header.classList.add('scrolled');
    } else {
        header.classList.remove('scrolled');
    }
});

const headerMobile = document.querySelector('.header_mobile');

window.addEventListener('scroll', () => {
    if (!headerMobile) return;
    if (window.scrollY > 50) {
        headerMobile.classList.add('scrolled');
    } else {
        headerMobile.classList.remove('scrolled');
    }
});

// *******************Slider_top****************

const slides = document.querySelector(".slider_item")

let index = 1
const total = 4

if (slides) {
  slides.style.transform = "translateX(-100%)"
}

function updateDot(i){
  document.getElementById("s"+i).checked = true
}

function nextSlide(){
  if (!slides) return

  index++

  slides.style.transition = "0.6s"
  slides.style.transform = `translateX(-${index*100}%)`

  if(index <= total){
    updateDot(index)
  }

  if(index == total + 1){

    setTimeout(()=>{
      slides.style.transition = "none"
      index = 1
      slides.style.transform = "translateX(-100%)"
      updateDot(1)
    },600)

  }

}
if (slides) {
  setInterval(nextSlide,3000)
}
// *****************End_Slider_Top*********************

// **************************Form_Popup***********************

const orderPopup = document.getElementById("order")
const closeBtn = document.querySelector(".order__close")
const productBtnSelector = ".product_buyBtn, .product_buyBtn--detail"

const productBox = document.getElementById("orderProducts")
const addProductBtn = document.getElementById("addProduct")
const totalEl = document.getElementById("orderTotal")

const paymentMethodInputs = document.querySelectorAll('input[name="PaymentMethod"]')
const qrBox = document.getElementById("qrBox")
const invoiceToggle = document.getElementById("invoiceToggle")
const isExportInvoiceInput = document.getElementById("isExportInvoiceInput")
const invoiceFields = document.getElementById("invoiceFields")
const invoiceInputs = document.querySelectorAll("#companyName,#companyTaxCode,#companyAddress,#companyEmail")

let products = []
let isBodyScrollLocked = false
let lockedScrollY = 0

function lockBodyScroll() {
  if (isBodyScrollLocked) return
  lockedScrollY = window.scrollY || window.pageYOffset || 0
  document.documentElement.classList.add("modal-lock")
  document.body.classList.add("modal-lock")
  document.body.style.top = `-${lockedScrollY}px`
  isBodyScrollLocked = true
}

function unlockBodyScroll() {
  if (!isBodyScrollLocked) return
  document.documentElement.classList.remove("modal-lock")
  document.body.classList.remove("modal-lock")
  document.body.style.top = ""
  window.scrollTo(0, lockedScrollY)
  isBodyScrollLocked = false
}

function syncGlobalScrollLock() {
  const popupDetail = document.querySelector(".popup_product")
  const popupDetailOpen = !!popupDetail && popupDetail.style.display !== "none" && popupDetail.style.display !== ""
  const cartOpen = !!document.querySelector(".cart.active_cart")
  const shouldLockForCart = cartOpen && window.innerWidth <= 768
  const hasOverlayOpen =
    !!document.querySelector(".order.active") ||
    !!document.querySelector(".policy_popup.active") ||
    shouldLockForCart ||
    popupDetailOpen

  if (hasOverlayOpen) {
    lockBodyScroll()
  } else {
    unlockBodyScroll()
  }
}

function normalizePolicyText(rawContent) {
  if (rawContent === null || rawContent === undefined) return ""
  let text = String(rawContent)
    .replace(/<br\s*\/?>/gi, "\n")
    .replace(/<\/p>/gi, "\n\n")
    .replace(/<\/div>/gi, "\n")
    .replace(/<li>/gi, "- ")
    .replace(/<\/li>/gi, "\n")
    .replace(/<[^>]*>/g, "")

  const decodeEl = document.createElement("textarea")
  decodeEl.innerHTML = text
  text = decodeEl.value

  text = text
    .replace(/\r\n/g, "\n")
    .replace(/\u00a0/g, " ")
    .split("\n")
    .map((line) => line.replace(/[ \t]+/g, " ").trim())
    .join("\n")
    .replace(/\n{3,}/g, "\n\n")
    .replace(/\n +/g, "\n")
    .replace(/[ \t]+\n/g, "\n")

  return text.trim()
}

function getSheetProductsForOrder() {
  if (Array.isArray(window.googleSheetProductsForOrder) && window.googleSheetProductsForOrder.length) {
    return window.googleSheetProductsForOrder
  }

  const domProducts = Array.from(document.querySelectorAll(".product_track .product_listItem"))
    .map((item) => ({
      id: Number(item.dataset.productId || 0),
      name: item.dataset.productName || "",
      price: Number(item.dataset.productPrice || 0)
    }))
    .filter((p) => p.id > 0 && p.name)

  window.googleSheetProductsForOrder = domProducts
  return domProducts
}

function buildOrderOptionsHtml(selectedName = "", selectedPrice = "") {
  const sheetProducts = getSheetProductsForOrder()

  let optionsHtml = '<option value="">--Chon san pham--</option>'

  if (sheetProducts.length) {
    optionsHtml += sheetProducts.map((product) => {
      const isSelected = product.name === selectedName ? " selected" : ""
      const safeName = String(product.name || "").replace(/"/g, "&quot;")
      const safePrice = String(product.price || "").replace(/"/g, "&quot;")
      const productId = Number(product.id || 0)
      return `<option value="${productId}" data-price="${safePrice}" data-product-id="${productId}"${isSelected}>${safeName}</option>`
    }).join("")

    return optionsHtml
  }

  return optionsHtml
}

function refreshOrderProductSelects() {
  getSheetProductsForOrder()

  document.querySelectorAll(".order__productRow .order__select").forEach((select) => {
    const selectedOption = select.options[select.selectedIndex]
    const selectedName = selectedOption ? selectedOption.text : ""
    const selectedPrice = selectedOption ? selectedOption.dataset.price || "" : ""

    select.innerHTML = buildOrderOptionsHtml(selectedName, selectedPrice)

    if (selectedName && !Array.from(select.options).some((option) => option.text === selectedName)) {
      const customOption = document.createElement("option")
      customOption.text = selectedName
      customOption.value = selectedName
      customOption.dataset.price = selectedPrice || 0
      customOption.selected = true
      select.appendChild(customOption)
    }
  })

  updateTotal()
  checkForm()
}

function openOrder(){
  orderPopup.classList.add("active")
  syncGlobalScrollLock()
  checkForm()
}

window.openOrder = openOrder

getSheetProductsForOrder()

function resetOrderForm(){

    // xoÃ¡ danh sÃ¡ch sáº£n pháº©m
    productBox.innerHTML = ""

    // reset tá»•ng tiá»n
    totalEl.innerText = "0"

    // reset form
    document.getElementById("orderForm").reset()
    submitBtn.classList.remove("loading")
    submitBtn.textContent = "Đặt hàng"

    // áº©n QR
    qrBox.style.display = "none"
    checkForm()

}

function closeOrder(){
  orderPopup.classList.remove("active")

  resetOrderForm()

  if(invoiceToggle){
    invoiceToggle.checked = false
  }

  if(invoiceFields){
    invoiceFields.classList.remove("active")
  }
  syncGlobalScrollLock()
}

if(invoiceToggle){
  invoiceToggle.onchange = ()=>{
    invoiceFields.classList.toggle("active", invoiceToggle.checked)
    if (isExportInvoiceInput) {
      isExportInvoiceInput.value = invoiceToggle.checked ? "true" : "false"
    }
    checkForm()
  }
}

function getSelectedPaymentMethod() {
  const checked = document.querySelector('input[name="PaymentMethod"]:checked')
  return checked ? checked.value : "COD"
}

closeBtn.onclick = closeOrder

const popupProduct = document.querySelector(".popup_product")
const popupProductOverlay = document.querySelector(".popup_product--overlay")
const popupProductClose = document.getElementById("btn_closePopup")
const popupProductImage = document.querySelector(".popup_product--img")
const popupProductTitle = document.querySelector(".popup_product--title")
const popupProductPrice = document.querySelector(".popup_product--price")
const popupProductSummary = document.querySelector(".popup_product--summary")
const popupProductDesc = document.querySelector(".popup_product--desc")
const popupProductBuyBtn = document.querySelector(".popup_product--buyBtn")
const popupProductAddCartBtn = document.querySelector(".popup_product--addCartBtn")
const popupProductQty = document.getElementById("qty")
const popupSuggestList = document.querySelector(".popup_productSuggestList")
const popupSuggestViewport = document.querySelector(".popup_productSuggestViewport")
const popupSuggestPrevBtn = document.querySelector(".popup_productSuggestNav--prev")
const popupSuggestNextBtn = document.querySelector(".popup_productSuggestNav--next")
let popupSuggestCount = 0

function showAppMessage(message, isError = false) {
  const popup = document.getElementById("successPopup")
  const textEl = popup?.querySelector("p")
  if (!popup || !textEl) return
  textEl.textContent = message
  popup.classList.add("active")
  popup.classList.toggle("is-error", !!isError)
}

function normalizeCartItems() {
  carts = (Array.isArray(carts) ? carts : []).map((item) => ({
    id: Number(item.id || 0),
    name: item.name || "",
    price: Number(item.price || 0),
    image: item.image || "",
    qty: Math.max(1, Number(item.qty || 1)),
    checked: item.checked !== false
  }))
}

function flyToCart(imgElement) {
  const cartIcons = Array.from(document.querySelectorAll(".icon_cartWrap"))
  const activeCartIcon = cartIcons.find(icon => icon.offsetWidth > 0)
  if (!imgElement || !activeCartIcon) return

  const imgRect = imgElement.getBoundingClientRect()
  const cartRect = activeCartIcon.getBoundingClientRect()
  const flyingImg = document.createElement("img")
  flyingImg.src = imgElement.src
  flyingImg.classList.add("flying-img")
  flyingImg.style.left = `${imgRect.left}px`
  flyingImg.style.top = `${imgRect.top}px`
  flyingImg.style.width = `${imgRect.width}px`
  flyingImg.style.height = `${imgRect.height}px`
  document.body.appendChild(flyingImg)

  setTimeout(() => {
    flyingImg.style.left = `${cartRect.left + cartRect.width / 2 - 10}px`
    flyingImg.style.top = `${cartRect.top + cartRect.height / 2 - 10}px`
    flyingImg.style.width = "20px"
    flyingImg.style.height = "20px"
    flyingImg.style.opacity = "0.2"
  }, 10)

  setTimeout(() => {
    flyingImg.remove()
    activeCartIcon.classList.add("shake-cart")
    setTimeout(() => activeCartIcon.classList.remove("shake-cart"), 300)
  }, 800)
}

function getSuggestedProducts(currentProductId) {
  const allCards = Array.from(document.querySelectorAll(".product_track .product_listItem"))
  return allCards
    .filter((item) => Number(item.dataset.productId || 0) !== Number(currentProductId))
    .slice(0, 9)
}

function updateSuggestNavState() {
  if (!popupSuggestViewport || !popupSuggestPrevBtn || !popupSuggestNextBtn) return

  if (popupSuggestCount <= 3) {
    popupSuggestPrevBtn.disabled = true
    popupSuggestNextBtn.disabled = true
    return
  }

  const maxScrollLeft = popupSuggestViewport.scrollWidth - popupSuggestViewport.clientWidth
  popupSuggestPrevBtn.disabled = popupSuggestViewport.scrollLeft <= 4
  popupSuggestNextBtn.disabled = popupSuggestViewport.scrollLeft >= maxScrollLeft - 4
}

function renderSuggestedProducts(currentProductId) {
  if (!popupSuggestList || !popupSuggestViewport) return

  const suggested = getSuggestedProducts(currentProductId)
  popupSuggestCount = suggested.length
  if (!suggested.length) {
    popupSuggestList.innerHTML = `<div class="popup_productSuggestEmpty">Chưa có sản phẩm gợi ý khác.</div>`
    updateSuggestNavState()
    return
  }

  popupSuggestList.innerHTML = suggested.map((item) => {
    const name = item.dataset.productName || ""
    const price = Number(item.dataset.productPrice || 0)
    const image = item.dataset.productImage || "https://placehold.co/600x400?text=No+Image"
    const productId = Number(item.dataset.productId || 0)
    return `
      <article class="popup_productSuggestItem" data-suggest-id="${productId}">
        <img src="${image}" alt="${name}">
        <h4 class="popup_productSuggestTitle">${name}</h4>
        <p class="popup_productSuggestPrice">${price.toLocaleString("vi-VN")} đ</p>
      </article>`
  }).join("")

  popupSuggestViewport.scrollLeft = 0
  requestAnimationFrame(() => {
    updateSuggestNavState()
    setTimeout(updateSuggestNavState, 120)
  })
}

function openProductDetailFromCard(card) {
  if (!card || !popupProduct) return

  const name = card.dataset.productName || ""
  const price = Number(card.dataset.productPrice || 0)
  const image = card.dataset.productImage || ""
  const summary = card.dataset.productSummary || ""
  const description = card.dataset.productDescription || ""
  const productId = Number(card.dataset.productId || 0)

  if (popupProductImage) {
    popupProductImage.src = image || "https://placehold.co/600x400?text=No+Image"
    popupProductImage.alt = name
  }

  if (popupProductTitle) popupProductTitle.textContent = name
  if (popupProductPrice) popupProductPrice.textContent = `${price.toLocaleString("vi-VN")} đ`
  if (popupProductSummary) {
    popupProductSummary.textContent = summary
    popupProductSummary.style.display = summary ? "block" : "none"
  }
  if (popupProductDesc) {
    popupProductDesc.textContent = description || "Chua co mo ta san pham."
  }

  if (popupProductBuyBtn) {
    popupProductBuyBtn.dataset.id = String(productId)
    popupProductBuyBtn.dataset.name = name
    popupProductBuyBtn.dataset.price = String(price)
  }

  renderSuggestedProducts(productId)
  popupProduct.style.display = "flex"
  syncGlobalScrollLock()
  requestAnimationFrame(updatePopupStickyAction)
}

function closeProductDetail() {
  if (popupProduct) {
    popupProduct.style.display = "none"
  }
  if (popupProductAction && actionDummy) {
    popupProductAction.classList.remove("is-sticky")
    popupProductAction.style.width = ""
    popupProductAction.style.left = ""
    popupProductAction.style.bottom = ""
    actionDummy.classList.remove("show")
  }
  syncGlobalScrollLock()
}

document.querySelectorAll(".product_detailImage, .product_detailTrigger").forEach((el) => {
  el.addEventListener("click", (e) => {
    const card = e.target.closest(".product_listItem")
    openProductDetailFromCard(card)
  })
})

if (popupProductClose) popupProductClose.addEventListener("click", closeProductDetail)
if (popupProductOverlay) popupProductOverlay.addEventListener("click", closeProductDetail)

const popupProductWrap = document.querySelector(".popup_productWrap")
const popupProductAction = document.querySelector(".popup_productAction")
let actionDummy = document.querySelector(".action-dummy")
let popupActionSticky = false

if (!actionDummy && popupProductAction) {
  actionDummy = document.createElement("div")
  actionDummy.className = "action-dummy"
  popupProductAction.parentNode?.insertBefore(actionDummy, popupProductAction)
}

function updatePopupStickyAction() {
  if (!popupProductWrap || !popupProductAction || !actionDummy) return
  if (window.innerWidth > 768) {
    popupActionSticky = false
    popupProductAction.classList.remove("is-sticky")
    popupProductAction.style.width = ""
    popupProductAction.style.left = ""
    popupProductAction.style.bottom = ""
    actionDummy.classList.remove("show")
    return
  }

  const wrapRect = popupProductWrap.getBoundingClientRect()
  const scrollTop = popupProductWrap.scrollTop
  const shouldStick = popupActionSticky ? scrollTop > 50 : scrollTop > 110

  if (shouldStick) {
    popupActionSticky = true
    popupProductAction.classList.add("is-sticky")
    popupProductAction.style.width = `${wrapRect.width}px`
    popupProductAction.style.left = `${wrapRect.left}px`
    popupProductAction.style.bottom = `${Math.max(0, window.innerHeight - wrapRect.bottom)}px`
    actionDummy.classList.add("show")
  } else {
    popupActionSticky = false
    popupProductAction.classList.remove("is-sticky")
    popupProductAction.style.width = ""
    popupProductAction.style.left = ""
    popupProductAction.style.bottom = ""
    actionDummy.classList.remove("show")
  }
}

if (popupProductWrap) {
  popupProductWrap.addEventListener("scroll", updatePopupStickyAction)
  popupProductWrap.addEventListener("touchmove", updatePopupStickyAction, { passive: true })
}
window.addEventListener("resize", updatePopupStickyAction)

if (popupSuggestViewport) {
  popupSuggestViewport.addEventListener("scroll", updateSuggestNavState)
}
window.addEventListener("resize", updateSuggestNavState)

if (popupSuggestPrevBtn && popupSuggestViewport) {
  popupSuggestPrevBtn.addEventListener("click", () => {
    popupSuggestViewport.scrollBy({ left: -320, behavior: "smooth" })
  })
}

if (popupSuggestNextBtn && popupSuggestViewport) {
  popupSuggestNextBtn.addEventListener("click", () => {
    popupSuggestViewport.scrollBy({ left: 320, behavior: "smooth" })
  })
}

if (popupSuggestList) {
  popupSuggestList.addEventListener("click", (e) => {
    const cardEl = e.target.closest(".popup_productSuggestItem")
    if (!cardEl) return
    const productId = Number(cardEl.dataset.suggestId || 0)
    if (!productId) return
    const sourceCard = document.querySelector(`.product_listItem[data-product-id="${productId}"]`)
    if (sourceCard) {
      openProductDetailFromCard(sourceCard)
    }
  })
}

if (popupProductBuyBtn) {
  popupProductBuyBtn.addEventListener("click", () => {
    openOrder()
    addProduct(
      popupProductBuyBtn.dataset.name || "",
      popupProductBuyBtn.dataset.price || 0,
      1,
      Number(popupProductBuyBtn.dataset.id || 0)
    )
    closeProductDetail()
  })
}

if (popupProductAddCartBtn) {
  popupProductAddCartBtn.addEventListener("click", () => {
    const productId = Number(popupProductBuyBtn?.dataset.id || 0)
    const qty = Math.max(1, Number(popupProductQty?.value || 1))
    const card = document.querySelector(`.product_listItem[data-product-id="${productId}"]`)
    if (!productId || !card) return

    const payload = {
      id: productId,
      name: popupProductBuyBtn?.dataset.name || card.dataset.productName || "",
      price: Number(popupProductBuyBtn?.dataset.price || card.dataset.productPrice || 0),
      image: card.dataset.productImage || ""
    }

    for (let i = 0; i < qty; i++) {
      addItemToCart(payload)
    }

    const imgEl = card.querySelector(".product_listItem--img")
    if (imgEl) flyToCart(imgEl)
  })
}



function bindDynamicProductButtons() {
  document.querySelectorAll(productBtnSelector).forEach(btn=>{
    if (btn.dataset.orderBound === "true") {
      return
    }

    btn.dataset.orderBound = "true"

    btn.addEventListener("click",()=>{

        openOrder()

        addProduct(
          btn.dataset.name,
          btn.dataset.price,
          1,
          Number(btn.dataset.id || 0)
        )
    })
  })
}

bindDynamicProductButtons()
window.bindDynamicProductButtons = bindDynamicProductButtons
window.refreshOrderProductSelects = refreshOrderProductSelects

window.addProduct = addProduct

function addProduct(name = "", price = 0, qty = 1, productId = 0){
  console.log("ADD PRODUCT:", name, price, qty);

  const row = document.createElement("div")
  row.className = "order__productRow"

  row.innerHTML = `

    <select class="order__select">
      ${buildOrderOptionsHtml(name, price)}
    </select>

    <input
      type="number"
      name = "qty"
      class="order__qty"
      value="${qty}"
      min="1"
    >

    <button type="button" class="order__remove">
      x
    </button>
  `

  productBox.appendChild(row)

  if(name){

    const select = row.querySelector("select")
    let hasMatchedOption = false

    for(let option of select.options){

      if(option.text === name){
        option.selected = true
        hasMatchedOption = true
      }

    }

    if(!hasMatchedOption){
      const customOption = document.createElement("option")
      customOption.text = name
      customOption.value = String(productId || 0)
      customOption.dataset.price = price || 0
      customOption.dataset.productId = String(productId || 0)
      customOption.selected = true
      select.appendChild(customOption)
    }

  }

  updateTotal()
  checkForm()
}



  addProductBtn.onclick = ()=>{

  const rows = document.querySelectorAll(".order__productRow")
  const lastRow = rows[rows.length - 1]

  if (lastRow) {
    const select = lastRow.querySelector("select")
    if (!select.value) {
      showAppMessage("Chọn sản phẩm trước đã 😅", true)
      return
    }
  }
    addProduct()

  }



  productBox.addEventListener("click",(e)=>{

    if(e.target.classList.contains("order__remove")){

    e.target.parentElement.remove()

    updateTotal()

    }

  })


productBox.addEventListener("change",updateTotal)
productBox.addEventListener("input",updateTotal)



function updateTotal(){

  let total = 0

  document.querySelectorAll(".order__productRow")
  .forEach(row=>{

    const select = row.querySelector("select")
    const qty = row.querySelector("input").value

    const price = select.options[select.selectedIndex]?.dataset.price

    if(price){

      total += price * qty

    }

  })

    totalEl.innerText = total.toLocaleString()
    document.getElementById("orderTotalInput").value = total
    checkForm()

}



if (paymentMethodInputs.length) {
  paymentMethodInputs.forEach((input) => {
    input.addEventListener("change", () => {
      const method = getSelectedPaymentMethod()
      qrBox.style.display = method === "COD" ? "none" : "block"
    })
  })
}

// **********Validate*********************

function validatePhone(phone){

  const regex = /^(0[3|5|7|8|9])[0-9]{8}$/

  return regex.test(phone)

}


const submitBtn = document.getElementById("orderSubmit")
const customerPhone = document.getElementById("customerPhone")
const inputs = document.querySelectorAll("#customerName,#customerEmail,#customerPhone,#customerAddress")

function checkForm(){

  let valid = true

  inputs.forEach(input=>{

    if(input.value.trim()===""){
    valid=false
  }

  })

  if(document.querySelectorAll(".order__productRow").length === 0){
    valid = false
  }

  if(!validatePhone(customerPhone.value)){
    valid=false
  }

  if(invoiceToggle?.checked){
    invoiceInputs.forEach(input=>{
      if(input.value.trim()===""){
        valid=false
      }
    })
  }

  submitBtn.disabled = !valid

}

inputs.forEach(input=>{

  input.addEventListener("input",checkForm)

})

invoiceInputs.forEach(input=>{
  input.addEventListener("input",checkForm)
})

// ***********Notification***********************

const successPopup = document.getElementById("successPopup")
const successOk = document.getElementById("successOk")

successOk.onclick = ()=>{
  successPopup.classList.remove("active")
}

// ******************Form_Submit*****************

const API_BASE_URL = "http://localhost:8080/api"
const form = document.getElementById("orderForm")

form.addEventListener("submit", function(e){
  const rows = Array.from(document.querySelectorAll(".order__productRow"))
  const hiddenBox = document.getElementById("orderItemsHidden")

  if (!rows.length) {
    e.preventDefault()
    showAppMessage("Vui long them it nhat 1 san pham", true)
    return
  }

  hiddenBox.innerHTML = ""

  let hasInvalid = false
  rows.forEach((row, index) => {
    const select = row.querySelector("select")
    const selectedOption = select.options[select.selectedIndex]
    const productId = Number(selectedOption?.dataset.productId || selectedOption?.value || 0)
    const qty = Number(row.querySelector(".order__qty").value || 1)

    if (!productId || qty < 1) {
      hasInvalid = true
      return
    }

    hiddenBox.insertAdjacentHTML("beforeend",
      `<input type="hidden" name="Items[${index}].ProductId" value="${productId}">` +
      `<input type="hidden" name="Items[${index}].Quantity" value="${qty}">`)
  })

  if (hasInvalid) {
    e.preventDefault()
    showAppMessage("Mot so san pham khong hop le. Vui long kiem tra lai.", true)
    return
  }

  if (isExportInvoiceInput) {
    isExportInvoiceInput.value = invoiceToggle?.checked ? "true" : "false"
  }

  const pendingProductIds = rows.map((row) => {
    const select = row.querySelector("select")
    const selectedOption = select.options[select.selectedIndex]
    return Number(selectedOption?.dataset.productId || selectedOption?.value || 0)
  }).filter((x) => x > 0)
  localStorage.setItem("hx_pending_checkout_product_ids", JSON.stringify([...new Set(pendingProductIds)]))

  qrBox.style.display = getSelectedPaymentMethod() === "COD" ? "none" : "block"

  submitBtn.classList.add("loading")
  submitBtn.textContent = "Đang chuyển hướng..."
})
// ******************Menu mobile**************************

const menu = document.querySelector(".menu_nav")
const openBtn = document.getElementById("menuToggle")
const closeBtnMobile = document.getElementById("btn_close")

if (openBtn) {
  openBtn.onclick = function(){
    menu.classList.add("active")
  }
}

if (closeBtnMobile) {
  closeBtnMobile.onclick = function(){
    menu.classList.remove("active")
  }
}

document.addEventListener("click", (e) => {
    if(!menu){
      return
    }
    const clickedMenu = menu.contains(e.target)
    const clickedOpen = openBtn ? openBtn.contains(e.target) : false
    if(!clickedMenu && !clickedOpen){
        menu.classList.remove("active")
    }

})

// ************* CLICK SUBMENU (TÃCH Há»¢P Vá»šI LAYOUT GRID Má»šI) *************
const menuItemss = document.querySelectorAll(".menu_product--item");

menuItemss.forEach(menu => {
    menu.addEventListener("click", () => {
        const filter = menu.getAttribute("data-filter");

        // TÃ¬m cÃ¡i nÃºt Danh má»¥c chÃ­nh (Pill) tÆ°Æ¡ng á»©ng vÃ  "chá»t" (báº¥m giáº£) vÃ o nÃ³
        const targetNavBtn = document.querySelector(`.product_nav--item[data-filter="${filter}"]`);
        
        if(targetNavBtn) {
            targetNavBtn.click(); // Chuyá»ƒn lá»‡nh nÃ y cho file ggsheet.js tá»± xá»­ lÃ½
        }
    });
});



// ************* CLICK TAB (PRODUCT NAV) *************
const navBtns = document.querySelectorAll(".product_nav--item");

navBtns.forEach(btn => {
    btn.addEventListener("click", () => {

        const filter = btn.getAttribute("data-filter");

        // active tab
        navBtns.forEach(b => b.classList.remove("active"));
        btn.classList.add("active");

        filterProducts(filter);
    });
});


// ************* CLICK SUBMENU *************
const menuItems = document.querySelectorAll(".menu_product--item");

menuItems.forEach(menu => {
    menu.addEventListener("click", () => {

        const filter = menu.getAttribute("data-filter");

        // active tab bÃªn dÆ°á»›i
        navBtns.forEach(btn => {
            btn.classList.toggle(
                "active",
                btn.getAttribute("data-filter") === filter
            );
        });

        filterProducts(filter);
    });
});

// ************************Cart*************************************

const cart = document.querySelector(".cart")
const openBtn_cart = document.querySelectorAll(".icon_cartWrap")
const closeBtnCart = document.getElementById("btn_closeCart")
const cartWrap = document.querySelector(".main_cart--wrap")
const totalCartEl = document.getElementById("total_cart")
const checkAllEl = document.querySelector(".end_cart--checkAll input")
const btnOrderCart = document.querySelector(".btn_buyCart")
const cartCountEl = document.querySelectorAll(".cart_count")
let carts = []

function saveCart() {
  localStorage.setItem("cart", JSON.stringify(carts))
}

function loadCart() {
  const data = localStorage.getItem("cart")
  carts = data ? JSON.parse(data) : []
  normalizeCartItems()
}

function formatPriceCart(price) {
  return Number(price || 0).toLocaleString("vi-VN") + "đ"
}

function updateCartCount() {
  const totalQty = carts.reduce((sum, item) => sum + Number(item.qty || 0), 0)
  cartCountEl.forEach((el) => {
    el.textContent = totalQty
  })
}

function updateCartTotal() {
  let total = 0
  document.querySelectorAll(".main_cart--item").forEach((itemEl) => {
    const checked = itemEl.querySelector(".cart_check")?.checked
    if (checked) {
      const index = Number(itemEl.dataset.index)
      const item = carts[index]
      if (item) {
        total += Number(item.price) * Number(item.qty)
      }
    }
  })

  if (totalCartEl) {
    totalCartEl.innerText = formatPriceCart(total)
  }
}

function renderCart() {
  const emptyCartMsg = document.querySelector(".main_cart--empty")
  const cartDetailHeader = document.querySelector(".main_cart--detail")

  if (!cartWrap || !emptyCartMsg || !cartDetailHeader) return

  if (!carts.length) {
    cartWrap.innerHTML = ""
    emptyCartMsg.style.display = "flex"
    cartDetailHeader.style.display = "none"
    if (totalCartEl) totalCartEl.innerText = "0đ"
    if (checkAllEl) checkAllEl.checked = false
    updateCartCount()
    return
  }

  emptyCartMsg.style.display = "none"
  cartDetailHeader.style.display = "flex"

  cartWrap.innerHTML = carts.map((item, index) => `
    <div class="main_cart--item" data-index="${index}">
      <div class="main_cart--itemBtnRemove">×</div>
      <input type="checkbox" class="cart_check" ${item.checked ? "checked" : ""}>
      <img src="${item.image || ''}" class="main_cart--itemImg" alt="${item.name || ''}">
      <div class="main_cart--itemContent">
        <h5 class="main_cart--itemName">${item.name || ''}</h5>
        <div class="main_cart--itemQty">
          <div>Số lượng:</div>
          <div class="qty-wrapper">
            <button type="button" class="qty-btn qty-minus">-</button>
            <input type="number" class="cart_qty" value="${Number(item.qty || 1)}" min="1">
            <button type="button" class="qty-btn qty-plus">+</button>
          </div>
        </div>
        <div class="main_cart--itemPrice">
          <div>Giá:</div>
          <div>${formatPriceCart(item.price)}</div>
        </div>
      </div>
      <div class="main_cart--itemTotal">${formatPriceCart(Number(item.price) * Number(item.qty || 1))}</div>
    </div>
  `).join("")

  bindCartEvents()
  updateCartTotal()
  updateCartCount()
  if (checkAllEl) {
    checkAllEl.checked = carts.length > 0 && carts.every((x) => x.checked)
  }
}

function bindCartEvents() {
  document.querySelectorAll(".main_cart--itemBtnRemove").forEach((btn) => {
    btn.onclick = (e) => {
      const itemEl = e.target.closest(".main_cart--item")
      const index = Number(itemEl?.dataset.index || -1)
      if (index < 0) return
      carts.splice(index, 1)
      saveCart()
      renderCart()
    }
  })

  document.querySelectorAll(".cart_qty").forEach((input) => {
    input.oninput = (e) => {
      const itemEl = e.target.closest(".main_cart--item")
      const index = Number(itemEl?.dataset.index || -1)
      if (index < 0 || !carts[index]) return

      let qty = Number(e.target.value || 1)
      if (qty < 1) qty = 1
      e.target.value = qty
      carts[index].qty = qty
      saveCart()

      const totalEl = itemEl.querySelector(".main_cart--itemTotal")
      if (totalEl) totalEl.textContent = formatPriceCart(Number(carts[index].price) * qty)
      updateCartCount()
      updateCartTotal()
    }
  })

  document.querySelectorAll(".qty-minus").forEach((btn) => {
    btn.onclick = (e) => {
      const input = e.target.parentElement?.querySelector(".cart_qty")
      if (!input) return
      input.value = Math.max(1, Number(input.value || 1) - 1)
      input.dispatchEvent(new Event("input", { bubbles: true }))
    }
  })

  document.querySelectorAll(".qty-plus").forEach((btn) => {
    btn.onclick = (e) => {
      const input = e.target.parentElement?.querySelector(".cart_qty")
      if (!input) return
      input.value = Number(input.value || 1) + 1
      input.dispatchEvent(new Event("input", { bubbles: true }))
    }
  })

  document.querySelectorAll(".cart_check").forEach((cb) => {
    cb.onchange = (e) => {
      const itemEl = e.target.closest(".main_cart--item")
      const index = Number(itemEl?.dataset.index || -1)
      if (index >= 0 && carts[index]) {
        carts[index].checked = !!cb.checked
        saveCart()
      }
      updateCartTotal()
      const totalChecks = document.querySelectorAll(".cart_check").length
      const checkedCount = document.querySelectorAll(".cart_check:checked").length
      if (checkAllEl) {
        checkAllEl.checked = totalChecks > 0 && totalChecks === checkedCount
      }
    }
  })
}

function addItemToCart(product) {
  if (!product?.id || !product?.name) return
  const exists = carts.find((x) => Number(x.id) === Number(product.id))
  if (exists) {
    exists.qty += 1
    exists.checked = true
  } else {
    carts.push({
      id: Number(product.id),
      name: product.name,
      price: Number(product.price || 0),
      image: product.image || "",
      qty: 1,
      checked: true
    })
  }
  saveCart()
  renderCart()
}

document.querySelectorAll(".product_hoverBtn").forEach((btn) => {
  btn.addEventListener("click", (e) => {
    e.stopPropagation()
    const card = btn.closest(".product_listItem")
    if (!card) return

    addItemToCart({
      id: Number(btn.dataset.id || card.dataset.productId || 0),
      name: btn.dataset.name || card.dataset.productName || "",
      price: Number(btn.dataset.price || card.dataset.productPrice || 0),
      image: card.dataset.productImage || ""
    })
    const imgEl = card.querySelector(".product_listItem--img")
    if (imgEl) flyToCart(imgEl)
  })
})

if (checkAllEl) {
  checkAllEl.onchange = () => {
    const checked = checkAllEl.checked
    document.querySelectorAll(".cart_check").forEach((cb, index) => {
      cb.checked = checked
      if (carts[index]) {
        carts[index].checked = checked
      }
    })
    saveCart()
    updateCartTotal()
  }
}

if (btnOrderCart) {
  btnOrderCart.onclick = () => {
    const selectedItems = carts.filter((_, index) => {
      const el = document.querySelector(`.main_cart--item[data-index="${index}"]`)
      return el && el.querySelector(".cart_check")?.checked
    })

    if (!selectedItems.length) {
      showAppMessage("Chon it nhat 1 san pham", true)
      return
    }

    productBox.innerHTML = ""
    openOrder()
    selectedItems.forEach((item) => {
      addProduct(item.name, item.price, item.qty, item.id)
    })
    updateTotal()
    cart.classList.remove("active_cart")
    syncGlobalScrollLock()
  }
}

loadCart()
renderCart()
checkForm()

if (window.__checkoutMessage) {
  const isError = window.__checkoutStatus !== "success"
  showAppMessage(window.__checkoutMessage, isError)

  const pendingRaw = localStorage.getItem("hx_pending_checkout_product_ids")
  if (window.__checkoutStatus === "success" && pendingRaw) {
    const purchasedIds = JSON.parse(pendingRaw)
    if (Array.isArray(purchasedIds) && purchasedIds.length) {
      carts = carts.filter((x) => !purchasedIds.includes(Number(x.id)))
      saveCart()
      renderCart()
    }
  }

  localStorage.removeItem("hx_pending_checkout_product_ids")
}

openBtn_cart.forEach(btn => {
  btn.addEventListener("click", (e) => {
    e.stopPropagation();
    cart.classList.add("active_cart")
    menu.classList.remove("active")
    syncGlobalScrollLock()
  })

})
if (closeBtnCart) {
  closeBtnCart.onclick = function(){
    cart.classList.remove("active_cart")
    syncGlobalScrollLock()
  }
}

document.addEventListener("click", (e) => {
    if(!cart){
        return
    }
    if(!cart.contains(e.target)){
        cart.classList.remove("active_cart")
        syncGlobalScrollLock()
    }

})

window.addProduct = addProduct;
window.updateTotal = updateTotal;
window.openOrder = openOrder;
window.productBox = productBox;




// End Cart

// **************logo_xoay******************
const orbits = document.querySelectorAll('.orbit');
const totals = orbits.length;

const radius = 190;
const duration = 20;

orbits.forEach((item, index) => {
    const angle = (360 / totals) * index;

    item.style.setProperty('--angle', angle + 'deg');
    item.style.setProperty('--radius', radius + 'px');

    item.style.animation = `spinOrbit ${duration}s linear infinite`;
});

// **********lang_mobile********************
// ================= Xá»¬ LÃ NÃšT NGÃ”N NGá»® BUNG RA (MOBILE) =================
// ================= Xá»¬ LÃ NÃšT NGÃ”N NGá»® BUNG RA (MOBILE) =================
const langFlagsWrap = document.querySelector(".lang-flags_mobile");

if (langFlagsWrap) {
    langFlagsWrap.addEventListener("click", function (e) {
        if (window.innerWidth <= 768) {
            e.stopPropagation(); // PhÃ©p thuáº­t náº±m á»Ÿ Ä‘Ã¢y: Cháº·n khÃ´ng cho lá»‡nh click bay ra ngoÃ i
            this.classList.toggle("active");
        }
    });

    // Khi click ra vÃ¹ng trá»‘ng ngoÃ i web thÃ¬ tá»± Ä‘á»™ng thu cá»¥m cá» láº¡i
    document.addEventListener("click", function (e) {
        if (window.innerWidth <= 768 && langFlagsWrap.classList.contains("active")) {
            // Náº¿u click á»Ÿ ngoÃ i cá»¥m cá» thÃ¬ má»›i Ä‘Ã³ng
            if (!langFlagsWrap.contains(e.target)) {
                langFlagsWrap.classList.remove("active");
            }
        }
    });
}

// **********footer policy popup********************
const policyPopup = document.getElementById("policyPopup")
const policyPopupOverlay = document.querySelector(".policy_popup--overlay")
const policyPopupClose = document.querySelector(".policy_popup--close")
const policyPopupTitle = document.querySelector(".policy_popup--title")
const policyPopupBody = document.querySelector(".policy_popup--body")
const policyPopupSource = document.querySelector(".policy_popup--source")
const policyLinkButtons = document.querySelectorAll(".footer_policy--link")

const policyData = window.__policyData || {}

function closePolicyPopup() {
  if (!policyPopup) return
  policyPopup.classList.remove("active")
  syncGlobalScrollLock()
}

function openPolicyPopup(policyKey) {
  const policy = policyData[policyKey]
  if (!policy || !policyPopup || !policyPopupTitle || !policyPopupBody) return
  policyPopupTitle.textContent = policy.title || policy.Title || ""
  policyPopupBody.textContent = normalizePolicyText(policy.content || policy.Content || "")
  if (policyPopupSource) {
    policyPopupSource.href = policy.source || policy.Source || "#"
  }
  policyPopup.classList.add("active")
  syncGlobalScrollLock()
}

policyLinkButtons.forEach((btn) => {
  btn.addEventListener("click", () => {
    openPolicyPopup(btn.dataset.policyKey || "")
  })
})

if (policyPopupOverlay) {
  policyPopupOverlay.addEventListener("click", closePolicyPopup)
}
if (policyPopupClose) {
  policyPopupClose.addEventListener("click", closePolicyPopup)
}

// **********mobile footer accordion********************
const footerAccButtons = document.querySelectorAll(".footer_accBtn")
footerAccButtons.forEach((btn) => {
  btn.addEventListener("click", () => {
    const item = btn.closest(".footer_accItem")
    if (!item) return
    item.classList.toggle("active")
  })
})

window.addEventListener("resize", syncGlobalScrollLock)

});













