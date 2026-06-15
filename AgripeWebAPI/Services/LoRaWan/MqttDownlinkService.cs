using System.Net.Security;
using System.Text.Json;
using AgripeWebAPI.Configuration;
using AgripeWebAPI.Models.Interfaces;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace AgripeWebAPI.Services.LoRaWan
{
    public class MqttDownlinkService : ILoRaWanDownlinkService, IAsyncDisposable
    {
        private readonly MqttDownlinkSettings _settings;
        private readonly ILogger<MqttDownlinkService> _logger;
        private readonly IMqttClient _client;
        private readonly SemaphoreSlim _connectLock = new(1, 1);

        public MqttDownlinkService(
            IOptions<MqttDownlinkSettings> settings,
            ILogger<MqttDownlinkService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
            _client = new MqttFactory().CreateMqttClient();
        }

        public async Task<bool> SendUplinkIntervalAsync(string devEui, int intervalSeconds, CancellationToken cancellationToken)
        {
            var cleanDevEui = StripSuffix(devEui);
            var data = Convert.ToBase64String(BuildPayload(intervalSeconds));

            var message = JsonSerializer.Serialize(new
            {
                devEUI = cleanDevEui,
                fPort = 1,
                confirmed = false,
                data
            });

            try
            {
                await EnsureConnectedAsync(cancellationToken);

                var mqttMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(_settings.Topic)
                    .WithPayload(message)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                    .Build();

                await _client.PublishAsync(mqttMessage, cancellationToken);
                _logger.LogInformation("Downlink published for DevEUI {DevEui} (interval={Interval}s)", cleanDevEui, intervalSeconds);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQTT downlink failed for DevEUI {DevEui}", cleanDevEui);
                return false;
            }
        }

        private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
        {
            if (_client.IsConnected) return;

            await _connectLock.WaitAsync(cancellationToken);
            try
            {
                if (_client.IsConnected) return;

                var optionsBuilder = new MqttClientOptionsBuilder()
                    .WithTcpServer(_settings.Host, _settings.Port)
                    .WithClientId(_settings.ClientId)
                    .WithCleanSession();

                if (!string.IsNullOrEmpty(_settings.Username))
                    optionsBuilder = optionsBuilder.WithCredentials(_settings.Username, _settings.Password);

                if (_settings.UseTls)
                {
                    var allowUntrusted = _settings.AllowUntrustedCertificates;
                    if (allowUntrusted)
                        _logger.LogWarning("MQTT TLS: AllowUntrustedCertificates=true — do not use in production");

                    optionsBuilder = optionsBuilder.WithTlsOptions(o =>
                        o.WithCertificateValidationHandler(ctx =>
                            allowUntrusted || ctx.SslPolicyErrors == SslPolicyErrors.None));
                }

                var options = optionsBuilder.Build();

                _logger.LogInformation("Connecting to MQTT broker at {Host}:{Port} (TLS={UseTls})...",
                    _settings.Host, _settings.Port, _settings.UseTls);

                await _client.ConnectAsync(options, cancellationToken);
                _logger.LogInformation("Connected to MQTT broker");
            }
            finally
            {
                _connectLock.Release();
            }
        }

        private static byte[] BuildPayload(int intervalSeconds) => new[]
        {
            (byte)0x01,
            (byte)((intervalSeconds >> 16) & 0xFF),
            (byte)((intervalSeconds >> 8)  & 0xFF),
            (byte)( intervalSeconds        & 0xFF)
        };

        private static string StripSuffix(string code)
            => code.Contains('_') ? code[..code.IndexOf('_')] : code;

        public async ValueTask DisposeAsync()
        {
            if (_client.IsConnected)
                await _client.DisconnectAsync();
            _client.Dispose();
            _connectLock.Dispose();
        }
    }
}
