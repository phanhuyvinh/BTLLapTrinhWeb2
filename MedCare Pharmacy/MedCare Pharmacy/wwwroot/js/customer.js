/* ============================================================
   customer.js — Logic các trang customer/*.html
   ============================================================ */

/* ---------- products.html: danh sách + lọc + tìm kiếm ---------- */
function initProductsPage() {
    const listEl = document.getElementById("productList");
    if (!listEl) return;

    const q = getQuery("q");
    const catParam = getQuery("cat");

    const state = {
        keyword: q,
        category: catParam,
        sort: "default"
    };

    const $kw = document.getElementById("filterKeyword");
    if ($kw) $kw.value = q;

    // đánh dấu category đang chọn (nếu có checkbox filter)
    document.querySelectorAll("input[name='cat']").forEach(el => {
        if (el.value === catParam) el.checked = true;
    });
    document.querySelectorAll("input[name='cat']").forEach(el => {
        el.addEventListener("change", () => {
            state.category = el.checked ? el.value : "";
            loadAndRender();
        });
    });
    if ($kw) {
        $kw.addEventListener("input", () => {
            state.keyword = $kw.value.trim();
            loadAndRender();
        });
    }
    const sortEl = document.getElementById("sortSelect");
    if (sortEl) {
        sortEl.addEventListener("change", () => {
            state.sort = sortEl.value;
            loadAndRender();
        });
    }

    async function loadAndRender() {
        listEl.innerHTML = `<p class="empty">Đang tải sản phẩm...</p>`;

        try {
            // Gọi API ProductsController.GetAll
            // Endpoint: GET /api/products?q=...&cat=...&sort=...
            let endpoint = API.PRODUCTS + "?";
            if (state.keyword) endpoint += `q=${encodeURIComponent(state.keyword)}&`;
            if (state.category) endpoint += `cat=${encodeURIComponent(state.category)}&`;
            if (state.sort && state.sort !== "default") endpoint += `sort=${encodeURIComponent(state.sort)}&`;

            const products = await apiGet(endpoint);

            if (!products || !products.length) {
                listEl.innerHTML = `<p class="empty">Không tìm thấy sản phẩm phù hợp.</p>`;
                return;
            }

            // Ánh xạ field từ API (Product entity) sang format giao diện
            listEl.innerHTML = products.map(p => {
                const categoryName = (p.category && p.category.name) ? p.category.name : (p.category || "—");
                const active = p.activeIngredient || "—";
                const stock = p.stockQuantity != null ? p.stockQuantity : 0;
                const icon = p.imageUrl || "💊";
                const unit = p.unit || "Hộp";

                return `
                <article class="product">
                    <div class="product-image">${icon}</div>
                    <div class="product-body">
                        <span class="product-category">${escapeHtml(categoryName)}</span>
                        <h3>${escapeHtml(p.name)}</h3>
                        <div class="rating">HC: ${escapeHtml(active)}</div>
                        <div class="price">${formatPrice(p.price)}</div>
                        <div class="text-muted" style="font-size:12px;margin-bottom:8px">
                            Tồn kho: ${stock > 0 ? stock + " " + unit : "Hết hàng"}
                        </div>
                        <div style="display:flex;gap:6px">
                            <a class="btn ghost small" style="flex:1;text-align:center;text-decoration:none"
                               href="/Customer/ProductDetail?id=${p.id}">Xem</a>
                            <button class="add-cart" style="flex:1"
                                    onclick="addToCartFromApi(${p.id}, '${escapeHtml(p.name).replace(/'/g, "\\'")}', ${p.price}, '${escapeHtml(unit)}', '${icon}', ${stock})"
                                    ${stock === 0 ? "disabled" : ""}>
                                ${stock === 0 ? "Hết hàng" : "Thêm giỏ"}
                            </button>
                        </div>
                    </div>
                </article>`;
            }).join("");
        } catch (err) {
            listEl.innerHTML = `<p class="empty">Lỗi tải sản phẩm: ${escapeHtml(err.message || "Không xác định")}</p>`;
            showToast(err.message || "Không tải được danh sách sản phẩm", "error");
        }
    }

    loadAndRender();
}

