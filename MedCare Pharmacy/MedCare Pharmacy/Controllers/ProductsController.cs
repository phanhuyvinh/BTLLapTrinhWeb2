using MedCare_Pharmacy.Models.Data;
using MedCare_Pharmacy.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedCare_Pharmacy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProductsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/products?q=...&cat=...&sort=...
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? q, [FromQuery] string? cat, [FromQuery] string? sort)
        {
            var query = _context.Products.AsNoTracking().Include(p => p.Category).Include(p => p.Supplier).AsQueryable();

            // Tìm kiếm từ khóa
            if (!string.IsNullOrEmpty(q))
            {
                query = query.Where(p => p.Name.Contains(q) || (p.ActiveIngredient != null && p.ActiveIngredient.Contains(q)));
            }

            // Lọc theo danh mục (Hỗ trợ cả ID số lẫn Tên chuỗi danh mục)
            if (!string.IsNullOrEmpty(cat))
            {
                if (int.TryParse(cat, out int catId))
                {
                    query = query.Where(p => p.CategoryId == catId);
                }
                else
                {
                    query = query.Where(p => p.Category != null && p.Category.Name == cat);
                }
            }

            // Sắp xếp
            if (sort == "low") query = query.OrderBy(p => p.Price);
            else if (sort == "high") query = query.OrderByDescending(p => p.Price);

            var products = await query.ToListAsync();
            await ApplySellableStockAsync(products);
            return Ok(products);
        }

        // GET: api/products/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (product == null) return NotFound(new { message = "Không tìm thấy sản phẩm" });
            await ApplySellableStockAsync([product]);
            return Ok(product);
        }

        // POST: api/products
        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Create([FromBody] Product product)
        {
            if (!ValidProduct(product)) return BadRequest(new { message = "Thông tin thuốc hoặc giá bán không hợp lệ." });
            product.StockQuantity = 0; // Stock can only change through receiving or sales workflows.
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
        }

        // PUT: api/products/5
        [HttpPut("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Update(int id, [FromBody] Product product)
        {
            if (id != product.Id) return BadRequest();
            if (!ValidProduct(product)) return BadRequest(new { message = "Thông tin thuốc hoặc giá bán không hợp lệ." });
            var current = await _context.Products.FindAsync(id);
            if (current == null) return NotFound(new { message = "Không tìm thấy thuốc." });

            // Do not bind client supplied stock quantity or overwrite unrelated fields.
            current.Name = product.Name.Trim();
            current.ActiveIngredient = product.ActiveIngredient?.Trim();
            current.Price = product.Price;
            current.Code = product.Code?.Trim() ?? string.Empty;
            current.Unit = product.Unit.Trim();
            current.Description = product.Description ?? string.Empty;
            current.ImageUrl = product.ImageUrl ?? string.Empty;
            current.CategoryId = product.CategoryId;
            current.SupplierId = product.SupplierId;
            await _context.SaveChangesAsync();
            return Ok(current);
        }

        // DELETE: api/products/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            var hasHistory = await _context.OrderDetails.AnyAsync(d => d.ProductId == id) ||
                             await _context.InventoryBatches.AnyAsync(b => b.ProductId == id);
            if (hasHistory)
                return Conflict(new { message = "Không thể xóa thuốc đã có giao dịch hoặc lô nhập để bảo toàn lịch sử." });

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã xóa sản phẩm thành công" });
        }

        private static bool ValidProduct(Product? product) =>
            product != null && !string.IsNullOrWhiteSpace(product.Name) && product.Name.Trim().Length <= 200 &&
            product.Price > 0 && product.Price <= 9999999999999999.99m &&
            !string.IsNullOrWhiteSpace(product.Unit) && product.Unit.Trim().Length <= 50 &&
            (product.Code?.Length ?? 0) <= 50 &&
            (product.ActiveIngredient?.Length ?? 0) <= 200;

        private async Task ApplySellableStockAsync(List<Product> products)
        {
            if (products.Count == 0) return;
            var ids = products.Select(p => p.Id).ToList();
            var today = DateTime.Today;
            var batches = await _context.InventoryBatches.AsNoTracking()
                .Where(b => ids.Contains(b.ProductId) && b.Quantity > 0 && b.ExpDate >= today)
                .GroupBy(b => b.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(b => b.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Quantity);

            foreach (var product in products)
            {
                var sellable = batches.GetValueOrDefault(product.Id);
                product.StockQuantity = Math.Min(product.StockQuantity, sellable);
            }
        }
    }
}
