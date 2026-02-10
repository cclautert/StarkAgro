using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Models;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Pivots
{
    public class EditPivotHandler : IRequestHandler<EditPivotRequest, EditPivotResponse>
    {
        private readonly agpDBContext _dbContext;
        public EditPivotHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<EditPivotResponse> Handle(EditPivotRequest request, CancellationToken cancellationToken)
        {
            var pivot = await _dbContext.Pivots.Find(p => p.Id == request.Id).FirstOrDefaultAsync(cancellationToken);
            if (pivot == null)
            {
                throw new KeyNotFoundException("Pivot not found");
            }
            pivot.Name = request.Name;
            await _dbContext.Pivots.ReplaceOneAsync(x => x.Id == pivot.Id, pivot, cancellationToken: cancellationToken);
            return new EditPivotResponse { Id = pivot.Id };
        }
    }
}