/* Hàm phụ: thêm vào giỏ từ dữ liệu API (tạm thời vẫn dùng localStorage của cart.js) */
function addToCartFromApi(id, name, price, unit, icon, stock) {
    if (stock <= 0) {
        showToast("Sản phẩm hết hàng", "warn");
        return;
    }
    const cart = getCart();
    const item = cart.find(x => x.id === id);
    if (item) item.quantity += 1;
    else cart.push({ id, name, price, icon, unit, quantity: 1 });
    saveCart(cart);
    showToast(`Đã thêm "${name}" vào giỏ hàng`);
}
/* ---------- product-detail.html ---------- */
async function initProductDetailPage() {
    const box = document.getElementById("pdBox");
    if (!box) return;

    const id = Number(getQuery("id") || 0);
    if (!id) {
        showToast("Không tìm thấy sản phẩm", "error");
        return;
    }

    try {
        // Gọi API ProductsController.GetById
        const p = await apiGet(`${API.PRODUCTS}/${id}`);

        const categoryName = (p.category && p.category.name) ? p.category.name : "—";
        const active = p.activeIngredient || "—";
        const stock = p.stockQuantity != null ? p.stockQuantity : 0;
        const icon = p.imageUrl || "💊";
        const unit = p.unit || "Hộp";
        const supplierName = (p.supplier && p.supplier.name) ? p.supplier.name : "—";
        const desc = p.description || "Đang cập nhật mô tả.";

        document.getElementById("pdIcon").textContent = icon;
        document.getElementById("pdName").textContent = p.name;
        document.getElementById("pdCat").textContent = categoryName;
        document.getElementById("pdActive").textContent = active;
        document.getElementById("pdPrice").textContent = formatPrice(p.price);
        document.getElementById("pdStock").textContent = stock > 0 ? `${stock} ${unit}` : "Hết hàng";
        document.getElementById("pdDesc").textContent = desc;
        document.getElementById("pdUnit").textContent = unit;
        document.getElementById("pdSupplier").textContent = supplierName;

        const qtyInput = document.getElementById("pdQty");
        const btnMinus = document.getElementById("pdMinus");
        const btnPlus = document.getElementById("pdPlus");
        const btnAdd = document.getElementById("pdAdd");
        const btnBuy = document.getElementById("pdBuy");

        if (btnMinus) btnMinus.onclick = () => { qtyInput.value = Math.max(1, +qtyInput.value - 1); };
        if (btnPlus) btnPlus.onclick = () => { qtyInput.value = +qtyInput.value + 1; };

        if (btnAdd) {
            btnAdd.onclick = () => {
                addToCartFromApi(p.id, p.name, p.price, unit, icon, stock);
            };
            if (stock === 0) btnAdd.disabled = true;
        }
        if (btnBuy) {
            btnBuy.onclick = () => {
                addToCartFromApi(p.id, p.name, p.price, unit, icon, stock);
                setTimeout(() => location.href = "/Customer/Cart", 300);
            };
            if (stock === 0) btnBuy.disabled = true;
        }
    } catch (err) {
        showToast(err.message || "Không tải được chi tiết sản phẩm", "error");
        box.innerHTML = `<p class="empty">Không tìm thấy sản phẩm. <a href="/Customer/Products">Quay lại danh sách</a></p>`;
    }
}
/* ---------- cart.html ---------- */
function initCartPage() {
    const wrap = document.getElementById("cartWrap");
    if (!wrap) return;

    function render() {
        const cart = getCart();
        if (!cart.length) {
            wrap.innerHTML = `<p class="empty">Giỏ hàng trống. <a href="/Customer/Products">Mua sắm ngay</a></p>`;
            renderSummary({ subtotal: 0, shipping: 0, total: 0 });
            return;
        }
        wrap.innerHTML = cart.map(i => `
            <div class="cart-item" data-id="${i.id}">
                <div class="thumb">${i.icon || "💊"}</div>
                <div>
                    <div style="font-weight:600">${escapeHtml(i.name)}</div>
                    <div class="text-muted" style="font-size:13px">${formatPrice(i.price)} / ${i.unit || "Hộp"}</div>
                </div>
                <div class="qty">
                    <input type="number" min="1" value="${i.quantity}"
                           onchange="onCartQtyChange(${i.id}, this.value)">
                </div>
                <div style="font-weight:700;min-width:100px;text-align:right">
                    ${formatPrice(i.price * i.quantity)}
                </div>
                <button class="rm" title="Xóa" onclick="onCartRemove(${i.id})">✕</button>
            </div>
        `).join("");
        renderSummary(getCartTotals());
    }

    function renderSummary(t) {
        const sum = document.getElementById("cartSummary");
        if (!sum) return;
        sum.innerHTML = `
            <h3 style="margin-bottom:14px">Tóm tắt đơn hàng</h3>
            <div class="row"><span>Tạm tính</span><b>${formatPrice(t.subtotal)}</b></div>
            <div class="row"><span>Phí giao hàng</span><b>${t.shipping === 0 ? "Miễn phí" : formatPrice(t.shipping)}</b></div>
            <div class="total"><span>Tổng cộng</span><span>${formatPrice(t.total)}</span></div>
            <a class="primary-btn" style="display:block;text-align:center;text-decoration:none;margin-top:16px"
               href="/Customer/Checkout">Tiến hành đặt hàng</a>
            <button class="btn muted" style="width:100%;margin-top:8px" onclick="onCartClear()">Xóa giỏ hàng</button>
        `;
    }

    window.onCartQtyChange = (id, v) => { updateCartItem(id, +v); render(); };
    window.onCartRemove = (id) => { removeCartItem(id); showToast("Đã xóa sản phẩm"); render(); };
    window.onCartClear = () => { if (confirm("Xóa toàn bộ giỏ hàng?")) { clearCart(); render(); } };

    render();
}

