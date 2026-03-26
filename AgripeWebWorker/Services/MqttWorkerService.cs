using System.Text;
using System.Text.Json;
using AgripeWebWorker.Configuration;
using AgripeWebAPI.Domain.Commands.Requests.Reads;
using MediatR;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace AgripeWebWorker.Services
{
    public class MqttWorkerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly MqttSettings _mqttSettings;
        private readonly ILogger<MqttWorkerService> _logger;
        private readonly IMqttClientWrapper _mqttClient;

        public MqttWorkerService(
            IServiceProvider serviceProvider,
            IOptions<MqttSettings> mqttSettings,
            ILogger<MqttWorkerService> logger,
            IMqttClientWrapper mqttClient)
        {
            _serviceProvider = serviceProvider;
            _mqttSettings = mqttSettings.Value;
            _logger = logger;
            _mqttClient = mqttClient;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _mqttClient.ApplicationMessageReceivedAsync += async e =>
            {
                try
                {
                    var segment = e.ApplicationMessage.PayloadSegment;
                    var payload = segment.Count > 0
                        ? Encoding.UTF8.GetString(segment.Array!, segment.Offset, segment.Count)
                        : string.Empty;

                    _logger.LogInformation("MQTT message received on topic '{Topic}': {Payload}",
                        e.ApplicationMessage.Topic, payload);

                    if (string.IsNullOrWhiteSpace(payload))
                    {
                        _logger.LogWarning("Empty MQTT payload received");
                        return;
                    }

                    var message = JsonSerializer.Deserialize<MqttReadMessage>(payload, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (message == null || string.IsNullOrEmpty(message.Code))
                    {
                        _logger.LogWarning("Invalid MQTT message: missing 'code' field");
                        return;
                    }

                    using var scope = _serviceProvider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                    var request = new CreateReadRequest
                    {
                        Code = message.Code,
                        Value = message.Value
                    };

                    await mediator.Send(request, stoppingToken);
                    _logger.LogInformation("Successfully processed read for sensor '{Code}'", message.Code);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing MQTT message");
                }
            };

            _mqttClient.DisconnectedAsync += async e =>
            {
                if (stoppingToken.IsCancellationRequested) return;

                _logger.LogWarning("MQTT client disconnected. Reconnecting in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

                try
                {
                    await ConnectAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reconnect to MQTT broker");
                }
            };

            await ConnectAsync(stoppingToken);

            // Keep the service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task ConnectAsync(CancellationToken cancellationToken)
        {
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(_mqttSettings.Host, _mqttSettings.Port)
                .WithClientId(_mqttSettings.ClientId)
                .WithCleanSession()
                .Build();

            _logger.LogInformation("Connecting to MQTT broker at {Host}:{Port}...", _mqttSettings.Host, _mqttSettings.Port);

            await _mqttClient.ConnectAsync(options, cancellationToken);

            var subscribeOptions = new MqttFactory().CreateSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic(_mqttSettings.Topic).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
                .Build();

            await _mqttClient.SubscribeAsync(subscribeOptions, cancellationToken);

            _logger.LogInformation("Connected and subscribed to topic '{Topic}'", _mqttSettings.Topic);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), cancellationToken);
                _logger.LogInformation("MQTT client disconnected gracefully");
            }

            _mqttClient.Dispose();
            await base.StopAsync(cancellationToken);
        }

        private sealed class MqttReadMessage
        {
            public string Code { get; set; } = string.Empty;
            public decimal Value { get; set; }
        }
    }
}
