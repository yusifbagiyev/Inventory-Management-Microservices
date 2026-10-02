using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductService.Application.DTOs;
using ProductService.Application.Features.Departments.Queries;
using ProductService.Application.Features.Products.Commands;
using ProductService.Application.Features.Products.Queries;
using ProductService.Application.Interfaces;
using ProductService.Domain.Common;
using SharedServices.Authorization;
using SharedServices.Exceptions;
using SharedServices.Identity;
using System.Security.Claims;
using System.Text.Json;

namespace ProductService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProductsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IProductManagementService _productManagementService;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(IMediator mediator, IProductManagementService productManagementService, ILogger<ProductsController> logger)
        {
            _mediator = mediator; 
            _productManagementService = productManagementService;
            _logger = logger;
        }


        [HttpGet]
        [Permission(AllPermissions.ProductView)]
        public async Task<ActionResult<PagedResult<ProductDto>>> GetAll(
            [FromQuery] int? pageNumber = 1,
            [FromQuery] int? pageSize = 30,
            [FromQuery] string? search = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] bool? status = null,
            [FromQuery] bool? availability = null,
            [FromQuery] int? categoryId = null,
            [FromQuery] int? departmentId = null)
        {
            var products = await _mediator.Send(new GetAllProductsQuery(
                pageNumber, pageSize, search, startDate, endDate,
                status, availability, categoryId, departmentId));
            return Ok(products);
        }



        [HttpGet("{id}")]
        [Permission(AllPermissions.ProductView)]
        public async Task<ActionResult<ProductDto>> GetById(int id)
        {
            var product = await _mediator.Send(new GetProductByIdQuery(id));
            return product == null ? NotFound() : Ok(product);
        }



        [HttpGet("search/inventory-code/{inventoryCode}")]
        [Permission(AllPermissions.ProductView)]
        public async Task<ActionResult<ProductDto>> GetByInventoryCode(int inventoryCode)
        {
            var product = await _mediator.Send(new GetProductByInventoryCodeQuery(inventoryCode));
            if (product == null)
                return NotFound();
            return Ok(product);
        }



        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] CreateProductDto dto)
        {
            try
            {
                var userId = _productManagementService.GetUserId(User);
                var userName = _productManagementService.GetUserName(User);
                var userPermissions = _productManagementService.GetUserPermissions(User);

                var product = await _productManagementService.CreateProductWithApprovalAsync(
                    dto, userId, userName, userPermissions);

                return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
            }
            catch (ApprovalRequiredException ex)
            {
                return Accepted(new
                {
                    ApprovalRequestId = ex.ApprovalRequestId,
                    Message = ex.Message,
                    Status = ex.Status
                });
            }
            catch (DuplicateEntityException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InsufficientPermissionsException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating product");
                return StatusCode(500, new { error = "An unexpected error occurred" });
            }
        }



        [HttpPut("{id}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Update(int id, [FromForm] UpdateProductDto dto)
        {
            try
            {
                var userId = _productManagementService.GetUserId(User);
                var userName = _productManagementService.GetUserName(User);
                var userPermissions = _productManagementService.GetUserPermissions(User);

                await _productManagementService.UpdateProductWithApprovalAsync(
                    id, dto, userId, userName, userPermissions);

                return NoContent();
            }
            catch (ApprovalRequiredException ex)
            {
                return Accepted(new
                {
                    ApprovalRequestId = ex.ApprovalRequestId,
                    Message = ex.Message,
                    Status = ex.Status
                });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InsufficientPermissionsException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error updating product {ProductId}", id);
                return StatusCode(500, new { error = "An unexpected error occurred" });
            }
        }



        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var userId = _productManagementService.GetUserId(User);
                var userName = _productManagementService.GetUserName(User);
                var userPermissions = _productManagementService.GetUserPermissions(User);

                await _productManagementService.DeleteProductWithApprovalAsync(
                    id, userId, userName, userPermissions);

                return NoContent();
            }
            catch (ApprovalRequiredException ex)
            {
                return Accepted(new
                {
                    ApprovalRequestId = ex.ApprovalRequestId,
                    Message = ex.Message,
                    Status = ex.Status
                });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InsufficientPermissionsException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting product {ProductId}", id);
                return StatusCode(500, new { error = "An unexpected error occurred" });
            }
        }



        [HttpPost("approved/multipart")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ProductDto>> CreateApprovedMultipart([FromForm] CreateProductDto dto)
        {
            try
            {
                var product = await _mediator.Send(new CreateProduct.Command(dto));
                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating approved product");
                return BadRequest(new { error = ex.Message });
            }
        }



        [HttpPost("approved")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateApproved([FromBody] JsonElement productData)
        {
            try
            {
                var dto = new CreateProductDto
                {
                    InventoryCode = productData.GetProperty("inventoryCode").GetInt32(),
                    Model = productData.TryGetProperty("model", out var model) ? model.GetString() : "",
                    Vendor = productData.TryGetProperty("vendor", out var vendor) ? vendor.GetString() : "",
                    Worker = productData.TryGetProperty("worker", out var worker) ? worker.GetString() : "",
                    Description = productData.TryGetProperty("description", out var desc) ? desc.GetString() : "",
                    IsWorking = productData.TryGetProperty("isWorking", out var working) ? working.GetBoolean() : true,
                    IsActive = productData.TryGetProperty("isActive", out var active) ? active.GetBoolean() : true,
                    IsNewItem = productData.TryGetProperty("isNewItem", out var newItem) ? newItem.GetBoolean() : true,
                    CategoryId = productData.GetProperty("categoryId").GetInt32(),
                    DepartmentId = productData.GetProperty("departmentId").GetInt32()
                };

                return await Create(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating approved product");
                return BadRequest($"Error: {ex.Message}");
            }
        }



        [HttpPut("{id}/approved")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateApproved(int id, [FromBody] JsonElement updateData)
        {
            try
            {
                var dto = new UpdateProductDto
                {
                    Model = updateData.TryGetProperty("model", out var model) ? model.GetString() : "",
                    Vendor = updateData.TryGetProperty("vendor", out var vendor) ? vendor.GetString() : "",
                    Worker = updateData.TryGetProperty("worker", out var worker) ? worker.GetString() : "",
                    Description = updateData.TryGetProperty("description", out var desc) ? desc.GetString() : "",
                    IsWorking = updateData.TryGetProperty("isWorking", out var working) ? working.GetBoolean() : true,
                    IsActive = updateData.TryGetProperty("isActive", out var active) ? active.GetBoolean() : true,
                    IsNewItem = updateData.TryGetProperty("isNewItem", out var newItem) ? newItem.GetBoolean() : true,
                    CategoryId = updateData.GetProperty("categoryId").GetInt32(),
                    DepartmentId = updateData.GetProperty("departmentId").GetInt32()
                };

                return await Update(id, dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating approved product");
                return BadRequest($"Error: {ex.Message}");
            }
        }



        [HttpPut("{id}/approved/multipart")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateApprovedMultipart(int id, [FromForm] UpdateProductDto dto)
        {
            try
            {
                return await Update(id, dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating approved product with multipart data");
                return BadRequest(new { error = ex.Message, details = ex.InnerException?.Message });
            }
        }



        [HttpDelete("{id}/approved")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteApproved(int id)
        {
            var userName = User.Identity?.Name ?? "Unknown";
            await _mediator.Send(new DeleteProduct.Command(id,userName));
            return NoContent();
        }




        [HttpPut("{id}/inventory-code")]
        [Permission(AllPermissions.ProductUpdate)]
        public async Task<IActionResult> UpdateInventoryCode(int id, [FromBody] UpdateInventoryCodeDto dto)
        {
            var product = await _mediator.Send(new GetProductByIdQuery(id));
            if (product == null)
                return NotFound();

            var existing = await _mediator.Send(new GetProductByInventoryCodeQuery(dto.InventoryCode));
            if (existing != null && existing.Id != id)
                return BadRequest(new { error = "Inventory code already exists" });

            await _mediator.Send(new UpdateProductInventoryCode.Command(id, dto.InventoryCode));
            return NoContent();
        }



        [HttpGet("{id}/statistics")]
        [Permission(AllPermissions.ProductView)]
        public async Task<ActionResult<DepartmentStatisticsDto>> GetDepartmentStatistics(int id)
        {
            var department = await _mediator.Send(new GetDepartmentByIdQuery(id));
            if (department == null)
                return NotFound();

            var products = await _mediator.Send(new GetAllProductsQuery(
                pageNumber: 1,
                PageSize: 10000,
                departmentId: id));

            var statistics = new
            {
                TotalProducts = products.TotalCount,
                ActiveProducts = products.Items.Count(p => p.IsActive),
                WorkingProducts = products.Items.Count(p => p.IsWorking),
                NewItems = products.Items.Count(p => p.IsNewItem),
                AssignedWorkers = products.Items
                    .Where(p => !string.IsNullOrEmpty(p.Worker))
                    .Select(p => p.Worker)
                    .Distinct()
                    .Count(),
                CategoryBreakdown = products.Items
                    .GroupBy(p => p.CategoryName)
                    .Select(g => new { Category = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToList()
            };

            return Ok(statistics);
        }

        public record DepartmentStatisticsDto
        {
            public int TotalProducts { get; set; }

            public int ActiveProducts { get; set; }

            public int WorkingProducts { get; set; }

            public int NewItems { get; set; }

            /// <summary>Distinct workers holding at least one product here.</summary>
            public int AssignedWorkers { get; set; }

            public List<CategoryBreakdownDto> CategoryBreakdown { get; set; } = new();

            public List<string> WorkerList { get; set; } = new();
        }

        public record CategoryBreakdownDto
        {
            public string Category { get; set; } = string.Empty;

            public int Count { get; set; }

            public decimal Percentage { get; set; }
        }
    }
}