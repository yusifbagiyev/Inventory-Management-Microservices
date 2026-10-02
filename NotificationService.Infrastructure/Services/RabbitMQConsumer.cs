using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Events;
using NotificationService.Application.Extensions;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Repositories;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NotificationService.Infrastructure.Services
{
    public class RabbitMQConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RabbitMQConsumer> _logger;
        private IConnection? _connection;
        private IModel? _channel;
        private readonly IConfiguration _configuration;
        private readonly string _queueName = "notification-queue";

        public RabbitMQConsumer(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<RabbitMQConsumer> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;

            InitializeRabbitMQ();
        }
        private void InitializeRabbitMQ()
        {
            try
            {
                var hostname = _configuration["RabbitMQ:HostName"] ??
                                              Environment.GetEnvironmentVariable("RabbitMQ__HostName") ??
                                              "localhost";

                var username = _configuration["RabbitMQ:UserName"] ??
                              Environment.GetEnvironmentVariable("RabbitMQ__UserName") ??
                              "guest";

                var password = _configuration["RabbitMQ:Password"] ??
                              Environment.GetEnvironmentVariable("RabbitMQ__Password") ??
                              "guest";

                var port = int.Parse(_configuration["RabbitMQ:Port"] ??
                                    Environment.GetEnvironmentVariable("RabbitMQ__Port") ??
                                    "5672");

                _logger.LogInformation($"Connecting RabbitMQ Consumer to {hostname}:{port} with user {username}");

                var factory = new ConnectionFactory
                {
                    HostName = hostname,
                    UserName = username,
                    Password = password,
                    Port = port,
                    DispatchConsumersAsync=true,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                _channel.ExchangeDeclare("inventory-events", ExchangeType.Topic, durable: true);
                _channel.QueueDeclare(_queueName, durable: true, exclusive: false, autoDelete: false);
                _channel.QueueBind(_queueName, "inventory-events", "approval.request.created");
                _channel.QueueBind(_queueName, "inventory-events", "approval.request.processed");
                _channel.QueueBind(_queueName, "inventory-events", "approval.request.cancelled");
                _channel.QueueBind(_queueName, "inventory-events", "product.created");
                _channel.QueueBind(_queueName, "inventory-events", "product.deleted");
                _channel.QueueBind(_queueName, "inventory-events", "product.updated");
                _channel.QueueBind(_queueName, "inventory-events", "route.created");
                _channel.QueueBind(_queueName, "inventory-events", "route.completed");

                _logger.LogInformation("RabbitMQ Consumer successfully connected");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize RabbitMQ connection");
                throw;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.Received += async (sender, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var routingKey = ea.RoutingKey;

                    await ProcessMessage(routingKey, message);

                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message");
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume(_queueName, false, consumer);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        private async Task ProcessMessage(string routingKey, string message)
        {
            switch (routingKey)
            {
                case "approval.request.created":
                    await HandleApprovalRequestCreated(message);
                    break;
                case "approval.request.processed":
                    await HandleApprovalRequestProcessed(message);
                    break;
                case "approval.request.cancelled":
                    await HandleApprovalRequestCancelled(message);
                    break;
                case "product.created":
                    await HandleProductCreated(message);
                    break;
                case "product.deleted":
                    await HandleProductDeleted(message);
                    break;
                case "route.created":
                    await HandleRouteCreated(message);
                    break;
                case "route.completed":
                    await HandleRouteCompleted(message);
                    break;
                default:
                    _logger.LogWarning($"Unknown routing key: {routingKey}");
                    break;
            }
        }

        private async Task HandleApprovalRequestCreated(string message)
        {
            try
            {
                var approvalEvent = JsonSerializer.Deserialize<ApprovalRequestCreatedEvent>(message);
                if (approvalEvent == null) return;

                using var scope = _serviceProvider.CreateScope();
                var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub>>();

                var adminUsers = await userService.GetUsersAsync("Admin");
                _logger.LogInformation($"Found {adminUsers.Count} admin users to notify");

                var readableType = GetReadableRequestType(approvalEvent.RequestType);
                var actionDescription = GetActionDescription(approvalEvent.RequestType);

                var tasks = adminUsers.Select(async admin =>
                {
                    var notification = new Notification(
                        admin.Id,
                        "ApprovalRequest",
                        $"New {readableType} Request",
                        $"{approvalEvent.RequestedByName} has requested to {actionDescription}. Request #{approvalEvent.RequestId} needs your approval.",
                        JsonSerializer.Serialize(new
                        {
                            approvalRequestId = approvalEvent.RequestId,
                            requestType = approvalEvent.RequestType,
                            requestedBy = approvalEvent.RequestedByName
                        })
                    );

                    await SaveAndSendNotification(notification);
                });

                await Task.WhenAll(tasks);

                // Lets open approval pages reload their list.
                await hubContext.Clients.Group("role-Admin").SendAsync("RefreshApprovals", new
                {
                    requestId = approvalEvent.RequestId,
                    requestType = approvalEvent.RequestType
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling approval requested event");
            }
        }

        private async Task HandleApprovalRequestProcessed(string message)
        {
            try
            {
                var approvalEvent = JsonSerializer.Deserialize<ApprovalRequestProcessedEvent>(message);
                if (approvalEvent == null) return;

                var readableType = GetReadableRequestType(approvalEvent.RequestType);
                var actionDescription = GetActionDescription(approvalEvent.RequestType);

                string title;
                string messageText;

                if (approvalEvent.Status == "Approved" || approvalEvent.Status == "Executed")
                {
                    title = "Request Approved ✓";
                    messageText = $"Your request to {actionDescription} (Request #{approvalEvent.RequestId}) has been approved by {approvalEvent.ProcessedByName}.";
                }
                else if (approvalEvent.Status == "Rejected")
                {
                    title = "Request Rejected ✗";
                    messageText = $"Your request to {actionDescription} (Request #{approvalEvent.RequestId}) has been rejected by {approvalEvent.ProcessedByName}.";
                    if (!string.IsNullOrEmpty(approvalEvent.RejectionReason))
                    {
                        messageText += $" Reason: {approvalEvent.RejectionReason}";
                    }
                }
                else if (approvalEvent.Status == "Failed")
                {
                    title = "Request Failed ⚠";
                    messageText = $"Your request to {actionDescription} (Request #{approvalEvent.RequestId}) was approved but failed to execute.";
                    if (!string.IsNullOrEmpty(approvalEvent.RejectionReason))
                    {
                        messageText += $" Error: {approvalEvent.RejectionReason}";
                    }
                }
                else
                {
                    title = "Request Updated";
                    messageText = $"Your {readableType} request (#{approvalEvent.RequestId}) status has been updated to: {approvalEvent.Status}";
                }

                var notification = new Notification(
                    approvalEvent.RequestedById,
                    "ApprovalResponse",
                    title,
                    messageText,
                    JsonSerializer.Serialize(new
                    {
                        approvalRequestId = approvalEvent.RequestId,
                        status = approvalEvent.Status,
                        processedBy = approvalEvent.ProcessedByName
                    })
                );

                await SaveAndSendNotification(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling approval processed event");
            }
        }

        private async Task HandleApprovalRequestCancelled(string message)
        {
            try
            {
                var cancelEvent = JsonSerializer.Deserialize<ApprovalRequestCancelledEvent>(message);
                if (cancelEvent == null) return;

                using var scope = _serviceProvider.CreateScope();
                var notificationRepo = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
                var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var notifications = await notificationRepo.GetByUserIdAsync(cancelEvent.RequestedById, false);

                foreach (var notification in notifications)
                {
                    // The request id may be stored under either key.
                    if (notification.Data?.Contains($"\"approvalRequestId\":{cancelEvent.RequestId}") == true ||
                        notification.Data?.Contains($"\"RequestId\":{cancelEvent.RequestId}") == true)
                    {
                        await notificationRepo.DeleteAsync(notification);
                        _logger.LogInformation($"Deleted notification {notification.Id} for cancelled request {cancelEvent.RequestId}");
                    }
                }

                var adminUsers = await userService.GetUsersAsync("Admin");
                foreach (var admin in adminUsers)
                {
                    var adminNotifications = await notificationRepo.GetByUserIdAsync(admin.Id, false);

                    foreach (var notification in adminNotifications)
                    {
                        if (notification.Data?.Contains($"\"approvalRequestId\":{cancelEvent.RequestId}") == true)
                        {
                            await notificationRepo.DeleteAsync(notification);
                            _logger.LogInformation($"Deleted admin notification {notification.Id} for cancelled request {cancelEvent.RequestId}");
                        }
                    }
                }

                await unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling approval request cancellation");
            }
        }

        private async Task HandleProductCreated(string message)
        {
            try
            {
                var productEvent = JsonSerializer.Deserialize<ProductCreatedEvent>(message);
                if (productEvent == null) return;

                await SendWhatsAppProductNotification(productEvent, "created");

                var allUsers = await GetAllUsersId();

                foreach (var userId in allUsers)
                {
                    var notification = new Notification(
                        userId,
                        "ProductUpdate",
                        "New Product Added",
                        $"Product {productEvent.Model} by {productEvent.Vendor} (Code: {productEvent.InventoryCode}) has been added to {productEvent.DepartmentName}",
                        JsonSerializer.Serialize(new
                        {
                            productId = productEvent.ProductId,
                            inventoryCode = productEvent.InventoryCode,
                            model = productEvent.Model
                        })
                    );

                    await SaveAndSendNotification(notification);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling product created event");
            }
        }

        private async Task HandleProductDeleted(string message)
        {
            try
            {
                var productEvent = JsonSerializer.Deserialize<ProductDeletedEvent>(message);
                if (productEvent == null) return;

                var allUsers = await GetAllUsersId();

                foreach (var userId in allUsers)
                {
                    var notification = new Notification(
                        userId,
                        "ProductUpdate",
                        "Product Deleted",
                        $"Product {productEvent.Model} (Code: {productEvent.InventoryCode}) has been deleted from {productEvent.DepartmentName}",
                        JsonSerializer.Serialize(new
                        {
                            productId = productEvent.ProductId,
                            inventoryCode = productEvent.InventoryCode,
                            departmentName = productEvent.DepartmentName
                        })
                    );

                    await SaveAndSendNotification(notification);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling product deleted event");
            }
        }

        private async Task HandleRouteCreated(string message)
        {
            try
            {
                var routeEvent = JsonSerializer.Deserialize<RouteCreatedEvent>(message);
                if (routeEvent == null) return;

                var allUsers = await GetAllUsersId();

                foreach (var userId in allUsers)
                {
                    var notification = new Notification(
                        userId,
                        "RouteUpdate",
                        "Incoming Product Transfer",
                        $"Product {routeEvent.Model} (Code: {routeEvent.InventoryCode}) is being transferred to {routeEvent.ToDepartmentName}",
                        JsonSerializer.Serialize(new
                        {
                            routeId = routeEvent.RouteId,
                            productId = routeEvent.ProductId
                        })
                    );

                    await SaveAndSendNotification(notification);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling route created event");
            }
        }

        private async Task HandleRouteCompleted(string message)
        {
            try
            {
                var routeEvent = JsonSerializer.Deserialize<RouteCompletedEvent>(message);
                if (routeEvent == null) return;

                await SendWhatsAppRouteNotification(routeEvent, "transferred");

                var allUsers = await GetAllUsersId();

                foreach (var userId in allUsers)
                {
                    var notification = new Notification(
                        userId,
                        "RouteUpdate",
                        "Transfer Completed",
                        $"Product {routeEvent.Model} (Code: {routeEvent.InventoryCode}) transfer to {routeEvent.ToDepartmentName} has been completed",
                        JsonSerializer.Serialize(new
                        {
                            routeId = routeEvent.RouteId,
                            productId = routeEvent.ProductId
                        })
                    );

                    await SaveAndSendNotification(notification);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling route completed event");
            }
        }

        private async Task SaveAndSendNotification(Notification notification)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub>>();

                await repository.AddAsync(notification);
                await unitOfWork.SaveChangesAsync();
                _logger.LogInformation($"✅ Notification saved to database for user {notification.UserId}");

                var notificationDto = new
                {
                    id = notification.Id,
                    type = notification.Type,
                    title = notification.Title,
                    message = notification.Message,
                    createdAt = notification.CreatedAt,
                    isRead = notification.IsRead,
                    data = notification.Data
                };

                var userGroup = $"user-{notification.UserId}";

                await hubContext.Clients.Group(userGroup).SendAsync("ReceiveNotification", notificationDto);

                _logger.LogInformation($"📤 Notification sent via SignalR to group {userGroup}");
                _logger.LogInformation($"📦 Notification content: Type={notification.Type}, Title={notification.Title}, Message={notification.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving/sending notification for user {notification.UserId}");
                throw;
            }
        }

        private async Task SendWhatsAppProductNotification(ProductCreatedEvent productEvent, string notificationType)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var whatsAppService = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();
                var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                var whatsAppEnabled = configuration.GetValue<bool>("Whatsapp:Enabled", true);
                if (!whatsAppEnabled)
                {
                    _logger.LogInformation("Whatsapp notifications are disabled");
                    return;
                }

                var groupId = configuration["Whatsapp:DefaultGroupId"];
                if (string.IsNullOrEmpty(groupId))
                {
                    _logger.LogWarning("WhatsApp DefaultGroupId not configured");
                    return;
                }

                var notification = new WhatsAppProductNotification
                {
                    ProductId = productEvent.ProductId,
                    InventoryCode = productEvent.InventoryCode,
                    Model = productEvent.Model,
                    Vendor = productEvent.Vendor,
                    CategoryName = productEvent.CategoryName,
                    ToDepartmentName = productEvent.DepartmentName,
                    ToWorker = productEvent.Worker,
                    CreatedAt = productEvent.CreatedAt,
                    IsNewItem = productEvent.IsNewItem,
                    IsWorking = productEvent.IsWorking,
                    Notes = productEvent.Description,
                    NotificationType = notificationType,
                    ImageUrl = productEvent.ImageUrl,
                    ImageData = productEvent.ImageData,
                    ImageFileName = productEvent.ImageFileName
                };

                var message = whatsAppService.FormatNotification(notification);

                bool success;

                if (productEvent.ImageUrl != null && productEvent.ImageData?.Length > 0)
                {
                    success = await whatsAppService.SendGroupMessageWithImageDataAsync(
                        groupId,
                        message,
                        productEvent.ImageData,
                        productEvent.ImageFileName ?? $"product_{productEvent.InventoryCode}.jpg");
                }
                else if (!string.IsNullOrEmpty(productEvent.ImageUrl))
                {
                    try
                    {
                        var baseUrl = configuration["Services:ProductServiceUrl"] ?? "http://localhost:5001";
                        var fullImageUrl = $"{baseUrl}{productEvent.ImageUrl}";

                        using var client = new HttpClient();
                        var imageBytes = await client.GetByteArrayAsync(fullImageUrl);

                        _logger.LogInformation($"Fetched image from URL for product {productEvent.InventoryCode}, sending to WhatsApp");
                        success = await whatsAppService.SendGroupMessageWithImageDataAsync(
                            groupId,
                            message,
                            imageBytes,
                            Path.GetFileName(productEvent.ImageUrl) ?? $"product_{productEvent.InventoryCode}.jpg");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation($"No image available for product {productEvent.InventoryCode}, sending text only");
                        success = await whatsAppService.SendGroupMessageAsync(groupId, message);
                    }
                }
                else
                {
                    success = await whatsAppService.SendGroupMessageAsync(groupId, message);
                }

                if (success)
                {
                    _logger.LogInformation($"WhatsApp notification sent for product {productEvent.InventoryCode}");
                }
                else
                {
                    _logger.LogWarning($"Failed to send WhatsApp notification for product {productEvent.InventoryCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending WhatsApp notification");
            }
        }

        private async Task SendWhatsAppRouteNotification(RouteCompletedEvent routeEvent, string notificationType)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var whatsAppService = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();
                var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                var httpClientFactory = scope.ServiceProvider.GetService<IHttpClientFactory>();

                var whatsAppEnabled = configuration.GetValue<bool>("Whatsapp:Enabled", true);
                if (!whatsAppEnabled)
                {
                    _logger.LogInformation("Whatsapp notifications are disabled");
                    return;
                }

                var groupId = configuration["Whatsapp:DefaultGroupId"];
                if (string.IsNullOrEmpty(groupId))
                {
                    _logger.LogWarning("WhatsApp DefaultGroupId not configured");
                    return;
                }

                var notification = new WhatsAppProductNotification
                {
                    ProductId = routeEvent.ProductId,
                    InventoryCode = routeEvent.InventoryCode,
                    Model = routeEvent.Model,
                    Vendor = routeEvent.Vendor,
                    CategoryName = routeEvent.CategoryName,
                    FromDepartmentName = routeEvent.FromDepartmentName,
                    FromWorker = routeEvent.FromWorker,
                    ToDepartmentName = routeEvent.ToDepartmentName,
                    ToWorker = routeEvent.ToWorker,
                    CreatedAt = routeEvent.CompletedAt,
                    Notes = routeEvent.Notes,
                    NotificationType = notificationType
                };

                var message = whatsAppService.FormatNotification(notification);

                bool success;

                if (routeEvent.ImageUrl != null & routeEvent.ImageData?.Length > 0)
                {
                    success = await whatsAppService.SendGroupMessageWithImageDataAsync(
                        groupId,
                        message,
                        routeEvent.ImageData!,
                        routeEvent.ImageFileName ?? $"route_{routeEvent.InventoryCode}.jpg");
                }
                else if (!string.IsNullOrEmpty(routeEvent.ImageUrl))
                {
                    try
                    {
                        var baseUrl = configuration["Services:RouteServiceUrl"] ?? "http://localhost:5002";
                        var fullImageUrl = $"{baseUrl}{routeEvent.ImageUrl}";

                        using var client = new HttpClient();
                        var imageBytes = await client.GetByteArrayAsync(fullImageUrl);

                        _logger.LogInformation($"Fetched image from URL for route {routeEvent.InventoryCode}, sending to WhatsApp");
                        success = await whatsAppService.SendGroupMessageWithImageDataAsync(
                            groupId,
                            message,
                            imageBytes,
                            Path.GetFileName(routeEvent.ImageUrl) ?? $"route_{routeEvent.InventoryCode}.jpg");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation($"No image available for route {routeEvent.InventoryCode}, sending text only");
                        success = await whatsAppService.SendGroupMessageAsync(groupId, message);
                    }
                }
                else
                {
                    success = await whatsAppService.SendGroupMessageAsync(groupId, message);
                }
                if (success)
                {
                    _logger.LogInformation($"WhatsApp notification sent for route {routeEvent.InventoryCode}");
                }
                else
                {
                    _logger.LogWarning($"Failed to send WhatsApp notification for route {routeEvent.InventoryCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending WhatsApp notification");
            }
        }

        private async Task<List<int>> GetAllUsersId()
        {
            using var scope = _serviceProvider.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

            var users = await userService.GetUsersAsync("User");
            var operators = await userService.GetUsersAsync("Operator");
            var admins = await userService.GetUsersAsync("Admin");

            return users.Select(u=>u.Id)
                .Union(operators.Select(u => u.Id))
                .Union(admins.Select(u => u.Id))
                .Distinct()
                .ToList();
        }

        private string GetReadableRequestType(string requestType)
        {
            return requestType switch
            {
                "product.create" => "Product Creation",
                "product.update" => "Product Update",
                "product.delete" => "Product Deletion",
                "product.transfer" => "Product Transfer",
                "route.update" => "Route Update",
                "route.delete" => "Route Deletion",
                _ => requestType.Replace(".", " ").ToTitleCase()
            };
        }

        private string GetActionDescription(string requestType)
        {
            return requestType switch
            {
                "product.create" => "create a new product",
                "product.update" => "update product information",
                "product.delete" => "delete a product",
                "product.transfer" => "transfer a product to another department",
                "route.update" => "update route information",
                "route.delete" => "delete a route",
                _ => requestType.Replace(".", " ")
            };
        }

        public override void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            base.Dispose();
        }
    }
}