using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Security.Claims;

namespace InventoryManagement.Web.Controllers
{
    /// <summary>Cookie sign-in backed by the identity API, with the JWT kept in the server session.</summary>
    public class AccountController : Controller
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AccountController> _logger;
        private readonly IUserManagementService _userManagementService;
        private readonly ITokenManager _tokenManager;
        public AccountController(
            IAuthService authService,
            IUserManagementService userManagementService,
            ILogger<AccountController> logger,
            ITokenManager tokenManager)
        {
            _authService = authService;
            _logger = logger;
            _userManagementService = userManagementService;
            _tokenManager = tokenManager;
        }


        [HttpGet]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated ?? false)
            {
                var validToken = await _tokenManager.GetValidTokenAsync();
                if (!string.IsNullOrEmpty(validToken))
                {
                    return RedirectToAction("Index", "Home");
                }

                // The cookie outlived the JWT, so sign out fully before showing the form
                await CleanupAuthenticationAsync();
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                try
                {
                    var result = await _authService.LoginAsync(model.Username, model.Password, model.RememberMe);
                    if (result != null && !string.IsNullOrEmpty(result.AccessToken))
                    {
                        // The access token stays in the server-side session and never reaches the browser
                        HttpContext.Session.SetString("JwtToken", result.AccessToken);

                        HttpContext.Session.SetString("UserData", JsonConvert.SerializeObject(new
                        {
                            result.User.Id,
                            result.User.Username,
                            result.User.Email,
                            result.User.FirstName,
                            result.User.LastName
                        }));

                        var rememberMe = result.RememberMe ?? model.RememberMe;

                        // HttpOnly so page scripts can't read the refresh token
                        var refreshCookieOptions = new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = Request.IsHttps,
                            SameSite = SameSiteMode.Strict,
                            Expires = rememberMe
                                ? DateTimeOffset.Now.AddDays(30)
                                : DateTimeOffset.Now.AddHours(1),
                            Path = "/",
                            IsEssential = true
                        };
                        Response.Cookies.Append("refresh_token", result.RefreshToken, refreshCookieOptions);

                        if (rememberMe)
                        {
                            var rememberCookieOptions = new CookieOptions
                            {
                                HttpOnly = false,
                                Secure = Request.IsHttps,
                                SameSite = SameSiteMode.Strict,
                                Expires = DateTimeOffset.Now.AddDays(365),
                                Path = "/"
                            };
                            Response.Cookies.Append("remember_me", "true", rememberCookieOptions);
                            Response.Cookies.Append("username", model.Username, rememberCookieOptions);
                        }
                        else
                        {
                            Response.Cookies.Delete("remember_me");
                            Response.Cookies.Delete("username");
                        }

                        HttpContext.Session.SetString("LastActivity", DateTime.Now.ToString("o"));

                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.NameIdentifier, result.User.Id.ToString()),
                            new Claim(ClaimTypes.Name, result.User.Username),
                            new Claim(ClaimTypes.Email, result.User.Email),
                            new Claim("FirstName", result.User.FirstName),
                            new Claim("LastName", result.User.LastName),
                            new Claim("RememberMe", rememberMe.ToString()),
                            new Claim("LoginTime", DateTime.Now.ToString("o"))
                        };

                        foreach (var role in result.User.Roles)
                        {
                            claims.Add(new Claim(ClaimTypes.Role, role));
                        }

                        foreach (var permission in result.User.Permissions)
                        {
                            claims.Add(new Claim("permission", permission));
                        }

                        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                        var authProperties = new AuthenticationProperties
                        {
                            IsPersistent = rememberMe,
                            ExpiresUtc = rememberMe
                                ? DateTimeOffset.Now.AddDays(30)
                                : DateTimeOffset.Now.AddHours(8),
                            AllowRefresh = true,
                            IssuedUtc = DateTimeOffset.Now
                        };

                        await HttpContext.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            new ClaimsPrincipal(claimsIdentity),
                            authProperties);

                        _logger.LogInformation(
                            "User {Username} logged in successfully with RememberMe={RememberMe}",
                            model.Username,
                            rememberMe);

                        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        {
                            return Redirect(returnUrl);
                        }

                        return RedirectToAction("Dashboard", "Home");
                    }

                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Login error");
                    ModelState.AddModelError(string.Empty, "An error occurred during login. Please try again.");
                }
            }

            return View(model);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            _logger?.LogInformation($"User - {User?.Identity?.Name} logged out");
            await CleanupAuthenticationAsync();
            return RedirectToAction("Login", "Account");
        }



        public IActionResult AccessDenied()
        {
            return View();
        }



        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return RedirectToAction("Login");
            }

            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                if (userId == 0)
                {
                    return RedirectToAction("Login");
                }

                var viewModel = await _userManagementService.GetUserProfileAsync(userId);

                if (viewModel == null)
                    return RedirectToAction("NotFound", "Home", "?statusCode=404");

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading user profile");
                TempData["Error"] = "An error occurred while loading your profile";
                return RedirectToAction("Dashboard", "Home");
            }
        }



        [HttpGet]
        public async Task<IActionResult> RefreshToken()
        {
            var refreshToken = Request.Cookies["refresh_token"];
            var accessToken = HttpContext.Session.GetString("JwtToken");

            if (string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(accessToken))
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "No refresh token available" });
                }
                return RedirectToAction("Login");
            }

            try
            {
                var result = await _authService.RefreshTokenAsync(refreshToken,accessToken);

                if (result != null)
                {
                    HttpContext.Session.SetString("JwtToken", result.AccessToken);
                    HttpContext.Session.SetString("UserData", JsonConvert.SerializeObject(new
                    {
                        result.User.Id,
                        result.User.Username,
                        result.User.Email,
                        result.User.FirstName,
                        result.User.LastName
                    }));
                    HttpContext.Session.SetString("LastActivity", DateTime.Now.ToString("o"));

                    // The API rotates refresh tokens, so the old one is already dead
                    var rememberMe = Request.Cookies["remember_me"] == "true";
                    var refreshCookieOptions = new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = Request.IsHttps,
                        SameSite = SameSiteMode.Strict,
                        Expires = rememberMe
                            ? DateTimeOffset.Now.AddDays(30)
                            : DateTimeOffset.Now.AddHours(1),
                        Path = "/",
                        IsEssential = true
                    };
                    Response.Cookies.Append("refresh_token", result.RefreshToken, refreshCookieOptions);

                    return Json(new
                    {
                        success = true
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Token refresh error");
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = false, message = "Token refresh failed" });
            }

            return RedirectToAction("Login");
        }



        [HttpGet]
        public async Task<IActionResult> ResetPassword(int id)
        {
            try
            {
                if (id == 0)
                    return RedirectToAction("NotFound", "Home", "?statusCode=404");

                var user = await _userManagementService.GetUserByIdAsync(id);
                if (user == null)
                    return RedirectToAction("NotFound", "Home", "?statusCode=404");

                var model = new ResetPasswordViewModel
                {
                    UserId = user.Id,
                    Username = user.Username
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading user profile");
                TempData["Error"] = "An error occurred while loading your profile";
                return RedirectToAction("Dashboard", "Home");
            }
        }
        

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return HandleValidationErrors(model);
            }

            try
            {
                var success = await _userManagementService.ResetPasswordAsync(model.UserId, model.NewPassword);

                if (success)
                {
                    TempData["Success"] = "Password reset successfully";
                    return RedirectToAction("Profile", "Account");
                }

                ModelState.AddModelError("", "Failed to reset password");
                return View(model);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading user profile");
                TempData["Error"] = "An error occurred while loading your profile";
                return RedirectToAction("Dashboard", "Home");
            }
        }


        private async Task CleanupAuthenticationAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            HttpContext.Session.Clear();

            Response.Cookies.Delete("refresh_token", new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });

            Response.Cookies.Delete("remember_me");
            Response.Cookies.Delete("username");
        }


        protected bool IsAjaxRequest()
        {
            return Request.Headers["X-Requested-With"] == "XMLHttpRequest";
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
    }
}