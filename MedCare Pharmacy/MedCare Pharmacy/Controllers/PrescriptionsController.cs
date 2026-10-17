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
    public class PrescriptionsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public PrescriptionsController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private int? CurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }

        // GET: api/prescriptions
        [HttpGet]
        public async Task<IActionResult> GetMine()
        {
            var me = CurrentUserId();
            if (me == null) return Unauthorized(new { message = "Chưa đăng nhập" });

            var list = await _context.Prescriptions
                .Where(p => p.UserId == me)
                .OrderByDescending(p => p.UploadedAt)
                .Select(p => new
                {
                    p.Id,
                    p.FileName,
                    FileUrl = $"/api/prescriptions/{p.Id}/file",
                    p.FileType,
                    p.UploadedAt
                })
                .ToListAsync();

            return Ok(list);
        }

        // POST: api/prescriptions/upload  (multipart/form-data, field name = "file")
        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            var me = CurrentUserId();
            if (me == null) return Unauthorized(new { message = "Chưa đăng nhập" });

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Chưa chọn file" });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowed = new[] { ".png", ".jpg", ".jpeg", ".pdf" };
            if (!allowed.Contains(ext))
                return BadRequest(new { message = "Chỉ hỗ trợ PNG, JPG, JPEG, PDF" });

            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { message = "File tối đa 5MB" });

            var folder = PrivateFolder();
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var storedName = $"{me}_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
            var physicalPath = Path.Combine(folder, storedName);

            if (!await HasValidSignature(file, ext))
                return BadRequest(new { message = "Nội dung tệp không khớp với định dạng đã chọn." });

            using (var stream = new FileStream(physicalPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var fileType = ext == ".pdf" ? "pdf" : "image";

            var entity = new Prescription
            {
                UserId = me.Value,
                FileName = file.FileName,
                FileUrl = storedName,
                FileType = fileType,
                UploadedAt = DateTime.Now
            };

            _context.Prescriptions.Add(entity);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Đã tải lên đơn thuốc",
                prescription = new
                {
                    entity.Id,
                    entity.FileName,
                    FileUrl = $"/api/prescriptions/{entity.Id}/file",
                    entity.FileType,
                    entity.UploadedAt
                }
            });
        }

        [HttpGet("{id}/file")]
        public async Task<IActionResult> Download(int id)
        {
            var me = CurrentUserId();
            if (me == null) return Unauthorized();
            var item = await _context.Prescriptions.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (item == null) return NotFound();

            var role = User.FindFirst(ClaimTypes.Role)?.Value?.ToLowerInvariant();
            if (item.UserId != me.Value && role is not ("admin" or "pharmacist")) return Forbid();

            var storedName = Path.GetFileName(item.FileUrl);
            var path = Path.Combine(PrivateFolder(), storedName);
            // Support reading existing records while they are migrated from the old public folder.
            if (!System.IO.File.Exists(path) && item.FileUrl.StartsWith("/uploads/prescriptions/", StringComparison.OrdinalIgnoreCase))
                path = Path.Combine(_env.WebRootPath, "uploads", "prescriptions", storedName);
            if (!System.IO.File.Exists(path)) return NotFound(new { message = "Không tìm thấy tệp đơn thuốc." });

            var extension = Path.GetExtension(storedName).ToLowerInvariant();
            var contentType = extension switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                _ => "image/jpeg"
            };
            return PhysicalFile(path, contentType, enableRangeProcessing: true);
        }

        // DELETE: api/prescriptions/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var me = CurrentUserId();
            if (me == null) return Unauthorized(new { message = "Chưa đăng nhập" });

            var item = await _context.Prescriptions
                .FirstOrDefaultAsync(p => p.Id == id && p.UserId == me);

            if (item == null)
                return NotFound(new { message = "Không tìm thấy đơn thuốc" });

            // Xóa file vật lý nếu còn
            try
            {
                var storedName = Path.GetFileName(item.FileUrl);
                var physical = Path.Combine(PrivateFolder(), storedName);
                if (!System.IO.File.Exists(physical) && item.FileUrl.StartsWith("/uploads/prescriptions/", StringComparison.OrdinalIgnoreCase))
                    physical = Path.Combine(_env.WebRootPath, "uploads", "prescriptions", storedName);
                if (System.IO.File.Exists(physical))
                    System.IO.File.Delete(physical);
            }
            catch { /* bỏ qua lỗi xóa file */ }

            _context.Prescriptions.Remove(item);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã xóa đơn thuốc" });
        }

        private string PrivateFolder() => Path.Combine(_env.ContentRootPath, "PrivateUploads", "prescriptions");

        private static async Task<bool> HasValidSignature(IFormFile file, string extension)
        {
            var header = new byte[8];
            await using var stream = file.OpenReadStream();
            var read = await stream.ReadAsync(header.AsMemory());
            if (extension == ".pdf") return read >= 5 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46 && header[4] == 0x2D;
            if (extension == ".png") return read >= 8 && header.SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            if (extension is ".jpg" or ".jpeg") return read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
            return false;
        }
    }
}
