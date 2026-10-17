/* ============================================================
   chat.js — Tư vấn trực tuyến (API ChatController)
   ============================================================ */

const CHAT_POLL_MS = 4000;

function getCurrentUser() {
    try {
        return JSON.parse(localStorage.getItem("medcare_user") || "null");
    } catch {
        return null;
    }
}

function renderChatBubble(m, isMine) {
    const time = new Date(m.at).toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
    const textHtml = (m.text || "").trim()
        ? `<div class="chat-text">${escapeHtml(m.text)}</div>`
        : "";
    const imgHtml = m.imageUrl
        ? `<div class="chat-image" style="margin:6px 0">
               <a href="${escapeHtml(m.imageUrl)}" target="_blank" rel="noopener">
                   <img src="${escapeHtml(m.imageUrl)}" alt="Ảnh đính kèm"
                        style="max-width:220px;max-height:220px;border-radius:8px;display:block;object-fit:contain"
                        onerror="this.style.display='none'">
               </a>
           </div>`
        : "";
    return `
        <div class="chat-msg ${isMine ? "mine" : "theirs"}">
            <div class="chat-bubble">
                ${imgHtml}
                ${textHtml}
                <div class="chat-meta">${escapeHtml(m.senderName || "")} · ${time}</div>
            </div>
        </div>
    `;
}

/* ---------- Số tin chưa đọc (dùng trên Dashboard) ---------- */
async function chatTotalUnreadPharmacistAsync() {
    try {
        const data = await apiGet(API.CHAT_UNREAD);
        return data && data.count != null ? data.count : 0;
    } catch {
        return 0;
    }
}

// Giữ tên cũ (sync) — trả 0; Dashboard dùng API riêng
function chatTotalUnreadPharmacist() {
    return 0;
}

/* ============================================================
   TRANG KHÁCH HÀNG
   ============================================================ */
