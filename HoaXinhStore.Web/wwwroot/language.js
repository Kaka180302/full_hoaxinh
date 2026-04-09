
(function () {
    const BASE_LANG = 'vi';
    const COOKIE_PATH = 'path=/';
    const COOKIE_MAX_AGE = 'max-age=31536000';

    function readCurrentLang() {
        const m = document.cookie.match(/(?:^|;)\s*googtrans=([^;]+)/);
        if (!m) return null;
        const val = decodeURIComponent(m[1] || '');
        const parts = val.split('/');
        return parts[parts.length - 1] || null;
    }

    function writeGoogTransCookie(lang) {
        const value = `/${BASE_LANG}/${lang}`;
        const encoded = encodeURIComponent(value);
        document.cookie = `googtrans=${encoded}; ${COOKIE_PATH}; ${COOKIE_MAX_AGE}`;
        document.cookie = `googtrans=${encoded}; ${COOKIE_PATH}; domain=${location.hostname}; ${COOKIE_MAX_AGE}`;
    }

    function updateActiveFlag() {
        const current = readCurrentLang() || BASE_LANG;
        document.querySelectorAll('.lang-flag').forEach((btn) => {
            btn.classList.toggle('is-active', btn.dataset.lang === current);
        });
    }

    function triggerComboChange(lang, retry) {
        const select = document.querySelector('#google_translate_element select.goog-te-combo');
        if (!select) {
            if (retry > 0) {
                setTimeout(() => triggerComboChange(lang, retry - 1), 120);
            }
            return;
        }

        select.value = lang;
        select.dispatchEvent(new Event('change', { bubbles: true }));

        if (document.createEvent) {
            const event = document.createEvent('HTMLEvents');
            event.initEvent('change', true, true);
            select.dispatchEvent(event);
        }
    }

    function setLanguage(lang) {
        if (!lang) return;
        writeGoogTransCookie(lang);
        updateActiveFlag();
        triggerComboChange(lang, 40);
    }

    document.addEventListener('click', (e) => {
        const btn = e.target.closest('.lang-flag');
        if (!btn) return;
        e.preventDefault();
        setLanguage(btn.dataset.lang);
    });

    window.setLanguage = setLanguage;

    window.addEventListener('load', () => {
        updateActiveFlag();
        const observer = new MutationObserver(updateActiveFlag);
        observer.observe(document.documentElement, { subtree: true, childList: true });
    });
})();
