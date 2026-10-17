using MedCare_Pharmacy.Models.Data;
using MedCare_Pharmacy.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MedCare_Pharmacy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private const decimal MaxOrderAmount = 9999999999999999.99m;
        private readonly AppDbContext _context;

        public OrdersController(AppDbContext context) => _context = context;

        [HttpGet]
        [Authorize(Roles = "admin,pharmacist")]
        public async Task<IActionResult> GetAll()
        {
            var orders = await _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new
                {
                    o.Id, o.UserId, o.CustomerName, o.Phone, o.Address, o.OrderType,
                    o.Status, o.PrescriptionUrl, o.TotalAmount, o.CreatedAt,
                    o.PaymentMethod, o.PaymentStatus, o.AmountReceived, o.ChangeAmount, o.PaidAt,
                    orderDetails = o.OrderDetails.Select(d => new
                    {
                        d.Id, d.OrderId, d.ProductId, d.Quantity, d.UnitPrice,
                        product = d.Product == null ? null : new
                        {
                            d.Product.Id, d.Product.Name, d.Product.Price,
                            d.Product.ImageUrl, d.Product.Unit
                        }
                    }).ToList()
                }).ToListAsync();
            return Ok(orders);
        }

        // Khách có thể đặt hàng dạng guest; khi có JWT thì chủ đơn luôn lấy từ claim.
        [HttpPost]
        [AllowAnonymous]
        [EnableRateLimiting("order-create")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
        {
            if (User.Identity?.IsAuthenticated == true && !User.IsInRole("customer")) return Forbid();
            if (!ValidContact(dto.CustomerName, 150) || !ValidContact(dto.Phone, 20) ||
                !ValidContact(dto.Address, 300) || dto.Items == null || dto.Items.Count == 0 || dto.Items.Count > 100)
                return BadRequest(new { message = "Thông tin giao hàng hoặc danh sách sản phẩm không hợp lệ." });

            var quantities = ValidateAndCombine(dto.Items);
            if (quantities.Error != null) return BadRequest(new { message = quantities.Error });

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var products = await _context.Products.AsNoTracking()
                    .Where(p => quantities.Items.Keys.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);
                if (products.Count != quantities.Items.Count)
                    return BadRequest(new { message = "Một hoặc nhiều sản phẩm không còn tồn tại." });

                decimal subtotal = 0;
                var lines = new List<OrderDetail>();
                var stockMovements = new List<InventoryMovement>();
                foreach (var (productId, quantity) in quantities.Items.OrderBy(x => x.Key))
                {
                    var product = products[productId];
                    if (product.Price <= 0) return BadRequest(new { message = $"Giá sản phẩm {product.Name} không hợp lệ." });

                    var consume = await ConsumeStockByExpiryAsync(product, quantity, "Đặt hàng trực tuyến");
                    if (consume.Error != null)
                    {
                        await transaction.RollbackAsync();
                        return Conflict(new { message = consume.Error });
                    }
                    stockMovements.AddRange(consume.Movements);

                    var lineTotal = product.Price * quantity;
                    if (lineTotal > MaxOrderAmount - subtotal)
                        return BadRequest(new { message = "Tổng tiền đơn hàng vượt giới hạn." });
                    subtotal += lineTotal;
                    lines.Add(new OrderDetail { ProductId = productId, Quantity = quantity, UnitPrice = product.Price });
                }

                var shipping = subtotal == 0 || subtotal >= 300000m ? 0m : 30000m;
                if (shipping > MaxOrderAmount - subtotal)
                    return BadRequest(new { message = "Tổng tiền đơn hàng vượt giới hạn." });
                var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
                int? userId = int.TryParse(userIdValue, out var authenticatedId) ? authenticatedId : null;
                var order = new Order
                {
                    UserId = userId,
                    CustomerName = dto.CustomerName.Trim(),
                    Phone = dto.Phone.Trim(),
                    Address = dto.Address.Trim(),
                    OrderType = "Online",
                    Status = "Pending",
                    PrescriptionUrl = null,
                    TotalAmount = subtotal + shipping,
                    CreatedAt = DateTime.UtcNow,
                    OrderDetails = lines
                };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
                foreach (var movement in stockMovements) movement.OrderId = order.Id;
                _context.InventoryMovements.AddRange(stockMovements);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { success = true, orderId = order.Id, totalAmount = order.TotalAmount, message = "Đặt hàng thành công!" });
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                return Conflict(new { message = "Không thể lưu đơn hàng. Vui lòng tải lại giỏ hàng và thử lại." });
            }
        }

        [HttpPost("pos")]
        [Authorize(Roles = "admin,pharmacist")]
        public async Task<IActionResult> CreatePosOrder([FromBody] CreatePosDto dto)
        {
            if (dto.Items == null || dto.Items.Count == 0 || dto.Items.Count > 100)
                return BadRequest(new { message = "Hóa đơn phải có từ 1 đến 100 dòng hàng." });
            // POS khách vãng lai không bắt buộc cung cấp số điện thoại.
            if (!ValidContact(dto.CustomerName ?? "Khách vãng lai", 150) ||
                (!string.IsNullOrWhiteSpace(dto.Phone) && dto.Phone.Trim().Length > 20))
                return BadRequest(new { message = "Thông tin khách hàng không hợp lệ." });

            var quantities = ValidateAndCombine(dto.Items);
            if (quantities.Error != null) return BadRequest(new { message = quantities.Error });
            var paymentMethod = dto.PaymentMethod?.Trim();
            if (paymentMethod is not ("Cash" or "BankTransfer" or "Card"))
                return BadRequest(new { message = "Phương thức thanh toán không hợp lệ." });
            if (paymentMethod != "Cash" && !dto.NonCashPaymentConfirmed)
                return BadRequest(new { message = "Vui lòng xác nhận đã nhận thanh toán chuyển khoản/thẻ trước khi hoàn tất hóa đơn." });

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var products = await _context.Products.AsNoTracking()
                    .Where(p => quantities.Items.Keys.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);
                if (products.Count != quantities.Items.Count)
                    return BadRequest(new { message = "Một hoặc nhiều sản phẩm không còn tồn tại." });

                decimal subtotal = 0;
                var lines = new List<OrderDetail>();
                var stockMovements = new List<InventoryMovement>();
                foreach (var (productId, quantity) in quantities.Items.OrderBy(x => x.Key))
                {
                    var product = products[productId];
                    if (product.Price <= 0) return BadRequest(new { message = $"Giá sản phẩm {product.Name} không hợp lệ." });
                    var consume = await ConsumeStockByExpiryAsync(product, quantity, "Bán hàng tại quầy");
                    if (consume.Error != null)
                    {
                        await transaction.RollbackAsync();
                        return Conflict(new { message = consume.Error });
                    }
                    stockMovements.AddRange(consume.Movements);

                    var lineTotal = product.Price * quantity;
                    if (lineTotal > MaxOrderAmount - subtotal)
                        return BadRequest(new { message = "Tổng tiền hóa đơn vượt giới hạn." });
                    subtotal += lineTotal;
                    lines.Add(new OrderDetail { ProductId = productId, Quantity = quantity, UnitPrice = product.Price });
                }

                if (subtotal > MaxOrderAmount / 1.05m)
                    return BadRequest(new { message = "Tổng tiền hóa đơn vượt giới hạn." });

                var amountDue = Math.Round(subtotal * 1.05m, 2, MidpointRounding.AwayFromZero);
                var amountReceived = paymentMethod == "Cash" ? dto.AmountReceived : amountDue;
                if (!amountReceived.HasValue || amountReceived.Value < amountDue || amountReceived.Value > MaxOrderAmount)
                    return BadRequest(new { message = "Số tiền khách đưa phải lớn hơn hoặc bằng số tiền cần thanh toán." });
                var changeAmount = amountReceived.Value - amountDue;

                var order = new Order
                {
                    CustomerName = (dto.CustomerName ?? "Khách vãng lai").Trim(),
                    Phone = (dto.Phone ?? "").Trim(),
                    OrderType = "POS",
                    Status = "Completed",
                    PaymentMethod = paymentMethod!,
                    PaymentStatus = "Paid",
                    AmountReceived = amountReceived.Value,
                    ChangeAmount = changeAmount,
                    PaidAt = DateTime.UtcNow,
                    TotalAmount = amountDue,
                    CreatedAt = DateTime.UtcNow,
                    OrderDetails = lines
                };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
                foreach (var movement in stockMovements) movement.OrderId = order.Id;
                _context.InventoryMovements.AddRange(stockMovements);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { success = true, orderId = order.Id, totalAmount = order.TotalAmount,
                    paymentMethod = order.PaymentMethod, paymentStatus = order.PaymentStatus,
                    amountReceived = order.AmountReceived, changeAmount = order.ChangeAmount,
                    message = "Thanh toán POS thành công!" });
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                return Conflict(new { message = "Không thể lưu hóa đơn. Vui lòng tải lại tồn kho và thử lại." });
            }
        }

        [HttpPut("{id}/status")]
        [Authorize(Roles = "admin,pharmacist")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] StatusDto dto)
        {
            var next = dto.Status?.Trim();
            if (next is not ("Approved" or "Shipping" or "Completed" or "Cancelled"))
                return BadRequest(new { message = "Trạng thái đơn không hợp lệ." });

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var order = await _context.Orders.Include(o => o.OrderDetails).FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng!" });

            var currentStatus = order.Status;
            var allowed = order.Status switch
            {
                "Pending" => next is "Checking" or "Approved" or "Cancelled",
                "Checking" => next is "Approved" or "Cancelled",
                "Approved" => next is "Shipping" or "Cancelled",
                "Shipping" => next == "Completed",
                _ => false
            };
            if (!allowed) return Conflict(new { message = $"Không thể chuyển đơn từ {order.Status} sang {next}." });

            // Compare-and-set the status before changing stock so duplicate cancellation requests
            // cannot restore the same reserved units twice.
            var statusChanged = await _context.Orders
                .Where(o => o.Id == id && o.Status == currentStatus)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, next));
            if (statusChanged != 1)
            {
                await transaction.RollbackAsync();
                return Conflict(new { message = "Đơn hàng vừa được cập nhật ở một phiên khác. Tải lại rồi thử lại." });
            }

            if (next == "Completed" && order.PaymentMethod == "COD")
            {
                var paidAt = DateTime.UtcNow;
                await _context.Orders.Where(o => o.Id == id).ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.PaymentStatus, "Paid")
                    .SetProperty(o => o.AmountReceived, o => o.TotalAmount)
                    .SetProperty(o => o.ChangeAmount, 0m)
                    .SetProperty(o => o.PaidAt, paidAt));
            }

            if (next == "Cancelled")
            {
                var saleMovements = await _context.InventoryMovements.AsNoTracking()
                    .Where(m => m.OrderId == id && m.MovementType == "Sale")
                    .OrderBy(m => m.Id).ToListAsync();
                var performedById = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId)
                    ? currentUserId : (int?)null;
                foreach (var line in order.OrderDetails)
                {
                    var product = await _context.Products.AsNoTracking().Where(p => p.Id == line.ProductId)
                        .Select(p => new { p.StockQuantity }).SingleOrDefaultAsync();
                    if (product == null || product.StockQuantity > int.MaxValue - line.Quantity)
                    {
                        await transaction.RollbackAsync();
                        return Conflict(new { message = "Không thể hoàn lại tồn kho cho đơn này. Hãy kiểm tra dữ liệu kho." });
                    }
                    var productMovements = saleMovements.Where(m => m.ProductId == line.ProductId).ToList();
                    var restores = new List<(int? BatchId, int Quantity)>();
                    foreach (var sale in productMovements)
                    {
                        if (sale.Quantity >= 0)
                        {
                            await transaction.RollbackAsync();
                            return Conflict(new { message = "Nhật ký xuất kho của đơn không hợp lệ; không thể hoàn tồn tự động." });
                        }
                        var restoreQuantity = -sale.Quantity;
                        if (sale.InventoryBatchId is int batchId)
                        {
                            var batch = await _context.InventoryBatches.AsNoTracking()
                                .Where(b => b.Id == batchId).Select(b => new { b.Quantity }).SingleOrDefaultAsync();
                            if (batch == null || batch.Quantity > int.MaxValue - restoreQuantity)
                            {
                                await transaction.RollbackAsync();
                                return Conflict(new { message = "Không thể hoàn lại số lượng vào lô hàng của đơn." });
                            }
                            var batchUpdated = await _context.InventoryBatches
                                .Where(b => b.Id == batchId && b.Quantity == batch.Quantity && b.Quantity <= int.MaxValue - restoreQuantity)
                                .ExecuteUpdateAsync(s => s.SetProperty(b => b.Quantity, b => b.Quantity + restoreQuantity));
                            if (batchUpdated != 1)
                            {
                                await transaction.RollbackAsync();
                                return Conflict(new { message = "Lô hàng vừa được cập nhật. Tải lại rồi thử hủy đơn." });
                            }
                        }
                        restores.Add((sale.InventoryBatchId, restoreQuantity));
                    }

                    var restoredFromHistory = restores.Sum(x => x.Quantity);
                    if (restoredFromHistory > line.Quantity)
                    {
                        await transaction.RollbackAsync();
                        return Conflict(new { message = "Số lượng trong lịch sử kho không khớp với chi tiết đơn." });
                    }
                    if (restoredFromHistory < line.Quantity)
                        restores.Add((null, line.Quantity - restoredFromHistory)); // Orders created before batch-level history.

                    var changed = await _context.Products
                        .Where(p => p.Id == line.ProductId && p.StockQuantity == product.StockQuantity && p.StockQuantity <= int.MaxValue - line.Quantity)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity + line.Quantity));
                    if (changed != 1)
                    {
                        await transaction.RollbackAsync();
                        return Conflict(new { message = "Không thể hoàn lại tồn kho cho đơn này. Hãy kiểm tra dữ liệu kho." });
                    }

                    var runningStock = product.StockQuantity;
                    foreach (var restore in restores)
                    {
                        _context.InventoryMovements.Add(new InventoryMovement
                        {
                            ProductId = line.ProductId, InventoryBatchId = restore.BatchId, OrderId = id,
                            MovementType = "OrderCancelled", Quantity = restore.Quantity,
                            StockBefore = runningStock, StockAfter = runningStock + restore.Quantity,
                            PerformedByUserId = performedById, Note = $"Hoàn tồn do hủy đơn #{id}", CreatedAt = DateTime.UtcNow
                        });
                        runningStock += restore.Quantity;
                    }
                }
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();
            return Ok(new { success = true, message = "Đã cập nhật trạng thái đơn hàng!" });
        }

        [HttpGet("by-user")]
        [Authorize(Roles = "customer")]
        public async Task<IActionResult> GetByUser()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userId, out var id)) return Unauthorized();
            var orders = await _context.Orders.AsNoTracking()
                .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
                .Where(o => o.UserId == id)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new
                {
                    o.Id, o.UserId, o.CustomerName, o.Phone, o.Address, o.Status,
                    o.TotalAmount, o.CreatedAt, o.PrescriptionUrl,
                    o.PaymentMethod, o.PaymentStatus, o.AmountReceived, o.ChangeAmount, o.PaidAt,
                    orderDetails = o.OrderDetails.Select(d => new
                    {
                        d.Id, d.ProductId, d.Quantity, d.UnitPrice,
                        product = d.Product == null ? null : new { d.Product.Id, d.Product.Name, d.Product.Price }
                    }).ToList()
                }).ToListAsync();
            return Ok(orders);
        }

        private async Task<(List<InventoryMovement> Movements, string? Error)> ConsumeStockByExpiryAsync(
            Product product, int quantity, string note)
        {
            if (product.StockQuantity < quantity)
                return ([], $"Không đủ tồn kho cho sản phẩm {product.Name}.");

            var today = DateTime.Today;
            var productBatches = await _context.InventoryBatches.AsNoTracking()
                .Where(b => b.ProductId == product.Id && b.Quantity > 0)
                .OrderBy(b => b.ExpDate).ThenBy(b => b.MfgDate).ThenBy(b => b.Id)
                .ToListAsync();
            var batchTotal = productBatches.Sum(b => (long)b.Quantity);
            if (batchTotal != product.StockQuantity)
                return ([], $"Tồn kho lô của {product.Name} ({batchTotal}) chưa khớp tồn sản phẩm ({product.StockQuantity}). Hãy đối soát kho trước khi bán.");

            var batches = productBatches.Where(b => b.ExpDate >= today).ToList();
            if (batches.Sum(b => (long)b.Quantity) < quantity)
                return ([], $"Không đủ hàng còn hạn sử dụng của {product.Name}. Kiểm tra lại tồn theo lô.");

            var movements = new List<InventoryMovement>();
            var remaining = quantity;
            var productStock = product.StockQuantity;
            var actorId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (int?)null;
            foreach (var batch in batches)
            {
                if (remaining == 0) break;
                var consumed = Math.Min(batch.Quantity, remaining);
                var changed = await _context.InventoryBatches
                    .Where(b => b.Id == batch.Id && b.Quantity == batch.Quantity && b.Quantity >= consumed)
                    .ExecuteUpdateAsync(s => s.SetProperty(b => b.Quantity, b => b.Quantity - consumed));
                if (changed != 1)
                    return ([], $"Lô {batch.LotNumber} vừa được cập nhật. Vui lòng tải lại và bán lại.");

                movements.Add(new InventoryMovement
                {
                    ProductId = product.Id, InventoryBatchId = batch.Id,
                    MovementType = "Sale", Quantity = -consumed,
                    StockBefore = productStock, StockAfter = productStock - consumed,
                    PerformedByUserId = actorId, Note = note, CreatedAt = DateTime.UtcNow
                });
                productStock -= consumed;
                remaining -= consumed;
            }

            var productChanged = await _context.Products
                .Where(p => p.Id == product.Id && p.StockQuantity == product.StockQuantity && p.StockQuantity >= quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity - quantity));
            if (productChanged != 1)
                return ([], $"Tồn kho {product.Name} vừa được cập nhật. Vui lòng tải lại và thử lại.");

            return (movements, null);
        }

        private static (Dictionary<int, int> Items, string? Error) ValidateAndCombine(List<OrderItemDto> items)
        {
            var result = new Dictionary<int, int>();
            foreach (var item in items)
            {
                if (item.ProductId <= 0 || item.Quantity <= 0 || item.Quantity > 10000)
                    return (result, "Mã sản phẩm hoặc số lượng không hợp lệ.");
                if (result.TryGetValue(item.ProductId, out var existing))
                {
                    if (existing > 10000 - item.Quantity) return (result, "Số lượng sản phẩm vượt giới hạn.");
                    result[item.ProductId] = existing + item.Quantity;
                }
                else result[item.ProductId] = item.Quantity;
            }
            return (result, null);
        }

        private static bool ValidContact(string? value, int maxLength) =>
            !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength;
    }

    // Giá, tổng tiền, userId và prescription URL từ client không được tin cậy.
    public record CreateOrderDto(string CustomerName, string Phone, string Address, List<OrderItemDto> Items);
    public record CreatePosDto(string? CustomerName, string? Phone, List<OrderItemDto> Items,
        string? PaymentMethod = "Cash", decimal? AmountReceived = null, bool NonCashPaymentConfirmed = false);
    public record OrderItemDto(int ProductId, int Quantity);
    public record StatusDto(string Status);
}
