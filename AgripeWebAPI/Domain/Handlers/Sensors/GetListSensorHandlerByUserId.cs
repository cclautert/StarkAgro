using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using AgripeWebAPI.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgripeWebAPI.Domain.Handlers.Sensors
{
    public class GetListSensorByUserIdHandler : IRequestHandler<GetListSensorRequest, IList<GetSensorResponse>>
    {
        private readonly agpDBContext _dbContext;

        public GetListSensorByUserIdHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }
        
        public async Task<IList<GetSensorResponse>> Handle(GetListSensorRequest request, CancellationToken cancellationToken)
        {
            return await _dbContext.Sensors
                .Where(x => x.PivoId == request.PivotId)
                .Select(x => new GetSensorResponse
                {
                    Id = x.Id,
                    Code = x.Code,
                    Pivot = x.Pivot,
                    Quadrante = x.Quadrante
                }).ToListAsync(cancellationToken);
        }
    }
}
