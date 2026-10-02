using Microsoft.EntityFrameworkCore;
using RouteService.Domain.Common;
using RouteService.Domain.Entities;
using RouteService.Domain.Enums;
using RouteService.Domain.Repositories;
using RouteService.Infrastructure.Data;
using SharedServices.Services;

namespace RouteService.Infrastructure.Repositories
{
    public class InventoryRouteRepository : IInventoryRouteRepository
    {
        private readonly RouteDbContext _context;

        public InventoryRouteRepository(RouteDbContext context)
        {
            _context = context;
        }


        public async Task<InventoryRoute?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.InventoryRoutes
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        }


        public async Task<IEnumerable<InventoryRoute>> GetByProductIdAsync(int productId, CancellationToken cancellationToken = default)
        {
            return await _context.InventoryRoutes
                .Where(r => r.ProductSnapshot.ProductId == productId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync(cancellationToken);
        }


        public async Task<IEnumerable<InventoryRoute>> GetByDepartmentIdAsync(int departmentId, CancellationToken cancellationToken = default)
        {
            return await _context.InventoryRoutes
                .Where(r => r.FromDepartmentId == departmentId || r.ToDepartmentId == departmentId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync(cancellationToken);
        }


        public async Task<IEnumerable<InventoryRoute>> GetByRouteTypeAsync(RouteType routeType, CancellationToken cancellationToken = default)
        {
            return await _context.InventoryRoutes
                .Where(r => r.RouteType == routeType)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        
        public async Task<InventoryRoute> AddAsync(InventoryRoute route, CancellationToken cancellationToken = default)
        {
            await _context.InventoryRoutes.AddAsync(route, cancellationToken);
            return route;
        }


        public Task UpdateAsync(InventoryRoute route, CancellationToken cancellationToken = default)
        {
            _context.Entry(route).State = EntityState.Modified;
            return Task.CompletedTask;
        }


        public async Task<InventoryRoute?> GetLatestRouteForProductAsync(int productId, CancellationToken cancellationToken = default)
        {
            return await _context.InventoryRoutes
                .Where(r => r.ProductSnapshot.ProductId == productId)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }


        public async Task<PagedResult<InventoryRoute>> GetAllAsync(
            int pageNumber,
            int pageSize,
            string? search,
            bool? isCompleted,
            DateTime? startDate,
            DateTime? endDate,
            CancellationToken cancellationToken = default)
        {
            var query = _context.InventoryRoutes.AsQueryable();

            if (isCompleted.HasValue)
                query = query.Where(r => r.IsCompleted == isCompleted.Value);

            if (startDate.HasValue)
            {
                query=query.Where(r=>r.CreatedAt>= startDate);
            }

            if (endDate.HasValue)
            {
                // Include the whole end day
                var EndDate = endDate.Value.AddDays(1).AddTicks(-1);
                query = query.Where(r => r.CreatedAt <= EndDate);
            }

            IEnumerable<InventoryRoute> items;
            int totalCount;

            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim();

                // ILIKE narrows the rows in SQL but misses Azerbaijani letter variants
                var broadQuery = query.Where(r =>
                    EF.Functions.ILike(r.ProductSnapshot.InventoryCode.ToString(), $"%{search}%") ||
                    EF.Functions.ILike(r.ProductSnapshot.CategoryName, $"%{search}%") ||
                    EF.Functions.ILike(r.ProductSnapshot.Vendor, $"%{search}%") ||
                    EF.Functions.ILike(r.ProductSnapshot.Model, $"%{search}%") ||
                    (r.FromDepartmentName != null && EF.Functions.ILike(r.FromDepartmentName, $"%{search}%")) ||
                    EF.Functions.ILike(r.ToDepartmentName, $"%{search}%") ||
                    (r.FromWorker != null && EF.Functions.ILike(r.FromWorker, $"%{search}%")) ||
                    (r.ToWorker != null && EF.Functions.ILike(r.ToWorker, $"%{search}%"))
                );

                var allFilteredItems = await broadQuery
                    .OrderByDescending(r => !r.IsCompleted)
                    .ThenByDescending(r => r.CompletedAt)
                    .ToListAsync(cancellationToken);

                items = allFilteredItems.Where(r =>
                    SearchHelper.ContainsAzerbaijani(r.ProductSnapshot.InventoryCode.ToString(), search) ||
                    SearchHelper.ContainsAzerbaijani(r.ProductSnapshot.CategoryName, search) ||
                    SearchHelper.ContainsAzerbaijani(r.ProductSnapshot.Vendor, search) ||
                    SearchHelper.ContainsAzerbaijani(r.ProductSnapshot.Model, search) ||
                    SearchHelper.ContainsAzerbaijani(r.FromDepartmentName, search) ||
                    SearchHelper.ContainsAzerbaijani(r.ToDepartmentName, search) ||
                    SearchHelper.ContainsAzerbaijani(r.FromWorker, search) ||
                    SearchHelper.ContainsAzerbaijani(r.ToWorker, search)
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
                    .OrderByDescending(r => !r.IsCompleted)
                    .ThenByDescending(r => r.CompletedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);
            }

            return new PagedResult<InventoryRoute>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }


        public async Task<IEnumerable<InventoryRoute>> GetIncompleteRoutesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.InventoryRoutes
                .Where(r => !r.IsCompleted)
                .OrderBy(r => r.CreatedAt)
                .ToListAsync(cancellationToken);
        }


        public Task DeleteAsync(InventoryRoute route, CancellationToken cancellationToken = default)
        {
            _context.InventoryRoutes.Remove(route);
            return Task.CompletedTask;
        }


        public async Task<InventoryRoute?> GetPreviousRouteForProductAsync(int productId, int currentRouteId, CancellationToken cancellationToken = default)
        {
            return await _context.InventoryRoutes
                .Where(r => r.ProductSnapshot.ProductId == productId && r.Id < currentRouteId)
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}