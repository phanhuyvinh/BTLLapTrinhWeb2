/* chat.js — Tư vấn trực tuyến */
const CHAT_POLL_MS = 4000;

function getCurrentUser() {
    try { return JSON.parse(localStorage.getItem("medcare_user") || "null"); }
    catch { return null; }
}

function escapeHtmlChat(str) {
    if (str == null) return "";
    return String(str)
        .replace(/&/g, "&amp;").replace(/</g, "&lt;")
        .replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

function avatarHtml(url, size, fallbackEmoji) {
    const s = size || 40;
    if (url) {
        return `<img class="conv-avatar-img" src="${escapeHtmlChat(url)}" alt=""
            style="width:${s}px;height:${s}px;border-radius:50%;object-fit:cover"
            onerror="this.style.display='none';this.nextElementSibling.style.display='flex'">
            <span class="conv-avatar" style="width:${s}px;height:${s}px;display:none">${fallbackEmoji || "💊"}</span>`;
    }
    return `<span class="conv-avatar" style="width:${s}px;height:${s}px">${fallbackEmoji || "💊"}</span>`;
}

function renderChatBubble(m, isMine) {
    const time = new Date(m.at).toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
    const textHtml = (m.text || "").trim()
        ? `<div class="chat-text">${escapeHtmlChat(m.text)}</div>` : "";
    const imgHtml = m.imageUrl
        ? `<div class="chat-image" style="margin:6px 0">
               <a href="${escapeHtmlChat(m.imageUrl)}" target="_blank" rel="noopener">
                   <img src="${escapeHtmlChat(m.imageUrl)}" alt=""
                        style="max-width:220px;max-height:220px;border-radius:8px;display:block;object-fit:contain"
                        onerror="this.style.display='none'">
               </a>
           </div>` : "";
    const nameLabel = isMine ? "Bạn" : (m.senderName || "Người gửi");
    return `
        <div class="chat-msg ${isMine ? "mine" : "theirs"}">
            <div class="chat-bubble">
                ${imgHtml}${textHtml}
                <div class="chat-meta">${escapeHtmlChat(nameLabel)} · ${time}</div>
            </div>
        </div>`;
}

async function chatTotalUnreadPharmacistAsync() {
    try {
        const data = await apiGet(API.CHAT_UNREAD);
        return data && data.count != null ? data.count : 0;
    } catch { return 0; }
}
function chatTotalUnreadPharmacist() { return 0; }

/* ========== KHÁCH HÀNG ========== */
function initCustomerChat() {
    const root = document.getElementById("chatRoot");
    if (!root) return;

    const user = getCurrentUser();
    if (!user) {
        root.innerHTML = `<div class="empty">Vui lòng <a href="/Customer/Login">đăng nhập</a> để tư vấn.</div>`;
        return;
    }
    const role = (user.role || "").toLowerCase();
    if (role === "admin" || role === "pharmacist") {
        root.innerHTML = `<div class="empty" style="padding:24px">Trang này dành cho khách hàng.
            <br><a href="/Pharmacist/Consultation">→ Tư vấn (Dược sĩ)</a></div>`;
        return;
    }

    let peerId = null;
    let selectedFile = null;
    let pharmacistsCache = [];
    let convsCache = [];

    const listEl = document.getElementById("chatPharmacistList");
    const wrap = document.getElementById("chatMessages");
    const input = document.getElementById("chatInput");
    const btn = document.getElementById("chatSend");
    const headerEl = document.getElementById("chatHeader");
    const fileInput = document.getElementById("chatImage");
    const preview = document.getElementById("chatImagePreview");

    function clearImageSelection() {
        selectedFile = null;
        if (fileInput) fileInput.value = "";
        if (preview) { preview.style.display = "none"; preview.removeAttribute("src"); }
    }

    if (fileInput) {
        fileInput.addEventListener("change", () => {
            const f = fileInput.files && fileInput.files[0];
            if (!f) { clearImageSelection(); return; }
            const allowed = ["image/jpeg", "image/png", "image/webp", "image/gif"];
            if (!allowed.includes(f.type)) {
                showToast("Chỉ chấp nhận ảnh JPG, PNG, WebP, GIF.", "error");
                clearImageSelection(); return;
            }
            if (f.size > 2 * 1024 * 1024) {
                showToast("Ảnh không quá 2 MB.", "error");
                clearImageSelection(); return;
            }
            selectedFile = f;
            if (preview) { preview.src = URL.createObjectURL(f); preview.style.display = "block"; }
        });
    }

    function renderPharmacistList() {
        if (!listEl) return;
        const map = {};
        pharmacistsCache.forEach(p => { map[p.id] = p; });
        convsCache.forEach(c => {
            if (!map[c.peerId]) {
                map[c.peerId] = {
                    id: c.peerId,
                    fullName: c.peerName,
                    email: c.peerEmail,
                    avatarUrl: c.peerAvatar
                };
            }
        });
        const items = Object.values(map);
        if (!items.length) {
            listEl.innerHTML = `<div class="empty" style="padding:12px">Chưa có dược sĩ.</div>`;
            return;
        }
        listEl.innerHTML = items.map(p => {
            const id = p.id;
            const name = p.fullName || p.FullName || ("DS #" + id);
            const conv = convsCache.find(c => c.peerId === id);
            const active = peerId === id ? "active" : "";
            const unread = conv && conv.unread
                ? `<span class="conv-unread">${conv.unread}</span>` : "";
            const last = conv ? escapeHtmlChat((conv.lastMessage || "").slice(0, 36)) : "Chưa có tin nhắn";
            const av = avatarHtml(p.avatarUrl || p.AvatarUrl, 40, "💊");
            return `
                <div class="chat-conv-item ${active}" data-id="${id}" onclick="window.chatSelectPharmacist(${id})">
                    ${av}
                    <div class="conv-info">
                        <div class="conv-name">${escapeHtmlChat(name)} ${unread}</div>
                        <div class="conv-last">${last}</div>
                    </div>
                </div>`;
        }).join("");
    }

    function updateHeader() {
        if (!headerEl) return;
        const p = pharmacistsCache.find(x => x.id === peerId)
            || convsCache.find(c => c.peerId === peerId);
        if (!p) {
            headerEl.innerHTML = `<div class="peer-name">Chọn dược sĩ để bắt đầu</div>`;
            return;
        }
        const name = p.fullName || p.FullName || p.peerName || ("DS #" + peerId);
        const email = p.email || p.Email || p.peerEmail || "";
        const av = avatarHtml(p.avatarUrl || p.AvatarUrl || p.peerAvatar, 44, "💊");
        headerEl.innerHTML = `${av}<div>
            <div class="peer-name">${escapeHtmlChat(name)}</div>
            <div class="peer-sub">${escapeHtmlChat(email) || "Dược sĩ tư vấn"}</div>
        </div>`;
    }

    async function loadPharmacistsAndConvs() {
        try {
            const [list, convs] = await Promise.all([
                apiGet("/chat/pharmacists"),
                apiGet(API.CHAT_CONVERSATIONS)
            ]);
            pharmacistsCache = Array.isArray(list) ? list : [];
            convsCache = Array.isArray(convs) ? convs : [];
            if (!peerId) {
                if (convsCache.length) peerId = convsCache[0].peerId;
                else if (pharmacistsCache.length) peerId = pharmacistsCache[0].id;
            }
            renderPharmacistList();
            updateHeader();
        } catch (err) {
            showToast(err.message || "Không tải được danh sách dược sĩ", "error");
        }
    }

    async function renderMessages() {
        if (!peerId || !wrap) return;
        try {
            const msgs = await apiGet(`${API.CHAT_MESSAGES}?peerId=${peerId}`);
            const list = Array.isArray(msgs) ? msgs : [];
            wrap.innerHTML = list.length
                ? list.map(m => renderChatBubble(m, !!m.isMine)).join("")
                : `<div class="empty">Bắt đầu trò chuyện với dược sĩ 💬</div>`;
            wrap.scrollTop = wrap.scrollHeight;
            await apiPut(`${API.CHAT_MESSAGES}/read?peerId=${peerId}`, {});
            const conv = convsCache.find(c => c.peerId === peerId);
            if (conv) conv.unread = 0;
            renderPharmacistList();
        } catch (err) { console.warn(err); }
    }

    window.chatSelectPharmacist = async (id) => {
        peerId = id;
        clearImageSelection();
        updateHeader();
        renderPharmacistList();
        await renderMessages();
    };

    async function send() {
        if (!peerId) { showToast("Vui lòng chọn dược sĩ trước.", "error"); return; }
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
            await loadPharmacistsAndConvs();
            await renderMessages();
        } catch (err) {
            showToast(err.message || "Gửi tin thất bại", "error");
        }
    }

    if (btn) btn.addEventListener("click", send);
    if (input) input.addEventListener("keydown", e => {
        if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); send(); }
    });

    (async () => {
        await loadPharmacistsAndConvs();
        if (!peerId) {
            if (wrap) wrap.innerHTML = `<div class="empty">Hiện chưa có dược sĩ trực tuyến.</div>`;
            return;
        }
        await renderMessages();
        setInterval(async () => {
            await loadPharmacistsAndConvs();
            if (peerId) await renderMessages();
        }, CHAT_POLL_MS);
    })();
}

