using System;
using System.Collections.Generic;

namespace MedCare_Pharmacy.Models.Entities
{
    public class Order
    {
        public int Id { get; set; }

        // Thêm trường UserId (Khóa ngoại trỏ tới User, có thể null nếu là khách vãng lai)
        public int? UserId { get; set; }

        public string CustomerName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        // Thêm trường Địa chỉ
        public string Address { get; set; } = string.Empty;

        // Thêm trường Loại đơn hàng ("Online" hoặc "POS")
        public string OrderType { get; set; } = "Online";

        // Trạng thái đơn ("Pending", "Approved", "Cancelled", "Completed")
        public string Status { get; set; } = "Pending";

        public string PaymentMethod { get; set; } = "COD";
        public string PaymentStatus { get; set; } = "Unpaid";
        public decimal? AmountReceived { get; set; }
        public decimal ChangeAmount { get; set; }
        public DateTime? PaidAt { get; set; }

        public string? PrescriptionUrl { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Quan hệ 1 - Nhiều với OrderDetail
        public List<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    }
}
