/* ============================================================
   api.js — Trung tâm giao tiếp với backend ASP.NET Core Web API
   Tất cả module khác (auth.js, cart.js, customer.js, pharmacy.js,
   admin.js) đều gọi qua các hàm dưới đây, không hard-code URL.
   ============================================================ */

const API_BASE_URL = "/api";

/* ------- Lấy token đăng nhập (nếu có) để gắn vào header ------- */
function getAuthToken() {
    return localStorage.getItem("medcare_token") || "";
}

/* ------------------ Hàm tiện ích chung ------------------ */
async function apiRequest(method, endpoint, body = null, options = {}) {
    const headers = Object.assign(
        { "Content-Type": "application/json" },
        options.headers || {}
    );

    const token = getAuthToken();
    if (token) headers["Authorization"] = "Bearer " + token;

    const config = {
        method,
        headers,
        mode: "cors",
    };

    // Nếu body là FormData (upload file) thì bỏ Content-Type để browser tự set
    if (body instanceof FormData) {
        delete headers["Content-Type"];
        config.body = body;
    } else if (body !== null && body !== undefined) {
        config.body = JSON.stringify(body);
    }

    const res = await fetch(`${API_BASE_URL}${endpoint}`, config);

    if (!res.ok) {
        let msg = `API ${method} ${endpoint} lỗi (${res.status})`;
        try {
            const data = await res.json();
            if (data && data.message) msg = data.message;
        } catch (_) { /* ignore */ }
        const error = new Error(msg);
        error.status = res.status;
        throw error;
    }

    if (res.status === 204) return null;
    const ct = res.headers.get("content-type") || "";
    if (ct.includes("application/json")) return res.json();
    return res.text();
}

async function openProtectedFile(endpoint) {
    const popup = window.open("about:blank", "_blank");
    if (!popup) {
        showToast("Cho phép cửa sổ bật lên để xem tệp.", "error");
        return;
    }
    try {
        const url = new URL(endpoint, window.location.origin);
        if (url.origin !== window.location.origin) throw new Error("Địa chỉ tệp không hợp lệ.");
        const response = await fetch(url, {
            headers: { Authorization: "Bearer " + getAuthToken() }
        });
        if (!response.ok) throw new Error("Không thể tải tệp (" + response.status + ").");
        const objectUrl = URL.createObjectURL(await response.blob());
        popup.location.href = objectUrl;
        setTimeout(() => URL.revokeObjectURL(objectUrl), 60000);
    } catch (error) {
        popup.close();
        showToast(error.message || "Không thể mở tệp.", "error");
    }
}

/* ---------------- CRUD wrapper ---------------- */
const apiGet    = (endpoint, opts)        => apiRequest("GET",    endpoint, null, opts);
const apiPost   = (endpoint, body, opts)  => apiRequest("POST",   endpoint, body, opts);
const apiPut    = (endpoint, body, opts)  => apiRequest("PUT",    endpoint, body, opts);
const apiDelete = (endpoint, opts)        => apiRequest("DELETE", endpoint, null, opts);

/* ---------------- Endpoint groups (gợi ý sẵn) ----------------
   Dùng khi backend sẵn sàng:
   apiGet(API.PRODUCTS)                     // GET /api/products
   apiPost(API.AUTH_LOGIN, { email, password })
   ...
--------------------------------------------------------------- */
const API = {
    // auth
    AUTH_LOGIN:    "/auth/login",
    AUTH_REGISTER: "/auth/register",
    AUTH_LOGOUT: "/auth/logout",

    // catalog
    PRODUCTS:      "/products",
    CATEGORIES:    "/categories",
    SUPPLIERS:     "/suppliers",

    // cart
    CART:          "/cart",
    CART_ITEMS:    "/cart/items",

    // orders
    ORDERS:        "/orders",

    // inventory
    INV_ALERTS: "/inventorybatches/alerts",
    INVENTORY_BATCHES: "/inventorybatches",
    PURCHASE_IMPORT: "/inventorybatches/import",
    INVENTORY_MOVEMENTS: "/inventorybatches/movements",
    PURCHASE_RECEIPTS: "/inventorybatches/receipts",

    // pos
    POS_INVOICES:  "/pos/invoices",

    // users
    USERS:         "/users",

    // reports
    REPORT_REVENUE: "/reports/revenue",
    REPORT_BEST_SELLING: "/reports/best-selling",
    REPORT_DASHBOARD: "/reports/dashboard",
    REPORT_PHARMA_DASH: "/reports/pharmacist-dashboard",
    // chat
    CHAT_CONVERSATIONS: "/chat/conversations",
    CHAT_MESSAGES: "/chat/messages",
    CHAT_UNREAD: "/chat/unread",
    // prescriptions
    PRESCRIPTIONS: "/prescriptions",
    PRESCRIPTIONS_UPLOAD: "/prescriptions/upload",
};

/* ---------------- Demo helper ----------------
   Trong bản demo chưa có backend, các hàm dưới đây trả dữ liệu
   mẫu. Khi tích hợp backend chỉ cần xoá `DEMO` và dùng api*.
------------------------------------------------- */
const DEMO = {
    /** Mô phỏng gọi API trả về Promise với dữ liệu cho trước. */
    mock(data, delay = 120) {
        return new Promise(resolve => setTimeout(() => resolve(data), delay));
    }
};
