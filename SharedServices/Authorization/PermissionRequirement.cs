using Microsoft.AspNetCore.Authorization;

namespace SharedServices.Authorization
{
    public class PermissionRequirement:IAuthorizationRequirement
    {
        public string Permission { get; }
        public PermissionRequirement(string permission)
        {
            Permission = permission ?? throw new ArgumentNullException(nameof(permission));
        }
    }
    public class PermissionHandler:AuthorizationHandler<PermissionRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context, 
            PermissionRequirement requirement)
        {
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                return Task.CompletedTask;
            }

            var hasPermission = context.User.Claims.Any(c =>
                c.Type.Equals("permission", StringComparison.OrdinalIgnoreCase) &&
                c.Value.Equals(requirement.Permission, StringComparison.OrdinalIgnoreCase));

            if (hasPermission)
            {
                context.Succeed(requirement);
            }

            // Admins pass every permission check
            else if (context.User.IsInRole("Admin"))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}