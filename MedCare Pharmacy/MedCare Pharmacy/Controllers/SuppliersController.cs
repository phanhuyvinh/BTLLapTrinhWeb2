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

            _context.Entry(supplier).State = EntityState.Modified;
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

            _context.Suppliers.Remove(supplier);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã xóa nhà cung cấp!" });
        }
    }
}
