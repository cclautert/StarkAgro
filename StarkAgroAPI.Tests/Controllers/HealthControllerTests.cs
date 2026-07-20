using StarkAgroAPI.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace StarkAgroAPI.Tests.Controllers
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
