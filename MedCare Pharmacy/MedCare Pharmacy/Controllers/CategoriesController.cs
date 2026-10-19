using MedCare_Pharmacy.Models.Data;
using MedCare_Pharmacy.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedCare_Pharmacy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CategoriesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/categories
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var categories = await _context.Categories.ToListAsync();
            return Ok(categories);
        }

        // GET: api/categories/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound(new { message = "Không tìm thấy danh mục!" });
            return Ok(category);
        }

        // POST: api/categories
        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Create([FromBody] Category category)
        {
            if (!ValidCategory(category))
                return BadRequest(new { message = "Tên danh mục là bắt buộc và tối đa 150 ký tự." });

            category.Name = category.Name.Trim();
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Thêm danh mục thành công!", category });
        }

        // PUT: api/categories/5
        [HttpPut("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Update(int id, [FromBody] Category category)
        {
            if (id != category.Id) return BadRequest(new { message = "ID không trùng khớp!" });
            if (!ValidCategory(category))
                return BadRequest(new { message = "Tên danh mục là bắt buộc và tối đa 150 ký tự." });

            var existing = await _context.Categories.FindAsync(id);
            if (existing == null) return NotFound(new { message = "Không tìm thấy danh mục!" });

            existing.Name = category.Name.Trim();
            existing.Description = category.Description;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Cập nhật danh mục thành công!" });
        }

        // DELETE: api/categories/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound(new { message = "Không tìm thấy danh mục!" });

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã xóa danh mục!" });
        }

        private static bool ValidCategory(Category? category) =>
            category != null && !string.IsNullOrWhiteSpace(category.Name) && category.Name.Trim().Length <= 150;
    }
}
