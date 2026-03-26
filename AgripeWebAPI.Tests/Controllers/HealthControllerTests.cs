using AgripeWebAPI.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AgripeWebAPI.Tests.Controllers
{
    public class HealthControllerTests
    {
        [Fact]
        public void Get_ReturnsOk()
        {
            // Arrange
            var controller = new HealthController();

            // Act
            var result = controller.Get();

            // Assert
            Assert.IsType<OkResult>(result);
        }
    }
}
