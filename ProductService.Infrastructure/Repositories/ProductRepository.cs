using Microsoft.EntityFrameworkCore;
using ProductService.Domain.Common;
using ProductService.Domain.Entities;
using ProductService.Domain.Repositories;
using ProductService.Infrastructure.Data;
using SharedServices.Services;

namespace ProductService.Infrastructure.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly ProductDbContext _context;

        public ProductRepository(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Department)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        }


        public async Task<PagedResult<Product>> GetAllAsync(
            int pageNumber,
            int pageSize,
            string? search,
            DateTime? startDate,
            DateTime? endDate,
            bool? status,
            bool? availability,
            int? categoryId = null,
            int? departmentId = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Department)
                .AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);

            if (departmentId.HasValue)
                query = query.Where(p => p.DepartmentId == departmentId.Value);

            if (status.HasValue)
                query = query.Where(p => p.IsWorking == status.Value);

            if (availability.HasValue)
                query = query.Where(p => p.IsActive == availability.Value);

            if (startDate.HasValue)
            {
                query = query.Where(r => r.CreatedAt >= startDate);
            }

            if (endDate.HasValue)
            {
                var EndDate = endDate.Value.AddDays(1).AddTicks(-1);
                query = query.Where(r => r.CreatedAt <= EndDate);
            }

            IEnumerable<Product> items;
            int totalCount;

            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim();

                // ILIKE narrows the rows in SQL but misses Azerbaijani letter variants.
                var broadQuery = query.Where(r =>
                    EF.Functions.ILike(r.InventoryCode.ToString(), $"%{search}%") ||
                    EF.Functions.ILike(r.Vendor, $"%{search}%") ||
                    EF.Functions.ILike(r.Model, $"%{search}%") ||
                    (r.Category != null && EF.Functions.ILike(r.Category.Name, $"%{search}%")) ||
                    (r.Department != null && EF.Functions.ILike(r.Department.Name, $"%{search}%")) ||
                    EF.Functions.ILike(r.Description ?? "", $"%{search}%") ||
                    EF.Functions.ILike(r.Worker ?? "", $"%{search}%")
                );

                var allFilteredItems = await broadQuery
                    .OrderByDescending(r => r.CreatedAt)
                    .ThenByDescending(r => r.UpdatedAt)
                    .ToListAsync(cancellationToken);

                // The Azerbaijani-aware match runs in memory, so paging has to follow it there.
                items = allFilteredItems.Where(r =>
                    SearchHelper.ContainsAzerbaijani(r.InventoryCode.ToString(), search) ||
                    SearchHelper.ContainsAzerbaijani(r.Vendor, search) ||
                    SearchHelper.ContainsAzerbaijani(r.Model, search) ||
                    SearchHelper.ContainsAzerbaijani(r.Category?.Name, search) ||
                    SearchHelper.ContainsAzerbaijani(r.Department?.Name, search) ||
                    SearchHelper.ContainsAzerbaijani(r.Description, search) ||
                    SearchHelper.ContainsAzerbaijani(r.Worker, search)
                ).ToList();

                totalCount = items.Count();

                items = items
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
            }
            else
            {
                totalCount = await query.CountAsync(cancellationToken);

                items = await query
                    .OrderByDescending(r => r.CreatedAt)
                    .ThenByDescending(r => r.UpdatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);
            }

            return new PagedResult<Product>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }


        public async Task<IEnumerable<Product>> GetByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Department)
                .Where(p => p.CategoryId == categoryId)
                .ToListAsync(cancellationToken);
        }


        public async Task<IEnumerable<Product>> GetByDepartmentIdAsync(int departmentId, CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Department)
                .Where(p => p.DepartmentId == departmentId)
                .ToListAsync(cancellationToken);
        }


        public async Task<Product?> GetByInventoryCodeAsync(int inventoryCode, CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Department)
                .FirstOrDefaultAsync(p => p.InventoryCode == inventoryCode, cancellationToken);
        }


        public async Task<Product> AddAsync(Product product, CancellationToken cancellationToken = default)
        {
            await _context.Products.AddAsync(product, cancellationToken);
            return product;
        }


        public Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
        {
            _context.Entry(product).State = EntityState.Modified;
            return Task.CompletedTask;
        }


        public Task DeleteAsync(Product product, CancellationToken cancellationToken = default)
        {
            _context.Products.Remove(product);
            return Task.CompletedTask;
        }


        public async Task<bool> ExistsByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Products.AnyAsync(p => p.Id == id, cancellationToken);
        }
    }
}