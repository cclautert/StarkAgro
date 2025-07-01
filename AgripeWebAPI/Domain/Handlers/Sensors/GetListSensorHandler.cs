using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using AgripeWebAPI.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgripeWebAPI.Domain.Handlers.Sensors
{
    public class GetListSensorHandler : IRequestHandler<GetListSensorByUserIdRequest, IList<GetSensorResponse>>
    {
        private readonly agpDBContext _dbContext;

        public GetListSensorHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }
        
        public async Task<IList<GetSensorResponse>> Handle(GetListSensorByUserIdRequest request, CancellationToken cancellationToken)
        {
            return await _dbContext.Sensors
                .Where(x => x.UserId == request.UserId)
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
