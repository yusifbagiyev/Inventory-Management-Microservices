using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProductService.Application.Events;
using ProductService.Application.Interfaces;
using ProductService.Domain.Repositories;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ProductService.Infrastructure.Services
{
    /// <summary>Moves a product to its new department when RouteService completes a transfer.</summary>
    public class RabbitMQConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RabbitMQConsumer> _logger;
        private IConnection? _connection;
        private IModel? _channel;
        private readonly string _queueName = "product-transfers";
        private readonly IConfiguration _configuration;

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
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                _channel.ExchangeDeclare("inventory-events", ExchangeType.Topic, durable: true);
                _channel.QueueDeclare(_queueName, durable: true, exclusive: false, autoDelete: false);
                _channel.QueueBind(_queueName, "inventory-events", "product.transferred");

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
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (sender, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    var transferEvent = JsonSerializer.Deserialize<ProductTransferredEvent>(message);
                    if (transferEvent != null)
                        await ProcessProductTransfer(transferEvent);

                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message");
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };
            _channel.BasicConsume(_queueName, false, consumer);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task ProcessProductTransfer(ProductTransferredEvent transferEvent)
        {
            using var scope = _serviceProvider.CreateScope();
            var productRepository = scope.ServiceProvider.GetRequiredService<IProductRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var imageService = scope.ServiceProvider.GetRequiredService<IImageService>();

            var product = await productRepository.GetByIdAsync(transferEvent.ProductId);
            if (product == null)
            {
                _logger.LogWarning($"Product {transferEvent.ProductId} not found");
                return;
            }

            product.UpdateAfterRouting(transferEvent.ToDepartmentId, transferEvent.ToWorker);

            // A transfer photo replaces the product's image
            if (transferEvent.ImageData != null && transferEvent.ImageData.Length > 0)
            {
                if (!string.IsNullOrEmpty(product.ImageUrl))
                    await imageService.DeleteImageAsync(product.ImageUrl);

                using var stream = new MemoryStream(transferEvent.ImageData);
                var imageUrl = await imageService.UploadImageAsync(
                    stream,
                    transferEvent.ImageFileName ?? $"{product.InventoryCode}.jpg",
                    product.InventoryCode);

                product.UpdateImage(imageUrl);
            }

            await productRepository.UpdateAsync(product);
            await unitOfWork.SaveChangesAsync();
        }

        public override void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            base.Dispose();
        }
    }
}