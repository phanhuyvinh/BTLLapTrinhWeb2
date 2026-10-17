/* ============================================================
   pharmacy.js — Logic khu vực Dược sĩ
   ============================================================ */

/* ---------- POS (đã chuyển sang API Products + Orders/pos) ---------- */
let posInvoice = [];
let posProductsCache = [];   // danh sách thuốc từ API
let posLastReceipt = null;

function initPosPage() {
    const gridEl = document.getElementById("posGrid");
    if (!gridEl) return;

    document.getElementById("posSearch").addEventListener("input", (e) => {
        const kw = e.target.value.trim().toLowerCase();
        const data = posProductsCache.filter(p =>
            (p.name || "").toLowerCase().includes(kw) ||
            (p.activeIngredient || "").toLowerCase().includes(kw)
        );
        renderPosGrid(data);
    });

    document.getElementById("btnCheckout").onclick = doPosCheckout;
    document.getElementById("btnPrintReceipt").onclick = printPosReceipt;
    const paymentMethod = document.getElementById("posPaymentMethod");
    if (paymentMethod) paymentMethod.addEventListener("change", () => {
        const confirmation = document.getElementById("posNonCashConfirmed");
        if (confirmation) confirmation.checked = false;
        syncPosPaymentFields();
    });
    const receivedInput = document.getElementById("posAmountReceived");
    if (receivedInput) receivedInput.addEventListener("input", () => syncPosPaymentFields());
    syncPosPaymentFields();
    document.getElementById("btnClearInvoice").onclick = () => {
        if (!posInvoice.length) return;
        if (confirm("Xóa toàn bộ hóa đơn?")) {
            posInvoice = [];
            renderInvoice();
        }
    };

    loadPosProducts();
}

// Alias để tương thích nếu chỗ khác còn gọi initPos()
function initPos() {
    initPosPage();
}

async function loadPosProducts() {
    const gridEl = document.getElementById("posGrid");
    if (!gridEl) return;
    gridEl.innerHTML = `<p class="empty">Đang tải sản phẩm...</p>`;

    try {
        const products = await apiGet(API.PRODUCTS);
        posProductsCache = Array.isArray(products) ? products : [];
        renderPosGrid(posProductsCache);
        renderInvoice();
    } catch (err) {
        gridEl.innerHTML = `<p class="empty">Lỗi tải sản phẩm: ${escapeHtml(err.message || "Không xác định")}</p>`;
        showToast(err.message || "Không tải được danh sách thuốc", "error");
    }
}

function renderPosGrid(data) {
    const gridEl = document.getElementById("posGrid");
    if (!gridEl) return;

    if (!data || !data.length) {
        gridEl.innerHTML = `<p class="empty">Không có sản phẩm.</p>`;
        return;
    }

    gridEl.innerHTML = data.map(p => {
        const stock = p.stockQuantity != null ? p.stockQuantity : 0;
        const imgUrl = (p.imageUrl || p.ImageUrl || "").trim();
        const isPath = imgUrl.startsWith("/") || imgUrl.startsWith("http");
        const iconHtml = isPath
            ? `<img src="${escapeHtml(imgUrl)}" alt="" style="max-width:64px;max-height:64px;object-fit:contain" onerror="this.outerHTML='💊'">`
            : `<span>${escapeHtml(imgUrl || "💊")}</span>`;
        const disabledCls = stock <= 0 ? " style=\"opacity:0.5;cursor:not-allowed\"" : "";
        return `
    <div class="pos-card" onclick="posAdd(${p.id})"${disabledCls}>
        <div class="ic">${iconHtml}</div>
        <div class="nm">${escapeHtml(p.name)}</div>
        <div class="pr">${formatPrice(p.price)}</div>
        <div class="st">Tồn: ${stock}</div>
    </div>`;
    }).join("");
}

function posAdd(id) {
    const p = posProductsCache.find(x => x.id === id);
    if (!p) return;
    const stock = p.stockQuantity != null ? p.stockQuantity : 0;
    if (stock <= 0) {
        showToast("Sản phẩm hết hàng", "warn");
        return;
    }

    const it = posInvoice.find(x => x.id === id);
    if (it) {
        if (it.quantity >= stock) {
            showToast("Không đủ tồn kho", "warn");
            return;
        }
        it.quantity++;
    } else {
        posInvoice.push({
            id: p.id,
            name: p.name,
            price: p.price,
            quantity: 1,
            maxStock: stock
        });
    }
    renderInvoice();
}

