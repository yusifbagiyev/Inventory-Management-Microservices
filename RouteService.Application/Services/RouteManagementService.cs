using MediatR;
using Microsoft.Extensions.Logging;
using RouteService.Application.DTOs;
using RouteService.Application.Features.Routes.Commands;
using RouteService.Application.Features.Routes.Queries;
using RouteService.Application.Interfaces;
using SharedServices.DTOs;
using SharedServices.Enum;
using SharedServices.Exceptions;
using SharedServices.Identity;
using System.Security.Claims;

namespace RouteService.Application.Services
{
    /// <summary>Runs a route change directly for holders of the .direct permission, otherwise files it for approval.</summary>
    public class RouteManagementService : IRouteManagementService
    {
        private readonly IMediator _mediator;
        private readonly IApprovalService _approvalService;
        private readonly IProductServiceClient _productClient;
        private readonly ILogger<RouteManagementService> _logger;

        public RouteManagementService(
            IMediator mediator,
            IApprovalService approvalService,
            IProductServiceClient productClient,
            ILogger<RouteManagementService> logger)
        {
            _mediator = mediator;
            _approvalService = approvalService;
            _productClient = productClient;
            _logger = logger;
        }

        public async Task<InventoryRouteDto> TransferInventoryWithApprovalAsync(
            TransferInventoryDto dto,
            int userId,
            string userName,
            List<string> userPermissions)
        {
            if (userPermissions.Contains(AllPermissions.RouteCreateDirect))
            {
                _logger.LogInformation($"User {userName} creating transfer for product {dto.ProductId} directly");
                return await _mediator.Send(new TransferInventory.Command(dto));
            }

            if (!userPermissions.Contains(AllPermissions.RouteCreate))
            {
                throw new InsufficientPermissionsException("You don't have permission to create transfers");
            }

            var transferData = await BuildTransferApprovalData(dto);

            var approvalRequest = new CreateApprovalRequestDto
            {
                RequestType = RequestType.TransferProduct,
                EntityType = "Route",
                EntityId = null,
                ActionData = transferData
            };

            var result = await _approvalService.CreateApprovalRequestAsync(approvalRequest, userId, userName);

            _logger.LogInformation($"Approval request {result.Id} created for transfer of product {dto.ProductId}");
            throw new ApprovalRequiredException(result.Id, "Transfer request has been submitted for approval");
        }



        public async Task UpdateRouteWithApprovalAsync(
            int id,
            UpdateRouteDto dto,
            int userId,
            string userName,
            List<string> userPermissions)
        {
            var existingRoute = await _mediator.Send(new GetRouteByIdQuery(id));
            if (existingRoute == null)
            {
                throw new NotFoundException($"Route with ID {id} not found");
            }

            if (existingRoute.IsCompleted)
            {
                throw new InvalidOperationException("Cannot update a completed route");
            }

            if (userPermissions.Contains(AllPermissions.RouteUpdateDirect))
            {
                _logger.LogInformation($"User {userName} updating route {id} directly");
                await _mediator.Send(new UpdateRoute.Command(id, dto));
                return;
            }

            if (!userPermissions.Contains(AllPermissions.RouteUpdate))
            {
                throw new InsufficientPermissionsException("You don't have permission to update routes");
            }

            var updateData = await BuildRouteUpdateData(existingRoute, dto);

            var approvalRequest = new CreateApprovalRequestDto
            {
                RequestType = RequestType.UpdateRoute,
                EntityType = "Route",
                EntityId = id,
                ActionData = new
                {
                    RouteId = id,
                    UpdateData = updateData,
                    existingRoute.InventoryCode,
                    existingRoute.Model,
                    existingRoute.FromDepartmentName,
                    existingRoute.ToDepartmentName,
                    Changes = BuildRouteChangeSummary(existingRoute, dto)
                }
            };

            var result = await _approvalService.CreateApprovalRequestAsync(approvalRequest, userId, userName);

            _logger.LogInformation($"Approval request {result.Id} created for updating route {id}");
            throw new ApprovalRequiredException(result.Id, "Route update request submitted for approval");
        }



