using MQTTnet;
using MQTTnet.Client;

namespace StarkAgroWorker.Services
{
    public class MqttClientWrapper : IMqttClientWrapper
    {
        private readonly IMqttClient _client;

        public MqttClientWrapper()
        {
            _client = new MqttFactory().CreateMqttClient();
        }

        public bool IsConnected => _client.IsConnected;

        public event Func<MqttApplicationMessageReceivedEventArgs, Task> ApplicationMessageReceivedAsync
        {
            add => _client.ApplicationMessageReceivedAsync += value;
            remove => _client.ApplicationMessageReceivedAsync -= value;
        }

        public event Func<MqttClientDisconnectedEventArgs, Task> DisconnectedAsync
        {
            add => _client.DisconnectedAsync += value;
            remove => _client.DisconnectedAsync -= value;
        }

        public Task<MqttClientConnectResult> ConnectAsync(MqttClientOptions options, CancellationToken cancellationToken)
            => _client.ConnectAsync(options, cancellationToken);

        public Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken)
            => _client.SubscribeAsync(options, cancellationToken);

        public Task DisconnectAsync(MqttClientDisconnectOptions options, CancellationToken cancellationToken)
            => _client.DisconnectAsync(options, cancellationToken);

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}