function renderInvoice() {
    const listEl = document.getElementById("invList");
    const totEl = document.getElementById("invTotals");
    const cntEl = document.getElementById("invCount");
    if (!listEl) return;

    if (!posInvoice.length) {
        listEl.innerHTML = `<p class="empty">Chưa có sản phẩm trong hóa đơn.</p>`;
    } else {
        listEl.innerHTML = posInvoice.map(i => `
            <div class="inv-row">
                <div>
                    <div style="font-weight:600">${escapeHtml(i.name)}</div>
                    <div class="text-muted" style="font-size:12px">${formatPrice(i.price)}</div>
                </div>
                <div class="q">
                    <input type="number" min="1" max="${i.maxStock || 999}" value="${i.quantity}"
                           onchange="posQty(${i.id}, this.value)">
                </div>
                <button class="rm" onclick="posRemove(${i.id})">✕</button>
            </div>
        `).join("");
    }

    const subtotal = posInvoice.reduce((s, i) => s + i.price * i.quantity, 0);
    const vat = Math.round(subtotal * 0.05);
    const total = subtotal + vat;

    if (cntEl) cntEl.textContent = posInvoice.reduce((s, i) => s + i.quantity, 0);
    if (totEl) {
        totEl.innerHTML = `
            <div class="row"><span>Tạm tính</span><b>${formatPrice(subtotal)}</b></div>
            <div class="row"><span>VAT (5%)</span><b>${formatPrice(vat)}</b></div>
            <div class="row grand"><span>Khách phải trả</span><span>${formatPrice(total)}</span></div>
        `;
    }
    syncPosPaymentFields(total);
}

function syncPosPaymentFields(total) {
    const method = document.getElementById("posPaymentMethod")?.value || "Cash";
    const cashFields = document.getElementById("posCashFields");
    const nonCashConfirm = document.getElementById("posNonCashConfirmWrap");
    if (cashFields) cashFields.style.display = method === "Cash" ? "" : "none";
    if (nonCashConfirm) nonCashConfirm.style.display = method === "Cash" ? "none" : "block";
    const subtotal = posInvoice.reduce((sum, item) => sum + item.price * item.quantity, 0);
    const amountDue = total ?? subtotal + Math.round(subtotal * 0.05);
    const received = Number(document.getElementById("posAmountReceived")?.value || 0);
    const change = Math.max(0, received - amountDue);
    const changeEl = document.getElementById("posChangeAmount");
    if (changeEl) changeEl.textContent = received >= amountDue && amountDue > 0
        ? `Tiền thừa: ${formatPrice(change)}`
        : `Còn thiếu: ${formatPrice(Math.max(0, amountDue - received))}`;
}

window.posQty = (id, v) => {
    const it = posInvoice.find(x => x.id === id);
    if (!it) return;
    let q = Math.max(1, +v || 1);
    if (it.maxStock != null && q > it.maxStock) {
        q = it.maxStock;
        showToast("Số lượng không vượt quá tồn kho (" + it.maxStock + ")", "warn");
    }
    it.quantity = q;
    renderInvoice();
};

window.posRemove = (id) => {
    posInvoice = posInvoice.filter(x => x.id !== id);
    renderInvoice();
};

