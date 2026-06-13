using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Responses.Reads;
using AgripeWebAPI.Models;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Sensors
{
    public class GetListReadBySensorIdHandler : IRequestHandler<GetAllListReadBySensorIdRequest, IAsyncEnumerable<GetAllReadBySensorIdResponse>>
    {
        private readonly agpDBContext _dbContext;

        public GetListReadBySensorIdHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<IAsyncEnumerable<GetAllReadBySensorIdResponse>> Handle(GetAllListReadBySensorIdRequest request, CancellationToken cancellationToken)
        {
            var startDate = DateTime.UtcNow.AddDays(-request.NumberOfReads);

            var reads = await _dbContext.ReadSensors
                .Find(x => x.SensorId == request.SensorId && x.Date >= startDate)
                .SortBy(x => x.Date)
                .Project(x => new GetAllReadBySensorIdResponse
                {
                    Id = x.Id,
                    SensorId = x.SensorId,
                    Value = x.Value,
                    Date = x.Date,
                    Humidity = x.Humidity,
                    Temperature = x.Temperature,
                    BatteryVoltage = x.BatteryVoltage
                })
                .ToListAsync(cancellationToken);

            return ToAsyncEnumerable(reads, cancellationToken);
        }

        private static async IAsyncEnumerable<GetAllReadBySensorIdResponse> ToAsyncEnumerable(
            IEnumerable<GetAllReadBySensorIdResponse> reads,
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
