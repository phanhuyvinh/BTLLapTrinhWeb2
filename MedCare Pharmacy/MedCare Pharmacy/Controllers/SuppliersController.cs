using MedCare_Pharmacy.Models.Data;
using MedCare_Pharmacy.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedCare_Pharmacy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "admin,pharmacist")]
    public class SuppliersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SuppliersController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/suppliers
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var suppliers = await _context.Suppliers.ToListAsync();
            return Ok(suppliers);
        }

        // GET: api/suppliers/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound(new { message = "Không tìm thấy nhà cung cấp!" });
            return Ok(supplier);
        }

        // POST: api/suppliers
        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Create([FromBody] Supplier supplier)
        {
            if (!ValidSupplier(supplier))
                return BadRequest(new { message = "Thông tin nhà cung cấp không hợp lệ hoặc vượt giới hạn độ dài." });

            supplier.Name = supplier.Name.Trim();
            supplier.Phone = TrimOrNull(supplier.Phone);
            supplier.Email = TrimOrNull(supplier.Email);
            supplier.Address = TrimOrNull(supplier.Address);
            supplier.Status = NormalizeStatus(supplier.Status);
            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Thêm nhà cung cấp thành công!", supplier });
        }

        // PUT: api/suppliers/5
        [HttpPut("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Update(int id, [FromBody] Supplier supplier)
        {
            if (id != supplier.Id) return BadRequest(new { message = "ID không trùng khớp!" });
            if (!ValidSupplier(supplier))
                return BadRequest(new { message = "Thông tin nhà cung cấp không hợp lệ hoặc vượt giới hạn độ dài." });

            var existing = await _context.Suppliers.FindAsync(id);
            if (existing == null) return NotFound(new { message = "Không tìm thấy nhà cung cấp!" });

            existing.Name = supplier.Name.Trim();
            existing.Phone = TrimOrNull(supplier.Phone);
            existing.Email = TrimOrNull(supplier.Email);
            existing.Address = TrimOrNull(supplier.Address);
            existing.Status = NormalizeStatus(supplier.Status);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Cập nhật nhà cung cấp thành công!" });
        }

        // DELETE: api/suppliers/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound(new { message = "Không tìm thấy nhà cung cấp!" });

            if (await _context.PurchaseReceipts.AnyAsync(r => r.SupplierId == id))
                return Conflict(new { message = "Không thể xóa nhà cung cấp đã có phiếu nhập để bảo toàn lịch sử." });

            _context.Suppliers.Remove(supplier);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã xóa nhà cung cấp!" });
        }

        private static bool ValidSupplier(Supplier? supplier) =>
            supplier != null && !string.IsNullOrWhiteSpace(supplier.Name) && supplier.Name.Trim().Length <= 200 &&
            (supplier.Phone?.Length ?? 0) <= 20 && (supplier.Email?.Length ?? 0) <= 150 &&
            (supplier.Address?.Length ?? 0) <= 300 &&
            (string.IsNullOrWhiteSpace(supplier.Status) ||
             supplier.Status.Equals("Active", StringComparison.OrdinalIgnoreCase) ||
             supplier.Status.Equals("Inactive", StringComparison.OrdinalIgnoreCase));

        private static string? TrimOrNull(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string NormalizeStatus(string? value) =>
            string.Equals(value?.Trim(), "Inactive", StringComparison.OrdinalIgnoreCase) ? "Inactive" : "Active";
    }
}
