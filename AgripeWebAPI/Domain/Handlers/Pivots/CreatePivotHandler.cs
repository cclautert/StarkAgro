using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Pivots
{
    public class CreatePivotHandler : IRequestHandler<CreatePivotRequest, CreatePivotResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;

        public CreatePivotHandler(agpDBContext dbContext, ICurrentUserContext currentUser)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<CreatePivotResponse> Handle(CreatePivotRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required to create a pivot.");

            var pivot = new Pivot
            {
                Id = await _dbContext.GetNextIdAsync(nameof(Pivot), cancellationToken),
                Name = request.Name,
                UserId = userId
            };

            await _dbContext.Pivots.InsertOneAsync(pivot, cancellationToken: cancellationToken);

            return new CreatePivotResponse
            {
                Id = pivot.Id
            };

        }
    }
}
