using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Reads;
using AgripeWebAPI.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
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

            IQueryable<GetAllReadByPivotIdResponse> query = _dbContext.ReadSensors
                .Where(x => x.SensorId == request.SensorId && x.Sensor.Quadrante == request.Quadrante && x.Date >= startDate)
                .OrderByDescending(x => x.Date)
                .Select(x => new GetAllReadByPivotIdResponse
                {
                    Id = x.Id,
                    SensorId = x.SensorId,
                    Value = x.Value,
                    Date = x.Date
                });

            return query.AsAsyncEnumerable();
        }
    }
}