async function doPosCheckout() {
    if (!posInvoice.length) {
        showToast("Hóa đơn trống", "error");
        return;
    }

    const subtotal = posInvoice.reduce((s, i) => s + i.price * i.quantity, 0);
    const total = subtotal + Math.round(subtotal * 0.05);
    const paymentMethod = document.getElementById("posPaymentMethod")?.value || "Cash";
    const amountReceived = paymentMethod === "Cash"
        ? Number(document.getElementById("posAmountReceived")?.value || 0)
        : total;
    const nonCashPaymentConfirmed = paymentMethod === "Cash"
        ? false
        : Boolean(document.getElementById("posNonCashConfirmed")?.checked);
    if (paymentMethod === "Cash" && amountReceived < total) {
        showToast("Tiền khách đưa chưa đủ thanh toán", "error");
        return;
    }
    if (paymentMethod !== "Cash" && !nonCashPaymentConfirmed) {
        showToast("Hãy xác nhận đã nhận thanh toán trước khi hoàn tất hóa đơn", "error");
        return;
    }

    // Server lấy giá sản phẩm và tính tổng; client chỉ gửi mã hàng/số lượng.
    const body = {
        customerName: "Khách vãng lai",
        phone: "",
        paymentMethod,
        amountReceived,
        nonCashPaymentConfirmed,
        items: posInvoice.map(i => ({
            productId: i.id,
            quantity: i.quantity
        }))
    };

    try {
        // POST api/orders/pos  → backend tự trừ StockQuantity
        const result = await apiPost(`${API.ORDERS}/pos`, body);
        const paidTotal = result?.totalAmount ?? total;
        posLastReceipt = {
            orderId: result?.orderId,
            items: posInvoice.map(item => ({ ...item })),
            subtotal,
            tax: paidTotal - subtotal,
            total: paidTotal,
            paymentMethod,
            amountReceived: result?.amountReceived ?? amountReceived,
            changeAmount: result?.changeAmount ?? Math.max(0, amountReceived - paidTotal),
            paidAt: new Date()
        };
        const printButton = document.getElementById("btnPrintReceipt");
        if (printButton) printButton.style.display = "block";
        showToast(result?.orderId
            ? `Thanh toán thành công hóa đơn #${result.orderId} — ${formatPrice(paidTotal)}${result.changeAmount > 0 ? ` · Tiền thừa ${formatPrice(result.changeAmount)}` : ""}`
            : (result?.message || `Thanh toán thành công ${formatPrice(paidTotal)}`));

        posInvoice = [];
        document.getElementById("posAmountReceived").value = "";
        document.getElementById("posNonCashConfirmed").checked = false;
        document.getElementById("posPaymentMethod").value = "Cash";
        renderInvoice();
        // Tải lại tồn kho mới từ API
        await loadPosProducts();
    } catch (err) {
        showToast(err.message || "Lỗi thanh toán POS", "error");
    }
}

function printPosReceipt() {
    if (!posLastReceipt) return;
    const receipt = posLastReceipt;
    const printWindow = window.open("", "_blank", "width=420,height=720");
    if (!printWindow) {
        showToast("Trình duyệt đang chặn cửa sổ in. Hãy cho phép cửa sổ bật lên rồi thử lại.", "error");
        return;
    }
    const methodLabels = { Cash: "Tiền mặt", BankTransfer: "Chuyển khoản", Card: "Thẻ ngân hàng" };
    const rows = receipt.items.map(item => `
        <tr><td>${escapeHtml(item.name)}<br><small>${item.quantity} × ${formatPrice(item.price)}</small></td>
            <td class="amount">${formatPrice(item.quantity * item.price)}</td></tr>`).join("");
    printWindow.document.write(`<!doctype html><html lang="vi"><head><meta charset="utf-8"><title>Hóa đơn #${receipt.orderId || "POS"}</title>
        <style>body{font:14px Arial,sans-serif;width:340px;margin:24px auto;color:#111}h1,p{text-align:center;margin:6px 0}h1{font-size:20px}.meta{margin:18px 0;border-top:1px dashed #777;border-bottom:1px dashed #777;padding:8px 0}table{width:100%;border-collapse:collapse}td{padding:7px 0;vertical-align:top}.amount{text-align:right;white-space:nowrap}.totals{margin-top:12px;border-top:1px dashed #777;padding-top:10px}.line{display:flex;justify-content:space-between;margin:6px 0}.grand{font-size:17px;font-weight:bold}.thanks{margin-top:20px}@media print{body{margin:0 auto}}</style></head><body>
        <h1>MedCare Pharmacy</h1><p>HÓA ĐƠN BÁN LẺ</p><div class="meta"><div>Mã hóa đơn: #${receipt.orderId || "—"}</div>
        <div>Thời gian: ${receipt.paidAt.toLocaleString("vi-VN")}</div><div>Thanh toán: ${methodLabels[receipt.paymentMethod] || escapeHtml(receipt.paymentMethod)}</div></div>
        <table>${rows}</table><div class="totals"><div class="line"><span>Tạm tính</span><span>${formatPrice(receipt.subtotal)}</span></div>
        <div class="line"><span>Thuế (5%)</span><span>${formatPrice(receipt.tax)}</span></div><div class="line grand"><span>Tổng thanh toán</span><span>${formatPrice(receipt.total)}</span></div>
        <div class="line"><span>Khách đưa</span><span>${formatPrice(receipt.amountReceived)}</span></div><div class="line"><span>Tiền thừa</span><span>${formatPrice(receipt.changeAmount)}</span></div></div>
        <p class="thanks">Cảm ơn quý khách!</p><script>window.onload=()=>window.print();<\/script></body></html>`);
    printWindow.document.close();
}

