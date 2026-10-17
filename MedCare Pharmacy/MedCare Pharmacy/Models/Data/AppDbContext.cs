using MedCare_Pharmacy.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedCare_Pharmacy.Models.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Category> Categories { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<InventoryBatch> InventoryBatches { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Prescription> Prescriptions { get; set; }
        public DbSet<PurchaseReceipt> PurchaseReceipts { get; set; }
        public DbSet<PurchaseReceiptLine> PurchaseReceiptLines { get; set; }
        public DbSet<InventoryMovement> InventoryMovements { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Category>().ToTable("Category");
            modelBuilder.Entity<ChatMessage>().ToTable("ChatMessage");
            modelBuilder.Entity<InventoryBatch>().ToTable("InventoryBatch");
            modelBuilder.Entity<Order>().ToTable("Order");
            modelBuilder.Entity<OrderDetail>().ToTable("OrderDetail");
            modelBuilder.Entity<Product>().ToTable("Product");
            modelBuilder.Entity<Supplier>().ToTable("Supplier");
            modelBuilder.Entity<User>().ToTable("User");
            modelBuilder.Entity<Prescription>().ToTable("Prescription");
            modelBuilder.Entity<PurchaseReceipt>().ToTable("PurchaseReceipt");
            modelBuilder.Entity<PurchaseReceiptLine>().ToTable("PurchaseReceiptLine");
            modelBuilder.Entity<InventoryMovement>().ToTable("InventoryMovement");
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Cấu hình rõ ràng mối quan hệ Order - OrderDetail
            modelBuilder.Entity<Order>()
                .HasMany(o => o.OrderDetails)
                .WithOne(d => d.Order)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Product>()
                .Property(p => p.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Order>()
                .Property(o => o.TotalAmount)
                .HasPrecision(18, 2);
            modelBuilder.Entity<Order>().Property(o => o.AmountReceived).HasPrecision(18, 2);
            modelBuilder.Entity<Order>().Property(o => o.ChangeAmount).HasPrecision(18, 2);

            modelBuilder.Entity<OrderDetail>()
                .Property(d => d.UnitPrice)
                .HasPrecision(18, 2);

            // Transaction history must survive product maintenance/deletion attempts.
            modelBuilder.Entity<OrderDetail>()
                .HasOne(d => d.Product)
                .WithMany()
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryBatch>()
                .HasOne(b => b.Product)
                .WithMany()
                .HasForeignKey(b => b.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryBatch>()
                .Property(b => b.ImportPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<PurchaseReceipt>().Property(r => r.TotalAmount).HasPrecision(18, 2);
            modelBuilder.Entity<PurchaseReceiptLine>().Property(l => l.UnitCost).HasPrecision(18, 2);
            modelBuilder.Entity<PurchaseReceiptLine>().Property(l => l.LineTotal).HasPrecision(18, 2);
            modelBuilder.Entity<PurchaseReceipt>()
                .HasOne(r => r.Supplier).WithMany().HasForeignKey(r => r.SupplierId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<PurchaseReceipt>()
                .HasOne(r => r.CreatedBy).WithMany().HasForeignKey(r => r.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<PurchaseReceiptLine>()
                .HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<PurchaseReceiptLine>()
                .HasOne(l => l.InventoryBatch).WithMany().HasForeignKey(l => l.InventoryBatchId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<PurchaseReceiptLine>()
                .HasOne(l => l.PurchaseReceipt).WithMany(r => r.Lines).HasForeignKey(l => l.PurchaseReceiptId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<InventoryMovement>()
                .HasOne(m => m.Product).WithMany().HasForeignKey(m => m.ProductId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<InventoryMovement>()
                .HasOne(m => m.InventoryBatch).WithMany().HasForeignKey(m => m.InventoryBatchId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<InventoryMovement>()
                .HasOne(m => m.PurchaseReceipt).WithMany().HasForeignKey(m => m.PurchaseReceiptId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<InventoryMovement>()
                .HasOne(m => m.Order).WithMany().HasForeignKey(m => m.OrderId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<InventoryMovement>()
                .HasOne(m => m.PerformedBy).WithMany().HasForeignKey(m => m.PerformedByUserId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<PurchaseReceipt>().HasIndex(r => r.ReceiptNumber).IsUnique();
            modelBuilder.Entity<InventoryMovement>().HasIndex(m => m.CreatedAt);
        }
    }
}
