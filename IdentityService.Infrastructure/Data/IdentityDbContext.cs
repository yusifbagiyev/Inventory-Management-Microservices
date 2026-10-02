using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SharedServices.Identity;

namespace IdentityService.Infrastructure.Data
{
    public class IdentityDbContext : IdentityDbContext<User, Role, int>
    {
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }

        public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<User>(entity =>
            {
                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp without time zone");
                entity.Property(e => e.LastLoginAt)
                      .HasColumnType("timestamp without time zone");
            });

            builder.Entity<RolePermission>(entity =>
            {
                entity.HasKey(r => new { r.RoleId, r.PermissionId });

                entity.HasOne(r => r.Role)
                    .WithMany(r => r.RolePermissions)
                    .HasForeignKey(r => r.RoleId);

                entity.HasOne(r => r.Permission)
                    .WithMany(p => p.RolePermissions)
                    .HasForeignKey(rp => rp.PermissionId);
            });

            builder.Entity<UserPermission>(entity =>
            {
                entity.Property(u => u.GrantedAt)
                      .HasColumnType("timestamp without time zone");

                entity.HasKey(up => new { up.UserId, up.PermissionId });

                entity.HasOne(up => up.User)
                    .WithMany(u=>u.UserPermissions)
                    .HasForeignKey(up => up.UserId);

                entity.HasOne(up => up.Permission)
                    .WithMany(p=>p.UserPermissions)
                    .HasForeignKey(up => up.PermissionId);
            });

            builder.Entity<RefreshToken>(entity =>
            {
                entity.Property(u => u.CreatedAt)
                      .HasColumnType("timestamp without time zone");
                entity.Property(u => u.ExpiresAt)
                      .HasColumnType("timestamp without time zone");
                entity.Property(u => u.RevokedAt)
                      .HasColumnType("timestamp without time zone");

                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Token).IsUnique();

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            SeedData(builder);
        }

        private void SeedData(ModelBuilder builder)
        {
            // Seed values must be constants, otherwise every new migration sees a model change.
            var user = new User
            {
                Id = 1,
                UserName = "yusifbv24",
                FirstName = "Yusif",
                LastName = "Bagiyev",
                NormalizedUserName = "YUSIFBV24",
                Email = "yusifbv24@gmail.com",
                NormalizedEmail = "YUSIFBV24@GMAIL.COM",
                SecurityStamp = "STATIC_SECURITY_STAMP_123",
                ConcurrencyStamp = "STATIC_CONCURRENCY_STAMP_123",
                PasswordHash = "AQAAAAIAAYagAAAAEBdsDYTjRSp7rXe+WukGaCJhRB9exxLE+qm/liJNTSQIsqWO+prZlpvo6khA0uDi2Q==",
                LockoutEnabled = false,
                EmailConfirmed = true,
                AccessFailedCount = 0,
                CreatedAt = new DateTime(2025, 8, 1, 0, 0, 0)
            };

            builder.Entity<User>().HasData(user);

            builder.Entity<IdentityUserRole<int>>().HasData(
                new IdentityUserRole<int> 
                { 
                    UserId = 1, RoleId = 1 ,
                }
            );

            var roles = new[]
            {
                new Role { Id = 1, Name = "Admin", NormalizedName = "ADMIN", ConcurrencyStamp = "ADMIN_STAMP_123" },
                new Role { Id = 2, Name = "Operator", NormalizedName = "OPERATOR", ConcurrencyStamp = "OPERATOR_STAMP_123" },
                new Role { Id = 3, Name = "User", NormalizedName = "USER", ConcurrencyStamp = "USER_STAMP_123" }
            };
            builder.Entity<Role>().HasData(roles);

            var permissions = new[]
            {
                new Permission { Id = 1, Name = AllPermissions.RouteView, Category = "Route", Description = "View routes" },
                new Permission { Id = 2, Name = AllPermissions.RouteCreate, Category = "Route", Description = "Create routes" },
                new Permission { Id = 3, Name = AllPermissions.RouteCreateDirect, Category = "Route", Description = "Create routes (requires approval)" },
                new Permission { Id = 4, Name = AllPermissions.RouteUpdate, Category = "Route", Description = "Update routes (requires approval)" },
                new Permission { Id = 5, Name = AllPermissions.RouteUpdateDirect, Category = "Route", Description = "Update routes directly" },
                new Permission { Id = 6, Name = AllPermissions.RouteDelete, Category = "Route", Description = "Delete routes (requires approval)" },
                new Permission { Id = 7, Name = AllPermissions.RouteDeleteDirect, Category = "Route", Description = "Delete routes directly" },
                new Permission { Id = 8, Name = AllPermissions.RouteComplete, Category = "Route", Description = "Complete routes" },

                new Permission { Id = 9, Name = AllPermissions.ProductView, Category = "Product", Description = "View products" },
                new Permission { Id = 10, Name = AllPermissions.ProductCreate, Category = "Product", Description = "Create products (requires approval)" },
                new Permission { Id = 11, Name = AllPermissions.ProductCreateDirect, Category = "Product", Description = "Create products directly" },
                new Permission { Id = 12, Name = AllPermissions.ProductUpdate, Category = "Product", Description = "Update products (requires approval)" },
                new Permission { Id = 13, Name = AllPermissions.ProductUpdateDirect, Category = "Product", Description = "Update products directly" },
                new Permission { Id = 14, Name = AllPermissions.ProductDelete, Category = "Product", Description = "Delete products (requires approval)" },
                new Permission { Id = 15, Name = AllPermissions.ProductDeleteDirect, Category = "Product", Description = "Delete products directly" },
            };
            builder.Entity<Permission>().HasData(permissions);

            var rolePermissions = new List<RolePermission>();

            // Admin gets every permission.
            for (int i = 1; i <= 15; i++)
            {
                rolePermissions.Add(new RolePermission { RoleId = 1, PermissionId = i });
            }

            // Operator was meant to get view plus the approval-request permissions.
            // From id 8 on the ids are one too low, so it gets the permissions named below instead.
            rolePermissions.AddRange(new[]
            {
                new RolePermission { RoleId = 2, PermissionId = 1 }, // RouteView
                new RolePermission { RoleId = 2, PermissionId = 2 }, // RouteCreate
                new RolePermission { RoleId = 2, PermissionId = 4 }, // RouteUpdate
                new RolePermission { RoleId = 2, PermissionId = 6 }, // RouteDelete
                new RolePermission { RoleId = 2, PermissionId = 8 }, // RouteComplete
                new RolePermission { RoleId = 2, PermissionId = 9 }, // ProductView
                new RolePermission { RoleId = 2, PermissionId = 11 }, // ProductCreateDirect
                new RolePermission { RoleId = 2, PermissionId = 13 }, // ProductUpdateDirect
            });

            // User can only view.
            rolePermissions.AddRange(new[]
            {
                new RolePermission { RoleId = 3, PermissionId = 1 }, // RouteView
                new RolePermission { RoleId = 3, PermissionId = 9 }  // ProductView
            });

            builder.Entity<RolePermission>().HasData(rolePermissions);

        }
    }
}