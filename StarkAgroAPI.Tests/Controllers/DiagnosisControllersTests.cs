using StarkAgroAPI.Controllers;
using StarkAgroAPI.Domain.Commands.Requests.Agronomist;
using StarkAgroAPI.Domain.Commands.Requests.Diagnosis;
using StarkAgroAPI.Domain.Commands.Responses.Agronomist;
using StarkAgroAPI.Domain.Commands.Responses.Diagnosis;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
using System.Text;

namespace StarkAgroAPI.Tests.Controllers
{
    public class PlantDiagnosisControllerTests
    {
        private static PlantDiagnosisController Controller(INotifier notifier, string userId = "3")
            => new(notifier)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("id", userId)]))
                    }
                }
            };

        private static IFormFile Photo(byte[]? content = null, string contentType = "image/jpeg")
        {
            var bytes = content ?? [0xFF, 0xD8, 0xFF, 0xE0];
            var stream = new MemoryStream(bytes);

            return new FormFile(stream, 0, bytes.Length, "image", "folha.jpg")
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }

        [Fact]
        public async Task Create_ValidPhoto_Returns202()
        {
            // 202: a análise é assíncrona. Segurar a request esperando a IA custaria 10–30s.
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<CreatePlantDiagnosisRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CreatePlantDiagnosisResponse { Id = 1, Status = "Uploaded" });

            var controller = Controller(new Notificator());

            var result = await controller.Create(
                mediator.Object, Photo(), pivotId: 1, cropName: "tomate", notes: null,
                latitude: null, longitude: null, CancellationToken.None);

            var response = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status202Accepted, response.StatusCode);
        }

        [Fact]
        public async Task Create_WithoutPhoto_IsRefused()
        {
            var notifier = new Notificator();
            var controller = Controller(notifier);

            var result = await controller.Create(
                new Mock<IMediator>().Object, image: null, pivotId: null, cropName: null, notes: null,
                latitude: null, longitude: null, CancellationToken.None);

            Assert.IsType<ObjectResult>(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Create_PhotoTooLarge_IsRefusedBeforeReachingTheHandler()
        {
            var mediator = new Mock<IMediator>();
            var notifier = new Notificator();
            var controller = Controller(notifier);

            var huge = new byte[13 * 1024 * 1024];   // acima do limite de 12 MB
            huge[0] = 0xFF; huge[1] = 0xD8; huge[2] = 0xFF;

            await controller.Create(
                mediator.Object, Photo(huge), pivotId: null, cropName: null, notes: null,
                latitude: null, longitude: null, CancellationToken.None);

            Assert.True(notifier.HasNotification());
            mediator.Verify(m => m.Send(
                It.IsAny<CreatePlantDiagnosisRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetById_NotFound_Returns404()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetPlantDiagnosisByIdRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((PlantDiagnosisResponse?)null);

            var controller = Controller(new Notificator());

            var result = await controller.GetById(mediator.Object, 99, CancellationToken.None);

            // CustomResponse com resultado nulo devolve só o status.
            var response = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetAll_ReturnsTheList()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetPlantDiagnosisListRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([new PlantDiagnosisSummaryResponse { Id = 1 }]);

            var controller = Controller(new Notificator());

            var result = await controller.GetAll(mediator.Object, null, 20, 0, CancellationToken.None);

            Assert.IsType<ObjectResult>(result);
        }

        [Fact]
        public async Task GetStatus_ReturnsTheStatus()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetPlantDiagnosisStatusRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PlantDiagnosisStatusResponse { Id = 1, Status = "Processing" });

            var controller = Controller(new Notificator());

            var result = await controller.GetStatus(mediator.Object, 1, CancellationToken.None);

            var response = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        }

        [Fact]
        public async Task GetImage_ReturnsTheFileWithPrivateCache()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetPlantDiagnosisImageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PlantDiagnosisImageResponse { Content = [1, 2, 3], ContentType = "image/jpeg" });

            var controller = Controller(new Notificator());

            var result = await controller.GetImage(mediator.Object, 1, CancellationToken.None);

            var file = Assert.IsType<FileContentResult>(result);
            Assert.Equal("image/jpeg", file.ContentType);
            Assert.Contains("private", controller.Response.Headers.CacheControl.ToString());
        }

        [Fact]
        public async Task GetImage_NotFound_Returns404()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetPlantDiagnosisImageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((PlantDiagnosisImageResponse?)null);

            var controller = Controller(new Notificator());

            Assert.IsType<NotFoundResult>(
                await controller.GetImage(mediator.Object, 99, CancellationToken.None));
        }

        [Fact]
        public async Task GetPdf_ReturnsTheDocument()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetDiagnosisPdfRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DiagnosisPdfResponse
                {
                    Content = Encoding.ASCII.GetBytes("%PDF"),
                    FileName = "laudo-1.pdf"
                });

            var controller = Controller(new Notificator());

            var result = await controller.GetPdf(mediator.Object, 1, CancellationToken.None);

            var file = Assert.IsType<FileContentResult>(result);
            Assert.Equal("application/pdf", file.ContentType);
            Assert.Equal("laudo-1.pdf", file.FileDownloadName);
        }

        [Fact]
        public async Task GetPdf_NotFound_Returns404()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetDiagnosisPdfRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DiagnosisPdfResponse?)null);

            var controller = Controller(new Notificator());

            Assert.IsType<NotFoundResult>(
                await controller.GetPdf(mediator.Object, 99, CancellationToken.None));
        }

        [Fact]
        public async Task Reprocess_Returns202()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<ReprocessDiagnosisRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var controller = Controller(new Notificator());

            var result = await controller.Reprocess(mediator.Object, 1, CancellationToken.None);

            var response = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(StatusCodes.Status202Accepted, response.StatusCode);
        }

        [Fact]
        public async Task GetHistory_ReturnsTheTimeline()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetDiagnosisHistoryRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DiagnosisHistoryResponse { PivotId = 1, Trend = "piorou" });

            var controller = Controller(new Notificator());

            Assert.IsType<ObjectResult>(
                await controller.GetHistory(mediator.Object, 1, CancellationToken.None));
        }

        [Fact]
        public async Task GetAudit_NotFound_Returns404()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetDiagnosisAuditRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((List<DiagnosisAuditEntryResponse>?)null);

            var controller = Controller(new Notificator());

            var response = Assert.IsType<StatusCodeResult>(
                await controller.GetAudit(mediator.Object, 99, CancellationToken.None));

            Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Delete_Returns204()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<DeletePlantDiagnosisRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var controller = Controller(new Notificator());

            var response = Assert.IsType<StatusCodeResult>(
                await controller.Delete(mediator.Object, 1, CancellationToken.None));

            Assert.Equal(StatusCodes.Status204NoContent, response.StatusCode);
        }
    }

    public class AgronomistControllerTests
    {
        private static AgronomistController Controller(INotifier notifier, string userId = "4")
            => new(notifier)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim("id", userId),
                            new Claim("isAgronomist", "true")
                        ]))
                    }
                }
            };

        [Fact]
        public async Task GetQueue_ReturnsTheQueue()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetAgronomistQueueRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([new AgronomistQueueItemResponse { Id = 1, ClientName = "Produtor" }]);

            var controller = Controller(new Notificator());

            Assert.IsType<ObjectResult>(
                await controller.GetQueue(mediator.Object, null, 20, 0, CancellationToken.None));
        }

        [Fact]
        public async Task GetDiagnosis_NotFound_Returns404()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetAgronomistDiagnosisRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((PlantDiagnosisResponse?)null);

            var controller = Controller(new Notificator());

            var response = Assert.IsType<StatusCodeResult>(
                await controller.GetDiagnosis(mediator.Object, 99, CancellationToken.None));

            Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetDiagnosisImage_NotFound_Returns404()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetAgronomistDiagnosisImageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((PlantDiagnosisImageResponse?)null);

            var controller = Controller(new Notificator());

            Assert.IsType<NotFoundResult>(
                await controller.GetDiagnosisImage(mediator.Object, 99, CancellationToken.None));
        }

        [Fact]
        public async Task GetDiagnosisPdf_ReturnsTheDocument()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetDiagnosisPdfRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DiagnosisPdfResponse
                {
                    Content = Encoding.ASCII.GetBytes("%PDF"),
                    FileName = "laudo-1.pdf"
                });

            var controller = Controller(new Notificator());

            var file = Assert.IsType<FileContentResult>(
                await controller.GetDiagnosisPdf(mediator.Object, 1, CancellationToken.None));

            Assert.Equal("application/pdf", file.ContentType);
        }

        [Fact]
        public async Task Claim_Failure_Returns400()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<ClaimDiagnosisRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var controller = Controller(new Notificator());

            var response = Assert.IsType<StatusCodeResult>(
                await controller.Claim(mediator.Object, 1, CancellationToken.None));

            Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Claim_Success_Returns200()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<ClaimDiagnosisRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var controller = Controller(new Notificator());

            var response = Assert.IsType<StatusCodeResult>(
                await controller.Claim(mediator.Object, 1, CancellationToken.None));

            Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        }

        [Fact]
        public async Task Sign_SetsTheIdFromTheRoute()
        {
            SignDiagnosisRequest? captured = null;

            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<SignDiagnosisRequest>(), It.IsAny<CancellationToken>()))
                .Callback<IRequest<bool>, CancellationToken>((r, _) => captured = (SignDiagnosisRequest)r)
                .ReturnsAsync(true);

            var controller = Controller(new Notificator());

            await controller.Sign(
                mediator.Object, 42, new SignDiagnosisRequest { ReportMarkdown = "laudo" }, CancellationToken.None);

            Assert.Equal(42, captured!.Id);
        }

        [Fact]
        public async Task Review_SetsTheIdFromTheRoute()
        {
            ReviewDiagnosisRequest? captured = null;

            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<ReviewDiagnosisRequest>(), It.IsAny<CancellationToken>()))
                .Callback<IRequest<bool>, CancellationToken>((r, _) => captured = (ReviewDiagnosisRequest)r)
                .ReturnsAsync(true);

            var controller = Controller(new Notificator());

            await controller.Review(
                mediator.Object, 7, new ReviewDiagnosisRequest { ReportMarkdown = "rascunho" }, CancellationToken.None);

            Assert.Equal(7, captured!.Id);
        }

        [Fact]
        public async Task Reject_SetsTheIdFromTheRoute()
        {
            RejectDiagnosisRequest? captured = null;

            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<RejectDiagnosisRequest>(), It.IsAny<CancellationToken>()))
                .Callback<IRequest<bool>, CancellationToken>((r, _) => captured = (RejectDiagnosisRequest)r)
                .ReturnsAsync(true);

            var controller = Controller(new Notificator());

            await controller.Reject(
                mediator.Object, 5, new RejectDiagnosisRequest { Reason = "foto ruim" }, CancellationToken.None);

            Assert.Equal(5, captured!.Id);
        }

        [Fact]
        public async Task GetClients_ReturnsTheWallet()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetAgronomistClientsRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([new AgronomistClientResponse { Id = 1, ClientEmail = "p@t.com" }]);

            var controller = Controller(new Notificator());

            Assert.IsType<ObjectResult>(
                await controller.GetClients(mediator.Object, CancellationToken.None));
        }

        [Fact]
        public async Task InviteClient_Returns201()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<InviteClientRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgronomistClientResponse { Id = 1, ClientEmail = "p@t.com" });

            var controller = Controller(new Notificator());

            var response = Assert.IsType<ObjectResult>(await controller.InviteClient(
                mediator.Object, new InviteClientRequest { ClientEmail = "p@t.com" }, CancellationToken.None));

            Assert.Equal(StatusCodes.Status201Created, response.StatusCode);
        }

        [Fact]
        public async Task RevokeClient_Returns204()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<RevokeClientRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var controller = Controller(new Notificator());

            var response = Assert.IsType<StatusCodeResult>(
                await controller.RevokeClient(mediator.Object, 1, CancellationToken.None));

            Assert.Equal(StatusCodes.Status204NoContent, response.StatusCode);
        }

        [Fact]
        public async Task RevokeClient_NotFound_Returns404()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<RevokeClientRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var controller = Controller(new Notificator());

            var response = Assert.IsType<StatusCodeResult>(
                await controller.RevokeClient(mediator.Object, 99, CancellationToken.None));

            Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        }
    }
}
