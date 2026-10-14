using MedCare_Pharmacy.Models.Data;
using MedCare_Pharmacy.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedCare_Pharmacy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public OrdersController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/orders (Lấy toàn bộ danh sách đơn hàng cho Admin / Dược sĩ)
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var orders = await _context.Orders
                .Include(o => o.OrderDetails)
                .ThenInclude(d => d.Product)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new
                {
                    o.Id,
                    o.UserId,
                    o.CustomerName,
                    o.Phone,
                    o.Address,
                    o.OrderType,
                    o.Status,
                    o.PrescriptionUrl,
                    o.TotalAmount,
                    o.CreatedAt,
                    orderDetails = o.OrderDetails.Select(d => new
                    {
                        d.Id,
                        d.OrderId,
                        d.ProductId,
                        d.Quantity,
                        d.UnitPrice,
                        product = d.Product != null ? new
                        {
                            d.Product.Id,
                            d.Product.Name,
                            d.Product.Price,
                            d.Product.ImageUrl,
                            d.Product.Unit
                        } : null
                    }).ToList()
                })
                .ToListAsync();

            return Ok(orders);
        }

        // POST: api/orders (Khách hàng tạo đơn hàng Online)
        // POST: api/orders (Khách hàng tạo đơn hàng Online)
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
        {
            if (dto == null || dto.Items == null || dto.Items.Count == 0)
            {
                return BadRequest(new { message = "Đơn hàng phải có ít nhất một sản phẩm!" });
            }

            var order = new Order
            {
                UserId = dto.UserId,
                CustomerName = dto.CustomerName,
                Phone = dto.Phone,
                Address = dto.Address,
                OrderType = "Online",
                Status = "Pending",
                PrescriptionUrl = dto.PrescriptionUrl,
                TotalAmount = dto.TotalAmount,
                CreatedAt = DateTime.Now
            };

            foreach (var item in dto.Items)
            {
                if (item.ProductId <= 0)
                {
                    return BadRequest(new { message = "ID sản phẩm không hợp lệ!" });
                }

                // Kiểm tra sản phẩm có tồn tại trong Database không
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null)
                {
                    return BadRequest(new { message = $"Sản phẩm ID {item.ProductId} không tồn tại!" });
                }

                order.OrderDetails.Add(new OrderDetail
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                });
            }

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, orderId = order.Id, message = "Đặt hàng thành công!" });
        }

        // POST: api/orders/pos (Dược sĩ tạo hóa đơn bán tại quầy POS -> Tự động trừ tồn kho SQL)
        [HttpPost("pos")]
        public async Task<IActionResult> CreatePosOrder([FromBody] CreatePosDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = new Order
                {
                    CustomerName = dto.CustomerName ?? "Khách vãng lai",
                    Phone = dto.Phone ?? "",
                    OrderType = "POS",
                    Status = "Completed",
                    TotalAmount = dto.TotalAmount,
                    CreatedAt = DateTime.Now
                };

                foreach (var item in dto.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null || product.StockQuantity < item.Quantity)
                    {
                        return BadRequest(new { message = $"Sản phẩm ID {item.ProductId} không đủ số lượng tồn kho!" });
                    }

                    // Tự động trừ số lượng tồn kho trong SQL Server
                    product.StockQuantity -= item.Quantity;

                    order.OrderDetails.Add(new OrderDetail
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice
                    });
                }

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, orderId = order.Id, message = "Thanh toán POS thành công!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Lỗi xử lý hóa đơn POS: " + ex.Message });
            }
        }

        // PUT: api/orders/5/status (Dược sĩ/Admin cập nhật trạng thái đơn)
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] StatusDto dto)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng!" });

            order.Status = dto.Status;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã cập nhật trạng thái đơn hàng!" });
        }

        // GET: api/orders/by-user?userId=5  (Khách hàng xem đơn của mình)
        [HttpGet("by-user")]
        public async Task<IActionResult> GetByUser([FromQuery] int? userId)
        {
            if (userId == null || userId <= 0)
            {
                return BadRequest(new { message = "Thiếu userId" });
            }

            var orders = await _context.Orders
                .Include(o => o.OrderDetails)
                .ThenInclude(d => d.Product)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new
                {
                    o.Id,
                    o.UserId,
                    o.CustomerName,
                    o.Phone,
                    o.Address,
                    o.Status,
                    o.TotalAmount,
                    o.CreatedAt,
                    o.PrescriptionUrl,
                    OrderDetails = o.OrderDetails.Select(d => new
                    {
                        d.Id,
                        d.ProductId,
                        d.Quantity,
                        d.UnitPrice,
                        Product = d.Product != null ? new { d.Product.Id, d.Product.Name, d.Product.Price } : null
                    }).ToList()
                })
                .ToListAsync();

            return Ok(orders);
        }
    }

    public record CreateOrderDto(int? UserId, string CustomerName, string Phone, string Address, string? PrescriptionUrl, decimal TotalAmount, List<OrderItemDto> Items);
    public record CreatePosDto(string? CustomerName, string? Phone, decimal TotalAmount, List<OrderItemDto> Items);
    public record OrderItemDto(int ProductId, int Quantity, decimal UnitPrice);
    public record StatusDto(string Status);
}