namespace MedCare_Pharmacy.Models.Entities
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ActiveIngredient { get; set; }
        public decimal Price { get; set; }

        public string Code { get; set; } = string.Empty;

        // Thêm trường Số lượng tồn kho
        public int StockQuantity { get; set; }

        public string Unit { get; set; } = "Hộp";
        public string Description { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;

        // Khóa ngoại
        public int? CategoryId { get; set; }
        public Category? Category { get; set; }

        public int? SupplierId { get; set; }
        public Supplier? Supplier { get; set; }
    }
}