/* ---------- Online Orders (đã chuyển sang API OrdersController) ---------- */

// Map trạng thái backend ↔ hiển thị tiếng Việt
const ORDER_STATUS_LABEL = {
    "Pending": "Chờ xử lý",
    "Approved": "Đã duyệt",
    "Cancelled": "Đã hủy",
    "Completed": "Đã giao",
    "Shipping": "Đang giao",
    "Checking": "Đang kiểm tra"
};
const ORDER_STATUS_BADGE = {
    "Pending": "warn",
    "Approved": "success",
    "Cancelled": "danger",
    "Completed": "success",
    "Shipping": "info",
    "Checking": "info"
};

let onlineOrdersCache = [];   // dữ liệu thô từ API

function initOnlineOrdersPage() {
    const tbody = document.getElementById("ordersBody");
    if (!tbody) return;
    loadAndRenderOrders();
}

// Giữ tên cũ để tương thích nếu chỗ khác còn gọi
function initOnlineOrders() {
    initOnlineOrdersPage();
}

async function loadAndRenderOrders() {
    const tbody = document.getElementById("ordersBody");
    if (!tbody) return;
    tbody.innerHTML = `<tr><td colspan="8" class="empty">Đang tải đơn hàng...</td></tr>`;

    try {
        // GET api/orders
        const orders = await apiGet(API.ORDERS);
        // Chỉ lấy đơn Online (hoặc tất cả nếu muốn xem cả POS)
        onlineOrdersCache = (Array.isArray(orders) ? orders : [])
            .filter(o => !o.orderType || o.orderType === "Online");
        renderOrders(onlineOrdersCache);
    } catch (err) {
        tbody.innerHTML = `<tr><td colspan="8" class="empty">Lỗi tải đơn: ${escapeHtml(err.message || "Không xác định")}</td></tr>`;
        showToast(err.message || "Không tải được danh sách đơn hàng", "error");
    }
}

function renderOrders(orders) {
    const tbody = document.getElementById("ordersBody");
    if (!tbody) return;

    if (!orders || !orders.length) {
        tbody.innerHTML = `<tr><td colspan="8" class="empty">Không có đơn hàng.</td></tr>`;
        return;
    }

    tbody.innerHTML = orders.map(o => {
        // Tên sản phẩm từ OrderDetails
        const itemsText = (o.orderDetails && o.orderDetails.length)
            ? o.orderDetails.map(d => {
                const name = (d.product && d.product.name) ? d.product.name : ("SP#" + d.productId);
                return `${d.quantity}× ${name}`;
            }).join(", ")
            : "—";

        const hasRx = !!(o.prescriptionUrl);
        const statusKey = o.status || "Pending";
        const statusLabel = ORDER_STATUS_LABEL[statusKey] || statusKey;
        const badgeCls = ORDER_STATUS_BADGE[statusKey] || "muted";

        // Ẩn nút Duyệt/Hủy nếu đã xử lý xong
        const canAction = statusKey === "Pending" || statusKey === "Checking";

        return `
        <tr>
            <td><b>#${o.id}</b></td>
            <td>${escapeHtml(o.customerName || "—")}<br>
                <span class="text-muted" style="font-size:12px">${escapeHtml(o.phone || "")}</span>
            </td>
            <td>${escapeHtml(itemsText)}</td>
            <td>${hasRx
                ? '<span class="badge info">📎 Có</span>'
                : '<span class="badge muted">Không</span>'}
            </td>
            <td>${formatPrice(o.totalAmount)}</td>
            <td>${formatDateTime(o.createdAt)}</td>
            <td><span class="badge ${badgeCls}">${escapeHtml(statusLabel)}</span></td>
            <td>
                <div class="actions">
                    ${hasRx
                ? `<button class="btn small ghost" onclick="viewRx(${o.id})">Xem ĐT</button>`
                : ""}
                    ${canAction
                ? `<button class="btn small" onclick="approveOrder(${o.id})">Duyệt</button>
                           <button class="btn small danger" onclick="cancelOrder(${o.id})">Hủy</button>`
                : `<span class="text-muted" style="font-size:12px">—</span>`}
                </div>
            </td>
        </tr>`;
    }).join("");
}

