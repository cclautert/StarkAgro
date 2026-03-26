using System.Net;
using System.Security.Claims;
using AgripeWebAPI.Controllers;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using AgripeWebAPI.Tests.Mocks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Moq;
using Xunit;

namespace AgripeWebAPI.Tests.Controllers
{
    public class MainControllerTests
    {
        private class TestableMainController : MainController
        {
            public TestableMainController(INotifier notificador) : base(notificador) { }

            public ActionResult TestCustomResponse(object result = null, HttpStatusCode statusCode = HttpStatusCode.OK)
                => CustomResponse(result, statusCode);

            public ActionResult TestCustomResponseModelState(ModelStateDictionary modelState)
                => CustomResponse(modelState);

            public int TestGetCurrentUserId() => GetCurrentUserId();

            public void TestNotifyError(string msg) => NotifyError(msg);

            public bool TestValidOperation() => ValidOperation();
        }

        private static TestableMainController CreateController(INotifier notifier, string userId = null)
        {
            var controller = new TestableMainController(notifier);

            var claims = userId != null
                ? new[] { new Claim("id", userId) }
                : Array.Empty<Claim>();

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims))
                }
            };

            return controller;
        }

        [Fact]
        public void CustomResponse_NoErrors_ReturnsOkWithResult()
        {
            // Arrange
            var notifier = new MockNotifier();
            var controller = CreateController(notifier);
            var data = new { Name = "Test" };

            // Act
            var result = controller.TestCustomResponse(data);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(200, objectResult.StatusCode);
            Assert.Equal(data, objectResult.Value);
        }

        [Fact]
        public void CustomResponse_NoErrors_NullResult_ReturnsStatusCode()
        {
            // Arrange
            var notifier = new MockNotifier();
            var controller = CreateController(notifier);

            // Act
            var result = controller.TestCustomResponse(null);

            // Assert
            var statusCodeResult = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(200, statusCodeResult.StatusCode);
        }

        [Fact]
        public void CustomResponse_WithErrors_ReturnsBadRequest()
        {
            // Arrange
            var notifier = new MockNotifier();
            var controller = CreateController(notifier);
            controller.TestNotifyError("Something went wrong");

            // Act
            var result = controller.TestCustomResponse(new { Name = "Test" });

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
            // Verify the error messages are serialized in the response
            var json = System.Text.Json.JsonSerializer.Serialize(badRequest.Value);
            Assert.Contains("Something went wrong", json);
        }

        [Fact]
        public void CustomResponse_ModelState_Invalid_ReturnsBadRequest()
        {
            // Arrange
            var notifier = new MockNotifier();
            var controller = CreateController(notifier);
            var modelState = new ModelStateDictionary();
            modelState.AddModelError("Field", "Field is required");

            // Act
            var result = controller.TestCustomResponseModelState(modelState);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public void GetCurrentUserId_WithClaim_ReturnsId()
        {
            // Arrange
            var notifier = new MockNotifier();
            var controller = CreateController(notifier, "42");

            // Act
            var userId = controller.TestGetCurrentUserId();

            // Assert
            Assert.Equal(42, userId);
        }

        [Fact]
        public void GetCurrentUserId_NoClaim_ReturnsZero()
        {
            // Arrange
            var notifier = new MockNotifier();
            var controller = CreateController(notifier);

            // Act
            var userId = controller.TestGetCurrentUserId();

            // Assert
            Assert.Equal(0, userId);
        }

        [Fact]
        public void NotifyError_AddsNotification()
        {
            // Arrange
            var notifier = new MockNotifier();
            var controller = CreateController(notifier);

            // Act
            controller.TestNotifyError("Error message");

            // Assert
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public void ValidOperation_NoNotifications_ReturnsTrue()
        {
            // Arrange
            var notifier = new MockNotifier();
            var controller = CreateController(notifier);

            // Act
            var result = controller.TestValidOperation();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidOperation_WithNotifications_ReturnsFalse()
        {
            // Arrange
            var notifier = new MockNotifier();
            var controller = CreateController(notifier);
            controller.TestNotifyError("Error");

            // Act
            var result = controller.TestValidOperation();

            // Assert
            Assert.False(result);
        }
    }
}
