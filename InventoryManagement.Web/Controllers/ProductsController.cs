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
    public class ProductsController : BaseController
    {
        private readonly IApiService _apiService;
        private readonly IUrlService _urlService;
        public ProductsController(
            IApiService apiService,
            IUrlService urlService) 
            : base()
        {
            _apiService = apiService;
            _urlService = urlService;
        }

        public async Task<IActionResult> Index(
            int? pageNumber=1,
            int? pageSize=30,
            string? search=null,
            DateTime? startDate=null,
            DateTime? endDate=null,
            bool? status=null,
            bool? availability=null)
        {
            try
            {
                var queryString = new StringBuilder($"?pageNumber={pageNumber}&pageSize={pageSize}");

                if (!string.IsNullOrEmpty(search))
                    queryString.Append($"&search={Uri.EscapeDataString(search)}");

                if (startDate.HasValue)
                    queryString.Append($"&startDate={startDate.Value:yyyy-MM-dd}");

                if (endDate.HasValue)
                    queryString.Append($"&endDate={endDate.Value:yyyy-MM-dd}");

                if (status.HasValue)
                    queryString.Append($"&status={status}");

                if (availability.HasValue)
                    queryString.Append($"&availability={availability}");

                var products = await _apiService.GetAsync<PagedResultDto<ProductViewModel>>($"api/products{queryString}");

                if(products != null)
                {
                    foreach (var product in products.Items)
                    {
                        if (!string.IsNullOrEmpty(product.ImageUrl))
                        {
                            product.FullImageUrl = _urlService.GetImageUrl(product.ImageUrl);
                        }
                    }


                    var start = ((products.PageNumber - 1) * products.PageSize) + 1;
                    var end = Math.Min(products.PageNumber * products.PageSize, products.TotalCount);
                    ViewBag.ShowingStart = start;
                    ViewBag.ShowingEnd = end;
                    ViewBag.TotalCount = products.TotalCount;
                }

                ViewBag.PageNumber = pageNumber ?? 1;
                ViewBag.PageSize = pageSize ?? 30;
                ViewBag.CurrentSearch = search;
                ViewBag.CurrentStatus = status;
                ViewBag.CurrentAvailability = availability;
                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;


                return View(products ?? new PagedResultDto<ProductViewModel>());
            }
            catch (Exception ex)
            {
                return HandleException(ex, new PagedResultDto<ProductViewModel>());
            }
        }


        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var product = await _apiService.GetAsync<ProductViewModel>($"api/products/{id}");
                // A deleted product comes back as null
                if (product == null)
                {
                    return RedirectToAction("NotFound","Home");
                }

                if (!string.IsNullOrEmpty(product.ImageUrl))
                {
                    product.FullImageUrl = _urlService.GetImageUrl(product.ImageUrl);
                }

                return View(product);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading product details for ID: {ProductId}", id);
                return HandleException(ex);
            }
        }


        [PermissionAuthorize("product.create", "product.create.direct")]
        public async Task<IActionResult> Create()
        {
            var model = new ProductViewModel();
            await LoadDropdowns(model);
            return View(model);
        }



        [HttpPost]  
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("product.create", "product.create.direct")]
        public async Task<IActionResult> Create(ProductViewModel productModel)
        {
            if (!ModelState.IsValid)
            {
                await LoadDropdowns(productModel);
                return HandleValidationErrors(productModel);
            }
            var dto = new CreateProductDto
            {
                InventoryCode = productModel.InventoryCode,
                Model = productModel.Model,
                Vendor = productModel.Vendor,
                Worker = productModel.Worker,
                Description = productModel.Description,
                IsWorking = productModel.IsWorking,
                IsActive = productModel.IsActive,
                IsNewItem = productModel.IsNewItem,
                CategoryId = productModel.CategoryId,
                DepartmentId = productModel.DepartmentId
            };

            try
            {
                // The raw form goes along so the uploaded image reaches the API too
                var form = HttpContext.Request.Form;
                var response = await _apiService.PostFormAsync<dynamic>("api/products", form, dto);

                return HandleApiResponse(response, "Index");
            }
            catch (Exception ex)
            {
                await LoadDropdowns(productModel);
                return HandleException(ex, productModel);
            }
        }



        [PermissionAuthorize("product.update", "product.update.direct")]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var product = await _apiService.GetAsync<ProductViewModel>($"api/products/{id}");
                if (product == null)
                    return RedirectToAction("NotFound","Home","?statusCode=404");

                if (product != null)
                {
                    if (!string.IsNullOrEmpty(product.ImageUrl))
                    {
                        product.FullImageUrl = _urlService.GetImageUrl(product.ImageUrl);
                    }
                    await LoadDropdowns(product);
                }

                return View(product);
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("product.update", "product.update.direct")]
        public async Task<IActionResult> Edit(int id, ProductViewModel productModel)
        {
            if (!ModelState.IsValid)
            {
                await LoadDropdowns(productModel);
                return HandleValidationErrors(productModel);
            }
            try
            {
                var form = HttpContext.Request.Form;
                var response = await _apiService.PutFormAsync<bool>($"api/products/{id}", form, productModel);
                return HandleApiResponse(response, "Index");
            }
            catch(Exception ex)
            {
                await LoadDropdowns(productModel);
                return HandleException(ex, productModel);
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("product.update", "product.update.direct")]
        public async Task<IActionResult> UpdateInventoryCode([FromBody] UpdateInventoryCodeDto request)
        {
            try
            {
                var response = await _apiService.PutAsync<bool>(
                    $"api/products/{request.Id}/inventory-code",
                    new UpdateInventoryCodeDto { InventoryCode = request.InventoryCode });

                if (response.IsSuccess)
                {
                    return Json(new { success = true });
                }
                else
                {
                    return BadRequest(new { error = response.Message });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("product.delete", "product.delete.direct")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                if(id== 0)
                {
                    return RedirectToAction("NotFound", "Home", "?statusCode=404");
                }
                var response = await _apiService.DeleteAsync($"api/products/{id}");
                return HandleApiResponse(response, "Index");
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }


        private async Task LoadDropdowns(ProductViewModel model)
        {
            try
            {
                var categories = await _apiService.GetAsync<List<CategoryDto>>("api/categories");
                var departments = await _apiService.GetAsync<List<DepartmentDto>>("api/departments");

                model.Categories = categories?.Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                }).ToList() ?? [];

                model.Departments = departments?.Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = d.Name
                }).ToList() ?? [];
            }
            catch(Exception ex)
            {
                _logger?.LogError(ex, "Failed to load dropdowns");
                model.Categories = [];
                model.Departments = [];
            }
        }
    }
}