using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AgripeWebAPI.Controllers;
using AgripeWebAPI.Domain.Commands.Requests.Anomalies;
using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Anomalies;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using AgripeWebAPI.Tests.Mocks;
using Moq;
using Xunit;

namespace AgripeWebAPI.Tests.Controllers
{
    public class PivotControllerTests
    {
        [Fact]
        public async Task GetAll_Sets_UserId_And_Returns_CustomResponse()
        {
            // Arrange
            var notifier = new Mock<INotifier>();
            var mediator = new Mock<IMediator>();
            var controller = new PivotController(notifier.Object);

            controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("id", "7") }))
                }
            };

            var command = new GetListPivotByUserIdRequest();

            var responses = new List<GetPivotResponse>
            {
                new GetPivotResponse { Id = 3 }
            };

            mediator.Setup(m => m.Send(It.Is<GetListPivotByUserIdRequest>(c => c.UserId == 7), It.IsAny<CancellationToken>()))
                .ReturnsAsync(responses);

            // Act
            var actionResult = await controller.GetAll(mediator.Object, command, CancellationToken.None);

            // Assert
            mediator.Verify(m => m.Send(It.Is<GetListPivotByUserIdRequest>(c => c.UserId == 7), It.IsAny<CancellationToken>()), Times.Once);

            var objectResult = Assert.IsType<ObjectResult>(actionResult);
            Assert.Equal(200, objectResult.StatusCode);

            var returned = objectResult.Value as List<GetPivotResponse>;
            Assert.NotNull(returned);

            Assert.Single(returned);
            Assert.Equal(3, returned[0].Id);
        }

        [Fact]
        public async Task Add_Sets_UserId_And_Returns_CreateResponse()
        {
            // Arrange
            var notifier = new Mock<INotifier>();
            var mediator = new Mock<IMediator>();
            var controller = new PivotController(notifier.Object);

            controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("id", "2") }))
                }
            };

            var command = new CreatePivotRequest { Name = "Pivot A" };
            var expected = new CreatePivotResponse { Id = 99 };

            mediator.Setup(m => m.Send(It.Is<CreatePivotRequest>(c => c.UserId == 2 && c.Name == "Pivot A"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            // Act
            var actionResult = await controller.Add(mediator.Object, command, CancellationToken.None);

            // Assert
            mediator.Verify(m => m.Send(It.Is<CreatePivotRequest>(c => c.UserId == 2 && c.Name == "Pivot A"), It.IsAny<CancellationToken>()), Times.Once);
            var objectResult = Assert.IsType<ObjectResult>(actionResult);
            Assert.Equal(200, objectResult.StatusCode);
            var result = Assert.IsType<CreatePivotResponse>(objectResult.Value);
            Assert.Equal(99, result.Id);
        }
        private PivotController CreateControllerWithClaim(INotifier notifier, string userId = "7")
        {
            var controller = new PivotController(notifier);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("id", userId) }))
                }
            };
            return controller;
        }

        [Fact]
        public async Task GetById_Valid_ReturnsResponse()
        {
            var notifier = new Mock<INotifier>();
            var mediator = new Mock<IMediator>();
            var controller = CreateControllerWithClaim(notifier.Object);

            var expected = new GetPivotResponse { Id = 3, Name = "Pivot 3" };
            mediator.Setup(m => m.Send(It.IsAny<GetPivotRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var result = await controller.GetById(mediator.Object, new GetPivotRequest(), CancellationToken.None);

            var response = Assert.IsType<GetPivotResponse>(result.Value);
            Assert.Equal(3, response.Id);
        }

        [Fact]
        public async Task GetById_InvalidModelState_ReturnsBadRequest()
        {
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateControllerWithClaim(notifier);
            controller.ModelState.AddModelError("Id", "Required");

            var result = await controller.GetById(mediator.Object, new GetPivotRequest(), CancellationToken.None);

            var badRequest = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(400, badRequest.StatusCode);
        }

        [Fact]
        public async Task Update_Valid_ReturnsResponse()
        {
            var notifier = new Mock<INotifier>();
            var mediator = new Mock<IMediator>();
            var controller = CreateControllerWithClaim(notifier.Object);

            var expected = new EditPivotResponse { Id = 3 };
            mediator.Setup(m => m.Send(It.IsAny<EditPivotRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var result = await controller.Update(mediator.Object, new EditPivotRequest { Id = 3, Name = "Updated" }, CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(200, objectResult.StatusCode);
            var response = Assert.IsType<EditPivotResponse>(objectResult.Value);
            Assert.Equal(3, response.Id);
        }

        [Fact]
        public async Task Update_InvalidModelState_ReturnsBadRequest()
        {
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateControllerWithClaim(notifier);
            controller.ModelState.AddModelError("Name", "Required");

            var result = await controller.Update(mediator.Object, new EditPivotRequest(), CancellationToken.None);

            var badRequest = Assert.IsType<ObjectResult>(result);
            Assert.Equal(400, badRequest.StatusCode);
        }

        [Fact]
        public async Task GetForecast_Sets_UserId_And_Returns_Response()
        {
            var notifier = new Mock<INotifier>();
            var mediator = new Mock<IMediator>();
            var controller = CreateControllerWithClaim(notifier.Object, "11");

            var expected = new GetPivotForecastResponse
            {
                PivotId = 5,
                Days = 7,
                HasCoordinates = true
            };
            mediator
                .Setup(m => m.Send(It.Is<GetPivotForecastRequest>(c => c.UserId == 11 && c.PivotId == 5), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var result = await controller.GetForecast(
                mediator.Object,
                new GetPivotForecastRequest { PivotId = 5, Days = 7 },
                CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(200, objectResult.StatusCode);
            var response = Assert.IsType<GetPivotForecastResponse>(objectResult.Value);
            Assert.Equal(5, response.PivotId);
            Assert.True(response.HasCoordinates);
        }

        [Fact]
        public async Task GetForecast_InvalidModelState_ReturnsBadRequest()
        {
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateControllerWithClaim(notifier);
            controller.ModelState.AddModelError("PivotId", "Required");

            var result = await controller.GetForecast(
                mediator.Object,
                new GetPivotForecastRequest(),
                CancellationToken.None);

            var badRequest = Assert.IsType<ObjectResult>(result);
            Assert.Equal(400, badRequest.StatusCode);
        }

        [Fact]
        public async Task Delete_Valid_ReturnsResponse()
        {
            var notifier = new Mock<INotifier>();
            var mediator = new Mock<IMediator>();
            var controller = CreateControllerWithClaim(notifier.Object);

            var expected = new DeletePivotResponse();
            mediator.Setup(m => m.Send(It.IsAny<DeletePivotRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var result = await controller.Delete(mediator.Object, new DeletePivotRequest { Id = 3 }, CancellationToken.None);

            Assert.IsType<DeletePivotResponse>(result.Value);
        }

        [Fact]
        public async Task Delete_InvalidModelState_ReturnsBadRequest()
        {
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateControllerWithClaim(notifier);
            controller.ModelState.AddModelError("Id", "Required");

            var result = await controller.Delete(mediator.Object, new DeletePivotRequest(), CancellationToken.None);

            var badRequest = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(400, badRequest.StatusCode);
        }

        [Fact]
        public async Task UpdateLimits_Valid_ReturnsResponse()
        {
            var notifier = new Mock<INotifier>();
            var mediator = new Mock<IMediator>();
            var controller = CreateControllerWithClaim(notifier.Object);

            var expected = new EditPivotResponse { Id = 7 };
            mediator.Setup(m => m.Send(It.IsAny<EditPivotLimitsRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var result = await controller.UpdateLimits(mediator.Object, new EditPivotLimitsRequest { Id = 7 }, CancellationToken.None);

            var response = Assert.IsType<EditPivotResponse>(result.Value);
            Assert.Equal(7, response.Id);
        }

        [Fact]
        public async Task UpdateLimits_InvalidModelState_ReturnsBadRequest()
        {
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateControllerWithClaim(notifier);
            controller.ModelState.AddModelError("Id", "Required");

            var result = await controller.UpdateLimits(mediator.Object, new EditPivotLimitsRequest(), CancellationToken.None);

            var badRequest = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(400, badRequest.StatusCode);
        }

        [Fact]
        public async Task GetIrrigationTrend_Sets_UserId_And_Returns_Response()
        {
            var notifier = new Mock<INotifier>();
            var mediator = new Mock<IMediator>();
            var controller = CreateControllerWithClaim(notifier.Object, "5");

            var expected = new IrrigationTrendResponse { PivotId = 3 };
            mediator
                .Setup(m => m.Send(It.Is<GetIrrigationTrendRequest>(c => c.UserId == 5 && c.PivotId == 3), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var result = await controller.GetIrrigationTrend(
                mediator.Object,
                new GetIrrigationTrendRequest { PivotId = 3 },
                CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(200, objectResult.StatusCode);
            Assert.Same(expected, objectResult.Value);
        }

        [Fact]
        public async Task GetAnomalies_BuildsRequest_WithRoutePivotId()
        {
            var notifier = new Mock<INotifier>();
            var mediator = new Mock<IMediator>();
            var controller = CreateControllerWithClaim(notifier.Object, "8");

            mediator
                .Setup(m => m.Send(It.IsAny<GetPivotAnomaliesRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SensorAnomalyResponse>());

            var result = await controller.GetAnomalies(mediator.Object, 12, acknowledged: true, pageSize: 10, pageIndex: 1, CancellationToken.None);

            mediator.Verify(m => m.Send(
                It.Is<GetPivotAnomaliesRequest>(r =>
                    r.PivotId == 12 &&
                    r.UserId == 8 &&
                    r.AcknowledgedOnly == true &&
                    r.PageSize == 10 &&
                    r.PageIndex == 1),
                It.IsAny<CancellationToken>()), Times.Once);
            Assert.IsType<ObjectResult>(result);
        }

        [Fact]
        public async Task GetAIInsights_WhenMediatorReturnsNull_ReturnsServiceUnavailable()
        {
            var notifier = new Mock<INotifier>();
            var mediator = new Mock<IMediator>();
            var controller = CreateControllerWithClaim(notifier.Object);

            mediator
                .Setup(m => m.Send(It.IsAny<GetPivotAIInsightsRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((PivotAIInsightsResponse?)null);

            var result = await controller.GetAIInsights(mediator.Object, 4, CancellationToken.None);

            var statusResult = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(503, statusResult.StatusCode);
        }

        [Fact]
        public async Task GetAIInsights_WhenMediatorReturnsInsights_ReturnsOk()
        {
            var notifier = new Mock<INotifier>();
            var mediator = new Mock<IMediator>();
            var controller = CreateControllerWithClaim(notifier.Object, "2");

            var expected = new PivotAIInsightsResponse { Insights = "ok", GeneratedAt = DateTime.UtcNow };
            mediator
                .Setup(m => m.Send(It.Is<GetPivotAIInsightsRequest>(r => r.PivotId == 4 && r.UserId == 2), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var result = await controller.GetAIInsights(mediator.Object, 4, CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(200, objectResult.StatusCode);
            Assert.Same(expected, objectResult.Value);
        }

        [Fact]
        public async Task GetMoisturePrediction_Sets_UserId_And_Returns_Response()
        {
            var notifier = new Mock<INotifier>();
            var mediator = new Mock<IMediator>();
            var controller = CreateControllerWithClaim(notifier.Object, "6");

            var expected = new MoisturePredictionResponse { PivotId = 9 };
            mediator
                .Setup(m => m.Send(It.Is<GetMoisturePredictionRequest>(r => r.PivotId == 9 && r.UserId == 6), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var result = await controller.GetMoisturePrediction(mediator.Object, 9, CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(200, objectResult.StatusCode);
            Assert.Same(expected, objectResult.Value);
        }
    }
}
