using IdentityService.Application.DTOs;

namespace IdentityService.Application.Services
{
    public interface IAuthService
    {
        // Sign-in
        Task<TokenDto> LoginAsync(LoginDto dto);
        Task<TokenDto> RegisterAsync(RegisterDto dto);
        Task<TokenDto> RefreshTokenAsync(RefreshTokenDto dto);
        Task LogoutAsync(string refreshToken);

        // Users
        Task<UserDto?> GetUserAsync(int userId);
        Task<IEnumerable<UserDto>> GetAllUsersAsync();
        Task<bool> UpdateUserAsync(UpdateUserDto dto);
        Task<bool> DeleteUserAsync(int userId);
        Task<bool> ToggleUserStatusAsync(int userId);

        // Passwords
        Task<bool> ResetPasswordAsync(int userId, string newPassword);
        Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);

        // Roles
        Task<IEnumerable<string>> GetAllRolesAsync();
        Task<bool> AssignRoleAsync(int userId, string roleName);
        Task<bool> RemoveRoleAsync(int userId, string roleName);

        // Permissions
        Task<IEnumerable<object>> GetAllPermissionsAsync();
        Task<bool> HasPermissionAsync(int userId, string permission);
        Task<bool> GrantPermissionToUserAsync(int userId, string permissionName, string grantedBy);
        Task<bool> RevokePermissionFromUserAsync(int userId, string permissionName);
        Task<List<PermissionDto>> GetUserDirectPermissionsAsync(int userId);

        // Lookups
        Task<bool> UserExistsAsync(int userId);
        Task<bool> UserExistsAsync(string username);
        Task<UserDto?> GetUserByUsernameAsync(string username);
        Task<UserDto?> GetUserByEmailAsync(string email);
    }
}