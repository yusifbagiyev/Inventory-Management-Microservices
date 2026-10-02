using System.Text.Json;
using InventoryManagement.Web.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Web.Controllers
{
    /// <summary>Shared error handling and AJAX responses for the signed-in pages.</summary>
    [Authorize]
    public abstract class BaseController : Controller
    {
        protected readonly ILogger<BaseController>? _logger;

        protected BaseController(ILogger<BaseController>? logger = null)
        {
            _logger = logger;
        }


        protected bool IsAjaxRequest()
        {
            return Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        }



        /// <summary>Returns JSON for AJAX calls, otherwise redirects to the given action.</summary>
        protected IActionResult HandleApiResponse<T>(ApiResponse<T> response, string redirectAction)
        {
            if (IsAjaxRequest())
            {
                // A request sent for approval is not a success, but it isn't an error either
                if (!response.IsSuccess && !response.IsApprovalRequest)
                {
                    Response.StatusCode = 400;
                }
                return Json(new
                {
                    isSuccess = response.IsSuccess,
                    isApprovalRequest = response.IsApprovalRequest,
                    approvalRequestId = response.ApprovalRequestId,
                    message = response.Message,
                    data = response.Data
                });
            }
            return RedirectToAction(redirectAction);
        }

        /// <summary>Logs the error and sends it back as JSON for AJAX calls or through ModelState for forms.</summary>
        protected IActionResult HandleError(string errorMessage, object? model = null,
            Dictionary<string, string>? fieldErrors = null)
        {
            _logger?.LogError("Error in {Controller}: {ErrorMessage}",
                ControllerContext.ActionDescriptor.ControllerName, errorMessage);

            if (IsAjaxRequest())
            {
                var response = new
                {
                    isSuccess = false,
                    message = errorMessage,
                    errors = fieldErrors
                };

                Response.StatusCode = 400;
                return Json(response);
            }

            ModelState.AddModelError("", errorMessage);

            if (fieldErrors != null)
            {
                foreach (var error in fieldErrors)
                {
                    ModelState.AddModelError(error.Key, error.Value);
                }
            }

            return View(model);
        }



        /// <summary>Turns an exception into a message that is safe to show the user.</summary>
        protected IActionResult HandleException(Exception ex, object? model = null)
        {
            _logger?.LogError(ex, "Exception in {Controller}.{Action}",
                ControllerContext.ActionDescriptor.ControllerName,
                ControllerContext.ActionDescriptor.ActionName);

            string userFriendlyMessage = "An unexpected error occurred. Please try again.";

            if (ex is UnauthorizedAccessException)
            {
                userFriendlyMessage = "You don't have permission to perform this action.";
                if (IsAjaxRequest())
                {
                    Response.StatusCode = 403;
                }
            }
            else if (ex is InvalidOperationException && ex.Message.Contains("inventory code"))
            {
                userFriendlyMessage = ex.Message;
            }
            else if (ex is HttpRequestException)
            {
                userFriendlyMessage = "Unable to connect to the server. Please check your connection.";
            }

            return HandleError(userFriendlyMessage, model);
        }



        protected IActionResult HandleValidationErrors(object? model = null)
        {
            if (IsAjaxRequest())
            {
                Response.StatusCode = 400;

                var errors = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray()
                    );

                return Json(new
                {
                    isSuccess = false,
                    message = "Please correct the validation errors and try again.",
                    errors
                });
            }

            return View(model);
        }



        /// <summary>Pulls a readable message out of an API error body, whatever shape it has.</summary>
        protected string ParseApiErrorMessage(string responseContent, string defaultMessage = "Operation failed")
        {
            if (string.IsNullOrWhiteSpace(responseContent))
                return defaultMessage;

            try
            {
                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;

                // Our own errors, ProblemDetails and ModelState output each use a different field
                if (root.TryGetProperty("error", out var errorProp))
                    return errorProp.GetString() ?? defaultMessage;

                if (root.TryGetProperty("message", out var messageProp))
                    return messageProp.GetString() ?? defaultMessage;

                if (root.TryGetProperty("title", out var titleProp))
                    return titleProp.GetString() ?? defaultMessage;

                if (root.TryGetProperty("errors", out var errorsProp) && errorsProp.ValueKind == JsonValueKind.Object)
                {
                    var errorMessages = new List<string?>();
                    foreach (var error in errorsProp.EnumerateObject())
                    {
                        if (error.Value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var msg in error.Value.EnumerateArray())
                            {
                                errorMessages.Add(msg.GetString());
                            }
                        }
                        else
                        {
                            errorMessages.Add(error.Value.GetString());
                        }
                    }
                    return string.Join("; ", errorMessages);
                }
            }
            catch
            {
                // Not JSON, so show a short plain-text body but never an HTML error page
                if (responseContent.Length < 200 && !responseContent.Contains("<"))
                {
                    return responseContent;
                }
            }

            return defaultMessage;
        }



        protected IActionResult AjaxResponse(bool success, string message, object? data = null,
            Dictionary<string, string[]>? errors = null)
        {
            return Json(new
            {
                isSuccess = success,
                message,
                data,
                errors
            });
        }



        /// <summary>Returns 0 when the user has no id claim.</summary>
        protected int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }



        protected string GetCurrentUserName()
        {
            return User.Identity?.Name ?? "Unknown";
        }
    }
}