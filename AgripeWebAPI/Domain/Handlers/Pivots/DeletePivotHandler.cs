using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Models;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Pivots
{
    public class DeletePivotHandler : IRequestHandler<DeletePivotRequest, DeletePivotResponse>
    {
        private readonly agpDBContext _dbContext;
        public DeletePivotHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<DeletePivotResponse> Handle(DeletePivotRequest request, CancellationToken cancellationToken)
        {
            var deletePivotResult = await _dbContext.Pivots.DeleteOneAsync(p => p.Id == request.Id, cancellationToken);
            if (deletePivotResult.DeletedCount == 0)
            {
                throw new KeyNotFoundException("Pivot not found");
            }

            await _dbContext.Sensors.DeleteManyAsync(s => s.PivoId == request.Id, cancellationToken);
            return new DeletePivotResponse();
        }
    }
}
