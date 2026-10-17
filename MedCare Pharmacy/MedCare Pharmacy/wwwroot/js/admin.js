/* ============================================================
   admin.js — Logic khu vực Admin
   (Đã cập nhật khớp chuẩn ASP.NET Core MVC Routes)
   ============================================================ */

/* ---------- Dashboard ---------- */
/* ---------- Dashboard ---------- */
async function initAdminDashboard() {
    const root = document.getElementById("adminStats");
    if (!root) return;

    try {
        const data = await apiGet(API.REPORT_DASHBOARD);

        const el = (id) => document.getElementById(id);
        if (el("stRevenue")) el("stRevenue").textContent = formatPrice(data.totalRevenue || 0);
        if (el("stOrders")) el("stOrders").textContent = String(data.totalOrders || 0);
        if (el("stProducts")) el("stProducts").textContent = String(data.totalProducts || 0);
        if (el("stUsers")) el("stUsers").textContent = String(data.totalUsers || 0);
        if (el("stAlerts")) el("stAlerts").textContent = String(data.alerts || 0);

        // Các thẻ phụ trên Index (nếu có text cứng) — cập nhật nếu tìm thấy theo thứ tự
        // Đơn chờ duyệt / Sản phẩm sắp hết / Lô cận hạn nằm trong stat-grid thứ 2
        // Không bắt buộc sửa HTML; số chính đã cập nhật qua id ở trên.
    } catch (err) {
        showToast(err.message || "Không tải được thống kê dashboard", "error");
    }
}

