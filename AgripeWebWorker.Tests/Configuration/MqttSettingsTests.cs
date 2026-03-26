using AgripeWebWorker.Configuration;

namespace AgripeWebWorker.Tests.Configuration
{
    public class MqttSettingsTests
    {
        [Fact]
        public void DefaultHost_ShouldBe_Localhost()
        {
            var settings = new MqttSettings();
            Assert.Equal("localhost", settings.Host);
        }

        [Fact]
        public void DefaultPort_ShouldBe_1883()
        {
            var settings = new MqttSettings();
            Assert.Equal(1883, settings.Port);
        }

        [Fact]
        public void DefaultTopic_ShouldBe_Reads()
        {
            var settings = new MqttSettings();
            Assert.Equal("reads", settings.Topic);
        }

        [Fact]
        public void SectionName_ShouldBe_Mqtt_And_DefaultClientId()
        {
            var settings = new MqttSettings();
            Assert.Equal("Mqtt", MqttSettings.SectionName);
            Assert.Equal("agripeweb-worker", settings.ClientId);
        }
    }
}
