using StarkAgroWorker.Services;
using MQTTnet.Client;

namespace StarkAgroWorker.Tests.Services
{
    public class MqttClientWrapperTests
    {
        [Fact]
        public void Constructor_ShouldCreateNonNullClient()
        {
            using var wrapper = new MqttClientWrapper();
            Assert.NotNull(wrapper);
        }

        [Fact]
        public void IsConnected_WhenNew_ShouldBeFalse()
        {
            using var wrapper = new MqttClientWrapper();
            Assert.False(wrapper.IsConnected);
        }

        [Fact]
        public void Dispose_ShouldNotThrow()
        {
            var wrapper = new MqttClientWrapper();
            var ex = Record.Exception(() => wrapper.Dispose());
            Assert.Null(ex);
        }

        [Fact]
        public void ApplicationMessageReceivedAsync_AddRemoveHandler_ShouldNotThrow()
        {
            using var wrapper = new MqttClientWrapper();
            Func<MqttApplicationMessageReceivedEventArgs, Task> handler = _ => Task.CompletedTask;

            var ex = Record.Exception(() =>
            {
                wrapper.ApplicationMessageReceivedAsync += handler;
                wrapper.ApplicationMessageReceivedAsync -= handler;
            });

            Assert.Null(ex);
        }

        [Fact]
        public void DisconnectedAsync_AddRemoveHandler_ShouldNotThrow()
        {
            using var wrapper = new MqttClientWrapper();
            Func<MqttClientDisconnectedEventArgs, Task> handler = _ => Task.CompletedTask;

            var ex = Record.Exception(() =>
            {
                wrapper.DisconnectedAsync += handler;
                wrapper.DisconnectedAsync -= handler;
            });

            Assert.Null(ex);
        }
    }
}
