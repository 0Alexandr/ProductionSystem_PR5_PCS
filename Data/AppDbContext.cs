using Microsoft.EntityFrameworkCore;
using ProductionSystem.Models;

namespace ProductionSystem.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Product> Products { get; set; }
        public DbSet<ProductionLine> ProductionLines { get; set; }
        public DbSet<Material> Materials { get; set; }
        public DbSet<ProductMaterial> ProductMaterials { get; set; }
        public DbSet<WorkOrder> WorkOrders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProductMaterial>()
                .HasKey(pm => new { pm.ProductId, pm.MaterialId });

            modelBuilder.Entity<ProductMaterial>()
                .HasOne(pm => pm.Product)
                .WithMany(p => p.ProductMaterials)
                .HasForeignKey(pm => pm.ProductId);

            modelBuilder.Entity<ProductMaterial>()
                .HasOne(pm => pm.Material)
                .WithMany(m => m.ProductMaterials)
                .HasForeignKey(pm => pm.MaterialId);

            modelBuilder.Entity<WorkOrder>()
                .HasOne(wo => wo.ProductionLine)
                .WithMany(pl => pl.WorkOrders)
                .HasForeignKey(wo => wo.ProductionLineId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ProductionLine>()
                .HasOne(pl => pl.CurrentWorkOrder)
                .WithMany()
                .HasForeignKey(pl => pl.CurrentWorkOrderId)
                .OnDelete(DeleteBehavior.SetNull);

            // -------------------------------------------------------
            // Seed — ТОЛЬКО константные значения (без DateTime.Now)!
            // EF Core требует фиксированные значения в HasData.
            // -------------------------------------------------------

            modelBuilder.Entity<Material>().HasData(
                new Material { Id = 1, Name = "Сталь листовая", Quantity = 500, UnitOfMeasure = "кг", MinimalStock = 100 },
                new Material { Id = 2, Name = "Алюминий", Quantity = 80, UnitOfMeasure = "кг", MinimalStock = 150 },
                new Material { Id = 3, Name = "Болты М8", Quantity = 2000, UnitOfMeasure = "шт", MinimalStock = 500 },
                new Material { Id = 4, Name = "Масло машинное", Quantity = 30, UnitOfMeasure = "литр", MinimalStock = 50 },
                new Material { Id = 5, Name = "Пластик АБС", Quantity = 300, UnitOfMeasure = "кг", MinimalStock = 100 }
            );

            modelBuilder.Entity<Product>().HasData(
                new Product { Id = 1, Name = "Корпус насоса", Category = "Насосы", MinimalStock = 10, ProductionTimePerUnit = 45, Description = "Корпус центробежного насоса" },
                new Product { Id = 2, Name = "Крышка редуктора", Category = "Редукторы", MinimalStock = 5, ProductionTimePerUnit = 30, Description = "Крышка цилиндрического редуктора" },
                new Product { Id = 3, Name = "Кронштейн опорный", Category = "Конструкции", MinimalStock = 20, ProductionTimePerUnit = 15, Description = "Опорный кронштейн для оборудования" }
            );

            modelBuilder.Entity<ProductionLine>().HasData(
                new ProductionLine { Id = 1, Name = "Линия А — Механообработка", Status = "Active", EfficiencyFactor = 1.0f },
                new ProductionLine { Id = 2, Name = "Линия Б — Сборка", Status = "Stopped", EfficiencyFactor = 1.2f },
                new ProductionLine { Id = 3, Name = "Линия В — Покраска", Status = "Active", EfficiencyFactor = 0.8f }
            );

            modelBuilder.Entity<ProductMaterial>().HasData(
                new ProductMaterial { ProductId = 1, MaterialId = 1, QuantityNeeded = 5.5m },
                new ProductMaterial { ProductId = 1, MaterialId = 3, QuantityNeeded = 8m },
                new ProductMaterial { ProductId = 2, MaterialId = 2, QuantityNeeded = 3.2m },
                new ProductMaterial { ProductId = 2, MaterialId = 3, QuantityNeeded = 12m },
                new ProductMaterial { ProductId = 3, MaterialId = 1, QuantityNeeded = 2.0m },
                new ProductMaterial { ProductId = 3, MaterialId = 3, QuantityNeeded = 4m }
            );

            // WorkOrder seed — фиксированные даты (константа, не DateTime.Now)
            modelBuilder.Entity<WorkOrder>().HasData(
                new WorkOrder
                {
                    Id = 1,
                    ProductId = 1,
                    ProductionLineId = 1,
                    Quantity = 10,
                    StartDate = new DateTime(2025, 1, 10, 9, 0, 0),
                    EstimatedEndDate = new DateTime(2025, 1, 20, 9, 0, 0),
                    ActualStartDate = null,
                    Status = "Pending",
                    Progress = 0
                },
                new WorkOrder
                {
                    Id = 2,
                    ProductId = 2,
                    ProductionLineId = null,
                    Quantity = 5,
                    StartDate = new DateTime(2025, 1, 15, 9, 0, 0),
                    EstimatedEndDate = new DateTime(2025, 1, 18, 9, 0, 0),
                    ActualStartDate = null,
                    Status = "Pending",
                    Progress = 0
                }
            );
        }
    }
}