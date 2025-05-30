using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgripeWebAPI.Domain.Handlers.Pivots
{
    public class GetListPivotHandler : IRequestHandler<GetListPivotByUserIdRequest, IList<GetPivotResponse>>
    {
        private readonly agpDBContext _dbContext;

        public GetListPivotHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<IList<GetPivotResponse>> Handle(GetListPivotByUserIdRequest request, CancellationToken cancellationToken)
        {
            return await _dbContext.Pivots
                .Where(x => x.UserId == request.UserId)
                .Select(x => new GetPivotResponse
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToListAsync(cancellationToken);
        }
    }
}
