using MQTTnet.Client;

namespace AgripeWebWorker.Services
{
    public interface IMqttClientWrapper : IDisposable
    {
        Task<MqttClientConnectResult> ConnectAsync(MqttClientOptions options, CancellationToken cancellationToken);
        Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken);
        Task DisconnectAsync(MqttClientDisconnectOptions options, CancellationToken cancellationToken);
        bool IsConnected { get; }

        event Func<MqttApplicationMessageReceivedEventArgs, Task> ApplicationMessageReceivedAsync;
        event Func<MqttClientDisconnectedEventArgs, Task> DisconnectedAsync;
    }
}