/* ========== DƯỢC SĨ / ADMIN (Consultation) ========== */
function initPharmacistChat() {
    const root = document.getElementById("chatRoot");
    if (!root) return;
    const user = getCurrentUser();
    if (!user || (user.role !== "pharmacist" && user.role !== "admin")) {
        root.innerHTML = `<div class="empty">Bạn không có quyền truy cập.</div>`;
        return;
    }

    const isAdmin = (user.role || "").toLowerCase() === "admin";
    let activePeerId = null;
    let filterPharmacistId = null;
    let convsCache = [];
    let selectedFile = null;

    const listEl = document.getElementById("chatConvList");
    const msgEl = document.getElementById("chatMessages");
    const input = document.getElementById("chatInput");
    const btn = document.getElementById("chatSend");
    const headerEl = document.getElementById("chatHeader");
    const fileInput = document.getElementById("chatImage");
    const preview = document.getElementById("chatImagePreview");
    const filterEl = document.getElementById("chatPharmacistFilter");

    function clearImageSelection() {
        selectedFile = null;
        if (fileInput) fileInput.value = "";
        if (preview) { preview.style.display = "none"; preview.removeAttribute("src"); }
    }

    if (fileInput) {
        fileInput.addEventListener("change", () => {
            const f = fileInput.files && fileInput.files[0];
            if (!f) { clearImageSelection(); return; }
            selectedFile = f;
            if (preview) { preview.src = URL.createObjectURL(f); preview.style.display = "block"; }
        });
    }

    function convQuery() {
        if (isAdmin && filterPharmacistId)
            return `${API.CHAT_CONVERSATIONS}?pharmacistId=${filterPharmacistId}`;
        return API.CHAT_CONVERSATIONS;
    }
    function messagesQuery(peerId) {
        let q = `${API.CHAT_MESSAGES}?peerId=${peerId}`;
        if (isAdmin && filterPharmacistId) q += `&pharmacistId=${filterPharmacistId}`;
        return q;
    }

    async function setupAdminFilter() {
        if (!isAdmin || !filterEl) return;
        try {
            const list = await apiGet("/chat/pharmacists");
            const arr = Array.isArray(list) ? list : [];
            filterEl.innerHTML = `<option value="">— Tất cả dược sĩ —</option>` +
                arr.map(p => `<option value="${p.id}">${escapeHtmlChat(p.fullName || p.FullName)}</option>`).join("");
            filterEl.style.display = "inline-block";
            filterEl.addEventListener("change", async () => {
                filterPharmacistId = filterEl.value ? parseInt(filterEl.value, 10) : null;
                activePeerId = null;
                if (headerEl) headerEl.textContent = "Chọn một cuộc trò chuyện";
                if (msgEl) msgEl.innerHTML = `<div class="empty">Chọn hội thoại bên trái.</div>`;
                await loadConvs();
            });
            // Hỗ trợ ?pharmacistId= từ trang chi tiết Admin
            const params = new URLSearchParams(location.search);
            const qid = params.get("pharmacistId");
            if (qid) {
                filterPharmacistId = parseInt(qid, 10);
                filterEl.value = String(filterPharmacistId);
            }
        } catch (e) { console.warn(e); }
    }

    function renderList() {
        if (!listEl) return;
        if (!convsCache.length) {
            listEl.innerHTML = `<div class="empty" style="padding:12px">Chưa có hội thoại.</div>`;
            return;
        }
        listEl.innerHTML = convsCache.map(c => {
            const active = activePeerId === c.peerId ? "active" : "";
            const unread = c.unread ? `<span class="conv-unread">${c.unread}</span>` : "";
            return `
                <div class="chat-conv-item ${active}" onclick="window.chatSelectConv(${c.peerId})">
                    ${avatarHtml(c.peerAvatar, 40, "👤")}
                    <div class="conv-info">
                        <div class="conv-name">${escapeHtmlChat(c.peerName)} ${unread}</div>
                        <div class="conv-last">${escapeHtmlChat((c.lastMessage || "").slice(0, 40))}</div>
                    </div>
                </div>`;
        }).join("");
    }

    async function loadConvs() {
        try {
            const convs = await apiGet(convQuery());
            convsCache = Array.isArray(convs) ? convs : [];
            renderList();
        } catch (err) {
            if (listEl) listEl.innerHTML = `<div class="empty">${escapeHtmlChat(err.message)}</div>`;
        }
    }

    async function renderMessages() {
        if (!activePeerId) return;
        const conv = convsCache.find(c => c.peerId === activePeerId);
        if (headerEl) {
            headerEl.textContent = conv ? `${conv.peerName} · ${conv.peerEmail || ""}` : ("User #" + activePeerId);
        }
        try {
            const msgs = await apiGet(messagesQuery(activePeerId));
            const list = Array.isArray(msgs) ? msgs : [];
            if (msgEl) {
                msgEl.innerHTML = list.length
                    ? list.map(m => renderChatBubble(m, !!m.isMine)).join("")
                    : `<div class="empty">Chưa có tin nhắn.</div>`;
                msgEl.scrollTop = msgEl.scrollHeight;
            }
            let readUrl = `${API.CHAT_MESSAGES}/read?peerId=${activePeerId}`;
            if (isAdmin && filterPharmacistId) readUrl += `&pharmacistId=${filterPharmacistId}`;
            await apiPut(readUrl, {});
            if (conv) conv.unread = 0;
            renderList();
        } catch (err) {
            if (msgEl) msgEl.innerHTML = `<div class="empty">${escapeHtmlChat(err.message)}</div>`;
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
    if (input) input.addEventListener("keydown", e => {
        if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); send(); }
    });

    (async () => {
        await setupAdminFilter();
        await loadConvs();
        setInterval(async () => {
            await loadConvs();
            if (activePeerId) await renderMessages();
        }, CHAT_POLL_MS);
    })();
}