using AgripeWebAPI.Domain.Handlers.Sensors;

namespace AgripeWebAPI.Tests.Domain.Handlers.Reads
{
    public class GetReadByPivotIdHandlerTests
    {
        [Fact]
        public void Constructor_NullDbContext_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new GetReadByPivotIdHandler(null!));
        }
    }
}
