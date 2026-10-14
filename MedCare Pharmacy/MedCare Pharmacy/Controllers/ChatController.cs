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

        // GET: api/chat/conversations?pharmacistId=2
        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations([FromQuery] int? pharmacistId = null)
        {
            var me = CurrentUserId();
            if (me == null) return Unauthorized();

            var role = CurrentUserRole() ?? "";
            List<ChatMessage> messages;
            List<int> peerIds;

            if (role == "pharmacist")
            {
                messages = await _context.ChatMessages
                    .Where(m => m.SenderId == me || m.ReceiverId == me)
                    .OrderByDescending(m => m.Timestamp)
                    .ToListAsync();

                var staffIds = await _context.Users
                    .Where(u => u.Role.ToLower() == "admin" || u.Role.ToLower() == "pharmacist")
                    .Select(u => u.Id)
                    .ToListAsync();

                peerIds = messages
                    .SelectMany(m => new[] { m.SenderId, m.ReceiverId })
                    .Where(id => id != me.Value && !staffIds.Contains(id))
                    .Distinct()
                    .ToList();
            }
            else if (role == "admin")
            {
                var staffIds = await _context.Users
                    .Where(u => u.Role.ToLower() == "admin" || u.Role.ToLower() == "pharmacist")
                    .Select(u => u.Id)
                    .ToListAsync();

                if (pharmacistId.HasValue && pharmacistId.Value > 0)
                {
                    var pid = pharmacistId.Value;
                    messages = await _context.ChatMessages
                        .Where(m => m.SenderId == pid || m.ReceiverId == pid)
                        .OrderByDescending(m => m.Timestamp)
                        .ToListAsync();

                    peerIds = messages
                        .SelectMany(m => new[] { m.SenderId, m.ReceiverId })
                        .Where(id => id != pid && !staffIds.Contains(id))
                        .Distinct()
                        .ToList();
                }
                else
                {
                    messages = await _context.ChatMessages
                        .OrderByDescending(m => m.Timestamp)
                        .ToListAsync();

                    peerIds = messages
                        .SelectMany(m => new[] { m.SenderId, m.ReceiverId })
                        .Where(id => !staffIds.Contains(id))
                        .Distinct()
                        .ToList();
                }
            }
            else
            {
                messages = await _context.ChatMessages
                    .Where(m => m.SenderId == me || m.ReceiverId == me)
                    .OrderByDescending(m => m.Timestamp)
                    .ToListAsync();

                peerIds = messages
                    .Select(m => m.SenderId == me ? m.ReceiverId : m.SenderId)
                    .Distinct()
                    .ToList();

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
                IEnumerable<ChatMessage> thread;
                if (role == "pharmacist")
                {
                    thread = messages.Where(m =>
                        (m.SenderId == me && m.ReceiverId == pid) ||
                        (m.SenderId == pid && m.ReceiverId == me));
                }
                else if (role == "admin" && pharmacistId.HasValue && pharmacistId.Value > 0)
                {
                    var sid = pharmacistId.Value;
                    thread = messages.Where(m =>
                        (m.SenderId == sid && m.ReceiverId == pid) ||
                        (m.SenderId == pid && m.ReceiverId == sid));
                }
                else
                {
                    thread = messages.Where(m => m.SenderId == pid || m.ReceiverId == pid);
                }

                var last = thread.OrderByDescending(m => m.Timestamp).FirstOrDefault();
                if (last == null) return null!;

                int unread;
                if (role == "pharmacist")
                    unread = thread.Count(m => m.SenderId == pid && m.ReceiverId == me && !m.IsRead);
                else if (role == "admin" && pharmacistId.HasValue && pharmacistId.Value > 0)
                    unread = thread.Count(m => m.SenderId == pid && m.ReceiverId == pharmacistId.Value && !m.IsRead);
                else
                    unread = thread.Count(m => m.SenderId == pid && !m.IsRead);

                peers.TryGetValue(pid, out var peer);
                return new
                {
                    peerId = pid,
                    peerName = peer?.FullName ?? ("User #" + pid),
                    peerEmail = peer?.Email ?? "",
                    peerRole = peer?.Role?.ToLower() ?? "",
                    peerAvatar = peer?.AvatarUrl,
                    lastMessage = !string.IsNullOrEmpty(last.Content)
                        ? last.Content
                        : (!string.IsNullOrEmpty(last.ImageUrl) ? "[Ảnh]" : ""),
                    lastUpdate = last.Timestamp,
                    unread
                };
            })
            .Where(c => c != null)
            .OrderByDescending(c => c!.lastUpdate)
            .ToList();

            return Ok(convs);
        }

        // GET: api/chat/messages?peerId=5&pharmacistId=2
        [HttpGet("messages")]
        public async Task<IActionResult> GetMessages([FromQuery] int peerId, [FromQuery] int? pharmacistId = null)
        {
            var me = CurrentUserId();
            if (me == null) return Unauthorized();
            if (peerId <= 0) return BadRequest(new { message = "Thiếu peerId." });

            var role = CurrentUserRole() ?? "";
            List<ChatMessage> msgs;

            if (role == "pharmacist")
            {
                msgs = await _context.ChatMessages
                    .Where(m =>
                        (m.SenderId == me && m.ReceiverId == peerId) ||
                        (m.SenderId == peerId && m.ReceiverId == me))
                    .OrderBy(m => m.Timestamp)
                    .ToListAsync();
            }
            else if (role == "admin" && pharmacistId.HasValue && pharmacistId.Value > 0)
            {
                var sid = pharmacistId.Value;
                msgs = await _context.ChatMessages
                    .Where(m =>
                        (m.SenderId == sid && m.ReceiverId == peerId) ||
                        (m.SenderId == peerId && m.ReceiverId == sid))
                    .OrderBy(m => m.Timestamp)
                    .ToListAsync();
            }
            else if (role == "admin")
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

            var userIds = msgs.SelectMany(m => new[] { m.SenderId, m.ReceiverId }).Distinct().ToList();
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
                senderAvatar = users.ContainsKey(m.SenderId) ? users[m.SenderId].AvatarUrl : null,
                isMine = m.SenderId == me
            });

            return Ok(result);
        }

        // POST: api/chat/messages
        [HttpPost("messages")]
        [RequestSizeLimit(3 * 1024 * 1024)]
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

            var sender = await _context.Users.FindAsync(me.Value);

            return Ok(new
            {
                success = true,
                id = msg.Id,
                senderId = msg.SenderId,
                receiverId = msg.ReceiverId,
                text = msg.Content,
                imageUrl = msg.ImageUrl,
                at = msg.Timestamp,
                senderName = sender?.FullName ?? "",
                isMine = true
            });
        }

        // PUT: api/chat/messages/read?peerId=5&pharmacistId=2
        [HttpPut("messages/read")]
        public async Task<IActionResult> MarkRead([FromQuery] int peerId, [FromQuery] int? pharmacistId = null)
        {
            var me = CurrentUserId();
            if (me == null) return Unauthorized();

            var role = CurrentUserRole() ?? "";
            List<ChatMessage> unread;

            if (role == "pharmacist")
            {
                unread = await _context.ChatMessages
                    .Where(m => m.SenderId == peerId && m.ReceiverId == me && !m.IsRead)
                    .ToListAsync();
            }
            else if (role == "admin" && pharmacistId.HasValue && pharmacistId.Value > 0)
            {
                var sid = pharmacistId.Value;
                unread = await _context.ChatMessages
                    .Where(m => m.SenderId == peerId && m.ReceiverId == sid && !m.IsRead)
                    .ToListAsync();
            }
            else if (role == "admin")
            {
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
            int count;

            if (role == "pharmacist")
            {
                count = await _context.ChatMessages
                    .CountAsync(m => m.ReceiverId == me && !m.IsRead);
            }
            else if (role == "admin")
            {
                var staffIds = await _context.Users
                    .Where(u => u.Role.ToLower() == "admin" || u.Role.ToLower() == "pharmacist")
                    .Select(u => u.Id)
                    .ToListAsync();

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

        // GET: api/chat/pharmacists
        [HttpGet("pharmacists")]
        public async Task<IActionResult> GetPharmacists()
        {
            var list = await _context.Users
                .Where(u => u.Role.ToLower() == "pharmacist" && u.Status.ToLower() == "active")
                .OrderBy(u => u.FullName)
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.Email,
                    u.Phone,
                    avatarUrl = u.AvatarUrl
                })
                .ToListAsync();
            return Ok(list);
        }
    }
}