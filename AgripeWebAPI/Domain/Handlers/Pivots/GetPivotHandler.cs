using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgripeWebAPI.Domain.Handlers.Pivots
{
    public class GetPivotHandler : IRequestHandler<GetPivotRequest, GetPivotResponse>
    {
        private readonly agpDBContext _dbContext;

        public GetPivotHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<GetPivotResponse?> Handle(GetPivotRequest request, CancellationToken cancellationToken)
        {
            return await _dbContext.Pivots
                .Where(x => x.Id == request.Id)
                .Select(x => new GetPivotResponse
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
