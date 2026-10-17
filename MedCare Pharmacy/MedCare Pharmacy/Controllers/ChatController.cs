using MedCare_Pharmacy.Models.Data;
using MedCare_Pharmacy.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MedCare_Pharmacy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ChatController(AppDbContext context)
        {
            _context = context;
        }

        private int? CurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }

        private string? CurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value?.ToLower();
        }

        // GET: api/chat/conversations
        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations()
        {
            var me = CurrentUserId();
            if (me == null) return Unauthorized();

            var role = CurrentUserRole() ?? "";
            bool isStaff = role == "admin" || role == "pharmacist";

            List<ChatMessage> messages;
            List<int> peerIds;

            if (isStaff)
            {
                // Nếu là Admin hoặc Dược sĩ: Lấy toàn bộ tin nhắn trong hệ thống
                messages = await _context.ChatMessages
                    .OrderByDescending(m => m.Timestamp)
                    .ToListAsync();

                var staffIds = await _context.Users
                    .Where(u => u.Role.ToLower() == "admin" || u.Role.ToLower() == "pharmacist")
                    .Select(u => u.Id)
                    .ToListAsync();

                // Lọc ra danh sách ID khách hàng đã tham gia nhắn tin
                peerIds = messages
                    .SelectMany(m => new[] { m.SenderId, m.ReceiverId })
                    .Where(id => !staffIds.Contains(id))
                    .Distinct()
                    .ToList();
            }
            else
            {
                // Khách hàng: chỉ tin nhắn của chính mình
                messages = await _context.ChatMessages
                    .Where(m => m.SenderId == me || m.ReceiverId == me)
                    .OrderByDescending(m => m.Timestamp)
                    .ToListAsync();

                peerIds = messages
                    .Select(m => m.SenderId == me ? m.ReceiverId : m.SenderId)
                    .Distinct()
                    .ToList();

                // Chỉ giữ peer là dược sĩ/admin (tránh dính hội thoại không hợp lệ)
                var staffIdSet = await _context.Users
                    .Where(u => u.Role.ToLower() == "admin" || u.Role.ToLower() == "pharmacist")
                    .Select(u => u.Id)
                    .ToListAsync();

                peerIds = peerIds.Where(id => staffIdSet.Contains(id)).ToList();
            }

            var peers = await _context.Users
                .Where(u => peerIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);

            var convs = peerIds.Select(pid =>
            {
                var last = messages.First(m =>
                    m.SenderId == pid || m.ReceiverId == pid);

                var unread = messages.Count(m =>
                    m.SenderId == pid && !m.IsRead);

                peers.TryGetValue(pid, out var peer);

                return new
                {
                    peerId = pid,
                    peerName = peer?.FullName ?? ("User #" + pid),
                    peerEmail = peer?.Email ?? "",
                    peerRole = peer?.Role?.ToLower() ?? "",
                    lastMessage = !string.IsNullOrEmpty(last.Content)
                    ? last.Content
                    : (!string.IsNullOrEmpty(last.ImageUrl) ? "[Ảnh]" : ""),
                    lastUpdate = last.Timestamp,
                    unread
                };
            })
            .OrderByDescending(c => c.lastUpdate)
            .ToList();

            return Ok(convs);
        }

        // GET: api/chat/messages?peerId=5
        [HttpGet("messages")]
        public async Task<IActionResult> GetMessages([FromQuery] int peerId)
        {
            var me = CurrentUserId();
            if (me == null) return Unauthorized();

            var role = CurrentUserRole() ?? "";
            bool isStaff = role == "admin" || role == "pharmacist";

            List<ChatMessage> msgs;
            if (isStaff)
            {
                msgs = await _context.ChatMessages
                    .Where(m => m.SenderId == peerId || m.ReceiverId == peerId)
                    .OrderBy(m => m.Timestamp)
                    .ToListAsync();
            }
            else
            {
                msgs = await _context.ChatMessages
                    .Where(m =>
                        (m.SenderId == me && m.ReceiverId == peerId) ||
                        (m.SenderId == peerId && m.ReceiverId == me))
                    .OrderBy(m => m.Timestamp)
                    .ToListAsync();
            }

            var userIds = msgs.Select(m => m.SenderId).Distinct().ToList();
            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);

            var result = msgs.Select(m => new
            {
                id = m.Id,
                senderId = m.SenderId,
                receiverId = m.ReceiverId,
                text = m.Content,
                imageUrl = m.ImageUrl,
                at = m.Timestamp,
                isRead = m.IsRead,
                senderName = users.ContainsKey(m.SenderId) ? users[m.SenderId].FullName : "",
                isMine = m.SenderId == me
            });

            return Ok(result);
        }
        // POST: api/chat/messages  (FormData: receiverId, content?, image?)
        [HttpPost("messages")]
        [RequestSizeLimit(3 * 1024 * 1024)] // tối đa ~3MB request
        public async Task<IActionResult> Send(
            [FromForm] int receiverId,
            [FromForm] string? content,
            [FromForm] IFormFile? image)
        {
            var me = CurrentUserId();
            if (me == null) return Unauthorized();

            if (receiverId <= 0)
                return BadRequest(new { message = "Thiếu người nhận." });

            var receiver = await _context.Users.FindAsync(receiverId);
            if (receiver == null)
                return BadRequest(new { message = "Không tìm thấy người nhận." });

            var text = (content ?? "").Trim();
            string? imageUrl = null;

            if (image != null && image.Length > 0)
            {
                var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
                var ext = Path.GetExtension(image.FileName).ToLowerInvariant();
                if (string.IsNullOrEmpty(ext) || !allowedExt.Contains(ext))
                    return BadRequest(new { message = "Chỉ chấp nhận ảnh JPG, JPEG, PNG, WebP, GIF." });

                if (image.Length > 2 * 1024 * 1024)
                    return BadRequest(new { message = "Ảnh không được vượt quá 2 MB." });

                // Chặn MIME không phải ảnh
                var contentType = (image.ContentType ?? "").ToLowerInvariant();
                if (!contentType.StartsWith("image/"))
                    return BadRequest(new { message = "Tệp không phải là ảnh hợp lệ." });

                var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "chat");
                Directory.CreateDirectory(folder);

                var fileName = $"{Guid.NewGuid():N}{ext}";
                var fullPath = Path.Combine(folder, fileName);
                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }
                imageUrl = $"/uploads/chat/{fileName}";
            }

            if (string.IsNullOrEmpty(text) && string.IsNullOrEmpty(imageUrl))
                return BadRequest(new { message = "Vui lòng nhập nội dung hoặc chọn ảnh." });

            var msg = new ChatMessage
            {
                SenderId = me.Value,
                ReceiverId = receiverId,
                Content = text,
                ImageUrl = imageUrl,
                Timestamp = DateTime.Now,
                IsRead = false
            };

            _context.ChatMessages.Add(msg);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                id = msg.Id,
                senderId = msg.SenderId,
                receiverId = msg.ReceiverId,
                text = msg.Content,
                imageUrl = msg.ImageUrl,
                at = msg.Timestamp
            });
        }
        // PUT: api/chat/messages/read?peerId=5
        [HttpPut("messages/read")]
        public async Task<IActionResult> MarkRead([FromQuery] int peerId)
        {
            var me = CurrentUserId();
            if (me == null) return Unauthorized();

            var role = CurrentUserRole() ?? "";
            bool isStaff = role == "admin" || role == "pharmacist";

            List<ChatMessage> unread;
            if (isStaff)
            {
                // Nhân viên đánh dấu tất cả tin nhắn gửi đến từ khách hàng peerId là đã đọc
                unread = await _context.ChatMessages
                    .Where(m => m.SenderId == peerId && !m.IsRead)
                    .ToListAsync();
            }
            else
            {
                unread = await _context.ChatMessages
                    .Where(m => m.SenderId == peerId && m.ReceiverId == me && !m.IsRead)
                    .ToListAsync();
            }

            foreach (var m in unread) m.IsRead = true;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, marked = unread.Count });
        }

        // GET: api/chat/unread
        [HttpGet("unread")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var me = CurrentUserId();
            if (me == null) return Unauthorized();

            var role = CurrentUserRole() ?? "";
            bool isStaff = role == "admin" || role == "pharmacist";

            int count;
            if (isStaff)
            {
                var staffIds = await _context.Users
                    .Where(u => u.Role.ToLower() == "admin" || u.Role.ToLower() == "pharmacist")
                    .Select(u => u.Id)
                    .ToListAsync();

                // Đếm tổng số tin nhắn chưa đọc từ khách hàng gửi tới hệ thống
                count = await _context.ChatMessages
                    .CountAsync(m => !m.IsRead && !staffIds.Contains(m.SenderId));
            }
            else
            {
                count = await _context.ChatMessages
                    .CountAsync(m => m.ReceiverId == me && !m.IsRead);
            }

            return Ok(new { count });
        }

        // GET: api/chat/pharmacists  (khách lấy dược sĩ để bắt đầu chat)
        [HttpGet("pharmacists")]
        public async Task<IActionResult> GetPharmacists()
        {
            var list = await _context.Users
                .Where(u => u.Role.ToLower() == "pharmacist" && u.Status.ToLower() == "active")
                .Select(u => new { u.Id, u.FullName, u.Email })
                .ToListAsync();
            return Ok(list);
        }
    }

    //public record SendChatDto(int ReceiverId, string Content);
}
