using ApprovalService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SharedServices.Enum;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace ApprovalService.Infrastructure.Services
{
    /// <summary>Runs an approved request by calling the owning service's approved endpoints over HTTP.</summary>
    public class ActionExecutor:IActionExecutor
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ActionExecutor> _logger;
        private readonly IConfiguration _configuration;
        private int? _currentUserId;
        private string? _currentUserName;

        public ActionExecutor(HttpClient httpClient,IConfiguration configuration,ILogger<ActionExecutor> logger)
        {
            _httpClient= httpClient;
            _configuration= configuration;
            _logger= logger;
        }

        public async Task<bool> ExecuteAsync(
            string requestType, 
            string actionData, 
            int userId,
            string userName,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _currentUserId = userId;
                _currentUserName = userName;

                AddAuthorizationHeader();

                return requestType switch
                {
                    RequestType.CreateProduct => await ExecuteCreateProduct(actionData, cancellationToken),
                    RequestType.UpdateProduct => await ExecuteUpdateProduct(actionData, cancellationToken),
                    RequestType.DeleteProduct => await ExecuteDeleteProduct(actionData, cancellationToken),
                    RequestType.TransferProduct => await ExecuteTransferProduct(actionData, cancellationToken),
                    RequestType.UpdateRoute => await ExecuteUpdateRoute(actionData, cancellationToken),
                    RequestType.DeleteRoute => await ExecuteDeleteRoute(actionData, cancellationToken),
                    _ => throw new NotSupportedException($"Request type '{requestType}' is not supported")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to execute action for request type: {requestType}");
                return false;
            }
        }

        // Signs a short-lived admin token so the target service skips approval and runs the action
        private void AddAuthorizationHeader()
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!);

            var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _currentUserId.ToString()!),
            new(ClaimTypes.Name, _currentUserName!),
            new(ClaimTypes.Role, "Admin"),
            new("permission","product.create.direct"),
            new("permission","product.update.direct"),
            new("permission","product.delete.direct"),
            new("permission","product.transfer.direct"),
            new("permission","route.update.direct"),
            new("permission","route.delete.direct")
        };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.Now.AddMinutes(5),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"]
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", tokenHandler.WriteToken(token));

            _logger.LogInformation("Created authorization token for admin {AdminName} (ID: {AdminId})",
                _currentUserName, _currentUserId);
        }

        private async Task<bool> ExecuteCreateProduct(string actionData, CancellationToken cancellationToken)
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(actionData);
                var root = jsonDoc.RootElement;

                // Older requests nest the fields under ProductData, newer ones keep them flat
                JsonElement productElement = GetProductElement(root);

                var inventoryCode = GetIntProperty(productElement, "inventoryCode", "InventoryCode");
                var model = GetStringProperty(productElement, "model", "Model");
                var vendor = GetStringProperty(productElement, "vendor", "Vendor");
                var worker = GetStringProperty(productElement, "worker", "Worker");
                var description = GetStringProperty(productElement, "description", "Description");
                var isWorking = GetBoolProperty(productElement, "isWorking", "IsWorking", true);
                var isActive = GetBoolProperty(productElement, "isActive", "IsActive", true);
                var isNewItem = GetBoolProperty(productElement, "isNewItem", "IsNewItem", true);
                var categoryId = GetIntProperty(productElement, "categoryId", "CategoryId");
                var departmentId = GetIntProperty(productElement, "departmentId", "DepartmentId");

                var imageData = GetImageData(productElement);
                var imageFileName = GetImageFileName(productElement);

                if (imageData != null && imageFileName != null)
                {
                    _logger.LogInformation($"Creating product {inventoryCode} with image {imageFileName}");

                    using var formContent = new MultipartFormDataContent();

                    formContent.Add(new StringContent(inventoryCode.ToString()), "InventoryCode");
                    formContent.Add(new StringContent(model), "Model");
                    formContent.Add(new StringContent(vendor), "Vendor");
                    formContent.Add(new StringContent(worker), "Worker");
                    formContent.Add(new StringContent(description), "Description");
                    formContent.Add(new StringContent(isWorking.ToString()), "IsWorking");
                    formContent.Add(new StringContent(isActive.ToString()), "IsActive");
                    formContent.Add(new StringContent(isNewItem.ToString()), "IsNewItem");
                    formContent.Add(new StringContent(categoryId.ToString()), "CategoryId");
                    formContent.Add(new StringContent(departmentId.ToString()), "DepartmentId");

                    var imageContent = new ByteArrayContent(imageData);
                    imageContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(imageFileName));
                    formContent.Add(imageContent, "ImageFile", imageFileName);

                    var response = await _httpClient.PostAsync(
                        $"{_configuration["Services:ProductService"]}/api/products/approved/multipart",
                        formContent,
                        cancellationToken);

                    LogResponseIfError(response, "create product with image");
                    return response.IsSuccessStatusCode;
                }
                else
                {
                    _logger.LogInformation($"Creating product {inventoryCode} without image");

                    var productDto = new
                    {
                        inventoryCode,
                        model,
                        vendor,
                        worker,
                        description,
                        isWorking,
                        isActive,
                        isNewItem,
                        categoryId,
                        departmentId
                    };

                    var json = JsonSerializer.Serialize(productDto);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(
                        $"{_configuration["Services:ProductService"]}/api/products/approved",
                        content,
                        cancellationToken);

                    LogResponseIfError(response, "create product");
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing create product");
                return false;
            }
        }

        private async Task<bool> ExecuteUpdateProduct(string actionData, CancellationToken cancellationToken)
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(actionData);
                var root = jsonDoc.RootElement;

                var productId = GetIntProperty(root, "productId", "ProductId");
                if (productId == 0)
                {
                    _logger.LogError("Product ID not found in update action data");
                    return false;
                }

                JsonElement updateDataElement =GetUpdateDataElement(root);

                var model = GetStringProperty(updateDataElement, "model", "Model");
                var vendor = GetStringProperty(updateDataElement, "vendor", "Vendor");
                var worker = GetStringProperty(updateDataElement, "worker", "Worker");
                var description = GetStringProperty(updateDataElement, "description", "Description");
                var categoryId = GetIntProperty(updateDataElement, "categoryId", "CategoryId");
                var departmentId = GetIntProperty(updateDataElement, "departmentId", "DepartmentId");
                var isWorking = GetBoolProperty(updateDataElement, "isWorking", "IsWorking", true);
                var isActive = GetBoolProperty(updateDataElement, "isActive", "IsActive", true);
                var isNewItem = GetBoolProperty(updateDataElement, "isNewItem", "IsNewItem", true);

                var imageData = GetImageData(updateDataElement);
                var imageFileName = GetImageFileName(updateDataElement);

                if (imageData != null && imageFileName != null)
                {
                    _logger.LogInformation($"Updating product {productId} with new image");

                    using var formContent = new MultipartFormDataContent();

                    formContent.Add(new StringContent(model), "Model");
                    formContent.Add(new StringContent(vendor), "Vendor");
                    formContent.Add(new StringContent(worker), "Worker");
                    formContent.Add(new StringContent(description), "Description");
                    formContent.Add(new StringContent(categoryId.ToString()), "CategoryId");
                    formContent.Add(new StringContent(departmentId.ToString()), "DepartmentId");
                    formContent.Add(new StringContent(isWorking.ToString()), "IsWorking");
                    formContent.Add(new StringContent(isActive.ToString()), "IsActive");
                    formContent.Add(new StringContent(isNewItem.ToString()), "IsNewItem");

                    var imageContent = new ByteArrayContent(imageData);
                    imageContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(imageFileName));
                    formContent.Add(imageContent, "ImageFile", imageFileName);

                    var response = await _httpClient.PutAsync(
                        $"{_configuration["Services:ProductService"]}/api/products/{productId}/approved/multipart",
                        formContent,
                        cancellationToken);

                    LogResponseIfError(response, $"update product {productId}");
                    return response.IsSuccessStatusCode;
                }
                else
                {
                    _logger.LogInformation($"Updating product {productId} without image change");

                    var updateDto = new
                    {
                        model,
                        vendor,
                        worker,
                        description,
                        categoryId,
                        departmentId,
                        isWorking,
                        isActive,
                        isNewItem
                    };

                    var json = JsonSerializer.Serialize(updateDto);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PutAsync(
                        $"{_configuration["Services:ProductService"]}/api/products/{productId}/approved",
                        content,
                        cancellationToken);

                    LogResponseIfError(response, $"update product {productId}");
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing update product");
                return false;
            }
        }

        private async Task<bool> ExecuteDeleteProduct(string actionData, CancellationToken cancellationToken)
        {
            try
            {
                var jsonDoc=JsonDocument.Parse(actionData);
                var root=jsonDoc.RootElement;

                var productId = GetIntProperty(root, "productId", "ProductId");
                if (productId == 0)
                {
                    _logger.LogError("Product ID not found in delete action data");
                    return false;
                }
                _logger.LogInformation($"Deleting product {productId}");

                var response = await _httpClient.DeleteAsync(
                    $"{_configuration["Services:ProductService"]}/api/products/{productId}/approved",
                    cancellationToken);

                LogResponseIfError(response, $"delete product {productId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing delete product");
                return false;
            }
        }

        private async Task<bool> ExecuteTransferProduct(string actionData, CancellationToken cancellationToken)
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(actionData);
                var root = jsonDoc.RootElement;

                var productId = GetIntProperty(root, "productId", "ProductId");
                var toDepartmentId = GetIntProperty(root, "toDepartmentId", "ToDepartmentId");
                var toWorker = GetStringProperty(root, "toWorker", "ToWorker");
                var notes = GetStringProperty(root, "notes", "Notes");

                if (productId == 0 || toDepartmentId == 0)
                {
                    _logger.LogError("Invalid transfer data: missing product or department ID");
                    return false;
                }

                _logger.LogInformation($"Transferring product {productId} to department {toDepartmentId}");

                using var formContent = new MultipartFormDataContent();

                formContent.Add(new StringContent(productId.ToString()), "ProductId");
                formContent.Add(new StringContent(toDepartmentId.ToString()), "ToDepartmentId");
                formContent.Add(new StringContent(toWorker), "ToWorker");
                formContent.Add(new StringContent(notes), "Notes");

                var imageData = GetImageData(root);
                var imageFileName = GetImageFileName(root);

                if (imageData != null && imageFileName != null)
                {
                    _logger.LogInformation($"Transfer includes image: {imageFileName}");
                    var imageContent = new ByteArrayContent(imageData);
                    imageContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(imageFileName));
                    formContent.Add(imageContent, "ImageFile", imageFileName);
                }

                var response = await _httpClient.PostAsync(
                    $"{_configuration["Services:RouteService"]}/api/inventoryroutes/transfer/approved",
                    formContent,
                    cancellationToken);

                LogResponseIfError(response, $"transfer product {productId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing transfer product");
                return false;
            }
        }

        private async Task<bool> ExecuteUpdateRoute(string actionData, CancellationToken cancellationToken)
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(actionData);
                var root = jsonDoc.RootElement;

                var routeId = GetIntProperty(root, "routeId", "RouteId");
                if (routeId == 0)
                {
                    _logger.LogError("Route ID not found in update action data");
                    return false;
                }

                JsonElement updateDataElement = GetUpdateDataElement(root);

                var updateDto = new
                {
                    notes = GetStringProperty(updateDataElement, "notes", "Notes")
                };

                _logger.LogInformation($"Updating route {routeId}");

                var json = JsonSerializer.Serialize(updateDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(
                    $"{_configuration["Services:RouteService"]}/api/inventoryroutes/{routeId}/approved",
                    content,
                    cancellationToken);

                LogResponseIfError(response, $"update route {routeId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing update route");
                return false;
            }
        }

        private async Task<bool> ExecuteDeleteRoute(string actionData, CancellationToken cancellationToken)
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(actionData);
                var root = jsonDoc.RootElement;

                var routeId = GetIntProperty(root, "routeId", "RouteId");
                if (routeId == 0)
                {
                    _logger.LogError("Route ID not found in delete action data");
                    return false;
                }

                _logger.LogInformation($"Deleting route {routeId}");

                var response = await _httpClient.DeleteAsync(
                    $"{_configuration["Services:RouteService"]}/api/inventoryroutes/{routeId}/approved",
                    cancellationToken);

                LogResponseIfError(response, $"delete route {routeId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing delete route");
                return false;
            }
        }


        private JsonElement GetProductElement(JsonElement root)
        {
            if (root.TryGetProperty("ProductData", out var productDataProp))
                return productDataProp;

            if (root.TryGetProperty("productData", out var productDataCamel))
                return productDataCamel;

            return root;
        }

        private JsonElement GetUpdateDataElement(JsonElement root)
        {
            if (root.TryGetProperty("UpdateData", out var updateDataProp))
                return updateDataProp;

            if (root.TryGetProperty("updateData", out var updateDataCamel))
                return updateDataCamel;

            return root;
        }

        private byte[]? GetImageData(JsonElement element)
        {
            string? base64Data = null;

            var propertyNames = new[] { "imageData", "ImageData", "image", "Image" };

            foreach (var propName in propertyNames)
            {
                if (element.TryGetProperty(propName, out var prop) &&
                    prop.ValueKind == JsonValueKind.String)
                {
                    base64Data = prop.GetString();
                    if (!string.IsNullOrEmpty(base64Data))
                        break;
                }
            }

            if (!string.IsNullOrEmpty(base64Data))
            {
                try
                {
                    // Strip the data URL prefix if the image came as one
                    if (base64Data.Contains(","))
                    {
                        base64Data = base64Data.Split(',')[1];
                    }

                    return Convert.FromBase64String(base64Data);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to decode image data: {ex.Message}");
                }
            }

            return null;
        }

        private string? GetImageFileName(JsonElement element)
        {
            var propertyNames = new[] { "imageFileName", "ImageFileName", "imageName", "ImageName", "filename", "FileName" };

            foreach (var propName in propertyNames)
            {
                if (element.TryGetProperty(propName, out var prop) &&
                    prop.ValueKind == JsonValueKind.String)
                {
                    var value = prop.GetString();
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }
            }

            return null;
        }

        // Older requests were stored in PascalCase, so every field is looked up in both casings
        private string GetStringProperty(JsonElement element, string camelCase, string pascalCase)
        {
            if (element.TryGetProperty(camelCase, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.String)
                    return prop.GetString() ?? "";
                if (prop.ValueKind != JsonValueKind.Null && prop.ValueKind != JsonValueKind.Undefined)
                    return prop.ToString();
            }

            if (element.TryGetProperty(pascalCase, out var propPascal))
            {
                if (propPascal.ValueKind == JsonValueKind.String)
                    return propPascal.GetString() ?? "";
                if (propPascal.ValueKind != JsonValueKind.Null && propPascal.ValueKind != JsonValueKind.Undefined)
                    return propPascal.ToString();
            }

            return "";
        }

        private bool GetBoolProperty(JsonElement element, string camelCase, string pascalCase, bool defaultValue)
        {
            if (element.TryGetProperty(camelCase, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.True) return true;
                if (prop.ValueKind == JsonValueKind.False) return false;

                if (prop.ValueKind == JsonValueKind.String)
                {
                    var strValue = prop.GetString()?.ToLower();
                    if (strValue == "true") return true;
                    if (strValue == "false") return false;
                }
            }

            if (element.TryGetProperty(pascalCase, out var propPascal))
            {
                if (propPascal.ValueKind == JsonValueKind.True) return true;
                if (propPascal.ValueKind == JsonValueKind.False) return false;

                if (propPascal.ValueKind == JsonValueKind.String)
                {
                    var strValue = propPascal.GetString()?.ToLower();
                    if (strValue == "true") return true;
                    if (strValue == "false") return false;
                }
            }

            return defaultValue;
        }

        private int GetIntProperty(JsonElement element, string camelCase, string pascalCase)
        {
            if (element.TryGetProperty(camelCase, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number)
                    return prop.GetInt32();

                if (prop.ValueKind == JsonValueKind.String &&
                    int.TryParse(prop.GetString(), out var parsed))
                    return parsed;
            }

            if (element.TryGetProperty(pascalCase, out var propPascal))
            {
                if (propPascal.ValueKind == JsonValueKind.Number)
                    return propPascal.GetInt32();

                if (propPascal.ValueKind == JsonValueKind.String &&
                    int.TryParse(propPascal.GetString(), out var parsed))
                    return parsed;
            }

            return 0;
        }

        private string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName)?.ToLowerInvariant() ?? "";
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream"
            };
        }

        private async void LogResponseIfError(HttpResponseMessage response, string operation)
        {
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to {operation}: Status={response.StatusCode}, Content={content}");
            }
        }
    }
}