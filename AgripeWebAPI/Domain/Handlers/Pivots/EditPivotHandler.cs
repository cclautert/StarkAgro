using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Models;
using MediatR;

namespace AgripeWebAPI.Domain.Handlers.Pivots
{
    public class EditPivotHandler : IRequestHandler<EditPivotRequest, EditPivotResponse>
    {
        private readonly agpDBContext _dbContext;
        public EditPivotHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext;
        }
        public Task<EditPivotResponse> Handle(EditPivotRequest request, CancellationToken cancellationToken)
        {
            var pivot = _dbContext.Pivots.FirstOrDefault(p => p.Name == request.Name);
            if (pivot == null)
            {
                throw new KeyNotFoundException("Pivot not found");
            }
            pivot.Name = request.Name;
            _dbContext.SaveChanges();
            return Task.FromResult(new EditPivotResponse { Id = pivot.Id });
        }
    }
}
