using System.Text;
using System.Text.Json;
using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebWorker.Configuration;
using AgripeWebWorker.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MQTTnet;
using MQTTnet.Client;

namespace AgripeWebWorker.Tests.Services
{
    public class MqttWorkerServiceTests : IDisposable
    {
        private readonly Mock<IMqttClientWrapper> _mockMqttClient;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<ILogger<MqttWorkerService>> _mockLogger;
        private readonly MqttSettings _mqttSettings;
        private readonly MqttWorkerService _service;
        private readonly CancellationTokenSource _cts;

        private Func<MqttApplicationMessageReceivedEventArgs, Task>? _capturedMessageHandler;
        private Func<MqttClientDisconnectedEventArgs, Task>? _capturedDisconnectedHandler;

        public MqttWorkerServiceTests()
        {
            _mockMqttClient = new Mock<IMqttClientWrapper>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockLogger = new Mock<ILogger<MqttWorkerService>>();
            _mqttSettings = new MqttSettings
            {
                Host = "test-broker",
                Port = 1884,
                Topic = "test/reads",
                ClientId = "test-client"
            };
            _cts = new CancellationTokenSource();

            // Capture event handlers when they're registered
            _mockMqttClient
                .SetupAdd(c => c.ApplicationMessageReceivedAsync += It.IsAny<Func<MqttApplicationMessageReceivedEventArgs, Task>>())
                .Callback<Func<MqttApplicationMessageReceivedEventArgs, Task>>(handler => _capturedMessageHandler = handler);

            _mockMqttClient
                .SetupAdd(c => c.DisconnectedAsync += It.IsAny<Func<MqttClientDisconnectedEventArgs, Task>>())
                .Callback<Func<MqttClientDisconnectedEventArgs, Task>>(handler => _capturedDisconnectedHandler = handler);

            // ConnectAsync should complete immediately
            _mockMqttClient
                .Setup(c => c.ConnectAsync(It.IsAny<MqttClientOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MqttClientConnectResult());

            // SubscribeAsync should complete immediately - result is not inspected by the service
            _mockMqttClient
                .Setup(c => c.SubscribeAsync(It.IsAny<MqttClientSubscribeOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MqttClientSubscribeResult)null!);

            var options = Options.Create(_mqttSettings);
            _service = new MqttWorkerService(_mockServiceProvider.Object, options, _mockLogger.Object, _mockMqttClient.Object);
        }

        public void Dispose()
        {
            _cts.Dispose();
            _service.Dispose();
        }

        private async Task StartServiceAndCapture()
        {
            _cts.CancelAfter(TimeSpan.FromSeconds(2));
            try
            {
                await _service.StartAsync(_cts.Token);
                // Give ExecuteAsync a moment to wire up handlers
                await Task.Delay(500);
            }
            catch (OperationCanceledException) { }
        }

        private static MqttApplicationMessageReceivedEventArgs CreateMessageEventArgs(byte[] payloadBytes)
        {
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic("test/reads")
                .WithPayload(payloadBytes)
                .Build();

            // MqttApplicationMessageReceivedEventArgs requires a non-null MqttPublishPacket.
            // We use the MQTTnet Packets type to construct one.
            var publishPacket = new MQTTnet.Packets.MqttPublishPacket
            {
                Topic = "test/reads",
                PayloadSegment = new ArraySegment<byte>(payloadBytes)
            };

            return new MqttApplicationMessageReceivedEventArgs("test-client", msg, publishPacket, null);
        }

        private static MqttClientDisconnectedEventArgs CreateDisconnectedEventArgs()
        {
            return new MqttClientDisconnectedEventArgs(
                clientWasConnected: true,
                connectResult: null,
                reason: MqttClientDisconnectReason.NormalDisconnection,
                reasonString: null,
                userProperties: null,
                exception: null);
        }

        // --- Lifecycle tests ---

        [Fact]
        public async Task ExecuteAsync_ShouldConnectWithCredentials_WhenUsernameConfigured()
        {
            var settingsWithCreds = new MqttSettings
            {
                Host = "test-broker",
                Port = 1884,
                Topic = "test/reads",
                ClientId = "test-client",
                Username = "iot_device",
                Password = "s3cr3t"
            };

            var options = Options.Create(settingsWithCreds);
            var service = new MqttWorkerService(_mockServiceProvider.Object, options, _mockLogger.Object, _mockMqttClient.Object);

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            try { await service.StartAsync(cts.Token); await Task.Delay(500); }
            catch (OperationCanceledException) { }

            _mockMqttClient.Verify(c => c.ConnectAsync(
                It.Is<MqttClientOptions>(o => o.Credentials != null && o.Credentials.GetUserName(o) == "iot_device"),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);

            cts.Dispose();
            service.Dispose();
        }

        [Fact]
        public async Task ExecuteAsync_ShouldConnectWithoutCredentials_WhenUsernameNotConfigured()
        {
            await StartServiceAndCapture();

            _mockMqttClient.Verify(c => c.ConnectAsync(
                It.Is<MqttClientOptions>(o => o.Credentials == null),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldConnectToBroker_WithCorrectSettings()
        {
            await StartServiceAndCapture();

            _mockMqttClient.Verify(c => c.ConnectAsync(
                It.Is<MqttClientOptions>(o => o.ChannelOptions is MqttClientTcpOptions),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldSubscribeToTopic()
        {
            await StartServiceAndCapture();

            _mockMqttClient.Verify(c => c.SubscribeAsync(
                It.IsAny<MqttClientSubscribeOptions>(),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldRegisterMessageHandler()
        {
            await StartServiceAndCapture();

            Assert.NotNull(_capturedMessageHandler);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldRegisterDisconnectedHandler()
        {
            await StartServiceAndCapture();

            Assert.NotNull(_capturedDisconnectedHandler);
        }

        // --- Message processing tests ---

        [Fact]
        public async Task MessageReceived_ValidPayload_ShouldSendCreateDeviceReadRequest()
        {
            var mockMediator = new Mock<IMediator>();
            var mockScope = new Mock<IServiceScope>();
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockScopedProvider = new Mock<IServiceProvider>();

            mockMediator
                .Setup(m => m.Send(It.IsAny<CreateDeviceReadRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgripeWebAPI.Domain.Commands.Responses.Reads.CreateReadResponse { Id = 1, SensorId = 1, UserId = 10 });

            mockScopedProvider.Setup(sp => sp.GetService(typeof(IMediator))).Returns(mockMediator.Object);
            mockScope.Setup(s => s.ServiceProvider).Returns(mockScopedProvider.Object);
            mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IServiceScopeFactory))).Returns(mockScopeFactory.Object);

            await StartServiceAndCapture();
            Assert.NotNull(_capturedMessageHandler);

            var payload = JsonSerializer.Serialize(new { code = "SENS01", value = 512 });
            var payloadBytes = Encoding.UTF8.GetBytes(payload);

            await _capturedMessageHandler!(CreateMessageEventArgs(payloadBytes));

            mockMediator.Verify(m => m.Send(
                It.Is<CreateDeviceReadRequest>(r => r.Code == "SENS01" && r.Value == 512),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task MessageReceived_ValidPayload_PropagatesReadAt_WhenTimePresent()
        {
            var mockMediator = new Mock<IMediator>();
            var mockScope = new Mock<IServiceScope>();
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockScopedProvider = new Mock<IServiceProvider>();

            mockMediator
                .Setup(m => m.Send(It.IsAny<CreateDeviceReadRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgripeWebAPI.Domain.Commands.Responses.Reads.CreateReadResponse { Id = 1, SensorId = 1, UserId = 10 });

            mockScopedProvider.Setup(sp => sp.GetService(typeof(IMediator))).Returns(mockMediator.Object);
            mockScope.Setup(s => s.ServiceProvider).Returns(mockScopedProvider.Object);
            mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IServiceScopeFactory))).Returns(mockScopeFactory.Object);

            await StartServiceAndCapture();
            Assert.NotNull(_capturedMessageHandler);

            var payload = """{"code":"SENS01","value":22.7,"time":"2026-06-11T23:29:02Z"}""";
            var payloadBytes = Encoding.UTF8.GetBytes(payload);

            await _capturedMessageHandler!(CreateMessageEventArgs(payloadBytes));

            var expectedTime = new DateTime(2026, 6, 11, 23, 29, 2, DateTimeKind.Utc);
            mockMediator.Verify(m => m.Send(
                It.Is<CreateDeviceReadRequest>(r => r.ReadAt != null && r.ReadAt.Value.ToUniversalTime() == expectedTime),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task MessageReceived_UnregisteredSensor_LogsWarning_SkipsAnomalyDispatch()
        {
            var mockMediator = new Mock<IMediator>();
            var mockScope = new Mock<IServiceScope>();
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockScopedProvider = new Mock<IServiceProvider>();

            mockMediator
                .Setup(m => m.Send(It.IsAny<CreateDeviceReadRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AgripeWebAPI.Domain.Commands.Responses.Reads.CreateReadResponse?)null);

            mockScopedProvider.Setup(sp => sp.GetService(typeof(IMediator))).Returns(mockMediator.Object);
            mockScope.Setup(s => s.ServiceProvider).Returns(mockScopedProvider.Object);
            mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IServiceScopeFactory))).Returns(mockScopeFactory.Object);

            await StartServiceAndCapture();
            Assert.NotNull(_capturedMessageHandler);

            var payload = JsonSerializer.Serialize(new { code = "NOTFOUND_H", value = 50 });
            var payloadBytes = Encoding.UTF8.GetBytes(payload);

            await _capturedMessageHandler!(CreateMessageEventArgs(payloadBytes));

            mockMediator.Verify(m => m.Send(
                It.IsAny<AgripeWebAPI.Domain.Commands.Requests.Anomalies.DetectSensorAnomalyRequest>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task MessageReceived_EmptyCode_ShouldLogWarning_NotSendRequest()
        {
            var mockMediator = new Mock<IMediator>();
            var mockScope = new Mock<IServiceScope>();
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockScopedProvider = new Mock<IServiceProvider>();

            mockScopedProvider.Setup(sp => sp.GetService(typeof(IMediator))).Returns(mockMediator.Object);
            mockScope.Setup(s => s.ServiceProvider).Returns(mockScopedProvider.Object);
            mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IServiceScopeFactory))).Returns(mockScopeFactory.Object);

            await StartServiceAndCapture();
            Assert.NotNull(_capturedMessageHandler);

            var payload = JsonSerializer.Serialize(new { code = "", value = 100 });
            var payloadBytes = Encoding.UTF8.GetBytes(payload);

            await _capturedMessageHandler!(CreateMessageEventArgs(payloadBytes));

            mockMediator.Verify(m => m.Send(It.IsAny<CreateDeviceReadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task MessageReceived_InvalidJson_ShouldLogError()
        {
            await StartServiceAndCapture();
            Assert.NotNull(_capturedMessageHandler);

            var payloadBytes = Encoding.UTF8.GetBytes("not-valid-json{{{");

            // Should not throw
            await _capturedMessageHandler!(CreateMessageEventArgs(payloadBytes));

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task MessageReceived_EmptyPayload_ShouldLogWarning()
        {
            await StartServiceAndCapture();
            Assert.NotNull(_capturedMessageHandler);

            await _capturedMessageHandler!(CreateMessageEventArgs(Array.Empty<byte>()));

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        // --- Disconnect/reconnect tests ---

        [Fact]
        public async Task Disconnected_ShouldReconnect_WhenNotCancelled()
        {
            var nonCancelledCts = new CancellationTokenSource();

            var options = Options.Create(_mqttSettings);
            var service = new MqttWorkerService(_mockServiceProvider.Object, options, _mockLogger.Object, _mockMqttClient.Object);

            var connectCount = 0;
            _mockMqttClient
                .Setup(c => c.ConnectAsync(It.IsAny<MqttClientOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    connectCount++;
                    if (connectCount >= 2) nonCancelledCts.Cancel();
                    return new MqttClientConnectResult();
                });

            try
            {
                await service.StartAsync(nonCancelledCts.Token);
                await Task.Delay(500);

                Assert.NotNull(_capturedDisconnectedHandler);
                try
                {
                    await _capturedDisconnectedHandler!(CreateDisconnectedEventArgs());
                }
                catch (OperationCanceledException) { }
            }
            catch (OperationCanceledException) { }

            Assert.True(connectCount >= 2, $"Expected at least 2 connect calls, got {connectCount}");
            nonCancelledCts.Dispose();
            service.Dispose();
        }

        [Fact]
        public async Task Disconnected_ShouldNotReconnect_WhenCancelled()
        {
            _cts.Cancel();

            await StartServiceAndCapture();

            if (_capturedDisconnectedHandler != null)
            {
                await _capturedDisconnectedHandler(CreateDisconnectedEventArgs());
            }

            _mockMqttClient.Verify(c => c.ConnectAsync(
                It.IsAny<MqttClientOptions>(),
                It.IsAny<CancellationToken>()), Times.AtMostOnce);
        }

        // --- StopAsync tests ---

        [Fact]
        public async Task StopAsync_WhenConnected_ShouldDisconnect()
        {
            _mockMqttClient.Setup(c => c.IsConnected).Returns(true);
            _mockMqttClient.Setup(c => c.DisconnectAsync(It.IsAny<MqttClientDisconnectOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _service.StartAsync(CancellationToken.None);
            await Task.Delay(50);
            await _service.StopAsync(CancellationToken.None);

            _mockMqttClient.Verify(c => c.DisconnectAsync(It.IsAny<MqttClientDisconnectOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockMqttClient.Verify(c => c.Dispose(), Times.Once);
        }

        [Fact]
        public async Task StopAsync_WhenNotConnected_ShouldOnlyDispose()
        {
            _mockMqttClient.Setup(c => c.IsConnected).Returns(false);

            await _service.StartAsync(CancellationToken.None);
            await Task.Delay(50);
            await _service.StopAsync(CancellationToken.None);

            _mockMqttClient.Verify(c => c.DisconnectAsync(It.IsAny<MqttClientDisconnectOptions>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockMqttClient.Verify(c => c.Dispose(), Times.Once);
        }

        [Fact]
        public async Task StopAsync_ShouldNotThrow()
        {
            _mockMqttClient.Setup(c => c.IsConnected).Returns(false);

            var exception = await Record.ExceptionAsync(() => _service.StopAsync(CancellationToken.None));
            Assert.Null(exception);
        }

        // --- TLS tests ---

        [Fact]
        public async Task ExecuteAsync_ShouldConnectWithTls_WhenUseTlsEnabled()
        {
            var settingsWithTls = new MqttSettings
            {
                Host = "test-broker",
                Port = 8883,
                Topic = "test/reads",
                ClientId = "test-client",
                UseTls = true
            };

            var options = Options.Create(settingsWithTls);
            var service = new MqttWorkerService(_mockServiceProvider.Object, options, _mockLogger.Object, _mockMqttClient.Object);

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            try { await service.StartAsync(cts.Token); await Task.Delay(500); }
            catch (OperationCanceledException) { }

            _mockMqttClient.Verify(c => c.ConnectAsync(
                It.Is<MqttClientOptions>(o => (o.ChannelOptions as MqttClientTcpOptions)!.TlsOptions.UseTls),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);

            cts.Dispose();
            service.Dispose();
        }

        [Fact]
        public async Task ExecuteAsync_ShouldNotUseTls_WhenUseTlsDisabled()
        {
            await StartServiceAndCapture();

            _mockMqttClient.Verify(c => c.ConnectAsync(
                It.Is<MqttClientOptions>(o => !(o.ChannelOptions as MqttClientTcpOptions)!.TlsOptions.UseTls),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldConnectWithTlsAndCredentials_WhenBothConfigured()
        {
            var settingsFull = new MqttSettings
            {
                Host = "test-broker",
                Port = 8883,
                Topic = "test/reads",
                ClientId = "test-client",
                Username = "iot_device",
                Password = "s3cr3t",
                UseTls = true
            };

            var options = Options.Create(settingsFull);
            var service = new MqttWorkerService(_mockServiceProvider.Object, options, _mockLogger.Object, _mockMqttClient.Object);

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            try { await service.StartAsync(cts.Token); await Task.Delay(500); }
            catch (OperationCanceledException) { }

            _mockMqttClient.Verify(c => c.ConnectAsync(
                It.Is<MqttClientOptions>(o =>
                    (o.ChannelOptions as MqttClientTcpOptions)!.TlsOptions.UseTls &&
                    o.Credentials != null && o.Credentials.GetUserName(o) == "iot_device"),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);

            cts.Dispose();
            service.Dispose();
        }
    }
}
