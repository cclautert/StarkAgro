using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using MediatR;

namespace AgripeWebAPI.Domain.Handlers.Pivots
{
    public class CreatePivotHandler : IRequestHandler<CreatePivotRequest, CreatePivotResponse>
    {
        private readonly agpDBContext _dbContext;

        public CreatePivotHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<CreatePivotResponse> Handle(CreatePivotRequest request, CancellationToken cancellationToken)
        {
            var pivot = _dbContext.Pivots.Add(new Pivot { Name = request.Name });
            _dbContext.SaveChanges();

            return Task.FromResult(new CreatePivotResponse  { Id = pivot.Entity.Id });
        }
    }
}
