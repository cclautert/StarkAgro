using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Responses.Reads;
using AgripeWebAPI.Domain.Handlers.Sensors;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Reads
{
    public class GetListReadHandlerTests
    {
        [Fact]
        public async Task Handle_Returns_Reads_For_User()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockReadSensors = new Mock<IMongoCollection<ReadSensor>>();
            var mockCurrentUser = new Mock<ICurrentUserContext>();

            mockCurrentUser.Setup(u => u.UserId).Returns(10);
            var projected = new List<GetReadResponse>
            {
                new GetReadResponse { Id = 100, SensorId = 1, Value = 12.5m },
                new GetReadResponse { Id = 101, SensorId = 1, Value = 15.0m }
            };
            MongoMockHelper.SetupFindProjection<ReadSensor, GetReadResponse>(mockReadSensors, projected);
            mockDbContext.Setup(c => c.ReadSensors).Returns(mockReadSensors.Object);

            var handler = new GetListReadHandler(mockDbContext.Object, mockCurrentUser.Object);
            var request = new GetListReadRequest { NumberOfReads = 10 };

            var result = await handler.Handle(request, CancellationToken.None);

            Assert.NotNull(result);
            var list = new List<GetReadResponse>();
            await foreach (var item in result) list.Add(item);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task Handle_Returns_EmptyList_When_No_Reads()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockReadSensors = new Mock<IMongoCollection<ReadSensor>>();
            var mockCurrentUser = new Mock<ICurrentUserContext>();

            mockCurrentUser.Setup(u => u.UserId).Returns(99);
            MongoMockHelper.SetupFindProjection<ReadSensor, GetReadResponse>(mockReadSensors, new List<GetReadResponse>());
            mockDbContext.Setup(c => c.ReadSensors).Returns(mockReadSensors.Object);

            var handler = new GetListReadHandler(mockDbContext.Object, mockCurrentUser.Object);
            var request = new GetListReadRequest { NumberOfReads = 10 };

            var result = await handler.Handle(request, CancellationToken.None);

            Assert.NotNull(result);
            var list = new List<GetReadResponse>();
            await foreach (var item in result) list.Add(item);
            Assert.Empty(list);
        }
    }
}