/* ---------- checkout.html ---------- */
function initCheckoutPage() {
    const form = document.getElementById("checkoutForm");
    if (!form) return;

    // Render review
    const cart = getCart();
    const review = document.getElementById("orderReview");
    review.innerHTML = cart.length
        ? cart.map(i => `<div class="line"><span>${i.quantity}× ${escapeHtml(i.name)}</span><span>${formatPrice(i.price * i.quantity)}</span></div>`).join("")
        : `<p class="empty">Chưa có sản phẩm. <a href="products.html">Mua sắm</a></p>`;

    const t = getCartTotals();
    document.getElementById("ckSubtotal").textContent = formatPrice(t.subtotal);
    document.getElementById("ckShipping").textContent = t.shipping === 0 ? "Miễn phí" : formatPrice(t.shipping);
    document.getElementById("ckTotal").textContent = formatPrice(t.total);

    // File prescription
    const fileInput = document.getElementById("rxFile");
    const fileName = document.getElementById("rxFileName");
    fileInput.addEventListener("change", () => {
        if (fileInput.files.length) {
            fileName.textContent = "📎 " + fileInput.files[0].name;
        } else {
            fileName.textContent = "";
        }
    });

    form.addEventListener("submit", async (e) => {
        e.preventDefault();
        const data = Object.fromEntries(new FormData(form));

        if (!data.name || !data.phone || !data.address || !data.city) {
            showToast("Vui lòng nhập đủ thông tin giao hàng", "error");
            return;
        }
        if (!/^[0-9+\-\s()]{8,15}$/.test(data.phone)) {
            showToast("Số điện thoại không hợp lệ", "error");
            return;
        }
        if (!cart.length) {
            showToast("Giỏ hàng trống", "error");
            return;
        }

        // Ghép địa chỉ + thành phố
        const fullAddress = `${data.address}, ${data.city}`;

        // Chỉ gửi mã sản phẩm và số lượng; server tự lấy giá và tính tổng.
        const items = cart.map(i => ({
            productId: i.id,
            quantity: i.quantity
        }));

        const body = {
            customerName: data.name.trim(),
            phone: data.phone.trim(),
            address: fullAddress,
            items: items
        };

        try {
            // Gọi API OrdersController.CreateOrder
            const result = await apiPost(API.ORDERS, body);

            clearCart();
            showToast(result.message || "Đặt hàng thành công!");
            setTimeout(() => location.href = "/Customer/Orders", 600);
        } catch (err) {
            showToast(err.message || "Đặt hàng thất bại", "error");
        }
    });

}

