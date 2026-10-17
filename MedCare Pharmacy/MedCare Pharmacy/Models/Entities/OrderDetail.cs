namespace MedCare_Pharmacy.Models.Entities
{
    public class OrderDetail
    {
        public int Id { get; set; }

        // Sửa kiểu dữ liệu OrderId thành int (không để string)
        public int OrderId { get; set; }
        public Order? Order { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