window.viewRx = (orderId) => {
    const o = onlineOrdersCache.find(x => x.id === orderId);
    if (!o) return;

    const rxContent = o.prescriptionUrl
        ? (o.prescriptionUrl.startsWith("/api/prescriptions/")
            ? `<button class="btn" onclick="openProtectedFile('${escapeHtml(o.prescriptionUrl)}')">Mở tệp đơn thuốc</button>`
            : (o.prescriptionUrl.startsWith("http") || o.prescriptionUrl.startsWith("/")
            ? `<img src="${escapeHtml(o.prescriptionUrl)}" alt="Đơn thuốc"
                   style="max-width:100%;border-radius:8px;border:1px solid #ddd">`
            : `<div style="text-align:center;padding:30px;background:#f5faf8;border-radius:10px;font-size:60px">🖼️</div>
               <p class="text-muted mt-8" style="font-size:13px">${escapeHtml(o.prescriptionUrl)}</p>`))
        : `<div style="text-align:center;padding:30px;background:#f5faf8;border-radius:10px;font-size:60px">🖼️</div>
           <p class="text-muted mt-8" style="font-size:13px">Không có hình ảnh đơn thuốc.</p>`;

    openModal(`
        <h3>Đơn thuốc — Đơn #${o.id}</h3>
        ${rxContent}
        <p class="text-muted mt-8" style="font-size:13px">
            Khách: ${escapeHtml(o.customerName || "")} — ${escapeHtml(o.phone || "")}
        </p>
    `, []);
};

window.approveOrder = async (orderId) => {
    if (!confirm("Duyệt đơn hàng #" + orderId + "?")) return;
    try {
        // PUT api/orders/{id}/status
        await apiPut(`${API.ORDERS}/${orderId}/status`, { status: "Approved" });
        showToast("Đã duyệt đơn #" + orderId);
        await loadAndRenderOrders();
    } catch (err) {
        showToast(err.message || "Lỗi duyệt đơn", "error");
    }
};

window.cancelOrder = async (orderId) => {
    if (!confirm("Hủy đơn hàng #" + orderId + "?")) return;
    try {
        await apiPut(`${API.ORDERS}/${orderId}/status`, { status: "Cancelled" });
        showToast("Đã hủy đơn #" + orderId);
        await loadAndRenderOrders();
    } catch (err) {
        showToast(err.message || "Lỗi hủy đơn", "error");
    }
};

/* ---------- Inventory Alerts (đã chuyển sang API inventorybatches/alerts) ---------- */
function initInventoryAlertPage() {
    const root = document.getElementById("alertRoot");
    if (!root) return;

    root.innerHTML = `<div class="panel"><p class="empty">Đang tải cảnh báo kho...</p></div>`;
    loadAlerts();
}

// Alias để tương thích nếu chỗ khác còn gọi tên cũ
function initInventoryAlerts() {
    initInventoryAlertPage();
}

