using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Web.Controllers
{
    [Authorize(Roles ="Admin")]
    public class CategoriesController : BaseController
    {
        private readonly IApiService _apiService;
        private readonly IUrlService _urlService;

        public CategoriesController(
            IApiService apiService, 
            ILogger<CategoriesController> logger,
            IUrlService urlService)
            : base(logger)
        {
            _apiService = apiService;
            _urlService = urlService;
        }

        public async Task<IActionResult> Index(
            int pageNumber = 1, 
            int pageSize = 20, 
            string? search = null)
        {
            try
            {
                var queryString=$"?pageNumber={pageNumber}&pageSize={pageSize}";
                if(!string.IsNullOrWhiteSpace(search))
                    queryString+=$"&search={Uri.EscapeDataString(search)}"; 

                var result=await _apiService.GetAsync<PagedResultDto<CategoryViewModel>>(
                    $"api/categories/paged{queryString}");
                var allCategories = await _apiService.GetAsync<List<CategoryViewModel>>(
                    $"api/categories");

                if (result == null)
                {
                    result = new PagedResultDto<CategoryViewModel>
                    {
                        Items = new List<CategoryViewModel>(),
                        TotalCount = 0,
                        PageNumber = pageNumber,
                        PageSize = pageSize
                    };
                }

                if (allCategories != null)
                {
                    var activeCategories = 0;
                    var inActiveCategories = 0;
                    var categoriesInWithProducts = 0;

                    // While searching, the counters only cover the matching page
                    if (!string.IsNullOrEmpty(search))
                    {
                        activeCategories = result.Items.Count(c => c.IsActive);
                        inActiveCategories = result.Items.Count(c => !c.IsActive);
                        categoriesInWithProducts = result.Items.Sum(c => c.ProductCount);
                    }
                    else
                    {
                        activeCategories = allCategories.Count(c => c.IsActive);
                        inActiveCategories = allCategories.Count(c => !c.IsActive);
                        categoriesInWithProducts = allCategories.Sum(c => c.ProductCount);
                    }

                    ViewBag.ActiveCategories = activeCategories;
                    ViewBag.InActiveCategories = inActiveCategories;
                    ViewBag.CategoriesInWithProducts = categoriesInWithProducts;
                }

                ViewBag.CurrentSearch = search;
                ViewBag.PageNumber = pageNumber;
                ViewBag.PageSize = pageSize;

                return View(result);
            }
            catch (Exception ex)
            {
                return HandleException(ex, new PagedResultDto<CategoryViewModel>
                {
                    Items = new List<CategoryViewModel>(),
                    TotalCount = 0,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                });
            }
        }


        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var category = await _apiService.GetAsync<CategoryViewModel>($"api/categories/{id}");
                if (category == null)
                    return RedirectToAction("NotFound", "Home", "?statusCode=404");

                // One oversized page, because the details page lists every product of the category
                var products = await _apiService.GetAsync<PagedResultDto<ProductViewModel>>(
                    $"api/products?pageSize=10000&pageNumber=1&categoryId={id}");

                var categoryProducts = products?.Items ?? new List<ProductViewModel>();

                foreach (var product in categoryProducts)
                {
                    if (!string.IsNullOrEmpty(product.ImageUrl))
                    {
                        product.FullImageUrl = _urlService.GetImageUrl(product.ImageUrl);
                    }
                }

                ViewBag.Products = categoryProducts.ToList();
                category.ProductCount = categoryProducts.Count();

                return View(category);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading category details for ID: {CategoryId}", id);
                return HandleException(ex);
            }
        }


        public IActionResult Create()
        {
            return View(new CategoryViewModel());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CategoryViewModel model)
        {
            if(!ModelState.IsValid)
            {
                return HandleValidationErrors(model);
            }
            try
            {
                var dto = new CreateCategoryDto
                {
                    Name = model.Name,
                    Description = model.Description,
                    IsActive = model.IsActive
                };

                var response = await _apiService.PostAsync<CategoryDto>("api/categories", dto);
                return HandleApiResponse(response, "Index");
            }
            catch (Exception ex)
            {
                return HandleException(ex,model);
            }
        }



        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var category = await _apiService.GetAsync<CategoryViewModel>($"api/categories/{id}");

                if (category == null)
                    return RedirectToAction("NotFound", "Home", "?statusCode=404");

                return View(category);
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CategoryViewModel model)
        {
            if(!ModelState.IsValid)
            {
                return HandleValidationErrors(model);
            }
            try
            {
                var dto = new UpdateCategoryDto
                {
                    Name = model.Name,
                    Description = model.Description,
                    IsActive = model.IsActive
                };
                var response = await _apiService.PutAsync<bool>($"api/categories/{id}", dto);
                return HandleApiResponse(response, "Index");
            }
            catch (Exception ex)
            {
                return HandleException(ex, model);
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var response = await _apiService.DeleteAsync($"api/categories/{id}");
                return HandleApiResponse(response, "Index");
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }
    }
}