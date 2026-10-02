using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RouteService.Application.DTOs;
using RouteService.Application.Features.Routes.Commands;
using RouteService.Application.Features.Routes.Queries;
using RouteService.Application.Interfaces;
using RouteService.Domain.Exceptions;
using SharedServices.Authorization;
using SharedServices.Exceptions;
using SharedServices.Identity;
using System.Text.Json;

namespace RouteService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InventoryRoutesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IRouteManagementService _routeManagementService;
        private readonly ILogger<InventoryRoutesController> _logger;
        public InventoryRoutesController(
            IMediator mediator,
            IRouteManagementService routeManagementService,
            ILogger<InventoryRoutesController> logger)
        {
            _mediator = mediator;
            _routeManagementService = routeManagementService;
            _logger = logger;
        }


        [HttpPost("transfer")]
        [Consumes("multipart/form-data")]
        [Permission(AllPermissions.RouteCreate)]
        public async Task<IActionResult> TransferInventory([FromForm] TransferInventoryDto dto)
        {
            try
            {
                var userId = _routeManagementService.GetUserId(User);
                var userName = _routeManagementService.GetUserName(User);
                var userPermissions = _routeManagementService.GetUserPermissions(User);

                var result = await _routeManagementService.TransferInventoryWithApprovalAsync(
                    dto, userId, userName, userPermissions);

                return Ok(result);
            }
            catch (ApprovalRequiredException ex)
            {
                return Accepted(new
                {
                    ex.ApprovalRequestId,
                    ex.Message,
                    ex.Status
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
                _logger.LogError(ex, "Unexpected error creating transfer");
                return StatusCode(500, new { error = "An unexpected error occurred" });
            }
        }



        [HttpPut("{id}")]
        [Consumes("multipart/form-data")]
        [Permission(AllPermissions.RouteUpdate)]
        public async Task<IActionResult> UpdateRoute(int id, [FromForm] UpdateRouteDto dto)
        {
            try
            {
                var userId = _routeManagementService.GetUserId(User);
                var userName = _routeManagementService.GetUserName(User);
                var userPermissions = _routeManagementService.GetUserPermissions(User);

                await _routeManagementService.UpdateRouteWithApprovalAsync(
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
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InsufficientPermissionsException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error updating route {RouteId}", id);
                return StatusCode(500, new { error = "An unexpected error occurred" });
            }
        }



        [HttpGet("product/{productId}")]
        [Permission(AllPermissions.RouteView)]
        public async Task<ActionResult<IEnumerable<InventoryRouteDto>>> GetInventoryByProductId(int productId)
        {
            var result = await _mediator.Send(new GetRoutesByProductQuery(productId));
            return Ok(result);
        }



        [HttpGet]
        [Permission(AllPermissions.RouteView)]
        public async Task<ActionResult<PagedResultDto<InventoryRouteDto>>> GetAllRoutes(
            [FromQuery] int? pageNumber = 1,
            [FromQuery] int? pageSize = 30,
            [FromQuery] string? search = null,
            [FromQuery] bool? isCompleted = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var result = await _mediator.Send(new GetAllRoutesQuery(
                pageNumber, pageSize, search, isCompleted, startDate, endDate));
            return Ok(result);
        }


        [HttpGet("{id}")]
        [Permission(AllPermissions.RouteView)]
        public async Task<ActionResult<InventoryRouteDto>> GetById(int id)
        {
            var result = await _mediator.Send(new GetRouteByIdQuery(id));
            return result == null ? NotFound() : Ok(result);
        }


        [HttpPut("{id}/complete")]
        [Permission(AllPermissions.RouteComplete)]
        public async Task<IActionResult> CompleteRoute(int id)
        {
            try
            {
                await _mediator.Send(new CompleteRoute.Command(id));
                return NoContent();
            }
            catch (RouteException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }



        [HttpGet("department/{departmentId}")]
        [Permission(AllPermissions.RouteView)]
        public async Task<ActionResult<IEnumerable<InventoryRouteDto>>> GetByDepartment(int departmentId)
        {
            var result = await _mediator.Send(new GetRoutesByDepartmentQuery(departmentId));
            return Ok(result);
        }



        [HttpGet("incomplete")]
        [Permission(AllPermissions.RouteView)]
        public async Task<ActionResult<IEnumerable<InventoryRouteDto>>> GetIncompleteRoutes()
        {
            var result = await _mediator.Send(new GetIncompleteRoutesQuery());
            return Ok(result);
        }



        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRoute(int id)
        {
            try
            {
                var userId = _routeManagementService.GetUserId(User);
                var userName =_routeManagementService.GetUserName(User);
                var userPermissions = _routeManagementService.GetUserPermissions(User);

                await _routeManagementService.DeleteRouteWithApprovalAsync(
                    id, userId, userName, userPermissions);

                return NoContent();
            }
            catch (ApprovalRequiredException ex)
            {
                return Accepted(new
                {
                    ex.ApprovalRequestId,
                    ex.Message,
                    ex.Status
                });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InsufficientPermissionsException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting route {RouteId}", id);
                return StatusCode(500, new { error = "An unexpected error occurred" });
            }
        }




        // The approved endpoints are called by ApprovalService with an admin token once a request is approved
        [HttpPost("transfer/approved")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<InventoryRouteDto>> TransferApprovedMultipart([FromForm] TransferInventoryDto dto)
        {
            try
            {
                _logger.LogInformation($"Executing approved transfer for product {dto.ProductId} to department {dto.ToDepartmentId}");
                var result = await _mediator.Send(new TransferInventory.Command(dto));
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing approved transfer");
                return BadRequest(new { error = ex.Message, details = ex.InnerException?.Message });
            }
        }



        [HttpPut("{id}/approved")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateApproved(int id, [FromBody] object updateData)
        {
            var json = updateData.ToString();
            var data = JsonSerializer.Deserialize<JsonElement>(json!);

            var dto = new UpdateRouteDto
            {
                Notes = data.TryGetProperty("notes", out var notes) ? notes.GetString() : null
            };

            await _mediator.Send(new UpdateRoute.Command(id, dto));
            return NoContent();
        }



        [HttpDelete("{id}/approved")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteApproved(int id)
        {
            await _mediator.Send(new DeleteRoute.Command(id));
            return NoContent();
        }
    }
}