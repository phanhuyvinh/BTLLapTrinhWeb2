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

        [HttpGet("movements")]
        public async Task<IActionResult> GetMovements([FromQuery] int limit = 100)
        {
            limit = Math.Clamp(limit, 1, 500);
            var movements = await _context.InventoryMovements.AsNoTracking()
                .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Take(limit)
                .Select(m => new
                {
                    m.Id, m.MovementType, m.Quantity, m.StockBefore, m.StockAfter, m.Note, m.CreatedAt,
                    productName = m.Product!.Name,
                    lotNumber = m.InventoryBatch == null ? null : m.InventoryBatch.LotNumber,
                    receiptNumber = m.PurchaseReceipt == null ? null : m.PurchaseReceipt.ReceiptNumber,
                    orderId = m.OrderId,
                    performedBy = m.PerformedBy == null ? null : m.PerformedBy.FullName
                }).ToListAsync();
            return Ok(movements);
        }

        [HttpGet("receipts")]
        public async Task<IActionResult> GetReceipts([FromQuery] int limit = 100)
        {
            limit = Math.Clamp(limit, 1, 500);
            var receipts = await _context.PurchaseReceipts.AsNoTracking()
                .Include(r => r.Supplier).Include(r => r.Lines).ThenInclude(l => l.Product)
                .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Take(limit)
                .Select(r => new
                {
                    r.Id, r.ReceiptNumber, r.Note, r.TotalAmount, r.CreatedAt,
                    supplierName = r.Supplier!.Name,
                    itemCount = r.Lines.Count,
                    totalQuantity = r.Lines.Sum(l => l.Quantity),
                    lines = r.Lines.Select(l => new { productName = l.Product!.Name, l.Quantity, l.UnitCost, l.LineTotal })
                }).ToListAsync();
            return Ok(receipts);
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

            var products = await _context.Products.AsNoTracking().Select(p => new { p.Id, p.Name, p.StockQuantity }).ToListAsync();
            var sellableByProduct = await _context.InventoryBatches.AsNoTracking()
                .Where(b => b.Quantity > 0 && b.ExpDate >= today)
                .GroupBy(b => b.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(b => b.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Quantity);
            var outOfStock = products
                .Select(p => new { p.Name, stock = Math.Min(p.StockQuantity, sellableByProduct.GetValueOrDefault(p.Id)) })
                .Where(p => p.stock <= 0)
                .Select(p => new { name = p.Name, p.stock })
                .ToList();

            var lowStock = products
                .Select(p => new { p.Name, stock = Math.Min(p.StockQuantity, sellableByProduct.GetValueOrDefault(p.Id)) })
                .Where(p => p.stock > 0 && p.stock <= 20)
                .Select(p => new { name = p.Name, p.stock })
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
            if (dto == null || dto.Items == null || dto.Items.Count == 0 || dto.Items.Count > 100)
            {
                return BadRequest(new { message = "Phiếu nhập phải có từ 1 đến 100 dòng hàng." });
            }

            if (dto.SupplierId <= 0 || !await _context.Suppliers.AnyAsync(s => s.Id == dto.SupplierId && s.Status == "Active"))
                return BadRequest(new { message = "Vui lòng chọn nhà cung cấp đang hoạt động." });
            if (dto.Items.Any(item => item.ProductId <= 0 || item.Quantity <= 0 || item.Quantity > 100000 ||
                string.IsNullOrWhiteSpace(item.LotNumber) || item.LotNumber.Trim().Length > 100 ||
                item.MfgDate.Date >= item.ExpDate.Date || item.ExpDate.Date < DateTime.UtcNow.Date ||
                item.ImportPrice <= 0 || item.ImportPrice > 9999999999999999.99m))
                return BadRequest(new { message = "Một hoặc nhiều dòng có số lô, số lượng, ngày hoặc giá nhập không hợp lệ." });
            if (!string.IsNullOrWhiteSpace(dto.Note) && dto.Note.Trim().Length > 500)
                return BadRequest(new { message = "Ghi chú tối đa 500 ký tự." });

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var receipt = new PurchaseReceipt
                {
                    ReceiptNumber = $"PN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
                    SupplierId = dto.SupplierId,
                    CreatedByUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null,
                    Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
                    CreatedAt = DateTime.UtcNow
                };
                var movements = new List<InventoryMovement>();
                decimal totalAmount = 0;
                foreach (var item in dto.Items)
                {
                    var product = await _context.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == item.ProductId);
                    if (product == null) return BadRequest(new { message = $"Không tìm thấy sản phẩm ID {item.ProductId}." });
                    if (item.Quantity > int.MaxValue - product.StockQuantity)
                        return Conflict(new { message = $"Tồn kho sản phẩm {product.Name} vượt giới hạn." });
                    var lineTotal = item.ImportPrice * item.Quantity;
                    if (lineTotal > 9999999999999999.99m - totalAmount)
                        return BadRequest(new { message = "Tổng tiền phiếu nhập vượt giới hạn." });
                    totalAmount += lineTotal;

                    var stockUpdated = await _context.Products
                        .Where(p => p.Id == item.ProductId && p.StockQuantity == product.StockQuantity && p.StockQuantity <= int.MaxValue - item.Quantity)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity + item.Quantity));
                    if (stockUpdated != 1)
                        return Conflict(new { message = $"Tồn kho sản phẩm {product.Name} vừa được cập nhật. Vui lòng gửi lại phiếu." });

                    var batch = new InventoryBatch
                    {
                        ProductId = item.ProductId,
                        LotNumber = item.LotNumber.Trim(),
                        Quantity = item.Quantity,
                        MfgDate = item.MfgDate,
                        ExpDate = item.ExpDate,
                        ImportPrice = item.ImportPrice,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.InventoryBatches.Add(batch);
                    receipt.Lines.Add(new PurchaseReceiptLine { ProductId = item.ProductId, InventoryBatch = batch,
                        Quantity = item.Quantity, UnitCost = item.ImportPrice, LineTotal = lineTotal });
                    movements.Add(new InventoryMovement { ProductId = item.ProductId, InventoryBatch = batch,
                        MovementType = "Import", Quantity = item.Quantity, StockBefore = product.StockQuantity,
                        StockAfter = product.StockQuantity + item.Quantity, Note = "Nhập hàng từ nhà cung cấp" });
                }

                receipt.TotalAmount = totalAmount;
                _context.PurchaseReceipts.Add(receipt);
                await _context.SaveChangesAsync();
                foreach (var movement in movements)
                {
                    movement.PurchaseReceiptId = receipt.Id;
                    movement.PerformedByUserId = receipt.CreatedByUserId;
                    movement.Note = $"Phiếu {receipt.ReceiptNumber}" + (receipt.Note == null ? "" : $" — {receipt.Note}");
                }
                _context.InventoryMovements.AddRange(movements);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, receiptId = receipt.Id, receiptNumber = receipt.ReceiptNumber,
                    totalAmount = receipt.TotalAmount, message = $"Đã lưu phiếu nhập {receipt.ReceiptNumber} thành công." });
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

    public record ImportBatchDto(int SupplierId, List<ImportBatchItemDto> Items, string? Note = null);
}
