namespace MedCare_Pharmacy.Models.Entities;

public class InventoryMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int? InventoryBatchId { get; set; }
    public InventoryBatch? InventoryBatch { get; set; }
    public int? PurchaseReceiptId { get; set; }
    public PurchaseReceipt? PurchaseReceipt { get; set; }
    public int? OrderId { get; set; }
    public Order? Order { get; set; }
    public int? PerformedByUserId { get; set; }
    public User? PerformedBy { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int StockBefore { get; set; }
    public int StockAfter { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