/* ---------- Medicines CRUD (đã chuyển sang API ProductsController) ---------- */
function initMedicinesPage() {
    const tbody = document.getElementById("medBody");
    if (!tbody) return;

    let categoriesCache = [];   // [{id, name}, ...]
    let suppliersCache = [];    // [{id, name}, ...]
    let productsCache = [];     // dữ liệu từ API

    // Load danh mục + nhà cung cấp + danh sách thuốc
    async function loadLookups() {
        try {
            const [cats, sups] = await Promise.all([
                apiGet(API.CATEGORIES),
                apiGet(API.SUPPLIERS)
            ]);
            categoriesCache = Array.isArray(cats) ? cats : [];
            suppliersCache = Array.isArray(sups) ? sups : [];
        } catch (err) {
            console.warn("Không tải được categories/suppliers:", err.message);
            categoriesCache = [];
            suppliersCache = [];
        }
    }

    async function loadAndRender() {
        tbody.innerHTML = `<tr><td colspan="8" class="empty">Đang tải...</td></tr>`;
        try {
            const products = await apiGet(API.PRODUCTS);
            productsCache = Array.isArray(products) ? products : [];
            render(productsCache);
        } catch (err) {
            tbody.innerHTML = `<tr><td colspan="8" class="empty">Lỗi tải thuốc: ${escapeHtml(err.message || "Không xác định")}</td></tr>`;
            showToast(err.message || "Không tải được danh sách thuốc", "error");
        }
    }

    function render(list) {
        if (!list || !list.length) {
            tbody.innerHTML = `<tr><td colspan="8" class="empty">Chưa có thuốc nào.</td></tr>`;
            return;
        }
        tbody.innerHTML = list.map(m => {
            const categoryName = (m.category && m.category.name) ? m.category.name : "—";
            const supplierName = (m.supplier && m.supplier.name) ? m.supplier.name : "—";
            const active = m.activeIngredient || "—";
            const stock = m.stockQuantity != null ? m.stockQuantity : 0;
            return `
            <tr>
                <td>${m.id}</td>
                <td><b>${escapeHtml(m.name)}</b></td>
                <td>${escapeHtml(active)}</td>
                <td>${formatPrice(m.price)}</td>
                <td>${stock}</td>
                <td>${escapeHtml(categoryName)}</td>
                <td>${escapeHtml(supplierName)}</td>
                <td>
                    <div class="actions">
                        <button class="btn small ghost" onclick="editMed(${m.id})">Sửa</button>
                        <button class="btn small danger" onclick="deleteMed(${m.id})">Xóa</button>
                    </div>
                </td>
            </tr>`;
        }).join("");
    }

    window.openMedicineForm = (med = null) => {
        // med là object từ API (có id, name, activeIngredient, price, stockQuantity, unit, description, categoryId, supplierId)
        const catOptions = categoriesCache.map(c =>
            `<option value="${c.id}" ${med && med.categoryId === c.id ? "selected" : ""}>${escapeHtml(c.name)}</option>`
        ).join("");
        const supOptions = suppliersCache.map(s =>
            `<option value="${s.id}" ${med && med.supplierId === s.id ? "selected" : ""}>${escapeHtml(s.name)}</option>`
        ).join("");

        openModal(`
            <h3>${med ? "Sửa" : "Thêm"} thuốc</h3>
            <div class="form-grid">
                <div class="form-field full"><label>Tên thuốc</label><input id="mName" value="${med ? escapeHtml(med.name) : ""}"></div>
                <div class="form-field"><label>Hoạt chất</label><input id="mActive" value="${med ? escapeHtml(med.activeIngredient || "") : ""}"></div>
                <div class="form-field"><label>Danh mục</label><select id="mCat"><option value="">-- Chọn --</option>${catOptions}</select></div>
                <div class="form-field"><label>Giá (đ)</label><input id="mPrice" type="number" min="0" value="${med ? med.price : 0}"></div>
                <div class="form-field"><label>Tồn kho</label><input id="mStock" type="number" min="0" value="${med ? (med.stockQuantity != null ? med.stockQuantity : 0) : 0}"></div>
                <div class="form-field"><label>Đơn vị</label><input id="mUnit" value="${med ? escapeHtml(med.unit || "Hộp") : "Hộp"}"></div>
                <div class="form-field"><label>Nhà cung cấp</label><select id="mSupplier"><option value="">-- Chọn --</option>${supOptions}</select></div>
                <div class="form-field full"><label>Mô tả</label><textarea id="mDesc" rows="2">${med ? escapeHtml(med.description || "") : ""}</textarea></div>
            </div>
            <div id="mError" class="error"></div>
        `, [{ label: med ? "Cập nhật" : "Thêm", cls: "", onclick: `saveMed(${med ? med.id : "null"})` }]);
    };

    window.saveMed = async (id) => {
        const name = document.getElementById("mName").value.trim();
        const price = +document.getElementById("mPrice").value;
        const stock = +document.getElementById("mStock").value;
        if (!name) { showToast("Tên thuốc bắt buộc", "error"); return; }
        if (price < 0 || stock < 0) { showToast("Giá và tồn kho >= 0", "error"); return; }

        const catVal = document.getElementById("mCat").value;
        const supVal = document.getElementById("mSupplier").value;

        const existing = id ? productsCache.find(x => x.id === id) : null;
        const body = {
            id: id || 0,
            name: name,
            activeIngredient: document.getElementById("mActive").value.trim() || null,
            price: price,
            stockQuantity: stock,
            unit: document.getElementById("mUnit").value.trim() || "Hộp",
            description: document.getElementById("mDesc").value.trim() || "",
            imageUrl: (existing && existing.imageUrl) ? existing.imageUrl : "",
            categoryId: catVal ? parseInt(catVal, 10) : null,
            supplierId: supVal ? parseInt(supVal, 10) : null
        };

        try {
            if (id) {
                // PUT api/products/{id}
                await apiPut(`${API.PRODUCTS}/${id}`, body);
                showToast("Đã cập nhật thuốc");
            } else {
                // POST api/products
                await apiPost(API.PRODUCTS, body);
                showToast("Đã thêm thuốc");
            }
            closeModal();
            await loadAndRender();
        } catch (err) {
            showToast(err.message || "Lỗi lưu thuốc", "error");
        }
    };

    window.editMed = (id) => {
        const med = productsCache.find(x => x.id === id);
        if (med) openMedicineForm(med);
        else showToast("Không tìm thấy thuốc", "error");
    };

    window.deleteMed = async (id) => {
        if (!confirm("Xóa thuốc này?")) return;
        try {
            await apiDelete(`${API.PRODUCTS}/${id}`);
            showToast("Đã xóa thuốc");
            await loadAndRender();
        } catch (err) {
            showToast(err.message || "Lỗi xóa thuốc", "error");
        }
    };

    // Khởi tạo
    document.getElementById("btnAddMed").onclick = () => openMedicineForm();
    (async () => {
        await loadLookups();
        await loadAndRender();
    })();
}