async function loadAlerts() {
    const root = document.getElementById("alertRoot");
    if (!root) return;

    try {
        // GET api/inventorybatches/alerts
        const data = await apiGet(API.INV_ALERTS);

        const nearExpiry = Array.isArray(data.nearExpiry) ? data.nearExpiry : [];
        const expired = Array.isArray(data.expired) ? data.expired : [];
        const outOfStock = Array.isArray(data.outOfStock) ? data.outOfStock : [];
        const lowStock = Array.isArray(data.lowStock) ? data.lowStock : [];

        root.innerHTML = `
            ${alertSection("Cận hạn (≤ 90 ngày)", "warn",
            ["Tên thuốc", "Số lô", "Hạn dùng", "Tồn lô"],
            nearExpiry.map(a => [
                a.name || "—",
                a.lot || "—",
                formatDate(a.expiry),
                a.stock != null ? a.stock : 0
            ]))}
            ${alertSection("Đã hết hạn", "danger",
                ["Tên thuốc", "Số lô", "Hạn dùng", "Tồn lô"],
                expired.map(a => [
                    a.name || "—",
                    a.lot || "—",
                    formatDate(a.expiry),
                    a.stock != null ? a.stock : 0
                ]))}
            ${alertSection("Hết hàng", "danger",
                    ["Tên thuốc", "Tồn kho"],
                    outOfStock.map(a => [
                        a.name || "—",
                        a.stock != null ? a.stock : 0
                    ]))}
            ${alertSection("Sắp hết hàng (≤ 20)", "warn",
                        ["Tên thuốc", "Tồn kho"],
                        lowStock.map(a => [
                            a.name || "—",
                            a.stock != null ? a.stock : 0
                        ]))}
        `;
    } catch (err) {
        root.innerHTML = `<div class="panel"><p class="empty">Lỗi tải cảnh báo: ${escapeHtml(err.message || "Không xác định")}</p></div>`;
        showToast(err.message || "Không tải được cảnh báo kho", "error");
    }
}

function alertSection(title, cls, cols, rows) {
    const headerRow = cols.map(c => `<th>${c}</th>`).join("");
    const bodyRows = rows.length
        ? rows.map(r => `<tr>${r.map(c => `<td>${escapeHtml(String(c))}</td>`).join("")}</tr>`).join("")
        : `<tr><td colspan="${cols.length}" class="empty">Không có dữ liệu</td></tr>`;

    return `
        <div class="panel">
            <div class="panel-title">
                <span>${title}</span>
                <span class="badge ${cls}">${rows.length}</span>
            </div>
            <div class="table-wrap">
                <table class="tbl">
                    <thead><tr>${headerRow}</tr></thead>
                    <tbody>${bodyRows}</tbody>
                </table>
            </div>
        </div>
    `;
}
/* ---------- Pharmacist Dashboard ---------- */
async function initPharmacistDashboard() {
    const root = document.getElementById("dashStats");
    if (!root) return;

    try {
        const data = await apiGet(API.REPORT_PHARMA_DASH);

        const set = (id, val) => {
            const el = document.getElementById(id);
            if (el) el.textContent = val;
        };

        set("stRevenue", formatPrice(data.revenueToday || 0));
        set("stPosCount", String(data.posCount || 0));
        set("stOnlineCount", String(data.onlineCount || 0));
        set("stAlerts", String(data.alerts || 0));
        set("chatUnread", String(data.chatUnread || 0));
    } catch (err) {
        showToast(err.message || "Không tải được thống kê", "error");
    }
}
/* ---------- Auto-detect Page Routes ---------- */
document.addEventListener("DOMContentLoaded", () => {
    const p = location.pathname.toLowerCase();

    // const dash = document.getElementById("dashStats");
    // if (dash && (p.includes("/pharmacist/index") || p === "/pharmacist" || p === "/pharmacist/")) {
    //     document.getElementById("stRevenue").textContent = formatPrice(4850000);
    //     document.getElementById("stPosCount").textContent = "23";
    //     document.getElementById("stOnlineCount").textContent = "6";
    //     document.getElementById("stAlerts").textContent = "4";
    // }
});

/* ---------- Shared modal ---------- */
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
    const actionsHtml = actions.map(a => `<button class="btn ${a.cls || ''}" onclick="${a.onclick || ''}">${a.label}</button>`).join("");
    body.innerHTML = html + `
        <div class="modal-actions">
            ${actionsHtml}
            <button class="btn muted" onclick="closeModal()">Đóng</button>
        </div>
    `;
    bd.classList.add("show");
}

function closeModal() {
    const bd = document.getElementById("globalModal");
    if (bd) bd.classList.remove("show");
}
