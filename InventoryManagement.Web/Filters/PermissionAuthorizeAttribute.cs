using InventoryManagement.Web.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace InventoryManagement.Web.Filters
{
    /// <summary>Lets the action run when the user holds either permission, usually an x and x.direct pair.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method ,AllowMultiple =true)]
    public class PermissionAuthorizeAttribute:Attribute,IAuthorizationFilter
    {
        private readonly string _permission;
        private readonly string _alternatePermission;

        public PermissionAuthorizeAttribute(string permission)
        {
            _permission= permission;
            _alternatePermission = null;
        }

        public PermissionAuthorizeAttribute(string permission,string alternatePermission)
        {
            _permission = permission;
            _alternatePermission = alternatePermission;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            if(user == null || !user?.Identity?.IsAuthenticated == true)
            {
                context.Result = new RedirectToActionResult("Login", "Account", new
                {
                    returnUrl = context.HttpContext.Request.Path
                });
                return;
            }

            if(user!= null)
            {
                bool hasPermission = user.HasPermission(_permission);

                if (!hasPermission && !string.IsNullOrEmpty(_alternatePermission))
                {
                    hasPermission=user.HasPermission(_alternatePermission);
                }

                if (!hasPermission)
                {
                    context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                    return;
                }
            }
        }
    }
}