using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Web.Controllers
{
    /// <summary>The approval requests a non-admin sent, where pending ones can still be cancelled.</summary>
    [Authorize(Roles = "User,Operator")]
    public class MyRequestsController : BaseController
    {
        private readonly IApprovalService _approvalService;

        public MyRequestsController(IApprovalService approvalService, ILogger<MyRequestsController> logger)
            : base(logger)
        {
            _approvalService = approvalService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var requests = await _approvalService.GetMyRequestsAsync();

                var viewModel = new MyRequestsViewModel
                {
                    Requests = requests,
                    StatusCounts = requests.GroupBy(r => r.Status)
                        .ToDictionary(g => g.Key, g => g.Count())
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                return HandleException(ex, new MyRequestsViewModel());
            }
        }


        public async Task<IActionResult> Details(int id)
        {
            try
            {
                if (id == 0)
                {
                    return RedirectToAction("NotFound", "Home", "?statusCode=404");
                }
                var request = await _approvalService.GetRequestDetailsAsync(id);

                if (request == null)
                    return RedirectToAction("NotFound", "Home", "?statusCode=404");

                // This page only shows a user's own requests
                if (request.RequestedById != GetCurrentUserId())
                {
                    return Forbid();
                }

                return PartialView("~/Views/Approvals/_ApprovalDetails.cshtml", request);
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            try
            {
                if(id == 0)
                {
                    return RedirectToAction("NotFound", "Home", "?statusCode=404");
                }
                var request = await _approvalService.GetRequestDetailsAsync(id);

                if (request == null)
                    return RedirectToAction("NotFound", "Home", "?statusCode=404");

                if (request.RequestedById != GetCurrentUserId())
                {
                    return HandleError("You can only cancel your own requests");
                }

                if (request.Status != "Pending")
                {
                    return HandleError("Only pending requests can be cancelled");
                }

                await _approvalService.CancelRequestAsync(id);

                if (IsAjaxRequest())
                {
                    return AjaxResponse(true, "Request cancelled successfully");
                }

                TempData["Success"] = "Request cancelled successfully";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }
    }
}