using InventoryManagement.Web.Filters;
using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    public class RoutesController : BaseController
    {
        private readonly IApiService _apiService;
        private readonly IUrlService _urlService;
        public RoutesController(
            IApiService apiService, 
            IUrlService urlService)
            : base()
        {
            _apiService = apiService;
            _urlService = urlService;
        }


        public async Task<IActionResult> Index(
            int? pageNumber = 1,
            int? pageSize = 30,
            string? search = null,
            bool? isCompleted = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            try
            {
                var queryString = new StringBuilder($"?pageNumber={pageNumber}&pageSize={pageSize}");

                if (!string.IsNullOrEmpty(search))
                    queryString.Append($"&search={Uri.EscapeDataString(search)}");

                if (isCompleted.HasValue)
                    queryString.Append($"&isCompleted={isCompleted.Value}");

                if (startDate.HasValue)
                    queryString.Append($"&startDate={startDate.Value:yyyy-MM-dd}");

                if (endDate.HasValue)
                    queryString.Append($"&endDate={endDate.Value:yyyy-MM-dd}");

                // Pending transfers first.
                queryString.Append("&orderBy=IsCompleted&ascending=true");

                var routes = await _apiService.GetAsync<PagedResultDto<RouteViewModel>>(
                    $"api/inventoryroutes{queryString}");

                if (routes != null)
                {
                    foreach (var route in routes.Items)
                    {
                        if (!string.IsNullOrEmpty(route.ImageUrl))
                        {
                            route.FullImageUrl = _urlService.GetImageUrl(route.ImageUrl);
                        }
                    }

                    var start = ((routes.PageNumber - 1) * routes.PageSize) + 1;
                    var end = Math.Min(routes.PageNumber * routes.PageSize, routes.TotalCount);
                    ViewBag.ShowingStart = start;
                    ViewBag.ShowingEnd = end;
                    ViewBag.TotalCount = routes.TotalCount;
                }

                ViewBag.CurrentFilter = isCompleted;
                ViewBag.StartDate = startDate;
                ViewBag.CurrentSearch = search;
                ViewBag.CurrentStatus = isCompleted;
                ViewBag.EndDate = endDate;
                ViewBag.PageNumber = pageNumber ?? 1;
                ViewBag.PageSize = pageSize ?? 30;

                return View(routes ?? new PagedResultDto<RouteViewModel>());
            }
            catch (Exception ex)
            {
                return HandleException(ex,new PagedResultDto<RouteViewModel>());
            }
        }



        [PermissionAuthorize("route.create", "route.create.direct")]
        public async Task<IActionResult> Transfer()
        {
            var model = new TransferViewModel();
            await LoadTransferDropdowns(model);
            return View(model);
        }



        [HttpGet]
        [PermissionAuthorize("route.update", "route.update.direct")]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                if (id == 0)
                    return RedirectToAction("NotFound", "Home", "?statusCode=404");

                var route = await _apiService.GetAsync<RouteViewModel>($"api/inventoryroutes/{id}");
                if (route == null)
                    return RedirectToAction("NotFound", "Home", "?statusCode=404");

                if (!string.IsNullOrEmpty(route.ImageUrl))
                {
                    route.FullImageUrl = _urlService.GetImageUrl(route.ImageUrl);
                }
                return View(route);
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("route.update", "route.update.direct")]
        public async Task<IActionResult> Edit(int id, [FromForm] UpdateRouteViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadDropdowns();
                return HandleValidationErrors(model);
            }

                var dto = new
            {
                model.ImageFile,
                model.ToDepartmentId,
                model.ToWorker,
                model.Notes
            };

            try
            {
                var form = HttpContext.Request.Form;
                var response = await _apiService.PutFormAsync<bool>($"api/inventoryroutes/{id}", form, dto);
                return HandleApiResponse(response, "Index");
            }
            catch (Exception ex)
            {
                await LoadDropdowns();
                return HandleException(ex, model);
            }
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("route.create", "route.create.direct")]
        public async Task<IActionResult> Transfer(TransferViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadTransferDropdowns(model);
                return HandleValidationErrors(model);
            }
            try
            {
                var product = await _apiService.GetAsync<ProductDto>($"api/products/{model.ProductId}");
                var departments = await _apiService.GetAsync<List<DepartmentDto>>("api/departments");

                var fromDepartment = departments?.FirstOrDefault(d => d.Id == product?.DepartmentId);
                var toDepartment = departments?.FirstOrDefault(d => d.Id == model.ToDepartmentId);

                var actionData = new Dictionary<string, object>
                {
                    ["productId"] = model.ProductId,
                    ["inventoryCode"] = product?.InventoryCode ?? 0,
                    ["productModel"] = product?.Model ?? "",
                    ["productVendor"] = product?.Vendor ?? "",
                    ["fromDepartmentId"] = product?.DepartmentId ?? 0,
                    ["fromDepartmentName"] = fromDepartment?.Name ?? "",
                    ["fromWorker"] = product?.Worker ?? "",
                    ["toDepartmentId"] = model.ToDepartmentId,
                    ["toDepartmentName"] = toDepartment?.Name ?? "",
                    ["toWorker"] = model.ToWorker ?? "",
                    ["notes"] = model.Notes ?? ""
                };

                if (HttpContext.Request.Form.Files.Count > 0)
                {
                    var imageFile = HttpContext.Request.Form.Files[0];
                    using var ms = new MemoryStream();
                    await imageFile.CopyToAsync(ms);
                    actionData["imageData"] = Convert.ToBase64String(ms.ToArray());
                    actionData["imageFileName"] = imageFile.FileName;
                }

                var response = await _apiService.PostFormAsync<RouteViewModel>("api/inventoryroutes/transfer", HttpContext.Request.Form);

                return HandleApiResponse(response, "Index");
            }
            catch (Exception ex)
            {
                await LoadTransferDropdowns(model);
                return HandleException(ex, model);
            }
        }



        public async Task<IActionResult> Timeline(int productId)
        {
            try
            {
                if (productId == 0)
                {
                    return RedirectToAction("NotFound", "Home", "?statusCode=404");
                }
                var routes = await _apiService.GetAsync<List<RouteViewModel>>($"api/inventoryroutes/product/{productId}");
                ViewBag.ProductId = productId;

                if (routes != null)
                {
                    foreach(var route in routes)
                    {
                        if (!string.IsNullOrEmpty(route.ImageUrl))
                        {
                            route.FullImageUrl = _urlService.GetImageUrl(route.ImageUrl);
                        }
                    }
                }
                return View(routes ?? []);
            }
            catch (Exception ex)
            {
                return HandleException(ex, new List<RouteViewModel>());
            }
        }



        public async Task<IActionResult> Details(int id)
        {
            try
            {
                if(id == 0)
                {
                    return RedirectToAction("NotFound", "Home", "?statusCode=404");
                }
                var route = await _apiService.GetAsync<RouteViewModel>($"api/inventoryroutes/{id}");
                if (route == null)
                    return RedirectToAction("NotFound", "Home", "?statusCode=404");

                if (route != null)
                {
                    if(!string.IsNullOrEmpty(route.ImageUrl))
                    {
                        route.FullImageUrl = _urlService.GetImageUrl(route.ImageUrl);
                    }
                }

                return View(route);
            }
            catch (Exception ex)
            {
                return HandleException(ex, new RouteViewModel());
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("route.complete")]
        public async Task<IActionResult> Complete(int id)
        {
            try
            {
                if (id == 0)
                {
                    return RedirectToAction("NotFound", "Home", "?statusCode=404");
                }
                var response = await _apiService.PutAsync<bool>($"api/inventoryroutes/{id}/complete", new { });

                if (response.IsSuccess)
                {
                    TempData["Success"] = "Route completed successfully";
                }
                else
                {
                    TempData["Error"] = response.Message ?? "Failed to complete route";
                }
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error completing route");
                TempData["Error"] = "An error occurred while completing the route";
                return RedirectToAction(nameof(Index));
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("route.delete", "route.delete.direct")]

        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                if (id == 0)
                {
                    return RedirectToAction("NotFound", "Home", "?statusCode=404");
                }
                var response = await _apiService.DeleteAsync($"api/inventoryroutes/{id}");

                if (response.IsSuccess)
                {
                    TempData["Success"] = "Route deleted successfully";
                }
                else
                {
                    TempData["Error"] = response.Message ?? "Failed to delete route";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error deleting route");
                TempData["Error"] = "An error occurred while deleting the route";
                return RedirectToAction(nameof(Index));
            }
        }



        private async Task LoadTransferDropdowns(TransferViewModel model)
        {
            try
            {
                var products = await _apiService.GetAsync<PagedResultDto<ProductDto>>("api/products");
                var departments = await _apiService.GetAsync<List<DepartmentDto>>("api/departments");

                model.Products = products?.Items.Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = $"{p.InventoryCode} - {p.Model} ({p.Vendor})"
                }).ToList() ?? [];

                model.Departments = departments?.Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = d.Name
                }).ToList() ?? [];
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load transfer dropdowns");
                model.Products = [];
                model.Departments = [];
            }
        }

        private async Task LoadDropdowns()
        {
            try
            {
                var departments = await _apiService.GetAsync<List<DepartmentDto>>("api/departments");
                ViewBag.Departments = departments?.Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = d.Name
                }).ToList() ?? [];
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load dropdowns in route view");
                ViewBag.Departments = new List<DepartmentDto>();
            }
        }

    }
}