        public async Task DeleteRouteWithApprovalAsync(
            int id,
            int userId,
            string userName,
            List<string> userPermissions)
        {
            var route = await _mediator.Send(new GetRouteByIdQuery(id));
            if (route == null)
            {
                throw new NotFoundException($"Route with ID {id} not found");
            }

            if (route.IsCompleted)
            {
                throw new InvalidOperationException("Cannot delete completed routes. They are part of the audit trail.");
            }

            if (userPermissions.Contains(AllPermissions.RouteDeleteDirect))
            {
                _logger.LogInformation($"User {userName} deleting route {id} directly");
                await _mediator.Send(new DeleteRoute.Command(id));
                return;
            }

            if (!userPermissions.Contains(AllPermissions.RouteDelete))
            {
                throw new InsufficientPermissionsException("You don't have permission to delete routes");
            }

            var approvalRequest = new CreateApprovalRequestDto
            {
                RequestType = RequestType.DeleteRoute,
                EntityType = "Route",
                EntityId = id,
                ActionData = new DeleteRouteActionDataWithRequest
                {
                    RouteId = id,
                    RouteType = route.RouteTypeName,
                    ProductInfo = $"{route.InventoryCode} - {route.Model} ({route.Vendor})",
                    FromLocation = $"{route.FromDepartmentName} - {route.FromWorker}",
                    ToLocation = $"{route.ToDepartmentName} - {route.ToWorker}",
                    CreatedDate = route.CreatedAt,
                    IsCompleted = route.IsCompleted
                }
            };

            var result = await _approvalService.CreateApprovalRequestAsync(approvalRequest, userId, userName);

            _logger.LogInformation($"Approval request {result.Id} created for deleting route {id}");
            throw new ApprovalRequiredException(result.Id, "Route deletion request submitted for approval");
        }


        private async Task<Dictionary<string, object>> BuildTransferApprovalData(TransferInventoryDto dto)
        {
            var product = await _productClient.GetProductByIdAsync(dto.ProductId);
            if (product == null)
            {
                throw new NotFoundException($"Product {dto.ProductId} not found");
            }

            var toDepartment = await _productClient.GetDepartmentByIdAsync(dto.ToDepartmentId);
            if (toDepartment == null)
            {
                throw new NotFoundException($"Target department {dto.ToDepartmentId} not found");
            }

            var fromDepartment = await _productClient.GetDepartmentByIdAsync(product.DepartmentId);

            var actionData = new Dictionary<string, object>
            {
                ["productId"] = dto.ProductId,
                ["inventoryCode"] = product.InventoryCode,
                ["productModel"] = product.Model ?? "",
                ["productVendor"] = product.Vendor ?? "",
                ["productCategory"] = product.CategoryName ?? "",
                ["fromDepartmentId"] = product.DepartmentId,
                ["fromDepartmentName"] = fromDepartment?.Name ?? "",
                ["fromWorker"] = product.Worker ?? "",
                ["toDepartmentId"] = dto.ToDepartmentId,
                ["toDepartmentName"] = toDepartment.Name,
                ["toWorker"] = dto.ToWorker ?? "",
                ["notes"] = dto.Notes ?? "",
                ["transferReason"] = BuildTransferReason(product, fromDepartment, toDepartment)
            };

            if (dto.ImageFile != null && dto.ImageFile.Length > 0)
            {
                using var ms = new MemoryStream();
                await dto.ImageFile.CopyToAsync(ms);
                actionData["imageData"] = Convert.ToBase64String(ms.ToArray());
                actionData["imageFileName"] = dto.ImageFile.FileName;
                actionData["imageSize"] = dto.ImageFile.Length;
            }

            return actionData;
        }


        private async Task<Dictionary<string, object>> BuildRouteUpdateData(InventoryRouteDto existing, UpdateRouteDto updated)
        {
            var updateData = new Dictionary<string, object>
            {
                ["notes"] = updated.Notes ?? existing.Notes ?? ""
            };

            var changes = new List<string>();

            if (existing.Notes != updated.Notes && !string.IsNullOrEmpty(updated.Notes))
            {
                changes.Add($"Notes updated");
            }

            if (updated.ImageFile != null && updated.ImageFile.Length > 0)
            {
                using var ms = new MemoryStream();
                await updated.ImageFile.CopyToAsync(ms);
                updateData["imageData"] = Convert.ToBase64String(ms.ToArray());
                updateData["imageFileName"] = updated.ImageFile.FileName;
                updateData["imageSize"] = updated.ImageFile.Length;
                changes.Add("New image uploaded");
            }

            updateData["changesSummary"] = string.Join(", ", changes);

            return updateData;
        }


        private string BuildRouteChangeSummary(InventoryRouteDto existing, UpdateRouteDto updated)
        {
            var changes = new List<string>();

            if (!string.IsNullOrEmpty(updated.Notes) && existing.Notes != updated.Notes)
            {
                changes.Add("Notes updated");
            }

            if (updated.ImageFile != null)
            {
                changes.Add($"New image: {updated.ImageFile.FileName}");
            }

            return changes.Any() ? string.Join(", ", changes) : "No changes";
        }



        private string BuildTransferReason(ProductInfoDto product, DepartmentDto? from, DepartmentDto to)
        {
            return $"Transfer of {product.Model} ({product.InventoryCode}) from {from?.Name ?? "Unknown"} to {to.Name}";
        }


        public int GetUserId(ClaimsPrincipal User)
        {
            return int.Parse(User.FindFirst("UserId")?.Value ??
               User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }



        public string GetUserName(ClaimsPrincipal User)
        {
            return User.Identity?.Name ?? "Unknown";
        }



        public List<string> GetUserPermissions(ClaimsPrincipal User)
        {
            return User.Claims
                .Where(c => c.Type == "permission")
                .Select(c => c.Value)
                .ToList();
        }
    }
}