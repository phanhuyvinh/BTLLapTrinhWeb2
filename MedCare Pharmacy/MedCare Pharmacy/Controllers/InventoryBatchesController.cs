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
    public class InventoryBatchesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public InventoryBatchesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/inventorybatches
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var batches = await _context.InventoryBatches
                .Include(b => b.Product)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
            return Ok(batches);
        }

        // GET: api/inventorybatches/alerts  (dùng sau cho Inventory Alert)
        [HttpGet("alerts")]
        public async Task<IActionResult> GetAlerts()
        {
            var today = DateTime.Today;
            var nearExpiryLimit = today.AddDays(90);

            var batches = await _context.InventoryBatches
                .Include(b => b.Product)
                .ToListAsync();

            var nearExpiry = batches
                .Where(b => b.ExpDate >= today && b.ExpDate <= nearExpiryLimit && b.Quantity > 0)
                .Select(b => new
                {
                    name = b.Product != null ? b.Product.Name : ("SP#" + b.ProductId),
                    lot = b.LotNumber,
                    expiry = b.ExpDate,
                    stock = b.Quantity
                })
                .ToList();

            var expired = batches
                .Where(b => b.ExpDate < today && b.Quantity > 0)
                .Select(b => new
                {
                    name = b.Product != null ? b.Product.Name : ("SP#" + b.ProductId),
                    lot = b.LotNumber,
                    expiry = b.ExpDate,
                    stock = b.Quantity
                })
                .ToList();

            var products = await _context.Products.ToListAsync();
            var outOfStock = products
                .Where(p => p.StockQuantity <= 0)
                .Select(p => new { name = p.Name, stock = p.StockQuantity })
                .ToList();

            var lowStock = products
                .Where(p => p.StockQuantity > 0 && p.StockQuantity <= 20)
                .Select(p => new { name = p.Name, stock = p.StockQuantity })
                .ToList();

            return Ok(new
            {
                nearExpiry,
                expired,
                outOfStock,
                lowStock
            });
        }

        // POST: api/inventorybatches/import  (Lưu phiếu nhập nhiều dòng)
        [HttpPost("import")]
        public async Task<IActionResult> Import([FromBody] ImportBatchDto dto)
        {
            if (dto == null || dto.Items == null || dto.Items.Count == 0)
            {
                return BadRequest(new { message = "Phiếu nhập trống!" });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var item in dto.Items)
                {
                    if (item.Quantity <= 0)
                    {
                        return BadRequest(new { message = "Số lượng phải > 0" });
                    }
                    if (item.MfgDate >= item.ExpDate)
                    {
                        return BadRequest(new { message = "Ngày sản xuất phải trước hạn sử dụng" });
                    }

                    if (string.IsNullOrWhiteSpace(item.LotNumber) || item.LotNumber.Trim().Length > 100 ||
                        item.ImportPrice <= 0 || item.ImportPrice > 9999999999999999.99m)
                        return BadRequest(new { message = "Số lô và giá nhập phải hợp lệ." });
                    if (item.ExpDate.Date < DateTime.UtcNow.Date)
                        return BadRequest(new { message = "Không thể nhập lô hàng đã hết hạn." });

                    var exists = await _context.Products.AnyAsync(p => p.Id == item.ProductId);
                    if (!exists)
                    {
                        return BadRequest(new { message = $"Không tìm thấy sản phẩm ID {item.ProductId}" });
                    }

                    var stockUpdated = await _context.Products
                        .Where(p => p.Id == item.ProductId && p.StockQuantity <= int.MaxValue - item.Quantity)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity + item.Quantity));
                    if (stockUpdated != 1)
                        return Conflict(new { message = "Số lượng tồn kho vượt giới hạn hoặc sản phẩm không còn tồn tại." });

                    var batch = new InventoryBatch
                    {
                        ProductId = item.ProductId,
                        LotNumber = item.LotNumber,
                        Quantity = item.Quantity,
                        MfgDate = item.MfgDate,
                        ExpDate = item.ExpDate,
                        ImportPrice = item.ImportPrice,
                        CreatedAt = DateTime.Now
                    };
                    _context.InventoryBatches.Add(batch);

                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, message = "Đã lưu phiếu nhập kho thành công!" });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Lỗi lưu phiếu nhập. Vui lòng thử lại hoặc liên hệ quản trị viên." });
            }
        }
    }

    public record ImportBatchItemDto(
        int ProductId,
        string LotNumber,
        int Quantity,
        DateTime MfgDate,
        DateTime ExpDate,
        decimal ImportPrice
    );

    public record ImportBatchDto(List<ImportBatchItemDto> Items);
}
