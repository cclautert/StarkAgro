using AgripeWebAPI.Domain.Commands.Requests.Sensor;
using AgripeWebAPI.Domain.Commands.Responses.Sensor;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgripeWebAPI.Domain.Handlers.Sensor
{
    public class GetReadHandler : IRequestHandler<GetListReadRequest, IList<GetReadResponse>>
    {
        private readonly agpDBContext _dbContext;

        // Constructor updated to inject IReadSensor dependency
        public GetReadHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<IList<GetReadResponse>> Handle(GetListReadRequest request, CancellationToken cancellationToken)
        {           
            return await _dbContext.ReadSensors.Where(x => x.Sensor.UserId == request.UserId).Select(x => new GetReadResponse
                {
                    Id = x.Id,
                    SensorId = x.SensorId,
                    Value = x.Value
                }).ToListAsync();
        }
    }
}
