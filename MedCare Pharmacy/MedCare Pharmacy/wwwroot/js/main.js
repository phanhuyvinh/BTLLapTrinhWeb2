/* ============================================================
   main.js — Tiện ích dùng chung toàn hệ thống
   Bao gồm: format, toast, escape HTML, query params, storage,
   gợi ý tìm kiếm sản phẩm.
   ============================================================ */

/* --------- Format --------- */
function formatPrice(v) {
    return Number(v || 0).toLocaleString("vi-VN") + "đ";
}
function formatDate(iso) {
    if (!iso) return "";
    const d = new Date(iso);
    if (isNaN(d)) return iso;
    return d.toLocaleDateString("vi-VN");
}
function formatDateTime(iso) {
    if (!iso) return "";
    const d = new Date(iso);
    if (isNaN(d)) return iso;
    return d.toLocaleString("vi-VN");
}

/* --------- Escape HTML (chống XSS khi render từ dữ liệu) --------- */
function escapeHtml(s) {
    return String(s ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#39;");
}

/* --------- Toast --------- */
function ensureToastEl() {
    let el = document.getElementById("toast");
    if (!el) {
        el = document.createElement("div");
        el.id = "toast";
        el.className = "toast";
        document.body.appendChild(el);
    }
    return el;
}
function showToast(message, type = "info") {
    const el = ensureToastEl();
    el.textContent = message;
    el.style.background =
        type === "error" ? "#b91c1c" :
            type === "warn" ? "#b45309" :
                "#16352d";
    el.classList.add("show");
    clearTimeout(el._t);
    el._t = setTimeout(() => el.classList.remove("show"), 2400);
}

/* --------- Query string --------- */
function getQuery(name) {
    return new URLSearchParams(location.search).get(name) || "";
}

/* --------- localStorage helpers --------- */
const LS = {
    get(key, fallback = null) {
        try { return JSON.parse(localStorage.getItem(key)) ?? fallback; }
        catch { return fallback; }
    },
    set(key, value) { localStorage.setItem(key, JSON.stringify(value)); },
    remove(key) { localStorage.removeItem(key); }
};

/* --------- Danh mục dùng chung (khớp Category.Name trong DB) --------- */
const CATEGORIES = [
    { id: "Sơ cứu", icon: "🩹" },
    { id: "Dị ứng", icon: "🤧" },
    { id: "Chăm sóc da", icon: "🧴" },
    { id: "Chăm sóc sức khỏe", icon: "💪" },
    { id: "Vitamin và khoáng chất", icon: "🍊" },
    { id: "Tiêu hóa", icon: "🫁" },
    { id: "Thuốc giảm đau", icon: "💊" },
    { id: "Tai mũi họng", icon: "👂" }
];

/* --------- Sản phẩm nổi bật trang chủ (lấy từ API thật, không dùng demo) --------- */
async function renderHomeProducts() {
    const listEl = document.getElementById("productList");
    if (!listEl) return;

    listEl.innerHTML = `<p class="empty">Đang tải sản phẩm nổi bật...</p>`;

    try {
        const endpoint = (typeof API !== "undefined" && API.PRODUCTS) ? API.PRODUCTS : "/products";
        let products = await apiGet(endpoint);
        if (!Array.isArray(products)) products = [];

        products = products
            .filter(p => {
                const img = p.imageUrl || p.ImageUrl || "";
                const stock = p.stockQuantity ?? p.StockQuantity ?? 0;
                return img && stock > 0;
            })
            .sort((a, b) => (a.id ?? a.Id) - (b.id ?? b.Id))
            .slice(0, 8);

        if (!products.length) {
            listEl.innerHTML = `<p class="empty">Chưa có sản phẩm nổi bật phù hợp.</p>`;
            return;
        }

        const sortEl = document.getElementById("sortSelect");
        if (sortEl) {
            if (sortEl.value === "low") products.sort((a, b) => (a.price ?? a.Price) - (b.price ?? b.Price));
            if (sortEl.value === "high") products.sort((a, b) => (b.price ?? b.Price) - (a.price ?? a.Price));
        }

        listEl.innerHTML = products.map(p => {
            const id = p.id ?? p.Id;
            const name = p.name ?? p.Name ?? "";
            const price = p.price ?? p.Price ?? 0;
            const stock = p.stockQuantity ?? p.StockQuantity ?? 0;
            const unit = p.unit ?? p.Unit ?? "Hộp";
            const catName = (p.category && (p.category.name || p.category.Name)) || "—";
            const img = p.imageUrl || p.ImageUrl || "";
            const imgHtml = img
                ? `<img src="${escapeHtml(img)}" alt="${escapeHtml(name)}" style="max-width:100%;max-height:120px;object-fit:contain" onerror="this.style.display='none';this.nextElementSibling.style.display='flex'"><span style="display:none;font-size:2rem;align-items:center;justify-content:center;height:100%">💊</span>`
                : `<span style="font-size:2rem">💊</span>`;

            return `
            <article class="product" data-product-id="${id}" style="cursor:pointer">
                <div class="product-image">${imgHtml}</div>
                <div class="product-body">
                    <span class="product-category">${escapeHtml(catName)}</span>
                    <h3>${escapeHtml(name)}</h3>
                    <div class="price">${formatPrice(price)}</div>
                    <div class="text-muted" style="font-size:12px;margin-bottom:8px">Tồn: ${stock} ${escapeHtml(unit)}</div>
                    <button class="add-cart" onclick="event.stopPropagation(); typeof addToCartFromApi==='function' && addToCartFromApi(${id}, '${escapeHtml(name).replace(/'/g, "\\'")}', ${price}, '${escapeHtml(unit)}', '${escapeHtml(img)}', ${stock})">
                        Thêm vào giỏ
                    </button>
                </div>
            </article>`;
        }).join("");

        listEl.querySelectorAll("article.product").forEach(el => {
            el.addEventListener("click", (e) => {
                if (e.target.closest("button, a")) return;
                const pid = el.getAttribute("data-product-id");
                if (pid && typeof openProductPopup === "function") openProductPopup(parseInt(pid, 10));
            });
        });
    } catch (err) {
        listEl.innerHTML = `<p class="empty">Không tải được sản phẩm: ${escapeHtml(err.message || "Lỗi")}</p>`;
    }
}

/* ---------- Modal dùng chung ---------- */
function openModal(html, actions = []) {
    let bd = document.getElementById("globalModal");
    if (!bd) {
        bd = document.createElement("div");
        bd.id = "globalModal";
        bd.className = "modal-backdrop";
        bd.innerHTML = `<div class="modal" id="globalModalBody"></div>`;
        bd.addEventListener("click", e => { if (e.target === bd) closeModal(); });
        document.body.appendChild(bd);
    }
    const body = document.getElementById("globalModalBody");
    body.innerHTML = html + `
        <div class="modal-actions">
            ${actions.map(a => `<button class="btn ${a.cls || ''}" onclick="${a.onclick || ''}">${a.label}</button>`).join("")}
            <button class="btn muted" onclick="closeModal()">Đóng</button>
        </div>
    `;
    bd.classList.add("show");
}
function closeModal() {
    const bd = document.getElementById("globalModal");
    if (bd) bd.classList.remove("show");
}

/* ============================================================
   G. Gợi ý tìm kiếm thuốc theo từ khóa đang nhập
   ============================================================ */
(function initSearchSuggest() {
    function setup() {
        const input = document.getElementById("searchInput");
        const box = document.getElementById("searchSuggest");
        if (!input || !box) return;

        let timer = null;
        let lastReq = 0;

        function hide() {
            box.classList.remove("show");
            box.innerHTML = "";
        }

        function showLoading() {
            box.innerHTML = `<div class="search-suggest-item muted">Đang tìm...</div>`;
            box.classList.add("show");
        }

        async function search(q) {
            const reqId = ++lastReq;
            showLoading();
            try {
                const endpoint = (typeof API !== "undefined" && API.PRODUCTS) ? API.PRODUCTS : "/products";
                const products = await apiGet(endpoint + "?q=" + encodeURIComponent(q));
                if (reqId !== lastReq) return;
                if (!products || !products.length) {
                    box.innerHTML = `<div class="search-suggest-item muted">Không có kết quả</div>`;
                    return;
                }
                box.innerHTML = products.slice(0, 8).map(p => {
                    const id = p.id ?? p.Id;
                    const name = p.name ?? p.Name ?? "";
                    return `<div class="search-suggest-item" role="option" data-id="${id}" data-name="${escapeHtml(name)}">${escapeHtml(name)}</div>`;
                }).join("");
                box.querySelectorAll(".search-suggest-item[data-id]").forEach(el => {
                    el.addEventListener("click", () => {
                        location.href = "/Customer/ProductDetail?id=" + el.getAttribute("data-id");
                    });
                });
            } catch {
                if (reqId === lastReq) box.innerHTML = `<div class="search-suggest-item muted">Lỗi tìm kiếm</div>`;
            }
        }

        input.addEventListener("input", () => {
            clearTimeout(timer);
            const q = input.value.trim();
            if (q.length < 2) { hide(); return; }
            timer = setTimeout(() => search(q), 280);
        });
        input.addEventListener("blur", () => setTimeout(hide, 200));
        input.addEventListener("focus", () => {
            if (input.value.trim().length >= 2) box.classList.add("show");
        });
    }
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", setup);
    else setup();
})();
