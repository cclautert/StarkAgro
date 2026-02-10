using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Reads;
using AgripeWebAPI.Models;
using MediatR;
using MongoDB.Driver;
using System.Linq;

namespace AgripeWebAPI.Domain.Handlers.Sensors
{
    public class GetListReadHandler : IRequestHandler<GetListReadRequest, IAsyncEnumerable<GetReadResponse>>
    {
        private readonly agpDBContext _dbContext;

        // Constructor updated to inject IReadSensor dependency
        public GetListReadHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<IAsyncEnumerable<GetReadResponse>> Handle(GetListReadRequest request, CancellationToken cancellationToken)
        {
            var startDate = DateTime.UtcNow.AddDays(-request.NumberOfReads);

            var reads = await _dbContext.ReadSensors
                .Find(x => x.UserId == request.UserId && x.Date >= startDate)
                .SortBy(x => x.Date)
                .Project(x => new GetReadResponse
                {
                    Id = x.Id,
                    SensorId = x.SensorId,
                    Value = x.Value,
                    Date = x.Date
                })
                .ToListAsync(cancellationToken);

            return ToAsyncEnumerable(reads, cancellationToken);
        }

        private static async IAsyncEnumerable<GetReadResponse> ToAsyncEnumerable(
            IEnumerable<GetReadResponse> reads,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var read in reads)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return read;
                await Task.CompletedTask;
            }
        }
    }
}
