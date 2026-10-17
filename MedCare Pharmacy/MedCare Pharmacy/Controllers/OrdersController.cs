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
                foreach (var (productId, quantity) in quantities.Items.OrderBy(x => x.Key))
                {
                    var product = products[productId];
                    if (product.Price <= 0) return BadRequest(new { message = $"Giá sản phẩm {product.Name} không hợp lệ." });

                    // Reserve stock atomically so concurrent checkouts cannot oversell.
                    var changed = await _context.Products
                        .Where(p => p.Id == productId && p.StockQuantity >= quantity)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity - quantity));
                    if (changed != 1)
                        return Conflict(new { message = $"Không đủ tồn kho cho sản phẩm {product.Name}." });

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
            if (!ValidContact(dto.CustomerName ?? "Khách vãng lai", 150) || !ValidContact(dto.Phone ?? "", 20))
                return BadRequest(new { message = "Thông tin khách hàng không hợp lệ." });

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
                foreach (var (productId, quantity) in quantities.Items.OrderBy(x => x.Key))
                {
                    var product = products[productId];
                    if (product.Price <= 0) return BadRequest(new { message = $"Giá sản phẩm {product.Name} không hợp lệ." });
                    var changed = await _context.Products
                        .Where(p => p.Id == productId && p.StockQuantity >= quantity)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity - quantity));
                    if (changed != 1)
                        return Conflict(new { message = $"Không đủ tồn kho cho sản phẩm {product.Name}." });

                    var lineTotal = product.Price * quantity;
                    if (lineTotal > MaxOrderAmount - subtotal)
                        return BadRequest(new { message = "Tổng tiền hóa đơn vượt giới hạn." });
                    subtotal += lineTotal;
                    lines.Add(new OrderDetail { ProductId = productId, Quantity = quantity, UnitPrice = product.Price });
                }

                if (subtotal > MaxOrderAmount / 1.05m)
                    return BadRequest(new { message = "Tổng tiền hóa đơn vượt giới hạn." });

                var order = new Order
                {
                    CustomerName = (dto.CustomerName ?? "Khách vãng lai").Trim(),
                    Phone = (dto.Phone ?? "").Trim(),
                    OrderType = "POS",
                    Status = "Completed",
                    TotalAmount = Math.Round(subtotal * 1.05m, 2, MidpointRounding.AwayFromZero),
                    CreatedAt = DateTime.UtcNow,
                    OrderDetails = lines
                };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { success = true, orderId = order.Id, totalAmount = order.TotalAmount, message = "Thanh toán POS thành công!" });
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

            if (next == "Cancelled")
            {
                foreach (var line in order.OrderDetails)
                {
                    var changed = await _context.Products
                        .Where(p => p.Id == line.ProductId && p.StockQuantity <= int.MaxValue - line.Quantity)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity + line.Quantity));
                    if (changed != 1)
                    {
                        await transaction.RollbackAsync();
                        return Conflict(new { message = "Không thể hoàn lại tồn kho cho đơn này. Hãy kiểm tra dữ liệu kho." });
                    }
                }
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
                    orderDetails = o.OrderDetails.Select(d => new
                    {
                        d.Id, d.ProductId, d.Quantity, d.UnitPrice,
                        product = d.Product == null ? null : new { d.Product.Id, d.Product.Name, d.Product.Price }
                    }).ToList()
                }).ToListAsync();
            return Ok(orders);
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
    public record CreatePosDto(string? CustomerName, string? Phone, List<OrderItemDto> Items);
    public record OrderItemDto(int ProductId, int Quantity);
    public record StatusDto(string Status);
}
