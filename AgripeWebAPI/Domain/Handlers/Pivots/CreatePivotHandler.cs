using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Pivots
{
    public class CreatePivotHandler : IRequestHandler<CreatePivotRequest, CreatePivotResponse>
    {
        private readonly agpDBContext _dbContext;

        public CreatePivotHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<CreatePivotResponse> Handle(CreatePivotRequest request, CancellationToken cancellationToken)
        {
            if (!request.UserId.HasValue)
            {
                throw new ArgumentNullException(nameof(request.UserId), "UserId cannot be null.");
            }

            var pivot = new Pivot
            {
                Id = await _dbContext.GetNextIdAsync(nameof(Pivot), cancellationToken),
                Name = request.Name,
                UserId = request.UserId.Value
            };

            await _dbContext.Pivots.InsertOneAsync(pivot, cancellationToken: cancellationToken);

            return new CreatePivotResponse
            {
                Id = pivot.Id
            };

        }
    }
}
