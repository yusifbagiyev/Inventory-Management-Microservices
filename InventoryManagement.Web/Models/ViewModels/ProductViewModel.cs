using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventoryManagement.Web.Models.ViewModels
{
    public class ProductViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Inventory Code")]
        [Range(1,9999)]
        public int InventoryCode { get; set; }


        [Display(Name = "Model")]
        public string? Model { get; set; }


        [Display(Name = "Vendor")]
        public string? Vendor { get; set; }


        [Display(Name = "Worker")]
        public string? Worker { get; set; }



        [Display(Name = "Description")]
        public string? Description { get; set; }



        [Display(Name = "Is Working")]
        public bool IsWorking { get; set; } = true;


        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;


        [Display(Name = "Is New Item")]
        public bool IsNewItem { get; set; } = true;


        [Required]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }


        [Required]
        [Display(Name = "Department")]
        public int DepartmentId { get; set; }



        [Display(Name = "Image")]
        public IFormFile? ImageFile { get; set; }

        public string? ImageUrl { get; set; }
        public string? CategoryName { get; set; }
        public string? DepartmentName { get; set; }

        [Display(Name = "Has Pending Approval")]
        public bool HasPendingApproval { get; set; }

        // Dropdown options for the form
        public List<SelectListItem>? Categories { get; set; }
        public List<SelectListItem>? Departments { get; set; }


        public string? FullImageUrl { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}