using AgripeWebWorker.Services;

namespace AgripeWebWorker.Tests.Services
{
    public class WorkerUserContextTests
    {
        [Fact]
        public void UserId_ShouldReturn_Null()
        {
            var context = new WorkerUserContext();
            Assert.Null(context.UserId);
        }

        [Fact]
        public void IsAuthenticated_ShouldReturn_False()
        {
            var context = new WorkerUserContext();
            Assert.False(context.IsAuthenticated);
        }
    }
}
