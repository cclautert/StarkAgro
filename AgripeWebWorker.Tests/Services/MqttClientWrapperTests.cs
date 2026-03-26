using AgripeWebWorker.Services;

namespace AgripeWebWorker.Tests.Services
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
    }
}
