using Microsoft.EntityFrameworkCore;
using RouteService.Domain.Entities;

namespace RouteService.Infrastructure.Data
{
    public class RouteDbContext : DbContext
    {
        public RouteDbContext(DbContextOptions<RouteDbContext> options) : base(options) { }

        public DbSet<InventoryRoute> InventoryRoutes => Set<InventoryRoute>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InventoryRoute>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.OwnsOne(e => e.ProductSnapshot, snapshot =>
                {
                    snapshot.Property(s => s.ProductId).HasColumnName("ProductId");
                    snapshot.Property(s => s.InventoryCode).HasColumnName("InventoryCode");
                    snapshot.Property(s => s.Model).HasColumnName("Model").HasMaxLength(100);
                    snapshot.Property(s => s.Vendor).HasColumnName("Vendor").HasMaxLength(100);
                    snapshot.Property(s => s.CategoryName).HasColumnName("CategoryName").HasMaxLength(100);
                    snapshot.Property(s => s.IsWorking).HasColumnName("IsWorking");
                    // The index has to be declared on the owned type, not on the route
                    snapshot.HasIndex(s => s.ProductId).HasDatabaseName("IX_InventoryRoutes_ProductId");
                });

                entity.Property(e => e.RouteType).HasConversion<string>();
                entity.Property(e => e.FromDepartmentName).HasMaxLength(100);
                entity.Property(e => e.ToDepartmentName).HasMaxLength(100);
                entity.Property(e => e.FromWorker).HasMaxLength(100);
                entity.Property(e => e.ToWorker).HasMaxLength(100);
                entity.Property(e => e.Notes).HasMaxLength(500);
                entity.Property(e=>e.CreatedAt)
                      .HasColumnType("timestamp without time zone");
                entity.Property(e=>e.CompletedAt)
                      .HasColumnType("timestamp without time zone");

                entity.HasIndex(e => e.FromDepartmentId).HasDatabaseName("IX_InventoryRoutes_FromDepartmentId");
                entity.HasIndex(e => e.ToDepartmentId).HasDatabaseName("IX_InventoryRoutes_ToDepartmentId");
                entity.HasIndex(e => e.CreatedAt);
            });
        }
    }
}