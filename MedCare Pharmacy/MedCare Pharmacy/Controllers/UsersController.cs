using MedCare_Pharmacy.Models.Data;
using MedCare_Pharmacy.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedCare_Pharmacy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/users
        [HttpGet]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetAll()
        {
            var users = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.Phone,
                    u.Email,
                    role = u.Role.ToLower(),
                    status = u.Status,
                    u.CreatedAt
                })
                .ToListAsync();

            return Ok(users);
        }

        // GET: api/users/5
        [HttpGet("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetById(int id)
        {
            var u = await _context.Users.FindAsync(id);
            if (u == null) return NotFound(new { message = "Không tìm thấy người dùng" });

            return Ok(new
            {
                u.Id,
                u.FullName,
                u.Phone,
                u.Email,
                role = u.Role.ToLower(),
                status = u.Status,
                u.CreatedAt
            });
        }

        // POST: api/users  (Admin tạo tài khoản)
        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 12 || dto.Password.Length > 128 ||
                !dto.Password.Any(char.IsLetter) || !dto.Password.Any(char.IsDigit))
            {
                return BadRequest(new { message = "Cần họ tên, email và mật khẩu tối thiểu 12 ký tự gồm chữ và số." });
            }

            var email = dto.Email.Trim().ToLowerInvariant();
            var role = (dto.Role ?? "customer").Trim().ToLowerInvariant();
            if (role is not ("admin" or "pharmacist" or "customer"))
                return BadRequest(new { message = "Vai trò không hợp lệ." });
            if (dto.FullName.Trim().Length > 150 || email.Length > 150 || (dto.Phone?.Length ?? 0) > 20 ||
                !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
                return BadRequest(new { message = "Thông tin tài khoản không hợp lệ hoặc vượt giới hạn độ dài." });

            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                return BadRequest(new { message = "Email đã được sử dụng" });
            }

            var user = new User
            {
                FullName = dto.FullName.Trim(),
                Phone = dto.Phone?.Trim() ?? "",
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = role,
                Status = string.IsNullOrWhiteSpace(dto.Status) ? "Active" : NormalizeStatus(dto.Status),
                CreatedAt = DateTime.Now
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Đã tạo tài khoản",
                user = new
                {
                    user.Id,
                    user.FullName,
                    user.Phone,
                    user.Email,
                    role = user.Role,
                    status = user.Status
                }
            });
        }

        // PUT: api/users/5
        [HttpPut("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDto dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng" });

            var newRole = string.IsNullOrWhiteSpace(dto.Role) ? user.Role : dto.Role.Trim().ToLowerInvariant();
            var newStatus = string.IsNullOrWhiteSpace(dto.Status) ? user.Status : NormalizeStatus(dto.Status);
            if (newRole is not ("admin" or "pharmacist" or "customer"))
                return BadRequest(new { message = "Vai trò không hợp lệ." });
            if (id == int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "-1") &&
                (newRole != "admin" || newStatus != "Active"))
                return BadRequest(new { message = "Không thể tự hạ quyền hoặc khóa tài khoản đang đăng nhập." });
            if (user.Role == "admin" && user.Status == "Active" &&
                (newRole != "admin" || newStatus != "Active") &&
                !await _context.Users.AnyAsync(u => u.Id != id && u.Role == "admin" && u.Status == "Active"))
                return Conflict(new { message = "Phải giữ ít nhất một quản trị viên đang hoạt động." });

            if (!string.IsNullOrWhiteSpace(dto.FullName))
                user.FullName = dto.FullName.Trim();

            if (dto.Phone != null)
                user.Phone = dto.Phone.Trim();

            var normalizedEmail = dto.Email?.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(normalizedEmail) && normalizedEmail != user.Email)
            {
                if (normalizedEmail.Length > 150 || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(normalizedEmail))
                    return BadRequest(new { message = "Email không hợp lệ." });
                if (await _context.Users.AnyAsync(u => u.Email == normalizedEmail && u.Id != id))
                    return BadRequest(new { message = "Email đã được sử dụng" });
                user.Email = normalizedEmail;
            }

            if ((dto.FullName?.Length ?? 0) > 150 || (dto.Phone?.Length ?? 0) > 20)
                return BadRequest(new { message = "Họ tên hoặc số điện thoại vượt giới hạn độ dài." });

            user.Role = newRole;

            user.Status = newStatus;

            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                if (dto.Password.Length < 12 || dto.Password.Length > 128 ||
                    !dto.Password.Any(char.IsLetter) || !dto.Password.Any(char.IsDigit))
                    return BadRequest(new { message = "Mật khẩu cần tối thiểu 12 ký tự gồm chữ và số." });
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Đã cập nhật tài khoản" });
        }

        // PUT: api/users/5/toggle-status  (Khóa / Mở khóa)
        [HttpPut("{id}/toggle-status")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng" });

            var currentId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var parsedId) ? parsedId : -1;
            if (user.Id == currentId && user.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Không thể tự khóa tài khoản đang đăng nhập." });
            if (user.Role == "admin" && user.Status.Equals("Active", StringComparison.OrdinalIgnoreCase) &&
                !await _context.Users.AnyAsync(u => u.Id != id && u.Role == "admin" && u.Status == "Active"))
                return Conflict(new { message = "Phải giữ ít nhất một quản trị viên đang hoạt động." });

            user.Status = user.Status.Equals("Active", StringComparison.OrdinalIgnoreCase)
                ? "Locked"
                : "Active";

            await _context.SaveChangesAsync();
            return Ok(new
            {
                success = true,
                message = user.Status == "Active" ? "Đã mở khóa tài khoản" : "Đã khóa tài khoản",
                status = user.Status
            });
        }

        private static string NormalizeStatus(string status)
        {
            var s = status.Trim().ToLower();
            if (s == "locked" || s == "inactive") return "Locked";
            return "Active";
        }
    }

    public record CreateUserDto(
        string FullName,
        string? Phone,
        string Email,
        string? Password,
        string? Role,
        string? Status
    );

    public record UpdateUserDto(
        string? FullName,
        string? Phone,
        string? Email,
        string? Password,
        string? Role,
        string? Status
    );
}
