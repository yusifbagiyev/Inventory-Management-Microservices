using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventoryManagement.Web.Models.ViewModels
{
    public record RouteViewModel
    {
        public int Id { get; set; }
        public string RouteType { get; set; } = string.Empty;
        public string RouteTypeName { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public int InventoryCode { get; set; }
        public string Model { get; set; } = string.Empty;
        public string Vendor { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int? FromDepartmentId { get; set; }
        public string? FromDepartmentName { get; set; }
        public int ToDepartmentId { get; set; }
        public string ToDepartmentName { get; set; } = string.Empty;
        public string? FromWorker { get; set; }
        public string? ToWorker { get; set; }
        public string? ImageUrl { get; set; }
        public string? Notes { get; set; }
        public bool IsCompleted { get; set; }
        public string? FullImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public record TransferViewModel
    {
        [Required]
        [Display(Name = "Product")]
        public int ProductId { get; set; }

        [Required]
        [Display(Name = "To Department")]
        public int ToDepartmentId { get; set; }

        [Display(Name = "To Worker")]
        public string? ToWorker { get; set; }

        [Display(Name = "Image")]
        public IFormFile? ImageFile { get; set; }

        [Display(Name = "Notes")]
        [MaxLength(500)]
        public string? Notes { get; set; }

        // Dropdown options for the form
        public List<SelectListItem>? Products { get; set; }
        public List<SelectListItem>? Departments { get; set; }
    }

    public record PagedResultDto<T>
    {
        public IEnumerable<T> Items { get; set; } = [];
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
        public int? ActiveItems { get; set; }
        public int? InActiveItems { get; set; }
        public int? ItemsInWithProducts { get; set; }
    }

    public record UpdateRouteViewModel
    {
        public IFormFile? ImageFile { get; set; }
        public int ToDepartmentId { get; set; }
        public string? ToWorker { get; set; }
        public string? Notes { get; set; }
    }
}