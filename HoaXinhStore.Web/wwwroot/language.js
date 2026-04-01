
// Helper: đọc target lang từ cookie googtrans (vd: "/vi/en")
function getCurrentGTLang() {
    const m = document.cookie.match(/(?:^|;)\s*googtrans=([^;]+)/);
    if (!m) return null;
    const parts = decodeURIComponent(m[1]).split('/');
    return parts.pop() || null;
}

// Đổi ngôn ngữ bằng cách set value cho select và trigger change
function setLanguage(lang) {
    const sel = document.querySelector('#google_translate_element select.goog-te-combo');
    if (!sel) { setTimeout(() => setLanguage(lang), 50); return; } // chờ widget render
    sel.value = lang;
    sel.dispatchEvent(new Event('change'));
    // Cập nhật active UI sau một nhịp (cookie được set không đồng bộ)
    setTimeout(updateActiveFlag, 300);
}

// Gán trạng thái active cho lá cờ đang chọn
function updateActiveFlag() {
    const current = getCurrentGTLang() || 'vi';
    document.querySelectorAll('.lang-flag').forEach(btn => {
        btn.classList.toggle('is-active', btn.dataset.lang === current);
    });
}

// Gắn sự kiện click cho các lá cờ
// Gắn sự kiện click TRỰC TIẾP cho từng lá cờ
document.querySelectorAll('.lang-flag').forEach(btn => {
    btn.addEventListener('click', (e) => {
        e.preventDefault();
        
        // Gọi hàm đổi ngôn ngữ
        setLanguage(btn.dataset.lang);

        // (Tùy chọn) Bấm chọn ngôn ngữ xong thì tự động thu cụm cờ lại cho gọn
        const langWrap = btn.closest('.lang-flags_mobile');
        if (langWrap) {
            langWrap.classList.remove('active');
        }
    });
});

// Đồng bộ UI khi tải trang xong
window.addEventListener('load', () => {
    // Lần đầu: nếu chưa có cookie, đánh dấu VI
    updateActiveFlag();
    // Khi Google đổi ngôn ngữ sẽ reload/hay DOM thay đổi — cập nhật lại
    const observer = new MutationObserver(updateActiveFlag);
    observer.observe(document.documentElement, { subtree: true, childList: true });
});