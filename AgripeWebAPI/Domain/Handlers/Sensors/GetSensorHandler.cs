using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using AgripeWebAPI.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgripeWebAPI.Domain.Handlers.Sensors
{
    public class GetSensorHandler : IRequestHandler<GetSensorRequest, GetSensorResponse>
    {
        private readonly agpDBContext _dbContext;

        public GetSensorHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<GetSensorResponse?> Handle(GetSensorRequest request, CancellationToken cancellationToken)
        {
            return await _dbContext.Sensors
                .Where(x => x.Id == request.Id)
                .Select(x => new GetSensorResponse
                {
                    Id = x.Id,
                    Name = x.Name,
                    Code = x.Code,
                    Pivot = x.Pivot,
                    Quadrante = x.Quadrante
                })
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
