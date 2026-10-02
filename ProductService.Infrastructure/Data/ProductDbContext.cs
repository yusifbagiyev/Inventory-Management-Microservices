using Microsoft.EntityFrameworkCore;
using ProductService.Domain.Entities;

namespace ProductService.Infrastructure.Data
{
    public class ProductDbContext : DbContext
    {
        public ProductDbContext(DbContextOptions<ProductDbContext> options) : base(options) { }

        public DbSet<Product> Products => Set<Product>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Department> Departments => Set<Department>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.InventoryCode).IsUnique();
                entity.Property(e => e.Model).HasMaxLength(50);
                entity.Property(e => e.Vendor).HasMaxLength(30);
                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp without time zone");
                entity.Property(e => e.UpdatedAt)
                      .HasColumnType("timestamp without time zone");

                entity.HasOne(e => e.Category)
                    .WithMany(c => c.Products)
                    .HasForeignKey(e => e.CategoryId);

                entity.HasOne(e => e.Department)
                    .WithMany(d => d.Products)
                    .HasForeignKey(e => e.DepartmentId);
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(20).IsRequired();
                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp without time zone");
                entity.Property(e => e.UpdatedAt)
                      .HasColumnType("timestamp without time zone");
            });

            modelBuilder.Entity<Department>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp without time zone");
                entity.Property(e => e.UpdatedAt)
                      .HasColumnType("timestamp without time zone");
            });

            SeedData(modelBuilder);
        }

        protected void SeedData(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Category>().HasData(
                new { Id = 1, Name = "Electronics", Description = "Electronic devices and equipment", IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = (DateTime?)null },
                new { Id = 2, Name = "Furniture", Description = "Office and warehouse furniture", IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = (DateTime?)null },
                new { Id = 3, Name = "Vehicles", Description = "Transportation vehicles and equipment", IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = (DateTime?)null },
                new { Id = 4, Name = "Tools", Description = "Hand and power tools", IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = (DateTime?)null },
                new { Id = 5, Name = "Safety Equipment", Description = "Personal protective equipment", IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = (DateTime?)null }
            );

            modelBuilder.Entity<Department>().HasData(
                new { Id = 1, Name = "Warehouse A", Description = "Main storage warehouse", DepartmentHead = "John Smith", IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = (DateTime?)null },
                new { Id = 2, Name = "Warehouse B", Description = "Secondary storage facility", DepartmentHead = "Sarah Johnson", IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = (DateTime?)null },
                new { Id = 3, Name = "Office", Description = "Administrative office", DepartmentHead = "Michael Brown", IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = (DateTime?)null },
                new { Id = 4, Name = "Loading Dock", Description = "Shipping and receiving area", DepartmentHead = "David Wilson", IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = (DateTime?)null },
                new { Id = 5, Name = "Maintenance", Description = "Equipment maintenance department", DepartmentHead = "Emily Davis", IsActive = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = (DateTime?)null }
            );

            modelBuilder.Entity<Product>().HasData(
                new
                {
                    Id = 1,
                    InventoryCode = 1001,
                    Model = "ThinkPad X1",
                    Vendor = "Lenovo",
                    Worker = "John Doe",
                    ImageUrl = (string?)null,
                    Description = "Business laptop",
                    IsWorking = true,
                    IsActive = true,
                    IsNewItem = false,
                    CategoryId = 1,
                    DepartmentId = 3,
                    CreatedAt = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = (DateTime?)null
                },
                new
                {
                    Id = 2,
                    InventoryCode = 1002,
                    Model = "Forklift 3000",
                    Vendor = "Toyota",
                    Worker = "Mike Johnson",
                    ImageUrl = (string?)null,
                    Description = "Electric forklift",
                    IsWorking = true,
                    IsActive = true,
                    IsNewItem = false,
                    CategoryId = 3,
                    DepartmentId = 1,
                    CreatedAt = new DateTime(2024, 1, 20, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = (DateTime?)null
                },
                new
                {
                    Id = 3,
                    InventoryCode = 1003,
                    Model = "Office Desk Pro",
                    Vendor = "IKEA",
                    Worker = (string?)null,
                    ImageUrl = (string?)null,
                    Description = "Height adjustable desk",
                    IsWorking = true,
                    IsActive = true,
                    IsNewItem = true,
                    CategoryId = 2,
                    DepartmentId = 3,
                    CreatedAt = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = (DateTime?)null
                },
                new
                {
                    Id = 4,
                    InventoryCode = 1004,
                    Model = "Impact Drill",
                    Vendor = "DeWalt",
                    Worker = "Sarah Smith",
                    ImageUrl = (string?)null,
                    Description = "Cordless impact drill",
                    IsWorking = true,
                    IsActive = true,
                    IsNewItem = false,
                    CategoryId = 4,
                    DepartmentId = 5,
                    CreatedAt = new DateTime(2024, 2, 10, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = (DateTime?)null
                },
                new
                {
                    Id = 5,
                    InventoryCode = 1005,
                    Model = "Safety Helmet",
                    Vendor = "3M",
                    Worker = (string?)null,
                    ImageUrl = (string?)null,
                    Description = "Hard hat with face shield",
                    IsWorking = true,
                    IsActive = true,
                    IsNewItem = true,
                    CategoryId = 5,
                    DepartmentId = 1,
                    CreatedAt = new DateTime(2024, 2, 15, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = (DateTime?)null
                },
                new
                {
                    Id = 6,
                    InventoryCode = 1006,
                    Model = "Pallet Jack",
                    Vendor = "Crown",
                    Worker = "Tom Wilson",
                    ImageUrl = (string?)null,
                    Description = "Manual pallet jack",
                    IsWorking = false,
                    IsActive = true,
                    IsNewItem = false,
                    CategoryId = 3,
                    DepartmentId = 4,
                    CreatedAt = new DateTime(2024, 2, 20, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = (DateTime?)null
                }
            );
        }
    }
}