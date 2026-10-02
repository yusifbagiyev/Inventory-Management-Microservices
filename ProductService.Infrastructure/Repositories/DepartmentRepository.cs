using Microsoft.EntityFrameworkCore;
using ProductService.Domain.Common;
using ProductService.Domain.Entities;
using ProductService.Domain.Repositories;
using ProductService.Infrastructure.Data;
using SharedServices.Services;

namespace ProductService.Infrastructure.Repositories
{
    public class DepartmentRepository : IDepartmentRepository
    {
        private readonly ProductDbContext _context;

        public DepartmentRepository(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<Department?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Departments
                .Include(d => d.Products)
                .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        }

        public async Task<IEnumerable<Department>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Departments
                .Include(p=>p.Products)
                .ToListAsync(cancellationToken);
        }


        public async Task<PagedResult<Department>> GetPagedAsync(
            int pageNumber, 
            int pageSize, 
            string search, 
            CancellationToken cancellationToken = default)
        {
            var query = _context.Departments.Include(c => c.Products).AsQueryable();

            IEnumerable<Department> items;
            int totalCount;

            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim();

                // ILIKE narrows the rows in SQL, then the Azerbaijani-aware match and paging run in memory
                var broadQuery = query.Where(r =>
                    EF.Functions.ILike(r.Name, $"%{search}%") ||
                    (r.DepartmentHead != null && EF.Functions.ILike(r.DepartmentHead, $"%{search}%")) ||
                    (r.Description != null && EF.Functions.ILike(r.Description, $"%{search}%"))
                );

                var allFilteredItems = await broadQuery
                    .OrderBy(n => n.Name)
                    .ToListAsync(cancellationToken);

                items = allFilteredItems.Where(r =>
                    SearchHelper.ContainsAzerbaijani(r.Name, search) ||
                    SearchHelper.ContainsAzerbaijani(r.DepartmentHead, search) ||
                    SearchHelper.ContainsAzerbaijani(r.Description, search))
                    .ToList();

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
                    .OrderBy(n => n.Name)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);
            }
            return new PagedResult<Department>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }


        public async Task<Department> AddAsync(Department department, CancellationToken cancellationToken = default)
        {
            await _context.Departments.AddAsync(department, cancellationToken);
            return department;
        }

        public Task UpdateAsync(Department department, CancellationToken cancellationToken = default)
        {
            _context.Entry(department).State = EntityState.Modified;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Department department, CancellationToken cancellationToken = default)
        {
            _context.Departments.Remove(department);
            return Task.CompletedTask;
        }

        public async Task<bool> ExistsByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Departments.AnyAsync(d => d.Id == id, cancellationToken);
        }
    }
}