namespace MedCare_Pharmacy.Models.Entities
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ActiveIngredient { get; set; }
        public decimal Price { get; set; }

        public int StockQuantity { get; set; }

        public string Unit { get; set; } = "Hộp";

        // Sửa 2 dòng này thành string? để chấp nhận giá trị NULL từ Database
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }

        // Khóa ngoại
        public int? CategoryId { get; set; }
        public Category? Category { get; set; }

        public int? SupplierId { get; set; }
        public Supplier? Supplier { get; set; }
    }
}