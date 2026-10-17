namespace MedCare_Pharmacy.Models.Entities;

public class PurchaseReceiptLine
{
    public int Id { get; set; }
    public int PurchaseReceiptId { get; set; }
    public PurchaseReceipt? PurchaseReceipt { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int InventoryBatchId { get; set; }
    public InventoryBatch? InventoryBatch { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
}
