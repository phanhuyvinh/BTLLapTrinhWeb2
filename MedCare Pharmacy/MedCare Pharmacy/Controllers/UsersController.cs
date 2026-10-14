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
                    avatarUrl = u.AvatarUrl,
                    u.CreatedAt
                })
                .ToListAsync();

            return Ok(users);
        }

        // GET: api/users/5
        [HttpGet("{id:int}")]
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
                avatarUrl = u.AvatarUrl,
                u.CreatedAt
            });
        }
        // GET: api/users/pharmacists — danh sách dược sĩ (Admin)
        [HttpGet("pharmacists")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetPharmacists()
        {
            var list = await _context.Users
                .Where(u => u.Role.ToLower() == "pharmacist")
                .OrderBy(u => u.FullName)
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.Phone,
                    u.Email,
                    role = u.Role.ToLower(),
                    status = u.Status,
                    avatarUrl = u.AvatarUrl,
                    u.CreatedAt
                })
                .ToListAsync();

            return Ok(list);
        }
        // POST: api/users  (Admin tạo tài khoản)
        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Email))
            {
                return BadRequest(new { message = "Họ tên và Email bắt buộc" });
            }

            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            {
                return BadRequest(new { message = "Email đã được sử dụng" });
            }

            var password = string.IsNullOrWhiteSpace(dto.Password) ? "123456" : dto.Password;

            var user = new User
            {
                FullName = dto.FullName.Trim(),
                Phone = dto.Phone?.Trim() ?? "",
                Email = dto.Email.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = (dto.Role ?? "customer").ToLower(),
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

            if (!string.IsNullOrWhiteSpace(dto.FullName))
                user.FullName = dto.FullName.Trim();

            if (dto.Phone != null)
                user.Phone = dto.Phone.Trim();

            if (!string.IsNullOrWhiteSpace(dto.Email) && dto.Email != user.Email)
            {
                if (await _context.Users.AnyAsync(u => u.Email == dto.Email && u.Id != id))
                    return BadRequest(new { message = "Email đã được sử dụng" });
                user.Email = dto.Email.Trim();
            }

            if (!string.IsNullOrWhiteSpace(dto.Role))
                user.Role = dto.Role.ToLower();

            if (!string.IsNullOrWhiteSpace(dto.Status))
                user.Status = NormalizeStatus(dto.Status);

            if (!string.IsNullOrWhiteSpace(dto.Password))
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

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