function initCustomerChat() {
    const root = document.getElementById("chatRoot");
    if (!root) return;

    const user = getCurrentUser();
    if (!user) {
        root.innerHTML = `
            <div class="empty">
                Vui lòng <a href="/Customer/Login">đăng nhập</a> để sử dụng tư vấn trực tuyến.
            </div>`;
        return;
    }
    const role = (user.role || "").toLowerCase();
    if (role === "admin" || role === "pharmacist") {
        root.innerHTML = `
            <div class="empty" style="padding:24px;line-height:1.6">
                Bạn đang đăng nhập với quyền <b>${escapeHtml(role)}</b>.<br>
                Trang này chỉ dành cho <b>khách hàng</b>.<br><br>
                Vui lòng dùng khu vực tư vấn của dược sĩ:<br>
                <a href="/Pharmacist/Consultation" style="color:#0b8f66;font-weight:600">
                    → Mở Tư vấn trực tuyến (Dược sĩ)
                </a>
            </div>`;
        return;
    }
    let peerId = null;
    let selectedFile = null;

    const wrap = document.getElementById("chatMessages");
    const input = document.getElementById("chatInput");
    const btn = document.getElementById("chatSend");
    const fileInput = document.getElementById("chatImage");
    const fileBtn = document.getElementById("chatImageBtn");
    const preview = document.getElementById("chatImagePreview");

    function clearImageSelection() {
        selectedFile = null;
        if (fileInput) fileInput.value = "";
        if (preview) {
            preview.style.display = "none";
            preview.removeAttribute("src");
        }
    }

    if (fileBtn && fileInput) {
        // fileBtn.addEventListener("click", () => fileInput.click());
        fileInput.addEventListener("change", () => {
            const f = fileInput.files && fileInput.files[0];
            if (!f) {
                clearImageSelection();
                return;
            }
            const allowed = ["image/jpeg", "image/png", "image/webp", "image/gif"];
            if (!allowed.includes(f.type)) {
                showToast("Chỉ chấp nhận ảnh JPG, PNG, WebP, GIF.", "error");
                clearImageSelection();
                return;
            }
            if (f.size > 2 * 1024 * 1024) {
                showToast("Ảnh không được vượt quá 2 MB.", "error");
                clearImageSelection();
                return;
            }
            selectedFile = f;
            if (preview) {
                preview.src = URL.createObjectURL(f);
                preview.style.display = "block";
            }
        });
    }

    async function ensurePharmacist() {
        try {
            const convs = await apiGet(API.CHAT_CONVERSATIONS);
            if (Array.isArray(convs) && convs.length) {
                peerId = convs[0].peerId;
                return;
            }
            const list = await apiGet("/chat/pharmacists");
            if (Array.isArray(list) && list.length) {
                peerId = list[0].id;
            }
        } catch (err) {
            showToast(err.message || "Không tìm được dược sĩ", "error");
        }
    }

    async function render() {
        if (!peerId || !wrap) return;
        try {
            const msgs = await apiGet(`${API.CHAT_MESSAGES}?peerId=${peerId}`);
            const list = Array.isArray(msgs) ? msgs : [];
            wrap.innerHTML = list.length
                ? list.map(m => renderChatBubble(m, !!m.isMine)).join("")
                : `<div class="empty">Bắt đầu cuộc trò chuyện với dược sĩ 💬</div>`;
            wrap.scrollTop = wrap.scrollHeight;
            await apiPut(`${API.CHAT_MESSAGES}/read?peerId=${peerId}`, {});
        } catch (err) {
            console.warn(err);
        }
    }

    async function send() {
        if (!peerId) return;
        const t = (input && input.value || "").trim();
        if (!t && !selectedFile) return;

        const form = new FormData();
        form.append("receiverId", String(peerId));
        if (t) form.append("content", t);
        if (selectedFile) form.append("image", selectedFile);

        try {
            await apiPost(API.CHAT_MESSAGES, form);
            if (input) input.value = "";
            clearImageSelection();
            await render();
        } catch (err) {
            showToast(err.message || "Gửi tin thất bại", "error");
        }
    }

    (async () => {
        await ensurePharmacist();
        if (!peerId) {
            if (wrap) wrap.innerHTML = `<div class="empty">Hiện chưa có dược sĩ trực tuyến.</div>`;
            return;
        }
        if (btn) btn.addEventListener("click", send);
        if (input) input.addEventListener("keydown", e => {
            if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); send(); }
        });
        await render();
        setInterval(render, CHAT_POLL_MS);
    })();
}

/* ============================================================
   TRANG DƯỢC SĨ
   ============================================================ */
