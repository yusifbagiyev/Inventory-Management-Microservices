using System.Security.Claims;
using IdentityService.Application.DTOs;
using IdentityService.Application.Services;
using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedServices.Identity;

namespace IdentityService.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<Role> _roleManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ITokenService _tokenService;
        private readonly IdentityDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            UserManager<User> userManager,
            RoleManager<Role> roleManager,
            SignInManager<User> signInManager,
            ITokenService tokenService,
            IdentityDbContext context,
            IConfiguration configuration,
            ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        #region Authentication Methods

        public async Task<TokenDto> LoginAsync(LoginDto dto)
        {
            var user = await _userManager.FindByNameAsync(dto.Username);
            if (user == null || !user.IsActive)
                throw new UnauthorizedAccessException("Invalid credentials");

            if (await _userManager.IsLockedOutAsync(user))
            {
                var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
                throw new UnauthorizedAccessException($"Account is locked until {lockoutEnd}");
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, false);

            if (!result.Succeeded)
            {
                if (result.IsLockedOut)
                {
                    var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
                    throw new UnauthorizedAccessException($"Account locked due to multiple failed attempts. Try again after {lockoutEnd}");
                }
                throw new UnauthorizedAccessException("Invalid credentials");
            }

            user.LastLoginAt = DateTime.Now;
            await _userManager.UpdateAsync(user);

            await _userManager.ResetAccessFailedCountAsync(user);

            // Signing in ends the user's other sessions.
            await RevokeAllUserRefreshTokensAsync(user.Id);

            return await GenerateTokenResponse(user);
        }

        public async Task<TokenDto> RegisterAsync(RegisterDto dto)
        {
            var user = new User
            {
                UserName = dto.Username,
                Email = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                CreatedAt = DateTime.Now,
                IsActive= true
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));

            var role = dto.SelectedRole ?? AllRoles.User;
            await _userManager.AddToRoleAsync(user, role);

            await AssignRolePermissionsToUser(user.Id, role);

            return await GenerateTokenResponse(user);
        }

        public async Task<TokenDto> RefreshTokenAsync(RefreshTokenDto dto)
        {
            // The refresh token alone proves who the user is. The access token is optional.
            var refreshToken = await _tokenService.GetRefreshTokenAsync(dto.RefreshToken);
            if (refreshToken == null || !refreshToken.IsActive)
                throw new UnauthorizedAccessException("Invalid refresh token");

            var user = refreshToken.User;
            if (!user.IsActive)
                throw new UnauthorizedAccessException("User is inactive");

            if (!string.IsNullOrEmpty(dto.AccessToken))
            {
                int? tokenUserId = null;
                try
                {
                    var principal = _tokenService.GetPrincipalFromExpiredToken(dto.AccessToken);
                    if (int.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsedUserId))
                        tokenUserId = parsedUserId;
                }
                catch (Exception ex)
                {
                    // An unreadable access token is ignored, the refresh token is enough.
                    _logger?.LogWarning(ex, "Access token validation failed during refresh, but continuing with valid refresh token");
                }

                // A readable token of another user is refused.
                if (tokenUserId.HasValue && tokenUserId.Value != user.Id)
                    throw new UnauthorizedAccessException("Token user mismatch");
            }
            else
            {
                _logger?.LogInformation("Refresh token request without access token - restoring lost session for user {UserId}", user.Id);
            }

            var newAccessToken = await _tokenService.GenerateAccessToken(user);
            var newRefreshToken = await _tokenService.GenerateRefreshToken();

            // Rotate the refresh token on every use.
            await _tokenService.RevokeRefreshTokenAsync(dto.RefreshToken, newRefreshToken);
            await _tokenService.CreateRefreshTokenAsync(user.Id, newRefreshToken);

            var userDto = await GetUserAsync(user.Id);

            return new TokenDto
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = DateTime.Now.AddMinutes(Convert.ToDouble(_configuration["Jwt:ExpirationInMinutes"] ?? "60")),
                User = userDto!
            };
        }

        public async Task LogoutAsync(string refreshToken)
        {
            await _tokenService.RevokeRefreshTokenAsync(refreshToken);
        }

        #endregion

        #region User Management Methods

        public async Task<UserDto?> GetUserAsync(int userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return null;

            var roles = await _userManager.GetRolesAsync(user);
            var permissions = await GetUserPermissionsAsync(userId, roles);
            var directPermissions = await GetUserDirectPermissionsAsync(userId);

            return new UserDto
            {
                Id = user.Id,
                Username = user.UserName!,
                Email = user.Email!,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                Roles = roles.ToList(),
                Permissions = permissions,
            };
        }

        public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
        {
            var users = await _userManager.Users.ToListAsync();
            var userDtos = new List<UserDto>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var permissions = await GetUserPermissionsAsync(user.Id, roles);

                userDtos.Add(new UserDto
                {
                    Id = user.Id,
                    Username = user.UserName!,
                    Email = user.Email!,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    IsActive = user.IsActive,
                    CreatedAt= user.CreatedAt,
                    LastLoginAt= user.LastLoginAt,
                    Roles = roles.ToList(),
                    Permissions = permissions
                });
            }

            return userDtos;
        }

        public async Task<bool> UpdateUserAsync(UpdateUserDto dto)
        {
            var user = await _userManager.FindByIdAsync(dto.Id.ToString());
            if (user == null)
                return false;

            user.UserName = dto.Username;
            user.Email = dto.Email;
            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;

            if (dto.IsActive.HasValue)
                user.IsActive = dto.IsActive.Value;

            var result = await _userManager.UpdateAsync(user);
            return result.Succeeded;
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return false;

            var result = await _userManager.DeleteAsync(user);

            await RevokeAllUserRefreshTokensAsync(userId);

            return result.Succeeded;
        }

        public async Task<bool> ToggleUserStatusAsync(int userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return false;

            user.IsActive = !user.IsActive;
            var result = await _userManager.UpdateAsync(user);

            if (!user.IsActive)
            {
                await RevokeAllUserRefreshTokensAsync(userId);
            }

            return result.Succeeded;
        }

        #endregion

        #region Password Management

        public async Task<bool> ResetPasswordAsync(int userId, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return false;

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            // A new password ends every existing session.
            if (result.Succeeded)
            {
                await RevokeAllUserRefreshTokensAsync(userId);
            }

            return result.Succeeded;
        }

        public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return false;

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

            if (result.Succeeded)
            {
                await RevokeAllUserRefreshTokensAsync(userId);
            }

            return result.Succeeded;
        }

        #endregion

        #region Role Management

        public async Task<IEnumerable<string>> GetAllRolesAsync()
        {
            var roles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
            return roles;
        }

        public async Task<bool> AssignRoleAsync(int userId, string roleName)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return false;

            var roleExists = await _roleManager.RoleExistsAsync(roleName);
            if (!roleExists)
                return false;

            var userRoles = await _userManager.GetRolesAsync(user);
            if (userRoles.Contains(roleName))
                return true;

            var result = await _userManager.AddToRoleAsync(user, roleName);
            return result.Succeeded;
        }

        public async Task<bool> RemoveRoleAsync(int userId, string roleName)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return false;

            var result = await _userManager.RemoveFromRoleAsync(user, roleName);
            return result.Succeeded;
        }

        #endregion

        #region Permission Management

        public async Task<IEnumerable<object>> GetAllPermissionsAsync()
        {
            var permissions = await _context.Permissions
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Category,
                    p.Description
                })
                .ToListAsync();

            return permissions.Cast<object>();
        }

        private async Task AssignRolePermissionsToUser(int userId, string roleName)
        {
            // The role's permissions are copied onto the user as their own grants.
            var rolePermissions = await _context.RolePermissions
                .Include(rp => rp.Permission)
                .Where(rp => rp.Role.Name == roleName)
                .ToListAsync();

            foreach (var rolePermission in rolePermissions)
            {
                var userPermission = new UserPermission
                {
                    UserId = userId,
                    PermissionId = rolePermission.PermissionId,
                    GrantedAt = DateTime.Now,
                    GrantedBy = "System - Role Assignment"
                };

                _context.UserPermissions.Add(userPermission);
            }

            await _context.SaveChangesAsync();
        }


        public async Task<bool> HasPermissionAsync(int userId, string permission)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null || !user.IsActive)
                return false;

            var userRoles = await _userManager.GetRolesAsync(user);

            var hasPermission = await _context.RolePermissions
                .Include(rp => rp.Role)
                .Include(rp => rp.Permission)
                .AnyAsync(rp => userRoles.Contains(rp.Role.Name!) && rp.Permission.Name == permission);

            return hasPermission;
        }

        public async Task<bool> GrantPermissionToUserAsync(int userId, string permissionName, string grantedBy)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return false;

            var permission = await _context.Permissions
                .FirstOrDefaultAsync(p => p.Name == permissionName);
            if (permission == null) return false;

            var existingPermission = await _context.UserPermissions
                .FirstOrDefaultAsync(up => up.UserId == userId && up.PermissionId == permission.Id);

            if (existingPermission != null) return true;

            _context.UserPermissions.Add(new UserPermission
            {
                UserId = userId,
                PermissionId = permission.Id,
                GrantedAt = DateTime.Now,
                GrantedBy = grantedBy
            });

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RevokePermissionFromUserAsync(int userId, string permissionName)
        {
            var permission = await _context.Permissions
                .FirstOrDefaultAsync(p => p.Name == permissionName);
            if (permission == null) return false;

            var userPermission = await _context.UserPermissions
                .FirstOrDefaultAsync(up => up.UserId == userId && up.PermissionId == permission.Id);

            if (userPermission == null) return true;

            _context.UserPermissions.Remove(userPermission);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<PermissionDto>> GetUserDirectPermissionsAsync(int userId)
        {
            var permissions = await _context.UserPermissions
                .Include(up => up.Permission)
                .Where(up => up.UserId == userId)
                .Select(up => new PermissionDto
                {
                    Id = up.Permission.Id,
                    Name = up.Permission.Name,
                    Description = up.Permission.Description,
                    Category = up.Permission.Category
                })
                .ToListAsync();

            return permissions;
        }

        private async Task<List<string>> GetUserPermissionsAsync(int userId, IList<string> roles)
        {
            var rolePermissions = await _context.RolePermissions
                .Include(rp => rp.Permission)
                .Where(rp => roles.Contains(rp.Role.Name!))
                .Select(rp => rp.Permission.Name)
                .ToListAsync();

            var userPermissions = await _context.UserPermissions
                .Include(up => up.Permission)
                .Where(up => up.UserId == userId)
                .Select(up => up.Permission.Name)
                .ToListAsync();

            return rolePermissions.Union(userPermissions).Distinct().ToList();
        }

        #endregion

        #region Additional Utility Methods

        public async Task<bool> UserExistsAsync(int userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            return user != null;
        }

        public async Task<bool> UserExistsAsync(string username)
        {
            var user = await _userManager.FindByNameAsync(username);
            return user != null;
        }

        public async Task<UserDto?> GetUserByUsernameAsync(string username)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user == null)
                return null;

            var roles = await _userManager.GetRolesAsync(user);
            var permissions = await GetUserPermissionsAsync(user.Id, roles);

            return new UserDto
            {
                Id = user.Id,
                Username = user.UserName!,
                Email = user.Email!,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Roles = roles.ToList(),
                Permissions = permissions
            };
        }

        public async Task<UserDto?> GetUserByEmailAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return null;

            var roles = await _userManager.GetRolesAsync(user);
            var permissions = await GetUserPermissionsAsync(user.Id, roles);

            return new UserDto
            {
                Id = user.Id,
                Username = user.UserName!,
                Email = user.Email!,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Roles = roles.ToList(),
                Permissions = permissions
            };
        }

        #endregion

        #region Private Helper Methods

        private async Task<TokenDto> GenerateTokenResponse(User user)
        {
            var accessToken = await _tokenService.GenerateAccessToken(user);
            var refreshToken = await _tokenService.GenerateRefreshToken();

            await _tokenService.CreateRefreshTokenAsync(user.Id, refreshToken);

            var userDto = await GetUserAsync(user.Id);

            return new TokenDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.Now.AddMinutes(Convert.ToDouble(_configuration["Jwt:ExpirationInMinutes"] ?? "60")),
                User = userDto!
            };
        }

        private async Task RevokeAllUserRefreshTokensAsync(int userId)
        {
            var activeTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                .ToListAsync();

            foreach (var token in activeTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.Now;
            }

            if (activeTokens.Any())
                await _context.SaveChangesAsync();
        }

        #endregion
    }
}