/* ---------- orders.html ---------- */
async function initOrdersPage() {
    const wrap = document.getElementById("ordersWrap");
    if (!wrap) return;

    const currentUser = getCurrentUser();
    if (!currentUser || !currentUser.id) {
        wrap.innerHTML = `<p class="empty">Vui lòng <a href="/Customer/Login">đăng nhập</a> để xem đơn hàng của bạn.</p>`;
        return;
    }

    const role = (currentUser.role || "customer").toLowerCase();
    if (role !== "customer") {
        const dashboardUrl = typeof dashboardHrefFor === "function"
            ? dashboardHrefFor(role)
            : "/Home/Index";
        wrap.innerHTML = `<p class="empty">Trang này dành cho khách hàng. Tài khoản ${escapeHtml(role)} có thể vào <a href="${dashboardUrl}">bảng điều khiển</a>.</p>`;
        return;
    }

    wrap.innerHTML = `<p class="empty">Đang tải đơn hàng...</p>`;

    // Ánh xạ status backend → tiếng Việt + badge
    const statusMap = {
        "Pending": { text: "Chờ xử lý", badge: "warn" },
        "Approved": { text: "Đã duyệt", badge: "info" },
        "Cancelled": { text: "Đã hủy", badge: "danger" },
        "Completed": { text: "Đã giao", badge: "success" }
    };

    try {
        // Gọi API mới: GET /api/orders/by-user?userId=...
        const orders = await apiGet(`${API.ORDERS}/by-user`);

        if (!orders || !orders.length) {
            wrap.innerHTML = `<p class="empty">Bạn chưa có đơn hàng nào. <a href="/Customer/Products">Mua sắm ngay</a></p>`;
            return;
        }

        wrap.innerHTML = orders.map(o => {
            const st = statusMap[o.status] || { text: o.status || "—", badge: "muted" };
            const itemsText = (o.orderDetails || []).map(d => {
                const name = (d.product && d.product.name) ? d.product.name : `SP #${d.productId}`;
                return `${d.quantity}× ${escapeHtml(name)}`;
            }).join(" · ") || "—";

            return `
            <div class="order-card">
                <div class="head">
                    <div>
                        <div class="code">#${o.id}</div>
                        <div class="text-muted" style="font-size:13px">${formatDateTime(o.createdAt)}</div>
                    </div>
                    <span class="badge ${st.badge}">${escapeHtml(st.text)}</span>
                </div>
                <div class="items">${itemsText}</div>
                <div class="foot">
                    <span class="text-muted">${o.prescriptionUrl ? "📎 Có đơn thuốc" : "Không có đơn thuốc"}</span>
                    <span class="total">${formatPrice(o.totalAmount)}</span>
                </div>
            </div>`;
        }).join("");
    } catch (err) {
        if (err.status === 401 || err.status === 403) {
            wrap.innerHTML = `<p class="empty">Phiên đăng nhập không còn phù hợp với quyền tài khoản. Vui lòng <a href="/Customer/Login">đăng nhập lại</a> bằng tài khoản khách hàng.</p>`;
            return;
        }
        wrap.innerHTML = `<p class="empty">Lỗi tải đơn hàng: ${escapeHtml(err.message || "Không xác định")}</p>`;
        showToast(err.message || "Không tải được danh sách đơn hàng", "error");
    }
}
/* ---------- prescription.html (API PrescriptionsController) ---------- */
function initPrescriptionPage() {
    const list = document.getElementById("rxList");
    if (!list) return;

    const user = typeof getCurrentUser === "function" ? getCurrentUser() : null;
    if (!user) {
        list.innerHTML = `<p class="empty">Vui lòng <a href="/Customer/Login">đăng nhập</a> để quản lý đơn thuốc.</p>`;
        return;
    }

    let itemsCache = [];

    async function loadAndRender() {
        list.innerHTML = `<p class="empty">Đang tải...</p>`;
        try {
            const data = await apiGet(API.PRESCRIPTIONS);
            itemsCache = Array.isArray(data) ? data : [];
            render(itemsCache);
        } catch (err) {
            list.innerHTML = `<p class="empty">Lỗi: ${escapeHtml(err.message || "Không tải được")}</p>`;
            showToast(err.message || "Không tải được đơn thuốc", "error");
        }
    }

    function render(data) {
        if (!data.length) {
            list.innerHTML = `<p class="empty">Chưa có đơn thuốc nào được tải lên.</p>`;
            return;
        }
        list.innerHTML = data.map(rx => `
            <div class="rx-item">
                <div class="preview">${rx.fileType === "pdf" ? "📄" : "🖼️"}</div>
                <div style="font-weight:600">
                    <a href="#" onclick="event.preventDefault();openProtectedFile('${escapeHtml(rx.fileUrl)}')">
                        ${escapeHtml(rx.fileName)}
                    </a>
                </div>
                <div class="text-muted" style="font-size:12px;margin-top:4px">${formatDateTime(rx.uploadedAt)}</div>
                <button class="btn danger small" style="margin-top:8px"
                        onclick="removeRx(${rx.id})">Xóa</button>
            </div>
        `).join("");
    }

    window.removeRx = async (id) => {
        if (!confirm("Xóa đơn thuốc này?")) return;
        try {
            await apiDelete(`${API.PRESCRIPTIONS}/${id}`);
            showToast("Đã xóa đơn thuốc");
            await loadAndRender();
        } catch (err) {
            showToast(err.message || "Lỗi xóa", "error");
        }
    };

    const input = document.getElementById("rxUpload");
    if (input) {
        input.addEventListener("change", async () => {
            const file = input.files[0];
            if (!file) return;

            const ext = (file.name.split(".").pop() || "").toLowerCase();
            if (!["png", "jpg", "jpeg", "pdf"].includes(ext)) {
                showToast("Chỉ hỗ trợ PNG, JPG, JPEG, PDF", "error");
                input.value = "";
                return;
            }

            const form = new FormData();
            form.append("file", file);

            try {
                // apiRequest hỗ trợ FormData (đã có trong api.js)
                await apiPost(API.PRESCRIPTIONS_UPLOAD, form);
                showToast("Đã tải lên đơn thuốc");
                await loadAndRender();
            } catch (err) {
                showToast(err.message || "Lỗi tải lên", "error");
            }
            input.value = "";
        });
    }

    loadAndRender();
}

/* ---------- bootstrap auto-detect page ---------- */
document.addEventListener("DOMContentLoaded", () => {
    updateCartCount();
    const p = location.pathname.toLowerCase();
    if (p.includes("/customer/products")) initProductsPage();
    if (p.includes("/customer/productdetail")) initProductDetailPage();
    if (p.includes("/customer/cart")) initCartPage();
    if (p.includes("/customer/checkout")) initCheckoutPage();
    if (p.includes("/customer/orders")) initOrdersPage();
    if (p.includes("/customer/prescription")) initPrescriptionPage();
});
