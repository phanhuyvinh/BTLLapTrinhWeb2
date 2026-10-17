namespace MedCare_Pharmacy.Models.Entities;

public class PurchaseReceipt
{
    public int Id { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public int? CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }
    public string? Note { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<PurchaseReceiptLine> Lines { get; set; } = [];
}