/* ---------- Categories (đã chuyển sang API CategoriesController) ---------- */
function initCategoriesPage() {
    const tbody = document.getElementById("catBody");
    if (!tbody) return;

    let categoriesCache = [];
    let productCountMap = {};   // categoryId -> số sản phẩm

    async function loadProductCounts() {
        try {
            const products = await apiGet(API.PRODUCTS);
            productCountMap = {};
            (Array.isArray(products) ? products : []).forEach(p => {
                if (p.categoryId != null) {
                    productCountMap[p.categoryId] = (productCountMap[p.categoryId] || 0) + 1;
                }
            });
        } catch (_) {
            productCountMap = {};
        }
    }

    async function loadAndRender() {
        tbody.innerHTML = `<tr><td colspan="5" class="empty">Đang tải...</td></tr>`;
        try {
            await loadProductCounts();
            const list = await apiGet(API.CATEGORIES);
            categoriesCache = Array.isArray(list) ? list : [];
            render(categoriesCache);
        } catch (err) {
            tbody.innerHTML = `<tr><td colspan="5" class="empty">Lỗi: ${escapeHtml(err.message || "Không xác định")}</td></tr>`;
            showToast(err.message || "Không tải được danh mục", "error");
        }
    }

    function render(list) {
        if (!list || !list.length) {
            tbody.innerHTML = `<tr><td colspan="5" class="empty">Chưa có danh mục.</td></tr>`;
            return;
        }
        tbody.innerHTML = list.map(c => `
            <tr>
                <td>${c.id}</td>
                <td><b>${escapeHtml(c.name)}</b></td>
                <td>${escapeHtml(c.description || "")}</td>
                <td>${productCountMap[c.id] || 0}</td>
                <td>
                    <div class="actions">
                        <button class="btn small ghost" onclick="editCat(${c.id})">Sửa</button>
                        <button class="btn small danger" onclick="deleteCat(${c.id})">Xóa</button>
                    </div>
                </td>
            </tr>
        `).join("");
    }

    window.openCatForm = (c = null) => {
        openModal(`
            <h3>${c ? "Sửa" : "Thêm"} danh mục</h3>
            <div class="form-grid">
                <div class="form-field full"><label>Tên danh mục</label><input id="cName" value="${c ? escapeHtml(c.name) : ""}"></div>
                <div class="form-field full"><label>Mô tả</label><textarea id="cDesc" rows="2">${c ? escapeHtml(c.description || "") : ""}</textarea></div>
            </div>
        `, [{ label: c ? "Cập nhật" : "Thêm", onclick: `saveCat(${c ? c.id : "null"})` }]);
    };

    window.saveCat = async (id) => {
        const name = document.getElementById("cName").value.trim();
        const description = document.getElementById("cDesc").value.trim();
        if (!name) { showToast("Tên bắt buộc", "error"); return; }

        const body = { id: id || 0, name, description: description || null };

        try {
            if (id) {
                await apiPut(`${API.CATEGORIES}/${id}`, body);
                showToast("Đã cập nhật danh mục");
            } else {
                await apiPost(API.CATEGORIES, body);
                showToast("Đã thêm danh mục");
            }
            closeModal();
            await loadAndRender();
        } catch (err) {
            showToast(err.message || "Lỗi lưu danh mục", "error");
        }
    };

    window.editCat = (id) => {
        const c = categoriesCache.find(x => x.id === id);
        if (c) openCatForm(c);
        else showToast("Không tìm thấy danh mục", "error");
    };

    window.deleteCat = async (id) => {
        if (!confirm("Xóa danh mục này?")) return;
        try {
            await apiDelete(`${API.CATEGORIES}/${id}`);
            showToast("Đã xóa danh mục");
            await loadAndRender();
        } catch (err) {
            showToast(err.message || "Lỗi xóa danh mục", "error");
        }
    };

    document.getElementById("btnAddCat").onclick = () => openCatForm();
    loadAndRender();
}
/* ---------- Suppliers (đã chuyển sang API SuppliersController) ---------- */
function initSuppliersPage() {
    const tbody = document.getElementById("supBody");
    if (!tbody) return;

    let suppliersCache = [];

    async function loadAndRender() {
        tbody.innerHTML = `<tr><td colspan="7" class="empty">Đang tải...</td></tr>`;
        try {
            const list = await apiGet(API.SUPPLIERS);
            suppliersCache = Array.isArray(list) ? list : [];
            render(suppliersCache);
        } catch (err) {
            tbody.innerHTML = `<tr><td colspan="7" class="empty">Lỗi: ${escapeHtml(err.message || "Không xác định")}</td></tr>`;
            showToast(err.message || "Không tải được nhà cung cấp", "error");
        }
    }

    function render(list) {
        if (!list || !list.length) {
            tbody.innerHTML = `<tr><td colspan="7" class="empty">Chưa có nhà cung cấp.</td></tr>`;
            return;
        }
        tbody.innerHTML = list.map(s => {
            // Entity dùng Status = "Active" / "Inactive" (chữ hoa đầu)
            const isActive = (s.status || "").toLowerCase() === "active";
            return `
            <tr>
                <td>${s.id}</td>
                <td><b>${escapeHtml(s.name)}</b></td>
                <td>${escapeHtml(s.phone || "")}</td>
                <td>${escapeHtml(s.email || "")}</td>
                <td>${escapeHtml(s.address || "")}</td>
                <td><span class="badge ${isActive ? "success" : "muted"}">${isActive ? "Hoạt động" : "Ngưng"}</span></td>
                <td>
                    <div class="actions">
                        <button class="btn small ghost" onclick="editSup(${s.id})">Sửa</button>
                        <button class="btn small danger" onclick="deleteSup(${s.id})">Xóa</button>
                    </div>
                </td>
            </tr>`;
        }).join("");
    }

    window.openSupForm = (s = null) => {
        const currentStatus = s ? (s.status || "Active") : "Active";
        const isActive = currentStatus.toLowerCase() === "active";
        openModal(`
            <h3>${s ? "Sửa" : "Thêm"} nhà cung cấp</h3>
            <div class="form-grid">
                <div class="form-field full"><label>Tên</label><input id="sName" value="${s ? escapeHtml(s.name) : ""}"></div>
                <div class="form-field"><label>SĐT</label><input id="sPhone" value="${s ? escapeHtml(s.phone || "") : ""}"></div>
                <div class="form-field"><label>Email</label><input id="sEmail" value="${s ? escapeHtml(s.email || "") : ""}"></div>
                <div class="form-field full"><label>Địa chỉ</label><input id="sAddress" value="${s ? escapeHtml(s.address || "") : ""}"></div>
                <div class="form-field full"><label>Trạng thái</label>
                    <select id="sStatus">
                        <option value="Active"   ${isActive ? "selected" : ""}>Hoạt động</option>
                        <option value="Inactive" ${!isActive ? "selected" : ""}>Ngưng</option>
                    </select>
                </div>
            </div>
        `, [{ label: s ? "Cập nhật" : "Thêm", onclick: `saveSup(${s ? s.id : "null"})` }]);
    };

    window.saveSup = async (id) => {
        const name = document.getElementById("sName").value.trim();
        if (!name) { showToast("Tên NCC bắt buộc", "error"); return; }

        const body = {
            id: id || 0,
            name: name,
            phone: document.getElementById("sPhone").value.trim() || null,
            email: document.getElementById("sEmail").value.trim() || null,
            address: document.getElementById("sAddress").value.trim() || null,
            status: document.getElementById("sStatus").value   // "Active" | "Inactive"
        };

        try {
            if (id) {
                await apiPut(`${API.SUPPLIERS}/${id}`, body);
                showToast("Đã cập nhật nhà cung cấp");
            } else {
                await apiPost(API.SUPPLIERS, body);
                showToast("Đã thêm nhà cung cấp");
            }
            closeModal();
            await loadAndRender();
        } catch (err) {
            showToast(err.message || "Lỗi lưu nhà cung cấp", "error");
        }
    };

    window.editSup = (id) => {
        const s = suppliersCache.find(x => x.id === id);
        if (s) openSupForm(s);
        else showToast("Không tìm thấy nhà cung cấp", "error");
    };

    window.deleteSup = async (id) => {
        if (!confirm("Xóa NCC này?")) return;
        try {
            await apiDelete(`${API.SUPPLIERS}/${id}`);
            showToast("Đã xóa nhà cung cấp");
            await loadAndRender();
        } catch (err) {
            showToast(err.message || "Lỗi xóa nhà cung cấp", "error");
        }
    };

    document.getElementById("btnAddSup").onclick = () => openSupForm();
    loadAndRender();
}

