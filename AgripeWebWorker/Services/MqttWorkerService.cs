using System.Net.Security;
using System.Text;
using System.Text.Json;
using AgripeWebWorker.Configuration;
using AgripeWebAPI.Domain.Commands.Requests.Anomalies;
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

                    var createRequest = new CreateReadRequest
                    {
                        Code = message.Code,
                        Value = message.Value,
                        IsEdgeAnomaly = message.IsEdgeAnomaly,
                        EdgeStats = message.EdgeStats != null ? new AgripeWebAPI.Domain.Commands.Requests.Reads.EdgeStats
                        {
                            Mean = message.EdgeStats.Mean,
                            StdDev = message.EdgeStats.StdDev,
                            WindowSize = message.EdgeStats.WindowSize
                        } : null
                    };

                    var createResponse = await mediator.Send(createRequest, stoppingToken);
                    _logger.LogInformation("Successfully processed read for sensor '{Code}'", message.Code);

                    if (createResponse.Id > 0)
                    {
                        await mediator.Send(new DetectSensorAnomalyRequest
                        {
                            ReadSensorId = createResponse.Id,
                            SensorId = createResponse.SensorId,
                            UserId = createResponse.UserId,
                            Value = message.Value
                        }, stoppingToken);
                    }
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

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ConnectAsync(stoppingToken);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to connect to MQTT broker. Retrying in 5 seconds...");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            // Keep the service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task ConnectAsync(CancellationToken cancellationToken)
        {
            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(_mqttSettings.Host, _mqttSettings.Port)
                .WithClientId(_mqttSettings.ClientId)
                .WithCleanSession();

            if (!string.IsNullOrEmpty(_mqttSettings.Username))
                optionsBuilder = optionsBuilder.WithCredentials(_mqttSettings.Username, _mqttSettings.Password);

            if (_mqttSettings.UseTls)
            {
                var allowUntrusted = _mqttSettings.AllowUntrustedCertificates;
                if (allowUntrusted)
                    _logger.LogWarning("MQTT TLS: AllowUntrustedCertificates=true — do not use in production");

                optionsBuilder = optionsBuilder.WithTlsOptions(o =>
                    o.WithCertificateValidationHandler(ctx =>
                        allowUntrusted || ctx.SslPolicyErrors == SslPolicyErrors.None));
            }

            var options = optionsBuilder.Build();

            _logger.LogInformation("Connecting to MQTT broker at {Host}:{Port} (TLS={UseTls})...",
                _mqttSettings.Host, _mqttSettings.Port, _mqttSettings.UseTls);

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
            public bool IsEdgeAnomaly { get; set; }
            public MqttEdgeStats? EdgeStats { get; set; }
        }

        private sealed class MqttEdgeStats
        {
            public decimal? Mean { get; set; }
            public decimal? StdDev { get; set; }
            public int? WindowSize { get; set; }
        }
    }
}
