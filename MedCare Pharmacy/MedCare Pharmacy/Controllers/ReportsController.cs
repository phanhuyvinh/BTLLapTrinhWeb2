using MedCare_Pharmacy.Models.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedCare_Pharmacy.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReportsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/reports/revenue?from=2025-01-01&to=2025-01-31
        [HttpGet("revenue")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetRevenue([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var fromDate = (from ?? DateTime.Today.AddDays(-6)).Date;
            var toDate = (to ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);

            var orders = await _context.Orders
                .Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate
                            && o.Status != "Cancelled" && o.PaymentStatus == "Paid")
                .ToListAsync();

            // Doanh thu theo ngày
            var byDay = orders
                .GroupBy(o => o.CreatedAt.Date)
                .Select(g => new
                {
                    date = g.Key,
                    label = g.Key.ToString("dd/MM"),
                    revenue = g.Sum(x => x.TotalAmount),
                    orderCount = g.Count()
                })
                .OrderBy(x => x.date)
                .ToList();

            var totalRevenue = orders.Sum(o => o.TotalAmount);
            var totalOrders = orders.Count;
            var avgOrder = totalOrders > 0 ? totalRevenue / totalOrders : 0;

            // Tổng số lượng sản phẩm bán
            var orderIds = orders.Select(o => o.Id).ToList();
            var totalItems = await _context.OrderDetails
                .Where(d => orderIds.Contains(d.OrderId))
                .SumAsync(d => (int?)d.Quantity) ?? 0;

            return Ok(new
            {
                from = fromDate,
                to = toDate.Date,
                totalRevenue,
                totalOrders,
                totalItems,
                avgOrder,
                byDay
            });
        }

        // GET: api/reports/best-selling?from=...&to=...&top=10
        [HttpGet("best-selling")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetBestSelling(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int top = 10)
        {
            var fromDate = (from ?? DateTime.Today.AddDays(-30)).Date;
            var toDate = (to ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);

            var query = from d in _context.OrderDetails
                        join o in _context.Orders on d.OrderId equals o.Id
                        join p in _context.Products on d.ProductId equals p.Id
                        where o.CreatedAt >= fromDate
                              && o.CreatedAt <= toDate
                              && o.Status != "Cancelled" && o.PaymentStatus == "Paid"
                        group d by new { p.Id, p.Name } into g
                        select new
                        {
                            productId = g.Key.Id,
                            name = g.Key.Name,
                            qty = g.Sum(x => x.Quantity),
                            revenue = g.Sum(x => x.Quantity * x.UnitPrice)
                        };

            var list = await query
                .OrderByDescending(x => x.qty)
                .Take(top > 0 ? top : 10)
                .ToListAsync();

            return Ok(list);
        }

        // GET: api/reports/dashboard  (số liệu tổng quan Admin)
        [HttpGet("dashboard")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetDashboard()
        {
            var totalRevenue = await _context.Orders
                .Where(o => o.Status != "Cancelled" && o.PaymentStatus == "Paid")
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

            var totalOrders = await _context.Orders.CountAsync();
            var totalProducts = await _context.Products.CountAsync();
            var totalUsers = await _context.Users.CountAsync();

            var pendingOrders = await _context.Orders
                .CountAsync(o => o.Status == "Pending");

            var today = DateTime.Today;
            var (lowStock, outOfStock) = await GetStockAlertCountsAsync(today);
            var nearExpiry = await _context.InventoryBatches
                .CountAsync(b => b.ExpDate >= today
                                 && b.ExpDate <= today.AddDays(90)
                                 && b.Quantity > 0);

            var alerts = lowStock + outOfStock + nearExpiry;

            return Ok(new
            {
                totalRevenue,
                totalOrders,
                totalProducts,
                totalUsers,
                pendingOrders,
                lowStock,
                outOfStock,
                nearExpiry,
                alerts
            });
        }
        // GET: api/reports/pharmacist-dashboard
        [HttpGet("pharmacist-dashboard")]
        [Authorize(Roles = "pharmacist,admin")]
        public async Task<IActionResult> GetPharmacistDashboard()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var todayOrders = await _context.Orders
                .Where(o => o.CreatedAt >= today && o.CreatedAt < tomorrow
                            && o.Status != "Cancelled")
                .ToListAsync();

            var revenueToday = todayOrders.Where(o => o.PaymentStatus == "Paid").Sum(o => o.TotalAmount);
            var posCount = todayOrders.Count(o => o.OrderType == "POS");
            var onlineCount = todayOrders.Count(o => o.OrderType == "Online");

            var (lowStock, outOfStock) = await GetStockAlertCountsAsync(today);
            var nearExpiry = await _context.InventoryBatches
                .CountAsync(b => b.ExpDate >= today
                                 && b.ExpDate <= today.AddDays(90)
                                 && b.Quantity > 0);
            var alerts = lowStock + outOfStock + nearExpiry;

            // Tin nhắn chưa đọc gửi tới các user role pharmacist
            var pharmacistIds = await _context.Users
                .Where(u => u.Role.ToLower() == "pharmacist")
                .Select(u => u.Id)
                .ToListAsync();

            var chatUnread = await _context.ChatMessages
                .CountAsync(m => pharmacistIds.Contains(m.ReceiverId) && !m.IsRead);

            return Ok(new
            {
                revenueToday,
                posCount,
                onlineCount,
                alerts,
                chatUnread
            });
        }

        private async Task<(int LowStock, int OutOfStock)> GetStockAlertCountsAsync(DateTime today)
        {
            var products = await _context.Products.AsNoTracking()
                .Select(p => new { p.Id, p.StockQuantity }).ToListAsync();
            var sellable = await _context.InventoryBatches.AsNoTracking()
                .Where(b => b.Quantity > 0 && b.ExpDate >= today)
                .GroupBy(b => b.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(b => b.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Quantity);

            var available = products.Select(p => Math.Min(p.StockQuantity, sellable.GetValueOrDefault(p.Id))).ToList();
            return (available.Count(x => x > 0 && x <= 20), available.Count(x => x <= 0));
        }
    }
}
