using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProductService.Application.Interfaces;
using RabbitMQ.Client;

namespace ProductService.Infrastructure.Services
{
    public class RabbitMQPublisher : IMessagePublisher, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly string _exchangeName = "inventory-events";
        private readonly ILogger<RabbitMQPublisher> _logger;

        public RabbitMQPublisher(IConfiguration configuration, ILogger<RabbitMQPublisher> logger)
        {
            _logger = logger;


            var hostname = configuration["RabbitMQ:HostName"] ??
                           Environment.GetEnvironmentVariable("RabbitMQ__HostName") ??
                           "localhost";

            var username = configuration["RabbitMQ:UserName"] ??
                          Environment.GetEnvironmentVariable("RabbitMQ__UserName") ??
                          "guest";

            var password = configuration["RabbitMQ:Password"] ??
                          Environment.GetEnvironmentVariable("RabbitMQ__Password") ??
                          "guest";

            var port = int.Parse(configuration["RabbitMQ:Port"] ??
                                Environment.GetEnvironmentVariable("RabbitMQ__Port") ??
                                "5672");

            _logger.LogInformation($"Connecting to RabbitMQ at {hostname}:{port} with user {username}");


            var factory = new ConnectionFactory
            {
                HostName = hostname,
                UserName = username,
                Password = password,
                Port = port,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            try
            {
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                _channel.ExchangeDeclare(_exchangeName, ExchangeType.Topic, durable: true);
                _logger.LogInformation("Successfully connected to RabbitMQ");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to connect to RabbitMQ at {hostname}:{port}");
                throw;
            }
        }

        public async Task PublishAsync<T>(T message, string routingKey, CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            await Task.Run(() =>
            {
                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;

                _channel.BasicPublish(
                    exchange: _exchangeName,
                    routingKey: routingKey,
                    basicProperties: properties,
                    body: body);

                _logger.LogDebug($"Published message to {routingKey}");


            }, cancellationToken);
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}