function initPharmacistChat() {
    const root = document.getElementById("chatRoot");
    if (!root) return;

    const user = getCurrentUser();
    if (!user || (user.role !== "pharmacist" && user.role !== "admin")) {
        root.innerHTML = `<div class="empty">Bạn không có quyền truy cập tư vấn trực tuyến.</div>`;
        return;
    }

    let activePeerId = null;
    let convsCache = [];
    let selectedFile = null;

    const listEl = document.getElementById("chatConvList");
    const msgEl = document.getElementById("chatMessages");
    const input = document.getElementById("chatInput");
    const btn = document.getElementById("chatSend");
    const headerEl = document.getElementById("chatHeader");
    const fileInput = document.getElementById("chatImage");
    const fileBtn = document.getElementById("chatImageBtn");
    const preview = document.getElementById("chatImagePreview");

    function clearImageSelection() {
        selectedFile = null;
        if (fileInput) fileInput.value = "";
        if (preview) {
            preview.style.display = "none";
            preview.removeAttribute("src");
        }
    }

    if (fileInput) {
        fileInput.addEventListener("change", () => {
            const f = fileInput.files && fileInput.files[0];
            if (!f) {
                clearImageSelection();
                return;
            }
            const allowed = ["image/jpeg", "image/png", "image/webp", "image/gif"];
            if (!allowed.includes(f.type)) {
                showToast("Chỉ chấp nhận ảnh JPG, PNG, WebP, GIF.", "error");
                clearImageSelection();
                return;
            }
            if (f.size > 2 * 1024 * 1024) {
                showToast("Ảnh không được vượt quá 2 MB.", "error");
                clearImageSelection();
                return;
            }
            selectedFile = f;
            if (preview) {
                preview.src = URL.createObjectURL(f);
                preview.style.display = "block";
            }
        });
    }

    async function loadConvs() {
        try {
            const convs = await apiGet(API.CHAT_CONVERSATIONS);
            convsCache = Array.isArray(convs) ? convs : [];
            renderList();
        } catch (err) {
            if (listEl) {
                listEl.innerHTML = `<div class="empty" style="padding:20px 10px">${escapeHtml(err.message || "Lỗi tải hội thoại")}</div>`;
            }
        }
    }

    function renderList() {
        if (!listEl) return;
        if (!convsCache.length) {
            listEl.innerHTML = `<div class="empty" style="padding:20px 10px">Chưa có cuộc trò chuyện nào.</div>`;
            return;
        }
        listEl.innerHTML = convsCache.map(c => `
            <div class="conv-item ${c.peerId === activePeerId ? "active" : ""}"
                 onclick="chatSelectConv(${c.peerId})">
                <div class="conv-avatar">👤</div>
                <div class="conv-body">
                    <div class="conv-name">${escapeHtml(c.peerName || "")}</div>
                    <div class="conv-last">${escapeHtml(c.lastMessage || "—")}</div>
                </div>
                ${c.unread ? `<span class="conv-badge">${c.unread}</span>` : ""}
            </div>
        `).join("");
    }

    async function renderMessages() {
        if (!activePeerId) {
            if (headerEl) headerEl.textContent = "Chọn một cuộc trò chuyện";
            if (msgEl) msgEl.innerHTML = `<div class="empty">Chọn cuộc trò chuyện bên trái để xem tin nhắn.</div>`;
            return;
        }
        const conv = convsCache.find(c => c.peerId === activePeerId);
        if (headerEl) {
            headerEl.textContent = conv
                ? `${conv.peerName} · ${conv.peerEmail || ""}`
                : ("User #" + activePeerId);
        }
        try {
            const msgs = await apiGet(`${API.CHAT_MESSAGES}?peerId=${activePeerId}`);
            const list = Array.isArray(msgs) ? msgs : [];
            if (msgEl) {
                msgEl.innerHTML = list.length
                    ? list.map(m => renderChatBubble(m, !!m.isMine)).join("")
                    : `<div class="empty">Chưa có tin nhắn.</div>`;
                msgEl.scrollTop = msgEl.scrollHeight;
            }
            await apiPut(`${API.CHAT_MESSAGES}/read?peerId=${activePeerId}`, {});
            if (conv) conv.unread = 0;
            renderList();
        } catch (err) {
            if (msgEl) msgEl.innerHTML = `<div class="empty">${escapeHtml(err.message || "Lỗi")}</div>`;
        }
    }

    window.chatSelectConv = (peerId) => {
        activePeerId = peerId;
        renderList();
        renderMessages();
    };

    async function send() {
        if (!activePeerId) return;
        const t = (input && input.value || "").trim();
        if (!t && !selectedFile) return;

        const form = new FormData();
        form.append("receiverId", String(activePeerId));
        if (t) form.append("content", t);
        if (selectedFile) form.append("image", selectedFile);

        try {
            await apiPost(API.CHAT_MESSAGES, form);
            if (input) input.value = "";
            clearImageSelection();
            await loadConvs();
            await renderMessages();
        } catch (err) {
            showToast(err.message || "Gửi tin thất bại", "error");
        }
    }

    if (btn) btn.addEventListener("click", send);
    if (input) {
        input.addEventListener("keydown", e => {
            if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                send();
            }
        });
    }

    loadConvs();
    setInterval(async () => {
        await loadConvs();
        if (activePeerId) await renderMessages();
    }, CHAT_POLL_MS);
}
