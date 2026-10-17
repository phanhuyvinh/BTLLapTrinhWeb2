using MedCare_Pharmacy.Models.Data;
using MedCare_Pharmacy.Models.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MedCare_Pharmacy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AppDbContext context, IConfiguration config, ILogger<AuthController> logger)
        {
            _context = context;
            _config = config;
            _logger = logger;
        }

        public record LoginDto(string Email, string Password);
        public record RegisterDto(string FullName, string Phone, string Email, string Password);
        public record BootstrapAdminDto(string FullName, string Phone, string Email, string Password);

        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrEmpty(dto.Password))
                return BadRequest(new { message = "Email hoặc mật khẩu không chính xác" });
            var email = dto.Email.Trim().ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                return BadRequest(new { message = "Email hoặc mật khẩu không chính xác" });
            }

            var isValidPassword = false;
            try
            {
                isValidPassword = user.PasswordHash.StartsWith("$2", StringComparison.Ordinal) &&
                                  BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
            }
            catch (ArgumentException) { /* Invalid/legacy hashes are rejected; plaintext is never accepted. */ }
            if (!isValidPassword)
            {
                return BadRequest(new { message = "Email hoặc mật khẩu không chính xác" });
            }

            if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Tài khoản của bạn đã bị khóa" });
            }

            var token = GenerateJwtToken(user);
            return Ok(new
            {
                token,
                user = new
                {
                    user.Id,
                    user.FullName,
                    user.Email,
                    user.Phone,
                    role = user.Role.ToLower(),
                    user.Status,
                    avatarUrl = user.AvatarUrl
                }
            });
        }

        [HttpPost("register")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Email) ||
                dto.FullName.Trim().Length > 150 || dto.Email.Trim().Length > 150 ||
                !new EmailAddressAttribute().IsValid(dto.Email.Trim()) ||
                !ValidPassword(dto.Password) || string.IsNullOrWhiteSpace(dto.Phone) || dto.Phone.Trim().Length > 20)
                return BadRequest(new { message = "Họ tên, email, số điện thoại là bắt buộc; mật khẩu cần ít nhất 12 ký tự gồm chữ và số." });

            var email = dto.Email.Trim().ToLowerInvariant();
            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                return BadRequest(new { message = "Email đã được sử dụng" });
            }

            var user = new User
            {
                FullName = dto.FullName.Trim(),
                Phone = dto.Phone.Trim(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = "customer",
                Status = "Active"
            };

            _context.Users.Add(user);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.GetBaseException() is SqlException sqlException && sqlException.Number is 2601 or 2627)
            {
                return Conflict(new { message = "Email này đã được đăng ký. Vui lòng dùng email khác hoặc đăng nhập." });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Could not save registration for {Email}", email);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Không thể lưu tài khoản. Hãy kiểm tra cấu trúc bảng User và nhật ký ứng dụng." });
            }

            return Ok(new { message = "Đăng ký thành công" });
        }

        // One-time initial setup. This endpoint is unavailable after the first account exists.
        [HttpPost("bootstrap-admin")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> BootstrapAdmin([FromBody] BootstrapAdminDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Email) ||
                dto.FullName.Trim().Length > 150 || dto.Email.Trim().Length > 150 ||
                !new EmailAddressAttribute().IsValid(dto.Email.Trim()) ||
                !ValidPassword(dto.Password) || string.IsNullOrWhiteSpace(dto.Phone) || dto.Phone.Trim().Length > 20)
                return BadRequest(new { message = "Cần họ tên, email, số điện thoại và mật khẩu tối thiểu 12 ký tự gồm chữ và số." });

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            if (await _context.Users.AnyAsync())
                return Conflict(new { message = "Tài khoản quản trị đã được khởi tạo." });

            var user = new User
            {
                FullName = dto.FullName.Trim(),
                Phone = dto.Phone.Trim(),
                Email = dto.Email.Trim().ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = "admin",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { success = true, message = "Đã tạo tài khoản quản trị đầu tiên. Hãy đăng nhập." });
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                return Conflict(new { message = "Không thể khởi tạo. Có thể tài khoản quản trị đã được tạo." });
            }
        }

        private static bool ValidPassword(string? password) =>
            password is { Length: >= 12 and <= 128 } && password.Any(char.IsLetter) && password.Any(char.IsDigit);

        private string GenerateJwtToken(User user)
        {
            var jwtKey = _config["Jwt:Key"]!;
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role.ToLower())
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(Math.Clamp(_config.GetValue<int?>("Jwt:ExpireDays") ?? 1, 1, 30)),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [HttpPost("forgot-password")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ForgotPasswordApi([FromBody] ForgotPasswordDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.EmailOrPhone))
            {
                return BadRequest(new { message = "Vui lòng nhập thông tin tài khoản" });
            }

            // No email/SMS delivery service is configured yet; do not claim a reset was sent.
            return StatusCode(StatusCodes.Status501NotImplemented,
                new { success = false, message = "Chức năng khôi phục mật khẩu chưa được cấu hình dịch vụ gửi email/SMS." });
        }

        [HttpPost("update-profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng" });

            user.FullName = dto.FullName;
            user.Phone = dto.Phone;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Cập nhật cơ sở dữ liệu thành công",
                fullName = user.FullName,
                phone = user.Phone,
                avatarUrl = user.AvatarUrl
            });
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            if (string.IsNullOrEmpty(dto.CurrentPassword) || !ValidPassword(dto.NewPassword))
                return BadRequest(new { message = "Mật khẩu mới cần tối thiểu 12 ký tự gồm chữ và số." });
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng" });

            bool isValidPassword = BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash);
            if (!isValidPassword)
            {
                return BadRequest(new { message = "Mật khẩu hiện tại không chính xác" });
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đổi mật khẩu thành công" });
        }

        /// <summary>
        /// Đổi ảnh hồ sơ — chỉ user đang đăng nhập, chỉ sửa avatar của chính mình.
        /// </summary>
        [HttpPost("avatar")]
        [Authorize]
        [RequestSizeLimit(3 * 1024 * 1024)]
        public async Task<IActionResult> UploadAvatar(IFormFile file)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Vui lòng chọn ảnh." });

            var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !allowedExt.Contains(ext))
                return BadRequest(new { message = "Chỉ hỗ trợ JPG, JPEG, PNG, WebP." });

            if (file.Length > 2 * 1024 * 1024)
                return BadRequest(new { message = "Ảnh không được vượt quá 2 MB." });

            var contentType = (file.ContentType ?? "").ToLowerInvariant();
            if (!contentType.StartsWith("image/"))
                return BadRequest(new { message = "Tệp không phải là ảnh hợp lệ." });

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng." });

            var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars");
            Directory.CreateDirectory(folder);

            var fileName = $"{userId}_{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(folder, fileName);
            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            user.AvatarUrl = $"/uploads/avatars/{fileName}";
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Đổi ảnh đại diện thành công.",
                avatarUrl = user.AvatarUrl
            });
        }

        public record UpdateProfileDto(string FullName, string Phone);
        public record ChangePasswordDto(string CurrentPassword, string NewPassword);
        public record ForgotPasswordDto(string EmailOrPhone);
    }
}
