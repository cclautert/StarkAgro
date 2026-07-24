using StarkAgroAPI.Controllers;
using StarkAgroAPI.Domain.Commands.Requests.Admin;
using StarkAgroAPI.Tests.Mocks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace StarkAgroAPI.Tests.Controllers
{
    public class CulturesControllerTests
    {
        [Fact]
        public async Task List_ReturnsOk()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetCulturesRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "Milho", "Soja" });

            var result = await new CulturesController(new MockNotifier()).List(mediator.Object, default);

            var obj = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(200, obj.StatusCode);
        }
    }
}
