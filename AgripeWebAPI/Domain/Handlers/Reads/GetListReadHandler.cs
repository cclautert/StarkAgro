using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Responses.Reads;
using AgripeWebAPI.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace AgripeWebAPI.Domain.Handlers.Sensors
{
    public class GetListReadHandler : IRequestHandler<GetListReadRequest, IList<GetReadResponse>>
    {
        private readonly agpDBContext _dbContext;

        // Constructor updated to inject IReadSensor dependency
        public GetListReadHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<IList<GetReadResponse>> Handle(GetListReadRequest request, CancellationToken cancellationToken)
        {
            var startDate = DateTime.UtcNow.AddDays(-request.NumberOfReads);

            return await _dbContext.ReadSensors
                .Where(x => x.UserId == request.UserId && x.Date >= startDate)
                .OrderByDescending(x => x.Date)
                .Select(x => new GetReadResponse
                {
                    Id = x.Id,
                    SensorId = x.SensorId,
                    Value = x.Value,
                    Date = x.Date
                })
                .ToListAsync();

        }
    }
}
