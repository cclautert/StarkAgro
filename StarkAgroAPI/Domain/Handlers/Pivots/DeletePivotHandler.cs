using StarkAgroAPI.Domain.Commands.Requests.Pivots;
using StarkAgroAPI.Domain.Commands.Responses.Pivots;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Interfaces;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Pivots
{
    public class DeletePivotHandler : IRequestHandler<DeletePivotRequest, DeletePivotResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;

        public DeletePivotHandler(agpDBContext dbContext, ICurrentUserContext currentUser)
        {
            _dbContext = dbContext;
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<DeletePivotResponse> Handle(DeletePivotRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user required.");

            var deletePivotResult = await _dbContext.Pivots.DeleteOneAsync(
                p => p.Id == request.Id && p.UserId == userId,
                cancellationToken);

            if (deletePivotResult.DeletedCount == 0)
            {
                throw new KeyNotFoundException("Pivot not found");
            }

            await _dbContext.Sensors.DeleteManyAsync(s => s.PivoId == request.Id, cancellationToken);
            return new DeletePivotResponse();
        }
    }
}
