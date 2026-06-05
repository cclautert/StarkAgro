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

        [Fact]
        public void DefaultUsername_ShouldBe_Null()
        {
            var settings = new MqttSettings();
            Assert.Null(settings.Username);
        }

        [Fact]
        public void DefaultPassword_ShouldBe_Null()
        {
            var settings = new MqttSettings();
            Assert.Null(settings.Password);
        }

        [Fact]
        public void Username_And_Password_ShouldAcceptValues()
        {
            var settings = new MqttSettings { Username = "iot_device", Password = "s3cr3t" };
            Assert.Equal("iot_device", settings.Username);
            Assert.Equal("s3cr3t", settings.Password);
        }

        [Fact]
        public void DefaultUseTls_ShouldBe_False()
        {
            var settings = new MqttSettings();
            Assert.False(settings.UseTls);
        }

        [Fact]
        public void DefaultAllowUntrustedCertificates_ShouldBe_False()
        {
            var settings = new MqttSettings();
            Assert.False(settings.AllowUntrustedCertificates);
        }

        [Fact]
        public void UseTls_ShouldAcceptTrue()
        {
            var settings = new MqttSettings { UseTls = true, Port = 8883 };
            Assert.True(settings.UseTls);
            Assert.Equal(8883, settings.Port);
        }

        [Fact]
        public void AllowUntrustedCertificates_IsDevOnlyFlag_ShouldDefaultFalse()
        {
            var settings = new MqttSettings { UseTls = true };
            Assert.False(settings.AllowUntrustedCertificates);
        }
    }
}