/* ---------- Nhập kho (đã chuyển sang API InventoryBatchesController) ---------- */
let purchaseDraft = [];
let nhapKhoProducts = [];   // danh sách thuốc từ API để đổ select

function initNhapKhoPage() {
    const tbody = document.getElementById("poBody");
    if (!tbody) return;

    purchaseDraft = [];
    document.getElementById("btnAddPoLine").onclick = addPoLine;
    document.getElementById("btnSavePo").onclick = savePo;

    loadProductsForSelect();
    loadSuppliers();
    loadHistory();
    render();

    async function loadProductsForSelect() {
        const sel = document.getElementById("poMed");
        if (!sel) return;
        sel.innerHTML = `<option value="">-- Đang tải --</option>`;
        try {
            const products = await apiGet(API.PRODUCTS);
            nhapKhoProducts = Array.isArray(products) ? products : [];
            sel.innerHTML = `<option value="">-- Chọn thuốc --</option>` +
                nhapKhoProducts.map(p =>
                    `<option value="${p.id}">${escapeHtml(p.name)} — ${formatPrice(p.price)} (Tồn: ${p.stockQuantity != null ? p.stockQuantity : 0})</option>`
                ).join("");
        } catch (err) {
            sel.innerHTML = `<option value="">-- Lỗi tải thuốc --</option>`;
            showToast(err.message || "Không tải được danh sách thuốc", "error");
        }
    }

    async function loadSuppliers() {
        const sel = document.getElementById("poSupplier");
        if (!sel) return;
        try {
            const suppliers = await apiGet(API.SUPPLIERS);
            const active = Array.isArray(suppliers) ? suppliers.filter(s => (s.status || "Active").toLowerCase() === "active") : [];
            sel.innerHTML = `<option value="">-- Chọn nhà cung cấp --</option>` + active
                .map(s => `<option value="${s.id}">${escapeHtml(s.name)}</option>`).join("");
        } catch (err) {
            sel.innerHTML = `<option value="">-- Lỗi tải nhà cung cấp --</option>`;
            showToast(err.message || "Không tải được nhà cung cấp", "error");
        }
    }

    async function loadHistory() {
        const receiptBody = document.getElementById("receiptHistoryBody");
        const movementBody = document.getElementById("movementHistoryBody");
        try {
            const [receipts, movements] = await Promise.all([
                apiGet(API.PURCHASE_RECEIPTS), apiGet(API.INVENTORY_MOVEMENTS)
            ]);
            receiptBody.innerHTML = receipts?.length ? receipts.map(r => `
                <tr><td>${escapeHtml(r.receiptNumber)}</td><td>${escapeHtml(r.supplierName)}</td><td>${r.itemCount}</td>
                <td>${r.totalQuantity}</td><td>${formatPrice(r.totalAmount)}</td><td>${formatDate(r.createdAt)}</td></tr>`).join("")
                : `<tr><td colspan="6" class="empty">Chưa có phiếu nhập.</td></tr>`;
            movementBody.innerHTML = movements?.length ? movements.map(m => `
                <tr><td>${formatDate(m.createdAt)}</td><td>${escapeHtml(m.productName)}</td><td>${movementLabel(m.movementType)}</td>
                <td>${m.quantity > 0 ? "+" : ""}${m.quantity}</td><td>${m.stockBefore}</td><td>${m.stockAfter}</td>
                <td>${escapeHtml(m.lotNumber || m.receiptNumber || (m.orderId ? `Đơn #${m.orderId}` : "—"))}</td>
                <td>${escapeHtml(m.performedBy || "Hệ thống")}</td></tr>`).join("")
                : `<tr><td colspan="8" class="empty">Chưa có biến động tồn kho.</td></tr>`;
        } catch (err) {
            receiptBody.innerHTML = `<tr><td colspan="6" class="empty">${escapeHtml(err.message || "Không tải được phiếu nhập")}</td></tr>`;
            movementBody.innerHTML = `<tr><td colspan="8" class="empty">${escapeHtml(err.message || "Không tải được lịch sử kho")}</td></tr>`;
        }
    }

    function movementLabel(type) {
        return ({ Import: "Nhập hàng", Sale: "Bán hàng", OrderCancelled: "Hoàn tồn do hủy đơn" })[type] || type;
    }

    function render() {
        tbody.innerHTML = purchaseDraft.length
            ? purchaseDraft.map((l, idx) => `
                <tr>
                    <td>${escapeHtml(l.medName)}</td>
                    <td>${escapeHtml(l.lot)}</td>
                    <td>${formatDate(l.mfgDate)}</td>
                    <td>${formatDate(l.expDate)}</td>
                    <td>${l.quantity}</td>
                    <td>${formatPrice(l.unitPrice)}</td>
                    <td>${formatPrice(l.quantity * l.unitPrice)}</td>
                    <td><button class="btn small danger" onclick="removePoLine(${idx})">Xóa</button></td>
                </tr>
            `).join("")
            : `<tr><td colspan="8" class="empty">Chưa có dòng nào trong phiếu.</td></tr>`;

        const totalQty = purchaseDraft.reduce((s, l) => s + l.quantity, 0);
        const totalAmt = purchaseDraft.reduce((s, l) => s + l.quantity * l.unitPrice, 0);
        document.getElementById("poTotalQty").textContent = totalQty;
        document.getElementById("poTotalAmt").textContent = formatPrice(totalAmt);
    }

    function addPoLine() {
        const medId = +document.getElementById("poMed").value;
        const lot = document.getElementById("poLot").value.trim();
        const mfg = document.getElementById("poMfg").value;
        const exp = document.getElementById("poExp").value;
        const qty = +document.getElementById("poQty").value;
        const price = +document.getElementById("poPrice").value;

        if (!medId || !lot || !mfg || !exp || qty <= 0 || price < 0) {
            showToast("Vui lòng nhập đầy đủ thông tin lô", "error");
            return;
        }
        if (new Date(mfg) >= new Date(exp)) {
            showToast("Ngày sản xuất phải trước hạn sử dụng", "error");
            return;
        }

        const med = nhapKhoProducts.find(m => m.id === medId);
        if (!med) {
            showToast("Không tìm thấy thuốc", "error");
            return;
        }

        purchaseDraft.push({
            medId,
            medName: med.name,
            lot,
            mfgDate: mfg,
            expDate: exp,
            quantity: qty,
            unitPrice: price
        });

        ["poLot", "poQty", "poPrice"].forEach(id => {
            const el = document.getElementById(id);
            if (el) el.value = id === "poQty" ? "1" : "";
        });
        render();
    }

    window.removePoLine = (idx) => {
        purchaseDraft.splice(idx, 1);
        render();
    };

    async function savePo() {
        if (!purchaseDraft.length) {
            showToast("Phiếu trống", "error");
            return;
        }

        const supplierId = Number(document.getElementById("poSupplier").value);
        if (!supplierId) {
            showToast("Vui lòng chọn nhà cung cấp", "error");
            return;
        }

        const body = {
            supplierId,
            note: document.getElementById("poNote").value.trim(),
            items: purchaseDraft.map(l => ({
                productId: l.medId,
                lotNumber: l.lot,
                quantity: l.quantity,
                mfgDate: l.mfgDate,
                expDate: l.expDate,
                importPrice: l.unitPrice
            }))
        };

        try {
            const result = await apiPost(API.PURCHASE_IMPORT, body);
            showToast(result && result.message ? result.message : "Đã lưu phiếu nhập kho");
            purchaseDraft = [];
            document.getElementById("poNote").value = "";
            render();
            // Cập nhật lại dropdown (tồn mới)
            await loadProductsForSelect();
            await loadHistory();
        } catch (err) {
            showToast(err.message || "Lỗi lưu phiếu nhập", "error");
        }
    }
}

/* ---------- Users (đã chuyển sang API UsersController) ---------- */
function initUsersPage() {
    const tbody = document.getElementById("userBody");
    if (!tbody) return;

    let usersCache = [];

    async function loadAndRender() {
        tbody.innerHTML = `<tr><td colspan="7" class="empty">Đang tải...</td></tr>`;
        try {
            const list = await apiGet(API.USERS);
            usersCache = Array.isArray(list) ? list : [];
            render(usersCache);
        } catch (err) {
            tbody.innerHTML = `<tr><td colspan="7" class="empty">Lỗi: ${escapeHtml(err.message || "Không xác định")}</td></tr>`;
            showToast(err.message || "Không tải được danh sách người dùng", "error");
        }
    }

    function render(list) {
        if (!list || !list.length) {
            tbody.innerHTML = `<tr><td colspan="7" class="empty">Chưa có người dùng.</td></tr>`;
            return;
        }
        tbody.innerHTML = list.map(u => {
            const role = (u.role || "").toLowerCase();
            const status = (u.status || "").toLowerCase();
            const isActive = status === "active";
            return `
            <tr>
                <td>${u.id}</td>
                <td><b>${escapeHtml(u.fullName || "")}</b></td>
                <td>${escapeHtml(u.phone || "")}</td>
                <td>${escapeHtml(u.email || "")}</td>
                <td>
                    <span class="badge ${role === "admin" ? "danger" :
                    role === "pharmacist" ? "warn" : "info"}">${escapeHtml(role)}</span>
                </td>
                <td>
                    <span class="badge ${isActive ? "success" : "muted"}">
                        ${isActive ? "Hoạt động" : "Đã khóa"}
                    </span>
                </td>
                <td>
                    <div class="actions">
                        <button class="btn small ghost" onclick="editUser(${u.id})">Sửa</button>
                        <button class="btn small muted" onclick="toggleUser(${u.id})">
                            ${isActive ? "Khóa" : "Mở"}
                        </button>
                    </div>
                </td>
            </tr>`;
        }).join("");
    }

    window.openUserForm = (u = null) => {
        const role = u ? (u.role || "customer").toLowerCase() : "customer";
        const status = u ? (u.status || "Active") : "Active";
        const isActive = status.toLowerCase() === "active";

        openModal(`
            <h3>${u ? "Sửa" : "Tạo"} tài khoản</h3>
            <div class="form-grid">
                <div class="form-field full"><label>Họ tên</label>
                    <input id="uName" value="${u ? escapeHtml(u.fullName || "") : ""}">
                </div>
                <div class="form-field"><label>SĐT</label>
                    <input id="uPhone" value="${u ? escapeHtml(u.phone || "") : ""}">
                </div>
                <div class="form-field"><label>Email</label>
                    <input id="uEmail" value="${u ? escapeHtml(u.email || "") : ""}" ${u ? "readonly" : ""}>
                </div>
                <div class="form-field"><label>Mật khẩu ${u ? "(để trống = giữ nguyên)" : ""}</label>
                    <input id="uPassword" type="password" placeholder="${u ? "Để trống nếu không đổi" : "Tối thiểu 12 ký tự, gồm chữ và số"}">
                </div>
                <div class="form-field"><label>Vai trò</label>
                    <select id="uRole">
                        <option value="customer"   ${role === "customer" ? "selected" : ""}>Customer</option>
                        <option value="pharmacist" ${role === "pharmacist" ? "selected" : ""}>Pharmacist</option>
                        <option value="admin"      ${role === "admin" ? "selected" : ""}>Admin</option>
                    </select>
                </div>
                <div class="form-field"><label>Trạng thái</label>
                    <select id="uStatus">
                        <option value="Active" ${isActive ? "selected" : ""}>Hoạt động</option>
                        <option value="Locked" ${!isActive ? "selected" : ""}>Đã khóa</option>
                    </select>
                </div>
            </div>
        `, [{ label: u ? "Cập nhật" : "Tạo", onclick: `saveUser(${u ? u.id : "null"})` }]);
    };

    window.saveUser = async (id) => {
        const fullName = document.getElementById("uName").value.trim();
        if (!fullName) { showToast("Họ tên bắt buộc", "error"); return; }

        const email = document.getElementById("uEmail").value.trim();
        if (!email) { showToast("Email bắt buộc", "error"); return; }

        const password = document.getElementById("uPassword").value;
        const body = {
            fullName,
            phone: document.getElementById("uPhone").value.trim(),
            email,
            role: document.getElementById("uRole").value,
            status: document.getElementById("uStatus").value
        };
        if (password) body.password = password;

        try {
            if (id) {
                await apiPut(`${API.USERS}/${id}`, body);
                showToast("Đã cập nhật tài khoản");
            } else {
                if (!password) {
                    showToast("Hãy đặt mật khẩu tối thiểu 12 ký tự gồm chữ và số", "error");
                    return;
                }
                await apiPost(API.USERS, body);
                showToast("Đã tạo tài khoản");
            }
            closeModal();
            await loadAndRender();
        } catch (err) {
            showToast(err.message || "Lỗi lưu tài khoản", "error");
        }
    };

    window.editUser = (id) => {
        const u = usersCache.find(x => x.id === id);
        if (u) openUserForm(u);
        else showToast("Không tìm thấy người dùng", "error");
    };

    window.toggleUser = async (id) => {
        try {
            const result = await apiPut(`${API.USERS}/${id}/toggle-status`, {});
            showToast(result && result.message ? result.message : "Đã cập nhật trạng thái");
            await loadAndRender();
        } catch (err) {
            showToast(err.message || "Lỗi cập nhật trạng thái", "error");
        }
    };

    document.getElementById("btnAddUser").onclick = () => openUserForm();
    loadAndRender();
}

/* ---------- Reports (đã chuyển sang API ReportsController) ---------- */
function initReportsPage() {
    const canvas = document.getElementById("revChart");
    if (!canvas) return;

    // Mặc định 7 ngày gần nhất
    const to = new Date();
    const from = new Date();
    from.setDate(to.getDate() - 6);

    const fromEl = document.getElementById("fromDate");
    const toEl = document.getElementById("toDate");
    if (fromEl) fromEl.value = from.toISOString().slice(0, 10);
    if (toEl) toEl.value = to.toISOString().slice(0, 10);

    document.getElementById("btnFilter").onclick = () => loadReports();

    loadReports();
}

async function loadReports() {
    const fromEl = document.getElementById("fromDate");
    const toEl = document.getElementById("toDate");
    const from = fromEl && fromEl.value ? fromEl.value : "";
    const to = toEl && toEl.value ? toEl.value : "";

    let q = "";
    if (from) q += `from=${encodeURIComponent(from)}&`;
    if (to) q += `to=${encodeURIComponent(to)}&`;

    try {
        const [rev, best] = await Promise.all([
            apiGet(`${API.REPORT_REVENUE}?${q}`),
            apiGet(`${API.REPORT_BEST_SELLING}?${q}top=10`)
        ]);

        // KPI
        const kpiEl = document.querySelector(".kpi-inline");
        if (kpiEl) {
            kpiEl.innerHTML = `
                Doanh thu: <b>${formatPrice(rev.totalRevenue || 0)}</b> ·
                Số đơn: <b>${rev.totalOrders || 0}</b> ·
                Sản phẩm bán: <b>${rev.totalItems || 0}</b> ·
                TB/đơn: <b>${formatPrice(rev.avgOrder || 0)}</b>
            `;
        }

        // Biểu đồ cột theo ngày
        const canvas = document.getElementById("revChart");
        const byDay = Array.isArray(rev.byDay) ? rev.byDay : [];
        if (!byDay.length) {
            canvas.innerHTML = `<p class="empty" style="padding:40px;text-align:center">Không có dữ liệu trong khoảng đã chọn.</p>`;
        } else {
            // v hiển thị triệu đồng
            const values = byDay.map(x => Number(x.revenue) / 1_000_000);
            const max = Math.max(...values, 0.01);
            const n = byDay.length;
            canvas.innerHTML = byDay.map((x, i) => {
                const v = Number(x.revenue) / 1_000_000;
                const h = (v / max) * 200;
                const left = (i + 0.5) * (100 / n);
                const label = x.label || "";
                return `<div class="bar" style="height:${h}px;left:calc(${left}% - 13px)">
                            <span>${v >= 1 ? v.toFixed(1) : v.toFixed(2)}tr</span>
                        </div>
                        <div class="x-label" style="left:calc(${left}% - 10px)">${escapeHtml(label)}</div>`;
            }).join("");
        }

        // Top bán chạy
        const bestEl = document.getElementById("bestSelling");
        const bestList = Array.isArray(best) ? best : [];
        if (!bestList.length) {
            bestEl.innerHTML = `<li class="empty">Chưa có dữ liệu bán chạy.</li>`;
        } else {
            bestEl.innerHTML = bestList.map((b, i) => `
                <li>
                    <span><span class="rank">${i + 1}</span>${escapeHtml(b.name || "—")}</span>
                    <b>${b.qty || 0}</b>
                </li>
            `).join("");
        }

        showToast("Đã tải báo cáo");
    } catch (err) {
        showToast(err.message || "Lỗi tải báo cáo", "error");
    }
}

/* ---------- Auto-detect Page Routes ---------- */
document.addEventListener("DOMContentLoaded", () => {
    const p = location.pathname.toLowerCase();
    if (p.includes("/admin/index") || p === "/admin" || p === "/admin/") initAdminDashboard();
    if (p.includes("/admin/medicines")) initMedicinesPage();
    if (p.includes("/admin/categories")) initCategoriesPage();
    if (p.includes("/admin/suppliers")) initSuppliersPage();
    if (p.includes("/admin/nhapkho")) initNhapKhoPage(); // Đã khớp tên với NhapKho.cshtml
    if (p.includes("/admin/users")) initUsersPage();
    if (p.includes("/admin/reports")) initReportsPage();
});
