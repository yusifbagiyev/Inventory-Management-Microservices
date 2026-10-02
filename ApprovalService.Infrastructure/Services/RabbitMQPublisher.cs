using ApprovalService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace ApprovalService.Infrastructure.Services
{
    public class RabbitMQPublisher : IMessagePublisher, IDisposable
    {
        private IConnection _connection;
        private IModel _channel;
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

                _channel.ExchangeDeclare("inventory-events", ExchangeType.Topic, durable: true);
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

            _logger.LogInformation($"Publishing message to RabbitMQ: RoutingKey={routingKey}");
            _logger.LogDebug($"Message content: {json}");

            await Task.Run(() =>
            {
                try
                {
                    var properties = _channel.CreateBasicProperties();
                    properties.Persistent = true;

                    _channel.BasicPublish(
                        exchange: "inventory-events",
                        routingKey: routingKey,
                        basicProperties: properties,
                        body: body);

                    _logger.LogInformation($"Message published successfully to {routingKey}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to publish message to {routingKey}");
                    throw;
                }
            }, cancellationToken);
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}