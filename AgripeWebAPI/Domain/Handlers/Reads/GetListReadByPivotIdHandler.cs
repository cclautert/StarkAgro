using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Reads;
using AgripeWebAPI.Models;
using MediatR;
using MongoDB.Driver;
using System.Linq;

namespace AgripeWebAPI.Domain.Handlers.Sensors
{
    public class GetListReadByPivotIdHandler : IRequestHandler<GetAllListReadByPivotIdRequest, IAsyncEnumerable<GetAllReadByPivotIdResponse>>
    {
        private readonly agpDBContext _dbContext;

        // Constructor updated to inject IReadSensor dependency
        public GetListReadByPivotIdHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<IAsyncEnumerable<GetAllReadByPivotIdResponse>> Handle(GetAllListReadByPivotIdRequest request, CancellationToken cancellationToken)
        {
            var startDate = DateTime.UtcNow.AddDays(-request.NumberOfReads);
            var sensor = await _dbContext.Sensors
                .Find(x => x.Id == request.SensorId && x.Quadrante == request.Quadrante)
                .FirstOrDefaultAsync(cancellationToken);

            if (sensor == null)
            {
                return ToAsyncEnumerable(Array.Empty<GetAllReadByPivotIdResponse>(), cancellationToken);
            }

            var reads = await _dbContext.ReadSensors
                .Find(x => x.SensorId == request.SensorId && x.Date >= startDate)
                .SortBy(x => x.Date)
                .Project(x => new GetAllReadByPivotIdResponse
                {
                    Id = x.Id,
                    SensorId = x.SensorId,
                    Value = x.Value,
                    Date = x.Date
                })
                .ToListAsync(cancellationToken);

            return ToAsyncEnumerable(reads, cancellationToken);
        }

        private static async IAsyncEnumerable<GetAllReadByPivotIdResponse> ToAsyncEnumerable(
            IEnumerable<GetAllReadByPivotIdResponse> reads,
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
